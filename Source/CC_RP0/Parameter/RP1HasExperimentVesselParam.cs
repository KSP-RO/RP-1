using ContractConfigurator.Parameters;
using KERBALISM;
using KSP.Localization;
using System.Linq;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class RP1HasExperimentVesselParam : VesselParameter
    {
        protected string experiment { get; set; }

        public RP1HasExperimentVesselParam()
            : base(null)
        {
            experiment = "RP0telemetry1";
        }

        public RP1HasExperimentVesselParam(string experiment, string title)
            : base(title)
        {
            this.experiment = experiment;
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            experiment = ConfigNodeUtil.ParseValue<string>(node, "experiment");
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("experiment", experiment);
        }

        protected override string GetParameterTitle()
        {
            if (!string.IsNullOrEmpty(title))
                return title;
            return title = Localizer.Format("#rp0_CC_HasExperimentParam", ScienceDB.GetExperimentInfo(experiment).Title);
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (vessel == null) return false;
            foreach (Part p in vessel.Parts)
            {
                foreach (Experiment exp in p.FindModulesImplementing<Experiment>())
                {
                    if (exp.isEnabled && (exp.experiment_id == experiment || exp.ExpInfo.IncludedExperiments.Any(inc => inc.ExperimentId == experiment)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
