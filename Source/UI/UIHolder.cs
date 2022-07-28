using System;
using UnityEngine;
using KSP.UI.Screens;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
    public class UIHolder : MonoBehaviour
    {
        private bool _isGuiEnabled = false;
        private bool _wasGuiEnabled = false;
        private ApplicationLauncherButton _button;
        private TopWindow _tw;

        public static UIHolder Instance { get; protected set; }

        protected void Awake()
        {
            if (Instance != null)
                Destroy(Instance);

            Instance = this;

            GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
            GameEvents.onGameSceneLoadRequested.Add(OnSceneChange);
        }

        protected void Start()
        {
            _tw = new TopWindow();
            _tw.Start();

            Tooltip.RecreateInstance();    // Need to make sure that a new Tooltip instance is created after every scene change
        }

        protected void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneChange);
            if (_button != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(_button);
            }

            if (Instance == this)
                Instance = null;
        }

        protected void OnGUI()
        {
            if (_isGuiEnabled)
                _tw.OnGUI();
        }

        public void HideIfShowing()
        {
            _wasGuiEnabled = _isGuiEnabled;
            if (_isGuiEnabled)
                _button.toggleButton.Value = false;
        }

        public void ShowIfWasHidden()
        {
            if(_wasGuiEnabled && !_isGuiEnabled)
                _button.toggleButton.Value = true;
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
            _button = ApplicationLauncher.Instance.AddModApplication(
                ShowWindow,
                HideWindow,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
                GameDatabase.Instance.GetTexture("RP-0/maintecost", false));
        }
    }
}
