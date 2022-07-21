// Ported from Strategia by nightingale/jrossignol.

using KSP.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace RP0.Programs
{
    public class ProgramSpeedListItem : MonoBehaviour
    {
        private UIRadioButton toggleButton;
        public UIRadioButton ToggleButton => toggleButton;
        private UIRadioButtonStateChanger toggleStateChanger;
        private KSP.UI.TooltipTypes.TooltipController_Text tooltipController;
        private TMPro.TextMeshProUGUI text;

        private string _title;
        private const string validColor = "#caff00";
        private const string invalidColor = "#bdbdbd";
        private bool _setup = false;
        private bool _allowable;

        private Program.Speed speed;
        public Program.Speed Speed => speed;

        private void Awake()
        {
            var buttonTransform = transform.GetChild(0);
            text = buttonTransform.Find("Text").GetComponent<TMPro.TextMeshProUGUI>();
            text.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            toggleButton = buttonTransform.GetComponent<UIRadioButton>();
            toggleButton.SetGroup("RP0ProgramSpeed".GetHashCode());
            toggleStateChanger = buttonTransform.GetComponent<UIRadioButtonStateChanger>();
            tooltipController = GetComponent<KSP.UI.TooltipTypes.TooltipController_Text>();

            var rawImage = buttonTransform.Find("RawImage");
            if (rawImage != null)
                GameObject.Destroy(rawImage.gameObject);
        }

        public void Initialize(string title, Program.Speed speed)
        {
            this._title = title;
            this.speed = speed;
        }

        private void SetButtonTitle(string title, string colorHex)
        {
            text.text = $"<color={colorHex}>{title}</color>";
        }

        public void ResetInteractivity(bool isSelected)
        {
            if (isSelected)
                toggleButton.Interactable = false;
            else
                toggleButton.Interactable = true; // was _allowable -- but we want it always clickable, just colored.
        }

        public void SetState(UIRadioButton.State state)
        {
            toggleButton.SetState(state, UIRadioButton.CallType.APPLICATION, null, true);
        }

        public UIRadioButton SetupButton(bool allowed, string tooltip, Program program, UnityAction<PointerEventData, UIRadioButton.CallType> onTrue, UnityAction<PointerEventData, UIRadioButton.CallType> onFalse)
        {
            _setup = true;
            _allowable = allowed;

            toggleButton.Data = program;
            toggleButton.onTrue.AddListener(onTrue);
            toggleButton.onFalse.AddListener(onFalse);
            toggleButton.Interactable = true; // was allowed -- but we want it always clickable, just colored.

            tooltipController.SetText(tooltip);

            if (allowed)
            {
                SetButtonTitle(_title, validColor);
                toggleStateChanger.SetState("ok");
                return toggleButton;
            }
            else
            {
                SetButtonTitle(_title, invalidColor);
                toggleStateChanger.SetState("na");
                return toggleButton;
            }
        }

        public void UpdateButton(bool allowed)
        {
            if (!_setup)
                Debug.LogError("[RP-0] Programs: Tried to update Program Speed button but Setup was not called!");
            if (allowed)
            {
                SetButtonTitle(_title, validColor);
                toggleStateChanger.SetState("ok");
            }
            else
            {
                SetButtonTitle(_title, invalidColor);
                toggleStateChanger.SetState("na");
            }
        }
    }
}