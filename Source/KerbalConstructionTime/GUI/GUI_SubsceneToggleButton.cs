using System.Collections.Generic;
using System.Linq;
using ToolbarControl_NS;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static GUIStyle _subsceneToggleButton;
        private static Texture2D _backgroundSubsceneToggleButton;
        private static GUIContent _upSubsceneToggleButtonContentSubsceneToggleButton;
        private static GUIContent _hoverSubsceneToggleButtonContentSubsceneToggleButton;
        private static Rect _rectSubsceneToggleButton;
        private static float _scaleSubsceneToggleButton;
        private static GUIContent _contentSubsceneToggleButton;

        private static Texture2D _upSubsceneToggleButton;
        private static Texture2D _hoverSubsceneToggleButton;

        internal static void InitSubsceneToggleButton()
        {
            _subsceneToggleButton = new GUIStyle(HighLogic.Skin.button);
            _subsceneToggleButton.margin = new RectOffset(0, 0, 0, 0);
            _subsceneToggleButton.padding = new RectOffset(0, 0, 0, 0);
            _subsceneToggleButton.border = new RectOffset(0, 0, 0, 0);
            _subsceneToggleButton.normal = _subsceneToggleButton.hover;
            _subsceneToggleButton.active = _subsceneToggleButton.hover;

            _backgroundSubsceneToggleButton = new Texture2D(2, 2);
            Color[] color = new Color[4];
            color[0] = new Color(1, 1, 1, 0);
            color[1] = color[0];
            color[2] = color[0];
            color[3] = color[0];
            _backgroundSubsceneToggleButton.SetPixels(color);

            _subsceneToggleButton.normal.background = _backgroundSubsceneToggleButton;
            _subsceneToggleButton.hover.background = _backgroundSubsceneToggleButton;
            _subsceneToggleButton.onHover.background = _backgroundSubsceneToggleButton;
            _subsceneToggleButton.active.background = _backgroundSubsceneToggleButton;
            _subsceneToggleButton.onActive.background = _backgroundSubsceneToggleButton;

            _upSubsceneToggleButton = new Texture2D(2, 2);
            _hoverSubsceneToggleButton = new Texture2D(2, 2);
            ToolbarControl.LoadImageFromFile(ref _upSubsceneToggleButton, KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/Icons/KCT_add_normal");
            ToolbarControl.LoadImageFromFile(ref _hoverSubsceneToggleButton, KSPUtil.ApplicationRootPath + "GameData/RP-0/PluginData/Icons/KCT_add_hover");
            //up = GameDatabase.Instance.GetTexture("RP-0/PluginData/Icons/KCT_add_normal", false);
            //hover = GameDatabase.Instance.GetTexture("RP-0/PluginData/Icons/KCT_add_hover", false);

            PositionAndSizeSubsceneToggleIcon();
        }

        private static void PositionAndSizeSubsceneToggleIcon()
        {
            Texture2D upTex = Texture2D.Instantiate(_upSubsceneToggleButton);
            Texture2D hoverTex = Texture2D.Instantiate(_hoverSubsceneToggleButton);

            int offset = 0;
            bool steamPresent = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "KSPSteamCtrlr");
            bool mechjebPresent = AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "MechJeb2");
            if (steamPresent)
                offset = 46;
            if (mechjebPresent)
                offset = 140;
            _scaleSubsceneToggleButton = GameSettings.UI_SCALE;

            _rectSubsceneToggleButton = new Rect(Screen.width - (304 + offset) * _scaleSubsceneToggleButton, 0, 42 * _scaleSubsceneToggleButton, 38 * _scaleSubsceneToggleButton);
            {
                TextureScale.Bilinear(upTex, (int)(_upSubsceneToggleButton.width * _scaleSubsceneToggleButton), (int)(_upSubsceneToggleButton.height * _scaleSubsceneToggleButton));
                TextureScale.Bilinear(hoverTex, (int)(_hoverSubsceneToggleButton.width * _scaleSubsceneToggleButton), (int)(_hoverSubsceneToggleButton.height * _scaleSubsceneToggleButton));
            }
            _upSubsceneToggleButtonContentSubsceneToggleButton = new GUIContent("", upTex, "");
            _hoverSubsceneToggleButtonContentSubsceneToggleButton = new GUIContent("", hoverTex, "");
        }

        private static void DoSubsceneToggleIcon()
        {
            if (_rectSubsceneToggleButton.Contains(Mouse.screenPos))
                _contentSubsceneToggleButton = _hoverSubsceneToggleButtonContentSubsceneToggleButton;
            else
                _contentSubsceneToggleButton = _upSubsceneToggleButtonContentSubsceneToggleButton;
            if (_scaleSubsceneToggleButton != GameSettings.UI_SCALE)
            {
                PositionAndSizeSubsceneToggleIcon();
            }
            // When this is true, and the mouse is NOT over the toggle, the toggle code is making the toggle active
            // which is showing the corners of the button as unfilled
            bool wasShown = GUIStates.IsMainGuiVisible;
            bool newShown = GUI.Toggle(_rectSubsceneToggleButton, wasShown, _contentSubsceneToggleButton, _subsceneToggleButton);
            if (newShown)
            {
                if (!wasShown)
                {
                    if (PrevGUIStates == null)
                        ToggleVisibility(true);
                    else
                        RestorePrevUIState();
                }
            }
            else if (wasShown)
                KCTEvents.Instance.HideAllGUIs();
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
