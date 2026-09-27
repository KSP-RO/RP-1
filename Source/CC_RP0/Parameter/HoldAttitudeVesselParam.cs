using System;
using ContractConfigurator.Parameters;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class HoldAttitude : VesselParameter
    {
        protected double targetPitch { get; set; }
        protected double targetHeading { get; set; }
        protected double targetRoll { get; set; }
        protected double pitchTolerance { get; set; }
        protected double headingTolerance { get; set; }
        protected double rollTolerance { get; set; }
        protected float updateFrequency { get; set; }

        private float lastUpdate = 0f;
        private bool met = false;

        internal const float DEFAULT_UPDATE_FREQUENCY = 2.0f;
        internal const double DEFAULT_TOLERANCE = 5.0;

        public HoldAttitude() : base(null) { }

        public HoldAttitude(string title, double targetPitch, double targetHeading, double targetRoll,
            double pitchTolerance, double headingTolerance, double rollTolerance,
            float updateFrequency)
            : base(title)
        {
            this.targetPitch = targetPitch;
            this.targetHeading = targetHeading;
            this.targetRoll = targetRoll;
            this.pitchTolerance = pitchTolerance;
            this.headingTolerance = headingTolerance;
            this.rollTolerance = rollTolerance;
            this.updateFrequency = updateFrequency;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("targetPitch", targetPitch);
            node.AddValue("targetHeading", targetHeading);
            node.AddValue("targetRoll", targetRoll);
            node.AddValue("pitchTolerance", pitchTolerance);
            node.AddValue("headingTolerance", headingTolerance);
            node.AddValue("rollTolerance", rollTolerance);
            node.AddValue("updateFrequency", updateFrequency);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            targetPitch = ConfigNodeUtil.ParseValue<double>(node, "targetPitch");
            targetHeading = ConfigNodeUtil.ParseValue<double>(node, "targetHeading");
            targetRoll = ConfigNodeUtil.ParseValue<double>(node, "targetRoll");
            pitchTolerance = ConfigNodeUtil.ParseValue<double>(node, "pitchTolerance", DEFAULT_TOLERANCE);
            headingTolerance = ConfigNodeUtil.ParseValue<double>(node, "headingTolerance", DEFAULT_TOLERANCE);
            rollTolerance = ConfigNodeUtil.ParseValue<double>(node, "rollTolerance", DEFAULT_TOLERANCE);
            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
        }

        protected override string GetParameterTitle()
        {
            string attitudePart = $"Pitch:{targetPitch:N0}° Heading:{targetHeading:N0}° Roll:{targetRoll:N0}°";
            if (!string.IsNullOrEmpty(title))
                return title + $" ({attitudePart})";
            return $"Hold attitude {attitudePart}";
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (vessel == null) return false;

            // Compute pitch, heading and roll using the same approach MechJeb uses so
            // our readouts match theirs. This builds a surface rotation and transforms
            // the vessel rotation into that surface frame.
            Vector3 North = vessel.north;
            Vector3 East = vessel.east;
            Vector3 Forward = vessel.GetTransform().up;
            Vector3 Up = vessel.up; // surface up vector
            Quaternion RotationSurface = Quaternion.LookRotation(North, Up);
            Quaternion RotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * RotationSurface);
            Vector3 rotEuler = RotationVesselSurface.eulerAngles;
            double heading = rotEuler.y;
            double pitch = rotEuler.x > 180.0 ? 360.0 - rotEuler.x : -rotEuler.x;
            double roll = rotEuler.z > 180.0 ? rotEuler.z - 360.0 : rotEuler.z;

            bool pitchOk = AngleWithin(pitch, targetPitch, pitchTolerance);
            bool headingOk = AngleWithin(heading, targetHeading, headingTolerance);
            bool rollOk = AngleWithin(roll, targetRoll, rollTolerance);

            return pitchOk && headingOk && rollOk;
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

        private static double NormalizeAngle(double a)
        {
            // Normalize to -180..180
            a = (a + 180.0) % 360.0;
            if (a < 0) a += 360.0;
            return a - 180.0;
        }

        private static bool AngleWithin(double value, double target, double tol)
        {
            double diff = Math.Abs(NormalizeAngle(value - target));
            return diff <= Math.Abs(tol);
        }
    }
}
