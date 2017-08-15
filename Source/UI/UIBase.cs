using System;
using UnityEngine;

namespace RP0
{
    public class UIBase
    {
        protected GUIStyle rightLabel, boldLabel, boldRightLabel, pressedButton;
        public UIBase()
        {
            rightLabel = new GUIStyle(HighLogic.Skin.label);
            rightLabel.alignment = TextAnchor.MiddleRight;
            boldLabel = new GUIStyle(HighLogic.Skin.label);
            boldLabel.fontStyle = FontStyle.Bold;
            boldRightLabel = new GUIStyle(rightLabel);
            boldRightLabel.fontStyle = FontStyle.Bold;
            pressedButton = new GUIStyle(HighLogic.Skin.button);
            pressedButton.normal = pressedButton.active;
        }

        public enum tabs { SUMMARY, Facilities, Integration, Astronauts, Tooling, ToolingType };

        public bool toggleButton(string text, bool selected)
        {
            return GUILayout.Button(text, selected ? pressedButton : HighLogic.Skin.button);
        }
    }
}

