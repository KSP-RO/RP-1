/**
 * Based on the InstallChecker from the Kethane mod for Kerbal Space Program.
 * https://github.com/Majiir/Kethane/blob/b93b1171ec42b4be6c44b257ad31c7efd7ea1702/Plugin/InstallChecker.cs
 * 
 * Original is (C) Copyright Majiir.
 * CC0 Public Domain (http://creativecommons.org/publicdomain/zero/1.0/)
 * http://forum.kerbalspaceprogram.com/threads/65395-CompatibilityChecker-Discussion-Thread?p=899895&viewfull=1#post899895
 * 
 * This file has been modified extensively and is released under the same license.
 */
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace KerbalConstructionTime
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class InstallChecker : MonoBehaviour
    {
        private const string _modName = "Kerbal Construction Time";
        private const string _folderName = "RP-0";
        private const string _expectedPath = _folderName + "/Plugins";

        protected void Start()
        {
            // Search for this mod's DLL existing in the wrong location. This will also detect duplicate copies because only one can be in the right place.
            var assemblies = AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name ||
                                                                        a.assembly.GetName().Name == "KerbalConstructionTime")
                                                            .Where(a => a.url != _expectedPath);
            if (assemblies.Any())
            {
                var badPaths = assemblies.Select(a => a.path).Select(p => Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString().Replace('/', Path.DirectorySeparatorChar)));
                PopupDialog.SpawnPopupDialog
                (
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    "test",
                    "Incorrect " + _modName + " Installation",
                    _modName + " has been installed incorrectly and will not function properly. All files should be located in KSP/GameData/" + _folderName + ". Do not move any files from inside that folder.\n\nIncorrect path(s):\n" + string.Join("\n", badPaths.ToArray()),
                    "OK",
                    false,
                    HighLogic.UISkin
                );
                Debug.Log("Incorrect " + _modName + " Installation: " + _modName + " has been installed incorrectly and will not function properly. All files should be located in KSP/GameData/" + _expectedPath +
                    ". Do not move any files from inside that folder.\n\nIncorrect path(s):\n" + string.Join("\n", badPaths.ToArray()));

            }

            if (!AssemblyLoader.loadedAssemblies.Any(a => string.Equals(a.assembly.GetName().Name, "magicore", StringComparison.OrdinalIgnoreCase)))
            {
                PopupDialog.SpawnPopupDialog
                               (
                                   new Vector2(0.5f, 0.5f),
                                   new Vector2(0.5f, 0.5f),
                                   "test2",
                                   "Missing MagiCore Installation",
                                   "Magicore is required by Kerbal Construction Time.\nKerbal Construction Time will not function until it is installed",
                                   "OK",
                                   false,
                                   HighLogic.UISkin
                               );
                Debug.Log("Missing MagiCore Installation" );
            }
        }
    }
}

