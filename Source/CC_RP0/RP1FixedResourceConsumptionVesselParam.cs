using System;
using System.Collections.Generic;
using ContractConfigurator.Parameters;
using Contracts;
using KerbalConstructionTime;
using KSP.Localization;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class RP1FixedResourceConsumptionChecker : MonoBehaviour
    {
        private int activeCount = 0;
        private HashSet<PartResourceDefinition> resources = new HashSet<PartResourceDefinition>();

        private static int CHECK_SIZE = 10;
        private int checks = 0;
        private Dictionary<PartResourceDefinition, double[]> checkValues = new Dictionary<PartResourceDefinition, double[]>();
        private Guid currentVessel;
        private int partCount;

        public static RP1FixedResourceConsumptionChecker Instance;

        void Start()
        {
            Instance = this;
        }

        void FixedUpdate()
        {
            if (activeCount == 0 || FlightGlobals.ActiveVessel == null)
            {
                return;
            }

            if (FlightGlobals.ActiveVessel.id != currentVessel)
            {
                currentVessel = FlightGlobals.ActiveVessel.id;
                checks = 0;
                partCount = -1;
            }

            foreach (PartResourceDefinition resource in resources)
            {
                int localCount = 0;
                double quantity = 0.0;
                foreach (Part part in FlightGlobals.ActiveVessel.Parts)
                {
                    localCount++;
                    PartResource pr = part.Resources[resource.name];
                    if (pr != null)
                    {
                        quantity += pr.amount;
                    }
                }

                if (partCount == -1)
                {
                    partCount = localCount;
                }
                // Reset counter on vessel change
                if (partCount != localCount)
                {
                    partCount = localCount;
                    checks = 0;
                }

                checkValues[resource][checks++ % CHECK_SIZE] = quantity;
            }
        }

        public static void Register(PartResourceDefinition resource)
        {
            if (Instance == null || !Instance.isActiveAndEnabled)
            {
                return;
            }

            Instance.activeCount++;
            if (!Instance.resources.Contains(resource))
            {
                Instance.resources.Add(resource);
                Instance.checkValues[resource] = new double[CHECK_SIZE];
            }
        }

        public static void UnRegister()
        {
            if (Instance == null || !Instance.isActiveAndEnabled)
            {
                return;
            }

            Instance.activeCount--;
            if (Instance.activeCount <= 0)
            {
                Instance.activeCount = 0;
                Instance.checkValues.Clear();
                Instance.resources.Clear();
            }
        }

        public static bool CanCheckVessel(Vessel vessel)
        {
            if (Instance == null || !Instance.isActiveAndEnabled)
            {
                return false;
            }

            return vessel.id == Instance.currentVessel && Instance.checks >= CHECK_SIZE;
        }

        public double Consumption(PartResourceDefinition resource)
        {
            double delta = 0;
            double[] vals = checkValues[resource];
            for (int i = 0; i < CHECK_SIZE - 1; i++)
            {
                delta += vals[(i + checks) % CHECK_SIZE] - vals[(i + checks + 1) % CHECK_SIZE];
            }
            delta /= (CHECK_SIZE - 1) * Time.fixedDeltaTime;

            return delta;
        }
    }

    /// <summary>
    /// Parameter for checking the consumption of the given resource
    /// </summary>
    public class RP1FixedResourceConsumption : VesselParameter
    {
        public PartResourceDefinition resource { get; set; }
        public double minRate { get; set; }
        public double maxRate { get; set; }

        private float lastUpdate = 0.0f;
        private const float UPDATE_FREQUENCY = 0.25f;

        public RP1FixedResourceConsumption()
            : base(null)
        {
        }

        public RP1FixedResourceConsumption(double minRate, double maxRate, PartResourceDefinition resource, string title = null)
            : base(title)
        {
            if (minRate < 0 && maxRate < 0 && maxRate < minRate)
            {
                this.minRate = maxRate;
                this.maxRate = minRate;
            }
            else
            {
                this.minRate = minRate;
                this.maxRate = maxRate;
            }

            if (maxRate == double.MaxValue && minRate == double.MinValue)
            {
                this.minRate = this.maxRate = 0.0;
            }

            this.resource = resource;
        }

        protected override string GetParameterTitle()
        {
            string output = null;
            if (string.IsNullOrEmpty(title))
            {
                string resourceStr;
                if (maxRate == 0.0 && minRate == 0.0)
                {
                    resourceStr = Localizer.GetStringByTag("#cc.param.count.none");
                }
                else if (maxRate == double.MaxValue)
                {
                    resourceStr = Localizer.Format("#cc.param.RP1FixedResourceConsumption.atLeast", minRate.ToString("N1"));
                }
                else if (minRate == 0.0)
                {
                    resourceStr = Localizer.Format("#cc.param.RP1FixedResourceConsumption.atMost", maxRate.ToString("N1"));
                }
                else if (minRate == double.MinValue)
                {
                    resourceStr = Localizer.Format("#cc.param.RP1FixedResourceConsumption.atLeast", (-maxRate).ToString("N1"));
                }
                else if (maxRate == 0.0)
                {
                    resourceStr = Localizer.Format("#cc.param.RP1FixedResourceConsumption.atMost", (-minRate).ToString("N1"));
                }
                else if (minRate >= 0)
                {
                    resourceStr = Localizer.Format("#cc.param.RP1FixedResourceConsumption.between", minRate.ToString("N1"), maxRate.ToString("N1"));
                }
                else if (minRate < 0)
                {
                    resourceStr = Localizer.Format("#cc.param.RP1FixedResourceConsumption.between", (-maxRate).ToString("N1"), (-minRate).ToString("N1"));
                }
                else
                {
                    // Shouldn't happen
                    resourceStr = "Unknown";
                }
                output = Localizer.Format((minRate >= 0.0 && maxRate > 0.0 ? "#cc.param.RP1FixedResourceConsumption.consumption" : "#cc.param.RP1FixedResourceConsumption.production"), resource.name, resourceStr);
            }
            else
            {
                output = title;
            }
            return output;
        }

        protected static bool VesselHasResource(Vessel vessel, PartResourceDefinition resource, bool capacity, double minQuantity, double maxQuantity)
        {
            double quantity = capacity ? vessel.ResourceCapacity(resource) : vessel.ResourceQuantity(resource);
            return quantity >= minQuantity && quantity <= maxQuantity;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            if (minRate != double.MinValue)
            {
                node.AddValue("minRate", minRate);

            }
            if (maxRate != double.MaxValue)
            {
                node.AddValue("maxRate", maxRate);
            }
            node.AddValue("resource", resource.name);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            resource = ConfigNodeUtil.ParseValue<PartResourceDefinition>(node, "resource");
            minRate = ConfigNodeUtil.ParseValue<double>(node, "minRate", double.MinValue);
            maxRate = ConfigNodeUtil.ParseValue<double>(node, "maxRate", double.MaxValue);
        }

        protected override void OnRegister()
        {
            RP1FixedResourceConsumptionChecker.Register(resource);
            base.OnRegister();
        }

        protected override void OnUnregister()
        {
            RP1FixedResourceConsumptionChecker.UnRegister();
            base.OnUnregister();
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (UnityEngine.Time.fixedTime - lastUpdate > UPDATE_FREQUENCY)
            {
                lastUpdate = UnityEngine.Time.fixedTime;
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
            LoggingUtil.LogVerbose(this, "Checking VesselMeetsCondition: {0}", vessel.id);

            double delta = 0.0;
            if (RP1FixedResourceConsumptionChecker.CanCheckVessel(vessel))
            {
                delta = RP1FixedResourceConsumptionChecker.Instance.Consumption(resource);
            }
            else
            {
                return false;
            }

            LoggingUtil.LogVerbose(this, "Delta for resource {0} is: {1}", resource.name, delta);
            return delta - minRate >= -0.001 && maxRate - delta >= -0.001;
        }
    }
}
