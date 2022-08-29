using System;
using UnityEngine;
using KSP.UI.Screens;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.FlightEditorAndKSC, false)]
    public class UIHolder : MonoBehaviour
    {
        public static GUISkin RescaledKSPSkin
        {
            get
            {
                if (_rescaledKSPSkin == null)
                    _rescaledKSPSkin = RescaleSkin(HighLogic.Skin);
                
                return _rescaledKSPSkin;
            }
        }
        private static GUISkin _rescaledKSPSkin = null;

        public static GUISkin RescaledSkin
        {
            get
            {
                if (_rescaledSkin == null)
                {
                    GUI.skin = null; // reset GUI.skin to default
                    _rescaledSkin = RescaleSkin(GUI.skin);
                }
                
                return _rescaledSkin;
            }
        }
        private static GUISkin _rescaledSkin = null;

        
        public static float UIScale => GameSettings.UI_SCALE * GameSettings.UI_SCALE_APPS;

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
            GameEvents.OnGameSettingsApplied.Add(OnGameSettingsApplied);
        }

        protected void Start()
        {
            // force reset rescaling of the UI
            _rescaledKSPSkin = null;
            _rescaledSkin = null;

            _tw = new TopWindow();
            _tw.Start();

            Tooltip.RecreateInstance();    // Need to make sure that a new Tooltip instance is created after every scene change
        }

        protected void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
            GameEvents.onGameSceneLoadRequested.Remove(OnSceneChange);
            GameEvents.OnGameSettingsApplied.Add(OnGameSettingsApplied);
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
            _wasGuiEnabled = _isGuiEnabled;
            if (_isGuiEnabled)
                _button.toggleButton.Value = false;
        }

        public void ShowIfWasHidden()
        {
            if(_wasGuiEnabled && !_isGuiEnabled)
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
                ApplicationLauncher.AppScenes.SPACECENTER | ApplicationLauncher.AppScenes.VAB | ApplicationLauncher.AppScenes.SPH,
                GameDatabase.Instance.GetTexture("RP-0/maintecost", false));
        }

        private void OnGameSettingsApplied()
        {
            // force reset rescaling of the UI and tooltip style
            _rescaledKSPSkin = null;
            _rescaledSkin = null;
            
            Tooltip.ResetTooltipStyle();

            _tw = new TopWindow();
            _tw.Start();
            
            // reset KCT UI
            KerbalConstructionTime.KCT_GUI.ResetStylesAndPositions();
        }
        
        // Code ripped from principia
        // https://github.com/mockingbirdnest/Principia/blob/master/ksp_plugin_adapter/window_renderer.cs#L32-L82
        private static GUISkin RescaleSkin(GUISkin template)
        {
            GUISkin skin = UnityEngine.Object.Instantiate(template);

            // Creating a dynamic font as is done below results in Unity producing
            // incorrect character bounds and everything looks ugly.  They even
            // "document" it in their source code, see
            // https://github.com/Unity-Technologies/UnityCsReference/blob/57f723ec72ca50427e5d17cad0ec123be2372f67/Modules/GraphViewEditor/Views/GraphView.cs#L262.
            // So here I am, sizing a pangram to get an idea of the shape of things and
            // nudging pixels by hand.  It's the 90's, go for it!
            var pangram = new GUIContent("Portez ce vieux whisky au juge blond qui fume.");
            float buttonHeight = skin.button.CalcHeight(pangram, width : 1000);
            float labelHeight = skin.label.CalcHeight(pangram, width : 1000);
            float textAreaHeight = skin.textArea.CalcHeight(pangram, width : 1000);
            float textFieldHeight = skin.textField.CalcHeight(pangram, width : 1000);
            float toggleHeight = skin.toggle.CalcHeight(pangram, width : 1000);

            skin.font = Font.CreateDynamicFontFromOSFont(
                skin.font.fontNames,
                (int)(skin.font.fontSize * UIScale));
            skin.font.material.mainTexture.filterMode = FilterMode.Bilinear;
            skin.font.material.mainTexture.anisoLevel = 4;

            skin.button.contentOffset = new Vector2(0, -buttonHeight * UIScale / 10);
            skin.button.fixedHeight = buttonHeight * UIScale;
            skin.horizontalSlider.fixedHeight = 21 * UIScale;
            skin.horizontalSliderThumb.fixedHeight = 21 * UIScale;
            skin.horizontalSliderThumb.fixedWidth = 12 * UIScale;
            skin.label.contentOffset = new Vector2(0, -labelHeight * UIScale / 20);
            skin.label.fixedHeight = labelHeight * UIScale;
            skin.label.wordWrap = true;
            skin.textArea.contentOffset = new Vector2(0, -textAreaHeight * UIScale / 20);
            skin.textArea.fixedHeight = textAreaHeight * UIScale;
            skin.textField.contentOffset = new Vector2(0, -textAreaHeight * UIScale / 20);
            skin.textField.fixedHeight = textFieldHeight * UIScale;
            skin.toggle.fixedHeight = toggleHeight * UIScale;
            skin.toggle.contentOffset = new Vector2(0, -toggleHeight * (UIScale - 1) / 3);
            skin.toggle.margin = new RectOffset(
                (int)(skin.toggle.margin.left * UIScale),
                skin.toggle.margin.right,
                (int)(skin.toggle.margin.top * 1.7 * UIScale),
                skin.toggle.margin.bottom);
            return skin;
        }
        
        public static GUILayoutOption Width(float units)
        {
            return GUILayout.Width(units * UIScale);
        }
        
        public static GUILayoutOption Height(float units)
        {
            return GUILayout.Height(units * UIScale);
        }

        public static GUILayoutOption MaxWidth(float units)
        {
            return GUILayout.MaxWidth(units * UIScale);
        }
        
        public static GUILayoutOption MaxHeight(float units)
        {
            return GUILayout.MaxHeight(units * UIScale);
        }
        
        public static void Space(float units)
        {
            GUILayout.Space((int) (units * UIScale));
        }
    }
}
