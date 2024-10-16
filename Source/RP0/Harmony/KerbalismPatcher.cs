using HarmonyLib;
using System;
using KERBALISM;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using ROUtils;

namespace RP0.Harmony
{
    /// <summary>
    /// Force sample transfer to be allowed at all difficulty levels
    /// </summary>
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

    /// <summary>
    /// Apply the radiation settings from the profile regardless of difficulty level
    /// </summary>
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

    /// <summary>
    /// Patch CrewSpecs to ignore tourists (until PR to Kerbalism is merged)
    /// </summary>
    [HarmonyPatch(typeof(CrewSpecs))]
    internal class PatchKerbalism_CrewSpecs
    {
        static bool Prepare() => KerbalismUtils.IsValidToPatch(new Version(3, 17, int.MaxValue, int.MaxValue), true);

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

    /// <summary>
    /// Patch Experiment's PAW status to show 'invalid situation'
    /// if you're trying to use an experiment landed/splashed/low
    /// on Earth. Note the info panel will still say it's Waiting
    /// and the science is collected, this is just the status by
    /// the togglebutton in the PAW itself.
    /// </summary>
    [HarmonyPatch(typeof(Experiment))]
    internal class PatchKerbalism_Experiment
    {
        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        internal static void Postfix_Update(Experiment __instance)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            var v = __instance.vessel;
            if (v == null || !v.loaded)
                return;

            if (!__instance.Running)
                return;

            if (__instance.issue.Length > 0 || __instance.Status != Experiment.ExpStatus.Waiting)
                return;

            // we have to use the gameobject because the experiments are tied to the original
            // body name, not the nameLater name. The GO for Earth is still Kerbin so we use that.
            if (!Database.StartCompletedExperiments.TryGetValue(v.mainBody.gameObject.name, out var exps))
                return;

            if (!exps.TryGetValue(__instance.experiment_id, out var sits))
                return;

            if (sits.Contains(ScienceUtil.GetExperimentSituation(v)))
                __instance.Events["ToggleEvent"].guiName = Lib.StatusToggle(Lib.Ellipsis(__instance.ExpInfo.Title, Styles.ScaleStringLength(25)), Experiment.StatusInfo(Experiment.ExpStatus.Issue, Local.Module_Experiment_issue1));
        }
    }

    #region Solar Panel Exposure

    [HarmonyPatch(typeof(Sim))]
    internal class PatchKerbalism_Sim
    {
        static bool Prepare() => KerbalismUtils.IsValidToPatch(new Version(3, 17, int.MaxValue, int.MaxValue), true);

        [HarmonyPrefix]
        [HarmonyPatch("ShadowPeriod", new Type[] { typeof(Vessel) })]
        internal static bool Prefix_ShadowPeriod(Vessel v, ref double __result)
        {
            // We have only 1 sun so don't try to figure out what's the current sunData.
            Vector3d sunVec = (Planetarium.fetch.Sun.position - Lib.VesselPosition(v)).normalized;
            __result = EclipseFraction(v, Planetarium.fetch.Sun, sunVec) * Sim.OrbitalPeriod(v);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("SampleSunFactor")]
        internal static bool Prefix_SampleSunFactor(Vessel v, double elapsedSeconds, ref double __result)
        {
            // We have only 1 sun so don't try to figure out what's the current sunData.
            __result = SampleSunFactor(v, elapsedSeconds, Planetarium.fetch.Sun);
            return false;
        }

