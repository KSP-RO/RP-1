using UnityEngine;
using KSP.UI.Screens;
using System.Collections.Generic;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
    public class UIHolder : MonoBehaviour
    {
        private bool _isGuiEnabled = false;
        private Stack<bool> _wasGuiEnabled = new Stack<bool>();
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
            _tw.Destroy();
            _tw = null;
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
            if (_isGuiEnabled && !Milestones.NewspaperUI.IsOpen)
                _tw.OnGUI();
        }

        public void HideIfShowing()
        {
            _wasGuiEnabled.Push(_isGuiEnabled);
            if (_isGuiEnabled)
                _button.toggleButton.Value = false;
        }

        public void ShowIfWasHidden()
        {
            var oldVal = _wasGuiEnabled.Pop();
            if (oldVal && !_isGuiEnabled)
                _button.toggleButton.Value = true;
        }

        public void ShowWindow()
        {
            _isGuiEnabled = true;
        }

        public void HideWindow()
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
                ApplicationLauncher.AppScenes.ALWAYS & ~ApplicationLauncher.AppScenes.MAINMENU,
                GameDatabase.Instance.GetTexture("RP-1/maintecost", false));
        }
    }
}
