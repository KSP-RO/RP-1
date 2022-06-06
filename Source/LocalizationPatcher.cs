using HarmonyLib;
using KerbalConstructionTime;
using KSP.UI.Screens;
using KSP.UI.Screens.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class LocalizationPatcher : MonoBehaviour
    {
        private void Start()
        {
            foreach (ConfigNode patchNode in GameDatabase.Instance.GetConfigNodes("LocalizationPatch"))
            {
                foreach (ConfigNode locNode in GameDatabase.Instance.GetConfigNodes("Localization"))
                {
                    foreach (ConfigNode langNode in locNode.nodes)
                    {
                        foreach (ConfigNode.Value kvp in patchNode.values)
                        {
                            if (langNode.HasValue(kvp.name))
                                langNode.SetValue(kvp.name, kvp.value);
                        }
                    }
                }
            }
            KSP.Localization.Localizer.SwitchToLanguage(KSP.Localization.Localizer.GetLanguageIdFromFile());
        }
    }
}