        public static double EclipseFraction(Vessel v, CelestialBody sun, Vector3d sunVec)
        {
            var obt = v.orbitDriver?.orbit;
            if (obt == null)
                return 0;

            bool incNaN = double.IsNaN(obt.inclination);
            if (Lib.Landed(v) || incNaN)
            {
                var mb = v.mainBody;
                if (sun == mb)
                {
                    return 0;
                }

                if (mb.referenceBody == sun && mb.tidallyLocked)
                {
                    Vector3d vPos = incNaN ? (Vector3d)v.transform.position : v.orbitDriver.pos + mb.position;
                    // We have to refind orbit pos in case inc is NaN
                    return Vector3d.Dot(sunVec, mb.position - vPos) < 0 ? 0 : 1.0;
                }

                // Just assume half the body's rotation period (note that
                // for landed vessels, the orbital period is considered the
                // body's rotation period).
                return 0.5 * mb.rotationPeriod;
            }

            double e = obt.eccentricity;
            if (e >= 1d)
            {
                // This is wrong, of course, but given the speed of an escape trajectory
                // you'll be in shadow for a very miniscule fraction of the period.
                return 0;
            }
            Vector3d planeNormal = Vector3d.Cross(v.orbitDriver.vel, -v.orbitDriver.pos).normalized;
            double sunDot = Math.Abs(Vector3d.Dot(sunVec, planeNormal));
            double betaAngle = Math.PI * 0.5d - Math.Acos(sunDot);

            double a = obt.semiMajorAxis;
            double R = obt.referenceBody.Radius;

            // Now, branch depending on if we're in a low-ecc orbit
            // We check locally for betaStar because we might bail early in the Kerbalism case
            double frac;
            if (e < 0.1d)
                frac = FracEclipseCircular(betaAngle, a, R);
            else
                frac = FracEclipseElliptical(v, betaAngle, a, R, e, sunVec);

            return frac;
        }

        /// <summary>
        /// This computes eclipse fraction for circular orbits
        /// (well, realy circular _low_ orbits, but at higher altitudes
        /// you're not spending much time in shadow anyway).
        /// </summary>
        /// <param name="betaAngle">The beta angle (angle between the solar normal and its projection on the orbital plane)</param>
        /// <param name="sma">The semi-major axis</param>
        /// <param name="R">The body's radius</param>
        /// <returns></returns>
        private static double FracEclipseCircular(double betaAngle, double sma, double R)
        {
            // from https://commons.erau.edu/cgi/viewcontent.cgi?article=1412&context=ijaaa
            // beta* is the angle above which there is no occlusion of the orbit
            double betaStar = Math.Asin(R / sma);
            if (Math.Abs(betaAngle) >= betaStar)
                return 0;

            double avgHeight = sma - R;
            return (1.0 / Math.PI) * Math.Acos(Math.Sqrt(avgHeight * avgHeight + 2.0 * R * avgHeight) / (sma * Math.Cos(betaAngle)));
        }

