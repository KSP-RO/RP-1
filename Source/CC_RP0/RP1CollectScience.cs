using ContractConfigurator.Parameters;
using ContractConfigurator.Util;
using Contracts;
using KSP.Localization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ContractConfigurator.RP0
{
    public class RP1CollectScience : ContractConfiguratorParameter
    {
        protected string biome { get; set; }
        protected ExperimentSituations? situation { get; set; }
        protected BodyLocation? location { get; set; }
        protected List<string> experiment { get; set; }
        protected double fractionComplete { get; set; }

        protected List<ScienceSubject> subjects;

        private float lastUpdate = 0.0f;
        private const float UPDATE_FREQUENCY = 2.5f;

        public RP1CollectScience()
            : base(null)
        {
            lastUpdate = UnityEngine.Time.fixedTime;
        }

        public RP1CollectScience(CelestialBody targetBody, string biome, ExperimentSituations? situation,
            BodyLocation? location, List<string> experiment, double fractionComplete, string title)
            : base(title)
        {
            lastUpdate = UnityEngine.Time.fixedTime;

            this.targetBody = targetBody;
            this.biome = biome;
            this.situation = situation;
            this.location = location;
            this.experiment = experiment;
            this.fractionComplete = fractionComplete;

            disableOnStateChange = true;

            LoadScienceSubjects();
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            if (targetBody != null)
            {
                node.AddValue("targetBody", targetBody.name);
            }

            if (!string.IsNullOrEmpty(biome))
            {
                node.AddValue("biome", biome);
            }

            if (situation != null)
            {
                node.AddValue("situation", situation);
            }

            if (location != null)
            {
                node.AddValue("location", location);
            }

            node.AddValue("fractionComplete", fractionComplete);

            foreach (string exp in experiment)
            {
                if (!string.IsNullOrEmpty(exp))
                {
                    node.AddValue("experiment", exp);
                }
            }
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            targetBody = ConfigNodeUtil.ParseValue(node, "targetBody", (CelestialBody)null);
            biome = ConfigNodeUtil.ParseValue(node, "biome", "").Replace(" ", "");
            situation = ConfigNodeUtil.ParseValue(node, "situation", (ExperimentSituations?)null);
            location = ConfigNodeUtil.ParseValue(node, "location", (BodyLocation?)null);
            experiment = ConfigNodeUtil.ParseValue(node, "experiment", new List<string>(0));
            fractionComplete = ConfigNodeUtil.ParseValue(node, "fractionComplete", 1d);

            if (State == ParameterState.Incomplete)
            {
                LoadScienceSubjects();
            }
        }

        protected override string GetParameterTitle()
        {
            string output;
            if (string.IsNullOrEmpty(title))
            {
                if (state != ParameterState.Incomplete)
                {
                    output = Localizer.GetStringByTag("#cc.param.CollectScience.0");
                }
                else
                {
                    string experimentStr = experiment.Count > 1 ? Localizer.GetStringByTag("#cc.science.experiment.many") : ExperimentName(experiment[0]);
                    string biomeStr = string.IsNullOrEmpty(biome) ? (targetBody?.displayName) : new Biome(targetBody, biome).ToString();
                    string situationStr = situation != null ? situation.Value.Print().ToLower() :
                        location != null ? Localizer.GetStringByTag(location.Value == BodyLocation.Surface ? "#cc.science.location.Surface" : "#cc.science.location.Space") : null;

                    if (biomeStr == null)
                    {
                        output = Localizer.Format("#cc.param.CollectScience.1", experimentStr);
                    }
                    else if (situationStr == null)
                    {
                        output = Localizer.Format("#cc.param.CollectScience.2", experimentStr, biomeStr);
                    }
                    else
                    {
                        output = Localizer.Format("#cc.param.CollectScience.3", experimentStr, biomeStr, situationStr);
                    }
                }
            }
            else
            {
                output = title;
            }
            return output;
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (UnityEngine.Time.fixedTime - lastUpdate > UPDATE_FREQUENCY)
            {
                lastUpdate = UnityEngine.Time.fixedTime;
                foreach (ScienceSubject subj in subjects)
                {
                    float curFraction = subj.science / subj.scienceCap;
                    if (curFraction >= fractionComplete)
                    {
                        SetState(ParameterState.Complete);
                    }
                }
            }
        }

        protected void LoadScienceSubjects()
        {
            subjects = new List<ScienceSubject>(experiment.Count);
            foreach (string exp in experiment)
            {
                ScienceExperiment se = ResearchAndDevelopment.GetExperiment(exp);

                if (situation.HasValue)
                {
                    subjects.Add(GetSubjectForSituation(se, situation.Value));
                }
                else
                {
                    if (location.Value == BodyLocation.Surface)
                    {
                        subjects.Add(GetSubjectForSituation(se, ExperimentSituations.SrfLanded));
                        subjects.Add(GetSubjectForSituation(se,ExperimentSituations.SrfSplashed));
                    }
                    else
                    {
                        subjects.Add(GetSubjectForSituation(se, ExperimentSituations.InSpaceLow));
                        subjects.Add(GetSubjectForSituation(se, ExperimentSituations.InSpaceHigh));
                    }
                }
            }
        }

        protected ScienceSubject GetSubjectForSituation(ScienceExperiment se, ExperimentSituations situation)
        {
            string biomeName = string.Empty;
            string biomeTitle = string.Empty;
            if (se.BiomeIsRelevantWhile(situation))
            {
                biomeName = biome;
                biomeTitle = ScienceUtil.GetBiomedisplayName(targetBody, biome);
            }

            return ResearchAndDevelopment.GetExperimentSubject(se, situation, targetBody, biomeName, biomeTitle);
        }

        protected string ExperimentName(string experiment)
        {
            if (string.IsNullOrEmpty(experiment))
            {
                return "Any experiment";
            }

            ScienceExperiment e = ResearchAndDevelopment.GetExperiment(experiment);
            if (e != null)
            {
                return e.experimentTitle;
            }

            string output = Regex.Replace(experiment, @"([A-Z]+?(?=[A-Z][^A-Z])|\B[A-Z]+?(?=[^A-Z]))", " $1");
            return output.Substring(0, 1).ToUpper() + output.Substring(1);
        }
    }
}
