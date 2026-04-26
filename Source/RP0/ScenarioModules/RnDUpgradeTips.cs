using RealFuels;
using RealFuels.Tanks;
using ROUtils.DataTypes;
using RP0.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RP0.Addons
{
    [KSPScenario((ScenarioCreationOptions)480, [GameScenes.EDITOR])]
    public class RnDUpgradeTips : ScenarioModule
    {
        private class UpgradeLine
        {
            public delegate bool ValidPartModuleFilter(PartModule pm);

            private readonly string _id;
            private readonly string[] _upgradeNames;
            private readonly string _moduleName;
            private readonly string _displayName;
            private bool _subscribedToPAWEvent;
            private readonly ValidPartModuleFilter _filter;

            public int bestUpgrade { get; private set; } = -1;
            public int bestUnpurchasedUpgrade { get; private set; } = -1;

            public UpgradeLine(string id, string moduleName, string displayName, string[] names, ValidPartModuleFilter filter = null)
            {
                _id = id;
                _moduleName = moduleName;
                _displayName = displayName;
                _upgradeNames = names;
                _filter = filter;
                bestUpgrade = _upgradeNames.IndexOf(Instance.GetBestShownUpgradeName(id));
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
                if (bestUpgrade != -1)
                    Instance.UpdateBestShownUpgradeName(_id, _upgradeNames[bestUpgrade]);
            }

            private void OnPartActionUIShown(UIPartActionWindow paw, Part part)
            {
                // If there is any module that matches the given name AND meets the filter requirement, show the tip.
                if (bestUnpurchasedUpgrade > bestUpgrade && part.Modules.modules.Any(pm => pm.ClassName == _moduleName && (_filter == null || _filter(pm))))
                    ShowAvailableTechTip();
            }

            private void ShowAvailableTechTip()
            {
                List<DialogGUIBase> controls = new List<DialogGUIBase>();
                string msg = $"A new <color=orange>{_displayName}</color> upgrade is available, and must be purchased here or in the R&D building before it can be used.\n" + 
                             "<color=orange>NOTE: For upgrades to apply, either a scene change must occur, or affected parts must be deleted and replaced.</color>";
                controls.Add(new DialogGUILabel(msg));

                var upgrade = GetUpgrade(bestUnpurchasedUpgrade);

                string txt = $"<b>{upgrade.title}</b>\n{upgrade.description}";
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
                            Instance.UpdateBestShownUpgradeName(_id, upgrade.name);
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
                        Instance.UpdateBestShownUpgradeName(_id, upgrade.name);
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

        private readonly List<UpgradeLine> _upgradeLines = new List<UpgradeLine>();

        [KSPField(isPersistant = true)]
        private PersistentDictionaryValueTypes<string, string> _bestUpgrades = new PersistentDictionaryValueTypes<string, string>();

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
            _upgradeLines.Add(new UpgradeLine("fairings", "ProceduralFairingSide", "Fairing Density", fairings));

            string[] hardDrives = { "Early", "Basic", "1961", "1964", "1969", "1975", "1990", "1998", "2010", "2020" };
            for (int i = hardDrives.Length - 1; i >= 0; --i)
            {
                hardDrives[i] = "HDD-Upgrade-" + hardDrives[i];
            }
            _upgradeLines.Add(new UpgradeLine("storage", "ModuleProceduralAvionics", "Data Storage", hardDrives));

            string[] mli = new string[5];
            for (int i = 0; i < 5; ++i)
            {
                mli[i] = $"MLI.Upgrade{i+1}";
            }
            _upgradeLines.Add(new UpgradeLine("mli", "ModuleFuelTanks", "Multi-Layer Insulation", mli, pm => {
                // Only fire for conventional or isogrid tanks.
                return pm is ModuleFuelTanks tank && (tank.type.StartsWith("Tank-Sep") || tank.type.StartsWith("Tank-Iso"));
            }));

            _upgradeLines.Add(new UpgradeLine("mliBalloon", "ModuleFuelTanks", "Multi-Layer Insulation (Balloon)", ["RFTech-MLI-UpgradeBalloon"], pm => {
                // Only fire for balloon
                return pm is ModuleFuelTanks tank && tank.type.StartsWith("Tank-Balloon");
            }));

            _upgradeLines.Add(new UpgradeLine("x2", "ModuleUnpressurizedCockpit", "X-1 Service Ceiling", ["X2CockpitUpgrade"]));
        }

        public void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            foreach (UpgradeLine line in _upgradeLines)
                line.OnDestroy();
        }

        public string GetBestShownUpgradeName(string id)
        {
            if (_bestUpgrades.TryGetValue(id, out string name))
                return name;
            return null;
        }

        public void UpdateBestShownUpgradeName(string id, string name)
        {
            RP0Debug.Log($"[RnDUpgradeTips] Upgrade {id} set to {name}");
            _bestUpgrades[id] = name;
        }
    }
}
