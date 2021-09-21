using ClickThroughFix;
using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        private const float TooltipMaxWidth = 200f;
        private const double TooltipShowDelay = 500;

        private static Rect _tooltipRect;
        private static GUIStyle _tooltipStyle;
        private static DateTime _tooltipBeginDt;
        private static int _tooltipActiveWindowId;
        private static string _tooltipText = string.Empty;
        private static string _prevNonEmptyTooltipText = string.Empty;
        private static bool _isTooltipChanged;

        /// <summary>
        /// Needs to be called after every scene change in Start(). Somehow the background texture goes missing on those.
        /// </summary>
        public static void InitTooltips()
        {
            Texture2D backTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            backTex.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f));
            backTex.Apply();

            _tooltipStyle = new GUIStyle(HighLogic.Skin.label);
            _tooltipStyle.normal.background = backTex;
            _tooltipStyle.normal.textColor = new Color32(224, 224, 224, 255);
            _tooltipStyle.padding = new RectOffset(3, 3, 3, 3);
            _tooltipStyle.alignment = TextAnchor.MiddleCenter;
        }

        private static Rect DrawWindowWithTooltipSupport(Rect pos, string windowName, string windowTitle, Action<int> drawWindow)
        {
            int windowID = WindowHelper.NextWindowId(windowName);
            Rect newPos = ClickThruBlocker.GUILayoutWindow(windowID, pos, (_) => DrawWindowAndRecordTooltip(windowID, drawWindow), windowTitle, HighLogic.Skin.window);

            ShowTooltip();

            return newPos;
        }

        private static void DrawWindowAndRecordTooltip(int windowID, Action<int> drawWindow)
        {
            drawWindow(windowID);
            RecordTooltip(windowID);
        }

        private static void RecordTooltip(int windowID)
        {
            if (Event.current.type == EventType.Repaint)
            {
                _tooltipText = GUI.tooltip;
                if (!string.IsNullOrEmpty(_tooltipText))
                {
                    _tooltipActiveWindowId = windowID;
                    if (_tooltipText != _prevNonEmptyTooltipText)
                    {
                        _isTooltipChanged = true;
                        _tooltipBeginDt = DateTime.UtcNow;
                        _prevNonEmptyTooltipText = _tooltipText;
                    }
                }
                else if (windowID == _tooltipActiveWindowId)
                {
                    _tooltipActiveWindowId = 0;
                    _isTooltipChanged = true;
                    _prevNonEmptyTooltipText = string.Empty;
                }
            }
        }

        private static void ShowTooltip()
        {
            if (!string.IsNullOrEmpty(_tooltipText) &&
                (DateTime.UtcNow - _tooltipBeginDt).TotalMilliseconds > TooltipShowDelay)
            {
                if (_isTooltipChanged)
                {
                    var c = new GUIContent(_tooltipText);
                    _tooltipStyle.CalcMinMaxWidth(c, out _, out float width);

                    width = Math.Min(width, TooltipMaxWidth);
                    float height = _tooltipStyle.CalcHeight(c, TooltipMaxWidth);
                    _tooltipRect = new Rect(
                        Input.mousePosition.x + 15,
                        Screen.height - Input.mousePosition.y + 10,
                        width, height);
                    _isTooltipChanged = false;
                }

                int id = WindowHelper.NextWindowId("DrawTooltipWindow");
                GUI.Window(
                    id,
                    _tooltipRect,
                    (_) => { },
                    _tooltipText,
                    _tooltipStyle);
                GUI.BringWindowToFront(id);
            }
        }
    }
}
