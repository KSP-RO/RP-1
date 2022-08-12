using KSP.UI;
using KSP.UI.Screens;
using KSP.UI.TooltipTypes;
using UniLinq;
using ToolbarControl_NS;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static GUIStyle _devPartsToggle;
        private static Texture2D _devPartsBackground;
        private static GUIContent _devPartsContent;
        private static GUIContent _devPartsOnContent;
        private static GUIContent _devPartsOffContent;
        private static Rect _devPartsRect;
        private static float _devPartsScale;

        private static Texture2D _devPartsOnTex;
        private static Texture2D _devPartsOffTex;

        public static bool DevPartsVisible = true;
        public static bool FirstOnGUIUpdate = true;

        private static int offset = 0;
        private static uint devPartsTooltipFrameCounter; //used to delay tooltip despawn for 2 frames (avoids flashing on/off 2 times on click)

        private static readonly string _tooltipOnText = "Hide Experimental parts";
        private static readonly string _tooltipOffText = "Show Experimental parts";
        private static GameObject tooltipObject = null;
        private static Tooltip_Text _tooltipPrefab = null;
        private static Tooltip_Text TooltipPrefab
        {
            get
            {
                if (_tooltipPrefab == null)
                {
                    _tooltipPrefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
                }
                return _tooltipPrefab;
            }
        }

        private static GameObject SetTooltip(string tooltip, GameObject gameObj = null)
        {
            if (gameObj == null)
            {
                gameObj = new GameObject("KCT_DevPartsTooltip");
            }

            TooltipController_Text tt = gameObj.AddOrGetComponent<TooltipController_Text>();
            if (tt != null)
            {
                tt.textString = tooltip;
                tt.prefab = TooltipPrefab;
                UIMasterController.Instance.SpawnTooltip(tt);
            }
            return gameObj;
        }


        private static readonly EditorPartListFilter<AvailablePart> expPartsFilter = new EditorPartListFilter<AvailablePart>("experimentalPartsFilter",
            (p => !ResearchAndDevelopment.IsExperimentalPart(p)));

        internal static void InitDevPartsToggle()
        {
            _devPartsToggle = new GUIStyle(HighLogic.Skin.button)
            {
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0)
            };
            _devPartsToggle.normal = _devPartsToggle.hover;
            _devPartsToggle.active = _devPartsToggle.hover;

            _devPartsBackground = new Texture2D(2, 2);
            Color[] color = new Color[4];
            color[0] = new Color(1, 1, 1, 0);
            color[1] = color[0];
            color[2] = color[0];
            color[3] = color[0];
            _devPartsBackground.SetPixels(color);

            _devPartsToggle.normal.background = _devPartsBackground;
            _devPartsToggle.hover.background = _devPartsBackground;
            _devPartsToggle.onHover.background = _devPartsBackground;
            _devPartsToggle.active.background = _devPartsBackground;
            _devPartsToggle.onActive.background = _devPartsBackground;

            _devPartsOnTex = new Texture2D(2, 2);
            _devPartsOffTex = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref _devPartsOnTex, KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/Icons/KCT_dev_parts_on");
            ToolbarControl.LoadImageFromFile(ref _devPartsOffTex, KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/Icons/KCT_dev_parts_off");

            PositionAndSizeDevPartsIcon();
        }

        private static void PositionAndSizeDevPartsIcon()
        {
            Texture2D onTex = Texture2D.Instantiate(_devPartsOnTex);
            Texture2D offTex = Texture2D.Instantiate(_devPartsOffTex);

            bool steamPresent = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "KSPSteamCtrlr");
            bool mechjebPresent = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "MechJeb2");
            if (steamPresent)
                offset = 46;
            if (mechjebPresent)
                offset = 140;
            _devPartsScale = GameSettings.UI_SCALE;

            _devPartsRect = new Rect(Screen.width - (260 + offset) * _scale, 0, 42 * _scale, 38 * _scale);
            {
                TextureScale.Bilinear(onTex, (int)(_devPartsOnTex.width * _scale), (int)(_devPartsOnTex.height * _scale));
                TextureScale.Bilinear(offTex, (int)(_devPartsOffTex.width * _scale), (int)(_devPartsOffTex.height * _scale));
            }
            _devPartsOnContent = new GUIContent("", onTex, _tooltipOnText);
            _devPartsOffContent = new GUIContent("", offTex, _tooltipOffText);

            devPartsTooltipFrameCounter = 0;
        }

        private static void CreateDevPartsToggle()
        {
            if (DevPartsVisible)
                _devPartsContent = _devPartsOnContent;
            else
                _devPartsContent = _devPartsOffContent;


            if (_devPartsScale != GameSettings.UI_SCALE)
                PositionAndSizeDevPartsIcon();

            if (EditorPartList.Instance != null && FirstOnGUIUpdate && !DevPartsVisible)
            {
                EditorPartList.Instance.ExcludeFilters.AddFilter(expPartsFilter);
                Utilities.RemoveResearchedPartsFromExperimental();
                FirstOnGUIUpdate = false;
            }

            if (GUI.Button(_devPartsRect, _devPartsContent, _devPartsToggle))
            {
                DevPartsVisible = !DevPartsVisible; // toggle manually

                if (DevPartsVisible)
                {
                    EditorPartList.Instance.ExcludeFilters.RemoveFilter(expPartsFilter);
                    Utilities.AddResearchedPartsToExperimental();
                }
                else
                {
                    EditorPartList.Instance.ExcludeFilters.AddFilter(expPartsFilter);
                    Utilities.RemoveResearchedPartsFromExperimental();
                }

                FirstOnGUIUpdate = false;
                EditorPartList.Instance.Refresh();
            }

            // hybrid IMGUI/ugui interface: the button and mouse hover detection uses IMGUI,
            // but then the tooltip uses ugui's gameobject-based system to make KSP spawn its own tooltip
            // to do this, we check when GUI.tooltip has the value we have assigned to the GUI.Button content.
            if (GUI.tooltip == _tooltipOnText || GUI.tooltip == _tooltipOffText || ++devPartsTooltipFrameCounter < 3)
            {
                tooltipObject = SetTooltip(DevPartsVisible ? _tooltipOnText : _tooltipOffText, tooltipObject);

                if (GUI.tooltip == _tooltipOnText || GUI.tooltip == _tooltipOffText)
                    devPartsTooltipFrameCounter = 0;
            }
            else if (tooltipObject != null)
            {
                GameObject.Destroy(tooltipObject);
                tooltipObject = null;
            }
        }
    }
}
