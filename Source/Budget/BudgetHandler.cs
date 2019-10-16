using System;
using UnityEngine;
using KSP.UI.Screens;
using System.Linq;

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
        public double buffer = 0;

        [KSPField(isPersistant = true)]
        public double payout = 0;

        [KSPField(isPersistant = true)]
        public double  lastreputation= 0;

        [KSPField(isPersistant = true)]
        public string BudgetAlarmID = "";

        public const int BudgetPeriodMonths = 3;
        public const double BaseBudgetStart = 20000 * BudgetPeriodMonths / 12;
        public const double BaseBudgetEnd = 200000 * BudgetPeriodMonths / 12;
        public const int StartToEndPeriods = 20 / BudgetPeriodMonths * 12;
        public const float ReputationDecayFactor = 0.15f;
        public const float BufferDecayFactor = 0.2f;
        public const float ReputationToFundsFactor = 1000;
        public static readonly DateTime Epoch = new DateTime(1951, 1, 1);

        public static BudgetHandler Instance { get; private set; } = null;

        public void Start()
        {
            if (reputation == -double.MaxValue)
            {
                reputation = Reputation.CurrentRep;
                lastreputation = reputation;
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
                    buffer += reputationChange;
                }
                Debug.Log($"[RP0] Reputation changed by: {reputationChange} (reason: {data.reason}, total reputation: {reputation + buffer})");
            }
        }

        public void Update()
        {
            if (HighLogic.CurrentGame == null)
            {
                return;
            }
            if (KACWrapper.APIReady && HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().SetBudgetAlamrs && BudgetAlarmID == "" && nextUpdate != 0)
            {
                ScheduleNextUpdate();  // If SetBudgetAlamrs setting changes we need to schedule.
            }

            if (nextUpdate > Planetarium.GetUniversalTime())
            {
                return;
            }
            StopTimeWarp();
            PayBudget();
            nextUpdate = Epoch.AddMonths(BudgetPeriodMonths * budgetCounter).Date.Subtract(Epoch).TotalSeconds;
            ScheduleNextUpdate();
        }

        private static void StopTimeWarp() => TimeWarp.SetRate(0, true);

        private void ScheduleNextUpdate()
        {
            if (KACWrapper.APIReady && HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().SetBudgetAlamrs)
            {
                if (BudgetAlarmID != "")
                {
                    KACWrapper.KAC.DeleteAlarm(BudgetAlarmID);
                }
                BudgetAlarmID = KACWrapper.KAC.CreateAlarm(KACWrapper.KACAPI.AlarmTypeEnum.Raw, "Next Budget Payout", nextUpdate);
                if (BudgetAlarmID != "")
                {
                    KACWrapper.KACAPI.KACAlarm a = KACWrapper.KAC.Alarms.First(z => z.ID == BudgetAlarmID);
                    a.AlarmAction = KACWrapper.KACAPI.AlarmActionEnum.KillWarpOnly;
                }
            } else
            {
                BudgetAlarmID = ""; 
            }
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
            var repTransfer = GetBufferTransfer();
            var budget = baseBudget + repBudget;
            StockMessage("Budget Report",
                String.Format("Budget Report for {0}\n", KSPUtil.PrintDate(BudgetHandler.Instance.nextUpdate, false)) +
                              "---------------------------------------------------------------\n" +
                String.Format("Last Period Reputation:\t\t{0:F0}\n", lastreputation) +
                String.Format("Reputation earned this period:\t{0:F0}\n", reputation + buffer - lastreputation) +
                String.Format("Reputation converted to funds:\t{0:F1}\n", repBudget/ReputationToFundsFactor) +
                String.Format("Reputation balance:\t\t\t{0:F0}\n\n", reputation + buffer - repBudget / ReputationToFundsFactor) +
                String.Format("Base budget payout: \t\t\t{0:F0}\n", baseBudget) +
                String.Format("Reputation conversion payout:\t{0:F0}\n", repBudget) +
                String.Format("Total payout:\t\t\t\t{0:F0}\n", budget));
            buffer -= repTransfer;
            lastreputation = reputation;
            reputation += repTransfer - repBudget / ReputationToFundsFactor;
            Debug.Log($"[RP0] Budget payout: {budget} (Base: {baseBudget}, Rep: {repBudget})");
            return budget;
        }

        public double GetBaseBudget() => BaseBudgetStart * Math.Pow(BaseBudgetEnd / BaseBudgetStart, (float)Math.Min(StartToEndPeriods, budgetCounter) / StartToEndPeriods);

        public double GetBufferTransfer()
        {
            var reputationToTransfer = Math.Max(buffer * BufferDecayFactor, 0);
            return reputationToTransfer;
        }
        public double GetRepBudget()
        {
            var reputationIncrease = GetBufferTransfer();
            var reputationToConvert = Math.Max((reputation + reputationIncrease) * ReputationDecayFactor, 0);
            return reputationToConvert * ReputationToFundsFactor;
        }
        private void StockMessage(String title, String text)
        {

            MessageSystem.Message m = new MessageSystem.Message(title, text, MessageSystemButton.MessageButtonColor.GREEN, MessageSystemButton.ButtonIcons.MESSAGE);
            MessageSystem.Instance.AddMessage(m);
        }
    }
}
