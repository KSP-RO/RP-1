using System.Collections.Generic;
using UnityEngine;
using KerbalConstructionTime;
using System;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class UnlockSubsidyHandler : ScenarioModule
    {
        public class UnlockCreditNode
        {
            [Persistent]
            public string tech;

            [Persistent]
            public double funds;

            public UnlockCreditNode() { }

            public UnlockCreditNode(UnlockCreditNode src)
            {
                tech = src.tech;
                funds = src.funds;
            }
        }

        public static UnlockSubsidyHandler Instance { get; private set; }

        private readonly Dictionary<string, UnlockCreditNode> _creditStorage = new Dictionary<string, UnlockCreditNode>();
        private static readonly Dictionary<string, double> _cacheDict = new Dictionary<string, double>();
        private static readonly HashSet<string> _seenNodes = new HashSet<string>();

        public double TotalCredit
        {
            get
            {
                double amt = 0;
                foreach (var node in _creditStorage.Values)
                    amt += node.funds;

                return amt;
            }
        }

        /// <summary>
        /// Note this is CMQ-neutral.
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="UT"></param>
        public void IncrementCreditTime(string tech, double UT) => IncrementCredit(tech, UT * MaintenanceHandler.Instance.ResearchSalaryPerDay / 86400d * MaintenanceHandler.Settings.researcherSalaryToUnlockCredit);

        /// <summary>
        /// Note this is CMQ-neutral.
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="amount"></param>
        public void IncrementCredit(string tech, double amount)
        {
            // Will also catch NaN
            if (!(amount > 0))
                return;

            if (!_creditStorage.TryGetValue(tech, out var sNode))
            {
                sNode = new UnlockCreditNode() { tech = tech };
                _creditStorage[tech] = sNode;
            }

            sNode.funds += amount;
        }

        public double GetLocalCreditAmount(string tech, Dictionary<string, UnlockCreditNode> dict = null)
        {
            if (dict == null)
                dict = _creditStorage;

            if (!dict.TryGetValue(tech, out var sNode))
                return 0d;

            return sNode.funds;
        }

        public double GetCreditAmount(string tech, Dictionary<string, UnlockCreditNode> dict = null)
        {
            _seenNodes.Clear();
            return _GetCreditAmount(tech, dict);
        }

        private double _GetCreditAmount(string tech, Dictionary<string, UnlockCreditNode> dict = null)
        {
            if (tech == null)
                return 0d;

            if (dict == null)
                dict = _creditStorage;

            double amount = GetLocalCreditAmount(tech, dict);

            List<string> parentList;
            if (!KerbalConstructionTimeData.techNameToParents.TryGetValue(tech, out parentList))
            {
                Debug.LogError($"[KCT] Could not find techToParent for tech {tech}");
                return amount;
            }
            foreach (var parent in parentList)
            {
                if (_seenNodes.Contains(parent))
                    continue;

                _seenNodes.Add(parent);
                amount += _GetCreditAmount(parent, dict);
            }

            _cacheDict[tech] = amount;
            return amount;
        }

        // This is not optimal, but it's better than naive unsorted.
        public double GetCreditAmount(List<AvailablePart> parts)
        {
            double creditAmount = 0d;
            var cloneDict = new Dictionary<string, UnlockCreditNode>();
            foreach (var kvp in _creditStorage)
                cloneDict.Add(kvp.Key, new UnlockCreditNode(kvp.Value));


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
                    creditAmount += local;
                }
                partCostDict[p] = cost;
            }
            foreach (var p in parts)
            {
                double cost = partCostDict[p];
                double excess = SpendCredit(p.TechRequired, cost, cloneDict);
                creditAmount += (cost - excess);
            }

            return creditAmount;
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
                ecmToTech[sanitizedName] = techs;
            }
        }

        public void SpendCreditAndCost(List<AvailablePart> parts)
        {
            // This is going to be expensive, because we have to chase down all the ECMs.
            double cmqMultiplier = -CurrencyUtils.Funds(TransactionReasonsRP0.PartOrUpgradeUnlock, -1d);
            if (cmqMultiplier == 0d)
                return;
            
            double recipCMQMult = 1d / cmqMultiplier;

            Dictionary<string, double> ecmToCost = new Dictionary<string, double>();
            Dictionary<string, HashSet<string>> ecmToTech = new Dictionary<string, HashSet<string>>();
            foreach (var p in parts)
                AddPartToDicts(p, ecmToCost, ecmToTech);

            // First apply our CMQ result
            foreach (var kvp in ecmToTech)
                ecmToCost[kvp.Key] = ecmToCost[kvp.Key] * cmqMultiplier;

            // first try to spend local credit in each case
            foreach (var kvp in ecmToTech)
            {
                foreach (string tech in kvp.Value)
                {
                    if (!_creditStorage.TryGetValue(tech, out var sNode))
                        continue;

                    double cost = ecmToCost[kvp.Key];
                    double local = sNode.funds;

                    // This check is needed because we might have multiple techs.
                    if (cost == 0 || local == 0)
                        continue;

                    // Now deduct local funds and lower cost.
                    if (local > cost)
                        local = cost;
                    cost -= local;
                    sNode.funds -= local;
                    ecmToCost[kvp.Key] = cost;
                }
            }

            // Now pull full credit
            foreach (var kvp in ecmToTech)
            {
                foreach (string tech in kvp.Value)
                {
                    double cost = ecmToCost[kvp.Key];

                    // This check is needed because we might have multiple techs.
                    if (cost == 0d)
                        continue;

                    ecmToCost[kvp.Key] = SpendCredit(tech, cost);
                }
            }

            // Finally, pay for the excess.
            double totalCost = 0d;
            foreach (double d in ecmToCost.Values)
                totalCost += d;

            if (totalCost > 0d)
                Funding.Instance.AddFunds(-totalCost * recipCMQMult, TransactionReasonsRP0.PartOrUpgradeUnlock.Stock());
        }

        public double SpendCredit(string tech, double cost, Dictionary<string, UnlockCreditNode> dict = null)
        {
            double amount = GetCreditAmount(tech, dict);
            if (amount == 0d)
                return cost;

            if (dict == null)
                dict = _creditStorage;

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

            _SpendCredit(tech, cost, excessCost > 0d, dict);
            return excessCost;
        }

        private void _SpendCredit(string tech, double cost, bool spendAll, Dictionary<string, UnlockCreditNode> dict)
        {
            if (dict == null)
                dict = _creditStorage;

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
                    _SpendCredit(parent, cost, true, dict);
                }
                return;
            }

            double parentCreditTotal = 0d;
            foreach (var parent in parentList)
            {
                double amount;
                if (!_cacheDict.TryGetValue(parent, out amount))
                {
                    amount = GetCreditAmount(parent, dict);
                    _cacheDict[parent] = amount;
                }
                parentCreditTotal += amount;
            }

            if (parentCreditTotal > 0d)
            {
                foreach (var parent in parentList)
                {
                    double portion = _cacheDict[parent] / parentCreditTotal * cost;
                    _SpendCredit(parent, portion, false, dict);
                }
            }
        }

        /// <summary>
        /// Transforms entrycost to post-strategy entrycost, spends credit,
        /// and returns remaining (unsubsidized) cost
        /// </summary>
        /// <param name="entryCost"></param>
        /// <param name="tech"></param>
        /// <returns></returns>
        private float ProcessCredit(float entryCost, string tech)
        {
            if (entryCost == 0f)
                return 0f;
            
            double postCMQCost = -CurrencyUtils.Funds(TransactionReasonsRP0.PartOrUpgradeUnlock, -entryCost);
            if (double.IsNaN(postCMQCost))
            {
                Debug.LogError("[RP-0] CMQ for a credit unlock returned NaN, ignoring and going back to regular cost.");
                postCMQCost = entryCost;
            }
            else if (postCMQCost == 0d)
            {
                Debug.LogError("[RP-0] CMQ for a credit unlock returned 0, not spending any credit.");
                return 0f;
            }

            // Actually spend credit and get (post-effect) remainder
            double remainingCost = SpendCredit(tech, postCMQCost);

            // Refresh description to show new credit remaining
            if (KSP.UI.Screens.RDController.Instance != null)
                KSP.UI.Screens.RDController.Instance.ShowNodePanel(KSP.UI.Screens.RDController.Instance.node_selected);

            //return the remainder after transforming to pre-effect numbers
            return (float)(remainingCost * (entryCost / postCMQCost));
        }

        private void OnPartPurchased(AvailablePart ap)
        {
            UnlockCreditUtility.StoredPartEntryCost = ap.entryCost;
            if (ap.costsFunds)
            {
                int remainingCost = (int)ProcessCredit(ap.entryCost, ap.TechRequired);
                ap.SetEntryCost(remainingCost);
            }
        }

        private void OnPartUpgradePurchased(PartUpgradeHandler.Upgrade up)
        {
            UnlockCreditUtility.StoredUpgradeEntryCost = up.entryCost;
            float remainingCost = ProcessCredit(up.entryCost, up.techRequired);
            up.entryCost = remainingCost;
        }

        public override void OnLoad(ConfigNode node)
        {
            _creditStorage.Clear();
            foreach (var cn in node.GetNodes("UnlockSubsidyNode"))
            {
                var sNode = new UnlockCreditNode();
                ConfigNode.LoadObjectFromConfig(sNode, cn);
                _creditStorage[sNode.tech] = sNode;
            }
        }

        public override void OnSave(ConfigNode node)
        {
            foreach (var sNode in _creditStorage.Values)
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
    public class UnlockCreditUtility : MonoBehaviour
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
