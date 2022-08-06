using UnityEngine;
using KSP.Localization;
using System.Collections.Generic;
using RP0.ProceduralAvionics;
using KerbalConstructionTime;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class LocalizationHandler : MonoBehaviour
    {
        public void Awake()
        {
            DontDestroyOnLoad(this);
            GameEvents.onLanguageSwitched.Add(OnLanguageChange);
            OnLanguageChange(); // Initialize
        }

        private void OnLanguageChange()
        {
            UpdateLocalizedText();
        }

        public static void UpdateLocalizedText()
        {
            KSCContextMenuOverrider.AreTextsUpdated = false;

            foreach (PartUpgradeHandler.Upgrade up in PartUpgradeManager.Handler)
            {
                // Proc avionics upgrades
                if (up.name.StartsWith("procAvionics-tltech"))
                {
                    string desc = BuildAvionicsStats(up.techRequired);
                    if (!string.IsNullOrEmpty(desc))
                        up.description = Localizer.GetStringByTag("#rp0AvionicsUpgradeText") + desc;

                    continue;
                }
                
                if (up.name.StartsWith("rp0EngineerUpgrade"))
                {
                    up.description = Localizer.Format("#rp0EngineerUpgradeText", (PresetManager.Instance.ActivePreset.GeneralSettings.EngineerEfficiencyUpgrades.GetValue(up.techRequired) * 100d).ToString("N0"));
                    continue;
                }

                if (up.name.StartsWith("rp0ResearcherUpgrade"))
                {
                    up.description = Localizer.Format("#rp0ResearcherUpgradeText", (PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherEfficiencyUpgrades.GetValue(up.techRequired) * 100d).ToString("N0"));
                    continue;
                }
            }
        }

        
        private static string BuildAvionicsStats(string techRequired)
        {
            string retStr = string.Empty;
            foreach (var avConfig in ProceduralAvionicsTechManager.AllAvionicsConfigs)
            {
                for (int i = 1; i < avConfig.TechNodesSorted.Length; ++i)
                {
                    ProceduralAvionicsTechNode node = avConfig.TechNodesSorted[i];
                    if (node.TechNodeName == techRequired)
                    {
                        ModuleProceduralAvionics.GetStatsForTechNode(avConfig.TechNodesSorted[i - 1], -1f, out float massOld, out float costOld, out float powerOld);
                        ModuleProceduralAvionics.GetStatsForTechNode(node, -1f, out float massNew, out float costNew, out float powerNew);

                        retStr += "\n" + Localizer.Format("#rp0AvionicsUpgradeTextLine",
                            Localizer.GetStringByTag("#rp0AvionicsType_" + avConfig.name),
                            FormatRatioAsPercent(massNew / massOld),
                            FormatRatioAsPercent(costNew / costOld),
                            FormatRatioAsPercent(powerNew / powerOld),
                            FormatBytes(node.kosDiskSpace));

                        break;
                    }
                }
            }

            return retStr;
        }

        public static void UpdateFacilityLevelStats(Upgradeables.UpgradeableObject.UpgradeLevel lvl, int lvlIdx, double fracLevel)
        {
            // levelText appears to be unused by KSP itself. We can use it to store original level stats.
            // Restoring old values is necessary because those persist between scene changes but some of them are based on current KCT settings.
            if (lvl.levelText == null)
            {
                lvl.levelText = ScriptableObject.CreateInstance<KSCUpgradeableLevelText>();
                lvl.levelText.facility = lvl.levelStats.facility;
                lvl.levelText.linePrefix = lvl.levelStats.linePrefix;
                lvl.levelText.textBase = lvl.levelStats.textBase;
            }
            else
            {
                lvl.levelStats.textBase = lvl.levelText.textBase;
            }

            RP0Debug.Log($"Overriding level stats text for {lvl.levelStats.facility} lvl {lvlIdx}");

            SpaceCenterFacility facilityType = lvl.levelStats.facility;
            if (facilityType == SpaceCenterFacility.VehicleAssemblyBuilding || facilityType == SpaceCenterFacility.LaunchPad)
            {
                lvl.levelStats.linePrefix = string.Empty;
                lvl.levelStats.textBase = "#autoLOC_rp0FacilityContextMenuVAB";
            }
            else if (facilityType == SpaceCenterFacility.SpaceplaneHangar || facilityType == SpaceCenterFacility.Runway)
            {
                lvl.levelStats.linePrefix = string.Empty;
                lvl.levelStats.textBase = "#autoLOC_rp0FacilityContextMenuSPH";
            }
            else if (facilityType == SpaceCenterFacility.ResearchAndDevelopment)
            {
                if (PresetManager.Instance != null)
                {
                    int limit = PresetManager.Instance.ActivePreset.ResearcherCaps[lvlIdx];
                    lvl.levelStats.textBase += $"\n{Localizer.GetStringByTag("#rp0FacilityContextMenuRnD_ResearcherLimit")} {(limit == -1 ? "#rp0FacilityContextMenuRnD_ResearcherLimit_unlimited" : limit.ToString("N0"))}";
                }
            }
            else if (facilityType == SpaceCenterFacility.AstronautComplex)
            {
                double rrMult = Crew.CrewHandler.RnRMultiplierFromACLevel(fracLevel);
                double trainingTimeMult = 1d / Crew.TrainingCourse.FacilityTrainingRate(fracLevel);

                if (rrMult != 1d)
                    lvl.levelStats.textBase += $"\n{Localizer.Format("#autoLOC_rp0FacilityContextMenuAC_RnR", FormatRatioAsPercent(rrMult))}";

                if (trainingTimeMult != 1d)
                    lvl.levelStats.textBase += $"\n{Localizer.Format("#autoLOC_rp0FacilityContextMenuAC_Training", FormatRatioAsPercent(trainingTimeMult))}";
            }
        }

        public static string FormatRatioAsPercent(double ratio)
        {
            if (ratio < 1d)
                return Localizer.Format("#rp0NegativePercent", ((1d - ratio) * 100d).ToString("N0"));

            return Localizer.Format("#rp0PositivePercent", ((ratio - 1d) * 100d).ToString("N0"));
        }

        public static string FormatBytes(int amount)
        {
            if (amount < 1024)
                return Localizer.Format("#rp0DiskSpaceB", amount);
            if(amount < 1024*1024)
                return Localizer.Format("#rp0DiskSpacekB", (amount / 1024d).ToString("0.#"));

            return Localizer.Format("#rp0DiskSpaceMB", (amount / (1024d * 1024d)).ToString("0.#"));
        }

        public static string FormatValuePositiveNegative(double value, string format)
        {
            if (value < 0d)
                return Localizer.Format("#rp0NegativeValue", (-value).ToString(format));

            return Localizer.Format("#rp0PositiveValue", value.ToString(format));
        }
    }
}
