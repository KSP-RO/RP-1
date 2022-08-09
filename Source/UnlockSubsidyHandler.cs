using System.Collections.Generic;
using UnityEngine;
using KerbalConstructionTime;
using System;

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

        /// <summary>
        /// Note this is CMQ-neutral.
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="UT"></param>
        public void IncrementSubsidyTime(string tech, double UT) => IncrementSubsidy(tech, UT * MaintenanceHandler.Instance.ResearchSalaryPerDay / 86400d * MaintenanceHandler.Settings.researcherSalaryToUnlockSubsidy);

        /// <summary>
        /// Note this is CMQ-neutral.
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="amount"></param>
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

        private void FillECMs(string techID, string ecmName, Dictionary<string, double> ecmToCost, Dictionary<string, HashSet<string>> ecmToTech)
        {
            // if this ECM is unlocked, we're done.
            if (RealFuels.EntryCostDatabase.IsUnlocked(ecmName))
                return;

            var holder = RealFuels.EntryCostDatabase.GetHolder(ecmName);
            if (holder == null)
                return;

            // If we've already seen this ECM, just add the tech and recurse
            // Note that by definition we will have already added all children
            // to the cost dict already.
            if (ecmToTech.TryGetValue(ecmName, out var techs))
            {
                techs.Add(techID);
                foreach (var childName in holder.children)
                    FillECMs(techID, childName, ecmToCost, ecmToTech);

                return;
            }

            // Fill from scratch and recurse.
            ecmToCost[holder.name] = holder.cost;
            techs = new HashSet<string>();
            techs.Add(techID);
            ecmToTech[holder.name] = techs;

            foreach (var childName in holder.children)
                FillECMs(techID, childName, ecmToCost, ecmToTech);
        }

        private void AddPartToDicts(AvailablePart ap, Dictionary<string, double> ecmToCost, Dictionary<string, HashSet<string>> ecmToTech)
        {
            string sanitizedName = RealFuels.Utilities.SanitizeName(ap.name);

            if (RealFuels.EntryCostDatabase.GetHolder(sanitizedName) != null)
            {
                FillECMs(ap.TechRequired, sanitizedName, ecmToCost, ecmToTech);
                return;
            }
            
            // It shouldn't already contain the key, but you never know.
            if (!ecmToCost.ContainsKey(sanitizedName))
            {
                ecmToCost[sanitizedName] = ap.entryCost;
                
                // ditto
                if (!ecmToTech.TryGetValue(sanitizedName, out var techs))
                    techs = new HashSet<string>();

                techs.Add(ap.TechRequired);
            }
        }

        public void SpendSubsidyAndCost(List<AvailablePart> parts)
        {
            // This is going to be expensive, because we have to chase down all the ECMs.
            double cmqMultiplier = -CurrencyUtils.Funds(TransactionReasonsRP0.RnDPartPurchase, -1d);
            double recipCMQMult = 1d / cmqMultiplier;

            Dictionary<string, double> ecmToCost = new Dictionary<string, double>();
            Dictionary<string, HashSet<string>> ecmToTech = new Dictionary<string, HashSet<string>>();
            foreach (var p in parts)
                AddPartToDicts(p, ecmToCost, ecmToTech);

            // First apply our CMQ result
            foreach (var kvp in ecmToTech)
                ecmToCost[kvp.Key] = ecmToCost[kvp.Key] * cmqMultiplier;

            // first try to spend local subsidy in each case
            foreach (var kvp in ecmToTech)
            {
                foreach (string tech in kvp.Value)
                {
                    if (!_subsidyStorage.TryGetValue(tech, out var sNode))
                        continue;

                    double cost = ecmToCost[kvp.Key];

                    // This check is needed because we might have multiple techs.
                    if (cost == 0)
                        continue;

                    // Now deduct local funds and lower cost.
                    double local = sNode.funds;
                    if (local > cost)
                        local = cost;
                    cost -= local;
                    sNode.funds -= local;
                    ecmToCost[kvp.Key] = cost;
                }
            }

            // Now pull full subsidy
            foreach (var kvp in ecmToTech)
            {
                foreach (string tech in kvp.Value)
                {
                    double cost = ecmToCost[kvp.Key];

                    // This check is needed because we might have multiple techs.
                    if (cost == 0d)
                        continue;

                    ecmToCost[kvp.Key] = SpendSubsidy(tech, cost);

                }
            }

            // Finally, pay for the excess.
            double totalCost = 0d;
            foreach (double d in ecmToCost.Values)
                totalCost += d;

            if (totalCost > 0d)
                Funding.Instance.AddFunds(-totalCost * recipCMQMult, TransactionReasons.RnDPartPurchase);
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
            double postCMQCost = -CurrencyUtils.Funds(TransactionReasonsRP0.RnDPartPurchase, -entryCost);
            double remainingCost = SpendSubsidy(tech, postCMQCost);
            // Refresh description to show new subsidy remaining
            if (KSP.UI.Screens.RDController.Instance != null)
                KSP.UI.Screens.RDController.Instance.ShowNodePanel(KSP.UI.Screens.RDController.Instance.node_selected);

            return (float)(remainingCost * (entryCost / postCMQCost));
        }

        private void OnPartPurchased(AvailablePart ap)
        {
            UnlockSubsidyUtility.StoredPartEntryCost = ap.entryCost;
            if (ap.costsFunds)
            {
                int remainingCost = (int)ProcessSubsidy(ap.entryCost, ap.TechRequired);
                ap.SetEntryCost(remainingCost);
            }
        }

        private void OnPartUpgradePurchased(PartUpgradeHandler.Upgrade up)
        {
            UnlockSubsidyUtility.StoredUpgradeEntryCost = up.entryCost;
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
    public class UnlockSubsidyUtility : MonoBehaviour
    {
        public static float StoredUpgradeEntryCost = -1f;
        public static int StoredPartEntryCost = -1;

        public static UnityEngine.UI.Button Button = null;
        public static string TooltipText = null;
        private static System.Reflection.MethodInfo IsHighlightedMethod = typeof(UnityEngine.UI.Selectable).GetMethod("IsHighlighted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        public static int WindowID = "PartListTooltip".GetHashCode();

        public void Awake()
        {
            GameEvents.OnPartPurchased.Add(ResetPartCost);
            GameEvents.OnPartUpgradePurchased.Add(ResetUpgradeCost);
            DontDestroyOnLoad(this);
        }

        public void OnGUI()
        {
            if (Button == null || !Button.gameObject.activeSelf)
                return;

            bool highlighted = (bool)IsHighlightedMethod.Invoke(Button, null);
            if (highlighted)
            {
                Tooltip.Instance.RecordTooltip(WindowID, false, TooltipText);
                Tooltip.Instance.ShowTooltip(WindowID);
            }
            else
            {
                Tooltip.Instance.RecordTooltip(WindowID, false, TooltipText);
            }
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