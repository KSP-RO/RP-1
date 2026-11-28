using System.IO;

namespace RP0
{
    /// <summary>
    /// Per-save toggles for Space Center Management features.
    /// </summary>
    public class SCMSaveSettings
    {
        public static SCMSaveSettings Instance { get; private set; }
        public static bool IsLoaded => Instance != null;

        public static string FilePath => $"{KSPUtil.ApplicationRootPath}/saves/{HighLogic.SaveFolder}/KCT_Settings.cfg";

        [Persistent]
        public bool Enabled = true, BuildTimes = true, TechUnlockTimes = true, KSCUpgradeTimes = true;

        public SCMSaveSettings()
        {
        }

        public SCMSaveSettings(SCMSaveSettings source)
        {
            Enabled = source.Enabled;
            BuildTimes = source.BuildTimes;
            TechUnlockTimes = source.TechUnlockTimes;
            KSCUpgradeTimes = source.KSCUpgradeTimes;
        }

        public static void LoadForCurrentSave()
        {
            var settings = new SCMSaveSettings();
            if (File.Exists(FilePath))
            {
                ConfigNode root = ConfigNode.Load(FilePath);
                // Older versions stored these values inside KCT_Preset/KCT_Preset_General nodes
                ConfigNode legacy = root.GetNode("KCT_Preset")?.GetNode("KCT_Preset_General");
                ConfigNode.LoadObjectFromConfig(settings, legacy ?? root);
                RP0Debug.Log("Loaded SCM save settings.");
            }
            else if (HighLogic.CurrentGame?.Mode == Game.Modes.SANDBOX)
            {
                // Start sandbox saves with SCM disabled
                settings.Enabled = false;
            }

            SetActive(settings);
        }

        public static void SetActive(SCMSaveSettings settings)
        {
            Instance = settings;
            if (settings != null)
                LocalizationHandler.UpdateLocalizedText();
        }

        public static void Clear()
        {
            Instance = null;
        }

        public void Save()
        {
            ConfigNode node = ConfigNode.CreateConfigFromObject(this, new ConfigNode());
            node.Save(FilePath);
        }
    }
}