        /// <summary>
        /// An analytic solution to the fraction of an orbit eclipsed by its primary
        /// </summary>
        /// <param name="v">The vessel</param>
        /// <param name="betaAngle">The beta angle (angle between the solar normal and its projection on the orbital plane)</param>
        /// <param name="a">semi-major axis</param>
        /// <param name="R">body radius</param>
        /// <param name="e">eccentricity</param>
        /// <param name="sunVec">The normalized vector to the sun</param>
        /// <returns></returns>
        private static double FracEclipseElliptical(Vessel v, double betaAngle, double a, double R, double e, Vector3d sunVec)
        {
            var obt = v.orbit;
            double b = obt.semiMinorAxis;
            // Just bail if we were going to report NaN, or we're in a weird state
            // We've likely avoided this already due to the eccentricity check in the main call, though
            if (a < b || b < R)
                return 0;

            // Compute where the Pe is with respect to the sun
            Vector3d PeToBody = -Planetarium.Zup.WorldToLocal(obt.semiLatusRectum / (1d + e) * obt.OrbitFrame.X).xzy;
            Vector3d orthog = Vector3d.Cross(obt.referenceBody.GetFrameVel().xzy.normalized, sunVec);
            Vector3d PeToBodyProj = (PeToBody - orthog * Vector3d.Dot(PeToBody, orthog)).normalized;
            // Use these to calculate true anomaly for this projected orbit
            double tA = Math.Acos(Vector3d.Dot(sunVec, PeToBodyProj));

            // Get distance to ellipse edge
            double r = a * (1.0 - e * e) / (1.0 + e * Math.Cos(tA));

            double betaStar = Math.Asin(R / r);
            double absBeta = Math.Abs(betaAngle);
            if (absBeta >= betaStar)
                return 0d;

            // Get the vector to the center of the eclipse
            double vecToHalfEclipsePortion = Math.Asin(R / r);
            // Get the true anomalies at the front and rear of the eclipse portion
            double vAhead = tA + vecToHalfEclipsePortion;
            double vBehind = tA - vecToHalfEclipsePortion;
            vAhead *= 0.5;
            vBehind *= 0.5;
            double ePlusOneSqrt = Math.Sqrt(1 + e);
            double eMinusOneSqrt = Math.Sqrt(1 - e);
            // Calculate eccentric and mean anomalies
            double EAAhead = 2.0 * Math.Atan2(eMinusOneSqrt * Math.Sin(vAhead), ePlusOneSqrt * Math.Cos(vAhead));
            double MAhead = EAAhead - e * Math.Sin(EAAhead);
            double EABehind = 2.0 * Math.Atan2(eMinusOneSqrt * Math.Sin(vBehind), ePlusOneSqrt * Math.Cos(vBehind));
            double Mbehind = EABehind - e * Math.Sin(EABehind);
            // Finally, calculate the eclipse fraction from mean anomalies
            double eclipseFrac = (MAhead - Mbehind) / (2.0 * Math.PI);
            // This is not quite correct I think, but it'll be close enough.
            // We just lerp between 0 occlusion at beta = betaStar, and full occlusion
            // at beta = 0. This takes advantage of the difference 1 degree makes being larger
            // as beta approaches zero, at the same time as the proportional increase in
            // occlusion *area* tapers off as the plane approaches the body's horizon.
            return eclipseFrac * absBeta / betaStar;
        }

        /// <summary>
		/// This expects to be called repeatedly
		/// </summary>
		public static double SampleSunFactor(Vessel v, double elapsedSeconds, CelestialBody sun)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Sim.SunFactor2");

            bool isSurf = Lib.Landed(v);
            if (v.orbitDriver == null || v.orbitDriver.orbit == null || (!isSurf && double.IsNaN(v.orbit.inclination)))
            {
                UnityEngine.Profiling.Profiler.EndSample();
                return 1d; // fail safe
            }

            int sunSamples = 0;

            var now = Planetarium.GetUniversalTime();

            var vd = v.KerbalismData();
            List<CelestialBody> occluders = vd.EnvVisibleBodies;

            // set up CB position caches
            bodyCache.SetForOccluders(occluders);

            // Set max time to calc
            double maxCalculation = elapsedSeconds * 1.01d;

            // cache values for speed
            double semiLatusRectum = 0d;
            CelestialBody mb = v.mainBody;
            Vector3d surfPos;
            Vector3d polarAxis;
            if (isSurf)
            {
                surfPos = mb.GetRelSurfacePosition(v.latitude, v.longitude, v.altitude);
                // Doing this manually instead of swizzling surfPos avoids one of the two swizzles
                surfPos = (surfPos.x * mb.BodyFrame.X + surfPos.z * mb.BodyFrame.Y + surfPos.y * mb.BodyFrame.Z).xzy;

                // This will not be quite correct for Principia but at least it's
                // using the BodyFrame, which Principia clobbers, rather than the
                // transform.
                polarAxis = mb.BodyFrame.Rotation.swizzle * Vector3d.up;
            }
            else
            {
                semiLatusRectum = v.orbit.semiLatusRectum;
                maxCalculation = Math.Min(maxCalculation, v.orbit.period);
                surfPos = new Vector3d();
                polarAxis = new Vector3d();
            }

