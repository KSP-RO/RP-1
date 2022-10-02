using KSP.UI.Screens;
using UnityEngine;

namespace KerbalConstructionTime
{
    internal class GUI_AbMcButton : GUI_TopRightButton
    {
        public bool IsMcUp => MissionControl.Instance != null;
        public bool IsAbUp => Administration.Instance != null;

        public GUI_AbMcButton(int offset) : base(offset, GUI_TopRightButton.StateMode.Toggle)
        {
        }

        public override void OnGUI()
        {
            if (!IsMcUp && !IsAbUp) return;

            if (Event.current.type == EventType.Repaint)
                IsOn = IsAbUp;

            base.OnGUI();
        }

        protected override void OnClick()
        {
            if (IsAbUp)
            {
                GameEvents.onGUIAdministrationFacilityDespawn.Fire();
                GameEvents.onGUIMissionControlSpawn.Fire();
            }
            else if (IsMcUp)
            {
                GameEvents.onGUIMissionControlDespawn.Fire();
                GameEvents.onGUIAdministrationFacilitySpawn.Fire();
            }
        }
    }
}
