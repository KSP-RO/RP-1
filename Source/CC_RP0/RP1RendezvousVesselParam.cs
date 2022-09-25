using ContractConfigurator.Parameters;
using Contracts;
using KerbalConstructionTime;
using KSP.Localization;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class RP1Rendezvous : VesselParameter
    {
        protected double distance { get; set; }
        protected double relativeSpeed { get; set; }

        private float lastUpdate = 0.0f;
        private const float UPDATE_FREQUENCY = 0.50f;

        public RP1Rendezvous()
            : base(null)
        {
        }

        public RP1Rendezvous(double distance, double relativeSpeed, string title)
            : base(title)
        {
            this.distance = distance;
            this.relativeSpeed = relativeSpeed;
            this.title = title;
            disableOnStateChange = true;
        }

        protected override string GetParameterTitle()
        {
            string output;
            if (string.IsNullOrEmpty(title))
            {
                output = Localizer.Format("#cc.param.Rendezvous.1", Localizer.GetStringByTag("#cc.param.vessel.Any"));
            }
            else
            {
                output = title;
            }
            return output;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("distance", distance);
            node.AddValue("relativeSpeed", relativeSpeed);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            distance = ConfigNodeUtil.ParseValue<double>(node, "distance");
            relativeSpeed = ConfigNodeUtil.ParseValue<double>(node, "relativeSpeed");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (Time.fixedTime - lastUpdate < UPDATE_FREQUENCY) return;

            lastUpdate = Time.fixedTime;

            if (FlightGlobals.VesselsLoaded.Count < 2) return;

            Vessel v1 = FlightGlobals.ActiveVessel;
            if (!IsValidVessel(v1)) return;

            string id1 = v1.GetKCTVesselId();
            bool forceStateChange = false;
            bool rendezvous = false;
            foreach (Vessel v2 in FlightGlobals.VesselsLoaded)
            {
                if (v2 != v1 && IsValidVessel(v2) && id1 != v2.GetKCTVesselId())
                {
                    float distance = Vector3.Distance(v1.transform.position, v2.transform.position);
                    double relSpeed = (v1.obt_velocity - v2.obt_velocity).magnitude;
                    if (distance < this.distance && (this.relativeSpeed <= 0 || relSpeed < this.relativeSpeed))
                    {
                        rendezvous = true;
                        forceStateChange |= SetState(v1, ParameterState.Complete);
                        forceStateChange |= SetState(v2, ParameterState.Complete);
                    }
                }
            }

            if (rendezvous)
            {
                CheckVessel(FlightGlobals.ActiveVessel, forceStateChange);
            }
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return GetState(vessel) == ParameterState.Complete;
        }

        private static bool IsValidVessel(Vessel v)
        {
            return v != null && !v.isEVA && v.vesselType != VesselType.Debris && v.vesselType != VesselType.Flag;
        }
    }
}
