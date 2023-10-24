﻿using System;
using UniLinq;
using UnityEngine;

namespace RP0InstallChecker
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class InstallChecker : MonoBehaviour
    {
        private bool _firstFrame = true;

        protected void Update()
        {
            if (_firstFrame)
            {
                _firstFrame = false;
                return;
            }

            Version minKSPCFVer = new Version(1, 30, 0);
            if (!AssemblyLoader.loadedAssemblies.Any(a => a.name.Equals("KSPCommunityFixes") && (new Version(a.versionMajor, a.versionMinor, a.versionRevision)) >= minKSPCFVer))
            {
                string titleText = "Incorrect RP-1 Installation";
                string contentText = "Make sure you have installed version 1.30.0 or above of KSPCommunityFixes. RP-1 will not load without this installed.";
                ShowErrorDialog(titleText, contentText);
                return;
            }

            var assembliesToCheck = new[] { "CC_RP0", "RP-0" };    // These are values of KSPAssembly attribute
            if (assembliesToCheck.Any(an => !AssemblyLoader.loadedAssemblies.Any(a => a.name.Equals(an, StringComparison.OrdinalIgnoreCase))))
            {
                string titleText = "Incorrect RP-1 Installation";
                string contentText = "This could be caused by downloading the RP-1 repo or some specific branch directly from GitHub, or by not installing or not updating dependencies. " +
                    "Make sure to follow the install guide located in the RP-1 wiki.\n\n" +
                    "If the goal was to obtain the latest developmental version of RP-1, then please use the link at the top of the RP-1 readme.";
                ShowErrorDialog(titleText, contentText);
                return;
            }

            if (AssemblyLoader.loadedAssemblies.Any(a => a.name.Equals("KerbalConstructionTime", StringComparison.OrdinalIgnoreCase)))
            {
                string titleText = "Incorrect RP-1 Installation";
                string contentText = "You still have a KerbalConstructionTime dll (RP0KCT.dll). Please uninstall RP-1 and properly reinstall it to remove this dll. " +
                    "Make sure to follow the install guide located in the RP-1 wiki.\n\n" +
                    "If the goal was to obtain the latest developmental version of RP-1, then please use the link at the top of the RP-1 readme after uninstalling this RP-1 install.";
                ShowErrorDialog(titleText, contentText);
                return;
            }

            assembliesToCheck = new[] { "ToolbarController", "ClickThroughBlocker", "RealFuels", "ContractConfigurator", "ModularFlightIntegrator" };    // These are values of KSPAssembly attribute
            var kerbAssembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name.StartsWith("Kerbalism", StringComparison.OrdinalIgnoreCase));
            if (assembliesToCheck.Any(an => !AssemblyLoader.loadedAssemblies.Any(a => a.name.Equals(an, StringComparison.OrdinalIgnoreCase)))
                || kerbAssembly == null
                || kerbAssembly.assembly.GetName().Version < new Version("3.18"))
            {
                string titleText = "Incorrect RP-1 Installation";
                string contentText = "You are missing dependencies for RP-1. This could be caused by manually installing RP-1, or by not updating dependencies. " +
                    "Make sure to follow the install guide located in the RP-1 wiki and make sure to update your mods in CKAN.\n\n" +
                    "If the goal was to obtain the latest developmental version of RP-1, then install normally through the guide and then use the link at the top of the RP-1 readme.";
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
                contentText, KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"), true, HighLogic.UISkin,
                true, string.Empty);
        }
    }
}
