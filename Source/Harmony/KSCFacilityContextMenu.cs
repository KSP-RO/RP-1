using HarmonyLib;
using KerbalConstructionTime;
using KSP.UI.Screens;
using UnityEngine;
using KSP.Localization;
using System.Collections.Generic;
using KSP.UI;
using Upgradeables;
using System;
using UnityEngine.UI;
using KSP.UI.TooltipTypes;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(KSCFacilityContextMenu))]
    internal class PatchKSCFacilityContextMenu
    {
        private static Dictionary<string, Dictionary<int, string>> _techGatings = null;

        public static bool AreTextsUpdated = false;


        [HarmonyPrefix]
        [HarmonyPatch("Create")]
        internal static bool Prefix_Create(SpaceCenterBuilding host, Callback<KSCFacilityContextMenu.DismissAction> onMenuDismiss, out KSCFacilityContextMenu __result)
        {
            KSCFacilityContextMenu menu = GameObject.Instantiate(AssetBase.GetPrefab("FacilityContextMenu")).GetComponent<KSCFacilityContextMenu>();
            menu.hasFacility = host != null && host.Facility != null;
            menu.isUpgradeable = ScenarioUpgradeableFacilities.Instance != null && menu.hasFacility && (object)host.Facility != null;
            menu.showDowngradeControls = Input.GetKey(KeyCode.LeftControl);
            menu.name = host.facilityName + " Context Menu";
            menu.anchor = host.transform;
            menu.host = host;
            menu.facilityName = host.buildingInfoName;
            menu.description = host.buildingDescription;
            menu.OnMenuDismiss = onMenuDismiss;
            menu.clampedToScreen = true;
            menu.transform.SetParent(UIMasterController.Instance.mainCanvas.transform, worldPositionStays: false);
            UIMasterController.ClampToScreen((RectTransform)menu.transform, Vector2.one * 50f);
            GameEvents.OnKSCStructureCollapsing.Add(menu.OnKSCStructureEvent);
            GameEvents.OnKSCStructureCollapsed.Add(menu.OnKSCStructureEvent);
            GameEvents.OnKSCStructureRepairing.Add(menu.OnKSCStructureEvent);
            GameEvents.OnKSCStructureRepaired.Add(menu.OnKSCStructureEvent);
            GameEvents.onFacilityContextMenuSpawn.Fire(menu);

            if (!AreTextsUpdated)
            {
                AreTextsUpdated = OverrideFacilityDescriptions();
            }

            __result = menu;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("CreateWindowContent")]
        internal static bool Prefix_CreateWindowContent(KSCFacilityContextMenu __instance)
        {
            bool kctActive = HighLogic.CurrentGame.Mode != Game.Modes.MISSION && !KCT_GUI.IsPrimarilyDisabled && PresetManager.Instance.ActivePreset.GeneralSettings.KSCUpgradeTimes;
            bool upgradeable = !kctActive || IsUpgradeable(__instance.host.Facility);
            bool kctUpgradeBlocked = false;
            string tooltip = null;
            if (kctActive && __instance.hasFacility)
            {
                if (FacilityUpgrade.AlreadyInProgressByID(__instance.host.Facility.id))
                {
                    kctUpgradeBlocked = true;
                    tooltip = Localizer.GetStringByTag("#rp0_FacilityContextMenu_AlreadyUpgrading");
                }
                else if ((float)__instance.host.Facility.FacilityLevel != __instance.host.Facility.MaxLevel)
                {
                    string gate = GetTechGate(__instance.host.Facility.id, __instance.host.Facility.FacilityLevel + 1);
                    kctUpgradeBlocked = gate != null && ResearchAndDevelopment.GetTechnologyState(gate) != RDTech.State.Available;
                    if (kctUpgradeBlocked)
                    {
                        tooltip = Localizer.Format("#rp0_FacilityContextMenu_TechGate", KerbalConstructionTimeData.techNameToTitle[gate]);
                    }
                    else
                    {
                        double cost = __instance.host.Facility.GetUpgradeCost();
                        var facilityType = GetFacilityType(__instance.host);
                        double bp = Formula.GetConstructionBP(cost, facilityType);
                        double rate = KerbalConstructionTime.Utilities.GetConstructionRate(0, KCTGameStates.ActiveKSC, facilityType);
                        double time = bp / rate;
                        double days = time / 86400d;
                        var cmqUpgrade = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StructureConstruction, -cost / days, 0d, 0d);
                        string costLine = cmqUpgrade.GetCostLineOverride(true, false, false, true);
                        if (KCTGameStates.Settings.UseDates)
                            tooltip = Localizer.Format("#rp0_FacilityContextMenu_UpgradeCostDate", costLine, KSPUtil.PrintDate(time + KSPUtils.GetUT(), false));
                        else
                            tooltip = Localizer.Format("#rp0_FacilityContextMenu_UpgradeCostTime", costLine, KSPUtil.PrintDateDelta(time, false));
                    }
                }
            }

            __instance.descriptionText.text = __instance.description;
            __instance.RepairButton.onClick.AddListener(__instance.OnRepairButtonInput);
            __instance.EnterButton.onClick.AddListener(__instance.OnEnterBtnInput);
            __instance.UpgradeButton.onClick.AddListener(__instance.OnUpgradeButtonInput);
            if (upgradeable)
            {
                UIOnHover uIOnHover = __instance.UpgradeButton.gameObject.AddComponent<UIOnHover>();
                uIOnHover.onEnter.AddListener(__instance.OnUpgradeButtonHoverIn);
                uIOnHover.onExit.AddListener(__instance.OnUpgradeButtonHoverOut);
                if (tooltip != null)
                {
                    var tooltipController = __instance.UpgradeButton.gameObject.AddComponent<TooltipController_Text>();
                    var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
                    tooltipController.prefab = prefab;
                    tooltipController.RequireInteractable = false;
                    tooltipController.textString = tooltip;
                    tooltipController.continuousUpdate = false;
                }
            }
            __instance.DemolishButton.onClick.AddListener(__instance.OnDemolishButtonInput);
            __instance.DowngradeButton.onClick.AddListener(__instance.OnDowngradeButtonInput);
            FacilitySetup(__instance, kctActive, upgradeable, kctUpgradeBlocked);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("Dismiss")]
        internal static bool Prefix_Dismiss(KSCFacilityContextMenu __instance, KSCFacilityContextMenu.DismissAction dma)
        {
            GameEvents.onFacilityContextMenuDespawn.Fire(__instance);
            if (dma == KSCFacilityContextMenu.DismissAction.Upgrade && HighLogic.CurrentGame.Mode != Game.Modes.MISSION && !KCT_GUI.IsPrimarilyDisabled && PresetManager.Instance.ActivePreset.GeneralSettings.KSCUpgradeTimes)
            {
                dma = KSCFacilityContextMenu.DismissAction.None;
                ProcessUpgrade(__instance.host);
            }

            __instance.OnMenuDismiss(dma);
            GameEvents.OnKSCStructureCollapsing.Remove(__instance.OnKSCStructureEvent);
            GameEvents.OnKSCStructureCollapsed.Remove(__instance.OnKSCStructureEvent);
            GameEvents.OnKSCStructureRepairing.Remove(__instance.OnKSCStructureEvent);
            GameEvents.OnKSCStructureRepaired.Remove(__instance.OnKSCStructureEvent);
            __instance.Terminate();

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnFacilityValuesModified")]
        internal static bool Prefix_OnFacilityValuesModified(KSCFacilityContextMenu __instance)
        {
            bool kctActive = HighLogic.CurrentGame.Mode != Game.Modes.MISSION && !KCT_GUI.IsPrimarilyDisabled && PresetManager.Instance.ActivePreset.GeneralSettings.KSCUpgradeTimes;
            bool upgradeable = !kctActive || IsUpgradeable(__instance.host.Facility);
            bool kctUpgradeBlocked = false;
            // Rerun the logic in CreateWindowContent
            if (kctActive && __instance.hasFacility)
            {
                if (FacilityUpgrade.AlreadyInProgressByID(__instance.host.Facility.id))
                {
                    kctUpgradeBlocked = true;
                }
                else if ((float)__instance.host.Facility.FacilityLevel != __instance.host.Facility.MaxLevel)
                {
                    string gate = GetTechGate(__instance.host.Facility.id, __instance.host.Facility.FacilityLevel + 1);
                    kctUpgradeBlocked = gate != null && ResearchAndDevelopment.GetTechnologyState(gate) != RDTech.State.Available;
                }
            }

            FacilitySetup(__instance, kctActive, upgradeable, kctUpgradeBlocked);
            return false;
        }

        private static void FacilitySetup(KSCFacilityContextMenu __instance, bool kctActive, bool upgradeable, bool kctUpgradeBlocked)
        {
            __instance.willRefresh = false;
            __instance.facilityDamage = __instance.host.GetStructureDamage();
            __instance.facilityDamageLevel = SpaceCenterBuilding.GetStructureDamageLevel(__instance.facilityDamage);
            __instance.damageColor = XKCDColors.ColorTranslator.ToHex(Color.Lerp(XKCDColors.ColorTranslator.FromHtml("#B4D455"), XKCDColors.Orange, Mathf.Clamp(__instance.facilityDamage, 0f, 100f) / 100f));
            CurrencyModifierQueryRP0 cmqRepair = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StructureRepair, -__instance.host.GetRepairsCost(), 0d, 0d);
            __instance.repairCost = -(float)cmqRepair.GetTotal(CurrencyRP0.Funds);
            __instance.repairCostColor = ((!(Funding.Instance != null)) ? XKCDColors.HexFormat.LightGrey : ((__instance.repairCost < Funding.Instance.Funds) ? "#B4D455" : XKCDColors.HexFormat.Orange));
            if (__instance.hasFacility)
            {
                __instance.upgradeCost = __instance.host.Facility.GetUpgradeCost();
                __instance.downgradeCost = __instance.host.Facility.GetDowngradeCost();
                __instance.level = __instance.host.Facility.FacilityLevel;
                __instance.maxLevel = __instance.host.Facility.MaxLevel;
                __instance.levelText = __instance.host.Facility.GetLevelText();
            }
            else
            {
                __instance.upgradeCost = 0f;
                __instance.downgradeCost = 0f;
                __instance.level = 0;
                __instance.maxLevel = 0f;
                __instance.levelText = string.Empty;
            }
            if (HighLogic.CurrentGame.Mode != Game.Modes.MISSION)
            {
                CurrencyModifierQueryRP0 cmqUpgrade = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StructureConstruction, -__instance.upgradeCost, 0d, 0d);
                __instance.upgradeCost = -(float)cmqUpgrade.GetTotal(CurrencyRP0.Funds);
                CurrencyModifierQueryRP0 cmqDowngrade = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StructureConstruction, -__instance.downgradeCost, 0d, 0d);
                __instance.downgradeCost = -(float)cmqDowngrade.GetTotal(CurrencyRP0.Funds);
                if (kctUpgradeBlocked)
                    __instance.upgradeString = $"<color={XKCDColors.HexFormat.BrightOrange}>{cmqUpgrade.GetCostLineOverride(displayInverted: true, useCurrencyColors: false, useInsufficientCurrencyColors: false, includePercentage: false, "\n")}</color>";
                else
                    __instance.upgradeString = cmqUpgrade.GetCostLineOverride(displayInverted: true, useCurrencyColors: true, useInsufficientCurrencyColors: !kctActive, includePercentage: false, "\n");

                __instance.downgradeString = cmqDowngrade.GetCostLineOverride(displayInverted: true, useCurrencyColors: true, useInsufficientCurrencyColors: true, includePercentage: false, "\n");

                if ((float)__instance.level != __instance.maxLevel && upgradeable)
                {
                    __instance.UpgradeButton.interactable = !kctUpgradeBlocked;
                    __instance.UpgradeButtonText.text = Localizer.Format("#autoLOC_475331", __instance.upgradeString);
                }
                else
                {
                    __instance.UpgradeButton.interactable = false;
                    __instance.UpgradeButtonText.text = Localizer.Format("#autoLOC_475336");
                    if(!upgradeable)
                        __instance.DowngradeButton.interactable = false;
                }
            }
            else
            {

                __instance.UpgradeButton.gameObject.SetActive(value: false);
                __instance.UpgradeButton.interactable = false;
                __instance.DowngradeButton.gameObject.SetActive(value: false);
                __instance.DowngradeButton.interactable = false;
            }
            __instance.levelFieldText.text = Localizer.Format("#autoLOC_475323", __instance.level + 1);
            __instance.levelStatsText.text = __instance.levelText;

            if (__instance.host.Operational)
            {
                __instance.windowTitleField.color = XKCDColors.ElectricLime;
                if (__instance.facilityDamage > 0f)
                {
                    __instance.statusText.text = Localizer.Format("#autoLOC_475347") + " <b><color=" + __instance.damageColor + ">" + Localizer.Format("#autoLOC_257237") + " (" + __instance.facilityDamageLevel + " " + Localizer.Format("#autoLOC_6002247") + ")</color></b>";
                }
                else
                {
                    __instance.statusText.text = Localizer.Format("#autoLOC_475351");
                }
                __instance.EnterButtonText.color = XKCDColors.ColorTranslator.FromHtml("#B4D455");
                __instance.EnterButton.interactable = true;
                if (__instance.showDowngradeControls)
                {
                    __instance.DowngradeButton.gameObject.SetActive(value: false);
                    __instance.DemolishButton.gameObject.SetActive(value: true);
                    if (__instance.host.destructibles.Length != 0)
                    {
                        __instance.DemolishButton.interactable = true;
                    }
                    else
                    {
                        __instance.DemolishButton.interactable = false;
                    }
                }
                else
                {
                    __instance.DowngradeButton.gameObject.SetActive(value: false);
                    __instance.DemolishButton.gameObject.SetActive(value: false);
                }
            }
            else
            {
                __instance.windowTitleField.color = XKCDColors.Orange;
                __instance.statusText.text = Localizer.Format("#autoLOC_475380", XKCDColors.HexFormat.Orange);
                __instance.EnterButtonText.color = Color.clear;
                __instance.EnterButton.interactable = false;
                if (__instance.level > 0 && __instance.showDowngradeControls && HighLogic.CurrentGame.Mode != Game.Modes.MISSION && upgradeable)
                {
                    __instance.DemolishButton.gameObject.SetActive(value: false);
                    __instance.DowngradeButton.gameObject.SetActive(__instance.isUpgradeable);
                    __instance.DowngradeButtonText.text = Localizer.Format("#autoLOC_6002251") + " " + Mathf.Max(0, __instance.level) + "\n" + __instance.downgradeString;
                    if (!Funding.CanAfford(__instance.downgradeCost))
                    {
                        __instance.DowngradeButton.interactable = false;
                    }
                    else
                    {
                        __instance.DowngradeButton.interactable = true;
                    }
                }
                else if (__instance.showDowngradeControls && upgradeable)
                {
                    __instance.DowngradeButton.gameObject.SetActive(value: false);
                    __instance.DemolishButton.gameObject.SetActive(value: true);
                    __instance.DemolishButton.interactable = false;
                }
                else
                {
                    __instance.DowngradeButton.gameObject.SetActive(value: false);
                    __instance.DemolishButton.gameObject.SetActive(value: false);
                }
            }
            if (__instance.repairCost == 0f)
            {
                if (__instance.facilityDamage > 0f)
                {
                    __instance.RepairButton.interactable = false;
                    __instance.RepairButtonText.color = __instance.btnTextBaseColor.A(0.6f);
                    __instance.RepairButtonText.text = "<color=" + XKCDColors.HexFormat.LightGrey + ">" + Localizer.Format("#autoLOC_6002248") + "</color>\n";
                }
                else
                {
                    __instance.RepairButton.interactable = false;
                    __instance.RepairButtonText.color = __instance.btnTextBaseColor.A(0.3f);
                    __instance.RepairButtonText.text = Localizer.Format("#autoLOC_475425");
                }
            }
            else
            {
                __instance.RepairButton.interactable = true;
                __instance.RepairButtonText.color = __instance.btnTextBaseColor.A(1f);
                if (Funding.Instance != null)
                {
                    __instance.RepairButtonText.text = Localizer.Format("#autoLOC_475433", __instance.repairCostColor, cmqRepair.GetCostLineOverride(displayInverted: true, useCurrencyColors: true, useInsufficientCurrencyColors: true, includePercentage: false, "\n"));
                }
                else
                {
                    __instance.RepairButtonText.text = Localizer.Format("#autoLOC_6002249");
                }
            }
        }

        private static void ProcessUpgrade(SpaceCenterBuilding host)
        {
            if (host == null)
                return;

            int oldLevel = host.Facility.FacilityLevel;
            KCTDebug.Log($"Upgrading from level {oldLevel}");

            string facilityID = host.Facility.id;
            SpaceCenterFacility facilityType = GetFacilityType(host);

            string gate = GetTechGate(facilityID, oldLevel + 1);
            if (gate != null && ResearchAndDevelopment.GetTechnologyState(gate) != RDTech.State.Available)
            {
                return;
            }

            var split = facilityID.Split('/');
            var upgrading = new FacilityUpgrade(facilityType, facilityID, oldLevel + 1, oldLevel, split[split.Length - 1]);

            if (!upgrading.AlreadyInProgress())
            {
                double cost = host.Facility.GetUpgradeCost();
                upgrading.SetBP(cost);
                upgrading.cost = cost;

                KCTGameStates.ActiveKSC.FacilityUpgrades.Add(upgrading);

                try
                {
                    KCTEvents.OnFacilityUpgradeQueued?.Fire(upgrading);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                ScreenMessages.PostScreenMessage(Localizer.GetStringByTag("#rp0_FacilityContextMenu_UpgradeStart"), 4f, ScreenMessageStyle.UPPER_CENTER);
                KCTDebug.Log($"Facility {facilityID} upgrade requested to lvl {oldLevel + 1}, BP {upgrading.BP}");
            }
            else if (oldLevel + 1 != upgrading.currentLevel)
            {
                ScreenMessages.PostScreenMessage(Localizer.GetStringByTag("#rp0_FacilityContextMenu_UpgradeInProgress"), 4f, ScreenMessageStyle.UPPER_CENTER);
                KCTDebug.Log($"Facility {facilityID} tried to upgrade to lvl {oldLevel + 1} but already in list!");
            }
        }

        private static SpaceCenterFacility GetFacilityType(SpaceCenterBuilding scb)
        {
            if (scb is AdministrationFacility) return SpaceCenterFacility.Administration;
            if (scb is AstronautComplexFacility) return SpaceCenterFacility.AstronautComplex;
            if (scb is LaunchSiteFacility lpFacility && lpFacility.facilityType == EditorFacility.VAB) return SpaceCenterFacility.LaunchPad;
            if (scb is LaunchSiteFacility rwFacility && rwFacility.facilityType == EditorFacility.SPH) return SpaceCenterFacility.Runway;
            if (scb is MissionControlBuilding) return SpaceCenterFacility.MissionControl;
            if (scb is RnDBuilding) return SpaceCenterFacility.ResearchAndDevelopment;
            if (scb is SpacePlaneHangarBuilding) return SpaceCenterFacility.SpaceplaneHangar;
            if (scb is TrackingStationBuilding) return SpaceCenterFacility.TrackingStation;
            //if (scb is VehicleAssemblyBuilding) return SpaceCenterFacility.VehicleAssemblyBuilding;
            return SpaceCenterFacility.VehicleAssemblyBuilding;
        }

        private static bool IsUpgradeable(UpgradeableFacility facility)
        {
            if (facility.id.IndexOf("launchpad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     facility.id.IndexOf("SpaceplaneHangar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     facility.id.IndexOf("VehicleAssemblyBuilding", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     facility.id.IndexOf("runway", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return true;
        }

        private static bool OverrideFacilityDescriptions()
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

        private static void CheckLoadDict()
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

        private static string GetTechGate(string facId, int level)
        {
            CheckLoadDict();
            if (_techGatings == null)
                return null;

            if (_techGatings.TryGetValue(facId, out var d))
                if (d.TryGetValue(level, out string node))
                    return node;

            return null;
        }
    }
}
