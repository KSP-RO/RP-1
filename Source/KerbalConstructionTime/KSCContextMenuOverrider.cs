﻿using KSP.UI;
using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using UniLinq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Upgradeables;

namespace KerbalConstructionTime
{
    /// <summary>
    /// This class attempts to override the KSC Upgrade buttons so that KCT can implement it's own form of KSC upgrading
    /// </summary>
    public class KSCContextMenuOverrider
    {
        private static Dictionary<string, Dictionary<int, string>> _techGatings = null;

        private readonly KSCFacilityContextMenu _menu;

        public static bool AreTextsUpdated { get; set; } = false;

        public KSCContextMenuOverrider(KSCFacilityContextMenu menu)
        {
            _menu = menu;

            if (!AreTextsUpdated)
            {
                AreTextsUpdated = OverrideFacilityDescriptions();
            }
        }

        public IEnumerator OnContextMenuSpawn()
        {
            yield return new WaitForFixedUpdate();
            if (PresetManager.Instance.ActivePreset.GeneralSettings.KSCUpgradeTimes && _menu != null)
            {
                SpaceCenterBuilding hostBuilding = _menu.host;
                KCTDebug.Log($"Trying to override upgrade button of menu for {hostBuilding.facilityName}");
                Button button = _menu.UpgradeButton;
                TMPro.TextMeshProUGUI buttonText = _menu.UpgradeButtonText;
                if (button != null)
                {
                    KCTDebug.Log("Found upgrade button, overriding it.");

                    if (buttonText != null && hostBuilding != null)
                    {
                        float upgradeCost = hostBuilding.Facility.GetUpgradeCost();
                        RP0.CurrencyModifierQueryRP0 upgradeQuery = RP0.CurrencyModifierQueryRP0.RunQuery(RP0.TransactionReasonsRP0.StructureConstruction, -upgradeCost, 0f, 0f);
                        string upgradeString = upgradeQuery.GetCostLineOverride(true, true, false, false, "\n");
                        buttonText.text = KSP.Localization.Localizer.Format("#autoLOC_475331", upgradeString);
                    }

                    button.onClick = new Button.ButtonClickedEvent();    //Clear existing KSP listener
                    button.onClick.AddListener(HandleUpgrade);

                    if (GetFacilityID().IndexOf("launchpad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         GetFacilityID().IndexOf("SpaceplaneHangar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         GetFacilityID().IndexOf("VehicleAssemblyBuilding", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         GetFacilityID().IndexOf("runway", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        button.interactable = false;
                        var hov = button.gameObject.GetComponent<UIOnHover>();
                        hov.gameObject.DestroyGameObject();
                    }
                }
                else
                {
                    throw new Exception("UpgradeButton not found. Cannot override.");
                }
            }
        }

        private bool OverrideFacilityDescriptions()
        {
            if (ScenarioUpgradeableFacilities.Instance == null)
                return false;

            try
            {
                Dictionary<string, ScenarioUpgradeableFacilities.ProtoUpgradeable> upgrades = ScenarioUpgradeableFacilities.protoUpgradeables;
                foreach (ScenarioUpgradeableFacilities.ProtoUpgradeable upgrade in upgrades.Values)
                {
                    foreach (UpgradeableFacility facilityUpgrade in upgrade.facilityRefs)
                    {
                        double mult = facilityUpgrade.UpgradeLevels.Length > 1 ? 1d / (facilityUpgrade.UpgradeLevels.Length - 1d) : 1d;
                        for (int i = 0; i < facilityUpgrade.UpgradeLevels.Length; i++)
                            RP0.LocalizationHandler.UpdateFacilityLevelStats(facilityUpgrade.UpgradeLevels[i], i, i * mult);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }

            return true;
        }

        protected static void CheckLoadDict()
        {
            if (_techGatings != null)
                return;

            _techGatings = new Dictionary<string, Dictionary<int, string>>();
            ConfigNode node = null;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("KCTBUILDINGTECHS"))
                node = n;

            if (node == null)
                return;

            foreach (ConfigNode n in node.nodes)
            {
                string fac = "SpaceCenter/" + n.name;
                var lst = new Dictionary<int, string>();

                foreach (ConfigNode.Value v in n.values)
                    lst.Add(int.Parse(v.name), v.value);

                _techGatings.Add(fac, lst);
            }
        }

        protected string GetTechGate(string facId, int level)
        {
            CheckLoadDict();
            if (_techGatings == null)
                return string.Empty;

            if (_techGatings.TryGetValue(facId, out var d))
                if (d.TryGetValue(level, out string node))
                    return node;

            return string.Empty;
        }

        internal void ProcessUpgrade()
        {
            int oldLevel = _menu.level;
            KCTDebug.Log($"Upgrading from level {oldLevel}");

            string facilityID = GetFacilityID();
            SpaceCenterFacility? facilityType = GetFacilityType();

            string gate = GetTechGate(facilityID, oldLevel + 1);
            KCTDebug.Log($"Gate for {facilityID}: {gate}");
            if (!string.IsNullOrEmpty(gate))
            {
                if (ResearchAndDevelopment.GetTechnologyState(gate) != RDTech.State.Available)
                {
                    PopupDialog.SpawnPopupDialog(new MultiOptionDialog("kctUpgradeLackTech",
                            $"Can't upgrade this facility. Requires {KerbalConstructionTimeData.techNameToTitle[gate]}.",
                            "Lack Tech to Upgrade",
                            HighLogic.UISkin,
                            new DialogGUIButton(KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"), () => { })),
                            false,
                            HighLogic.UISkin);

                    return;
                }
            }

            var upgrading = new FacilityUpgrade(facilityType, facilityID, oldLevel + 1, oldLevel, facilityID.Split('/').Last());

            if (!upgrading.AlreadyInProgress())
            {
                float cost = _menu.upgradeCost;
                upgrading.SetBP(cost);
                upgrading.Cost = cost;
                double rate = Utilities.GetConstructionRate(0, KCTGameStates.ActiveKSC, upgrading.FacilityType);
                double time = upgrading.BP / rate;
                double costPerDay = cost / (time / 86400d);

                InputLockManager.SetControlLock(ControlTypes.KSC_ALL, "KCTPopupLock");
                DialogGUIBase[] options = new DialogGUIBase[2];
                options[0] = new DialogGUIButton("Yes", () => {
                    KCT_GUI.RemoveInputLocks();

                    KCTGameStates.ActiveKSC.FacilityUpgrades.Add(upgrading);

                    try
                    {
                        KCTEvents.OnFacilityUpgradeQueued?.Fire(upgrading);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    ScreenMessages.PostScreenMessage("Facility upgrade requested!", 4f, ScreenMessageStyle.UPPER_CENTER);
                    KCTDebug.Log($"Facility {facilityID} upgrade requested to lvl {oldLevel + 1} for {cost} funds, resulting in a BP of {upgrading.BP}");
                
                });
                options[1] = new DialogGUIButton("No", KCT_GUI.RemoveInputLocks);
                PopupDialog.SpawnPopupDialog(new MultiOptionDialog("kctUpgradeConfirm",
                            $"Upgrade facility?\nExpected cost/day: {RP0.CurrencyModifierQueryRP0.RunQuery(RP0.TransactionReasonsRP0.StructureConstruction, -costPerDay, 0d, 0d).GetCostLineOverride(true, false, false, true)}\n"
                            + (GameSettings.SHOW_DEADLINES_AS_DATES ? $"Completes on {KSPUtil.PrintDate(time + RP0.KSPUtils.GetUT(), false)}"
                                : $"Completes in {KSPUtil.PrintDateDelta(time, false)}"),
                            "Upgrade",
                            HighLogic.UISkin,
                            options),
                            false,
                            HighLogic.UISkin);
            }
            else if (oldLevel + 1 != upgrading.CurrentLevel)
            {
                ScreenMessages.PostScreenMessage("Facility is already being upgraded!", 4f, ScreenMessageStyle.UPPER_CENTER);
                KCTDebug.Log($"Facility {facilityID} tried to upgrade to lvl {oldLevel + 1} but already in list!");
            }
        }

        internal void HandleUpgrade()
        {
            ProcessUpgrade();

            _menu.Dismiss(KSCFacilityContextMenu.DismissAction.None);
        }

        public string GetFacilityID()
        {
            return _menu.host.Facility.id;
        }

        public SpaceCenterFacility? GetFacilityType()
        {
            var scb = _menu.host;
            if (scb is AdministrationFacility) return SpaceCenterFacility.Administration;
            if (scb is AstronautComplexFacility) return SpaceCenterFacility.AstronautComplex;
            if (scb is LaunchSiteFacility lpFacility && lpFacility.facilityType == EditorFacility.VAB) return SpaceCenterFacility.LaunchPad;
            if (scb is LaunchSiteFacility rwFacility && rwFacility.facilityType == EditorFacility.SPH) return SpaceCenterFacility.Runway;
            if (scb is MissionControlBuilding) return SpaceCenterFacility.MissionControl;
            if (scb is RnDBuilding) return SpaceCenterFacility.ResearchAndDevelopment;
            if (scb is SpacePlaneHangarBuilding) return SpaceCenterFacility.SpaceplaneHangar;
            if (scb is TrackingStationBuilding) return SpaceCenterFacility.TrackingStation;
            if (scb is VehicleAssemblyBuilding) return SpaceCenterFacility.VehicleAssemblyBuilding;

            // Some mods define custom facilities
            return null;
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
