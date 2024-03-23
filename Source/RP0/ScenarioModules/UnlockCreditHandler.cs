using System.Collections.Generic;
using UnityEngine;
using System;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class UnlockCreditHandler : ScenarioModule
    {
        public static UnlockCreditHandler Instance { get; private set; }

        [KSPField(isPersistant = true)]
        private double _totalCredit = 0;

        public double TotalCredit => _totalCredit;

        public double CreditForTime(double UT)
        {
            double sum = 0d;
            double mult = UT * Database.SettingsSC.salaryResearchers * (1d / (86400d * 365.25d));
            
            int res = SpaceCenterManagement.Instance.Researchers;
            int totalCounted = 0;
            
            foreach (var kvp in Database.SettingsSC.researchersToUnlockCreditSalaryMultipliers)
            {
                if (totalCounted >= res)
                    break;

                int amountToCount = kvp.Key - totalCounted;
                int remaining = res - totalCounted;
                if (amountToCount > remaining)
                    amountToCount = remaining;

                sum += amountToCount * kvp.Value;
                totalCounted += amountToCount;
            }

            return sum * mult;
        }

        public double GetCreditAmount(string tech) => _totalCredit;
        public double GetCreditAmount(List<AvailablePart> partList) => _totalCredit;

        /// <summary>
        /// Note this is CMQ-neutral.
        /// </summary>
        /// <param name="tech"></param>
        /// <param name="UT"></param>
        public void IncrementCreditTime(string tech, double UT) => IncrementCredit(tech, CreditForTime(UT));

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

            amount *= CurrencyUtils.Rate(TransactionReasonsRP0.RateUnlockCreditIncrease);

            _totalCredit += amount;
        }

        private void FillECMs(string ecmName, Dictionary<string, double> ecmToCost)
        {
            // if this ECM is unlocked, we're done.
            if (RealFuels.EntryCostDatabase.IsUnlocked(ecmName))
                return;

            var holder = RealFuels.EntryCostDatabase.GetHolder(ecmName);
            if (holder == null)
                return;

            // Fill from scratch and recurse.
            ecmToCost[holder.name] = holder.cost;
            foreach (var childName in holder.children)
                FillECMs(childName, ecmToCost);
        }

        private void AddPartToDicts(AvailablePart ap, Dictionary<string, double> ecmToCost)
        {
            string sanitizedName = RealFuels.Utilities.SanitizeName(ap.name);

            if (RealFuels.EntryCostDatabase.GetHolder(sanitizedName) != null)
            {
                FillECMs(sanitizedName, ecmToCost);
                return;
            }
            
            // It shouldn't already contain the key, but you never know.
            if (!ecmToCost.ContainsKey(sanitizedName))
            {
                ecmToCost[sanitizedName] = ap.entryCost;
            }
        }

        public void SetCredit(double value)
        {
            _totalCredit = value;
        }

        public void SpendCreditAndCost(List<AvailablePart> parts)
        {
            // This is going to be expensive, because we have to chase down all the ECMs.
            double cmqMultiplier = -CurrencyUtils.Funds(TransactionReasonsRP0.PartOrUpgradeUnlock, -1d);
            if (cmqMultiplier == 0d)
                return;
            
            double recipCMQMult = 1d / cmqMultiplier;

            Dictionary<string, double> ecmToCost = new Dictionary<string, double>();
            foreach (var p in parts)
                AddPartToDicts(p, ecmToCost);

            double total = 0d;
            foreach (var kvp in ecmToCost)
                total += kvp.Value;

            total *= cmqMultiplier;
            double excess = SpendCredit(total);

            // Finally, pay for the excess.
            if (excess > 0d)
                Funding.Instance.AddFunds(-excess * recipCMQMult, TransactionReasonsRP0.PartOrUpgradeUnlock.Stock());
        }

        public double SpendCredit(string tech, double cost) => SpendCredit(cost);

        public double SpendCredit(double cost)
        {
            if (_totalCredit == 0d)
                return cost;

            double excessCost;
            if (_totalCredit < cost)
            {
                excessCost = cost - _totalCredit;
                if (CareerLog.Instance?.CurrentPeriod != null)
                    CareerLog.Instance.CurrentPeriod.SpentUnlockCredit += _totalCredit;
                _totalCredit = 0;
            }
            else
            {
                excessCost = 0d;
                _totalCredit -= cost;
                if (CareerLog.Instance?.CurrentPeriod != null)
                    CareerLog.Instance.CurrentPeriod.SpentUnlockCredit += cost;
            }
            return excessCost;
        }

        public CurrencyModifierQueryRP0 GetCMQ(double cost, string tech, TransactionReasonsRP0 reason)
        {
            var cmq = CurrencyModifierQueryRP0.RunQuery(reason, -cost, 0d, 0d);
            cmq.AddPostDelta(CurrencyRP0.Funds, Math.Min(-cmq.GetTotal(CurrencyRP0.Funds, true), GetCreditAmount(tech)), true);
            return cmq;
        }

        public CurrencyModifierQueryRP0 GetPrePostCostAndAffordability(double cost, string tech, TransactionReasonsRP0 reason, out double preCreditCost, out double postCreditCost, out double credit, out bool canAfford)
        {
            var cmq = CurrencyModifierQueryRP0.RunQuery(reason, -cost, 0d, 0d);
            preCreditCost = -cmq.GetTotal(CurrencyRP0.Funds, false);
            credit = Math.Min(preCreditCost, GetCreditAmount(tech));
            cmq.AddPostDelta(CurrencyRP0.Funds, credit, true);
            postCreditCost = -cmq.GetTotal(CurrencyRP0.Funds, true);
            canAfford = cmq.CanAfford();

            return cmq;
        }

        public void ProcessCredit(double cost, string tech, TransactionReasonsRP0 reason)
        {
            var cmq = CurrencyModifierQueryRP0.RunQuery(reason, -cost, 0d, 0d);
            double postCMQCost = -cmq.GetTotal(CurrencyRP0.Funds, true);
            double remainingCost = SpendCredit(postCMQCost);
            Funding.Instance.AddFunds(-(float)(remainingCost * (cost / postCMQCost)), reason.Stock());
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
                RP0Debug.LogError("CMQ for a credit unlock returned NaN, ignoring and going back to regular cost.");
                postCMQCost = entryCost;
            }
            else if (postCMQCost == 0d)
            {
                RP0Debug.LogError("CMQ for a credit unlock returned 0, not spending any credit.");
                return 0f;
            }

            // Actually spend credit and get (post-effect) remainder
            double remainingCost = SpendCredit(postCMQCost);

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
                if(HighLogic.LoadedSceneIsEditor)
                    SpaceCenterManagement.Instance.IsEditorRecalcuationRequired = true;
            }
        }

        private void OnPartUpgradePurchased(PartUpgradeHandler.Upgrade up)
        {
            UnlockCreditUtility.StoredUpgradeEntryCost = up.entryCost;
            float remainingCost = ProcessCredit(up.entryCost, up.techRequired);
            up.entryCost = remainingCost;
            if (HighLogic.LoadedSceneIsEditor)
                SpaceCenterManagement.Instance.IsEditorRecalcuationRequired = true;
        }

        public override void OnLoad(ConfigNode node)
        {
        }

        public override void OnSave(ConfigNode node)
        {
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
