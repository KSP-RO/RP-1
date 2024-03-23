using HarmonyLib;
using KSP.Localization;
using RP0.ConfigurableStart;
using UnityEngine;
using UnityEngine.UI;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(MainMenu))]
    internal class PatchMainMenu
    {
        [HarmonyPrefix]
        [HarmonyPatch("ConfirmNewGame")]
        internal static bool Prefix_ConfirmNewGame(MainMenu __instance)
        {
            if (MainMenu.newGameMode != Game.Modes.SCIENCE_SANDBOX)
                return true;

            PopupDialog dlg = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), 
                new Vector2(0.5f, 0.5f), 
                new MultiOptionDialog("NoScienceSandbox", 
                    Localizer.Format("#rp0_MainMenu_NoSciMode_Text"), 
                    Localizer.Format("#rp0_MainMenu_NoSciMode_Title"), 
                    null, 
                    new DialogGUIButton(Localizer.Format("#autoLOC_190905"), 
                        __instance.CancelOverwriteNewGame, dismissOnSelect: true)), persistAcrossScenes: false, null);
            dlg.OnDismiss = __instance.CancelOverwriteNewGame;
            MenuNavigation.SpawnMenuNavigation(dlg.gameObject, Navigation.Mode.Vertical, limitCheck: true);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("CreateNewGameDialog")]
        internal static void Postfix_CreateNewGameDialog(ref PopupDialog __result)
        {
            ScenarioHandler.Instance.ClobberNewGameUI(__result);
        }
    }
}
