using ContractConfigurator;
using Contracts;
using HarmonyLib;
using KSP.Localization;
using KSP.UI.Screens.DebugToolbar.Screens.Contract;
using TMPro;
using UniLinq;
using UnityEngine;
using UnityEngine.UI;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ScreenContractTools))]
    internal class PatchScreenContractTools
    {
        [HarmonyPrefix]
        [HarmonyPatch("Start")]
        internal static void Prefix_Start(ScreenContractTools __instance)
        {
            Object.Instantiate(__instance.clearFinished.transform.parent.parent.gameObject.GetChild("Spacer"), __instance.transform);
            GameObject containerObject = Object.Instantiate(__instance.clearFinished.transform.parent.gameObject, __instance.transform);
            Button button = containerObject.GetComponentInChildren<Button>();
            button.onClick.AddListener(CleanContracts);
            TextMeshProUGUI text = containerObject.GetComponentInChildren<TextMeshProUGUI>();
            text.text = "Clean Contracts";

            // Clobber the text of other buttons
            var btn2 = __instance.clearCurrent.GetComponentInChildren<TextMeshProUGUI>();
            btn2.text = Localizer.GetStringByTag(btn2.text) + " (Do not use)";
            btn2.fontSize = 11;
            var btn3 = __instance.clearFinished.GetComponentInChildren<TextMeshProUGUI>();
            btn3.text = Localizer.GetStringByTag(btn3.text) + " (Do not use)";
            btn3.fontSize = 11;
        }

        private static void CleanContracts()
        {
            if (ContractSystem.Instance == null)
            {
                Debug.LogError("Cannot clean contracts since ContractSystem.Instance doesn't exist");
                return;
            }

            var ccContracts = ContractSystem.Instance.ContractsFinished.OfType<ConfiguredContract>().ToArray();
            foreach (ConfiguredContract c in ccContracts)
            {
                c.parameters.Clear();

                if (c.Agent?.Name == "skopos_telecom_agent")
                {
                    ContractSystem.Instance.ContractsFinished.Remove(c);
                }
            }
        }
    }
}
