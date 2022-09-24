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

            // Handle sprite conversion
            TMPro.TMP_SpriteAsset asset = Resources.Load<TMPro.TMP_SpriteAsset>("sprite assets/CurrencySpriteAsset");
            asset.spriteSheet = GameDatabase.Instance.GetTexture("RP-0/Resources/CurrencySprites", false);
            TMPro.ShaderUtilities.GetShaderPropertyIDs();
            asset.material.SetTexture(TMPro.ShaderUtilities.ID_MainTex, asset.spriteSheet);
        }

        private void OnLanguageChange()
        {
            UpdateLocalizedText();
        }

        public static void UpdateLocalizedText()
        {
            Harmony.PatchKSCFacilityContextMenu.AreTextsUpdated = false;
            foreach (PartUpgradeHandler.Upgrade up in PartUpgradeManager.Handler)
            {
                // Proc avionics upgrades
                if (up.name.StartsWith("procAvionics-tltech"))
                {
                    string desc = BuildAvionicsStats(up.techRequired);
                    if (!string.IsNullOrEmpty(desc))
                        up.description = Localizer.Format("#rp0_Avionics_Upgrade_Text") + desc;

                    continue;
                }

                if (PresetManager.Instance != null && PresetManager.Instance.ActivePreset != null)
                {
                    if (up.name.StartsWith("rp0EngineerUpgrade"))
                    {
                        up.description = Localizer.Format("#rp0_EfficiencyUpgrade_Engineers_Text", 
                            (PresetManager.Instance.ActivePreset.GeneralSettings.LCEfficiencyUpgradesMin.GetValue(up.techRequired) * 100d).ToString("N0"),
                            (PresetManager.Instance.ActivePreset.GeneralSettings.LCEfficiencyUpgradesMax.GetValue(up.techRequired) * 100d).ToString("N0"));
                        continue;
                    }

                    if (up.name.StartsWith("rp0ResearcherUpgrade"))
                    {
                        up.description = Localizer.Format("#rp0_EfficiencyUpgrade_Researchers_Text", (PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherEfficiencyUpgrades.GetValue(up.techRequired) * 100d).ToString("N0"));
                        continue;
                    }
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

                        retStr += "\n" + Localizer.Format("#rp0_Avionics_Upgrade_TextLine",
                            Localizer.Format("#rp0_Avionics_Type_" + avConfig.name),
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
                lvl.levelStats.textBase = "#autoLOC_rp0_FacilityContextMenu_VAB";
            }
            else if (facilityType == SpaceCenterFacility.SpaceplaneHangar || facilityType == SpaceCenterFacility.Runway)
            {
                lvl.levelStats.linePrefix = string.Empty;
                lvl.levelStats.textBase = "#autoLOC_rp0_FacilityContextMenu_SPH";
            }
            else if (facilityType == SpaceCenterFacility.ResearchAndDevelopment)
            {
                if (PresetManager.Instance != null)
                {
                    int limit = PresetManager.Instance.ActivePreset.ResearcherCaps[lvlIdx];
                    lvl.levelStats.textBase += $"\n{Localizer.Format("#rp0_FacilityContextMenu_RnD_ResearcherLimit", (limit == -1 ? Localizer.Format("#rp0_FacilityContextMenu_RnD_ResearcherLimit_unlimited") : limit.ToString("N0")))}";
                }
            }
            else if (facilityType == SpaceCenterFacility.AstronautComplex)
            {
                double rrMult = Crew.CrewHandler.RnRMultiplierFromACLevel(fracLevel);
                double trainingTimeMult = 1d / Crew.TrainingCourse.FacilityTrainingRate(fracLevel);

                if (rrMult != 1d)
                    lvl.levelStats.textBase += $"\n{Localizer.Format("#autoLOC_rp0_FacilityContextMenu_AC_RnR", FormatRatioAsPercent(rrMult))}";

                if (trainingTimeMult != 1d)
                    lvl.levelStats.textBase += $"\n{Localizer.Format("#autoLOC_rp0_FacilityContextMenu_AC_Training", FormatRatioAsPercent(trainingTimeMult))}";
            }
        }

        public static string FormatRatioAsPercent(double ratio)
        {
            if (ratio < 1d)
                return Localizer.Format("#rp0_Generic_Percent_Negative", ((1d - ratio) * 100d).ToString("N0"));

            return Localizer.Format("#rp0_Generic_Percent_Positive", ((ratio - 1d) * 100d).ToString("N0"));
        }

        public static string FormatBytes(int amount)
        {
            if (amount < 1024)
                return Localizer.Format("#rp0_Generic_DiskSpace_B", amount);
            if(amount < 1024*1024)
                return Localizer.Format("#rp0_Generic_DiskSpace_kB", (amount / 1024d).ToString("0.#"));

            return Localizer.Format("#rp0_Generic_DiskSpace_MB", (amount / (1024d * 1024d)).ToString("0.#"));
        }

        public static string FormatValuePositiveNegative(double value, string format)
        {
            if (value < 0d)
                return Localizer.Format("#rp0_Generic_Value_Negative", (-value).ToString(format));

            return Localizer.Format("#rp0_Generic_Value_Positive", value.ToString(format));
        }

        public static string FormatList(List<string> list, bool isAnd = true)
        {
            if (list.Count == 0)
                return string.Empty;

            if (list.Count == 1)
                return list[0];

            return Localizer.Format($"<<{(isAnd ? "and" : "or")}(1,{list.Count})>>", list.ToArray());
        }
    }
}
