using System;
using UnityEngine;

namespace RP0
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class BudgetHandler : ScenarioModule
    {
        [KSPField(isPersistant = true)]
        public double nextUpdate = 0;

        [KSPField(isPersistant = true)]
        public int budgetCounter = 0;

        [KSPField(isPersistant = true)]
        public double reputation = -double.MaxValue;

        [KSPField(isPersistant = true)]
        public double payout = 0;

        public const int BudgetPeriodMonths = 3;
        public const double BaseBudgetStart = 20000 * BudgetPeriodMonths / 12;
        public const double BaseBudgetEnd = 200000 * BudgetPeriodMonths / 12;
        public const int StartToEndPeriods = 20 / BudgetPeriodMonths * 12;
        public const float ReputationDecayFactor = 0.1f;
        public const float ReputationToFundsFactor = 1000;
        public static readonly DateTime Epoch = new DateTime(1951, 1, 1);

        public static BudgetHandler Instance { get; private set; } = null;

        public void Start()
        {
            if(reputation == -double.MaxValue)
            {
                reputation = Reputation.CurrentRep;
            }
        }

        public override void OnAwake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;
            GameEvents.Modifiers.OnCurrencyModified.Add(OnCurrencyModified);
        }

        public void OnDestroy()
        {
            GameEvents.Modifiers.OnCurrencyModified.Remove(OnCurrencyModified);
        }

        private void OnCurrencyModified(CurrencyModifierQuery data)
        {
            var reputationChange = data.GetInput(Currency.Reputation);
            if (reputationChange != 0)
            {
                if ((data.reason & TransactionReasons.Contracts) > 0)
                {
                    Debug.Log($"[RP0] Reputation change valid for budgets:");
                    reputation += reputationChange;
                }
                Debug.Log($"[RP0] Reputation changed by: {reputationChange} (reason: {data.reason}, total reputation: {reputation})");
            }
        }

        public void Update()
        {
            if (HighLogic.CurrentGame == null)
            {
                return;
            }

            if (nextUpdate > Planetarium.GetUniversalTime())
            {
                return;
            }
            StopTimeWarp();
            PayBudget();
            ScheduleNextUpdate();
        }

        private static void StopTimeWarp() => TimeWarp.SetRate(0, true);

        private void ScheduleNextUpdate()
        {
            nextUpdate = Epoch.AddMonths(BudgetPeriodMonths * budgetCounter).Date.Subtract(Epoch).TotalSeconds;
        }

        private void PayBudget()
        {
            payout = GetBudget();
            Funding.Instance.AddFunds(payout, TransactionReasons.None);
            budgetCounter++;
        }

        private double GetBudget()
        {
            var baseBudget = GetBaseBudget();
            var repBudget = GetRepBudget();
            reputation -= repBudget / ReputationToFundsFactor;
            var budget = baseBudget + repBudget;
            Debug.Log($"[RP0] Budget payout: {budget} (Base: {baseBudget}, Rep: {repBudget})");
            return budget;
        }

        public double GetBaseBudget() => BaseBudgetStart * Math.Pow(BaseBudgetEnd / BaseBudgetStart, (float) Math.Min(StartToEndPeriods, budgetCounter) / StartToEndPeriods);

        public double GetRepBudget()
        {
            var reputationToConvert = Math.Max(reputation * ReputationDecayFactor, 0);
            return reputationToConvert * ReputationToFundsFactor;
        }
    }
}
