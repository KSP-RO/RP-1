using ContractConfigurator.Parameters;
using RP0;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class HorizontalLanding : VesselParameter
    {
        private struct Sample
        {
            public readonly double ut;
            public readonly double altitude;
            public readonly double groundTrack;

            public Sample(double ut, double altitude, double groundTrack)
            {
                this.ut = ut;
                this.altitude = altitude;
                this.groundTrack = groundTrack;
            }
        }

        protected double glideRatio { get; set; }
        protected double? maxSrfVel { get; set; }
        protected double timeWindow { get; set; }
        protected double maxAirborneTime { get; set; }
        protected bool wasPreviouslyMet { get; set; }
        protected float updateFrequency { get; set; }

        private float lastUpdate = 0.0f;

        // Rolling window of airborne samples used to measure the descent angle from
        // actual displacement rather than a single (noisy) velocity reading.
        private readonly List<Sample> samples = new List<Sample>();
        private double cumulativeGroundTrack;
        private double lastSampleUT;
        private bool prevLanded;
        private double lastDepartUT = -1.0;
        private bool stateInitialized;

        internal const float DEFAULT_UPDATE_FREQUENCY = 0.5f;
        internal const double DEFAULT_TIME_WINDOW = 5.0;
        internal const double DEFAULT_MAX_AIRBORNE_TIME = 5.0;

        // Fraction of timeWindow that must be covered by samples before the descent
        // angle can be judged; with less data the parameter is considered unmet.
        private const double MinWindowFraction = 0.8;

        public HorizontalLanding() : base(null)
        {
        }

        public HorizontalLanding(string title, double descentAngle, double? maxSrfVel, float updateFrequency, double timeWindow, double maxAirborneTime)
            : base(title)
        {
            this.title = title ?? $"Land horizontally with a descent angle below {descentAngle}°";
            this.glideRatio = 1 / Math.Tan(Mathf.Deg2Rad * descentAngle);
            this.maxSrfVel = maxSrfVel;
            this.updateFrequency = updateFrequency;
            this.timeWindow = timeWindow;
            this.maxAirborneTime = maxAirborneTime;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("updateFrequency", updateFrequency);
            node.AddValue("glideRatio", glideRatio);
            node.AddValue("timeWindow", timeWindow);
            node.AddValue("maxAirborneTime", maxAirborneTime);
            node.AddValue("wasPreviouslyMet", wasPreviouslyMet);

            if (maxSrfVel.HasValue)
            {
                node.AddValue("maxSrfVel", maxSrfVel);
            }
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
            glideRatio = ConfigNodeUtil.ParseValue<double>(node, "glideRatio");
            timeWindow = ConfigNodeUtil.ParseValue<double>(node, "timeWindow", DEFAULT_TIME_WINDOW);
            maxAirborneTime = ConfigNodeUtil.ParseValue<double>(node, "maxAirborneTime", DEFAULT_MAX_AIRBORNE_TIME);
            wasPreviouslyMet = ConfigNodeUtil.ParseValue<bool>(node, "wasPreviouslyMet");
            maxSrfVel = ConfigNodeUtil.ParseValue(node, "maxSrfVel", (double?)null);
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            double now = Planetarium.GetUniversalTime();
            bool landed = vessel.LandedOrSplashed;

            // A packed vessel has unreliable velocity/position data and cannot be actively
            // flown (e.g. on-rails warp), so pause sampling and hold the current state.
            if (vessel.packed)
            {
                samples.Clear();
                prevLanded = landed;
                return wasPreviouslyMet;
            }

            // On the first evaluation after creation or load we don't know the prior
            // airborne history, so just seed the transition state without judging anything.
            // This prevents a spurious "touchdown" from wiping an already-earned result.
            if (!stateInitialized)
            {
                stateInitialized = true;
                prevLanded = landed;
                lastDepartUT = landed ? -1.0 : now;
                return wasPreviouslyMet;
            }

            if (landed)
            {
                if (!prevLanded)
                {
                    // Touchdown. A short airborne stint just before this is a bounce and must
                    // not disturb the latched result; a longer one is a genuine (re-)approach
                    // that we judge from the descent recorded in the window.
                    double airborneDuration = lastDepartUT >= 0 ? now - lastDepartUT : double.MaxValue;
                    if (airborneDuration > maxAirborneTime)
                    {
                        wasPreviouslyMet = ApproachWasShallow(vessel);
                    }
                }

                // Ground roll is not part of the approach, so stop accumulating.
                samples.Clear();
            }
            else
            {
                if (prevLanded)
                {
                    lastDepartUT = now;
                    RP0Debug.Log($"HorizontalLandingVesselParam: vessel is airborne again");
                }

                RecordSample(vessel, now);

                // Airborne longer than the grace period means the vessel has flown away
                // (touch-and-go / go-around), so the previous landing no longer counts.
                if (now - lastDepartUT > maxAirborneTime)
                {
                    wasPreviouslyMet = false;
                }
            }

            prevLanded = landed;
            return wasPreviouslyMet;
        }

        private void RecordSample(Vessel vessel, double now)
        {
            if (samples.Count == 0)
            {
                cumulativeGroundTrack = 0.0;
            }
            else
            {
                double dt = now - lastSampleUT;
                if (dt > 0.0)
                {
                    cumulativeGroundTrack += vessel.horizontalSrfSpeed * dt;
                }
            }

            lastSampleUT = now;
            samples.Add(new Sample(now, vessel.altitude, cumulativeGroundTrack));

            double cutoff = now - timeWindow;
            while (samples.Count > 0 && samples[0].ut < cutoff)
            {
                samples.RemoveAt(0);
            }
        }

        private bool ApproachWasShallow(Vessel vessel)
        {
            if (samples.Count < 2) return false;

            Sample oldest = samples[0];
            Sample newest = samples[samples.Count - 1];

            // Require most of the window to be covered; with less data there is not enough
            // to judge the descent angle, so fail closed.
            if (newest.ut - oldest.ut < timeWindow * MinWindowFraction) return false;

            double ΔAltitude = newest.altitude - oldest.altitude;    // <= 0 while descending
            double ΔGroundTrack = newest.groundTrack - oldest.groundTrack;

            // Must be descending (not climbing) and shallower than the threshold angle, i.e.
            // the ground track covered exceeds the altitude lost times the glide ratio.
            bool met = ΔAltitude <= 0.0 && ΔGroundTrack > -ΔAltitude * glideRatio;
            RP0Debug.Log($"HorizontalLandingVesselParam: {met}; ΔAltitude: {ΔAltitude}, ΔGroundTrack: {ΔGroundTrack}");

            if (maxSrfVel.HasValue)
            {
                met &= vessel.srfSpeed < maxSrfVel.Value;
            }

            return met;
        }

        protected override void OnUpdate()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            base.OnUpdate();

            if (Time.fixedTime - lastUpdate > updateFrequency)
            {
                lastUpdate = Time.fixedTime;

                CheckVessel(v);
            }
        }
    }
}
