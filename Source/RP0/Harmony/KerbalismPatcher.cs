using HarmonyLib;
using System;
using System.Reflection;
using KERBALISM;
using UnityEngine;

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
        internal static void Postfix_RunningUpdate(Vessel v, Situation vs, Experiment prefab, Experiment.RunningState expState, ref string mainIssue)
        {
            if (expState != Experiment.RunningState.Running && expState != Experiment.RunningState.Forced)
                return;

            if (mainIssue.Length > 0)
                return;

            if (!v.loaded || vs == null || prefab == null)
                return;

            if (!Database.StartCompletedExperiments.TryGetValue(v.mainBody.name, out var exps))
                return;

            if (!exps.TryGetValue(prefab.experiment_id, out var sits))
                return;

            if (sits.Contains(vs.ScienceSituation.ToExperimentSituations()))
                mainIssue = Local.Module_Experiment_issue1;
        }
    }

    [HarmonyPatch(typeof(Sim))]
    internal class PatchKerbalism_Sim
    {
        [HarmonyPrefix]
        [HarmonyPatch("ShadowPeriod", new Type[] { typeof(Vessel) })]
        internal static bool Prefix_ShadowPeriod(Vessel v, ref double __result)
        {
            Orbit obt = v.orbitDriver?.orbit;
            if (obt == null)
            {
                __result = 0d;
                return false;
            }

            bool incNaN = double.IsNaN(obt.inclination);
            if(Lib.Landed(v) || incNaN)
            {
                var sun = Planetarium.fetch.Sun;
                var mb = v.mainBody;
                if(sun == mb)
                {
                    __result = 0d;
                }
                else if (mb.referenceBody == sun && mb.tidallyLocked)
                {
                    Vector3d vPos = incNaN ? v.transform.position : v.orbitDriver.pos + mb.position;
                    Vector3d sunV = sun.position - vPos;
                    // We have to refind orbit pos in case inc is NaN
                    __result = Vector3d.Dot(sunV, mb.position - vPos) > 0d ? mb.rotationPeriod : 0d;
                }
                else
                {
                    __result = mb.rotationPeriod * 0.5d;
                }
                return false;
            }

            double e = obt.eccentricity;
            if (e >= 1d)
            {
                // This is wrong, of course, but given the speed of an escape trajectory
                // you'll be in shadow for a very miniscule fraction of the period.
                __result = 0d;
                return false;
            }
            Vector3d planeNormal = Vector3d.Cross(v.orbitDriver.vel, -v.orbitDriver.pos).normalized;
            Vector3d sunVec = Planetarium.fetch.Sun.position - (v.mainBody.position + v.orbitDriver.pos).normalized;
            double sunDot = Math.Abs(Vector3d.Dot(sunVec, planeNormal));
            double betaAngle = Math.PI * 0.5d - Math.Acos(sunDot);

            double a = obt.semiMajorAxis;
            double R = obt.referenceBody.Radius;

            // Now, branch depending on if we're in a low-ecc orbit
            // We check locally for betaStar because we might bail early in the Kerbalism case
            if (e < 0.1d)
                __result = FracEclipseCirc(betaAngle, a, R);
            else
                __result = FracEclipseKerbalism(v, betaAngle, a, R, e, sunVec);

            __result *= obt.period;

            return false;
        }

        internal static double FracEclipseCirc(double betaAngle, double sma, double R)
        {
            // from https://commons.erau.edu/cgi/viewcontent.cgi?article=1412&context=ijaaa
            double betaStar = Math.Asin(R / sma);
            if (Math.Abs(betaAngle) >= betaStar)
                return 0d;

            double avgHeight = sma - R;
            return (1d / Math.PI) * Math.Acos(Math.Sqrt(avgHeight * avgHeight + 2 * R * avgHeight) / (sma * Math.Cos(betaAngle)));
        }

        internal static double FracEclipseKerbalism(Vessel v, double betaAngle, double a, double R, double e, Vector3d sunVec)
        {
            var obt = v.orbit;
            double b = obt.semiMinorAxis;
            // Just bail if we were going to report NaN, or we're in a weird state
            // We've likely avoided this already due to the eccentricity check in the main call, though
            if (a < b || b < R)
                return 0d;

            // Compute where the Pe is with respect to the sun
            Vector3d PeToBody = -Planetarium.Zup.WorldToLocal(obt.semiLatusRectum / (1d + e) * obt.OrbitFrame.X).xzy;
            Vector3d orthog = Vector3d.Cross(obt.referenceBody.GetFrameVel().xzy.normalized, sunVec);
            Vector3d PeToBodyProj = (PeToBody - orthog * Vector3d.Dot(PeToBody, orthog)).normalized;
            double tA = Math.Acos(Vector3d.Dot(sunVec, PeToBodyProj));

            // Get distance to ellipse edge
            double r = a * (1d - e * e) / (1 + e * Math.Cos(tA));

            double betaStar = Math.Asin(R / r);
            double absBeta = Math.Abs(betaAngle);
            if (absBeta >= betaStar)
                return 0d;

            double halfEclipsedv = Math.Asin(R / r);
            double vAhead = tA + halfEclipsedv;
            double vBehind = tA - halfEclipsedv;
            double sqrtep = Math.Sqrt(1d + e);
            double sqrten = Math.Sqrt(1d - e);
            double Eahead = 2d * Math.Atan2(sqrten * Math.Sin(vAhead * 0.5d), sqrtep * Math.Cos(vAhead * 0.5d));
            double Mahead = Eahead - e * Math.Sin(Eahead);
            double Ebehind = 2d * Math.Atan2(sqrten * Math.Sin(vBehind * 0.5d), sqrtep * Math.Cos(vBehind * 0.5d));
            double Mbehind = Ebehind - e * Math.Sin(Ebehind);
            double eclipseFrac = (Mahead - Mbehind) / (2d * Math.PI);
            // This is not quite correct I think, but it'll be close enough.
            // We just lerp between 0 occlusion at beta = betaStar, and full occlusion
            // at beta = 0. This takes advantage of the difference 1 degree makes being larger
            // as beta approaches zero, at the same time as the proportional increase in
            // occlusion *area* tapers off as the plane approaches the body's horizon.
            return eclipseFrac * absBeta / betaStar;
        }
    }
}