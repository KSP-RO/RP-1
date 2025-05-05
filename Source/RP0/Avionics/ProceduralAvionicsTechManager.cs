using RealFuels;
using System.Collections.Generic;
using System.Text;
using UniLinq;

namespace RP0.ProceduralAvionics
{
    /// <summary>
    /// This clas does all the work of determining which tech nodes are available
    /// </summary>
    public static class ProceduralAvionicsTechManager
    {
        private static List<ProceduralAvionicsConfig> _allTechNodes;
        public static List<ProceduralAvionicsConfig> AllAvionicsConfigs => _allTechNodes; 

        private static Dictionary<string, string> _unlockedTech;

        public static bool TechIsEnabled { get; private set; }

        #region calls made during OnLoad, OnSave, other initialization

        public static void LoadAvionicsConfigs()
        {
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("AVIONICSCONFIGS"))
            {
                _allTechNodes = new List<ProceduralAvionicsConfig>();
                foreach (ConfigNode tNode in node.GetNodes("AVIONICSCONFIG"))
                {
                    ProceduralAvionicsConfig config = new ProceduralAvionicsConfig();
                    config.Load(tNode);
                    config.InitializeTechNodes();
                    _allTechNodes.Add(config);
                    ProceduralAvionicsUtils.Log("Loaded AvionicsConfg: ", config.name);
                }
            }
        }

        /// <summary>
        /// Used for injecting the state of the current save.
        /// </summary>
        /// <param name="param">Serialized tech state of the current save. Can be null if nothing exists.</param>
        internal static void SetUnlockedTechState(string param)
        {
            TechIsEnabled = HighLogic.CurrentGame != null &&
                ResearchAndDevelopment.Instance != null &&
                (HighLogic.CurrentGame.Mode == Game.Modes.CAREER ||
                HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX);

            ProceduralAvionicsUtils.Log("Setting unlocked tech state");
            _unlockedTech = new Dictionary<string, string>();
            if (param != null)
            {
                string[] typeStrings = param.Split('|');
                if (typeStrings.Length > 1)
                {
                    for (int i = 0; i < typeStrings.Length; i += 2)
                    {
                        var configName = typeStrings[i];
                        if (_allTechNodes.Any(x => x.name == configName))
                        {
                            _unlockedTech.Add(configName, typeStrings[i + 1]);
                        }
                    }
                }
            }
            ProceduralAvionicsUtils.Log("unlocked tech has ", _unlockedTech.Keys.Count.ToString(), " nodes");

            //At this point, we can go through our configs and see if we have any that need to be unlocked
            foreach (var config in _allTechNodes)
            {
                if (!_unlockedTech.ContainsKey(config.name))
                {
                    var freeTech = config.TechNodes.Values.FirstOrDefault(techNode => GetUnlockCost(config.name, techNode) <= 1 && techNode.IsAvailable);
                    if (freeTech != null)
                    {
                        _unlockedTech.Add(config.name, freeTech.name);
                    }
                }
            }
        }

        #endregion

        public static List<string> GetAllConfigs() => _allTechNodes.Select(node => node.name).ToList();

        public static List<string> GetAvailableConfigs()
        {
            //ProceduralAvionicsUtils.Log("Getting Available configs, procedural avionics has ", allTechNodes.Count, " nodes loaded");
            List<string> availableConfigs = new List<string>();
            foreach (ProceduralAvionicsConfig config in _allTechNodes)
            {
                if (!TechIsEnabled || config.IsAvailable)
                {
                    availableConfigs.Add(config.name);
                }
            }
            return availableConfigs;
        }

        public static List<string> GetPurchasedConfigs()
        {
            if (TechIsEnabled)
            {
                return _unlockedTech.Keys.ToList();
            }
            else
            {
                return GetAllConfigs();
            }
        }

