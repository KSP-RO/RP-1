using System.Collections;
using System.Collections.Generic;
using static RP0.Programs.Program;

namespace RP0.ConfigurableStart
{
    public class Scenario
    {
        private string _scenarioName = null;
        private string _description = null;
        private string _startingDate = null;
        private string _unlockedTechs = null;
        private bool? _unlockPartsInParentNodes = null;
        private bool? _unlockPartUpgrades = null;
        private string _partUnlockFilters = null;
        private string _facilityUpgrades = null;
        private int? _applicants = null;
        private string _tfStartingDU = null;
        private string _rfUnlockedConfigs = null;
        private List<Program> _completedPrograms = null;
        private List<Program> _acceptedPrograms = null;
        private string _completedContracts = null;
        private string _acceptedContracts = null;
        private float? _startingFunds = null;
        private float? _startingScience = null;
        private float? _sciEarned = null;
        private float? _startingRep = null;
        private float? _startingConfidence = null;
        private float? _unlockCredit = null;
        private Dictionary<string, Dictionary<string, HashSet<ExperimentSituations>>> _completedExperiments;
        private List<LCData> _lcs = null;

        public string ScenarioName { get => _scenarioName; set => _scenarioName = value; }
        public string Description { get => _description; set => _description = value; }
        public string StartingDate { get => _startingDate; set => _startingDate = value; }
        public string UnlockedTechs { get => _unlockedTechs; set => _unlockedTechs = value; }
        public bool UnlockPartsInParentNodes { get => _unlockPartsInParentNodes ?? true; set => _unlockPartsInParentNodes = value; }
        public bool? UnlockPartUpgrades { get => _unlockPartUpgrades; set => _unlockPartUpgrades = value; }
        public string PartUnlockFilters { get => _partUnlockFilters; set => _partUnlockFilters = value; }
        public string FacilityUpgrades { get => _facilityUpgrades; set => _facilityUpgrades = value; }
        public int? Applicants { get => _applicants; set => _applicants = value; }
        public string TFStartingDU { get => _tfStartingDU; set => _tfStartingDU = value; }
        public string RFUnlockedConfigs { get => _rfUnlockedConfigs; set => _rfUnlockedConfigs = value; }
        public List<Program> CompletedPrograms { get => _completedPrograms; set => _completedPrograms = value; }
        public List<Program> AcceptedPrograms { get => _acceptedPrograms; set => _acceptedPrograms = value; }
        public string CompletedContracts { get => _completedContracts; set => _completedContracts = value; }
        public string AcceptedContracts { get => _acceptedContracts; set => _acceptedContracts = value; }
        public float? StartingFunds { get => _startingFunds; set => _startingFunds = value; }
        public float? StartingScience { get => _startingScience; set => _startingScience = value; }
        public float? SciEarned { get => _sciEarned; set => _sciEarned = value; }
        public float? StartingRep { get => _startingRep; set => _startingRep = value; }
        public float? StartingConfidence { get => _startingConfidence; set => _startingConfidence = value; }
        public float? UnlockCredit { get => _unlockCredit; set => _unlockCredit = value; }
        public Dictionary<string, Dictionary<string, HashSet<ExperimentSituations>>> CompletedExperiments { get => _completedExperiments; set => _completedExperiments = value; }
        public List<LCData> LCs { get => _lcs; set => _lcs = value; }
        public long StartingUT => string.IsNullOrEmpty(StartingDate) ? 0 : DateHandler.GetUTFromDate(StartingDate.Trim());

        public Scenario(string name)
        {
            _scenarioName = name;
        }

        public Scenario(ConfigNode node)
        {
            node.CSTryGetValue("name", out _scenarioName);
            node.CSTryGetValue("description", out _description);
            node.CSTryGetValue("startingDate", out _startingDate);
            node.CSTryGetValue("unlockedTechs", out _unlockedTechs);
            node.CSTryGetValue("unlockPartsInParentNodes", out _unlockPartsInParentNodes);
            node.CSTryGetValue("unlockPartUpgrades", out _unlockPartUpgrades);
            node.CSTryGetValue("partUnlockFilters", out _partUnlockFilters);
            node.CSTryGetValue("facilities", out _facilityUpgrades);
            node.CSTryGetValue("applicants", out _applicants);
            node.CSTryGetValue("tfStartingDU", out _tfStartingDU);
            node.CSTryGetValue("rfUnlockedConfigs", out _rfUnlockedConfigs);
            node.CSTryGetValue("completedContracts", out _completedContracts);
            node.CSTryGetValue("acceptedContracts", out _acceptedContracts);
            node.CSTryGetValue("startingRep", out _startingRep);
            node.CSTryGetValue("startingConfidence", out _startingConfidence);
            node.CSTryGetValue("startingScience", out _startingScience);
            node.CSTryGetValue("sciEarned", out _sciEarned);
            node.CSTryGetValue("startingFunds", out _startingFunds);
            node.CSTryGetValue("unlockCredit", out _unlockCredit);

            _acceptedPrograms = new List<Program>();
            _completedPrograms = new List<Program>();
            var cProgNodes = node.GetNodes("COMPLETED_PROGRAM");
            foreach (ConfigNode cn in cProgNodes)
            {
                _completedPrograms.Add(new Program(cn));
            }

            var aProgNodes = node.GetNodes("ACCEPTED_PROGRAM");
            foreach (ConfigNode cn in aProgNodes)
            {
                _acceptedPrograms.Add(new Program(cn));
            }

            _completedExperiments = new Dictionary<string, Dictionary<string, HashSet<ExperimentSituations>>>();
            var expNodes = node.GetNodes("COMPLETED_EXPERIMENTS");
            foreach (ConfigNode cn in expNodes)
            {
                IEnumerator e = KCTDataLoader.LoadExperiments(cn, _completedExperiments);
                while (e.MoveNext()) { }
            }

            _lcs = new List<LCData>();
            var lcNodes = node.GetNodes("LCData");
            foreach (ConfigNode cn in lcNodes)
            {
                var lc = new LCData();
                lc.Load(cn);
                _lcs.Add(lc);
            }
        }
    }

    public class Program : IConfigNode
    {
        [Persistent]
        public string name;

        [Persistent]
        public string accepted;

        [Persistent]
        public string objectivesCompleted;

        [Persistent]
        public string completed;

        [Persistent]
        public Speed speed = Speed.Normal;

        public bool HasAcceptedValue => !string.IsNullOrEmpty(accepted);
        public bool HasObjectivesCompletedValue => !string.IsNullOrEmpty(objectivesCompleted);
        public bool HasCompletedValue => !string.IsNullOrEmpty(completed);
        public double AcceptedUT => DateHandler.GetUTFromDate(accepted.Trim());
        public double ObjectivesCompletedUT => DateHandler.GetUTFromDate(objectivesCompleted.Trim());
        public double CompletedUT => DateHandler.GetUTFromDate(completed.Trim());

        public Program(ConfigNode node)
        {
            Load(node);
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
