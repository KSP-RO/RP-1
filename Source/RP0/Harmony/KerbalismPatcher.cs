using HarmonyLib;
using System;
using KERBALISM;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

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
            if (Lib.Landed(v) || incNaN)
            {
                var sun = Planetarium.fetch.Sun;
                var mb = v.mainBody;
                if (sun == mb)
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

        [HarmonyPrefix]
        [HarmonyPatch("SampleSunFactor")]
        internal static bool Prefix_SampleSunFactor(Vessel v, double elapsedSeconds, ref double __result)
        {
            __result = SampleSunFactor(v, elapsedSeconds);
            return false;
        }

        private static readonly List<Vector3d> _cbPositions = new List<Vector3d>();
        private static readonly List<int> _cbParents = new List<int>();
        private static readonly List<int> _occluderToPos = new List<int>();
        private static readonly List<double> _cbSLRs = new List<double>();

        private static void InitBodies()
        {
            int c = FlightGlobals.Bodies.Count;
            if (_cbPositions.Count == c)
                return;

            _cbPositions.Clear();
            _cbParents.Clear();
            _cbSLRs.Clear();
            if (_cbPositions.Capacity < c)
                _cbPositions.Capacity = c;
            if (_cbParents.Capacity < c)
                _cbParents.Capacity = c;
            if (_cbSLRs.Capacity < c)
                _cbSLRs.Capacity = c;
            for (int i = 0; i < c; ++i)
            {
                var cb = FlightGlobals.Bodies[i];
                var parent = cb.orbitDriver?.orbit?.referenceBody;
                if (parent != null && parent != cb)
                {
                    _cbParents.Add(FlightGlobals.GetBodyIndex(parent));
                    _cbSLRs.Add(cb.orbit.semiLatusRectum);
                }
                else
                {
                    _cbParents.Add(-1);
                    _cbSLRs.Add(1d);
                }
                _cbPositions.Add(new Vector3d());
            }
        }

        internal static void FillCBPositionsAtUT(double ut, List<CelestialBody> occluders)
        {
            // Start from unknown positions
            for (int i = _cbPositions.Count; i-- > 0;)
                _cbPositions[i] = new Vector3d(double.MaxValue, double.MaxValue);

            // Fill positions at UT, recursively (skipping calculated parents)
            for (int i = occluders.Count; i-- > 0;)
                _FillCBPositionAtUT(_occluderToPos[i], ut);


        }
        internal static void _FillCBPositionAtUT(int i, double ut)
        {
            if (_cbPositions[i].x != double.MaxValue)
                return;

            var cb = FlightGlobals.Bodies[i];
            int pIdx = _cbParents[i];
            if (pIdx == -1)
            {
                _cbPositions[i] = cb.position;
                return;
            }
            _FillCBPositionAtUT(pIdx, ut);
            _cbPositions[i] = _cbPositions[pIdx] + fastGetRelativePositionAtUT(cb.orbit, ut, _cbSLRs[i]);
        }

        internal static double SampleSunFactor(Vessel v, double elapsedSeconds)
        {
            bool isSurf = Lib.Landed(v);
            if (v.orbitDriver == null || (!isSurf && (v.orbit == null || double.IsNaN(v.orbit.inclination))))
                return 1d; // fail safe

            UnityEngine.Profiling.Profiler.BeginSample("Kerbalism.Sim.SunFactor2");

            int sunSamples = 0;

            var now = Planetarium.GetUniversalTime();

            var vd = v.KerbalismData();
            CelestialBody sun = vd.EnvMainSun.SunData.body;
            int sunIdx = FlightGlobals.GetBodyIndex(sun);
            List<CelestialBody> occluders = vd.EnvVisibleBodies;

            // set up CB position caches
            InitBodies();
            foreach (var cb in occluders)
            {
                _occluderToPos.Add(FlightGlobals.GetBodyIndex(cb));
            }

            // Set max time to calc
            double maxCalculation = elapsedSeconds * 1.01d;

            // cache values for speed
            double semiLatusRectum = 0d;
            int bodyIdx = FlightGlobals.GetBodyIndex(v.orbit.referenceBody);
            CelestialBody mb;
            Vector3d surfPos = new Vector3d();
            if (isSurf)
            {
                mb = v.mainBody;
                surfPos = v.mainBody.GetRelSurfacePosition(v.latitude, v.longitude, v.altitude);
            }
            else
            {
                mb = null;
                semiLatusRectum = v.orbit.semiLatusRectum;
                maxCalculation = Math.Min(maxCalculation, v.orbit.period);
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
                FillCBPositionsAtUT(ut, occluders);
                Vector3d pos;
                if (!isSurf)
                {
                    pos = _cbPositions[bodyIdx] + fastGetRelativePositionAtUT(v.orbit, ut, semiLatusRectum);
                }
                else
                {
                    // Doing this manually instead of calling LocalToWorld avoids a double swizzle (was LocalToWorld(surfPos.xzy).xzy )
                    pos = surfPos.x * mb.BodyFrame.X + surfPos.y * mb.BodyFrame.Z + surfPos.z * mb.BodyFrame.Y;
                    // Now rotate the pos based on where the body would have rotated in the past
                    pos = QuaternionD.AngleAxis(mb.rotPeriodRecip * -i * stepLength * 360d, mb.transform.up) * pos;
                    pos += _cbPositions[bodyIdx]; // and apply the position
                }
                bool vis = IsSunVisibleAtTime(v, pos, sun, sunIdx, occluders, ut);
                if (vis)
                    ++sunSamples;
            }
            _occluderToPos.Clear();

            UnityEngine.Profiling.Profiler.EndSample();

            double sunFactor = (double)sunSamples / (double)sampleCount;
            return sunFactor;
        }

        // We have to reimplement this code because we need to check at a specific time
        internal static bool IsSunVisibleAtTime(Vessel vessel, Vector3d vesselPos, CelestialBody sun, int sunIdx, List<CelestialBody> occluders, double UT)
        {
            // generate ray parameters
            Vector3d sunPos = _cbPositions[sunIdx] - vesselPos;
            var sunDir = sunPos;
            var sunDist = sunDir.magnitude;
            sunDir /= sunDist;
            sunDist -= sun.Radius;

            // for very small bodies the analytic method is very unreliable at high latitudes
            // So we use a modified version of the analytic method
            bool ignoreMainbody = false;
            if (Lib.Landed(vessel) && vessel.mainBody.Radius < 100000.0)
            {
                ignoreMainbody = true;
                Vector3d mainBodyPos = _cbPositions[FlightGlobals.GetBodyIndex(vessel.mainBody)];
                Vector3d mainBodyDir = (mainBodyPos - vesselPos).normalized;
                double dotSunBody = Vector3d.Dot(mainBodyDir, sunDir);
                Vector3d mainBodyDirProjected = mainBodyDir * dotSunBody;

                // Assume the sun is far enough away that we can treat the line from the vessel
                // to the sine as parallel to the line from the body center to the sun, which means
                // we can ignore testing further if we're very close to the plane orthogonal to the
                // sun vector, and we only care if the dot is positive
                if (mainBodyDirProjected.sqrMagnitude > 0.0001d && dotSunBody > 0d) // approx half a degree from the pole
                {
                    return false;
                }
            }

            // check if the ray intersect one of the provided bodies
            for (int i = 0; i < occluders.Count; ++i)
            {
                CelestialBody occludingBody = occluders[i];
                if (occludingBody == sun)
                    continue;
                if (ignoreMainbody && occludingBody == vessel.mainBody)
                    continue;

                Vector3d toBody = _cbPositions[_occluderToPos[i]] - vesselPos;
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

        internal static Vector3d fastGetRelativePositionAtUT(Orbit orbit, double UT, double semiLatusRectum)
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

    // The following patches don't work, and are done instead
    // by the hacky workarounds below them
#if disabled
    // This throws:
    // FormatException: Method static System.Void KERBALISM.Planner.Planner::AddSubPanelEC(KERBALISM.Panel p) cannot be patched. Reason: The type initializer for 'KERBALISM.Planner.Planner' threw an exception.
    //    HarmonyLib.PatchFunctions.UpdateWrapper
    [HarmonyPatch(typeof(KERBALISM.Planner.Planner))]
    internal class PatchKerbalism_Planner
    {
        [HarmonyPrefix]
        [HarmonyPatch("AddSubPanelEC")]
        internal static bool Prefix_AddSubPanelEC(Panel p, KERBALISM.Planner.ResourceSimulator ___resource_sim)
        {
            KERBALISM.Planner.SimulatedResource simulatedResource = ___resource_sim.Resource("ElectricCharge");
            string tooltip = simulatedResource.Tooltip();
            p.AddSection(Local.Planner_ELECTRICCHARGE);
            p.AddContent(Local.Planner_storage, Lib.HumanReadableAmount(simulatedResource.storage), tooltip);
            p.AddContent(Local.Planner_consumed, KSPUtil.PrintSI(simulatedResource.consumed * 1000d, "W", 3), tooltip);
            p.AddContent(Local.Planner_produced, KSPUtil.PrintSI(simulatedResource.produced * 1000d, "W", 3), tooltip);
            p.AddContent(Local.Planner_duration, Lib.HumanReadableDuration(simulatedResource.Lifetime()));

            return false;
        }
    }

    // This fails the same way.

    [HarmonyPatch(typeof(KERBALISM.Planner.Planner))]
    internal class PatchKerbalism_Planner
    {
        private static readonly FieldInfo _resSimField = typeof(KERBALISM.Planner.Planner).GetField("resource_sim", AccessTools.all);
        private static bool _needField = true;
        private static KERBALISM.Planner.ResourceSimulator _resource_sim;

        private static readonly MethodInfo _addSubECMethod = typeof(KERBALISM.Planner.Planner).GetMethod("AddSubPanelEC", AccessTools.all);
        private static readonly MethodInfo _replaceMethod = typeof(PatchKerbalism_Planner).GetMethod("ReplacementAddSubPanelEC", AccessTools.all);

        [HarmonyTranspiler]
        [HarmonyPatch("Update")]
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            for (int i = 0; i < code.Count; ++i)
            {
                if (code[i].Calls(_addSubECMethod))
                {
                    code[i] = new CodeInstruction(System.Reflection.Emit.OpCodes.Call, _replaceMethod);
                    break;
                }
            }
            return code;
        }

        internal static void ReplacementAddSubPanelEC(Panel p)
        {
            if (_needField)
            {
                _resource_sim = (KERBALISM.Planner.ResourceSimulator)_resSimField.GetValue(null);
                _needField = false;
            }

            KERBALISM.Planner.SimulatedResource simulatedResource = _resource_sim.Resource("ElectricCharge");
            string tooltip = simulatedResource.Tooltip();
            string[] splitTip = tooltip.Split('\n');
            for (int i = 0; i < splitTip.Length; ++i)
            {
                if (splitTip[i].Length > 0)
                    KerbalismUtils.HumanRateToSI(ref splitTip[i], "W", 1000d);
            }
            tooltip = string.Join("\n", splitTip);

            p.AddSection(Local.Planner_ELECTRICCHARGE);
            p.AddContent(Local.Planner_storage, Lib.HumanReadableAmount(simulatedResource.storage), tooltip);
            p.AddContent(Local.Planner_consumed, KSPUtil.PrintSI(simulatedResource.consumed * 1000d, "W", 4), tooltip);
            p.AddContent(Local.Planner_produced, KSPUtil.PrintSI(simulatedResource.produced * 1000d, "W", 4), tooltip);
            p.AddContent(Local.Planner_duration, Lib.HumanReadableDuration(simulatedResource.Lifetime()));
        }
    }
#endif

    /// <summary>
    /// This exists to signal that we just ran the Analyze method. It's
    /// only run in one place, right before we create the EC subpanel
    /// in the Planner. So we'll use that fact
    /// </summary>
    [HarmonyPatch(typeof(KERBALISM.Planner.ResourceSimulator))]
    internal class PatchKerbalism_Planner_ResourceSimulator
    {
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
}