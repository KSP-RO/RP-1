using RealFuels;
using RP0.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RP0.Addons
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class RnDUpgradeTips : MonoBehaviour
    {
        private class UpgradeLine
        {
            private readonly string[] _upgradeNames;
            private readonly string _moduleName;
            private readonly string _displayName;
            private bool _subscribedToPAWEvent;

            public int bestUpgrade { get; private set; } = -1;
            public int bestUnpurchasedUpgrade { get; private set; } = -1;

            public UpgradeLine(string moduleName, string displayName, string[] names)
            {
                _moduleName = moduleName;
                _displayName = displayName;
                _upgradeNames = names;
            }

            public void OnDestroy()
            { 
                if (_subscribedToPAWEvent) GameEvents.onPartActionUIShown.Remove(OnPartActionUIShown);
            }

            public PartUpgradeHandler.Upgrade GetUpgrade(int index)
            {
                return PartUpgradeManager.Handler.GetUpgrade(_upgradeNames[index]);
            }

            public int UpdateUpgradeInfo(int skipIndex)
            {
                bestUpgrade = skipIndex;
                bestUnpurchasedUpgrade = -1;
                for (int i = skipIndex + 1; i < _upgradeNames.Length; ++i)
                {
                    if (PartUpgradeManager.Handler.IsUnlocked(_upgradeNames[i]))
                        bestUpgrade = i;
                    else if (PartUpgradeManager.Handler.IsAvailableToUnlock(_upgradeNames[i]))
                        bestUnpurchasedUpgrade = i;
                }
                if (bestUnpurchasedUpgrade > bestUpgrade && !_subscribedToPAWEvent)
                {
                    _subscribedToPAWEvent = true;
                    GameEvents.onPartActionUIShown.Add(OnPartActionUIShown);
                }
                return bestUpgrade;
            }

            private void OnPartActionUIShown(UIPartActionWindow paw, Part part)
            {
                if (bestUnpurchasedUpgrade > bestUpgrade && part.Modules.Contains(_moduleName))
                    ShowAvailableTechTip();
            }

            private void ShowAvailableTechTip()
            {
                List<DialogGUIBase> controls = new List<DialogGUIBase>();
                string msg = $"A new <color=orange>{_displayName}</color> upgrade is available, and must be purchased here or in the R&D building before it can be used.\n" + 
                             "<color=orange>NOTE: For upgrades to apply, either a scene change must occur, or affected parts must be deleted and replaced.</color>";
                controls.Add(new DialogGUILabel(msg));

                var upgrade = GetUpgrade(bestUnpurchasedUpgrade);

                string txt = $"{upgrade.title}: {upgrade.description}\n";
                var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.PartOrUpgradeUnlock, -upgrade.entryCost, 0f, 0f);
                string costStr = cmq.GetCostLineOverride(true, false, false, true);
                double trueTotal = -cmq.GetTotal(CurrencyRP0.Funds, false);
                double invertCMQOp = upgrade.entryCost / trueTotal;
                double creditAmtToUse = Math.Min(trueTotal, UnlockCreditHandler.Instance.TotalCredit);
                cmq.AddPostDelta(CurrencyRP0.Funds, creditAmtToUse, true);
                string afterCreditLine = cmq.GetCostLineOverride(true, false, true, true, true);
                if (string.IsNullOrEmpty(afterCreditLine))
                    afterCreditLine = "free";
                var button = new DialogGUIButtonWithTooltip($"Unlock ({afterCreditLine})",
                    () =>
                    {
                        Harmony.RFECMPatcher.techNode = upgrade.techRequired;
                        bool success = EntryCostManager.Instance.PurchaseConfig(upgrade.name);
                        Harmony.RFECMPatcher.techNode = null;
                        if (success)
                        {
                            PartUpgradeManager.Handler.SetUnlocked(upgrade.name, true);
                            GameEvents.OnPartUpgradePurchased.Fire(upgrade);
                            bestUpgrade = bestUnpurchasedUpgrade;
                            UpdateShownUpgradeIndex(_displayName, bestUpgrade);
                        }
                    }, () => cmq.CanAfford(), 100, -1, true) 
                    { tooltipText = $"Spending {creditAmtToUse:N0} unlock credit\n(Base cost {costStr})" };
                controls.Add(new DialogGUIHorizontalLayout(TextAnchor.MiddleLeft,
                                new DialogGUILabel(txt, expandW: true),
                                button));
                controls.Add(new DialogGUIFlexibleSpace());
                controls.Add(new DialogGUIHorizontalLayout(sw: true, sh: false, 
                    new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"), () => { }), 
                    new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#rp0_GameplayTip_DontShowAgain"), () => 
                    { 
                        bestUpgrade = bestUnpurchasedUpgrade;
                        UpdateShownUpgradeIndex(_displayName, bestUpgrade);
                    }
                )));


                var dlgRect = new Rect(0.5f, 0.5f, 400, 100);

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new MultiOptionDialog("newAvailablePartUpgradePopup",
                        null,
                        $"New {_displayName} Upgrade",
                        HighLogic.UISkin,
                        dlgRect,
                        controls.ToArray()),
                    false,
                    HighLogic.UISkin).HideGUIsWhilePopup();
            }
        }

        public static RnDUpgradeTips Instance { get; private set; }

        private UpgradeLine _fairings;
        private UpgradeLine _hardDrives;
        private UpgradeLine _mli;
        private UpgradeLine _x2;

        internal void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;
        }

        internal void Start()
        {
            string[] fairings = { "I", "II", "III", "IV", "V", "VI" };
            for (int i = fairings.Length - 1; i >= 0; --i)
            {
                fairings[i] = "PFTech-Fairing-" + fairings[i];
            }
            _fairings = new UpgradeLine("ProceduralFairingSide", "Fairing Density", fairings);

            string[] hardDrives = { "Early", "Basic", "1961", "1964", "1969", "1975", "1990", "1998", "2010", "2020" };
            for (int i = hardDrives.Length - 1; i >= 0; --i)
            {
                hardDrives[i] = "HDD-Upgrade-" + hardDrives[i];
            }
            _hardDrives = new UpgradeLine("ModuleProceduralAvionics", "Data Storage", hardDrives);

            string[] mli = new string[5];
            for (int i = 0; i < 5; ++i)
            {
                mli[i] = $"MLI.Upgrade{i+1}";
            }
            _mli = new UpgradeLine("ModuleFuelTanks", "Multi-Layer Insulation", mli);

            _x2 = new UpgradeLine("ModuleUnpressurizedCockpit", "X-1 Service Ceiling", ["X2CockpitUpgrade"]);
            
            var rp0Settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
            rp0Settings.FairingTLTipShown = _fairings.UpdateUpgradeInfo(rp0Settings.FairingTLTipShown);
            rp0Settings.HardDriveTipShown = _hardDrives.UpdateUpgradeInfo(rp0Settings.HardDriveTipShown);
            rp0Settings.MLITipShown = _mli.UpdateUpgradeInfo(rp0Settings.MLITipShown);
            rp0Settings.X2TipShown = _x2.UpdateUpgradeInfo(rp0Settings.X2TipShown);
        }

        internal void OnDestroy()
        {
            _fairings.OnDestroy();
            _hardDrives.OnDestroy();
            _mli.OnDestroy();
            _x2.OnDestroy();
        }

        private static void UpdateShownUpgradeIndex(string displayName, int index)
        {
            var rp0Settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
            switch (displayName)
            {
                case "Fairing Density":
                    rp0Settings.FairingTLTipShown = index;
                    break;
                case "Data Storage":
                    rp0Settings.HardDriveTipShown = index;
                    break;
                case "Multi-Layer Insulation":
                    rp0Settings.MLITipShown = index;
                    break;
                case "X-1 Service Ceiling":
                    rp0Settings.X2TipShown = index;
                    break;
            }
        }
    }
}
