using KSP.UI;
using KSP.UI.TooltipTypes;
using ToolbarControl_NS;
using UnityEngine;

namespace RP0
{
    public abstract class GUI_TopRightButton
    {
        public enum StateMode { Single, Toggle }

        protected static GUIStyle _btnStyle;
        protected static Tooltip_Text _tooltipPrefab = null;

        protected readonly int _offset = 0;
        protected readonly StateMode _mode = 0;
        protected uint _tooltipFrameCounter = 0;    //used to delay tooltip despawn for 2 frames (avoids flashing on/off 2 times on click)
        protected Rect _btnRect;
        protected GUIContent _btnContent;
        protected GameObject _tooltipObject = null;

        public bool IsOn { get; set; }
        public string DefaultTexturePath { get; set; }
        public string DefaultHovTexturePath { get; set; }
        public string OnTexturePath { get; set; }
        public string OnHovTexturePath { get; set; }
        public string TooltipDefaultText { get; set; }
        public string TooltipOnText { get; set; }

        protected static Tooltip_Text TooltipPrefab
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

        public GUI_TopRightButton(int offset, StateMode mode)
        {
            _offset = offset;
            _mode = mode;
        }

        public virtual void Init()
        {
            if (_btnStyle == null)
            {
                _btnStyle = new GUIStyle(HighLogic.Skin.button)
                {
                    margin = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(0, 0, 0, 0)
                };
            }

            Texture2D tex_def = new Texture2D(256, 256);
            ToolbarControl.LoadImageFromFile(ref tex_def, KSPUtil.ApplicationRootPath + DefaultTexturePath);
            Texture2D tex_hov = new Texture2D(256, 256);
            ToolbarControl.LoadImageFromFile(ref tex_hov, KSPUtil.ApplicationRootPath + DefaultHovTexturePath);

            Texture2D tex_on = new Texture2D(256, 256);
            ToolbarControl.LoadImageFromFile(ref tex_on, KSPUtil.ApplicationRootPath + OnTexturePath);
            Texture2D tex_onHov = new Texture2D(256, 256);
            ToolbarControl.LoadImageFromFile(ref tex_onHov, KSPUtil.ApplicationRootPath + OnHovTexturePath);

            _btnStyle.normal.background = tex_def;
            _btnStyle.active.background = tex_def;
            _btnStyle.hover.background = tex_hov;
            _btnStyle.onNormal.background = tex_on;
            _btnStyle.onActive.background = tex_on;
            _btnStyle.onHover.background = tex_onHov;

            RescaleAndSetTextures();
        }

        public virtual void RescaleAndSetTextures()
        {
            float uiScale = GameSettings.UI_SCALE;
            _btnRect = new Rect(Screen.width - _offset * uiScale, 0, 42 * uiScale, 38 * uiScale);
            _btnContent = new GUIContent("", TooltipDefaultText);
            _tooltipFrameCounter = 0;
        }

        public virtual void OnGUI()
        {
            _btnContent.tooltip = IsOn ? TooltipOnText : TooltipDefaultText;

            if (_mode == StateMode.Toggle)
            {
                bool newState = GUI.Toggle(_btnRect, IsOn, _btnContent, _btnStyle);
                if (IsOn != newState)
                {
                    IsOn = !IsOn;
                    OnClick();
                }

            }
            else if (GUI.Button(_btnRect, _btnContent, _btnStyle))
            {
                if (_mode == StateMode.Toggle)
                OnClick();
            }

            HandleTooltip(_btnContent.tooltip);
        }

        protected abstract void OnClick();

        protected void HandleTooltip(string tooltipTextToLookFor)
        {
            // hybrid IMGUI/ugui interface: the button and mouse hover detection uses IMGUI,
            // but then the tooltip uses ugui's gameobject-based system to make KSP spawn its own tooltip
            // to do this, we check when GUI.tooltip has the value we have assigned to the GUI.Button content.

            if (GUI.tooltip == tooltipTextToLookFor || ++_tooltipFrameCounter < 3)
            {
                SetTooltip(tooltipTextToLookFor);

                if (GUI.tooltip == tooltipTextToLookFor)
                    _tooltipFrameCounter = 0;
            }
            else if (_tooltipObject != null)
            {
                Object.Destroy(_tooltipObject);
                _tooltipObject = null;
            }
        }

        protected void SetTooltip(string tooltip)
        {
            if (_tooltipObject == null)
            {
                _tooltipObject = new GameObject(GetType().Name);
            }

            TooltipController_Text tt = _tooltipObject.AddOrGetComponent<TooltipController_Text>();
            if (tt != null)
            {
                tt.textString = tooltip;
                tt.prefab = TooltipPrefab;
                UIMasterController.Instance.SpawnTooltip(tt);
            }
        }
    }
}
