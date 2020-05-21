using UnityEngine;

namespace RP0
{
    public class ModuleUnpressurizedCockpit : PartModule
    {
        /// <summary>
        /// Chance to die (%) per 1s interval.
        /// </summary>
        [KSPField]
        public double crewDeathChance = 0.02;

        /// <summary>
        /// Altitude in meters above which the crew can be killed.
        /// </summary>
        [KSPField]
        public double crewDeathAltitude = 16000;

        public double nextCheck = -1d;
        public double checkInterval = 1d;

        public double gDamageAdder = 0d;

        protected System.Random rnd;
        protected double pressureAtKillAltitude;

        private static bool? _origDoStockGCalcs;

        private bool _anyCrewAboveWarnThreshold = false;

        public override string GetInfo()
        {
            return $"Cockpit is unpressurized and will lead to crew death above {crewDeathAltitude / 1000:0.#}km";
        }

        public override void OnAwake()
        {
            base.OnAwake();
            gDamageAdder = PhysicsGlobals.KerbalGThresholdLOC * 0.04;
            rnd = new System.Random();
        }

        protected void FixedUpdate()
        {
            int pC;
            if (HighLogic.LoadedSceneIsFlight && part.CrewCapacity > 0 && (pC = part.protoModuleCrew.Count) > 0)
            {
                double UT = Planetarium.GetUniversalTime();
                if (nextCheck < 0d)
                    nextCheck = UT + checkInterval;
                else if (UT > nextCheck)
                {
                    if (pressureAtKillAltitude == default)
                    {
                        pressureAtKillAltitude = FlightGlobals.GetHomeBody().GetPressureAtm(crewDeathAltitude);
                        _origDoStockGCalcs = ProtoCrewMember.doStockGCalcs;
                    }

                    nextCheck = UT + checkInterval;
                    if (part.staticPressureAtm < pressureAtKillAltitude)
                    {
                        ScreenMessages.PostScreenMessage($"Cockpit is above the safe altitude which will lead to crew incapacitation and eventually to death", 1f, ScreenMessageStyle.UPPER_CENTER, XKCDColors.Red);

                        if (!_origDoStockGCalcs.HasValue)
                        {
                            _origDoStockGCalcs = ProtoCrewMember.doStockGCalcs;
                        }
                        ProtoCrewMember.doStockGCalcs = false;

                        bool killed = false;
                        for (int i = pC; i-- > 0;)
                        {
                            ProtoCrewMember pcm = part.protoModuleCrew[i];

                            double highGPenalty = vessel.geeForce > 3 ? vessel.geeForce : 1;
                            pcm.gExperienced += (0.5d + rnd.NextDouble()) * gDamageAdder * highGPenalty;

                            double gMult = ProtoCrewMember.GToleranceMult(pcm) * HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().KerbalGToleranceMult;
                            _anyCrewAboveWarnThreshold = pcm.gExperienced > PhysicsGlobals.KerbalGThresholdWarn * gMult;

                            double locThreshold = PhysicsGlobals.KerbalGThresholdLOC * gMult;
                            if (!pcm.outDueToG && pcm.gExperienced > locThreshold)
                            {
                                // Just passed out
                                ScreenMessages.PostScreenMessage($"<color=red>{pcm.name} has lost consciousness due to hypoxia!</color>", 5.5f, ScreenMessageStyle.UPPER_CENTER);
                            }

                            // There's at least one cycle of delay after passing out before the death chance rolls start
                            if (pcm.outDueToG && rnd.NextDouble() < crewDeathChance)
                            {
                                killed = true;
                                ScreenMessages.PostScreenMessage($"{vessel.vesselName}: Crewmember {pcm.name} has died from exposure to near-vacuum.", 30.0f, ScreenMessageStyle.UPPER_CENTER, XKCDColors.Red);
                                FlightLogger.fetch.LogEvent($"[{KSPUtil.PrintTime(vessel.missionTime, 3, false)}] {pcm.name} died from exposure to near-vacuum.");
                                part.RemoveCrewmember(pcm);
                                pcm.Die();
                            }
                        }

                        if (killed && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
                        {
                            CameraManager.Instance.SetCameraFlight();
                        }
                    }
                    else
                    {
                        if (_origDoStockGCalcs.HasValue)
                        {
                            ProtoCrewMember.doStockGCalcs = _origDoStockGCalcs.Value;
                            _origDoStockGCalcs = null;
                        }
                    }
                }
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            // Stock code adds it's G-limit messages after the FixedUpdate() of this partmodule is run.
            // Thus OnUpdate() is used for removing those standard messages.
            if (_anyCrewAboveWarnThreshold)
            {
                _anyCrewAboveWarnThreshold = false;
                for (int i = ScreenMessages.Instance.ActiveMessages.Count - 1; i >= 0; i--)
                {
                    // Note: Should probably find the "X: lost consciousness!" and "X: reaching G limit!" messages by text but that's a bit more complicated due to localization.
                    ScreenMessage m = ScreenMessages.Instance.ActiveMessages[i];
                    if (m.style == ScreenMessageStyle.UPPER_CENTER && (m.duration == 5f || m.duration == 3f))
                    {
                        ScreenMessages.RemoveMessage(m);
                    }
                }
            }
        }

        protected void OnDestroy()
        {
            if (_origDoStockGCalcs.HasValue)
            {
                ProtoCrewMember.doStockGCalcs = _origDoStockGCalcs.Value;
            }
        }
    }
}
