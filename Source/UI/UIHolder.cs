using System;
using UnityEngine;
using KSP.UI.Screens;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
    class UIHolder : MonoBehaviour
    {
        // GUI
        private bool guiEnabled = false;
        private ApplicationLauncherButton button;
        private TopWindow tw;

        protected void Awake()
        {
            try {
                GameEvents.onGUIApplicationLauncherReady.Add(this.OnGuiAppLauncherReady);
            } catch (Exception ex) {
                Debug.LogError("RP0 failed to register UIHolder.OnGuiAppLauncherReady");
                Debug.LogException(ex);
            }
        }

        protected void Start()
        {
            tw = new TopWindow();
            tw.Start();
        }

        private void ShowWindow()
        {
            guiEnabled = true;
        }

        private void HideWindow()
        {
            guiEnabled = false;
        }

        private void OnSceneChange(GameScenes s)
        {
            if (s == GameScenes.FLIGHT)
                HideWindow();
        }

        private void OnGuiAppLauncherReady()
        {
            try {
                button = ApplicationLauncher.Instance.AddModApplication(
                    ShowWindow,
                    HideWindow,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
                    GameDatabase.Instance.GetTexture("RP-0/maintecost", false));
                GameEvents.onGameSceneLoadRequested.Add(this.OnSceneChange);
            } catch (Exception ex) {
                Debug.LogError("RP0 failed to register UIHolder");
                Debug.LogException(ex);
            }
        }

        public void OnDestroy()
        {
            try {
                GameEvents.onGUIApplicationLauncherReady.Remove(this.OnGuiAppLauncherReady);
                if (button != null)
                    ApplicationLauncher.Instance.RemoveModApplication(button);
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        public void OnGUI()
        {
            if (guiEnabled)
                tw.OnGUI();
        }
    }
}

