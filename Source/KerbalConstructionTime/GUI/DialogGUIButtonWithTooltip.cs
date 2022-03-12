using KSP.UI.TooltipTypes;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class DialogGUIButtonWithTooltip : DialogGUIButton
    {
        public DialogGUIButtonWithTooltip(string optionText, Callback onSelected) : base(optionText, onSelected)
        {
        }

        public DialogGUIButtonWithTooltip(string optionText, Callback onSelected, bool dismissOnSelect) : base(optionText, onSelected, dismissOnSelect)
        {
        }

        public DialogGUIButtonWithTooltip(string optionText, Callback onSelected, Func<bool> EnabledCondition, bool dismissOnSelect) : base(optionText, onSelected, EnabledCondition, dismissOnSelect)
        {
        }

        public DialogGUIButtonWithTooltip(Sprite image, Callback onSelected, float w, float h, bool dismissOnSelect = false) : base(image, onSelected, w, h, dismissOnSelect)
        {
        }

        public DialogGUIButtonWithTooltip(string optionText, Callback onSelected, float w, float h, bool dismissOnSelect, UIStyle style) : base(optionText, onSelected, w, h, dismissOnSelect, style)
        {
        }

        public DialogGUIButtonWithTooltip(string optionText, Callback onSelected, float w, float h, bool dismissOnSelect, params DialogGUIBase[] options) : base(optionText, onSelected, w, h, dismissOnSelect, options)
        {
        }

        public DialogGUIButtonWithTooltip(Func<string> getString, Callback onSelected, float w, float h, bool dismissOnSelect, params DialogGUIBase[] options) : base(getString, onSelected, w, h, dismissOnSelect, options)
        {
        }

        public DialogGUIButtonWithTooltip(Sprite image, string text, Callback onSelected, float w, float h, bool dismissOnSelect = false) : base(image, text, onSelected, w, h, dismissOnSelect)
        {
        }

        public DialogGUIButtonWithTooltip(Func<string> getString, Callback onSelected, Func<bool> EnabledCondition, float w, float h, bool dismissOnSelect, params DialogGUIBase[] options) : base(getString, onSelected, EnabledCondition, w, h, dismissOnSelect, options)
        {
        }

        public DialogGUIButtonWithTooltip(string optionText, Callback onSelected, Func<bool> EnabledCondition, float w, float h, bool dismissOnSelect, UIStyle style = null) : base(optionText, onSelected, EnabledCondition, w, h, dismissOnSelect, style)
        {
        }

        public DialogGUIButtonWithTooltip(Func<string> getString, Callback onSelected, Func<bool> EnabledCondition, float w, float h, bool dismissOnSelect, UIStyle style) : base(getString, onSelected, EnabledCondition, w, h, dismissOnSelect, style)
        {
        }

        public override GameObject Create(ref Stack<Transform> layouts, UISkinDef skin)
        {
            GameObject obj = base.Create(ref layouts, skin);

            var tooltip = uiItem.AddComponent<TooltipController_Text>();
            var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
            tooltip.prefab = prefab;
            tooltip.RequireInteractable = false;

            var fi = typeof(DialogGUIBase).GetField("toolTip", BindingFlags.Instance | BindingFlags.NonPublic);
            fi.SetValue(this, tooltip);

            return obj;
        }
    }
}
