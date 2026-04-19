using UnityEngine;

namespace RP0
{
    public abstract class UIBase
    {
        public enum UITab
        {
            Budget, Facilities, Integration, Construction, AstronautCosts, Tooling, ToolingType,
            Astronauts, Training, NewCourse, Naut, Avionics, Contracts, CareerLog, Programs,
            CostBreakdown
        };

        protected GUIStyle RightLabel, BoldLabel, BoldRightLabel, PressedButton, InfoButton, HorizontalLineStyle;

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
            HorizontalLineStyle = new GUIStyle()
            {
                normal = new GUIStyleState() { background = Texture2D.whiteTexture },
                margin = new RectOffset(0, 0, 4, 4),
                fixedHeight = 1
            };
        }

        internal void Start()
        {
            OnStart();
        }

        protected virtual void OnStart() { }

        internal void Destroy()
        {
            OnDestroy();
        }

        protected virtual void OnDestroy() { }

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
                case UITab.CostBreakdown:
                    return HighLogic.CurrentGame.Mode == Game.Modes.CAREER && HighLogic.LoadedSceneIsEditor;
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
            return GUILayout.Button(text, selected ? PressedButton : HighLogic.Skin.button, options);
        }

        public bool RenderToggleButton(GUIContent c, bool selected, params GUILayoutOption[] options)
        {
            return GUILayout.Button(c, selected ? PressedButton : HighLogic.Skin.button, options);
        }

        protected void DrawHorizontalSeparator()
        {
            var c = GUI.color;
            GUI.color = Color.white;
            GUILayout.Box(GUIContent.none, HorizontalLineStyle);
            GUI.color = c;
        }
    }
}
