using System;
using UnityEngine;

namespace RP0
{
    public class Tooltip
    {
        private const float TooltipMaxWidth = 200f;
        private const double TooltipShowDelay = 500;

        private static readonly int _tooltipWindowId = "RP0Tooltip".GetHashCode();
        private static GUIStyle _tooltipStyle;
        private static Tooltip _instance;

        private Rect _tooltipRect;
        private DateTime _tooltipBeginDt;
        private string _tooltipText = string.Empty;
        private bool _isTooltipChanged;

        public static Tooltip Instance
        {
            get
            {
                if (_instance == null) RecreateInstance();
                return _instance;
            }
            private set => _instance = value;
        }

        private Tooltip() { }

        public static void RecreateInstance()
        {
            if (_tooltipStyle == null)
            {
                _tooltipStyle = new GUIStyle(HighLogic.Skin.label);
                _tooltipStyle.normal.textColor = new Color32(224, 224, 224, 255);
                _tooltipStyle.padding = new RectOffset(3, 3, 3, 3);
                _tooltipStyle.alignment = TextAnchor.MiddleCenter;
            }

            // The texture needs to be re-applied after every scene change
            Texture2D backTex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            backTex.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f));
            backTex.Apply();
            _tooltipStyle.normal.background = backTex;

            Instance = new Tooltip();
        }

        public void RecordTooltip()
        {
            if (Event.current.type == EventType.Repaint && _tooltipText != GUI.tooltip)
            {
                _isTooltipChanged = true;
                if (!string.IsNullOrEmpty(_tooltipText))
                {
                    _tooltipBeginDt = DateTime.UtcNow;
                }
                _tooltipText = GUI.tooltip;
            }
        }

        public void ShowTooltip()
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

                GUI.Window(
                    _tooltipWindowId,
                    _tooltipRect,
                    (_) => { },
                    _tooltipText,
                    _tooltipStyle);
                GUI.BringWindowToFront(_tooltipWindowId);
            }
        }
    }
}
