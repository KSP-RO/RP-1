using System.Collections.Generic;
using ContractConfigurator.Parameters;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// Instantaneous cruise-endurance test: met while the active vessel simultaneously satisfies ALL of:
    ///   * surface speed within [minSpeed, maxSpeed]
    ///   * vertical speed within [minVerticalSpeed, maxVerticalSpeed]
    ///   * projected range >= requiredRange
    ///
    /// Endurance is measured as RANGE rather than time, so flying faster and/or more efficiently (e.g.
    /// higher, where jets burn less for the same speed) is rewarded. Range is computed for EVERY mass-bearing
    /// propellant the vessel is actively burning (jets run on all sorts of resources here -- kerosene,
    /// hydrogen, methane, nitrous, water injection, enriched uranium), and the reported range is the MINIMUM
    /// across them, i.e. whichever fuel runs out first. Each fuel's burn rate is averaged over a rolling
    /// rateWindowSeconds, and samples are only taken while at cruise speed so the rate is representative.
    /// Massless resources (ElectricCharge, IntakeAir) are ignored.
    ///
    /// This parameter is a plain instantaneous check. To require the condition be held for a time -- and to
    /// get a countdown display -- pair it with a stock Duration parameter placed as a LATER sibling, and set
    /// disableOnStateChange = false here so its state toggles as the vessel enters/leaves the band (Duration
    /// times only while its preceding siblings are Complete, and resets when one stops).
    ///
    /// Config: requiredRange m (req), minSpeed/maxSpeed m/s, minVerticalSpeed/maxVerticalSpeed m/s,
    /// rateWindowSeconds (def 30), updateFrequency (def 0.5). Enable CC verbose logging to see the per-tick
    /// speed/VS/range/met values.
    /// </summary>
    public class SustainedCruise : VesselParameter
    {
        protected double requiredRange { get; set; }
        protected double minSpeed { get; set; }
        protected double maxSpeed { get; set; }
        protected double minVerticalSpeed { get; set; }
        protected double maxVerticalSpeed { get; set; }
        protected double rateWindowSeconds { get; set; }
        protected float updateFrequency { get; set; }

        internal const double DEFAULT_RATE_WINDOW = 30.0;
        internal const float DEFAULT_UPDATE_FREQUENCY = 0.5f;
        private const double MIN_RATE = 1e-4;   // below this the craft isn't really burning this resource

        private float lastUpdate = 0f;
        private double lastCheckTime = double.NegativeInfinity;
        private int lastPartCount = -1;
        private uint lastVesselPersistentId;
        private bool met = false;   // whether all conditions were satisfied as of the last sample
        private double curRange = 0.0;   // most recent projected range (m), shown live in the contract title
        // Rolling per-tick samples for the burn-rate average: sampleTimes[k] holds the time of tick k, and
        // sampleAmounts[k] maps resource id -> total amount on the vessel at that tick.
        private readonly List<double> sampleTimes = new List<double>();
        private readonly List<Dictionary<int, double>> sampleAmounts = new List<Dictionary<int, double>>();
        private readonly HashSet<int> massBearingIds = new HashSet<int>();

        public SustainedCruise() : base(null) { }

        public SustainedCruise(string title, double requiredRange,
                               double minSpeed, double maxSpeed, double minVerticalSpeed, double maxVerticalSpeed,
                               double rateWindowSeconds, float updateFrequency)
            : base(title)
        {
            this.requiredRange = requiredRange;
            this.minSpeed = minSpeed;
            this.maxSpeed = maxSpeed;
            this.minVerticalSpeed = minVerticalSpeed;
            this.maxVerticalSpeed = maxVerticalSpeed;
            this.rateWindowSeconds = rateWindowSeconds;
            this.updateFrequency = updateFrequency;
            // Leave title unset so GetTitle() falls back to GetParameterTitle() each redraw (live range).
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("requiredRange", requiredRange);
            node.AddValue("minSpeed", minSpeed);
            node.AddValue("maxSpeed", maxSpeed);
            node.AddValue("minVerticalSpeed", minVerticalSpeed);
            node.AddValue("maxVerticalSpeed", maxVerticalSpeed);
            node.AddValue("rateWindowSeconds", rateWindowSeconds);
            node.AddValue("updateFrequency", updateFrequency);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            requiredRange = ConfigNodeUtil.ParseValue<double>(node, "requiredRange");
            minSpeed = ConfigNodeUtil.ParseValue<double>(node, "minSpeed", 0.0);
            maxSpeed = ConfigNodeUtil.ParseValue<double>(node, "maxSpeed", double.MaxValue);
            minVerticalSpeed = ConfigNodeUtil.ParseValue<double>(node, "minVerticalSpeed", double.MinValue);
            maxVerticalSpeed = ConfigNodeUtil.ParseValue<double>(node, "maxVerticalSpeed", double.MaxValue);
            rateWindowSeconds = ConfigNodeUtil.ParseValue<double>(node, "rateWindowSeconds", DEFAULT_RATE_WINDOW);
            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
        }

        protected override string GetParameterTitle()
        {
            double reqKm = requiredRange / 1000.0;
            string band = maxSpeed >= double.MaxValue * 0.5 ? $">= {minSpeed:0} m/s" : $"{minSpeed:0}-{maxSpeed:0} m/s";
            if (FlightGlobals.ActiveVessel == null)
                return $"Cruise: range >= {reqKm:0} km, speed {band}";
            return $"Cruise: range {curRange / 1000.0:0} / {reqKm:0} km, speed {band}";
        }

        private void ResetAll()
        {
            sampleTimes.Clear();
            sampleAmounts.Clear();
        }

        // Smallest projected range (m) across every mass-bearing resource the craft is actively burning,
        // i.e. whichever fuel is the endurance limiter. False until the window is filled with a contiguous
        // run and at least one resource is actually being consumed. limitingResource names the limiter.
        private bool TryMinRange(Vessel v, double speed, out double minRange, out PartResourceDefinition limitingResource)
        {
            minRange = 0.0;
            limitingResource = null;
            int n = sampleTimes.Count;
            if (n < 2) return false;
            double span = sampleTimes[n - 1] - sampleTimes[0];
            if (span < rateWindowSeconds * 0.9) return false;

            Dictionary<int, double> oldest = sampleAmounts[0];
            Dictionary<int, double> newest = sampleAmounts[n - 1];
            bool any = false;
            double best = double.MaxValue;
            foreach (KeyValuePair<int, double> kv in newest)
            {
                // Only rank resources present for the whole window, else a tank coming online mid-window
                // reads as an impossibly high burn rate.
                if (!oldest.TryGetValue(kv.Key, out double old)) continue;
                double rate = (old - kv.Value) / span;
                if (rate < MIN_RATE) continue;   // not really burning this one
                PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(kv.Key);
                double range = ProjectedRange(v, speed, kv.Value, rate, def != null ? def.density : 0.0);
                if (range < best)
                {
                    best = range;
                    limitingResource = def;
                }
                any = true;
            }
            if (!any) return false;
            minRange = best;
            return true;
        }

        // Still-air range the craft could cover from now on a single resource. Uses the Breguet range
        // equation, which accounts for the craft getting lighter as fuel burns (burn rate falls with weight
        // in level cruise, so real range exceeds a naive fuel/rate*speed extrapolation):
        //     range = speed * (M / massRate) * ln(M / Mempty)
        // with M = current total mass and Mempty = mass once this resource is exhausted. Reduces to the
        // linear form at small fuel fractions; falls back to it if mass/density data is unavailable.
        private double ProjectedRange(Vessel v, double speed, double currentUnits, double rateUnits, double dens)
        {
            double M = v.totalMass;                          // current total mass (t)
            double mFuel = currentUnits * dens;              // remaining mass of this resource (t)
            double mEmpty = M - mFuel;                       // mass when it runs out (t)
            double massRate = rateUnits * dens;              // burn rate (t/s)
            if (dens > 0.0 && massRate > 0.0 && mFuel > 1e-6 && mEmpty > 1e-3)
                return speed * (M / massRate) * System.Math.Log(M / mEmpty);
            return speed * (currentUnits / rateUnits);       // linear fallback
        }

        // Snapshot of every mass-bearing resource's vessel-wide total, keyed by resource id. Massless
        // resources (ElectricCharge, IntakeAir) are skipped so they can't masquerade as a fuel.
        private Dictionary<int, double> SampleResources(Vessel v)
        {
            massBearingIds.Clear();
            for (int i = 0; i < v.parts.Count; i++)
            {
                PartResourceList prl = v.parts[i].Resources;
                for (int j = 0; j < prl.Count; j++)
                {
                    PartResource pr = prl[j];
                    if (pr.info.density > 0.0) massBearingIds.Add(pr.info.id);
                }
            }

            var snapshot = new Dictionary<int, double>(massBearingIds.Count);
            foreach (int id in massBearingIds)
            {
                v.GetConnectedResourceTotals(id, out double amount, out double _);
                snapshot[id] = amount;
            }
            return snapshot;
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;
            if (Time.fixedTime - lastUpdate < updateFrequency) return;
            lastUpdate = Time.fixedTime;

            double now = Time.fixedTime;
            double dt = now - lastCheckTime;
            bool gap = dt > updateFrequency * 4.0 || dt < 0;   // pause / warp / first tick
            if (v.persistentId != lastVesselPersistentId || v.parts.Count != lastPartCount || gap)
            {
                lastVesselPersistentId = v.persistentId;
                lastPartCount = v.parts.Count;
                ResetAll();
            }
            lastCheckTime = now;

            double speed = v.srfSpeed;
            double vs = v.verticalSpeed;
            bool speedOk = speed >= minSpeed && speed <= maxSpeed;

            // Out of the cruise speed band: rate isn't representative. Drop the window so the burn rate has
            // to rebuild before range can be proven again, and mark the condition unmet.
            if (!speedOk)
            {
                ResetAll();
                met = false;
                curRange = 0.0;
                LogState(speed, vs, false, false, false, 0.0, null);
                CheckVessel(v);
                GetTitle();   // refresh the contracts app with the live range
                return;
            }

            sampleTimes.Add(now);
            sampleAmounts.Add(SampleResources(v));
            while (sampleTimes.Count > 2 && now - sampleTimes[1] >= rateWindowSeconds)
            {
                sampleTimes.RemoveAt(0);
                sampleAmounts.RemoveAt(0);
            }

            bool rateReady = TryMinRange(v, speed, out double range, out PartResourceDefinition limiter);
            bool vsOk = vs >= minVerticalSpeed && vs <= maxVerticalSpeed;
            bool rangeOk = rateReady && range >= requiredRange;
            met = vsOk && rangeOk;   // speedOk already true here
            curRange = rateReady ? range : 0.0;

            LogState(speed, vs, speedOk, vsOk, rangeOk, range, limiter);
            CheckVessel(v);
            GetTitle();   // refresh the contracts app with the live range
        }

        private void LogState(double speed, double vs, bool speedOk, bool vsOk, bool rangeOk, double range, PartResourceDefinition limiter)
        {
            LoggingUtil.LogVerbose(this, "SustainedCruise spd={0:0}({1}) vs={2:0.0}({3}) range={4:0}/{5:0}km({6}) limiter={7} -> {8}",
                speed, speedOk ? "ok" : "X", vs, vsOk ? "ok" : "X",
                range / 1000.0, requiredRange / 1000.0, rangeOk ? "ok" : "X",
                limiter != null ? limiter.name : "-", met ? "IN" : "out");
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return vessel == FlightGlobals.ActiveVessel && met;
        }
    }
}
