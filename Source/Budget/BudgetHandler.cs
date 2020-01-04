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
        private int budgetCounter = 0;

        [KSPField(isPersistant = true)]
        private double reputation = -double.MaxValue;

        [KSPField(isPersistant = true)]
        private double reputationNaught = 0;

        [KSPField(isPersistant = true)]
        private double buffer = 0;

        [KSPField(isPersistant = true)]
        private double bufferNaught = 0;

        [KSPField(isPersistant = true)]
        private double lastPayout = 0;

        [KSPField(isPersistant = true)]
        private double totalPayout = 0;

        [KSPField(isPersistant = true)]
        private double lastReputation= 0;

        [KSPField(isPersistant = true)]
        private string BudgetAlarmID = "";

        // Quarterly updates
        private const int BudgetPeriodMonths = 3;

        // Define base budget behavior
        private const double BaseBudgetStart = 20000 * BudgetPeriodMonths / 12;
        private const double BaseBudgetEnd = 200000 * BudgetPeriodMonths / 12;

        // AFter 20 years base budget will no longer grow
        private const int StartToEndPeriods = 20 / BudgetPeriodMonths * 12;

        // How much reputation is released as cash each period
        private const float ReputationDecayFactor = 0.15f;

        // How much rep is transfered from the buffer to the releasable funds bucket
        private const float BufferDecayFactor = 0.2f;

        // How much cash per released funds
        private const float ReputationToFundsFactor = 1000;

        private static readonly DateTime Epoch = new DateTime(1951, 1, 1);

        public static BudgetHandler Instance { get; private set; } = null;

        public void Start()
        {
            if (reputation == -double.MaxValue) // Should only happen when starting the game
            {
                reputation = Reputation.CurrentRep;
                lastReputation = reputation;
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

        private void Update()
        {
            if (HighLogic.CurrentGame == null)
            {
                return;
            }
            if (KACWrapper.APIReady && HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().SetBudgetAlarms && BudgetAlarmID == "" && nextUpdate != 0)
            {
                ScheduleNextUpdate();  // If SetBudgetAlarms setting changes we need to schedule.
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
            if (KACWrapper.APIReady && HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().SetBudgetAlarms)
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
            // Message outputter
            static void StockMessage(string title, string text)
            {

                MessageSystem.Message m = new MessageSystem.Message(title, text, MessageSystemButton.MessageButtonColor.GREEN, MessageSystemButton.ButtonIcons.MESSAGE);
                MessageSystem.Instance.AddMessage(m);
            }

            // Track initial bucket values
            reputationNaught = reputation;
            bufferNaught = buffer;

            // Get transfer/conversion amounts
            var baseBudget = GetBaseBudget();
            var repBudget = GetRepBudget();
            var repTransfer = CalculateBufferTransfer();

            // Calculate payout
            var payout = baseBudget + repBudget;

            // Track payout to date
            totalPayout += payout;

            // Transfer from buffer to releasable bucket (also remove released funds from that bucket)
            reputation += repTransfer - reputation * ReputationDecayFactor;
            buffer -= repTransfer;

            // Release as funds
            Funding.Instance.AddFunds(payout, TransactionReasons.None);
            budgetCounter++;

            // Calculate things for the budget report
            var maintenanceReputation = Math.Ceiling(CalculateMaintenanceReputation());
            var oldGrowth = (payout - lastPayout) / lastPayout;
            var newGrowth = (GetBudget() - payout) / payout;
            if (budgetCounter == 1) oldGrowth = 0;             
            
            // Display budget report
            var infoMessage =
                String.Format("Budget Report for {0}\n", KSPUtil.PrintDate(BudgetHandler.Instance.nextUpdate, false)) +
                              "-----------------------------------------\n\n" +
                String.Format("Budget payout:\t\t\t\t{0:N0}\n", payout) +
                String.Format("Total budget to date:\t\t\t{0:N0}\n\n", totalPayout) +
                String.Format("Previous payout:\t\t\t{0:N0}\n", lastPayout) +
                String.Format("Growth from last period:\t\t{0:F1}%\n", oldGrowth * 100) +
                String.Format("Projected next payout:\t\t{0:N0}\n", GetBudget()) +
                String.Format("Projected next period growth:\t{0:F1}%\n\n", newGrowth * 100) +
                String.Format("Reputation earned last period:\t{0:F0}\n", GetOldReputation() - lastReputation) +
                String.Format("Reputation decayed last period:\t{0:F0}\n", GetOldReputation() - GetTotalReputation()) +
                String.Format("Net reputation change:\t\t{0:F0}\n\n", GetTotalReputation() - lastReputation) +
                String.Format("Current total reputatation:\t\t{0:F0}\n\n", GetTotalReputation());

            Debug.Log($"[RP0] " + infoMessage + String.Format("Rep change to maintain funding: {0:F0} rep", maintenanceReputation));

            if (budgetCounter == 1) // Young program
                infoMessage += "Welcome to the space race. Your new program is young and poorly funded. To increase your funding, you will need to grow its reputation by setting records and completing contracts.";
            else if (oldGrowth > 0.1) // Saw growth last quarter
            {
                if (newGrowth > 0.075) // Growing big 
                    infoMessage += "Congratulations! Your achievements have been very impressive. Your funding is on an upward trend and shows no signs of stopping. Your funders are happy and the near future looks bright.";
                else if (newGrowth > 0.05) // Coasting 
                    infoMessage += "Your recent accomplishments mean your program is projected to see substantial funding growth even if you don't do anything this period. Keep up the good work.";
                else if (newGrowth < 0)
                    infoMessage += String.Format("Congratulations on the recent sucesses. However, now is no time to slow down. You will need to earn at least {0:F0} reputation to maintain your current funding.", maintenanceReputation); // Unlikely to happen
                else // Nothing to worry about
                    infoMessage += "Congratulations on your recent sucesses. Your funders are happy to maintain your increased funding for the next period. Further achievements will prevent your program from stagnating.";
            }
            else 
            {
                if (newGrowth > 0.03) // Coasting 
                    infoMessage += "Your recent publicity has been excellent and your funding is projected to rise even if you don't do anything this period. Don't slow down now!"; // Should be pretty rare
                else if (oldGrowth < -0.025) // Need more reputation
                    infoMessage += String.Format("Your program has been stagnating badly and your funders are unimpressed. You must earn at least {0:F0} reputation to avoid further cuts.", maintenanceReputation);
                else if (oldGrowth < 0 && newGrowth < -0.01) // Need more reputation
                    infoMessage += String.Format("Your funders are disappointed with your sluggish progress and are threatening to cut funding. You need to earn at least {0:F0} reputation to maintain your current funding.", maintenanceReputation);
                else if (newGrowth < 0) // Need more reputation
                    infoMessage += String.Format("Your program has been doing well but any inactivity now will lose the trust of your funders. You will need to earn at least {0:F0} reputation to maintain your current funding.", maintenanceReputation);
                else // Nothing to worry about
                    infoMessage += "Your funding is projected to stay relatively constant over the next period. While you have little to worry about right now, you'll soon need to earn more reputation to avoid loss of future funding.";
            }

            StockMessage("Budget Report", infoMessage);

            lastPayout = payout;
            lastReputation = GetTotalReputation();
        }

        private double CalculateBufferTransfer() => Math.Max(buffer * BufferDecayFactor, 0);

        private double CalculateMaintenanceReputation() => (ReputationDecayFactor * reputationNaught / BufferDecayFactor) - bufferNaught - ((CalculateBaseFunding(budgetCounter) - CalculateBaseFunding(budgetCounter-1)) / ReputationDecayFactor / BufferDecayFactor / ReputationToFundsFactor);

        private double CalculateBaseFunding(int period) => BaseBudgetStart * Math.Pow(BaseBudgetEnd / BaseBudgetStart, (float)Math.Min(StartToEndPeriods, period) / StartToEndPeriods);

        public double GetTotalReputation() => reputation + buffer;

        public double GetOldReputation() => reputationNaught + bufferNaught;

        public double GetLastPayout() => lastPayout;

        public double GetBaseBudget() => CalculateBaseFunding(budgetCounter);

        public double GetRepBudget() => Math.Max((reputation + CalculateBufferTransfer()) * ReputationDecayFactor, 0) * ReputationToFundsFactor;

        public double GetBudget() => GetBaseBudget() + GetRepBudget();
    }
}