            // Set up timimg
            double stepLength = Math.Max(120d, elapsedSeconds * (1d / 40d));
            int sampleCount;
            if (stepLength > maxCalculation)
            {
                stepLength = maxCalculation;
                sampleCount = 1;
            }
            else
            {
                sampleCount = (int)Math.Ceiling(maxCalculation / stepLength);
                stepLength = maxCalculation / (double)sampleCount;
            }

            for (int i = sampleCount; i-- > 0;)
            {
                double ut = now - i * stepLength;
                bodyCache.SetForUT(ut, occluders);
                Vector3d bodyPos = bodyCache.GetBodyPosition(mb.flightGlobalsIndex);
                Vector3d pos;
                if (isSurf)
                {
                    // Rotate the surface position based on where the body would have rotated in the past
                    // Note: rotation is around *down* so we flip the sign of the rotation
                    pos = QuaternionD.AngleAxis(mb.rotPeriodRecip * i * stepLength * 360d, polarAxis) * surfPos;
                }
                else
                {
                    pos = FastGetRelativePositionAtUT(v.orbit, ut, semiLatusRectum);
                }
                // Apply the body's position
                pos += bodyPos;

                bool vis = IsSunVisibleAtTime(v, pos, sun, occluders, isSurf);
                if (vis)
                    ++sunSamples;
            }

            UnityEngine.Profiling.Profiler.EndSample();

            double sunFactor = (double)sunSamples / (double)sampleCount;
            //Lib.Log("Vessel " + v + " sun factor: " + sunFactor + " " + sunSamples + "/" + sampleCount + " #s=" + sampleCount + " e=" + elapsedSeconds + " step=" + stepLength);
            return sunFactor;
        }

        /// <summary>
        /// A version of IsBodyVisibleAt that is optimized for suns
        /// and supports using arbitrary time (assuming bodyCache is set)
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="vesselPos">Vessel position at time</param>
        /// <param name="sun"></param>
        /// <param name="sunIdx">The body index of the sun</param>
        /// <param name="occluders"></param>
        /// <param name="UT"></param>
        /// <param name="isSurf">is the vessel landed</param>
        /// <returns></returns>
        internal static bool IsSunVisibleAtTime(Vessel vessel, Vector3d vesselPos, CelestialBody sun, List<CelestialBody> occluders, bool isSurf)
        {
            // generate ray parameters
            Vector3d sunPos = bodyCache.GetBodyPosition(sun.flightGlobalsIndex) - vesselPos;
            var sunDir = sunPos;
            var sunDist = sunDir.magnitude;
            sunDir /= sunDist;
            sunDist -= sun.Radius;

            // for very small bodies the analytic method is very unreliable at high latitudes
            // So we use a modified version of the analytic method (unlike IsBodyVisible)
            bool ignoreMainbody = false;
            if (isSurf && vessel.mainBody.Radius < 100000.0)
            {
                ignoreMainbody = true;
                Vector3d mainBodyPos = bodyCache.GetBodyPosition(vessel.mainBody.flightGlobalsIndex);
                Vector3d mainBodyDir = (mainBodyPos - vesselPos).normalized;
                double dotSunBody = Vector3d.Dot(mainBodyDir, sunDir);
                Vector3d mainBodyDirProjected = mainBodyDir * dotSunBody;

                // Assume the sun is far enough away that we can treat the line from the vessel
                // to the sun as parallel to the line from the body center to the sun, which means
                // we can ignore testing further if we're very close to the plane orthogonal to the
                // sun vector but on the opposite side of the body from the sun.
                // We don't strictly test dot to give ourselves approx half a degree of slop
                if (mainBodyDirProjected.sqrMagnitude > 0.0001d && dotSunBody > 0d)
                {
                    return false;
                }
            }

            // check if the ray intersect one of the provided bodies
            for (int i = occluders.Count; i-- > 0;)
            {
                CelestialBody occludingBody = occluders[i];
                if (occludingBody == sun)
                    continue;
                if (ignoreMainbody && occludingBody == vessel.mainBody)
                    continue;

                Vector3d toBody = bodyCache.GetBodyPosition(occludingBody.flightGlobalsIndex) - vesselPos;
                // projection of origin->body center ray over the raytracing direction
                double k = Vector3d.Dot(toBody, sunDir);
                // the ray doesn't hit body if its minimal analytical distance along the ray is less than its radius
                // simplified from 'start + dir * k - body.position'
                bool hit = k > 0d && k < sunDist && (sunDir * k - toBody).magnitude < occludingBody.Radius;
                if (hit)
                    return false;
            }

            return true;
        }

