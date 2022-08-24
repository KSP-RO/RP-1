using ClickThroughFix;
using System;
using UnityEngine;
using System.Collections.Generic;
using RP0;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private static float TooltipMaxWidth => 200f * RP0.UIHolder.UIScale;
        private static double TooltipShowDelay => 500 * RP0.UIHolder.UIScale;

        private static Rect _tooltipRect;
        private static GUIStyle _tooltipStyle;
        private static DateTime _tooltipBeginDt;
        private static readonly Dictionary<int, string> _windowTooltipTexts = new Dictionary<int, string>();
        private static bool _isTooltipChanged;

        public static void ClearTooltips()
        {
            _windowTooltipTexts.Clear();
        }

        /// <summary>
        /// Needs to be called after every scene change in Start(). Somehow the background texture goes missing on those.
        /// </summary>
        public static void InitTooltips()
        {
            ClearTooltips();

            if (_tooltipStyle == null)
            {
                _tooltipStyle = new GUIStyle(UIHolder.RescaledKSPSkin.label);
                _tooltipStyle.normal.textColor = new Color32(224, 224, 224, 255);
                _tooltipStyle.padding = new RectOffset(
                    (int)(3 * UIHolder.UIScale), 
                    (int)(3 * UIHolder.UIScale), 
                    (int)(3 * UIHolder.UIScale), 
                    (int)(3 * UIHolder.UIScale));
                _tooltipStyle.alignment = TextAnchor.MiddleCenter;
                _tooltipStyle.font = UIHolder.RescaledKSPSkin.font;
                _tooltipStyle.fontSize = UIHolder.RescaledKSPSkin.font.fontSize;
                _tooltipStyle.fixedHeight = 0;
                _tooltipStyle.wordWrap = true;
            }

            Texture2D backTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            backTex.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f));
            backTex.Apply();
            _tooltipStyle.normal.background = backTex;
        }

        private static Rect DrawWindowWithTooltipSupport(Rect pos, string windowName, string windowTitle, Action<int> drawWindow)
        {
            int windowID = WindowHelper.NextWindowId(windowName);
            Rect newPos = ClickThruBlocker.GUILayoutWindow(windowID, pos, (_) => DrawWindowAndRecordTooltip(windowID, drawWindow), windowTitle, HighLogic.Skin.window);

            ShowTooltip(windowID);

            return newPos;
        }

        private static void DrawWindowAndRecordTooltip(int windowID, Action<int> drawWindow)
        {
            drawWindow(windowID);
            RecordTooltip(windowID);
        }

        private static void RecordTooltip(int windowID)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (!_windowTooltipTexts.TryGetValue(windowID, out string tooltipText))
            {
                tooltipText = string.Empty;
            }

            if (GUI.tooltip != tooltipText)
            {
                _isTooltipChanged = true;
                // Hack: If the tooltip identifier is unchanged, but the text is changed, *immediatley* show the replacement tooltip.
                // Otherwise, behave as normal
                int idx = GUI.tooltip.IndexOf('¶');
                int rIdx;
                if (idx != -1 && (rIdx = tooltipText.IndexOf('¶')) != -1 && idx == rIdx && string.Compare(GUI.tooltip, 0, tooltipText, 0, idx) == 0)
                {
                    _tooltipBeginDt = DateTime.MinValue;
                }
                else
                {
                    _tooltipBeginDt = DateTime.UtcNow;
                }

                // Store the identifier and the text
                _windowTooltipTexts[windowID] = GUI.tooltip;
            }
        }

        private static void ShowTooltip(int windowID, TextAnchor contentAlignment = TextAnchor.MiddleCenter)
        {
            if (_windowTooltipTexts.TryGetValue(windowID, out string tooltipText) && !string.IsNullOrEmpty(tooltipText) &&
                (DateTime.UtcNow - _tooltipBeginDt).TotalMilliseconds > TooltipShowDelay)
            {
                // overwrite the standard KSP skin with our own rescaled version
                var oldSkin = GUI.skin;
                GUI.skin = UIHolder.RescaledKSPSkin;
                
                int idx = tooltipText.IndexOf('¶');
                if (idx != -1)
                    tooltipText = tooltipText.Substring(idx + 1);

                if (_isTooltipChanged)
                {
                    var c = new GUIContent(tooltipText);
                    _tooltipStyle.CalcMinMaxWidth(c, out _, out float width);
                    _tooltipStyle.alignment = contentAlignment;
                    
                    width = Math.Min(width, TooltipMaxWidth);
                    float height = _tooltipStyle.CalcHeight(c, width);
                    _tooltipRect = new Rect(
                        Math.Min(Screen.width - width, Input.mousePosition.x + 15),
                        Math.Min(Screen.height - height, Screen.height - Input.mousePosition.y + 10),
                        width, height);
                    _isTooltipChanged = false;
                }

                int id = WindowHelper.NextWindowId("DrawTooltipWindow");
                GUI.Window(
                    id,
                    _tooltipRect,
                    (_) => { },
                    tooltipText,
                    _tooltipStyle);
                GUI.BringWindowToFront(id);

                GUI.skin = oldSkin;
            }
        }
        
        public static void ResetTooltipStyle()
        {
            _tooltipStyle = null;
        }
    }
}
