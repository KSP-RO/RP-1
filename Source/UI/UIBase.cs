using UnityEngine;

namespace RP0
{
    public abstract class UIBase
    {
        public enum UITab
        {
            Budget, Facilities, Integration, Construction, AstronautCosts, Tooling, ToolingType,
            Astronauts, Training, NewCourse, Naut, Avionics, Contracts, CareerLog, Programs
        };

        protected GUIStyle RightLabel, BoldLabel, BoldRightLabel, PressedButton, InfoButton;

        public UIBase()
        {
            RightLabel = new GUIStyle(UIHolder.RescaledKSPSkin.label)
            {
                alignment = TextAnchor.MiddleRight
            };
            BoldLabel = new GUIStyle(UIHolder.RescaledKSPSkin.label)
            {
                fontStyle = FontStyle.Bold
            };
            BoldRightLabel = new GUIStyle(RightLabel)
            {
                fontStyle = FontStyle.Bold
            };

            PressedButton = new GUIStyle(UIHolder.RescaledKSPSkin.button);
            PressedButton.normal = PressedButton.active;
            
            InfoButton = new GUIStyle(UIHolder.RescaledKSPSkin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 17f * UIHolder.UIScale,
                fixedWidth = 19f * UIHolder.UIScale,
                contentOffset = new Vector2(1 * UIHolder.UIScale, -1 * UIHolder.UIScale),
                margin = new RectOffset((int)(4 * UIHolder.UIScale),
                    (int)(4 * UIHolder.UIScale),
                    (int)(6 * UIHolder.UIScale),
                    (int)(4 * UIHolder.UIScale))
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
                case UITab.Budget:
                case UITab.Facilities:
                case UITab.Integration:
                case UITab.Construction:
                case UITab.Programs:
                case UITab.Tooling:
                case UITab.ToolingType:
                case UITab.AstronautCosts:
                case UITab.Contracts:
                case UITab.CareerLog:
                    return HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
                case UITab.Avionics:
                    return HighLogic.LoadedSceneIsEditor;
                case UITab.Astronauts:
                case UITab.Training:
                case UITab.NewCourse:
                case UITab.Naut:
                default:
                    return true;
            }
        }

        public bool RenderToggleButton(string text, bool selected, params GUILayoutOption[] options)
        {
            return GUILayout.Button(text, selected ? PressedButton : UIHolder.RescaledKSPSkin.button, options);
        }

        public bool RenderToggleButton(GUIContent c, bool selected, params GUILayoutOption[] options)
        {
            return GUILayout.Button(c, selected ? PressedButton : UIHolder.RescaledKSPSkin.button, options);
        }
    }
}