        /// <summary>
		/// A cache to speed calculation of body positions at a given UT, based on
		/// as set of occluders. Used when calculating solar exposure at analytic rates.
		/// This creates storage for each body in FlightGlobals, but only caches
		/// a lookup from occluder to body index, and then for each relevant occluder
		/// and its parents a lookup to each parent (and on up the chain) and the
		/// semilatus rectum. Then when set for a UT, it calculates positions
		/// for each occluder on up the chain to the root CB.
		/// </summary>
		public class BodyCache
        {
            private Vector3d[] positions = null;
            private int[] parents;
            private double[] semiLatusRectums;

            public Vector3d GetBodyPosition(int idx) { return positions[idx]; }

            /// <summary>
            /// Check and, if uninitialized, setup the body caches
            /// </summary>
            private void CheckInitBodies()
            {
                int c = FlightGlobals.Bodies.Count;
                if (positions != null && positions.Length == c)
                    return;

                positions = new Vector3d[c];
                parents = new int[c];
                semiLatusRectums = new double[c];

                for (int i = 0; i < c; ++i)
                {
                    var cb = FlightGlobals.Bodies[i];
                    // Set parent index lookup
                    var parent = cb.orbitDriver?.orbit?.referenceBody;
                    if (parent != null && parent != cb)
                    {
                        parents[i] = parent.flightGlobalsIndex;
                    }
                    else
                    {
                        parents[i] = -1;
                    }
                }
            }

            /// <summary>
            /// Initialize the cache for a set of occluders. This
            /// will set up the lookups for the occluder bodies and
            /// cache the semi-latus recturm for each body and its
            /// parents
            /// </summary>
            /// <param name="occluders"></param>
            public void SetForOccluders(List<CelestialBody> occluders)
            {
                CheckInitBodies();

                // Now clear all SLRs and then set only the relevant ones
                // (i.e. the occluders, their parents, their grandparents, etc)
                for (int i = semiLatusRectums.Length; i-- > 0;)
                    semiLatusRectums[i] = double.MaxValue;
                for (int i = occluders.Count; i-- > 0;)
                    SetSLRs(occluders[i].flightGlobalsIndex);
            }

            private void SetSLRs(int i)
            {
                // Check if set
                if (semiLatusRectums[i] != double.MaxValue)
                    return;

                // Check if parent
                int pIdx = parents[i];
                if (pIdx == -1)
                {
                    semiLatusRectums[i] = 1d;
                    return;
                }

                semiLatusRectums[i] = FlightGlobals.Bodies[i].orbit.semiLatusRectum;
                SetSLRs(pIdx);
            }

            /// <summary>
            /// Set the occluder body positions at the given UT
            /// </summary>
            /// <param name="ut"></param>
            public void SetForUT(double ut, List<CelestialBody> occluders)
            {
                // Start from unknown positions
                for (int i = positions.Length; i-- > 0;)
                    positions[i] = new Vector3d(double.MaxValue, double.MaxValue);

                // Fill positions at UT, recursively (skipping calculated parents)
                for (int i = occluders.Count; i-- > 0;)
                    SetForUTInternal(occluders[i].flightGlobalsIndex, ut);
            }

