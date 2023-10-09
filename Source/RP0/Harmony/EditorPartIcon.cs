using HarmonyLib;
using KSP.UI.Screens;
using UniLinq;
using UnityEngine.UI;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(EditorPartIcon))]
    internal class PatchEditorPartIcon
    {
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        internal static void PatchUpdate(EditorPartIcon __instance)
        {
            // stock's experimental display is broken and we want to replace it anyways, so use its state variables
            if (__instance.checkedExperimental || SpaceCenterManagement.Instance == null)
                return;

            AvailablePart part = __instance.AvailPart;
            Image btnImage = __instance.gameObject.GetChild("ButtonImage")?.GetComponent<Image>();

            if (btnImage == null || part == null)
                return;

            if (SpaceCenterManagement.Instance.TechList.Any(tech => tech.techID == part.TechRequired))
                btnImage.color = __instance.experimentalPartColor;

            __instance.checkedExperimental = true;
        }
    }
}
