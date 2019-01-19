using System.Collections.Generic;
using System.Collections;
using UniLinq;
using UnityEngine;

namespace RP0
{
    public class ModuleRangeSafety : PartModule
    {
        [KSPEvent(guiActive = true, guiName = "Range Safety")]
        public void BoomEvent()
        {
            SpawnConfirmationDialog();
        }

        private void SpawnConfirmationDialog()
        {
            var options = new DialogGUIBase[] {
                new DialogGUIButton("Yes", Boom),
                new DialogGUIButton("No", () => {})
            };
            var dialog = new MultiOptionDialog("ConfirmRangeSafety", "Destroy Vessel?", "Range Safety", HighLogic.UISkin, 300, options);
            PopupDialog.SpawnPopupDialog(dialog, true, HighLogic.UISkin);
        }

        [KSPAction("Range Safety")]
        public void BoomAction(KSPActionParam param)
        {
            Boom();
        }

        public void Boom()
        {
            FlightLogger.fetch.LogEvent("Range Safety: Destruction initiated");
            ExecuteBoom();
        }

        public void ExecuteBoom()
        {
            if (part != vessel.rootPart)
            {
                ModuleRangeSafety mrs = vessel.rootPart.Modules.GetModule<ModuleRangeSafety>();
                if (mrs == null)
                {
                    mrs = (vessel.rootPart.AddModule("ModuleRangeSafety") as ModuleRangeSafety);
                }
                mrs.ExecuteBoom();
            }
            else
            {
                StartCoroutine(BoomRoutine());
            }
        }

        // Destruction algorithm borrowed from TAC Self Destruct
        private void ExplodeLeafParts(Part p)
        {
            int c = p.children.Count;
            if (c == 0)
                p.explode();
            else
            {
                Part cP;
                bool passAgain = true;
                for (int i = c - 1; i >= 0; --i)
                {
                    cP = p.children[i];
                    if (cP.Modules.Contains("ProceduralFairingSide")) // this is because fairing sides hold up interstages
                        // so if you blow the sides, then everything below the interstage becomes a new vessel, and will not
                        // go boom properly.
                        continue;

                    ExplodeLeafParts(cP);
                    passAgain = false;
                }
                if (passAgain)
                {
                    for (int i = c - 1; i >= 0; --i)
                        ExplodeLeafParts(p.children[i]);
                }
            }
        }
        private IEnumerator BoomRoutine()
        {
            yield return new WaitForFixedUpdate();
            while (vessel.Parts.Count > 1)
            {
                ExplodeLeafParts(vessel.rootPart);
                yield return new WaitForFixedUpdate();
            }
            vessel.rootPart.explode();
        }
    }
}
