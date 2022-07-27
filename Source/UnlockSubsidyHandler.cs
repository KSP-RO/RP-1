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

            public UnlockSubsidyNode() { }

            public UnlockSubsidyNode(UnlockSubsidyNode src)
            {
                tech = src.tech;
                funds = src.funds;
            }
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

        public double GetLocalSubsidyAmount(string tech, Dictionary<string, UnlockSubsidyNode> dict = null)
        {
            if (dict == null)
                dict = _subsidyStorage;

            if (!dict.TryGetValue(tech, out var sNode))
                return 0d;

            return sNode.funds;
        }

        public double GetSubsidyAmount(string tech, Dictionary<string, UnlockSubsidyNode> dict = null)
        {
            if (tech == null)
                return 0d;

            if (dict == null)
                dict = _subsidyStorage;

            double amount = GetLocalSubsidyAmount(tech, dict);

            List<string> parentList;
            if (!KerbalConstructionTimeData.techNameToParents.TryGetValue(tech, out parentList))
            {
                Debug.LogError($"[KCT] Could not find techToParent for tech {tech}");
                return amount;
            }
            foreach (var parent in parentList)
                amount += GetSubsidyAmount(parent, dict);

            _cacheDict[tech] = amount;
            return amount;
        }

        // This is not optimal, but it's better than naive unsorted.
        public double GetSubsidyAmount(List<AvailablePart> parts)
        {
            double subsidyAmount = 0d;
            var cloneDict = new Dictionary<string, UnlockSubsidyNode>();
            foreach (var kvp in _subsidyStorage)
                cloneDict.Add(kvp.Key, new UnlockSubsidyNode(kvp.Value));


            var partCostDict = new Dictionary<AvailablePart, double>();
            // first pull from local.
            foreach (var p in parts)
            {
                double cost = p.entryCost;
                if (cloneDict.TryGetValue(p.TechRequired, out var sNode))
                {
                    double local = sNode.funds;
                    if (local > cost)
                        local = cost;
                    cost -= local;
                    sNode.funds -= local;
                    subsidyAmount += local;
                }
                partCostDict[p] = cost;
            }
            foreach (var p in parts)
            {
                double cost = partCostDict[p];
                double excess = SpendSubsidy(p.TechRequired, cost, cloneDict);
                subsidyAmount += (cost - excess);
            }

            return subsidyAmount;
        }

        public void SpendSubsidyAndCost(List<AvailablePart> parts)
        {
            double subsidyAmount = 0d;

            var partCostDict = new Dictionary<AvailablePart, double>();
            // first pull from local.
            foreach (var p in parts)
            {
                double cost = p.entryCost;
                if (_subsidyStorage.TryGetValue(p.TechRequired, out var sNode))
                {
                    double local = sNode.funds;
                    if (local > cost)
                        local = cost;
                    cost -= local;
                    sNode.funds -= local;
                    subsidyAmount += local;
                }
                partCostDict[p] = cost;
            }

            double totalCost = 0d;

            foreach (var p in parts)
            {
                double cost = partCostDict[p];
                double excess = SpendSubsidy(p.TechRequired, cost);
                subsidyAmount += (cost - excess);
                totalCost += excess;
            }

            Funding.Instance.AddFunds(-totalCost, TransactionReasons.RnDPartPurchase);
        }

        public double SpendSubsidy(string tech, double cost, Dictionary<string, UnlockSubsidyNode> dict = null)
        {
            double amount = GetSubsidyAmount(tech, dict);
            if (amount == 0d)
                return cost;

            if (dict == null)
                dict = _subsidyStorage;

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

            _SpendSubsidy(tech, cost, excessCost > 0d, dict);
            return excessCost;
        }

        private void _SpendSubsidy(string tech, double cost, bool spendAll, Dictionary<string, UnlockSubsidyNode> dict)
        {
            if (dict == null)
                dict = _subsidyStorage;

            if (dict.TryGetValue(tech, out var sNode))
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
                    _SpendSubsidy(parent, cost, true, dict);
                }
                return;
            }

            double parentSubsidyTotal = 0d;
            foreach (var parent in parentList)
            {
                double amount;
                if (!_cacheDict.TryGetValue(parent, out amount))
                {
                    amount = GetSubsidyAmount(parent, dict);
                    _cacheDict[parent] = amount;
                }
                parentSubsidyTotal += amount;
            }

            foreach (var parent in parentList)
            {
                double portion = _cacheDict[parent] / parentSubsidyTotal * cost;
                _SpendSubsidy(parent, portion, false, dict);
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
            UnlockSubsidyResetter.StoredPartEntryCost = ap.entryCost;
            if (ap.costsFunds)
            {
                int remainingCost = (int)ProcessSubsidy(ap.entryCost, ap.TechRequired);
                ap.SetEntryCost(remainingCost);
            }
        }

        private void OnPartUpgradePurchased(PartUpgradeHandler.Upgrade up)
        {
            UnlockSubsidyResetter.StoredUpgradeEntryCost = up.entryCost;
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

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class UnlockSubsidyResetter : MonoBehaviour
    {
        public static float StoredUpgradeEntryCost = -1f;
        public static int StoredPartEntryCost = -1;

        public void Awake()
        {
            GameEvents.OnPartPurchased.Add(ResetPartCost);
            GameEvents.OnPartUpgradePurchased.Add(ResetUpgradeCost);

            Destroy(this);
        }

        private void ResetPartCost(AvailablePart ap)
        {
            if (StoredPartEntryCost >= 0)
                ap.SetEntryCost(StoredPartEntryCost);

            StoredPartEntryCost = -1;
        }

        private void ResetUpgradeCost(PartUpgradeHandler.Upgrade up)
        {
            if (StoredUpgradeEntryCost >= 0)
                up.entryCost = StoredUpgradeEntryCost;

            StoredUpgradeEntryCost = -1f;
        }
    }
}