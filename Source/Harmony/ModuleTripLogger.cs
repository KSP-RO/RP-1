using HarmonyLib;
using KSP.UI.Screens;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ModuleTripLogger))]
    internal class PatchModuleTripLogger
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnStart")]
        internal static void Postfix_OnStart(ModuleTripLogger __instance)
        {
            if (__instance.part != null && __instance.part.partInfo != null && __instance.part.partInfo.partPrefab != __instance.part)
                TripLoggerExtender.Instance.AddLogger(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDestroy")]
        internal static void Postfix_OnDestroy(ModuleTripLogger __instance)
        {
            TripLoggerExtender.Instance.RemoveLogger(__instance);
        }
    }

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class TripLoggerExtender : MonoBehaviour
    {
        private enum VesselSituationExpanded
        {
            Flying,
            FlyingHigh,
            Suborbital
        }

        private class SituationExpanded
        {
            public VesselSituationExpanded sit;
            public bool skipPass = true;
            public SituationExpanded(VesselSituationExpanded s, bool b) { sit = s; skipPass = b; }
        }

        public static TripLoggerExtender Instance { get; private set; }

        private static readonly Dictionary<ModuleTripLogger, SituationExpanded> _loggers = new Dictionary<ModuleTripLogger, SituationExpanded>();

        private int _flightLogUpdateCounter = 0;
        private const int FlightLogUpdateInterval = 50;
        private const double KarmanAltitude = 100000d;

        private VesselSituationExpanded GetVesselSituation(Vessel v)
        {
            double alt = v.altitude;
            if (alt < FlightGlobals.GetHomeBody().scienceValues.flyingAltitudeThreshold)
                return VesselSituationExpanded.Flying;
            if (alt >= KarmanAltitude)
                return VesselSituationExpanded.Suborbital;

            return VesselSituationExpanded.FlyingHigh;
        }

        public void AddLogger(ModuleTripLogger m)
        {
            if (m.vessel != null)
                _loggers[m] = new SituationExpanded(GetVesselSituation(m.vessel), true);
        }

        public void RemoveLogger(ModuleTripLogger m)
        {
            if (_loggers.ContainsKey(m))
                _loggers.Remove(m);
        }

        public void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.currentMainBody.isHomeWorld && _flightLogUpdateCounter++ >= FlightLogUpdateInterval)
            {
                _flightLogUpdateCounter = 0;

                foreach (var kvp in _loggers)
                {
                    if (kvp.Key.vessel == null)
                        continue;

                    var currentSit = GetVesselSituation(kvp.Key.vessel);
                    var oldSit = kvp.Value.sit;
                    if (currentSit != oldSit)
                    {
                        if (kvp.Value.skipPass)
                        {
                            kvp.Value.skipPass = false;

                            // special handling if we're going super fast
                            if (Math.Abs((int)currentSit - (int)oldSit) > 1)
                                LogSituation(kvp.Key, Crew.CrewHandler.Situation_FlightHigh);

                            switch (currentSit)
                            {
                                case VesselSituationExpanded.FlyingHigh:
                                    LogSituation(kvp.Key, Crew.CrewHandler.Situation_FlightHigh);
                                    break;
                                case VesselSituationExpanded.Suborbital:
                                    LogSituation(kvp.Key, FlightLog.EntryType.Suborbit.ToString());
                                    break;
                            }
                        }
                        else
                        {
                            kvp.Value.sit = currentSit;
                            kvp.Value.skipPass = true;
                        }

                    }
                }
            }
        }

        private void LogSituation(ModuleTripLogger m, string type)
        {
            var lastEntry = m.Log.Last();
            m.Log.AddEntryUnique(new FlightLog.Entry(m.Log.Flight, type, FlightGlobals.currentMainBody.name));
            int i = 0;
            for (int count = m.part.protoModuleCrew.Count; i < count; i++)
            {
                var pcm = m.part.protoModuleCrew[i];
                pcm.flightLog.AddEntryUnique(new FlightLog.Entry(pcm.flightLog.Flight, type, FlightGlobals.currentMainBody.name));
                pcm.UpdateExperience();
            }

            if (lastEntry != m.Log.Last())
                GameEvents.OnFlightLogRecorded.Fire(m.vessel);
        }
    }
}
