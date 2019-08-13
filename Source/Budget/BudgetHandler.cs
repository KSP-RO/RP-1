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

        public const int BudgetPeriodMonths = 3;
        public const double BaseBudgetStart = 50000 * BudgetPeriodMonths / 12;
        public const double BaseBudgetEnd = 200000 * BudgetPeriodMonths / 12;
        public const int StartToEndPeriods = 12 * 4;
        public const float ReputationDecayFactor = 0.125f;
        public const float ReputationToFundsFactor = 1000;
        public static readonly DateTime Epoch = new DateTime(1951, 1, 1);

        public static BudgetHandler Instance { get; private set; } = null;

        public override void OnAwake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;
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
            nextUpdate = Epoch.AddMonths(BudgetPeriodMonths * (budgetCounter+1)).Date.Subtract(Epoch).TotalSeconds;
        }

        private void PayBudget()
        {
            Funding.Instance.AddFunds(GetBudget(), TransactionReasons.None);
            budgetCounter++;
        }

        private double GetBudget()
        {
            var baseBudget = GetBaseBudget();
            var repBudget = GetRepBudget();
            var budget = baseBudget + repBudget;
            Debug.Log($"[RP0] Budget payout: {budget} (Base: {baseBudget}, Rep: {repBudget})");
            return budget;
        }

        private double GetBaseBudget() => BaseBudgetStart * Math.Pow(BaseBudgetEnd / BaseBudgetStart, (float) Math.Min(StartToEndPeriods, budgetCounter) / StartToEndPeriods);

        private double GetRepBudget()
        {
            var reputationToConvert = Reputation.CurrentRep * ReputationDecayFactor;
            Reputation.Instance.AddReputation(-reputationToConvert, TransactionReasons.None);
            return reputationToConvert * ReputationToFundsFactor;
        }
    }
}
