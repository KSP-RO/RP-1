using UnityEngine;
using ContractConfigurator.Parameters;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// Sustained felt (proper) vertical-acceleration test. The active vessel must hold its VERTICAL felt
    /// acceleration within [minVerticalAccel, maxVerticalAccel] (m/s^2, signed; + is up) -- optionally while
    /// its surface speed is within [minSpeed, maxSpeed] -- continuously for holdSeconds.
    ///
    /// Vertical felt acceleration is the non-gravitational (proper) acceleration projected onto local up:
    ///     vertA = dot(vessel.perturbation, vessel.upAxis)      (perturbation = acceleration - gravity)
    /// i.e. what a G-meter reads along the vertical. Level flight reads ~+9.81 (lift), free-fall reads ~0,
    /// so a band around zero (e.g. [-0.5, 0.5]) tests reduced-gravity / parabolic flight, a positive band
    /// tests a sustained pull-up, and so on. The plain scalar acceleration magnitude cannot make these
    /// distinctions (a level turn reads ~1 g just like free-fall). Any violation resets the continuous hold
    /// timer so the condition can't be satisfied piecemeal.
    ///
    /// Config: minVerticalAccel and/or maxVerticalAccel m/s^2 (at least one), holdSeconds (def 20),
    /// minSpeed/maxSpeed m/s (optional), updateFrequency (def 0.5), debug (def false).
    /// </summary>
    public class VerticalAcceleration : VesselParameter
    {
        protected double minVerticalAccel { get; set; }   // m/s^2, signed (+ up)
        protected double maxVerticalAccel { get; set; }
        protected double minSpeed { get; set; }            // m/s
        protected double maxSpeed { get; set; }
        protected double holdSeconds { get; set; }
        protected float updateFrequency { get; set; }
        protected bool debug { get; set; }

        internal const double DEFAULT_HOLD = 20.0;
        internal const float DEFAULT_UPDATE_FREQUENCY = 0.5f;

        private float lastUpdate = 0f;
        private double lastCheckTime = double.NegativeInfinity;
        private uint lastVesselPersistentId;
        private float lastDebugLog = 0f;
        private double heldTime = 0.0;   // continuous time the condition has held

        public VerticalAcceleration() : base(null) { }

        public VerticalAcceleration(string title, double minVerticalAccel, double maxVerticalAccel,
                                    double minSpeed, double maxSpeed, double holdSeconds,
                                    float updateFrequency, bool debug)
            : base(title)
        {
            this.minVerticalAccel = minVerticalAccel;
            this.maxVerticalAccel = maxVerticalAccel;
            this.minSpeed = minSpeed;
            this.maxSpeed = maxSpeed;
            this.holdSeconds = holdSeconds;
            this.updateFrequency = updateFrequency;
            this.debug = debug;
            this.title = GetParameterTitle();
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("minVerticalAccel", minVerticalAccel);
            node.AddValue("maxVerticalAccel", maxVerticalAccel);
            node.AddValue("minSpeed", minSpeed);
            node.AddValue("maxSpeed", maxSpeed);
            node.AddValue("holdSeconds", holdSeconds);
            node.AddValue("updateFrequency", updateFrequency);
            node.AddValue("debug", debug);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            minVerticalAccel = ConfigNodeUtil.ParseValue<double>(node, "minVerticalAccel", double.MinValue);
            maxVerticalAccel = ConfigNodeUtil.ParseValue<double>(node, "maxVerticalAccel", double.MaxValue);
            minSpeed = ConfigNodeUtil.ParseValue<double>(node, "minSpeed", 0.0);
            maxSpeed = ConfigNodeUtil.ParseValue<double>(node, "maxSpeed", double.MaxValue);
            holdSeconds = ConfigNodeUtil.ParseValue<double>(node, "holdSeconds", DEFAULT_HOLD);
            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
            // bool collides with CC's (node, key, allowExpression) overload, so read it manually.
            debug = false;
            if (node.HasValue("debug") && bool.TryParse(node.GetValue("debug"), out bool db)) debug = db;
        }

        protected override string GetParameterTitle()
        {
            bool hasMin = minVerticalAccel > double.MinValue * 0.5;
            bool hasMax = maxVerticalAccel < double.MaxValue * 0.5;
            string band;
            if (hasMin && hasMax && System.Math.Abs(minVerticalAccel + maxVerticalAccel) < 1e-6)
                band = $"within +/-{maxVerticalAccel:0.##} m/s^2";
            else if (hasMin && hasMax)
                band = $"between {minVerticalAccel:0.##} and {maxVerticalAccel:0.##} m/s^2";
            else if (hasMax)
                band = $"below {maxVerticalAccel:0.##} m/s^2";
            else
                band = $"above {minVerticalAccel:0.##} m/s^2";
            string spd = (maxSpeed < double.MaxValue * 0.5 || minSpeed > 0.0)
                ? $" (speed {minSpeed:0}-{(maxSpeed < double.MaxValue * 0.5 ? maxSpeed.ToString("0") : "inf")} m/s)"
                : "";
            return $"Hold vertical acceleration {band} for {holdSeconds:0} s{spd}";
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
            bool gap = dt > updateFrequency * 4.0 || dt < 0;   // pause / warp / vessel switch / first tick
            if (v.persistentId != lastVesselPersistentId || gap)
            {
                lastVesselPersistentId = v.persistentId;
                heldTime = 0.0;
                dt = 0.0;
            }
            lastCheckTime = now;

            double vertAccel = Vector3d.Dot(v.perturbation, v.upAxis);
            bool accelOk = vertAccel >= minVerticalAccel && vertAccel <= maxVerticalAccel;
            bool speedOk = v.srfSpeed >= minSpeed && v.srfSpeed <= maxSpeed;

            if (accelOk && speedOk) heldTime += dt;
            else heldTime = 0.0;

            DebugLog(vertAccel, v.srfSpeed, accelOk, speedOk);
            CheckVessel(v);
        }

        private void DebugLog(double vertAccel, double speed, bool accelOk, bool speedOk)
        {
            if (!debug || Time.fixedTime - lastDebugLog < 2.0f) return;
            lastDebugLog = Time.fixedTime;
            Debug.Log($"[VerticalAcceleration] vertA={vertAccel:0.###}({(accelOk ? "ok" : "X")}) " +
                $"spd={speed:0}({(speedOk ? "ok" : "X")}) held={heldTime:0}/{holdSeconds:0}s");
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return vessel == FlightGlobals.ActiveVessel && heldTime >= holdSeconds;
        }
    }
}
