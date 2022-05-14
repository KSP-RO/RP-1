using ContractConfigurator.Parameters;
using KSP.Localization;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class RP1ReturnHome : VesselParameter
    {
        public const double DefaultMaxSpeed = 5;

        protected string landAtFacility = string.Empty;
        protected double maxSpeed = DefaultMaxSpeed;

        private float lastUpdate = 0.0f;
        private const float UPDATE_FREQUENCY = 0.5f;

        public RP1ReturnHome()
            : this(null, string.Empty, DefaultMaxSpeed)
        {
        }

        public RP1ReturnHome(string title, string landAtFacility, double maxSpeed)
            : base(title)
        {
            CelestialBody home = FlightGlobals.GetHomeBody();
            this.title = title;
            if (title == null)
            {
                this.title = string.IsNullOrEmpty(landAtFacility) ? Localizer.Format("#cc.param.ReturnHome", home.displayName) :
                                                                    $"Land at facility: {landAtFacility}";
            }
            this.landAtFacility = landAtFacility;
            this.maxSpeed = maxSpeed;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("landAtFacility", landAtFacility);
            node.AddValue("maxSpeed", maxSpeed);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            node.TryGetValue("landAtFacility", ref landAtFacility);
            node.TryGetValue("maxSpeed", ref maxSpeed);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (Time.fixedTime - lastUpdate > UPDATE_FREQUENCY)
            {
                lastUpdate = Time.fixedTime;
                CheckVessel(FlightGlobals.ActiveVessel);
            }
        }

        /// <summary>
        /// Whether this vessel meets the parameter condition.
        /// </summary>
        /// <param name="vessel">The vessel to check</param>
        /// <returns>Whether the vessel meets the condition</returns>
        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return vessel.mainBody.isHomeWorld &&
                (vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.SPLASHED) &&
                vessel.srfSpeed < maxSpeed &&
                (string.IsNullOrEmpty(landAtFacility) || (vessel.landedAt?.Contains(landAtFacility) ?? false));
        }
    }
}
