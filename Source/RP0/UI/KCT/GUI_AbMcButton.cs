using KSP.UI.Screens;
using System.Collections;
using UnityEngine;

namespace RP0
{
    internal class GUI_AbMcButton : GUI_TopRightButton
    {
        public bool IsMcUp => MissionControl.Instance != null;
        public bool IsAbUp => Administration.Instance != null;

        public GUI_AbMcButton(int offset) : base(offset, StateMode.Toggle)
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
                GameEvents.onGUIAdministrationFacilityDespawn.Remove(OnAnyOverlayClose);
                GameEvents.onGUIMissionControlDespawn.Add(OnAnyOverlayClose);
                GameEvents.onGUIAdministrationFacilityDespawn.Fire();
                GameEvents.onGUIMissionControlSpawn.Fire();
            }
            else if (IsMcUp)
            {
                GameEvents.onGUIMissionControlDespawn.Remove(OnAnyOverlayClose);
                GameEvents.onGUIAdministrationFacilityDespawn.Add(OnAnyOverlayClose);
                GameEvents.onGUIMissionControlDespawn.Fire();
                GameEvents.onGUIAdministrationFacilitySpawn.Fire();
            }
        }

        /// <summary>
        /// KSP assumes that it's only possible to switch between KSC scene and a single overlay.
        /// This button however makes it tri-state. Because of this, after using the button, KSP music logic
        /// will switch to the other overlay instead of KSC when closing the everlay entirely.
        /// So need to restore KSC state manually.
        /// </summary>
        private void OnAnyOverlayClose()
        {
            GameEvents.onGUIAdministrationFacilityDespawn.Remove(OnAnyOverlayClose);
            GameEvents.onGUIMissionControlDespawn.Remove(OnAnyOverlayClose);
            SpaceCenterManagement.Instance.StartCoroutine(RestoreKSCAudioRoutine());
        }

        private IEnumerator RestoreKSCAudioRoutine()
        {
            // Run after all the despawn event handlers have been fired
            yield return null;

            var ml = MusicLogic.fetch;

            // Clobber both the fade routines off. Otherwise they'll keep running for 0.5s.
            ml.fadeHandler1.PauseWithFade(0f, null, GameSettings.MUSIC_VOLUME, 0f, fadeClipLoop: true);
            ml.fadeHandler2.PauseWithFade(0f, null, GameSettings.MUSIC_VOLUME, 0f, fadeClipLoop: true);

            ml.GetSettings();
            ml.ResetConstValues();
            ml.ResetSpaceCenterValues();
            ml.SpaceCenterMusic();
        }
    }
}
