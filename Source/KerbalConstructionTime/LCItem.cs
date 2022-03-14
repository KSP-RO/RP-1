using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class LCItem
    {
        public string Name;
        protected Guid _id;
        public Guid ID => _id;
        public List<BuildListVessel> BuildList = new List<BuildListVessel>();
        public List<BuildListVessel> Warehouse = new List<BuildListVessel>();
        public SortedList<string, BuildListVessel> Plans = new SortedList<string, BuildListVessel>();
        public List<PadConstruction> PadConstructions = new List<PadConstruction>();
        public List<int> Upgrades = new List<int>() { 0 };
        public List<ReconRollout> Recon_Rollout = new List<ReconRollout>();
        public List<AirlaunchPrep> AirlaunchPrep = new List<AirlaunchPrep>();
        public List<double> Rates = new List<double>();
        public List<double> UpRates = new List<double>();

        public bool isOperational = false;
        public bool isPad = true;

        public float massMax, massMin;
        public Vector3 sizeMax;

        public List<KCT_LaunchPad> LaunchPads = new List<KCT_LaunchPad>();
        public int ActiveLaunchPadID = 0;

        public string SupportedMassAsPrettyText => massMax == -1f ? "unlimited" : $"{massMin:N0}-{massMax:N0}t";

        public string SupportedSizeAsPrettyText => sizeMax.y == float.MaxValue ? "unlimited" : $"{sizeMax.x:N0}x{sizeMax.y:N0}m";

        private KSCItem _ksc = null;

        public KSCItem KSC
        {
            get
            {
                // Set on create, shouldn't be needed.

                //if (_ksc == null)
                //{
                //    _ksc = KCTGameStates.KSCs.Find(ksc => ksc.LaunchComplexes.Contains(this));
                //}
                return _ksc;
            }
        }

        public LCItem(KSCItem ksc)
        {
            _ksc = ksc;
        }

        public LCItem(string name, float mMax, Vector3 sMax, bool isLCPad, KSCItem ksc)
        {
            this.Name = name;

            _id = Guid.NewGuid();
            _ksc = ksc;
            isPad = isLCPad;
            massMax = mMax;
            float fracLevel;

            KCT_GUI.GetPadStats(massMax, sMax, out massMin, out _, out _, out fracLevel);

            sizeMax = sMax;

            if (isPad)
            {
                var pad = new KCT_LaunchPad(this.Name + "A", fracLevel, massMax, sizeMax);
                pad.isOperational = true;
                LaunchPads.Add(pad);
            }
        }

        public KCT_LaunchPad ActiveLPInstance => LaunchPads.Count > ActiveLaunchPadID ? LaunchPads[ActiveLaunchPadID] : null;

        public int LaunchPadCount
        {
            get
            {
                int count = 0;
                foreach (KCT_LaunchPad lp in LaunchPads)
                    if (lp.isOperational) count++;
                return count;
            }
        }

        public bool IsEmpty => !BuildList.Any() && !Warehouse.Any() &&
                    Upgrades.All(i => i == 0) && !Recon_Rollout.Any() && !AirlaunchPrep.Any() &&
                    !PadConstructions.Any() && LaunchPads.Count < 2 && LaunchPads.All(lp => lp.level < 1);

        public ReconRollout GetReconditioning(string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.LaunchPadID == launchSite && ((IKCTBuildItem)r).GetItemName() == "LaunchPad Reconditioning");

        public ReconRollout GetReconRollout(ReconRollout.RolloutReconType type, string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.RRType == type && r.LaunchPadID == launchSite);

        public void RecalculateBuildRates()
        {
            Rates.Clear();
            double rate = 0.1;
            int index = 0;
            // These loops could clean up a little, is it intended to add a rate=0 in the loop as the last entry?
            while (rate > 0)
            {
                rate = MathParser.ParseBuildRateFormula(BuildListVessel.ListType.VAB, index, this);
                if (rate >= 0)
                    Rates.Add(rate);
                index++;
            }

            var m = StringBuilderCache.Acquire();
            m.AppendLine("Rates:");
            foreach (double v in Rates)
                m.AppendLine($"{v}");

            KCTDebug.Log(m.ToStringAndRelease());
        }

        public void RecalculateUpgradedBuildRates()
        {
            UpRates.Clear();
            double rate = 0.1;
            int index = 0;
            while (rate > 0)
            {
                rate = MathParser.ParseBuildRateFormula(BuildListVessel.ListType.VAB, index, this, true);
                if (rate >= 0 && (index == 0 || Rates[index - 1] > 0))
                    UpRates.Add(rate);
                else
                    break;
                index++;
            }
        }

        public void SwitchToPrevLaunchPad() => SwitchLaunchPad(false);
        public void SwitchToNextLaunchPad() => SwitchLaunchPad(true);

        public void SwitchLaunchPad(bool forwardDirection)
        {
            if (LaunchPadCount < 2) return;

            int idx = ActiveLaunchPadID;
            KCT_LaunchPad pad;
            do
            {
                if (forwardDirection)
                {
                    idx = (idx + 1) % LaunchPads.Count;
                }
                else
                {
                    //Simple fix for mod function being "weird" in the negative direction
                    //http://stackoverflow.com/questions/1082917/mod-of-negative-number-is-melting-my-brain
                    idx = ((idx - 1) % LaunchPads.Count + LaunchPads.Count) % LaunchPads.Count;
                }
                pad = LaunchPads[idx];
            } while (!pad.isOperational);

            SwitchLaunchPad(idx);
        }

        public void Rename(string newName)
        {
            //find everything that references this launchpad by name and update the name reference

            Name = newName;
            // TODO: do we need to rename vessels?
        }

        public void SwitchLaunchPad(int LP_ID = -1, bool updateDestrNode = true)
        {
            if (LP_ID < 0)
                LP_ID = ActiveLaunchPadID;

            //set the active LP's new state
            //activate new pad

            if (updateDestrNode)
                ActiveLPInstance?.RefreshDestructionNode();

            ActiveLPInstance?.SetActive();
        }

        private void BuildVesselAndShipNodeConfigs(BuildListVessel blv, ref ConfigNode node)
        {
            var storageItem = new BuildListStorageItem();
            storageItem.FromBuildListVessel(blv);
            var cnTemp = new ConfigNode("KCTVessel");
            cnTemp = ConfigNode.CreateConfigFromObject(storageItem, cnTemp);
            var shipNode = new ConfigNode("ShipNode");
            blv.ShipNode.CopyTo(shipNode);
            cnTemp.AddNode(shipNode);
            node.AddNode(cnTemp);
        }

        private BuildListVessel CreateBLVFromNode(in ConfigNode cn)
        {
            var listItem = new BuildListStorageItem();
            ConfigNode.LoadObjectFromConfig(listItem, cn);
            BuildListVessel blv = listItem.ToBuildListVessel();
            blv.ShipNode = cn.GetNode("ShipNode");
            blv.LC = this;
            return blv;
        }

        public ConfigNode AsConfigNode()
        {
            KCTDebug.Log("Saving LC " + Name);
            var node = new ConfigNode("LaunchComplex");
            node.AddValue("LCName", Name);
            node.AddValue("ActiveLPID", ActiveLaunchPadID);
            node.AddValue("operational", isOperational);
            node.AddValue("isPad", isPad);
            node.AddValue("massMax", massMax);
            node.AddValue("massMin", massMin);
            node.AddValue("sizeMax", sizeMax);
            node.AddValue("id", _id);

            var cnVABUp = new ConfigNode("Upgrades");
            foreach (int upgrade in Upgrades)
            {
                cnVABUp.AddValue("Upgrade", upgrade.ToString());
            }
            node.AddNode(cnVABUp);

            var cnBuildl = new ConfigNode("BuildList");
            foreach (BuildListVessel blv in BuildList)
            {
                BuildVesselAndShipNodeConfigs(blv, ref cnBuildl);
            }
            node.AddNode(cnBuildl);

            var cnWh = new ConfigNode("Warehouse");
            foreach (BuildListVessel blv in Warehouse)
            {
                BuildVesselAndShipNodeConfigs(blv, ref cnWh);
            }
            node.AddNode(cnWh);

            var cnPadConstructions = new ConfigNode("PadConstructions");
            foreach (PadConstruction pc in PadConstructions)
            {
                var storageItem = new PadConstructionStorageItem();
                storageItem.FromPadConstruction(pc);
                var cn = new ConfigNode("PadConstruction");
                cn = ConfigNode.CreateConfigFromObject(storageItem, cn);
                cnPadConstructions.AddNode(cn);
            }
            node.AddNode(cnPadConstructions);

            var cnPlans = new ConfigNode("Plans");
            foreach (BuildListVessel blv in Plans.Values)
            {
                BuildVesselAndShipNodeConfigs(blv, ref cnPlans);
            }
            node.AddNode(cnPlans);

            var cnRR = new ConfigNode("Recon_Rollout");
            foreach (ReconRollout rr in Recon_Rollout)
            {
                var storageItem = new ReconRolloutStorageItem();
                storageItem.FromReconRollout(rr);
                var rrCN = new ConfigNode("Recon_Rollout_Item");
                rrCN = ConfigNode.CreateConfigFromObject(storageItem, rrCN);
                cnRR.AddNode(rrCN);
            }
            node.AddNode(cnRR);

            var cnAP = new ConfigNode("Airlaunch_Prep");
            foreach (AirlaunchPrep ap in AirlaunchPrep)
            {
                var storageItem = new AirlaunchPrepStorageItem();
                storageItem.FromAirlaunchPrep(ap);
                var cn = new ConfigNode("Airlaunch_Prep_Item");
                cn = ConfigNode.CreateConfigFromObject(storageItem, cn);
                cnAP.AddNode(cn);
            }
            node.AddNode(cnAP);

            var cnLPs = new ConfigNode("LaunchPads");
            foreach (KCT_LaunchPad lp in LaunchPads)
            {
                ConfigNode lpCN = lp.AsConfigNode();
                lpCN.AddNode(lp.DestructionNode);
                cnLPs.AddNode(lpCN);
            }
            node.AddNode(cnLPs);

            //Cache the regular rates
            var cnCachedRates = new ConfigNode("RateCache");
            foreach (double rate in Rates)
            {
                cnCachedRates.AddValue("rate", rate);
            }
            node.AddNode(cnCachedRates);

            return node;
        }

        public LCItem FromConfigNode(ConfigNode node)
        {
            Upgrades.Clear();
            BuildList.Clear();
            Warehouse.Clear();
            Plans.Clear();
            PadConstructions.Clear();
            Recon_Rollout.Clear();
            AirlaunchPrep.Clear();
            Rates.Clear();

            Name = node.GetValue("LCName");
            ActiveLaunchPadID = 0;
            node.TryGetValue("ActiveLPID", ref ActiveLaunchPadID);
            node.TryGetValue("operational", ref isOperational);
            node.TryGetValue("isPad", ref isPad);
            node.TryGetValue("massMax", ref massMax);
            node.TryGetValue("massMin", ref massMin);
            node.TryGetValue("sizeMax", ref sizeMax);
            node.TryGetValue("id", ref _id);

            ConfigNode vabUp = node.GetNode("Upgrades");
            foreach (string upgrade in vabUp.GetValues("Upgrade"))
            {
                Upgrades.Add(int.Parse(upgrade));
            }

            ConfigNode tmp = node.GetNode("BuildList");
            foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
            {
                BuildList.Add(CreateBLVFromNode(cn));
            }

            tmp = node.GetNode("Warehouse");
            foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
            {
                Warehouse.Add(CreateBLVFromNode(cn));
            }

            if (node.TryGetNode("Plans", ref tmp))
            {
                if (tmp.HasNode("KCTVessel"))
                    foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
                    {
                        var blv = CreateBLVFromNode(cn);
                        Plans.Remove(blv.ShipName); 
                        Plans.Add(blv.ShipName, blv);
                    }
            }

            tmp = node.GetNode("Recon_Rollout");
            foreach (ConfigNode RRCN in tmp.GetNodes("Recon_Rollout_Item"))
            {
                var tempRR = new ReconRolloutStorageItem();
                ConfigNode.LoadObjectFromConfig(tempRR, RRCN);
                Recon_Rollout.Add(tempRR.ToReconRollout());
            }

            if (node.TryGetNode("Airlaunch_Prep", ref tmp))
            {
                foreach (ConfigNode cn in tmp.GetNodes("Airlaunch_Prep_Item"))
                {
                    var storageItem = new AirlaunchPrepStorageItem();
                    ConfigNode.LoadObjectFromConfig(storageItem, cn);
                    AirlaunchPrep.Add(storageItem.ToAirlaunchPrep());
                }
            }

            if (node.HasNode("LaunchPads"))
            {
                LaunchPads.Clear();
                tmp = node.GetNode("LaunchPads");
                foreach (ConfigNode cn in tmp.GetNodes("KCT_LaunchPad"))
                {
                    var tempLP = new KCT_LaunchPad("LP0");
                    ConfigNode.LoadObjectFromConfig(tempLP, cn);
                    tempLP.DestructionNode = cn.GetNode("DestructionState");
                    if (tempLP.fractionalLevel == -1) tempLP.MigrateFromOldState();
                    LaunchPads.Add(tempLP);
                }
            }

            if (node.HasNode("PadConstructions"))
            {
                tmp = node.GetNode("PadConstructions");
                foreach (ConfigNode cn in tmp.GetNodes("PadConstruction"))
                {
                    var storageItem = new PadConstructionStorageItem();
                    ConfigNode.LoadObjectFromConfig(storageItem, cn);
                    PadConstructions.Add(storageItem.ToPadConstruction());
                }
            }

            if (node.HasNode("RateCache"))
            {
                foreach (string rate in node.GetNode("RateCache").GetValues("rate"))
                {
                    if (double.TryParse(rate, out double r))
                    {
                        Rates.Add(r);
                    }
                }
            }

            return this;
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
