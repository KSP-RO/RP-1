using System;
using UnityEngine;
using KSP.UI.Screens;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
    public class UIHolder : MonoBehaviour
    {
        private bool _isGuiEnabled = false;
        private ApplicationLauncherButton _button;
        private TopWindow _tw;

        protected void Awake()
        {
            try
            {
                GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
            }
            catch (Exception ex)
            {
                Debug.LogError("[RP-0] failed to register UIHolder.OnGuiAppLauncherReady");
                Debug.LogException(ex);
            }
        }

        protected void Start()
        {
            _tw = new TopWindow();
        }

        protected void OnDestroy()
        {
            try
            {
                GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
                if (_button != null)
                    ApplicationLauncher.Instance.RemoveModApplication(_button);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        protected void OnGUI()
        {
            if (_isGuiEnabled)
                _tw.OnGUI();
        }

        private void ShowWindow()
        {
            _isGuiEnabled = true;
        }

        private void HideWindow()
        {
            _isGuiEnabled = false;
        }

        private void OnSceneChange(GameScenes s)
        {
            if (s == GameScenes.FLIGHT)
                HideWindow();
        }

        private void OnGuiAppLauncherReady()
        {
            try
            {
                _button = ApplicationLauncher.Instance.AddModApplication(
                    ShowWindow,
                    HideWindow,
                    null,
                    null,
                    null,
                    null,
                    ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
                    GameDatabase.Instance.GetTexture("RP-0/maintecost", false));
                GameEvents.onGameSceneLoadRequested.Add(OnSceneChange);
            }
            catch (Exception ex)
            {
                Debug.LogError("[RP-0] failed to register UIHolder");
                Debug.LogException(ex);
            }
        }
    }
}
