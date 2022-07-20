using Contracts;
using System.Collections.Generic;
using System.Linq;

namespace ContractConfigurator.RP0
{
    public class RP1CollectScienceFactory : ParameterFactory
    {
        protected Biome biome { get; set; }
        protected ExperimentSituations? situation { get; set; }
        protected BodyLocation? location { get; set; }
        protected List<ScienceExperiment> experiment { get; set; }
        protected List<ScienceSubject> subjects { get; set; }
        protected double fractionComplete { get; set; }

        public override bool Load(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue(configNode, "biome", x => biome = x, this, (Biome)null);
            valid &= ConfigNodeUtil.ParseValue(configNode, "situation", x => situation = x, this, (ExperimentSituations?)null);
            valid &= ConfigNodeUtil.ParseValue(configNode, "location", x => location = x, this, (BodyLocation?)null);
            valid &= ConfigNodeUtil.ParseValue(configNode, "experiment", x => experiment = x, this, new List<ScienceExperiment>(), x =>
                x.All(Validation.NotNull));
            valid &= ConfigNodeUtil.ParseValue(configNode, "fractionComplete", x => fractionComplete = x, this, 1d);

            valid &= ConfigNodeUtil.ParseValue(configNode, "subject", x => subjects = x, this, new List<ScienceSubject>());

            valid &= ConfigNodeUtil.MutuallyExclusive(configNode, new string[] { "subject" }, new string[] { "biome", "situation", "location", "experiment" }, this);

            // Validate subjects
            if (subjects != null && subjects.Count > 1)
            {
                Biome b = Util.Science.GetBiome(subjects[0]);
                ExperimentSituations es = Util.Science.GetSituation(subjects[0]);

                if (subjects.Any(s => !Util.Science.GetBiome(s).Equals(b)))
                {
                    LoggingUtil.LogError(this, "{0}: When using 'subject', the subjects must all have the same biome.", ErrorPrefix(configNode));
                    valid = false;
                }
                if (subjects.Any(s => !Util.Science.GetSituation(s).Equals(es)))
                {
                    LoggingUtil.LogError(this, "{0}: When using 'subject', the subjects must all have the same experiment situation.", ErrorPrefix(configNode));
                    valid = false;
                }
            }

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            if (subjects.Count > 0)
            {
                Biome b = Util.Science.GetBiome(subjects[0]);
                ExperimentSituations es = Util.Science.GetSituation(subjects[0]);

                return new RP1CollectScience(b == null ? targetBody : b.body, b == null ? "" : b.biome, es, location,
                    subjects.Select(s => Util.Science.GetExperiment(s).id).ToList(), fractionComplete, title);
            }
            else
            {
                return new RP1CollectScience(biome == null ? targetBody : biome.body, biome == null ? "" : biome.biome, situation, location,
                    experiment.Select(e => e.id).ToList(), fractionComplete, title);
            }
        }
    }
}
