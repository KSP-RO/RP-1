using HarmonyLib;
using System;
using System.Reflection;
using KERBALISM;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(PreferencesScience))]
    internal class PatchKerbalism_PreferencesScience
    {
        [HarmonyPostfix]
        [HarmonyPatch("SetDifficultyPreset")]
        internal static void Postfix_SetDifficultyPreset(ref bool ___sampleTransfer)
        {
            ___sampleTransfer = true;
        }
    }

    [HarmonyPatch(typeof(PreferencesRadiation))]
    internal class PatchKerbalism_PreferencesRadiation
    {
        [HarmonyPostfix]
        [HarmonyPatch("SetDifficultyPreset")]
        internal static void Postfix_SetDifficultyPreset(ref float ___shieldingEfficiency, ref float ___stormFrequency, ref float ___stormRadiation)
        {
            float shieldingEffic = 0.933f;
            float stormFreq = 0.15f;
            float stormRad = 100.0f;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("Kerbalism"))
            {
                if (n.GetValue("Profile") == "RealismOverhaul")
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

    [HarmonyPatch(typeof(CrewSpecs))]
    internal class PatchKerbalism_CrewSpecs
    {
        [HarmonyPrefix]
        [HarmonyPatch("Check", new Type[] { typeof(ProtoCrewMember) })]
        internal static bool Prefix_Check(ProtoCrewMember c, ref bool __result)
        {
            if (c.type == ProtoCrewMember.KerbalType.Tourist)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Experiment))]
    internal class PatchKerbalism_Experiment
    {
        [HarmonyPostfix]
        [HarmonyPatch("RunningUpdate")]
        internal static void Postfix_RunningUpdate(Vessel v, Situation vs, Experiment prefab, ref string mainIssue)
        {
            if (!v.loaded)
                return;

            if (!Database.StartCompletedExperiments.TryGetValue(vs.BodyName, out var exps))
                return;

            if (!exps.TryGetValue(prefab.experiment_id, out var sits))
                return;

            if (sits.Contains(vs.ScienceSituation.ToExperimentSituations()))
                mainIssue = Local.Module_Experiment_issue1;
        }
    }
}