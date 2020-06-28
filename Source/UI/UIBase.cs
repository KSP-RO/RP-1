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

        public enum Tabs
        { 
            Maintenance, Facilities, Integration, Astronauts, Tooling, ToolingType, 
            Training, Courses, NewCourse, Naut, Avionics, CareerLog
        };

        protected bool showTab(Tabs tab)
        {
            switch (tab) {
                case Tabs.Maintenance:
                case Tabs.Facilities:
                case Tabs.Integration:
                    return HighLogic.LoadedScene == GameScenes.SPACECENTER && HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
                case Tabs.Tooling:
                case Tabs.ToolingType:
                    return HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
                case Tabs.Avionics:
                    return HighLogic.LoadedSceneIsEditor;
                case Tabs.Astronauts:
                    return HighLogic.LoadedScene == GameScenes.SPACECENTER;
                case Tabs.Training:
                case Tabs.Courses:
                case Tabs.NewCourse:
                case Tabs.Naut:
                case Tabs.CareerLog:
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

