using RP0.ProceduralAvionics;
using UnityEngine;

namespace RP0.Avionics
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class EditorBinder : MonoBehaviour
    {
        public void Start()
        {
            GameEvents.onPartActionUIShown.Add(OnPartActionUIShown);
            GameEvents.onPartActionUIDismiss.Add(OnPartActionUIDismiss);
        }

        public void OnDestroy()
        {
            GameEvents.onPartActionUIShown.Remove(OnPartActionUIShown);
            GameEvents.onPartActionUIDismiss.Remove(OnPartActionUIDismiss);
        }

        private void OnPartActionUIShown(UIPartActionWindow paw, Part part)
        {
            if (!part.Modules.Contains(nameof(ModuleProceduralAvionics))) return;

            if (HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsProcAvionicsAutoShown)
                SetAvionicsShowGUI(part, true);
        }

        private void OnPartActionUIDismiss(Part part)
        {
            if (!part.Modules.Contains(nameof(ModuleProceduralAvionics))) return;
            SetAvionicsShowGUI(part, false);
        }

        private static void SetAvionicsShowGUI(Part part, bool isShown)
        {
            var pm = (ModuleProceduralAvionics)part.Modules[nameof(ModuleProceduralAvionics)];
            if (pm != null)
            {
                pm.showGUI = isShown;
                pm.ShowGUIChanged(null, null);
            }
        }
    }
}
