using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class StartupPopup : MonoBehaviour
    {
        private const string PreferenceFileName = "RP1UpgradeWarning";
        private static string PreferenceFilePath => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "PluginData",
            PreferenceFileName);

        public void Start()
        {
            if (File.Exists(PreferenceFilePath)) return;

            
                PopupDialog.SpawnPopupDialog(
                    new Vector2(0, 0),
                    new Vector2(0, 0),
                    new MultiOptionDialog(
                        "RP1StartupDialog",
                        "This is the legacy version of RP-1, and is no longer supported. There is a new version of RP-1, the Programs and Launch Complexes rework, now available. It is savebreaking, and a work in progress, but if you are starting a new save it is highly recommended to switch to that version. Please uninstall RP-1 (Legacy) and install RP-1 (PLC). You can do this by uninstalling and reinstalling the RP-1 Express Install and choosing one of the non-Legacy options.",
                        "RP-1",
                        HighLogic.UISkin,
                        new DialogGUIVerticalLayout(
                            new DialogGUIButton("Don't show again", RememberPreference, true),
                            new DialogGUIButton("Ok", () => { }, true)
                            )
                        ),
                true,
                    HighLogic.UISkin);
        }

        private static void RememberPreference()
        {
            FileInfo fi = new FileInfo(PreferenceFilePath);
            if (!Directory.Exists(fi.Directory.FullName))
                Directory.CreateDirectory(fi.Directory.FullName);

            // create empty file
            File.Create(PreferenceFilePath).Close();
        }
    }
}
