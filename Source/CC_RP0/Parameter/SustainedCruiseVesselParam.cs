using System.Collections.Generic;
using ContractConfigurator.Parameters;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// A single sustained-cruise test. The active vessel must hold ALL of these SIMULTANEOUSLY and
    /// continuously for holdSeconds:
    ///   * surface speed within [minSpeed, maxSpeed]
    ///   * vertical speed within [minVerticalSpeed, maxVerticalSpeed]
    ///   * projected range >= requiredRange, where range = speed * (fuel / averageBurnRate)
    ///
    /// Endurance is measured as RANGE rather than time, so flying faster and/or more efficiently (e.g.
    /// higher, where jets burn less for the same speed) is rewarded. Any violation resets the continuous
    /// hold timer, so the conditions can't be satisfied separately ("cheesed") -- they must all be true at
    /// once for the whole period. Burn rate is averaged over a rolling rateWindowSeconds, and samples are
    /// only taken while at cruise speed so the rate is representative.
    ///
    /// Config: resource (req), requiredRange m (req), holdSeconds (def 300), minSpeed/maxSpeed m/s,
    /// minVerticalSpeed/maxVerticalSpeed m/s, rateWindowSeconds (def 30), updateFrequency (def 0.5),
    /// debug (def false; set true to log the per-tick speed/VS/range/held values).
    /// </summary>
    public class SustainedCruise : VesselParameter
    {
        protected PartResourceDefinition resource { get; set; }
        protected double requiredRange { get; set; }
        protected double holdSeconds { get; set; }
        protected double minSpeed { get; set; }
        protected double maxSpeed { get; set; }
        protected double minVerticalSpeed { get; set; }
        protected double maxVerticalSpeed { get; set; }
        protected double rateWindowSeconds { get; set; }
        protected float updateFrequency { get; set; }
        protected bool debug { get; set; }

        internal const double DEFAULT_HOLD = 300.0;
        internal const double DEFAULT_RATE_WINDOW = 30.0;
        internal const float DEFAULT_UPDATE_FREQUENCY = 0.5f;
        private const double MIN_RATE = 1e-4;   // below this the craft isn't really burning fuel

        private float lastUpdate = 0f;
        private double lastCheckTime = double.NegativeInfinity;
        private int lastPartCount = -1;
        private uint lastVesselPersistentId;
        private float lastDebugLog = 0f;
        private double heldTime = 0.0;   // continuous time all conditions have held
        // Rolling (time, amount) samples for the burn-rate average.
        private readonly List<double> sampleTimes = new List<double>();
        private readonly List<double> sampleAmounts = new List<double>();

        public SustainedCruise() : base(null) { }

        public SustainedCruise(string title, PartResourceDefinition resource, double requiredRange, double holdSeconds,
                               double minSpeed, double maxSpeed, double minVerticalSpeed, double maxVerticalSpeed,
                               double rateWindowSeconds, float updateFrequency, bool debug)
            : base(title)
        {
            this.resource = resource;
            this.requiredRange = requiredRange;
            this.holdSeconds = holdSeconds;
            this.minSpeed = minSpeed;
            this.maxSpeed = maxSpeed;
            this.minVerticalSpeed = minVerticalSpeed;
            this.maxVerticalSpeed = maxVerticalSpeed;
            this.rateWindowSeconds = rateWindowSeconds;
            this.updateFrequency = updateFrequency;
            this.debug = debug;
            this.title = GetParameterTitle();
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("resource", resource.name);
            node.AddValue("requiredRange", requiredRange);
            node.AddValue("holdSeconds", holdSeconds);
            node.AddValue("minSpeed", minSpeed);
            node.AddValue("maxSpeed", maxSpeed);
            node.AddValue("minVerticalSpeed", minVerticalSpeed);
            node.AddValue("maxVerticalSpeed", maxVerticalSpeed);
            node.AddValue("rateWindowSeconds", rateWindowSeconds);
            node.AddValue("updateFrequency", updateFrequency);
            node.AddValue("debug", debug);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            resource = PartResourceLibrary.Instance.GetDefinition(ConfigNodeUtil.ParseValue<string>(node, "resource"));
            requiredRange = ConfigNodeUtil.ParseValue<double>(node, "requiredRange");
            holdSeconds = ConfigNodeUtil.ParseValue<double>(node, "holdSeconds", DEFAULT_HOLD);
            minSpeed = ConfigNodeUtil.ParseValue<double>(node, "minSpeed", 0.0);
            maxSpeed = ConfigNodeUtil.ParseValue<double>(node, "maxSpeed", double.MaxValue);
            minVerticalSpeed = ConfigNodeUtil.ParseValue<double>(node, "minVerticalSpeed", double.MinValue);
            maxVerticalSpeed = ConfigNodeUtil.ParseValue<double>(node, "maxVerticalSpeed", double.MaxValue);
            rateWindowSeconds = ConfigNodeUtil.ParseValue<double>(node, "rateWindowSeconds", DEFAULT_RATE_WINDOW);
            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
            // bool collides with CC's (node, key, allowExpression) overload, so read it manually.
            debug = false;
            if (node.HasValue("debug") && bool.TryParse(node.GetValue("debug"), out bool db)) debug = db;
        }

        protected override string GetParameterTitle()
        {
            double km = requiredRange / 1000.0;
            double hold = holdSeconds / 60.0;
            string band = maxSpeed >= double.MaxValue * 0.5 ? $">= {minSpeed:0} m/s" : $"{minSpeed:0}-{maxSpeed:0} m/s";
            return $"Sustain cruise for {hold:0} min: range >= {km:0} km, speed {band}";
        }

        private void ResetAll()
        {
            sampleTimes.Clear();
            sampleAmounts.Clear();
            heldTime = 0.0;
        }

        // Average burn rate (units/s) and current fuel over the rolling window; false until the window is
        // filled with a contiguous run and the craft is actually burning fuel.
        private bool TryRate(out double rate, out double current)
        {
            rate = 0.0; current = 0.0;
            int n = sampleTimes.Count;
            if (n < 2) return false;
            double span = sampleTimes[n - 1] - sampleTimes[0];
            current = sampleAmounts[n - 1];
            if (span < rateWindowSeconds * 0.9) return false;
            rate = (sampleAmounts[0] - sampleAmounts[n - 1]) / span;
            return rate >= MIN_RATE;
        }

        // Still-air range the craft could cover from now. Uses the Breguet range equation, which accounts
        // for the craft getting lighter as fuel burns (burn rate falls with weight in level cruise, so
        // real range exceeds a naive fuel/rate*speed extrapolation):
        //     range = speed * (M / massRate) * ln(M / Mempty)
        // with M = current total mass and Mempty = mass once this resource is exhausted. Reduces to the
        // linear form at small fuel fractions; falls back to it if mass/density data is unavailable.
        private double ProjectedRange(Vessel v, double speed, double currentUnits, double rateUnits)
        {
            double dens = resource.density;                 // tonnes per unit
            double M = v.totalMass;                          // current total mass (t)
            double mFuel = currentUnits * dens;              // remaining mass of this resource (t)
            double mEmpty = M - mFuel;                       // mass when it runs out (t)
            double massRate = rateUnits * dens;              // burn rate (t/s)
            if (dens > 0.0 && massRate > 0.0 && mFuel > 1e-6 && mEmpty > 1e-3)
                return speed * (M / massRate) * System.Math.Log(M / mEmpty);
            return speed * (currentUnits / rateUnits);       // linear fallback
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
                dt = 0.0;
            }
            lastCheckTime = now;

            double speed = v.srfSpeed;
            double vs = v.verticalSpeed;
            bool speedOk = speed >= minSpeed && speed <= maxSpeed;

            // Out of the cruise speed band: rate isn't representative and the hold is broken. Drop the
            // window + timer so endurance can't be "proven" by slowing down.
            if (!speedOk)
            {
                sampleTimes.Clear();
                sampleAmounts.Clear();
                heldTime = 0.0;
                DebugLog(speed, vs, false, false, false, 0.0, 0.0, 0.0);
                CheckVessel(v);
                return;
            }

            double amount = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                PartResource pr = v.parts[i].Resources.Get(resource.id);
                if (pr != null) amount += pr.amount;
            }
            sampleTimes.Add(now);
            sampleAmounts.Add(amount);
            while (sampleTimes.Count > 2 && now - sampleTimes[1] >= rateWindowSeconds)
            {
                sampleTimes.RemoveAt(0);
                sampleAmounts.RemoveAt(0);
            }

            bool rateReady = TryRate(out double rate, out double current);
            double range = rateReady ? ProjectedRange(v, speed, current, rate) : 0.0;
            bool vsOk = vs >= minVerticalSpeed && vs <= maxVerticalSpeed;
            bool rangeOk = rateReady && range >= requiredRange;

            if (vsOk && rangeOk) heldTime += dt;   // speedOk already true here
            else heldTime = 0.0;

            DebugLog(speed, vs, speedOk, vsOk, rangeOk, range, rate, current);
            CheckVessel(v);
        }

        private void DebugLog(double speed, double vs, bool speedOk, bool vsOk, bool rangeOk, double range, double rate, double current)
        {
            if (!debug || Time.fixedTime - lastDebugLog < 2.0f) return;
            lastDebugLog = Time.fixedTime;
            UnityEngine.Debug.Log($"[SustainedCruise] spd={speed:0}({(speedOk ? "ok" : "X")}) vs={vs:0.0}({(vsOk ? "ok" : "X")}) " +
                $"{resource.name}={current:0} rate={rate:0.###}/s range={range / 1000.0:0}/{requiredRange / 1000.0:0}km({(rangeOk ? "ok" : "X")}) " +
                $"held={heldTime:0}/{holdSeconds:0}s");
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return vessel == FlightGlobals.ActiveVessel && heldTime >= holdSeconds;
        }
    }
}
