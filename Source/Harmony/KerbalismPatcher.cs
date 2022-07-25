using HarmonyLib;
using KSP.UI.Screens;
using KSP.UI;
using Strategies;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using RP0.Programs;
using UniLinq;

namespace RP0.Harmony
{
    [HarmonyPatch]
    internal class PatchKerbalism_PreferencesScience
    {
        static MethodBase TargetMethod() => AccessTools.TypeByName("KERBALISM.PreferencesScience").GetMethod("SetDifficultyPreset", AccessTools.all);

        [HarmonyPostfix]
        internal static void Postfix_SetDifficultyPreset(ref bool ___sampleTransfer)
        {
            ___sampleTransfer = true;
        }
    }

    [HarmonyPatch]
    internal class PatchKerbalism_PreferencesRadiation
    {
        static MethodBase TargetMethod() => AccessTools.TypeByName("KERBALISM.PreferencesRadiation").GetMethod("SetDifficultyPreset", AccessTools.all);

        [HarmonyPostfix]
        internal static void Postfix_SetDifficultyPreset(ref float ___shieldingEfficiency, ref float ___stormFrequency, ref float ___stormRadiation)
        {
            float shieldingEffic = 0.933f;
            float stormFreq = 0.15f;
            float stormRad = 100.0f;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("Kerbalism"))
            {
                if (n.GetValue("name") == "RealismOverhaul")
                {
                    n.TryGetValue("ShieldingEfficiency", ref shieldingEffic);
                    n.TryGetValue("StormFrequency", ref stormFreq);
                    n.TryGetValue("StormRadiation", ref stormRad);
                }
            }

            ___shieldingEfficiency = shieldingEffic;
            ___stormFrequency = stormFreq;
            ___stormRadiation = stormRad;
        }
    }
}