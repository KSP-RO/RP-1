using UnityEngine;
using ContractConfigurator.Parameters;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// Instantaneous felt (proper) vertical-acceleration test: met while the active vessel's VERTICAL felt
    /// acceleration is within [minVerticalAccel, maxVerticalAccel] (m/s^2, signed; + is up), optionally while
    /// its surface speed is within [minSpeed, maxSpeed].
    ///
    /// Vertical felt acceleration is the non-gravitational (proper) acceleration projected onto local up:
    ///     vertA = dot(vessel.perturbation, vessel.upAxis)      (perturbation = acceleration - gravity)
    /// i.e. what a G-meter reads along the vertical. Level flight reads ~+9.81 (lift), free-fall reads ~0,
    /// so a band around zero (e.g. [-1, 1]) tests reduced-gravity / parabolic flight, a positive band a
    /// sustained pull-up, and so on. The plain scalar acceleration magnitude cannot make these distinctions
    /// (a level turn reads ~1 g just like free-fall).
    ///
    /// This parameter is a plain instantaneous check. To require the condition be held for a time -- and to
    /// get a countdown display -- pair it with a stock Duration parameter placed as a LATER sibling, and set
    /// disableOnStateChange = false here so its state toggles as the vessel enters/leaves the band (Duration
    /// times only while its preceding siblings are Complete, and resets when one stops).
    ///
    /// Config: minVerticalAccel and/or maxVerticalAccel m/s^2 (at least one), minSpeed/maxSpeed m/s (opt),
    /// updateFrequency (def 0.5), debug (def false).
    /// </summary>
    public class VerticalAcceleration : VesselParameter
    {
        protected double minVerticalAccel { get; set; }   // m/s^2, signed (+ up)
        protected double maxVerticalAccel { get; set; }
        protected double minSpeed { get; set; }            // m/s
        protected double maxSpeed { get; set; }
        protected float updateFrequency { get; set; }
        protected bool debug { get; set; }

        internal const float DEFAULT_UPDATE_FREQUENCY = 0.5f;

        private float lastUpdate = 0f;
        private float lastDebugLog = 0f;

        public VerticalAcceleration() : base(null) { }

        public VerticalAcceleration(string title, double minVerticalAccel, double maxVerticalAccel,
                                    double minSpeed, double maxSpeed, float updateFrequency, bool debug)
            : base(title)
        {
            this.minVerticalAccel = minVerticalAccel;
            this.maxVerticalAccel = maxVerticalAccel;
            this.minSpeed = minSpeed;
            this.maxSpeed = maxSpeed;
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
            return $"Hold vertical acceleration {band}{spd}";
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (FlightGlobals.ActiveVessel == null) return;
            if (Time.fixedTime - lastUpdate < updateFrequency) return;
            lastUpdate = Time.fixedTime;
            CheckVessel(FlightGlobals.ActiveVessel);
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (vessel != FlightGlobals.ActiveVessel) return false;

            double vertAccel = Vector3d.Dot(vessel.perturbation, vessel.upAxis);
            bool ok = vertAccel >= minVerticalAccel && vertAccel <= maxVerticalAccel
                      && vessel.srfSpeed >= minSpeed && vessel.srfSpeed <= maxSpeed;

            if (debug && Time.fixedTime - lastDebugLog >= 2.0f)
            {
                lastDebugLog = Time.fixedTime;
                Debug.Log($"[VerticalAcceleration] vertA={vertAccel:0.###} spd={vessel.srfSpeed:0} -> {(ok ? "IN" : "out")}");
            }

            return ok;
        }
    }
}
