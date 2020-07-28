using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalConstructionTime
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class KerbalConstructionTimeData : ScenarioModule
    {
        public static Dictionary<string, string> techNameToTitle = new Dictionary<string, string>();
        public static Dictionary<string, List<string>> techNameToParents = new Dictionary<string, List<string>>();

        protected void LoadTree()
        {
            if (HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX || HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                string fullPath = KSPUtil.ApplicationRootPath + HighLogic.CurrentGame.Parameters.Career.TechTreeUrl;
                KCTDebug.Log($"Loading tech tree from {fullPath}");

                ConfigNode fileNode = ConfigNode.Load(fullPath);
                if (fileNode != null && fileNode.HasNode("TechTree"))
                {
                    techNameToTitle.Clear();

                    ConfigNode treeNode = fileNode.GetNode("TechTree");
                    ConfigNode[] ns = treeNode.GetNodes("RDNode");
                    foreach (ConfigNode n in ns)
                    {
                        if (n.HasValue("id"))
                        {
                            string techID = n.GetValue("id");

                            if (n.HasValue("title"))
                                techNameToTitle[techID] = n.GetValue("title");

                            ConfigNode[] parents = n.GetNodes("Parent");
                            var pList = new List<string>();
                            foreach (ConfigNode p in parents)
                            {
                                if (p.HasValue("parentID"))
                                    pList.Add(p.GetValue("parentID"));
                            }
                            techNameToParents[techID] = pList;
                        }
                    }
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            if (Utilities.CurrentGameIsMission()) return;

            KCTDebug.Log("Writing to persistence.");
            base.OnSave(node);
            var kctVS = new KCT_DataStorage();
            node.AddNode(kctVS.AsConfigNode());
            foreach (KSCItem KSC in KCTGameStates.KSCs)
            {
                if (KSC != null && KSC.KSCName != null && KSC.KSCName.Length > 0)
                    node.AddNode(KSC.AsConfigNode());
            }
            var tech = new ConfigNode("TechList");
            foreach (TechItem techItem in KCTGameStates.TechList)
            {
                var techNode = new KCT_TechStorageItem();
                techNode.FromTechItem(techItem);
                var cnTemp = new ConfigNode("Tech");
                cnTemp = ConfigNode.CreateConfigFromObject(techNode, cnTemp);
                var protoNode = new ConfigNode("ProtoNode");
                techItem.ProtoNode.Save(protoNode);
                cnTemp.AddNode(protoNode);
                tech.AddNode(cnTemp);
            }
            node.AddNode(tech);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            LoadTree();

            if (Utilities.CurrentGameIsMission()) return;

            KCTDebug.Log("Reading from persistence.");
            KCTGameStates.KSCs.Clear();
            KCTGameStates.ActiveKSC = null;
            KCTGameStates.InitAndClearTechList();
            KCTGameStates.TechUpgradesTotal = 0;
            KCTGameStates.SciPointsTotal = -1;

            var kctVS = new KCT_DataStorage();
            ConfigNode cn = node.GetNode(kctVS.GetType().Name);
            if (cn != null)
                ConfigNode.LoadObjectFromConfig(kctVS, cn);

            bool foundStockKSC = false;
            foreach (ConfigNode ksc in node.GetNodes("KSC"))
            {
                string name = ksc.GetValue("KSCName");
                var loaded_KSC = new KSCItem(name);
                loaded_KSC.FromConfigNode(ksc);
                if (loaded_KSC != null && loaded_KSC.KSCName != null && loaded_KSC.KSCName.Length > 0)
                {
                    loaded_KSC.RDUpgrades[1] = KCTGameStates.TechUpgradesTotal;
                    if (KCTGameStates.KSCs.Find(k => k.KSCName == loaded_KSC.KSCName) == null)
                        KCTGameStates.KSCs.Add(loaded_KSC);
                    foundStockKSC |= string.Equals(loaded_KSC.KSCName, Utilities._legacyDefaultKscId, StringComparison.OrdinalIgnoreCase);
                }
            }

            Utilities.SetActiveKSCToRSS();
            if (foundStockKSC)
                TryMigrateStockKSC();

            ConfigNode tmp = node.GetNode("TechList");
            if (tmp != null)
            {
                foreach (ConfigNode techNode in tmp.GetNodes("Tech"))
                {
                    var techStorageItem = new KCT_TechStorageItem();
                    ConfigNode.LoadObjectFromConfig(techStorageItem, techNode);
                    TechItem techItem = techStorageItem.ToTechItem();
                    techItem.ProtoNode = new ProtoTechNode(techNode.GetNode("ProtoNode"));
                    KCTGameStates.TechList.Add(techItem);
                }
            }

            KCTGameStates.ErroredDuringOnLoad.OnLoadFinish();
        }

        private void TryMigrateStockKSC()
        {
            KSCItem stockKsc = KCTGameStates.KSCs.Find(k => string.Equals(k.KSCName, Utilities._legacyDefaultKscId, StringComparison.OrdinalIgnoreCase));
            if (KCTGameStates.KSCs.Count == 1)
            {
                // Rename the stock KSC to the new default (Cape)
                stockKsc.KSCName = Utilities._defaultKscId;
                Utilities.SetActiveKSC(stockKsc.KSCName);
                return;
            }

            if (stockKsc.IsEmpty)
            {
                // Nothing provisioned into the stock KSC so it's safe to just delete it
                KCTGameStates.KSCs.Remove(stockKsc);
                Utilities.SetActiveKSCToRSS();
                return;
            }

            int numOtherUsedKSCs = KCTGameStates.KSCs.Count(k => !k.IsEmpty && k != stockKsc);
            if (numOtherUsedKSCs == 0)
            {
                string kscName = Utilities.GetActiveRSSKSC() ?? Utilities._defaultKscId;
                KSCItem newDefault = KCTGameStates.KSCs.Find(k => string.Equals(k.KSCName, kscName, StringComparison.OrdinalIgnoreCase));
                if (newDefault != null)
                {
                    // Stock KSC isn't empty but the new default one is - safe to rename the stock and remove the old default item
                    stockKsc.KSCName = newDefault.KSCName;
                    KCTGameStates.KSCs.Remove(newDefault);
                    Utilities.SetActiveKSC(stockKsc);
                    return;
                }
            }

            // Can't really do anything if there's multiple KSCs in use.
            if (!Utilities.IsKSCSwitcherInstalled)
            {
                // Need to switch back to the legacy "Stock" KSC if KSCSwitcher isn't installed
                Utilities.SetActiveKSC(stockKsc.KSCName);
            }
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
