using ContractConfigurator.Parameters;
using KERBALISM;
using KSP.Localization;
using KSPCommunityFixes;
using UniLinq;

namespace ContractConfigurator.RP0
{
    public class RP1HasExperiment : VesselParameter
    {
        protected string experiment { get; set; }

        public RP1HasExperiment()
            : base(null)
        {
        }

        public RP1HasExperiment(string experiment, string title)
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
                foreach (Experiment exp in p.FindModulesImplementingReadOnly<Experiment>())
                {
                    if (exp.isEnabled && IncludesExperiment(exp.ExpInfo, experiment))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Does exp include an experiment with id `id`?
        private bool IncludesExperiment(ExperimentInfo exp, string id)
        {
            // Kerbalism makes sure there are no loops in the traversal.
            return exp.ExperimentId == id || exp.IncludedExperiments.Any(info => IncludesExperiment(info, id));
        }
    }
}
