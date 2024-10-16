using HarmonyLib;
using KSP.Localization;
using TMPro;
using UnityEngine;
using KSP.UI.Screens;
using KSP.UI;
using System.Collections.Generic;
using PreFlightTests;
using ROUtils;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(EngineersReport))]
    internal class PatchEngineersReport
    {
        private static int hashcodeTEATEB = "TEATEB".GetHashCode();

        private static string cacheHumanRatedLH;
        private static string cacheMassLH;
        private static string cacheSizeLH;
        private static string cacheResLH;
        private static string cacheYes;
        private static string cacheNo;
        private static string cacheOK;
        private static string cacheColorGood = XKCDColors.HexFormat.KSPBadassGreen;
        private static string cacheColorBad = XKCDColors.HexFormat.KSPNotSoGoodOrange;
        private static string cacheColorNeutral = XKCDColors.HexFormat.KSPNeutralUIGrey;

        private static TextMeshProUGUI humanRatedLH;
        private static TextMeshProUGUI humanRatedRH;
        private static TextMeshProUGUI massLH;
        private static TextMeshProUGUI massRH;
        private static TextMeshProUGUI sizeLH;
        private static TextMeshProUGUI sizeRH;
        private static TextMeshProUGUI resourcesLH;
        private static TextMeshProUGUI resourcesRH;

        public static bool IsValid => humanRatedLH != null;

        [HarmonyPostfix]
        [HarmonyPatch("CacheLocalStrings")]
        internal static void Prefix_CacheLocalStrings()
        {
            cacheYes = Localizer.Format("#autoLOC_439839");
            cacheNo = Localizer.Format("#autoLOC_439840");
            cacheOK = Localizer.Format("#autoLOC_190905");

            cacheHumanRatedLH = $"<color={cacheColorNeutral}>{Localizer.GetStringByTag("#rp0_EngineersReport_HumanRatedLH")}</color>";
            cacheMassLH = Localizer.Format("#autoLOC_443401", cacheColorNeutral);
            cacheSizeLH = "<line-height=110%><color=" + cacheColorNeutral + ">" + EngineersReport.cacheAutoLOC_443417 + "</color>\n<color=" +
                cacheColorNeutral + ">" + EngineersReport.cacheAutoLOC_443418 + "</color>\n<color=" +
                cacheColorNeutral + ">" + EngineersReport.cacheAutoLOC_443419 + "</color>\n<color=" +
                cacheColorNeutral + ">" + EngineersReport.cacheAutoLOC_443420 + "</color></line-height>";
            cacheResLH = $"<color={cacheColorNeutral}>{Localizer.GetStringByTag("#rp0_EngineersReport_ResourcesLH")}</color>";
        }

        // We'll use this to bind some private fields too
        [HarmonyPrefix]
        [HarmonyPatch("CreateCraftStatsbody")]
        internal static bool Prefix_CreateCraftStatsbody(EngineersReport __instance, out List<UIListItem> __result)
        {
            List<UIListItem> list = new List<UIListItem>();
            UIListItem uIListItem = __instance.cascadingListInfo.CreateBodyKeyValueAutofit("HumanRated:", "");
            humanRatedLH = uIListItem.GetTextElement("keyRich");
            humanRatedRH = uIListItem.GetTextElement("valueRich");
            list.Add(uIListItem);
            uIListItem = __instance.cascadingListInfo.CreateBodyKeyValueAutofit("Mass:", "");
            massLH = uIListItem.GetTextElement("keyRich");
            massRH = uIListItem.GetTextElement("valueRich");
            list.Add(uIListItem);
            uIListItem = __instance.cascadingListInfo.CreateBodyKeyValueAutofit("Size\nHeight:\nWidth:\nLength:", "Size\nHeight:\nWidth:\nLength:");
            sizeLH = uIListItem.GetTextElement("keyRich");
            sizeRH = uIListItem.GetTextElement("valueRich");
            list.Add(uIListItem);
            uIListItem = __instance.cascadingListInfo.CreateBodyKeyValueAutofit("Resources:", "");
            resourcesLH = uIListItem.GetTextElement("keyRich");
            resourcesRH = uIListItem.GetTextElement("valueRich");
            list.Add(uIListItem);

            __result = list;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("CreateStockDesignConcern")]
        internal static bool Prefix_CreateStockDesignConcern(EngineersReport __instance)
        {
            __instance.listItem_SeveritySelector = __instance.CreateSeveritySelector();
            __instance.listItem_SeveritySelector.transform.SetParent(__instance.cascadingListCheck.transform, worldPositionStays: false);
            __instance.listItem_partCountZero = __instance.cascadingListCheck.CreateBody(EngineersReport.cacheAutoLOC_443059);
            __instance.listItem_partCountZero.transform.SetParent(__instance.cascadingListCheck.transform, worldPositionStays: false);
            __instance.listItem_allTestsPassed = __instance.cascadingListCheck.CreateBody(EngineersReport.cacheAutoLOC_443064);
            __instance.listItem_allTestsPassed.transform.SetParent(__instance.cascadingListCheck.transform, worldPositionStays: false);
            __instance.AddTest(new ParachuteOnFirstStage());
            __instance.AddTest(new ParachuteOnEngineStage());
            //__instance.AddTest(new EnginesJettisonedBeforeUse());
            __instance.AddTest(new LandingGearPresent(EditorDriver.editorFacility));
            __instance.AddTest(new NoControlSources());
            __instance.AddTest(new KerbalSeatCollision());
            IEnumerator<PartResourceDefinition> enumerator = PartResourceLibrary.Instance.resourceDefinitions.GetEnumerator();
            while (enumerator.MoveNext())
            {
                PartResourceDefinition current = enumerator.Current;
                if (current.resourceFlowMode == ResourceFlowMode.NO_FLOW)
                {
                    continue;
                }
                if (current.id == hashcodeTEATEB // ignitors have no IResourceConsumer
                    || (Database.ResourceInfo.LCResourceTypes.ValueOrDefault(current.name) is LCResourceType t &&
                    ((t & LCResourceType.Fuel) == 0 || (t & LCResourceType.PadIgnore) != 0))) // if not fuel, or is ignored
                {
                    continue;
                }
                __instance.AddTest(new ResourceContainersReachable(current));
                __instance.AddTest(new ResourceConsumersReachable(current));
            }

            // RP-0 tests
            __instance.AddTest(new DesignConcerns.UntooledParts());

            return false;
        }

        public static void UpdateCraftStats()
        {
            if (EngineersReport.Instance != null && IsValid)
                Prefix_UpdateCratStats(EngineersReport.Instance, EditorLogic.fetch.ship);
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdateCratStats")]
        internal static bool Prefix_UpdateCratStats(EngineersReport __instance, ShipConstruct ship)
        {
            SpaceCenterFacility launchFacility;
            switch (EditorDriver.editorFacility)
            {
                default:
                case EditorFacility.VAB:
                    launchFacility = SpaceCenterFacility.LaunchPad;
                    break;
                case EditorFacility.SPH:
                    launchFacility = SpaceCenterFacility.Runway;
                    break;
            }

            bool isLP = launchFacility == SpaceCenterFacility.LaunchPad;

            float totalMass;
            Vector3 craftSize;
            bool vesselHumanRated;

            float massLimit;
            float minMassLimit;
            Vector3 maxSize;
            bool lcHumanRated;
            bool resourcesOK = true;
            if (PresetManager.Instance.ActivePreset.GeneralSettings.Enabled && PresetManager.Instance.ActivePreset.GeneralSettings.BuildTimes)
            {
                totalMass = SpaceCenterManagement.Instance.EditorVessel.mass;
                craftSize = SpaceCenterManagement.Instance.EditorVessel.ShipSize;
                vesselHumanRated = SpaceCenterManagement.Instance.EditorVessel.humanRated;

                resourcesOK = SpaceCenterManagement.Instance.EditorVessel.ResourcesOK(SpaceCenterManagement.Instance.EditorVessel.LC.Stats);

                massLimit = SpaceCenterManagement.Instance.EditorVessel.LC.MassMax;
                minMassLimit = SpaceCenterManagement.Instance.EditorVessel.LC.MassMin;
                maxSize = SpaceCenterManagement.Instance.EditorVessel.LC.SizeMax;
                lcHumanRated = SpaceCenterManagement.Instance.EditorVessel.LC.IsHumanRated;
                vesselHumanRated = SpaceCenterManagement.Instance.EditorVessel.humanRated;
            }
            else
            {
                totalMass = ship.GetTotalMass();
                craftSize = ship.shipSize;
                vesselHumanRated = false;

                minMassLimit = 0f;
                massLimit = GameVariables.Instance.GetCraftMassLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(launchFacility), isLP);
                maxSize = GameVariables.Instance.GetCraftSizeLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(launchFacility), isLP);
                lcHumanRated = true;
            }

            

            bool humanRatingOK = !vesselHumanRated || lcHumanRated;
            string humanRatingColorHex = humanRatingOK ? XKCDColors.HexFormat.KSPBadassGreen : cacheColorBad;
            humanRatedLH.text = cacheHumanRatedLH;
            humanRatedRH.text = $"<color={(humanRatingOK ? cacheColorGood : cacheColorBad)}>{(vesselHumanRated ? cacheYes : cacheNo)} {Localizer.GetStringByTag(lcHumanRated ? "#rp0_EngineersReport_HumanRatedLCYes" : "#rp0_EngineersReport_HumanRatedLCNo")}</color>";

            string partMassColorHex = totalMass <= massLimit && totalMass >= minMassLimit ? cacheColorGood : cacheColorBad;
            massLH.text = cacheMassLH;

            if (massLimit < float.MaxValue)
            {
                massRH.text = Localizer.Format("#autoLOC_443405", partMassColorHex, totalMass.ToString("N3"), $"  {minMassLimit:N0} - {massLimit:N0}");
            }
            else
            {
                massRH.text = Localizer.Format("#autoLOC_443409", partMassColorHex, totalMass.ToString("N3"));
            }

            string sizeForeAftHex = craftSize.y <= maxSize.y ? cacheColorGood : cacheColorBad;
            string sizeSpanHex = craftSize.x <= maxSize.x ? cacheColorGood : cacheColorBad;
            string sizeTHgtHex = craftSize.z <= maxSize.z ? cacheColorGood : cacheColorBad;


            sizeLH.text = cacheSizeLH;

            if (maxSize.x < float.MaxValue && maxSize.y < float.MaxValue && maxSize.z < float.MaxValue)
            {
                sizeRH.text =
                            "<line-height=110%>  \n<color=" + sizeForeAftHex + ">" + KSPUtil.LocalizeNumber(craftSize.y, "0.0") + EngineersReport.cacheAutoLOC_7001411 +
                                " / " + KSPUtil.LocalizeNumber(maxSize.y, "0.0") + EngineersReport.cacheAutoLOC_7001411 + "</color>\n<color=" +
                            sizeSpanHex + ">" + KSPUtil.LocalizeNumber(craftSize.x, "0.0") + EngineersReport.cacheAutoLOC_7001411 + " / " +
                            KSPUtil.LocalizeNumber(maxSize.x, "0.0") +
                            EngineersReport.cacheAutoLOC_7001411 + "</color>\n<color=" + sizeTHgtHex + ">" + KSPUtil.LocalizeNumber(craftSize.z, "0.0") + EngineersReport.cacheAutoLOC_7001411 + " / " +
                            KSPUtil.LocalizeNumber(maxSize.z, "0.0") + EngineersReport.cacheAutoLOC_7001411 + "</color></line-height>";
            }
            else
            {
                sizeRH.text = "<line-height=110%> \n<color=" + sizeForeAftHex + ">" + KSPUtil.LocalizeNumber(craftSize.y, "0.0") + EngineersReport.cacheAutoLOC_7001411 +
                "</color>\n<color=" + sizeSpanHex + ">" + KSPUtil.LocalizeNumber(craftSize.x, "0.0") + EngineersReport.cacheAutoLOC_7001411 +
                "</color>\n<color=" + sizeTHgtHex + ">" + KSPUtil.LocalizeNumber(craftSize.z, "0.0") + EngineersReport.cacheAutoLOC_7001411 + "</color></line-height>";
            }

            resourcesLH.text = cacheResLH;
            if (resourcesOK)
                resourcesRH.text = $"<color={cacheColorGood}>{cacheYes}</color>";
            else
                resourcesRH.text = $"<color={cacheColorBad}>{cacheNo}</color>";

            if (humanRatingOK && resourcesOK &&
                            totalMass <= massLimit &&
                            totalMass >= minMassLimit &&
                              craftSize.x <= maxSize.x &&
                                craftSize.y <= maxSize.y &&
                                 craftSize.z <= maxSize.z)
            {
                __instance.appFrame.header.color = XKCDColors.ElectricLime;
                __instance.appLauncherButton.sprite.color = Color.white;
            }
            else
            {
                __instance.appFrame.header.color = XKCDColors.Orange;
                __instance.appLauncherButton.sprite.color = XKCDColors.Orange;
            }

            return false;
        }

        // Don't need this yet.

        //[HarmonyPostfix]
        //[HarmonyPatch("OnAppInitialized")]
        //internal static void Postfix_OnAppInitialized(EngineersReport __instance)
        //{
        //    __instance.StopCoroutine(__instance.updateRoutine);
        //    __instance.updateRoutine = __instance.StartCoroutine(RunTests());
        //}

        //[HarmonyPostfix]
        //[HarmonyPatch("OnCraftModified")]
        //[HarmonyPatch(new System.Type[] { typeof(ShipConstruct) })]
        //internal static void Postfix_OnCraftModified(EngineersReport __instance)
        //{
        //    __instance.StopCoroutine(__instance.testRoutine);
        //    __instance.testRoutine = __instance.StartCoroutine(RunTests());
        //}

        //// Our own coroutine
        //internal static IEnumerator RunTests()
        //{
        //    yield return null;
        //    yield return null;

        //    EngineersReport.sccFlowGraphUCFinder = new RUI.Algorithms.SCCFlowGraphUCFinder(EditorLogic.fetch.ship.Parts);

        //    if (EditorLogic.fetch.ship.parts.Count != 0)
        //    {
        //        for (int i = 0, count = EngineersReport.Instance.tests.Count; i < count; i++)
        //        {
        //            EngineersReport.Instance.tests[i].RunTest();
        //        }
        //    }
        //    EngineersReport.Instance.UpdateDesignConcern();
        //}
    }
}
