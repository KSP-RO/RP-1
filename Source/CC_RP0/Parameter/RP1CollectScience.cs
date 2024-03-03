using ContractConfigurator.Parameters;
using ContractConfigurator.Util;
using Contracts;
using KSP.Localization;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UniLinq;

namespace ContractConfigurator.RP0
{
    public class RP1CollectScience : ContractConfiguratorParameter
    {
        protected string biome { get; set; }
        protected ExperimentSituations? situation { get; set; }
        protected BodyLocation? location { get; set; }
        protected List<string> experiment { get; set; }
        protected double fractionComplete { get; set; }
        protected double? fractionCompleteBiome { get; set; }
        protected int? minSubjectsToComplete { get; set; }
        protected float updateFrequency { get; set; }

        protected bool _expandedBiomes = false;
        protected List<ScienceSubject> subjects;
        protected Dictionary<string, string> paramIdToTitleDict = new Dictionary<string, string>();
        protected Dictionary<string, float> paramIdProgressDict = new Dictionary<string, float>();

        private float lastUpdate = 0.0f;
        internal const float DEFAULT_UPDATE_FREQUENCY = 2.5f;
        private const float FractionErrorMargin = 0.00025f;
        private const double MinFractionDiffForTitleUpdate = 0.001;

        public RP1CollectScience()
            : base(null)
        {
            lastUpdate = UnityEngine.Time.fixedTime;
        }

