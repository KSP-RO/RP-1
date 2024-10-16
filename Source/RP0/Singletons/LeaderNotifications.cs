using ContractConfigurator;
using Contracts;
using KSP.Localization;
using KSP.UI.Screens;
using ROUtils;
using RP0.Leaders;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static KSP.UI.Screens.MessageSystem;

namespace RP0.Singletons
{
    public class LeaderNotifications : HostedSingleton
    {
        public LeaderNotifications(SingletonHost host) : base(host) { }

        public override void Awake()
        {
            SubscribeToEvents();
        }

        public void SubscribeToEvents()
        {
            GameEvents.OnTechnologyResearched.Add(OnTechnologyResearched);
            GameEvents.Contract.onCompleted.Add(OnContractComplete);
        }

        public static void ShowNotificationForNewLeaders(IEnumerable<StrategyConfigRP0> newLeaders)
        {
            string leaderString = string.Join("\n", newLeaders.Select(s => s.Title));
            if (!string.IsNullOrEmpty(leaderString))
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "LeaderUnlocked",
                                             Localizer.Format("#rp0_Leaders_LeadersUnlockedTitle"),
                                             Localizer.Format("#rp0_Leaders_LeadersUnlocked") + leaderString,
                                             Localizer.GetStringByTag("#autoLOC_190905"),
                                             true,
                                             HighLogic.UISkin).HideGUIsWhilePopup();
            }
        }

        public static void AddNewLeadersUnlockedMessage(IEnumerable<StrategyConfigRP0> newLeaders)
        {
            var leaderLines = newLeaders.Select(s => s.title)
                                        .Distinct()
                                        .Select(s => $"• {s}");
            string leaderString = string.Join("\n", leaderLines);
            if (!string.IsNullOrEmpty(leaderString))
            {
                MessageSystem.Instance?.AddMessage(
                    new Message(Localizer.Format("#rp0_Leaders_LeadersUnlockedTitle"),
                                Localizer.Format("#rp0_Leaders_LeadersUnlocked") + leaderString,
                                MessageSystemButton.MessageButtonColor.BLUE,
                                MessageSystemButton.ButtonIcons.MESSAGE));
            }
        }

        private void OnContractComplete(Contract data)
        {
            if (data is ConfiguredContract cc)
            {
                var leadersUnlockedByThis = LeaderUtils.GetLeadersUnlockedByContract(cc);
                AddNewLeadersUnlockedMessage(leadersUnlockedByThis);
            }
        }

        private void OnTechnologyResearched(GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> data)
        {
            var leadersUnlockedByThis = LeaderUtils.GetLeadersUnlockedByTech(data.host.techID);
            AddNewLeadersUnlockedMessage(leadersUnlockedByThis);
        }
    }
}
