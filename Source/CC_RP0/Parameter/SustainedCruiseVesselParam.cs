using System.Collections.Generic;
using ContractConfigurator.Parameters;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// Instantaneous cruise-endurance check: met while the active vessel is within the [minSpeed, maxSpeed]
    /// and [minVerticalSpeed, maxVerticalSpeed] bands AND its projected still-air range >= requiredRange.
    /// Endurance is measured as RANGE, not time, so faster / more efficient flight is rewarded. Range is
    /// projected per fuel and the minimum reported; how fuels are tracked lives on GatherEnginePropellants,
    /// the min-range logic on TryMinRange, and the Breguet math on ProjectedRange.
    ///
    /// Pair with a LATER stock Duration sibling (and set disableOnStateChange = false here) to require the
    /// band be held for a time with a countdown. Config keys: requiredRange (req, m), minSpeed, maxSpeed,
    /// minVerticalSpeed, maxVerticalSpeed (m/s; all but requiredRange/minSpeed optional), rateWindowSeconds
    /// (def 30), updateFrequency (def 0.5).
    /// </summary>
    public class SustainedCruise : VesselParameter
    {
        protected double requiredRange { get; set; }
        protected double minSpeed { get; set; }
        protected double? maxSpeed { get; set; }            // null = no upper speed bound
        protected double? minVerticalSpeed { get; set; }    // null = no lower VS bound
        protected double? maxVerticalSpeed { get; set; }    // null = no upper VS bound
        protected double rateWindowSeconds { get; set; }
        protected float updateFrequency { get; set; }

        internal const double DEFAULT_RATE_WINDOW = 30.0;
        internal const float DEFAULT_UPDATE_FREQUENCY = 0.5f;
        private const double MIN_RATE = 1e-4;   // below this the craft isn't really burning this resource
        private const double EMPTY_UNITS = 1e-6;   // at/below this a resource is spent, not a live limiter

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
        // Mass-bearing propellants ever burned by a working engine. Additive: only cleared when the active
        // vessel changes, never on engine shutdown/staging, so old fuels keep projecting (see class summary).
        private readonly HashSet<int> trackedPropellantIds = new HashSet<int>();
        // Engines on the vessel, cached and only rebuilt on part-count / vessel change so the per-tick
        // propellant gather doesn't walk every part's module list. Entries can go null if a part is destroyed
        // between rebuilds, so the gather null-checks them.
        private readonly List<ModuleEngines> engineCache = new List<ModuleEngines>();
        // Snapshot dictionaries are pooled and reused across the rolling window so steady-state sampling
        // doesn't allocate a fresh Dictionary every tick.
        private readonly Stack<Dictionary<int, double>> dictPool = new Stack<Dictionary<int, double>>();

        public SustainedCruise() : base(null) { }

        public SustainedCruise(string title, double requiredRange,
                               double minSpeed, double? maxSpeed, double? minVerticalSpeed, double? maxVerticalSpeed,
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
            if (maxSpeed.HasValue) node.AddValue("maxSpeed", maxSpeed.Value);
            if (minVerticalSpeed.HasValue) node.AddValue("minVerticalSpeed", minVerticalSpeed.Value);
            if (maxVerticalSpeed.HasValue) node.AddValue("maxVerticalSpeed", maxVerticalSpeed.Value);
            node.AddValue("rateWindowSeconds", rateWindowSeconds);
            node.AddValue("updateFrequency", updateFrequency);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            requiredRange = ConfigNodeUtil.ParseValue<double>(node, "requiredRange");
            minSpeed = ConfigNodeUtil.ParseValue<double>(node, "minSpeed", 0.0);
            maxSpeed = ConfigNodeUtil.ParseValue(node, "maxSpeed", (double?)null);
            minVerticalSpeed = ConfigNodeUtil.ParseValue(node, "minVerticalSpeed", (double?)null);
            maxVerticalSpeed = ConfigNodeUtil.ParseValue(node, "maxVerticalSpeed", (double?)null);
            rateWindowSeconds = ConfigNodeUtil.ParseValue<double>(node, "rateWindowSeconds", DEFAULT_RATE_WINDOW);
            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
        }

        protected override string GetParameterTitle()
        {
            double reqKm = requiredRange / 1000.0;
            string speedBand = maxSpeed.HasValue ? $"{minSpeed:N0}-{maxSpeed.Value:N0} m/s" : $">= {minSpeed:N0} m/s";
            string rangePart = FlightGlobals.ActiveVessel == null
                ? $"range >= {reqKm:N0} km"
                : $"range {curRange / 1000.0:N0} / {reqKm:N0} km";
            return $"Cruise: {rangePart}, speed {speedBand}{VerticalSpeedBand()}";
        }

        // "" when unconstrained, else e.g. ", VS 0-5 m/s" / ", VS >= -2 m/s" / ", VS <= 5 m/s". Included in the
        // title so a player failing purely on vertical speed gets feedback rather than a silently-incomplete param.
        private string VerticalSpeedBand()
        {
            if (!minVerticalSpeed.HasValue && !maxVerticalSpeed.HasValue) return "";
            if (minVerticalSpeed.HasValue && maxVerticalSpeed.HasValue)
                return $", VS {minVerticalSpeed.Value:N0}-{maxVerticalSpeed.Value:N0} m/s";
            if (maxVerticalSpeed.HasValue)
                return $", VS <= {maxVerticalSpeed.Value:N0} m/s";
            return $", VS >= {minVerticalSpeed.Value:N0} m/s";
        }

        private void ResetAll()
        {
            for (int i = 0; i < sampleAmounts.Count; i++) dictPool.Push(sampleAmounts[i]);
            sampleTimes.Clear();
            sampleAmounts.Clear();
        }

        // Smallest projected range (m) across the tracked propellants still being consumed, i.e. whichever
        // fuel is the endurance limiter. False until the window is filled with a contiguous run and at least
        // one tracked propellant is actually being consumed. limitingResource names the limiter.
        private bool TryMinRange(Vessel v, double speed, out double minRange, out PartResourceDefinition limitingResource,
                                 out double limAmount, out double limRate)
        {
            minRange = 0.0;
            limitingResource = null;
            limAmount = 0.0;
            limRate = 0.0;
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
                // A fuel that has drained to empty is spent, not a live limiter: skip it so it can't drag the
                // min range to 0 for a window after it runs dry while the craft flies on another fuel.
                if (kv.Value <= EMPTY_UNITS) continue;
                double rate = (old - kv.Value) / span;
                if (rate < MIN_RATE) continue;   // not really burning this one
                PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(kv.Key);
                double range = ProjectedRange(v, speed, kv.Value, rate, def != null ? def.density : 0.0);
                if (range < best)
                {
                    best = range;
                    limitingResource = def;
                    limAmount = kv.Value;
                    limRate = rate;
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

        // Rebuild the engine cache. Called only on part-count / vessel change, so the per-tick gather below
        // iterates a short cached list instead of every part's module list.
        private void RebuildEngineCache(Vessel v)
        {
            engineCache.Clear();
            for (int i = 0; i < v.parts.Count; i++)
            {
                PartModuleList modules = v.parts[i].Modules;
                for (int m = 0; m < modules.Count; m++)
                    if (modules[m] is ModuleEngines me) engineCache.Add(me);
            }
        }

        // Add every mass-bearing propellant burned by a currently-working cached engine to the tracked set.
        // Additive: a fuel is never removed once seen, so shutting an engine down or staging it away leaves its
        // fuel tracked (its burn rate just falls to ~0 and it drops out of the range ranking). Massless
        // propellants (IntakeAir, ElectricCharge) are skipped so they can't masquerade as the endurance limiter.
        private void GatherEnginePropellants()
        {
            for (int i = 0; i < engineCache.Count; i++)
            {
                ModuleEngines me = engineCache[i];
                if (me == null || !me.isOperational || me.currentThrottle <= 0f) continue;
                List<Propellant> props = me.propellants;
                for (int k = 0; k < props.Count; k++)
                {
                    int id = props[k].id;
                    if (trackedPropellantIds.Contains(id)) continue;   // already tracked -- skip the lookup
                    PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(id);
                    if (def != null && def.density > 0.0) trackedPropellantIds.Add(id);
                }
            }
        }

        // Vessel-wide total of each tracked propellant, keyed by resource id. Uses a pooled dictionary so
        // steady-state sampling doesn't allocate; callers return the dict to dictPool once it ages out.
        private Dictionary<int, double> SnapshotTracked(Vessel v)
        {
            Dictionary<int, double> snapshot = dictPool.Count > 0 ? dictPool.Pop() : new Dictionary<int, double>();
            snapshot.Clear();
            foreach (int id in trackedPropellantIds)
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
            bool vesselChanged = v.persistentId != lastVesselPersistentId;
            if (vesselChanged || v.parts.Count != lastPartCount || gap)
            {
                lastVesselPersistentId = v.persistentId;
                lastPartCount = v.parts.Count;
                // The tracked-fuel set is per-vessel and additive, so only a different vessel clears it; a
                // part-count change (staging) or a warp/pause gap only invalidates the rolling rate window.
                if (vesselChanged) trackedPropellantIds.Clear();
                RebuildEngineCache(v);
                ResetAll();
            }
            lastCheckTime = now;

            double speed = v.srfSpeed;
            double vs = v.verticalSpeed;

            // Sample and project range every tick regardless of speed, so the estimate is shown even while
            // outside the cruise band (climbing / accelerating in). The rolling rate converges to the cruise
            // value within one window; met still requires speedOk each tick, so the Duration hold only
            // accrues in-band and can't be "proven" out of band.
            GatherEnginePropellants();
            sampleTimes.Add(now);
            sampleAmounts.Add(SnapshotTracked(v));
            while (sampleTimes.Count > 2 && now - sampleTimes[1] >= rateWindowSeconds)
            {
                dictPool.Push(sampleAmounts[0]);
                sampleTimes.RemoveAt(0);
                sampleAmounts.RemoveAt(0);
            }

            bool rateReady = TryMinRange(v, speed, out double range, out PartResourceDefinition limiter,
                                         out double limAmount, out double limRate);
            curRange = rateReady ? range : 0.0;

            bool speedOk = speed >= minSpeed && (!maxSpeed.HasValue || speed <= maxSpeed.Value);
            bool vsOk = (!minVerticalSpeed.HasValue || vs >= minVerticalSpeed.Value)
                     && (!maxVerticalSpeed.HasValue || vs <= maxVerticalSpeed.Value);
            bool rangeOk = rateReady && range >= requiredRange;
            met = speedOk && vsOk && rangeOk;

            // Gate the debug dumps on the log level so the params arrays / double boxing don't run every tick
            // in the normal Release case. Set the CC log level for this type to DEBUG via an
            // ADD_LOGLEVEL_EXCEPTION to see them (LogDebug, not LogVerbose, so they survive Release).
            if (LoggingUtil.GetLogLevel(GetType()) <= LoggingUtil.LogLevel.DEBUG)
            {
                LogState(speed, vs, speedOk, vsOk, rangeOk, range, limiter);
                if (rateReady && limiter != null) LogRangeBreakdown(v, speed, limiter, limAmount, limRate, range);
            }
            CheckVessel(v);
            GetTitle();   // refresh the contracts app with the live range
        }

        // Per-term dump of the Breguet inputs, to diff against FAR's PerformanceEnvelopeCalculator:
        // FAR range reduces to V*(M/mdot)*ln(M/Mempty), the same formula, so any mismatch is in these
        // inputs. Compare mdot against FAR's FuelFlowKgPerS / the engine PAW, and M/fuel against FAR's
        // MassKg / UsableFuelKg. Both the Breguet and linear (fuel/rate) ranges are shown.
        private void LogRangeBreakdown(Vessel v, double speed, PartResourceDefinition limiter, double amount, double rate, double range)
        {
            double dens = limiter.density;
            double M = v.totalMass;
            double mFuel = amount * dens;
            double mEmpty = M - mFuel;
            double mdotKgS = rate * dens * 1000.0;
            double lnTerm = mEmpty > 1e-3 ? System.Math.Log(M / mEmpty) : 0.0;
            double linear = speed * (amount / rate);
            LoggingUtil.LogDebug(this, "SustainedCruise[range] {0} V={1:0.0}m/s M={2:0.000}t fuel={3:0.000}t empty={4:0.000}t " +
                "mdot={5:0.####}kg/s ln={6:0.####} breguet={7:0}m linear={8:0}m",
                limiter.name, speed, M, mFuel, mEmpty, mdotKgS, lnTerm, range, linear);
        }

        private void LogState(double speed, double vs, bool speedOk, bool vsOk, bool rangeOk, double range, PartResourceDefinition limiter)
        {
            LoggingUtil.LogDebug(this, "SustainedCruise spd={0:0}({1}) vs={2:0.0}({3}) range={4:0}/{5:0}km({6}) limiter={7} -> {8}",
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
