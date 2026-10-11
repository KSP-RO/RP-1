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

        protected double pitch { get; set; }
        protected double heading { get; set; }
        protected double roll { get; set; }

        private float lastUpdate = 0f;

        internal const float DEFAULT_UPDATE_FREQUENCY = 0.5f;
        internal const double DEFAULT_TOLERANCE = 5.0;
        internal const double DEFAULT_ANGLE = double.MaxValue;

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

            CreateDelegates();
        }

        protected void CreateDelegates()
        {
            if (targetPitch != double.MaxValue)
            {
                AddParameter(new ParameterDelegate<Vessel>($"Pitch: {targetPitch:N0}°", v => AngleWithin(pitch, targetPitch, pitchTolerance)));
            }
            if (targetHeading != double.MaxValue)
            {
                AddParameter(new ParameterDelegate<Vessel>($"Heading: {targetHeading:N0}°", v => AngleWithin(heading, targetHeading, headingTolerance)));
            }
            if (targetRoll != double.MaxValue)
            {
                AddParameter(new ParameterDelegate<Vessel>($"Roll: {targetRoll:N0}°", v => AngleWithin(roll, targetRoll, rollTolerance)));
            }
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            if (targetPitch != double.MaxValue)
            {
                node.AddValue("targetPitch", targetPitch);
            }
            if (targetHeading != double.MaxValue)
            {
                node.AddValue("targetHeading", targetHeading);
            }
            if (targetRoll != double.MaxValue)
            {
                node.AddValue("targetRoll", targetRoll);
            }
            node.AddValue("pitchTolerance", pitchTolerance);
            node.AddValue("headingTolerance", headingTolerance);
            node.AddValue("rollTolerance", rollTolerance);
            node.AddValue("updateFrequency", updateFrequency);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            try
            {
                base.OnParameterLoad(node);
                targetPitch = ConfigNodeUtil.ParseValue<double>(node, "targetPitch", DEFAULT_ANGLE);
                targetHeading = ConfigNodeUtil.ParseValue<double>(node, "targetHeading", DEFAULT_ANGLE);
                targetRoll = ConfigNodeUtil.ParseValue<double>(node, "targetRoll", DEFAULT_ANGLE);
                pitchTolerance = ConfigNodeUtil.ParseValue<double>(node, "pitchTolerance", DEFAULT_TOLERANCE);
                headingTolerance = ConfigNodeUtil.ParseValue<double>(node, "headingTolerance", DEFAULT_TOLERANCE);
                rollTolerance = ConfigNodeUtil.ParseValue<double>(node, "rollTolerance", DEFAULT_TOLERANCE);
                updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);

                CreateDelegates();
            }
            finally
            {
                ParameterDelegate<Vessel>.OnDelegateContainerLoad(node);
            }
        }

        protected override string GetParameterTitle()
        {
            if (!string.IsNullOrEmpty(title))
                return title;
            return $"Hold specified attitude";
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            LoggingUtil.LogVerbose(this, "Checking VesselMeetsCondition: {0}", vessel.id);
            return ParameterDelegate<Vessel>.CheckChildConditions(this, vessel);
        }

        protected override void OnUpdate()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            base.OnUpdate();

            if (Time.fixedTime - lastUpdate > updateFrequency)
            {
                lastUpdate = Time.fixedTime;

                // Compute pitch, heading and roll using the same approach MechJeb uses so
                // our readouts match theirs. This builds a surface rotation and transforms
                // the vessel rotation into that surface frame.
                Vector3 North = v.north;
                Vector3 Up = v.up; // surface up vector
                Quaternion RotationSurface = Quaternion.LookRotation(North, Up);
                Quaternion RotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(v.GetTransform().rotation) * RotationSurface);
                Vector3 rotEuler = RotationVesselSurface.eulerAngles;
                heading = rotEuler.y;
                pitch = rotEuler.x > 180.0 ? 360.0 - rotEuler.x : -rotEuler.x;
                roll = rotEuler.z > 180.0 ? rotEuler.z - 360.0 : rotEuler.z;
                roll = -roll; // Invert to follow the rule that a roll to the right is positive roll

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
