using HarmonyLib;
using KerbalConstructionTime;
using KSP.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KSP.UI.Screens;
using KSP.UI;
using System.Collections.Generic;
using System.Reflection;
using PreFlightTests;
using System.Collections;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(EngineersReport))]
    internal class PatchEngineersReport
    {
        private static int hashcodeTEATEB = "TEATEB".GetHashCode();

        private static string cacheAutoLOC_442833;
        private static string cacheAutoLOC_443059;
        private static string cacheAutoLOC_443064;
        private static string cacheAutoLOC_443343;
        private static string cacheAutoLOC_443417;
        private static string cacheAutoLOC_443418;
        private static string cacheAutoLOC_443419;
        private static string cacheAutoLOC_443420;
        private static string cacheAutoLOC_7001411;
        private static string cacheAutoLOC_442811;
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

        private static GenericCascadingList cascadingListInfo;
        private static GenericCascadingList cascadingListCheck;
        private static TextMeshProUGUI humanRatedLH;
        private static TextMeshProUGUI humanRatedRH;
        private static TextMeshProUGUI massLH;
        private static TextMeshProUGUI massRH;
        private static TextMeshProUGUI sizeLH;
        private static TextMeshProUGUI sizeRH;
        private static TextMeshProUGUI resourcesLH;
        private static TextMeshProUGUI resourcesRH;
        private static GenericAppFrame appFrame;

        public static bool IsValid => appFrame != null;

        [HarmonyPrefix]
        [HarmonyPatch("CacheLocalStrings")]
        internal static void Prefix_CacheLocalStrings()
        {
            cacheAutoLOC_442833 = Localizer.Format("#autoLOC_442833");
            cacheAutoLOC_443059 = Localizer.Format("#autoLOC_443059");
            cacheAutoLOC_443064 = Localizer.Format("#autoLOC_443064");
            cacheAutoLOC_443343 = "<color=#e6752a>" + Localizer.Format("#autoLOC_443343") + "</color>";
            cacheAutoLOC_443417 = Localizer.Format("#autoLOC_443417");
            cacheAutoLOC_443418 = Localizer.Format("#autoLOC_443418");
            cacheAutoLOC_443419 = Localizer.Format("#autoLOC_443419");
            cacheAutoLOC_443420 = Localizer.Format("#autoLOC_443420");
            cacheAutoLOC_7001411 = Localizer.Format("#autoLOC_7001411");
            cacheAutoLOC_442811 = Localizer.Format("#autoLOC_442811");

            cacheYes = Localizer.Format("#autoLOC_439839");
            cacheNo = Localizer.Format("#autoLOC_439840");
            cacheOK = Localizer.Format("#autoLOC_174814");

            cacheHumanRatedLH = $"<color={cacheColorNeutral}>{Localizer.GetStringByTag("#rp0ER_HumanRatedLH")}</color>";
            cacheMassLH = Localizer.Format("#autoLOC_443401", cacheColorNeutral);
            cacheSizeLH = "<line-height=110%><color=" + cacheColorNeutral + ">" + cacheAutoLOC_443417 + "</color>\n<color=" +
                cacheColorNeutral + ">" + cacheAutoLOC_443418 + "</color>\n<color=" +
                cacheColorNeutral + ">" + cacheAutoLOC_443419 + "</color>\n<color=" +
                cacheColorNeutral + ">" + cacheAutoLOC_443420 + "</color></line-height>";
            cacheResLH = $"<color={cacheColorNeutral}>{Localizer.GetStringByTag("#rp0ER_ResourcesLH")}</color>";
        }

        // We'll use this to bind some private fields too
        [HarmonyPrefix]
        [HarmonyPatch("CreateCraftStatsbody")]
        internal static bool Prefix_CreateCraftStatsbody(EngineersReport __instance, ref List<UIListItem> __result, ref GenericCascadingList ___cascadingListInfo, ref GenericCascadingList ___cascadingListCheck, ref GenericAppFrame ___appFrame)
        {
            List<UIListItem> list = new List<UIListItem>();
            cascadingListInfo = ___cascadingListInfo;
            UIListItem uIListItem = cascadingListInfo.CreateBodyKeyValueAutofit("HumanRated:", "");
            humanRatedLH = uIListItem.GetTextElement("keyRich");
            humanRatedRH = uIListItem.GetTextElement("valueRich");
            list.Add(uIListItem);
            uIListItem = cascadingListInfo.CreateBodyKeyValueAutofit("Mass:", "");
            massLH = uIListItem.GetTextElement("keyRich");
            massRH = uIListItem.GetTextElement("valueRich");
            list.Add(uIListItem);
            uIListItem = cascadingListInfo.CreateBodyKeyValueAutofit("Size\nHeight:\nWidth:\nLength:", "Size\nHeight:\nWidth:\nLength:");
            sizeLH = uIListItem.GetTextElement("keyRich");
            sizeRH = uIListItem.GetTextElement("valueRich");
            list.Add(uIListItem);
            uIListItem = cascadingListInfo.CreateBodyKeyValueAutofit("Resources:", "");
            resourcesLH = uIListItem.GetTextElement("keyRich");
            resourcesRH = uIListItem.GetTextElement("valueRich");
            list.Add(uIListItem);

            cascadingListCheck = ___cascadingListCheck;
            appFrame = ___appFrame;

            __result = list;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("CreateStockDesignConcern")]
        internal static bool Prefix_CreateStockDesignConcern(EngineersReport __instance, ref UIListItem ___listItem_SeveritySelector, ref UIListItem ___listItem_partCountZero, ref UIListItem ___listItem_allTestsPassed)
        {
            ___listItem_SeveritySelector = AccessTools.Method(typeof(EngineersReport), "CreateSeveritySelector").Invoke(__instance, null) as UIListItem;
            ___listItem_SeveritySelector.transform.SetParent(cascadingListCheck.transform, worldPositionStays: false);
            ___listItem_partCountZero = cascadingListCheck.CreateBody(cacheAutoLOC_443059);
            ___listItem_partCountZero.transform.SetParent(cascadingListCheck.transform, worldPositionStays: false);
            ___listItem_allTestsPassed = cascadingListCheck.CreateBody(cacheAutoLOC_443064);
            ___listItem_allTestsPassed.transform.SetParent(cascadingListCheck.transform, worldPositionStays: false);
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
                if (!GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(current.name) 
                    || current.id == hashcodeTEATEB // ignitors have no IResourceConsumer
                    || GuiDataAndWhitelistItemsDatabase.PadIgnoreRes.Contains(current.name)) // to ignore LS resources
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
                totalMass = KCTGameStates.EditorVessel.TotalMass;
                craftSize = KCTGameStates.EditorVessel.ShipSize;
                vesselHumanRated = KCTGameStates.EditorVessel.IsHumanRated;

                resourcesOK = KCTGameStates.EditorVessel.ResourcesOK(KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance);

                massLimit = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.MassMax;
                minMassLimit = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.MassMin;
                maxSize = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.SizeMax;
                lcHumanRated = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.IsHumanRated;
                vesselHumanRated = KCTGameStates.EditorVessel.IsHumanRated;
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
            humanRatedRH.text = $"<color={(humanRatingOK ? cacheColorGood : cacheColorBad)}>{(vesselHumanRated ? cacheYes : cacheNo)} {Localizer.GetStringByTag(lcHumanRated ? "#rp0ER_HumanRatedLCYes" : "#rp0ER_HumanRatedLCNo")}</color>";

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
                            "<line-height=110%>  \n<color=" + sizeForeAftHex + ">" + KSPUtil.LocalizeNumber(craftSize.y, "0.0") + cacheAutoLOC_7001411 +
                                " / " + KSPUtil.LocalizeNumber(maxSize.y, "0.0") + cacheAutoLOC_7001411 + "</color>\n<color=" +
                            sizeSpanHex + ">" + KSPUtil.LocalizeNumber(craftSize.x, "0.0") + cacheAutoLOC_7001411 + " / " +
                            KSPUtil.LocalizeNumber(maxSize.x, "0.0") +
                            cacheAutoLOC_7001411 + "</color>\n<color=" + sizeTHgtHex + ">" + KSPUtil.LocalizeNumber(craftSize.z, "0.0") + cacheAutoLOC_7001411 + " / " +
                            KSPUtil.LocalizeNumber(maxSize.z, "0.0") + cacheAutoLOC_7001411 + "</color></line-height>";
            }
            else
            {
                sizeRH.text = "<line-height=110%> \n<color=" + sizeForeAftHex + ">" + KSPUtil.LocalizeNumber(craftSize.y, "0.0") + cacheAutoLOC_7001411 +
                "</color>\n<color=" + sizeSpanHex + ">" + KSPUtil.LocalizeNumber(craftSize.x, "0.0") + cacheAutoLOC_7001411 +
                "</color>\n<color=" + sizeTHgtHex + ">" + KSPUtil.LocalizeNumber(craftSize.z, "0.0") + cacheAutoLOC_7001411 + "</color></line-height>";
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
                appFrame.header.color = XKCDColors.ElectricLime;
                __instance.appLauncherButton.sprite.color = Color.white;
            }
            else
            {
                appFrame.header.color = XKCDColors.Orange;
                __instance.appLauncherButton.sprite.color = XKCDColors.Orange;
            }

            return false;
        }

        // Don't need this yet.

        // Coroutine patching
        //[HarmonyPostfix]
        //[HarmonyPatch("OnAppInitialized")]
        //internal static void Postfix_OnAppInitialized(EngineersReport __instance, ref Coroutine ___updateRoutine)
        //{
        //    __instance.StopCoroutine(___updateRoutine);
        //    ___updateRoutine = __instance.StartCoroutine(RunTests());
        //}

        //[HarmonyPostfix]
        //[HarmonyPatch("OnCraftModified")]
        //[HarmonyPatch(new System.Type[] { typeof(ShipConstruct) })]
        //internal static void Postfix_OnCraftModified(EngineersReport __instance, ref Coroutine ___testRoutine)
        //{
        //    __instance.StopCoroutine(___testRoutine);
        //    ___testRoutine = __instance.StartCoroutine(RunTests());
        //}

        //private static FieldInfo tests = typeof(EngineersReport).GetField("tests", AccessTools.all);
        //private static System.Type TestWrapper = AccessTools.TypeByName("KSP.UI.Screens.EngineersReport.TestWrapper");
        //private static MethodInfo RunTest = TestWrapper.GetMethod("RunTest", AccessTools.all);
        //private static MethodInfo UpdateDesignConcern = typeof(EngineersReport).GetMethod("UpdateDesignConcern", AccessTools.all);

        //private static DictionaryValueList<int, Part> resourceConsumers = new DictionaryValueList<int, Part>();
        //private static DictionaryValueList<int, PartSet> resourceProviders = new DictionaryValueList<int, PartSet>();

        //// Our own coroutine
        //internal static IEnumerator RunTests()
        //{
        //    yield return null;
        //    yield return null;

        //    IList list = tests.GetValue(EngineersReport.Instance) as IList;
        //    if (EditorLogic.fetch.ship.parts.Count != 0)
        //    {
        //        for (int i = 0, count = list.Count; i < count; i++)
        //        {
        //            RunTest.Invoke(list[i], null);
        //        }
        //    }
        //    UpdateDesignConcern.Invoke(EngineersReport.Instance, null);
        //}
    }
}
