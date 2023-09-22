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

    [HarmonyPatch(typeof(Sim))]
    internal class PatchKerbalism_Sim
    {
        [HarmonyPrefix]
        [HarmonyPatch("ShadowPeriod", new Type[] { typeof(Vessel) })]
        internal static bool Prefix_ShadowPeriod(Vessel v, ref double __result)
        {
            if (Lib.Landed(v) || v.orbit is not Orbit obt || double.IsNaN(obt.inclination))
            {
                var sun = Planetarium.fetch.Sun;
                var mb = v.mainBody;
                if (mb.referenceBody == sun && mb.tidallyLocked)
                {
                    Vector3 sunV = sun.transform.position - v.transform.position;
                    Vector3 bodyV = mb.transform.position - v.transform.position;
                    __result = Vector3.Dot(sunV, bodyV) > 0f ? mb.rotationPeriod : 0d;
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

            Vector3d planeNormal = Vector3d.Cross(v.velocityD, v.orbit.referenceBody.position - v.transform.position);
            double sunDot = Math.Abs(Vector3d.Dot(Planetarium.fetch.Sun.transform.position - v.transform.position, planeNormal));
            double betaAngle = Math.PI * 0.5d - Math.Acos(sunDot);

            double a = obt.semiMajorAxis;
            double R = obt.referenceBody.Radius;

            // Now, branch depending on if we're in a low-ecc orbit
            // We check locally for betaStar because we might bail early in the Kerbalism case
            if (e < 0.1d)
                __result = FracEclipseCirc(betaAngle, a, R);
            else
                __result = FracEclipseKerbalism(v, betaAngle, a, R, e);

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

        internal static double FracEclipseKerbalism(Vessel v, double betaAngle, double a, double R, double e)
        {
            // This is the Kerbalism code, complete with comments. However
            // we check for ecc >=1 and just bail in that case, instead of the NaN check
            // and we check for beta angle vs beta*

            // Calculation for elliptical orbits

            // see https://wiki.kerbalspaceprogram.com/wiki/Orbit_darkness_time

            // Limitations:
            //
            // - This method assumes the orbit is an ellipse / circle which is not
            // changing or being altered by other bodies.
            //
            // - It also assumes the sun's rays are parallel across the orbiting
            // planet, although all bodies are small enough and far enough from the
            // sun for this to be nearly true.
            //
            // - The method does not take into account darkness caused by eclipses
            // of a different body than the orbited body, for example, orbiting
            // Laythe but Jool blocks the sun.
            //
            // - The method gives the longest amount of time spent in darkness, which for some
            // orbits (e.g.polar orbits), will only be experienced periodically.
            // NOTE: corrected via beta angle (mostly)

            // The formula:
            // Td = (2ab / h) (asin(R/b) + eR/b)
            // a is the semi-major axis
            // b the semi-minor axis
            // h the specific angular momentum
            // e the eccentricity
            // R the radius of the planet or moon
            // For reference these terms can be calculated by knowing the apoapsis(Ap), periapsis(Pe) and body to orbit:
            // h = sqrt(lu)
            // l = (2 * ra * rp) / (ra + rp)
            // u = G * M, the gravitational parameter
            // ra = Ap + R (apoapsis from center of the body)
            // rp = Pe + R (periapsis from center of the body)
            // TRANSFORMED by dividing by period sqrt(a^3/u) to report eclipse fraction rather than time in darkness.
            // ab * (asin(R / b) + e * R / b) / (pi * sqrt(a^3 * l)

            double b = v.orbit.semiMinorAxis;
            // Just bail if we were going to report NaN, or we're in a weird state
            // We've likely avoided this already due to the eccentricity check in the main call, though
            if (a < b || b < R)
                return 0d;

            double betaStar = Math.Asin(R / a);
            if (Math.Abs(betaAngle) >= betaStar)
                return 0d;

            double ra = v.orbit.ApR;
            double rp = v.orbit.PeR;
            double RdivB = R / b;
            double l = (2d * ra * rp) / (ra + rp);

            double frac = a * b * (Math.Asin(RdivB) + e * RdivB) / (Math.PI * Math.Sqrt(a * a * a * l));

            // err on the light side:
            // if more than half the orbit duration is in shadow, invert
            // the result. this will be wrong when the apoapsis is near nadir.
            // this is just... WRONG, I know, but it is wrong in a good way.
            // f.i. a very eccentric orbit with the apoapsis above one of the
            // poles won't be assumed to spend most of the time in shadow.
            // NOTE: with the new beta angle code above, this should not be
            // hit? Beta* for high-ecc (and therefore high-SMA) orbits will
            // be very low, meaning we early-out in those cases.
            if (frac > 0.5d)
                frac = 1d - frac;
            return frac;
        }
    }
}