        internal static object GetUnlockedTechState()
        {
            StringBuilder builder = StringBuilderCache.Acquire();
            foreach (string unlockedTechType in _unlockedTech.Keys)
            {
                if (builder.Length != 0)
                {
                    builder.Append("|");
                }
                builder.Append(unlockedTechType);
                builder.Append("|");
                builder.Append(_unlockedTech[unlockedTechType]);
            }
            string state = builder.ToStringAndRelease();
            ProceduralAvionicsUtils.Log("Unlocked Tech state:", state);
            return state;
        }

        internal static void SetMaxUnlockedTech(string avionicsConfigName, string techNodeName)
        {
            ProceduralAvionicsUtils.Log("Unlocking ", techNodeName, " for ", avionicsConfigName);
            if (!_unlockedTech.ContainsKey(avionicsConfigName))
            {
                ProceduralAvionicsUtils.Log("Unlocking for the first time");
                _unlockedTech.Add(avionicsConfigName, techNodeName);
            }
            else
            {
                ProceduralAvionicsUtils.Log("Unlocking new level");
                _unlockedTech[avionicsConfigName] = techNodeName;
            }
        }

        internal static string GetMaxUnlockedTech(string avionicsConfigName)
        {
            if (!TechIsEnabled)
            {
                var techNodesForConfig = _allTechNodes.FirstOrDefault(config => config.name == avionicsConfigName);
                var tn = techNodesForConfig.TechNodes.Values.Last().name;
                return tn;
            }
            if (_unlockedTech.ContainsKey(avionicsConfigName))
            {
                return _unlockedTech[avionicsConfigName];
            }
            return string.Empty;
        }

        internal static string GetFirstTech(string avionicsConfigName)
        {
            var techNodesForConfig = _allTechNodes.FirstOrDefault(config => config.name == avionicsConfigName);
            if (techNodesForConfig == null)
                return string.Empty;
            return techNodesForConfig.TechNodes.Values.First().name;
        }

        internal static int GetUnlockCost(string avionicsConfigName, ProceduralAvionicsTechNode techNode)
        {
            if (!PartUpgradeManager.Handler.CanHaveUpgrades()) return 0;

            string upgdName = GetPartUpgradeName(avionicsConfigName, techNode);
            PartUpgradeHandler.Upgrade upgd = PartUpgradeManager.Handler.GetUpgrade(upgdName);
            if (upgd == null) return 0;

            if (PartUpgradeManager.Handler.IsEnabled(upgdName)) return 0;

            if (upgd.entryCost < 1.1 && PartUpgradeManager.Handler.IsAvailableToUnlock(upgdName) &&
                PurchaseConfig(avionicsConfigName, techNode))
            {
                return 0;
            }

            return (int)upgd.entryCost;
        }

        internal static bool PurchaseConfig(string avionicsConfigName, ProceduralAvionicsTechNode techNode)
        {
            string upgdName = GetPartUpgradeName(avionicsConfigName, techNode);
            PartUpgradeHandler.Upgrade upgd = PartUpgradeManager.Handler.GetUpgrade(upgdName);
            if (upgd == null) return false;

            Harmony.RFECMPatcher.techNode = techNode.TechNodeName;
            bool success = EntryCostManager.Instance.PurchaseConfig(upgdName);
            Harmony.RFECMPatcher.techNode = null;
            if (success)
            {
                PartUpgradeManager.Handler.SetUnlocked(upgd.name, true);
                GameEvents.OnPartUpgradePurchased.Fire(upgd);
            }

            return success;
        }

        public static string GetPartUpgradeName(string avionicsConfigName, ProceduralAvionicsTechNode techNode)
        {
            return $"{avionicsConfigName}-{techNode.name}";
        }

        public static ProceduralAvionicsConfig GetProceduralAvionicsConfig(string configName)
        {
            return _allTechNodes.FirstOrDefault(config => config.name == configName) ?? new ProceduralAvionicsConfig { name = "LegacyConfig" };
        }
    }
}
