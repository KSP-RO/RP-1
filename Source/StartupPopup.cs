using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using System.Linq;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class StartupPopup : MonoBehaviour
    {
        private const string PreferenceFileName = "RP1MentionLoadingImages";
        private static string PreferenceFilePath => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "PluginData",
            PreferenceFileName);

        public void Start()
        {
            if (File.Exists(PreferenceFilePath)) return;
            if (AssemblyLoader.loadedAssemblies.Any(a => a.name.Equals("ROLoadingImages", StringComparison.OrdinalIgnoreCase)))
                return;

            var options = new DialogGUIBase[] {
                new DialogGUIButton("Don't show again", RememberPreference, true),
                new DialogGUIButton("Ok", () => { }, true)
            };
            var dialog = new MultiOptionDialog("RP1StartupDialog",
                "Loading screen images have moved to a new mod, ROLoadingImages. Use CKAN to install it if you want custom loading images.",
                    "RP-1 Loading Screen Images",
                    HighLogic.UISkin, 300, options);
            PopupDialog.SpawnPopupDialog(dialog, true, HighLogic.UISkin);
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