        public RP1CollectScience(CelestialBody targetBody, string biome, ExperimentSituations? situation,
            BodyLocation? location, List<string> experiment, double fractionComplete, int? minSubjectsToComplete,
            double? fractionCompleteBiome, string title, float updateFrequency)
            : base(title)
        {
            lastUpdate = UnityEngine.Time.fixedTime;

            this.targetBody = targetBody;
            this.biome = biome;
            this.situation = situation;
            this.location = location;
            this.experiment = experiment;
            this.fractionComplete = fractionComplete;
            this.minSubjectsToComplete = minSubjectsToComplete;
            this.fractionCompleteBiome = fractionCompleteBiome;
            this.updateFrequency = updateFrequency;

            disableOnStateChange = true;

            LoadScienceSubjects();
            CreateDelegates();
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            node.AddValue("updateFrequency", updateFrequency);

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
            if (fractionCompleteBiome.HasValue)
            {
                node.AddValue("fractionCompleteBiome", fractionCompleteBiome);
            }

            if (minSubjectsToComplete.HasValue)
            {
                node.AddValue("minSubjectsToComplete", minSubjectsToComplete);
            }

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
            try
            {
                updateFrequency = ConfigNodeUtil.ParseValue<float>(node, "updateFrequency", DEFAULT_UPDATE_FREQUENCY);
                targetBody = ConfigNodeUtil.ParseValue(node, "targetBody", (CelestialBody)null);
                biome = ConfigNodeUtil.ParseValue(node, "biome", "").Replace(" ", "");
                situation = ConfigNodeUtil.ParseValue(node, "situation", (ExperimentSituations?)null);
                location = ConfigNodeUtil.ParseValue(node, "location", (BodyLocation?)null);
                experiment = ConfigNodeUtil.ParseValue(node, "experiment", new List<string>(0));
                fractionComplete = ConfigNodeUtil.ParseValue(node, "fractionComplete", 1d);
                minSubjectsToComplete = ConfigNodeUtil.ParseValue(node, "minSubjectsToComplete", (int?)null);
                fractionCompleteBiome = ConfigNodeUtil.ParseValue(node, "fractionCompleteBiome", (double?)null);

                if (State == ParameterState.Incomplete)
                {
                    LoadScienceSubjects();
                    CreateDelegates();
                }
            }
            finally
            {
                ParameterDelegate<Vessel>.OnDelegateContainerLoad(node);
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
                    string fractionStr = $"{Math.Round(fractionComplete):P0} ";
                    string fracBiomeStr = fractionCompleteBiome.HasValue ? $" (each {Math.Round(fractionCompleteBiome.Value):P0} complete)" : string.Empty;

                    if (_expandedBiomes)
                    {
                        if (minSubjectsToComplete > 1)
                        {
                            output = $"Collect science: {fractionStr} of {experimentStr} from at least {minSubjectsToComplete} biomes{fracBiomeStr} while {situationStr}";
                        }
                        else
                        {
                            output = $"Collect science: {fractionStr} of {experimentStr} from any biome{fracBiomeStr} while {situationStr}";
                        }
                    }
                    else
                    {
                        if (biomeStr == null)
                        {
                            output = $"Collect science: {fractionStr} of {experimentStr}";
                        }
                        else if (situationStr == null)
                        {
                            output = $"Collect science: {fractionStr} of {experimentStr} from {biomeStr} {fracBiomeStr}";
                        }
                        else
                        {
                            output = $"Collect science: {fractionStr} of {experimentStr} from {biomeStr}{fracBiomeStr} while {situationStr}";
                        }
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

            if (UnityEngine.Time.fixedTime - lastUpdate > updateFrequency)
            {
                lastUpdate = UnityEngine.Time.fixedTime;
                int numToComplete = minSubjectsToComplete ?? 1;
                int completeSubjCount = 0;
                float totalFrac = 0f;
                foreach (ScienceSubject subj in subjects)
                {
                    float curFraction = subj.science / subj.scienceCap;
                    totalFrac += curFraction;
                    if (curFraction >= (fractionCompleteBiome.HasValue ? fractionCompleteBiome.Value : fractionComplete))
                        ++completeSubjCount;
                    
                }
                totalFrac /= subjects.Count;
                if (totalFrac >= fractionComplete && completeSubjCount >= numToComplete)
                {
                    SetState(ParameterState.Complete);
                }

                UpdateDelegates();
            }
        }

        protected void CreateDelegates()
        {
            if (subjects.Count > 1)
            {
                foreach (var subj in subjects)
                {
                    float curFraction = GetCompletedFraction(subj);
                    string title = ConstructDelegateTitle(subj, curFraction);
                    AddParameter(new ParameterDelegate<ScienceSubject>(title, s => false), id: subj.id);
                    paramIdProgressDict[subj.id] = curFraction;
                }
            }
        }

        protected void UpdateDelegates()
        {
            if (subjects.Count < 2) return;

            foreach (ContractParameter genericParam in this.GetAllDescendents())
            {
                var param = genericParam as ParameterDelegate<ScienceSubject>;
                if (param == null || param.State == ParameterState.Complete)
                {
                    continue;
                }

                string oldTitle = param.Title;
                ScienceSubject subj = subjects.FirstOrDefault(s => s.id == param.ID);
                if (subj != null)
                {
                    float curFraction = GetCompletedFraction(subj);
                    float prevFraction = paramIdProgressDict[subj.id];
                    if (curFraction - prevFraction > MinFractionDiffForTitleUpdate)
                    {
                        paramIdProgressDict[subj.id] = curFraction;
                        param.SetTitle(ConstructDelegateTitle(subj, curFraction));
                        ContractsWindow.SetParameterTitle(param, param.Title);
                    }

                    if (curFraction >= (fractionCompleteBiome.HasValue ? fractionCompleteBiome.Value : fractionComplete))
                    {
                        param.SetState(ParameterState.Complete);
                    }
                }
            }
        }

        protected string ConstructDelegateTitle(ScienceSubject subj, float curFraction)
        {
            string title = paramIdToTitleDict[subj.id];
            return $"{title}: {curFraction:P1}";
        }

        protected static float GetCompletedFraction(ScienceSubject subj)
        {
            float curFraction = subj.science / subj.scienceCap;
            curFraction += FractionErrorMargin;
            return curFraction;
        }

        protected void LoadScienceSubjects()
        {
            subjects = new List<ScienceSubject>(experiment.Count);
            foreach (string exp in experiment)
            {
                ScienceExperiment se = ResearchAndDevelopment.GetExperiment(exp);

                if (situation.HasValue)
                {
                    AddSubjectsForSituation(subjects, se, situation.Value);
                }
                else
                {
                    if (location.Value == BodyLocation.Surface)
                    {
                        AddSubjectsForSituation(subjects, se, ExperimentSituations.SrfLanded);
                        AddSubjectsForSituation(subjects, se, ExperimentSituations.SrfSplashed);
                    }
                    else
                    {
                        AddSubjectsForSituation(subjects, se, ExperimentSituations.InSpaceLow);
                        AddSubjectsForSituation(subjects, se, ExperimentSituations.InSpaceHigh);
                    }
                }
            }
        }

        protected void AddSubjectsForSituation(List<ScienceSubject> subjects, ScienceExperiment se, ExperimentSituations situation)
        {
            string biomeName = string.Empty;
            string biomeTitle = string.Empty;
            bool hasBiomes = se.BiomeIsRelevantWhile(situation);
            if (hasBiomes && string.IsNullOrEmpty(biome))
            {
                List<string> allBiomes = ResearchAndDevelopment.GetBiomeTags(targetBody, false);
                foreach (string biomeName2 in allBiomes)
                {
                    string biomeTitle2 = ScienceUtil.GetBiomedisplayName(targetBody, biomeName2);
                    var subj2 = ResearchAndDevelopment.GetExperimentSubject(se, situation, targetBody, biomeName2, biomeTitle2);
                    paramIdToTitleDict[subj2.id] = biomeTitle2;
                    subjects.Add(subj2);
                }
                _expandedBiomes = true;
                return;
            }

            if (hasBiomes)
            {
                biomeName = biome;
                biomeTitle = ScienceUtil.GetBiomedisplayName(targetBody, biome);
            }

            var subj = ResearchAndDevelopment.GetExperimentSubject(se, situation, targetBody, biomeName, biomeTitle);
            paramIdToTitleDict[subj.id] = biomeTitle;
            subjects.Add(subj);
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
