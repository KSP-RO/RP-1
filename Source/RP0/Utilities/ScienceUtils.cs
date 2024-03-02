using System.Collections.Generic;

namespace RP0
{
    public static class ScienceUtils
    {
        public static void MarkExperimentsAsDone(Dictionary<string, Dictionary<string, HashSet<ExperimentSituations>>> bodyExperimentDict)
        {
            foreach (var bodyKVP in bodyExperimentDict)
                foreach (var expKVP in bodyKVP.Value)
                    foreach (var sit in expKVP.Value)
                        MarkExperimentAsDone(expKVP.Key, sit, bodyKVP.Key);
        }

        public static void MarkExperimentAsDone(string experimentID, ExperimentSituations situation, string bodyName)
        {
            ScienceExperiment experiment = ResearchAndDevelopment.GetExperiment(experimentID);
            if (experiment == null)
            {
                RP0Debug.LogError($"MarkExperimentAsDone: Invalid experiment {experimentID}");
                return;
            }
            CelestialBody body = FlightGlobals.GetBodyByName(bodyName);

            if (experiment.BiomeIsRelevantWhile(situation))
            {
                List<string> allBiomes = ResearchAndDevelopment.GetBiomeTags(body, false);
                foreach (string biomeName in allBiomes)
                {
                    string biomeTitle = ScienceUtil.GetBiomedisplayName(body, biomeName);
                    MarkAsDone(experiment, situation, body, biomeName, biomeTitle);
                }
            }
            else
            {
                MarkAsDone(experiment, situation, body, string.Empty, string.Empty);
            }
        }

        private static void MarkAsDone(ScienceExperiment experiment, ExperimentSituations situation, CelestialBody body, string biomeName, string biomeTitle)
        {
            ScienceSubject subj = ResearchAndDevelopment.GetExperimentSubject(experiment, situation, body, biomeName, biomeTitle);
            subj.scientificValue = 0;
            subj.science = subj.scienceCap;
        }
    }
}
