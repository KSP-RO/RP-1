using ContractConfigurator.Parameters;
using KERBALISM;
using KSP.Localization;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class RP1HasSamples : VesselParameter
    {
        protected string experiment { get; set; }

        private float lastUpdate = 0.0f;
        private float updateFrequency = DEFAULT_UPDATE_FREQUENCY;
        internal const float DEFAULT_UPDATE_FREQUENCY = 2.5f; // This really shouldn't change that often.

        public RP1HasSamples()
            : base(null)
        {
            lastUpdate = Time.fixedTime;
        }

        public RP1HasSamples(string experiment, string title)
            : base(title)
        {
            lastUpdate = Time.fixedTime;
            this.experiment = experiment;

        }
        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
            experiment = ConfigNodeUtil.ParseValue<string>(node, "experiment");
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("updateFrequency", updateFrequency);
            node.AddValue("experiment", experiment);
        }

        protected override string GetParameterTitle()
        {
            if (!string.IsNullOrEmpty(title))
                return title;
            return title = Localizer.Format("#rp0_CC_HasSamplesParam", ScienceDB.GetExperimentInfo(experiment).Title);
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

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (vessel == null) return false;
            foreach (Part p in vessel.Parts)
            {
                foreach (HardDrive hd in p.FindModulesImplementing<HardDrive>())
                {
                    foreach (SubjectData subject in hd.GetDrive().samples.Keys)
                    {
                        if (subject.ExpInfo.ExperimentId == experiment)
                            return true;
                    }
                }
            }
            return false;
        }
    }
}
