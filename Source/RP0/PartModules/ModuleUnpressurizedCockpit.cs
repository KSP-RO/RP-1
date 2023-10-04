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
        public double crewDeathAltitude = 16000d;

        [KSPField(isPersistant = true)]
        public double timeSinceHypoxiaStarted = 0d;

        private double _lastCheck = -1d;
        private const double _checkInterval = 1d;
        private double _referenceDensity = -1d;
        private double _referenceDensityMin;

        private double _gDamageAdder = 0d;

        private System.Random _rnd;

        private static bool? _origDoStockGCalcs;

        private bool _anyCrewAboveWarnThreshold = false;

        public override string GetInfo()
        {
            if (crewDeathAltitude < 0d)
                return "Cockpit is now fully pressurized.";

            return $"Cockpit is unpressurized and will lead to crew death above {crewDeathAltitude / 1000:0.#}km";
        }

        public override void OnAwake()
        {
            base.OnAwake();
            _gDamageAdder = PhysicsGlobals.KerbalGThresholdLOC * 0.04;
            _rnd = new System.Random();
        }

        protected void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            if (part.CrewCapacity == 0)
                return;

            int pC = part.protoModuleCrew.Count;
            if (pC == 0)
                return;

            double UT = Planetarium.GetUniversalTime();
            if (_lastCheck < 0d)
                _lastCheck = UT;

            double deltaTime = UT - _lastCheck;
            if (deltaTime < _checkInterval)
                return;

            if (!_origDoStockGCalcs.HasValue)
            {
                _origDoStockGCalcs = ProtoCrewMember.doStockGCalcs;
            }

            _lastCheck = UT;
            double curAltitute = part.vessel.altitude;
            if (curAltitute > crewDeathAltitude)
            {
                // Assume the standard atmosphere
                if (_referenceDensity < 0d)
                {
                    if (crewDeathAltitude >= Planetarium.fetch.Home.atmosphereDepth)
                    {
                        _referenceDensity = 0d;
                        _referenceDensityMin = 0d;
                    }
                    else
                    {
                        _referenceDensity = Planetarium.fetch.Home.GetDensity(Planetarium.fetch.Home.GetPressure(crewDeathAltitude), Planetarium.fetch.Home.GetTemperature(crewDeathAltitude));
                        _referenceDensityMin = _referenceDensity * 0.02d;
                    }
                }

                timeSinceHypoxiaStarted += deltaTime;

                ScreenMessages.PostScreenMessage($"Cockpit is above the safe altitude which will lead to crew incapacitation and eventually to death", 1f, ScreenMessageStyle.UPPER_CENTER, XKCDColors.Red);

                if (!_origDoStockGCalcs.HasValue)
                {
                    _origDoStockGCalcs = ProtoCrewMember.doStockGCalcs;
                }
                ProtoCrewMember.doStockGCalcs = false;

                double highGPenalty = vessel.geeForce > 3d ? System.Math.Pow(vessel.geeForce - 2d, 2d) : 1;

                double curDensity = part.atmDensity;
                if (curDensity < _referenceDensityMin)
                    curDensity = _referenceDensityMin;

                double altitudeMult = (curAltitute - crewDeathAltitude) / crewDeathAltitude * 10d;
                if (curDensity > 0d)
                    altitudeMult += _referenceDensity / curDensity - 1d;

                double timeMult = System.Math.Pow(timeSinceHypoxiaStarted, 1.5d) * 0.01d;

                bool killed = false;
                for (int i = pC; i-- > 0;)
                {
                    ProtoCrewMember pcm = part.protoModuleCrew[i];

                    pcm.gExperienced += (0.5d + _rnd.NextDouble()) * _gDamageAdder * highGPenalty * altitudeMult * timeMult;

                    double gMult = ProtoCrewMember.GToleranceMult(pcm) * HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().KerbalGToleranceMult;
                    _anyCrewAboveWarnThreshold = pcm.gExperienced > PhysicsGlobals.KerbalGThresholdWarn * gMult;

                    double locThreshold = PhysicsGlobals.KerbalGThresholdLOC * gMult;
                    if (!pcm.outDueToG && pcm.gExperienced > locThreshold)
                    {
                        // Just passed out
                        ScreenMessages.PostScreenMessage($"<color=red>{pcm.name} has lost consciousness due to hypoxia!</color>", 5.5f, ScreenMessageStyle.UPPER_CENTER);
                    }

                    // There's at least one cycle of delay after passing out before the death chance rolls start
                    if (pcm.outDueToG && _rnd.NextDouble() < crewDeathChance * altitudeMult * timeMult)
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
                timeSinceHypoxiaStarted = 0d;

                if (_origDoStockGCalcs.HasValue)
                {
                    ProtoCrewMember.doStockGCalcs = _origDoStockGCalcs.Value;
                    _origDoStockGCalcs = null;
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
