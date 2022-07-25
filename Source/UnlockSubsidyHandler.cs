using System.Collections.Generic;
using UnityEngine;
using KerbalConstructionTime;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class UnlockSubsidyHandler : ScenarioModule
    {
        public class UnlockSubsidyNode
        {
            [Persistent]
            public string tech;

            [Persistent]
            public double funds;
        }

        public static UnlockSubsidyHandler Instance { get; private set; }

        private readonly Dictionary<string, UnlockSubsidyNode> _subsidyStorage = new Dictionary<string, UnlockSubsidyNode>();
        private static readonly Dictionary<string, double> _cacheDict = new Dictionary<string, double>();

        public double TotalSubsidy
        {
            get
            {
                double amt = 0;
                foreach (var node in _subsidyStorage.Values)
                    amt += node.funds;

                return amt;
            }
        }

        public void IncrementSubsidyTime(string tech, double UT) => IncrementSubsidy(tech, UT * MaintenanceHandler.Instance.ResearchSalaryPerDay / 86400d * MaintenanceHandler.Settings.researcherSalaryToUnlockSubsidy);

        public void IncrementSubsidy(string tech, double amount)
        {
            if (!_subsidyStorage.TryGetValue(tech, out var sNode))
            {
                sNode = new UnlockSubsidyNode() { tech = tech };
                _subsidyStorage[tech] = sNode;
            }

            sNode.funds += amount;
        }

        public double GetLocalSubsidyAmount(string tech)
        {
            if (!_subsidyStorage.TryGetValue(tech, out var sNode))
                return 0d;

            return sNode.funds;
        }

        public double GetSubsidyAmount(string tech)
        {
            if (tech == null)
                return 0d;

            double amount = GetLocalSubsidyAmount(tech);

            List<string> parentList;
            if (!KerbalConstructionTimeData.techNameToParents.TryGetValue(tech, out parentList))
            {
                Debug.LogError($"[KCT] Could not find techToParent for tech {tech}");
                return amount;
            }
            foreach (var parent in parentList)
                amount += GetSubsidyAmount(parent);

            _cacheDict[tech] = amount;
            return amount;
        }

        public double SpendSubsidy(string tech, double cost)
        {
            double amount = GetSubsidyAmount(tech);
            if (amount == 0d)
                return cost;

            double excessCost;
            if (amount < cost)
            {
                excessCost = cost - amount;
                cost = amount;
            }
            else
            {
                excessCost = 0d;
            }

            _SpendSubsidy(tech, cost, excessCost > 0d);
            return excessCost;
        }

        private void _SpendSubsidy(string tech, double cost, bool spendAll)
        {
            if (_subsidyStorage.TryGetValue(tech, out var sNode))
            {
                if (spendAll)
                {
                    sNode.funds = 0d;
                }
                else
                {
                    if (sNode.funds <= cost)
                    {
                        cost -= sNode.funds;
                        sNode.funds = 0d;
                    }
                    else
                    {
                        sNode.funds -= cost;
                        cost = 0d;
                    }

                    if (cost < 0.01d)
                        return; // done
                }
            }

            // Distribute cost proportionally amongst parents
            List<string> parentList;
            if (!KerbalConstructionTimeData.techNameToParents.TryGetValue(tech, out parentList))
            {
                Debug.LogError($"[KCT] Could not find techToParent for tech {tech}");
                return; // error;
            }

            if (spendAll)
            {
                foreach (var parent in parentList)
                {
                    _SpendSubsidy(parent, cost, true);
                }
                return;
            }

            double parentSubsidyTotal = 0d;
            foreach (var parent in parentList)
            {
                double amount;
                if (!_cacheDict.TryGetValue(parent, out amount))
                {
                    amount = GetSubsidyAmount(parent);
                    _cacheDict[parent] = amount;
                }
                parentSubsidyTotal += amount;
            }

            foreach (var parent in parentList)
            {
                double portion = _cacheDict[parent] / parentSubsidyTotal * cost;
                _SpendSubsidy(parent, portion, spendAll);
            }
        }

        private float ProcessSubsidy(float entryCost, string tech)
        {
            double remainingCost = SpendSubsidy(tech, entryCost);
            // Refresh description to show new subsidy remaining
            if (KSP.UI.Screens.RDController.Instance != null)
                KSP.UI.Screens.RDController.Instance.ShowNodePanel(KSP.UI.Screens.RDController.Instance.node_selected);

            return (float)remainingCost;
        }

        private void OnPartPurchased(AvailablePart ap)
        {
            if (ap.costsFunds)
            {
                int remainingCost = (int)ProcessSubsidy(ap.entryCost, ap.TechRequired);
                ap.SetEntryCost(remainingCost);
            }
        }

        private void OnPartUpgradePurchased(PartUpgradeHandler.Upgrade up)
        {
            float remainingCost = ProcessSubsidy(up.entryCost, up.techRequired);
            up.entryCost = remainingCost;
        }

        public override void OnLoad(ConfigNode node)
        {
            _subsidyStorage.Clear();
            foreach (var cn in node.GetNodes("UnlockSubsidyNode"))
            {
                var sNode = new UnlockSubsidyNode();
                ConfigNode.LoadObjectFromConfig(sNode, cn);
                _subsidyStorage[sNode.tech] = sNode;
            }
        }

        public override void OnSave(ConfigNode node)
        {
            foreach (var sNode in _subsidyStorage.Values)
            {
                var cn = node.AddNode("UnlockSubsidyNode");
                ConfigNode.CreateConfigFromObject(sNode, cn);
            }
        }

        public override void OnAwake()
        {
            base.OnAwake();

            if (Instance != null)
                Destroy(Instance);

            Instance = this;
        }

        public void Start()
        {
            // This runs after KSP's Funding's OnAwake and thus we can bind after (and run before)
            GameEvents.OnPartPurchased.Add(OnPartPurchased);
            GameEvents.OnPartUpgradePurchased.Add(OnPartUpgradePurchased);
        }

        public void OnDestroy()
        {
            GameEvents.OnPartPurchased.Remove(OnPartPurchased);
            GameEvents.OnPartUpgradePurchased.Remove(OnPartUpgradePurchased);
            
            if (Instance == this)
                Instance = null;
        }
    }
}
