using UnityEngine;

namespace RP0
{
    public abstract class UIBase
    {
        public enum UITab
        {
            Maintenance, Facilities, Integration, Construction, Astronauts, Tooling, ToolingType,
            Training, Courses, NewCourse, Naut, Avionics, Contracts, CareerLog
        };

        protected GUIStyle RightLabel, BoldLabel, BoldRightLabel, PressedButton, InfoButton;

        public UIBase()
        {
            RightLabel = new GUIStyle(HighLogic.Skin.label)
            {
                alignment = TextAnchor.MiddleRight
            };
            BoldLabel = new GUIStyle(HighLogic.Skin.label)
            {
                fontStyle = FontStyle.Bold
            };
            BoldRightLabel = new GUIStyle(RightLabel)
            {
                fontStyle = FontStyle.Bold
            };

            PressedButton = new GUIStyle(HighLogic.Skin.button);
            PressedButton.normal = PressedButton.active;

            InfoButton = new GUIStyle(HighLogic.Skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 17f,
                fixedWidth = 19f,
                contentOffset = new Vector2(1, -1),
                margin = new RectOffset(4, 4, 6, 4)
            };
        }

        internal void Start()
        {
            OnStart();
        }

        protected virtual void OnStart() { }

        protected bool ShouldShowTab(UITab tab)
        {
            switch (tab)
            {
                case UITab.Maintenance:
                case UITab.Facilities:
                case UITab.Integration:
                    return HighLogic.LoadedScene == GameScenes.SPACECENTER && HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
                case UITab.Tooling:
                case UITab.ToolingType:
                case UITab.Contracts:
                    return HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
                case UITab.Avionics:
                    return HighLogic.LoadedSceneIsEditor;
                case UITab.Astronauts:
                    return HighLogic.LoadedScene == GameScenes.SPACECENTER;
                case UITab.Training:
                case UITab.Courses:
                case UITab.NewCourse:
                case UITab.Naut:
                case UITab.CareerLog:
                default:
                    return true;
            }
        }

        public bool RenderToggleButton(string text, bool selected, params GUILayoutOption[] options)
        {
            return GUILayout.Button(text, selected ? PressedButton : HighLogic.Skin.button, options);
        }

        public bool RenderToggleButton(GUIContent c, bool selected, params GUILayoutOption[] options)
        {
            return GUILayout.Button(c, selected ? PressedButton : HighLogic.Skin.button, options);
        }
    }
}
