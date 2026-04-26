using RealFuels;
using ROUtils.DataTypes;
using RP0.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RP0.Addons
{
    [KSPScenario((ScenarioCreationOptions)480, [GameScenes.EDITOR])]
    public class RnDUpgradeTips : ScenarioModule
    {
        private class UpgradeLine
        {
            private readonly string _id;
            private readonly string[] _upgradeNames;
            private readonly string _moduleName;
            private readonly string _displayName;
            private bool _subscribedToPAWEvent;

            public int bestUpgrade { get; private set; } = -1;
            public int bestUnpurchasedUpgrade { get; private set; } = -1;

            public UpgradeLine(string id, string moduleName, string displayName, string[] names)
            {
                _id = id;
                _moduleName = moduleName;
                _displayName = displayName;
                _upgradeNames = names;
                bestUpgrade = Instance.GetOrCreateUpgradeIndex(id);
                UpdateUpgradeInfo();
            }

            public void OnDestroy()
            {
                if (_subscribedToPAWEvent) GameEvents.onPartActionUIShown.Remove(OnPartActionUIShown);
            }

            public PartUpgradeHandler.Upgrade GetUpgrade(int index)
            {
                return PartUpgradeManager.Handler.GetUpgrade(_upgradeNames[index]);
            }

            public void UpdateUpgradeInfo()
            {
                bestUnpurchasedUpgrade = -1;
                for (int i = bestUpgrade + 1; i < _upgradeNames.Length; ++i)
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
                Instance.UpdateUpgradeIndex(_id, bestUpgrade);
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
                            Instance.UpdateUpgradeIndex(_id, bestUpgrade);
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
                        Instance.UpdateUpgradeIndex(_id, bestUpgrade);
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

        [KSPField(isPersistant = true)]
        private PersistentDictionaryValueTypes<string, int> _bestUpgrades = new PersistentDictionaryValueTypes<string, int>();

        public override void OnAwake()
        {
            if (Instance != null)
                Destroy(Instance);
            Instance = this;
        }

        internal void Start()
        {
            string[] fairings = { "I", "II", "III", "IV", "V", "VI" };
            for (int i = fairings.Length - 1; i >= 0; --i)
            {
                fairings[i] = "PFTech-Fairing-" + fairings[i];
            }
            _fairings = new UpgradeLine("fairings", "ProceduralFairingSide", "Fairing Density", fairings);

            string[] hardDrives = { "Early", "Basic", "1961", "1964", "1969", "1975", "1990", "1998", "2010", "2020" };
            for (int i = hardDrives.Length - 1; i >= 0; --i)
            {
                hardDrives[i] = "HDD-Upgrade-" + hardDrives[i];
            }
            _hardDrives = new UpgradeLine("storage", "ModuleProceduralAvionics", "Data Storage", hardDrives);

            string[] mli = new string[5];
            for (int i = 0; i < 5; ++i)
            {
                mli[i] = $"MLI.Upgrade{i+1}";
            }
            _mli = new UpgradeLine("mli", "ModuleFuelTanks", "Multi-Layer Insulation", mli);

            _x2 = new UpgradeLine("x2", "ModuleUnpressurizedCockpit", "X-1 Service Ceiling", ["X2CockpitUpgrade"]);
        }

        public void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            _fairings.OnDestroy();
            _hardDrives.OnDestroy();
            _mli.OnDestroy();
            _x2.OnDestroy();
        }

        public int GetOrCreateUpgradeIndex(string id)
        {
            if (_bestUpgrades.TryGetValue(id, out int index))
                return index;
            return _bestUpgrades[id] = -1;
        }

        public void UpdateUpgradeIndex(string id, int index)
        {
            RP0Debug.Log($"Upgrade {id} set to {index}");
            _bestUpgrades[id] = index;
        }
    }
}
