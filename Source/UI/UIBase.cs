using System;
using UnityEngine;
using KSP.UI.Screens;

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

        public enum tabs { Maintenance, Facilities, Integration, Astronauts, Tooling, ToolingType, Training, Courses, NewCourse, Naut, Avionics };

        protected bool showTab(tabs tab)
        {
            switch (tab) {
                case tabs.Maintenance:
                case tabs.Facilities:
                case tabs.Integration:
                    return HighLogic.LoadedScene == GameScenes.SPACECENTER && HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
                case tabs.Tooling:
                case tabs.ToolingType:
                    return HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
                case tabs.Avionics:
                    return HighLogic.LoadedSceneIsEditor;
                case tabs.Astronauts:
                    return HighLogic.LoadedScene == GameScenes.SPACECENTER;
                case tabs.Training:
                case tabs.Courses:
                case tabs.NewCourse:
                case tabs.Naut:
                default:
                    return true;
            }
        }

        public bool toggleButton(string text, bool selected, params GUILayoutOption[] options)
        {
            return GUILayout.Button(text, selected ? pressedButton : HighLogic.Skin.button, options);
        }
    }
}

