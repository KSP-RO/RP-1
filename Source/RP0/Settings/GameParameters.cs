using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class GameVariablesCorrector : MonoBehaviour
    {
        public void Update()
        {
            GameVariables.Instance.contractPrestigeTrivial = 1f;
            GameVariables.Instance.contractPrestigeSignificant = 1f;
            GameVariables.Instance.contractPrestigeExceptional = 1f;

            Destroy(this);
        }
    }

    public class RP0Settings : GameParameters.CustomParameterNode
    {
        public override string Title { get { return "General Settings"; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "RP-1"; } }
        public override string DisplaySection { get { return "RP-1"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return true; } }

        [GameParameters.CustomParameterUI("Include craft files", toolTip = "", newGameOnly = true)]
        public bool IncludeCraftFiles = true;

        [GameParameters.CustomParameterUI("Crews require proficiency training", toolTip = "Astronauts must complete lengthy proficiency training prior to their first launch in each cockpit or capsule.")]
        public bool IsTrainingEnabled = true;

        [GameParameters.CustomParameterUI("Crews require mission training", toolTip = "Crews also require shorter mission-specific training prior to each launch.")]
        public bool IsMissionTrainingEnabled = true;

        [GameParameters.CustomParameterUI("Enable crew retirement", toolTip = "Re-enabling this option can cause some of the older crewmembers to instantly retire.")]
        public bool IsRetirementEnabled = true;

        [GameParameters.CustomParameterUI("Downtime post mission", toolTip = "Enable crew downtime post mission, crew will be unavailable for flight/training.")]
        public bool IsCrewRnREnabled = true;

        [GameParameters.CustomFloatParameterUI("Starting Confidence", minValue = 0f, maxValue = 1000f, stepCount = 21, displayFormat = "N0", gameMode = GameParameters.GameMode.CAREER)]
        public float StartingConfidence = 500f;

        [GameParameters.CustomFloatParameterUI("Starting Unlock Credit", minValue = 0f, maxValue = 100000f, stepCount = 21, displayFormat = "N0", gameMode = GameParameters.GameMode.CAREER)]
        public float StartingUnlockCredit = 25000f;

        [GameParameters.CustomFloatParameterUI("Build/Integration Rate", minValue = 0.1f, maxValue = 1.5f, stepCount = 15, displayFormat = "N1", gameMode = GameParameters.GameMode.CAREER)]
        public float BuildRate = 1f;

        [GameParameters.CustomFloatParameterUI("Research Rate", minValue = 0.1f, maxValue = 1.5f, stepCount = 15, displayFormat = "N1", gameMode = GameParameters.GameMode.CAREER)]
        public float ResearchRate = 1f;

        [GameParameters.CustomFloatParameterUI("Kerbal Death Fixed Rep Loss", minValue = 20f, maxValue = 200f, stepCount = 31, displayFormat = "N0", gameMode = GameParameters.GameMode.CAREER)]
        public float RepLossNautDeathFixed = 50f;

        [GameParameters.CustomFloatParameterUI("Kerbal Death Percent Rep Loss", minValue = 0.05f, maxValue = 0.5f, stepCount = 46, displayFormat = "P0", gameMode = GameParameters.GameMode.CAREER)]
        public float RepLossNautDeathPercent = 0.1f;

        [GameParameters.CustomParameterUI("Enable career progress logging", gameMode = GameParameters.GameMode.CAREER)]
        public bool CareerLogEnabled = true;

        [GameParameters.CustomParameterUI("Kerbalism resource handling for avionics", toolTip = "Use Kerbalism (enabled) or Stock (disabled) rules for resource consumption during the flight scene.")]
        public bool AvionicsUseKerbalism = false;

        [GameParameters.CustomParameterUI("Procedural avionics window auto opens", toolTip = "When enabled, the Procedural Avionics configuration window is automatically opened when you right click on a part with Proc Avionics.")]
        public bool IsProcAvionicsAutoShown = true;

        [GameParameters.CustomParameterUI("Show tooling reminders", toolTip = "When enabled, a warning is shown on integration when the vessel has untooled parts.")]
        public bool ShowToolingReminders = true;

        // The following values are persisted to the savegame but are not shown in the difficulty settings UI
        public int CommsPayload = ContractGUI.MinPayload;
        public int WeatherPayload = ContractGUI.MinPayload;
        public string NewspaperTitle = "Space Gazette";
        public bool UseLastScreenshot = false;

        public bool AirlaunchTipShown = false;
        public bool Avionics_InterplanetaryWarningShown = false;
        public bool AvionicsWindow_ShowInfo1 = true;
        public bool AvionicsWindow_ShowInfo2 = true;
        public bool AvionicsWindow_ShowInfo3 = true;
        public bool NeverShowUntrainedReminders = false;
        public bool NeverShowHSFProgramReminders = false;
        public int RATLTipShown = 0;

        public string CareerLog_URL;
        public string CareerLog_Token;

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            bool isCareer = MainMenu.newGameMode == Game.Modes.CAREER;
            IsTrainingEnabled = IsMissionTrainingEnabled = IsCrewRnREnabled = IsRetirementEnabled = isCareer;
            CareerLogEnabled = isCareer;
            ShowToolingReminders = isCareer;

            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    IncludeCraftFiles = true;
                    RepLossNautDeathFixed = 50f;
                    RepLossNautDeathPercent = 0.1f;
                    StartingConfidence = 750f;
                    StartingUnlockCredit = 75000f;
                    BuildRate = 1.5f;
                    ResearchRate = 1.5f;
                    break;
                case GameParameters.Preset.Normal:
                    IncludeCraftFiles = true;
                    RepLossNautDeathFixed = 50f;
                    RepLossNautDeathPercent = 0.1f;
                    StartingConfidence = 500f;
                    StartingUnlockCredit = 50000f;
                    BuildRate = 1.2f;
                    ResearchRate = 1.2f;
                    break;
                case GameParameters.Preset.Moderate:
                    IncludeCraftFiles = false;
                    RepLossNautDeathFixed = 50f;
                    RepLossNautDeathPercent = 0.1f;
                    StartingConfidence = 500f;
                    StartingUnlockCredit = 25000f;
                    BuildRate = 1f;
                    ResearchRate = 1f;
                    break;
                case GameParameters.Preset.Hard:
                    IncludeCraftFiles = false;
                    RepLossNautDeathFixed = 50f;
                    RepLossNautDeathPercent = 0.2f;
                    StartingConfidence = 350f;
                    StartingUnlockCredit = 15000f;
                    BuildRate = 1f;
                    ResearchRate = 1f;
                    break;
            }
        }

        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            if (member.Name == "IsMissionTrainingEnabled")
            {
                IsMissionTrainingEnabled &= IsTrainingEnabled;
                return IsTrainingEnabled;
            }

            return base.Interactible(member, parameters);
        }
    }
}