            private void SetForUTInternal(int i, double ut)
            {
                // If we've already been here, bail
                if (positions[i].x != double.MaxValue)
                    return;

                // Check if we have a parent. If not
                // position is just the body's position
                var cb = FlightGlobals.Bodies[i];
                int pIdx = parents[i];
                if (pIdx == -1)
                {
                    positions[i] = cb.position;
                    return;
                }

                // If we do have a parent, recurse and then
                // set position based on newly-set parent's pos
                SetForUTInternal(pIdx, ut);
                positions[i] = positions[pIdx] + FastGetRelativePositionAtUT(cb.orbit, ut, semiLatusRectums[i]);
            }
        }

        /// <summary>
        /// A fast version of KSP's GetRelativePositionAtUT.
        /// It skips a bunch of steps and uses cached values
        /// </summary>
        /// <param name="orbit"></param>
        /// <param name="UT"></param>
        /// <param name="semiLatusRectum"></param>
        /// <returns></returns>
        private static Vector3d FastGetRelativePositionAtUT(Orbit orbit, double UT, double semiLatusRectum)
        {
            double T = orbit.getObtAtUT(UT);

            double M = T * orbit.meanMotion;
            double E = orbit.solveEccentricAnomaly(M, orbit.eccentricity);
            double v = orbit.GetTrueAnomaly(E);

            double cos = Math.Cos(v);
            double sin = Math.Sin(v);
            Vector3d pos = semiLatusRectum / (1.0 + orbit.eccentricity * cos) * (orbit.OrbitFrame.X * cos + orbit.OrbitFrame.Y * sin);
            return Planetarium.Zup.WorldToLocal(pos).xzy;
        }

