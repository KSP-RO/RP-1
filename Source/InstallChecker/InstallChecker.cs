using System;
using System.Linq;
using UnityEngine;

namespace RP0InstallChecker
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class InstallChecker : MonoBehaviour
    {
        protected void Start()
        {
            var assembliesToCheck = new[] { "CC_RP0", "RP-0", "KerbalConstructionTime" };    // These are values of KSPAssembly attribute
            if (assembliesToCheck.Any(an => !AssemblyLoader.loadedAssemblies.Any(a => a.name.Equals(an, StringComparison.OrdinalIgnoreCase))))
            {
                string titleText = "Incorrect RP-1 Installation";
                string contentText = "This could be caused by downloading the RP-1 repo or some specific branch directly from GitHub. " +
                    "Make sure to follow the install guide located in the RP-1 wiki.\n\n" +
                    "If the goal was to obtain the latest developmental version of RP-1, then please use the link at the top of the RP-1 readme.";
                ShowErrorDialog(titleText, contentText);
                return;
            }

            var commonBadPathSymbols = new[] { "'", "+", "&"};
            if (commonBadPathSymbols.Any(s => KSPUtil.ApplicationRootPath.Contains(s)))
            {
                string titleText = "Bad symbols in installation path";
                string contentText = $"Make sure that folder names do not contain special characters like <b>{string.Join(" ", commonBadPathSymbols)}</b>";
                ShowErrorDialog(titleText, contentText);
                return;
            }
        }

        private static void ShowErrorDialog(string titleText, string contentText)
        {
            string titleColor = "red";

            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "RP0InstallCheckerErr",
                $"<color={titleColor}>{titleText}</color>",
                contentText, "OK", true, HighLogic.UISkin,
                true, string.Empty);
        }
    }
}
