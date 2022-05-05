using KSP.UI;
using KSP.UI.Screens;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class EngineersReportClobberer
    {
        private static bool _engineerLocCached = false;
        private static string _cacheAutoLOC_443417;
        private static string _cacheAutoLOC_443418;
        private static string _cacheAutoLOC_443419;
        private static string _cacheAutoLOC_443420;
        private static string _cacheAutoLOC_7001411;

        private readonly KerbalConstructionTime _kctInstance;
        private Coroutine _clobberEngineersReportCoroutine = null;
        private bool _wasERActive = false;

        private TMPro.TextMeshProUGUI _refERpartMassLH, _refERpartMassRH, _refERsizeLH, _refERsizeRH, _refERpartCountLH, _refERpartCountRH;
        private GenericAppFrame _refERappFrame;

        public EngineersReportClobberer(KerbalConstructionTime instance)
        {
            _kctInstance = instance;
        }

        /// <summary>
        /// When notified the Engineer's Report app is ready, bind to it and set up a clobber.
        /// </summary>
        public void BindToEngineersReport()
        {
            // Set up all our fields
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            Type typeER = EngineersReport.Instance.GetType();
            _refERsizeLH = (TMPro.TextMeshProUGUI)typeER.GetField("sizeLH", flags).GetValue(EngineersReport.Instance);
            _refERsizeRH = (TMPro.TextMeshProUGUI)typeER.GetField("sizeRH", flags).GetValue(EngineersReport.Instance);
            _refERpartMassLH = (TMPro.TextMeshProUGUI)typeER.GetField("partMassLH", flags).GetValue(EngineersReport.Instance);
            _refERpartMassRH = (TMPro.TextMeshProUGUI)typeER.GetField("partMassRH", flags).GetValue(EngineersReport.Instance);
            _refERpartCountLH = (TMPro.TextMeshProUGUI)typeER.GetField("partCountLH", flags).GetValue(EngineersReport.Instance);
            _refERpartCountRH = (TMPro.TextMeshProUGUI)typeER.GetField("partCountRH", flags).GetValue(EngineersReport.Instance);
            _refERappFrame = (GenericAppFrame)typeER.GetField("appFrame", flags).GetValue(EngineersReport.Instance);

            EditorStarted();
        }

        public void EditorStarted()
        {
            // The ER, on startup, sets a 3s delayed callback. We run right after it.
            _kctInstance.StartCoroutine(CallbackUtil.DelayedCallback(3.1f, () => { ClobberEngineersReport(); }));
        }

        public void StartClobberingCoroutine()
        {
            if (_clobberEngineersReportCoroutine != null)
                _kctInstance.StopCoroutine(_clobberEngineersReportCoroutine);

            _clobberEngineersReportCoroutine = _kctInstance.StartCoroutine(ClobberEngineersReport_Coroutine());
        }

        /// <summary>
        /// Coroutine to override the Engineer's Report craft stats
        /// Needed because we disagree about craft size and mass.
        /// </summary>
        /// <returns></returns>
        public IEnumerator ClobberEngineersReport_Coroutine()
        {
            // Just in case
            while (EngineersReport.Instance == null)
                yield return new WaitForSeconds(0.1f);

            // Skip past Engineer report update. Yes there will be a few frames of wrongness, but better that
            // than have it clobber us instead!
            yield return new WaitForEndOfFrame();
            yield return null;

            ClobberEngineersReport();
        }

        public void PollForChanges()
        {
            // Polling sucks, but there's no event I can find for when an applauncher app gets displayed.
            if (HighLogic.LoadedSceneIsEditor && EngineersReport.Instance != null)
            {
                if (_refERappFrame != null)
                {
                    bool isERActive = _refERappFrame.gameObject.activeSelf;

                    if (isERActive && !_wasERActive)
                    {
                        ClobberEngineersReport();
                    }
                    _wasERActive = isERActive;
                }
            }
            else
            {
                _wasERActive = false;
            }
        }

        public void ClobberEngineersReport()
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;

            EnsureLocalizationsCached();

            ShipConstruct ship = EditorLogic.fetch.ship;

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

            float totalMass = Utilities.GetShipMass(ship, true, out _, out _);
            Vector3 craftSize = Utilities.GetShipSize(ship, true);
            bool vesselHumanRated = false;

            float massLimit;
            float minMassLimit;
            Vector3 maxSize;
            bool lcHumanRated;
            if (PresetManager.Instance.ActivePreset.GeneralSettings.Enabled && PresetManager.Instance.ActivePreset.GeneralSettings.BuildTimes)
            {
                massLimit = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.MassMax;
                minMassLimit = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.MassMin;
                maxSize = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.SizeMax;
                lcHumanRated = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.IsHumanRated;
                vesselHumanRated = KCTGameStates.EditorIsHumanRated;
            }
            else
            {
                minMassLimit = 0f;
                massLimit = GameVariables.Instance.GetCraftMassLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(launchFacility), isLP);
                maxSize = GameVariables.Instance.GetCraftSizeLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(launchFacility), isLP);
                lcHumanRated = true;
            }

            string neutralColorHex = XKCDColors.HexFormat.KSPNeutralUIGrey;

            bool humanRatingOK = !vesselHumanRated || lcHumanRated;
            string humanRatingColorHex = humanRatingOK ? XKCDColors.HexFormat.KSPBadassGreen : XKCDColors.HexFormat.KSPNotSoGoodOrange;
            _refERpartCountLH.text = "<color=" + neutralColorHex + ">Human-Rated:</color>"; //KSP.Localization.Localizer.Format("#autoLOC_443389", neutralColorHex);

            const string yes = "Yes", no = "No";
            _refERpartCountRH.text = "<color=" + humanRatingColorHex + ">" + (vesselHumanRated ? yes : no) + " / LC: " + (lcHumanRated ? yes : no) + "</color>";

            string partMassColorHex = totalMass <= massLimit  && totalMass >= minMassLimit ? XKCDColors.HexFormat.KSPBadassGreen : XKCDColors.HexFormat.KSPNotSoGoodOrange;
            _refERpartMassLH.text = KSP.Localization.Localizer.Format("#autoLOC_443401", neutralColorHex);

            if (massLimit < float.MaxValue)
            {
                _refERpartMassRH.text = KSP.Localization.Localizer.Format("#autoLOC_443405", partMassColorHex, totalMass.ToString("N3"), $"  {minMassLimit:N0} - {massLimit:N0}");
            }
            else
            {
                _refERpartMassRH.text = KSP.Localization.Localizer.Format("#autoLOC_443409", partMassColorHex, totalMass.ToString("N3"));
            }

            string sizeForeAftHex = craftSize.y <= maxSize.y ? XKCDColors.HexFormat.KSPBadassGreen : XKCDColors.HexFormat.KSPNotSoGoodOrange;
            string sizeSpanHex = craftSize.x <= maxSize.x ? XKCDColors.HexFormat.KSPBadassGreen : XKCDColors.HexFormat.KSPNotSoGoodOrange;
            string sizeTHgtHex = craftSize.z <= maxSize.z ? XKCDColors.HexFormat.KSPBadassGreen : XKCDColors.HexFormat.KSPNotSoGoodOrange;


            _refERsizeLH.text = "<line-height=110%><color=" + neutralColorHex + ">" + _cacheAutoLOC_443417 + "</color>\n<color=" +
                neutralColorHex + ">" + _cacheAutoLOC_443418 + "</color>\n<color=" +
                neutralColorHex + ">" + _cacheAutoLOC_443419 + "</color>\n<color=" +
                neutralColorHex + ">" + _cacheAutoLOC_443420 + "</color></line-height>";

            if (maxSize.x < float.MaxValue && maxSize.y < float.MaxValue && maxSize.z < float.MaxValue)
            {
                _refERsizeRH.text =
                            "<line-height=110%>  \n<color=" + sizeForeAftHex + ">" + KSPUtil.LocalizeNumber(craftSize.y, "0.0") + _cacheAutoLOC_7001411 +
                                " / " + KSPUtil.LocalizeNumber(maxSize.y, "0.0") + _cacheAutoLOC_7001411 + "</color>\n<color=" +
                            sizeSpanHex + ">" + KSPUtil.LocalizeNumber(craftSize.x, "0.0") + _cacheAutoLOC_7001411 + " / " +
                            KSPUtil.LocalizeNumber(maxSize.x, "0.0") +
                            _cacheAutoLOC_7001411 + "</color>\n<color=" + sizeTHgtHex + ">" + KSPUtil.LocalizeNumber(craftSize.z, "0.0") + _cacheAutoLOC_7001411 + " / " +
                            KSPUtil.LocalizeNumber(maxSize.z, "0.0") + _cacheAutoLOC_7001411 + "</color></line-height>";
            }
            else
            {
                _refERsizeRH.text = "<line-height=110%> \n<color=" + sizeForeAftHex + ">" + KSPUtil.LocalizeNumber(craftSize.y, "0.0") + _cacheAutoLOC_7001411 +
                "</color>\n<color=" + sizeSpanHex + ">" + KSPUtil.LocalizeNumber(craftSize.x, "0.0") + _cacheAutoLOC_7001411 +
                "</color>\n<color=" + sizeTHgtHex + ">" + KSPUtil.LocalizeNumber(craftSize.z, "0.0") + _cacheAutoLOC_7001411 + "</color></line-height>";
            }

            bool allGood = humanRatingOK &&
                            totalMass <= massLimit &&
                            totalMass >= minMassLimit &&
                              craftSize.x <= maxSize.x &&
                                craftSize.y <= maxSize.y &&
                                 craftSize.z <= maxSize.z;

            _refERappFrame.header.color = allGood ? XKCDColors.ElectricLime : XKCDColors.Orange;

            if (!allGood)
            {
                EngineersReport.Instance.appLauncherButton.sprite.color = XKCDColors.Orange;
            }
            if (allGood)
            {
                EngineersReport.Instance.appLauncherButton.sprite.color = Color.white;
            }
        }

        private static void EnsureLocalizationsCached()
        {
            if (!_engineerLocCached)
            {
                _engineerLocCached = true;

                _cacheAutoLOC_443417 = KSP.Localization.Localizer.Format("#autoLOC_443417");
                _cacheAutoLOC_443418 = KSP.Localization.Localizer.Format("#autoLOC_443418");
                _cacheAutoLOC_443419 = KSP.Localization.Localizer.Format("#autoLOC_443419");
                _cacheAutoLOC_443420 = KSP.Localization.Localizer.Format("#autoLOC_443420");
                _cacheAutoLOC_7001411 = KSP.Localization.Localizer.Format("#autoLOC_7001411");
            }
        }
    }
}