        static readonly BodyCache bodyCache = new BodyCache();
    }
    #endregion

    #region ElectricCharge display conversion from humanreadable rate to SI Watts

    /// <summary>
    /// Postfix the UI update for the solar panel module to find the unit display
    /// and replace it and the output with the SI rate
    /// </summary>
    [HarmonyPatch(typeof(SolarPanelFixer))]
    internal class PatchKerbalism_SolarPanelFixer
    {
        static bool Prepare() => KerbalismUtils.IsValidToPatch(new Version(3, 17, int.MaxValue, int.MaxValue), true);

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        internal static void Postfix_Update(SolarPanelFixer __instance)
        {
            int idx = __instance.panelStatus.IndexOf(__instance.EcUIUnit);
            if (idx == -1)
                return;

            int offset = __instance.EcUIUnit.Length;
            if (__instance.panelStatus.Length > idx + offset)
                __instance.panelStatus = KSPUtil.PrintSI(__instance.currentOutput * 1000d, "W", 3) + __instance.panelStatus.Substring(idx + offset);
            else
                __instance.panelStatus = KSPUtil.PrintSI(__instance.currentOutput * 1000d, "W", 3);
        }
    }

    /// <summary>
    /// Blanket-patch adding to Specifics: whenever the label
    /// is one of the ones where we know the value will be EC,
    /// replace the value with the SI version
    /// </summary>
    [HarmonyPatch(typeof(Specifics))]
    internal class PatchKerbalism_Specifics
    {
        static bool Prepare() => KerbalismUtils.IsValidToPatch(new Version(3, 17, int.MaxValue, int.MaxValue), true);

        private static string _ecName;
        private static bool _needName = true;

        [HarmonyPrefix]
        [HarmonyPatch("Add")]
        internal static void Prefix_Add(ref string label, ref string value)
        {
            if (_needName)
            {
                _ecName = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").displayName;
                _needName = false;
            }

            if (string.IsNullOrEmpty(value))
                return;

            if (label == Local.Module_Experiment_Specifics_info9
                || label == Local.Harvester_info7
                || label == "EC/s"
                || label == Local.Laboratory_ECrate
                || label == Local.DataTransmitter_ECidle
                || label == Local.DataTransmitter_ECTX
                || label == _ecName)
            {
                KerbalismUtils.HumanRateToSI(ref value, "W", 1000d);
            }
        }
    }

    /// <summary>
    /// This exists to signal that we just ran the Analyze method. It's
    /// only run in one place, right before we create the EC subpanel
    /// in the Planner. So we'll use that fact
    /// </summary>
    [HarmonyPatch(typeof(KERBALISM.Planner.ResourceSimulator))]
    internal class PatchKerbalism_Planner_ResourceSimulator
    {
        static bool Prepare() => KerbalismUtils.IsValidToPatch(new Version(3, 17, int.MaxValue, int.MaxValue), true);

        public static bool JustRanAnalyze = false;
        [HarmonyPostfix]
        [HarmonyPatch("Analyze")]
        internal static void Postfix_Analyze()
        {
            JustRanAnalyze = true;
        }
    }

    /// <summary>
    /// If we know via the ResourceSimulator patch that we just entered
    /// the AddSubPanelEC method, then replace Panel's AddContent behavior
    /// until we hit the last AddContent of the method. We skip the storage
    /// one because it's not a rate, but replace the value in the other two
    /// cases.
    /// </summary>
    [HarmonyPatch(typeof(KERBALISM.Panel))]
    internal class PatchKerbalism_Panel
    {
        static bool Prepare() => KerbalismUtils.IsValidToPatch(new Version(3, 17, int.MaxValue, int.MaxValue), true);

        [HarmonyPrefix]
        [HarmonyPatch("AddContent")]
        internal static void Prefix_AddContent(string label, ref string value)
        {
            if (PatchKerbalism_Planner_ResourceSimulator.JustRanAnalyze)
            {
                if (label == Local.Planner_duration)
                {
                    PatchKerbalism_Planner_ResourceSimulator.JustRanAnalyze = false;
                    return;
                }

                if (label == Local.Planner_storage)
                    return;

                KerbalismUtils.HumanRateToSI(ref value, "W", 1000d);
            }
        }
    }

    /// <summary>
    /// Because we can't just prefix-replace Tooltip without publicizing Kerbalism
    /// (it uses private classes...) we instead signal that the method has begun
    /// and is targeting EC, and then record when it ends. We'll use that when
    /// printing rates.
    /// </summary>
    [HarmonyPatch(typeof(KERBALISM.Planner.SimulatedResource))]
    internal class PatchKerbalism_Planner_SimulatedResource
    {
        static bool Prepare() => KerbalismUtils.IsValidToPatch(new Version(3, 17, int.MaxValue, int.MaxValue), true);

        public static bool IsEC = false;

        [HarmonyPrefix]
        [HarmonyPatch("Tooltip")]
        internal static void Prefix_Tooltip(KERBALISM.Planner.SimulatedResource __instance)
        {
            IsEC = __instance.resource_name == "ElectricCharge";
        }

        [HarmonyPostfix]
        [HarmonyPatch("Tooltip")]
        internal static void Postfix_Tooltip()
        {
            IsEC = false;
        }
    }

    /// <summary>
    /// Render_supplies is complicated and uses private classes. Instead
    /// of replacing it, we just mark when it begins and ends. We'll also
    /// use this class to hold the state of whether we're dealing with EC
    /// or not.
    /// </summary>
    [HarmonyPatch(typeof(Telemetry))]
    internal class PatchKerbalism_Telemetry
    {
        static bool Prepare() => KerbalismUtils.IsValidToPatch(new Version(3, 17, int.MaxValue, int.MaxValue), true);

        public static bool IsRenderSupplies = false;
        public static bool IsEC = false;

        [HarmonyPrefix]
        [HarmonyPatch("Render_supplies")]
        internal static void Prefix_Render_supplies()
        {
            IsRenderSupplies = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch("Render_supplies")]
        internal static void Postfix_Render_supplies()
        {
            IsRenderSupplies = false;
            IsEC = false;
        }
    }

    /// <summary>
    /// This replaces HumanReadableRate itself, changing its behavior
    /// when we know (per the above patches) that we're dealing with EC.
    /// This also has the dirtiest hack: SpacesOnCaps is called in very few
    /// places, so it's cheap to prefix and use it as a marker for when
    /// to replace rate-printing. If we hit that method while we're already
    /// in Render_supplies, and the string we're operating on is EC (so we
    /// know that's the resource), we mark it.
    /// The HumanReadableRate is simple: if we know we're dealing with EC,
    /// just SI-print it. We preserve the 'precision' passed to it by
    /// using that for the significant figures passed to PrintSI.
    /// </summary>
    [HarmonyPatch(typeof(Lib))]
    internal class PatchKerbalism_Lib
    {
        static bool Prepare() => KerbalismUtils.IsValidToPatch(new Version(3, 17, int.MaxValue, int.MaxValue), true);

        private static string _ecName;
        private static bool _needName = true;

        [HarmonyPrefix]
        [HarmonyPatch("HumanReadableRate")]
        internal static bool Prefix_HumanReadableRate(double rate, string precision, ref string __result)
        {
            if (!(PatchKerbalism_Planner_SimulatedResource.IsEC || PatchKerbalism_Telemetry.IsEC))
                return true;

            // The method takes precision as a format string. We need
            // to grab the digit in it to know how many significant figures
            // to use. Note that we don't support double-digit significant
            // figures, but then again that's kinda nuts.
            int sigfigs = 3;
            char c = precision[precision.Length - 1];
            if (char.IsDigit(c))
                sigfigs = (int)char.GetNumericValue(c);

            __result = KSPUtil.PrintSI(rate * 1000d, "W", sigfigs);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("SpacesOnCaps")]
        internal static void Prefix_SpacesOnCaps(string s)
        {
            if (!PatchKerbalism_Telemetry.IsRenderSupplies)
                return;

            if (_needName)
            {
                _ecName = PartResourceLibrary.Instance.GetDefinition("ElectricCharge").displayName;
                _needName = false;
            }

            PatchKerbalism_Telemetry.IsEC = s == _ecName;
        }
    }
    #endregion

    [HarmonyPatch(typeof(ExperimentRequirements))]
    internal class PatchKerbalism_ExperimentRequirements
    {
        // Don't even bother to patch if Principia isn't installed.
        // Also, of course, don't patch if Kerbalism 3.18 is out with this fix inside it.
        static bool Prepare() => ModUtils.IsPrincipiaInstalled && KerbalismUtils.IsValidToPatch(new Version(3, 17, int.MaxValue, int.MaxValue), true);

        private static double PrincipiaCorrectInclination(Orbit o)
        {
            if (ModUtils.IsPrincipiaInstalled && o.referenceBody != (FlightGlobals.currentMainBody ?? Planetarium.fetch.Home))
            {
                Vector3d polarAxis = o.referenceBody.BodyFrame.Z;

                double hSqrMag = o.h.sqrMagnitude;
                if (hSqrMag == 0d)
                {
                    return Math.Acos(Vector3d.Dot(polarAxis, o.pos) / o.pos.magnitude) * (180.0 / Math.PI);
                }
                else
                {
                    Vector3d orbitZ = o.h / Math.Sqrt(hSqrMag);
                    return Math.Atan2((orbitZ - polarAxis).magnitude, (orbitZ + polarAxis).magnitude) * (2d * (180.0 / Math.PI));
                }
            }
            else
            {
                return o.inclination;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("TestRequirements")]
        internal static void Prefix_TestRequirements(Vessel v, out double __state)
        {
            __state = v.orbit.inclination;
            v.orbit.inclination = PrincipiaCorrectInclination(v.orbit);
        }

        [HarmonyPostfix]
        [HarmonyPatch("TestRequirements")]
        internal static void Postfix_TestRequirements(Vessel v, double __state)
        {
            v.orbit.inclination = __state;
        }
    }
}