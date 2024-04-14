using ContractConfigurator;
using ROUtils;
using RP0.Programs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UniLinq;
using UnityEngine;
using UnityEngine.UI;
using Upgradeables;

namespace RP0.ConfigurableStart
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class ScenarioHandler : MonoBehaviour
    {
        public const string EmptyScenarioName = "None";

        private string _curScenarioName;
        private readonly Dictionary<string, ConfigNode> _contractNodes = new Dictionary<string, ConfigNode>();    // to cache the contract nodes
        private uint _runningContractCoroutines = 0;
        private bool _contractsIterated = false;
        private TextMeshProUGUI _btnText;

        public static ScenarioHandler Instance { get; private set; }
        public Dictionary<string, Scenario> LoadedScenarios { get; private set; }
        public bool ContractsInitialized => _runningContractCoroutines == 0 && _contractsIterated;
        public Scenario CurrentScenario
        {
            get
            {
                if (LoadedScenarios != null && _curScenarioName != null)
                    return LoadedScenarios[_curScenarioName];
                
                return null;
            }
            set
            {
                _curScenarioName = value.ScenarioName;
                Instance?.UpdateUI();
            }
        }

        public void SetCurrentScenarioFromName(string name)
        {
            if (!string.IsNullOrEmpty(name) && LoadedScenarios.ContainsKey(name))
            {
                _curScenarioName = name;
                Instance?.UpdateUI();
            }
        }

        internal void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;
        }

        internal void Start()
        {
            RP0Debug.Log("Start called");

            // don't destroy on scene switch
            DontDestroyOnLoad(this);

            GameEvents.onGameNewStart.Add(OnGameNewStart);

            LoadedScenarios = Database.CustomScenarios;
            _curScenarioName = EmptyScenarioName;
            PresetPickerGUI.Instance?.Setup(LoadedScenarios.Keys.ToArray());

            RP0Debug.Log("Start finished");
        }

        internal void OnDestroy()
        {
            GameEvents.onGameNewStart.Remove(OnGameNewStart);
            if (Instance == this)
                Instance = null;
        }
        
        public void OnGameNewStart()
        {
            PresetPickerGUI.Instance?.SetVisible(false);
            if (CurrentScenario == null || CurrentScenario.ScenarioName == EmptyScenarioName) return;

            switch (HighLogic.CurrentGame.Mode)
            {
                case Game.Modes.CAREER:
                    RP0Debug.Log("Career Detected");
                    ApplyScenarioToCareer(CurrentScenario);
                    break;
                case Game.Modes.SANDBOX:
                    RP0Debug.Log("Sandbox Detected");
                    ApplyScenarioToSandbox(CurrentScenario);
                    break;
                case Game.Modes.SCIENCE_SANDBOX:
                    RP0Debug.Log("Science Mode Detected");
                    ApplyScenarioToSandbox(CurrentScenario);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Validates the scenario and applies the date, if defined
        /// </summary>
        /// <param name="scn"></param>
        public void ApplyScenarioToSandbox(Scenario scn)
        {
            if (scn == null)
            {
                RP0Debug.LogWarning($"Selected Scenario doesn't exist, destroying");
                Destroy(this);
            }
            else
            {
                RP0Debug.Log($"Applying date from scenario {scn.ScenarioName}");
                
                if (scn.StartingUT != 0)
                {
                    SetDate(scn.StartingUT);
                }
            }
        }

        /// <summary>
        /// Validates the scenario and applies every defined field
        /// </summary>
        /// <param name="scn"></param>
        public void ApplyScenarioToCareer(Scenario scn)
        {
            if (scn == null)
            {
                RP0Debug.LogWarning($"Selected Scenario doesn't exist, destroying");
                Destroy(this);
            }
            else
            {
                RP0Debug.LogWarning($"Applying scenario {scn.ScenarioName}");
                StartCoroutine(CheckKSPIntializationAndSetParameters(scn));
            }
        }

        private IEnumerator CheckKSPIntializationAndSetParameters(Scenario scn)
        {
            // make sure that everything has initialized properly first
            yield return WaitForInitialization(() => ScenarioUpgradeableFacilities.Instance);
            yield return WaitForInitialization(() => ResearchAndDevelopment.Instance);
            yield return WaitForInitialization(() => Reputation.Instance);
            yield return WaitForInitialization(() => Funding.Instance);
            yield return WaitForInitialization(() => Confidence.Instance);
            yield return WaitForInitialization(() => UnlockCreditHandler.Instance);
            yield return WaitForInitialization(() => PartLoader.Instance);
            yield return WaitForInitialization(() => Contracts.ContractSystem.Instance);
            yield return WaitForInitialization(() => MaintenanceHandler.Instance);
            yield return WaitForInitialization(() => ProgramHandler.Instance);
            if (TFInterop.IsTestFlightInstalled) yield return WaitForInitialization(() => TFInterop.ManagerScenarioInstance);

            // just to be even safer
            yield return new WaitForEndOfFrame();

            try
            {
                if (scn.StartingUT != 0)
                {
                    SetDate(scn.StartingUT);
                }

                var allProgramsToAccept = scn.AcceptedPrograms.Concat(scn.CompletedPrograms);
                foreach (Program p in allProgramsToAccept)
                {
                    var newProgram = ProgramHandler.Instance.ActivateProgram(p.name, p.speed);
                    ApplyProgramParameters(p, newProgram);
                }

                foreach (Program p in scn.CompletedPrograms)
                {
                    var complProgram = ProgramHandler.Instance.CompleteProgram(p.name);
                    ApplyProgramParameters(p, complProgram);
                }

                if (!string.IsNullOrEmpty(scn.CompletedContracts))
                {
                    string[] contractNames = Utilities.ArrayFromCommaSeparatedList(scn.CompletedContracts);
                    HandleContracts(contractNames, complete: true);
                }

                if (!string.IsNullOrEmpty(scn.AcceptedContracts))
                {
                    string[] contractNames = Utilities.ArrayFromCommaSeparatedList(scn.AcceptedContracts);
                    HandleContracts(contractNames, complete: false);
                }
                _contractsIterated = true;

                if (!string.IsNullOrEmpty(scn.UnlockedTechs))
                {
                    Dictionary<string, bool> techIDs = Utilities.DictionaryFromCommaSeparatedString(scn.UnlockedTechs, defaultValue: false);

                    Dictionary<string, string> unlockFilters;
                    unlockFilters = string.IsNullOrEmpty(scn.PartUnlockFilters) ?
                        null : Utilities.DictionaryFromCommaSeparatedString<string>(scn.PartUnlockFilters, defaultValue: null);

                    UnlockTechnologies(techIDs, unlockFilters, scn.UnlockPartUpgrades, scn.UnlockPartsInParentNodes);
                }

                if (!string.IsNullOrEmpty(scn.FacilityUpgrades))
                {
                    Dictionary<string, int> facilities = Utilities.DictionaryFromCommaSeparatedString<int>(scn.FacilityUpgrades);
                    SetFacilityLevels(facilities);
                }

                if (scn.Applicants != null)
                {
                    SpaceCenterManagement.Instance.Applicants = scn.Applicants.Value;
                }

                if (!string.IsNullOrEmpty(scn.RFUnlockedConfigs))
                {
                    string[] configs = Utilities.ArrayFromCommaSeparatedList(scn.RFUnlockedConfigs);
                    foreach (string config in configs)
                    {
                        RealFuels.EntryCostDatabase.SetUnlocked(config);
                        RP0Debug.Log($"Unlocked {config} engine config");
                    }
                }

                if (scn.CompletedExperiments?.Count > 0)
                {
                    ScienceUtils.MarkExperimentsAsDone(scn.CompletedExperiments);
                }

                if (TFInterop.IsTestFlightInstalled && !string.IsNullOrEmpty(scn.TFStartingDU))
                {
                    Dictionary<string, float> engines = Utilities.DictionaryFromCommaSeparatedString<float>(scn.TFStartingDU);
                    TFInterop.SetFlightDataForParts(engines);
                }

                foreach (LCData lcData in scn.LCs)
                {
                    var lc = new LaunchComplex(lcData, SpaceCenterManagement.Instance.ActiveSC);
                    lc.IsOperational = true;
                    SpaceCenterManagement.Instance.ActiveSC.LaunchComplexes.Add(lc);
                }

                if (scn.StartingRep != null)
                {
                    SetReputation(scn.StartingRep.GetValueOrDefault(HighLogic.CurrentGame.Parameters.Career.StartingReputation));
                }

                if (scn.StartingScience != null)
                {
                    SetScience(scn.StartingScience.GetValueOrDefault(HighLogic.CurrentGame.Parameters.Career.StartingScience));
                }

                if (scn.SciEarned != null)
                {
                    SpaceCenterManagement.Instance.SciPointsTotal = scn.SciEarned.Value;
                }

                if (scn.StartingFunds != null)
                {
                    SetFunds(scn.StartingFunds.GetValueOrDefault(HighLogic.CurrentGame.Parameters.Career.StartingFunds));
                }

                if (scn.UnlockCredit != null)
                {
                    UnlockCreditHandler.Instance.SetCredit(scn.UnlockCredit.Value);
                }

                if (scn.StartingConfidence != null)
                {
                    Confidence.Instance.SetConfidence(scn.StartingConfidence.Value);
                }

                StartCoroutine(CompleteScenarioInitialization());
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                ShowScenarioFailedToApplyMessage();
            }
            
            yield break;
        }

        private IEnumerator CompleteScenarioInitialization()
        {
            while (!ContractsInitialized)
                yield return new WaitForFixedUpdate();

            RP0Debug.Log("Scenario applied");
            RP0Debug.Log("Destroying ScenarioLoader...");
            Destroy(this);
        }

        /// <summary>
        /// Coroutine that waits until the obj parameter has initialized before breaking.
        /// </summary>
        /// <param name="obj">The object to check</param>
        /// <returns></returns>
        private static IEnumerator WaitForInitialization(Func<object> obj)
        {
            while (obj() == null)
            {
                yield return new WaitForFixedUpdate();
            }
            RP0Debug.Log($"{obj().GetType().Name} initialized");
        }

        private static void ApplyProgramParameters(Program pParams, Programs.Program program)
        {
            if (pParams.HasAcceptedValue)
                program.acceptedUT = pParams.AcceptedUT;

            if (pParams.HasObjectivesCompletedValue)
                program.objectivesCompletedUT = pParams.ObjectivesCompletedUT;

            if (pParams.HasCompletedValue)
                program.completedUT = pParams.CompletedUT;

            double progDurSec = program.DurationYears * (365.25d * 86400d);
            double endDate = program.IsComplete ? program.completedUT : Planetarium.GetUniversalTime();
            program.fracElapsed = (endDate - program.acceptedUT) / progDurSec;
            program.fundsPaidOut = program.GetFundsForFutureTimestamp(Planetarium.GetUniversalTime());
        }

        /// <summary>
        /// Set the starting date.
        /// </summary>
        /// <param name="newUT"> The time in seconds before or after Epoch 0</param>
        public void SetDate(long newUT)
        {
            Planetarium.SetUniversalTime(newUT);
            RP0Debug.Log($"Set UT: {newUT}");
            ResetLastMaintenanceUpdate(newUT);
        }

        /// <summary>
        /// Set the funds available to the player. Needs to be done as the last thing so none of the other methods affect it.
        /// </summary>
        /// <param name="funds">How much money should the player have</param>
        public void SetFunds(double funds)
        {
            Funding.Instance.SetFunds(funds, TransactionReasons.Progression);
            RP0Debug.Log($"Set funds: {funds}");
        }

        /// <summary>
        /// Set the Science amount the player has.
        /// </summary>
        /// <param name="science">How much science the player should have</param>
        public void SetScience(float science)
        {
            ResearchAndDevelopment.Instance.SetScience(science, TransactionReasons.Progression);
            RP0Debug.Log($"Set science: {science}");
        }

        /// <summary>
        /// Set the amount of Reputation the player has
        /// TODO: investigate if it works as intended
        /// </summary>
        /// <param name="rep">How much reputation the player should have</param>
        public void SetReputation(float rep)
        {
            Reputation.Instance.SetReputation(rep, TransactionReasons.Progression);
            RP0Debug.Log($"Set reputation: {rep}");
        }

        /// <summary>
        /// Set the level of each facility specified in the config.
        /// </summary>
        /// <param name="facilityKeyValuePairs"></param>
        public void SetFacilityLevels(Dictionary<string, int> facilityKeyValuePairs)
        {
            foreach (var facility in facilityKeyValuePairs.Keys)
            {
                int level = facilityKeyValuePairs[facility] - 1;
                SetFacilityLevel(facility, level);
            }
        }

        /// <summary>
        /// Set the level of a facility.
        /// </summary>
        /// <param name="id"> the id of the facility</param>
        /// <param name="level"> the new level of the facility</param>
        public void SetFacilityLevel(string id, int level)
        {
            if (id == null) return;

            id = id.ToUpper() switch
            {
                "VAB" => "SpaceCenter/VehicleAssemblyBuilding",
                "SPH" => "SpaceCenter/SpaceplaneHangar",
                "RUNWAY" => "SpaceCenter/Runway",
                "R&D" => "SpaceCenter/ResearchAndDevelopment",
                "RD" => "SpaceCenter/ResearchAndDevelopment",
                "RESEARCH" => "SpaceCenter/ResearchAndDevelopment",
                "ASTRONAUT" => "SpaceCenter/AstronautComplex",
                "TRACKING" => "SpaceCenter/TrackingStation",
                "MISSION" => "SpaceCenter/MissionControl",
                "PAD" => "SpaceCenter/LaunchPad",
                "LAUNCHPAD" => "SpaceCenter/LaunchPad",
                "ADMIN" => "SpaceCenter/Administration",
                _ => "SpaceCenter/" + id,
            };

            foreach (UpgradeableFacility facility in FindObjectsOfType<UpgradeableFacility>())
            {
                if (facility.id == id)
                {
                    level = Mathf.Clamp(level, 0, facility.MaxLevel);
                    facility.SetLevel(level);
                    RP0Debug.Log($"Upgraded {facility.name} to level {++level}");
                    break;
                }
            }
        }

        /// <summary>
        /// Iterates through each tech that is defined in the Config and calls UnlockTech()
        /// </summary>
        /// <param name="techIDs"></param>
        public void UnlockTechnologies(Dictionary<string, bool> techIDs, Dictionary<string, string> unlockFilters, bool? unlockPartUpgrades, bool unlockPartsInParents)
        {
            var researchedNodes = new List<ProtoRDNode>();

            AssetBase.RnDTechTree.ReLoad();
            foreach (string tech in techIDs.Keys)
            {
                UnlockTechFromTechID(tech, researchedNodes, techIDs[tech], unlockPartsInParents, unlockPartUpgrades, false, unlockFilters);
            }
        }

        /// <summary>
        /// Find a ProtoRDNode from its techID and unlocks it, along with all its parents.
        /// </summary>
        /// <param name="techID"></param>
        public void UnlockTechFromTechID(string techID, List<ProtoRDNode> researchedNodes, bool unlockParts, bool unlockPartsInParents, bool? unlockPartUpgrades, bool isRecursive, Dictionary<string, string> unlockFilters)
        {
            if (string.IsNullOrEmpty(techID)) return;

            //for some reason, FindNodeByID is not a static method and you need a reference
            List<ProtoRDNode> rdNodes = AssetBase.RnDTechTree.GetTreeNodes().ToList();
            if (rdNodes[0].FindNodeByID(techID, rdNodes) is ProtoRDNode rdNode)
            {
                UnlockTechWithParents(rdNode, researchedNodes, unlockParts, unlockPartsInParents, unlockPartUpgrades, isRecursive, unlockFilters);
            }
            else
                RP0Debug.LogWarning($"{techID} node not found");
        }

        /// <summary>
        /// Unlock a technology and all of its parents.
        /// </summary>
        /// <param name="protoRDNode"></param>
        public void UnlockTechWithParents(ProtoRDNode protoRDNode, List<ProtoRDNode> researchedNodes, bool unlockParts, bool unlockPartsInParents, bool? unlockPartUpgrades, bool isRecursive, Dictionary<string, string> partUnlockFilters)
        {
            foreach (var parentNode in protoRDNode.parents ?? Enumerable.Empty<ProtoRDNode>())
            {
                if (!researchedNodes.Contains(parentNode))
                    UnlockTechWithParents(parentNode, researchedNodes, unlockParts, unlockPartsInParents, unlockPartUpgrades, true, partUnlockFilters);
            }

            bool b = unlockParts && (!isRecursive || (isRecursive && unlockPartsInParents));
            UnlockTech(protoRDNode.tech, b, unlockPartUpgrades, partUnlockFilters);
            researchedNodes.Add(protoRDNode);
        }

        /// <summary>
        /// Unlock a technology and all of the parts inside of the node. Includes handling for Part Unlock Costs. Code derived from Contract Configurator.
        /// Unlock parts if
        /// </summary>
        /// <param name="ptn"> The node to unlock</param>
        /// <param name="unlockFilters"> The dict of fields to either unlock or non-unlock parts from the node, according to unlockFilters.</param>
        /// <param name="unlockParts"> The bool that dictates the default unlock behaviour. If true, unlock all parts except those that match unlockFilters
        /// <param name="unlockPartUpgrades"> Whether to unlock part upgrades in the tech node</param>
        /// If false, only unlock the parts that match unlockFilters. </param>
        public void UnlockTech(ProtoTechNode ptn, bool unlockParts, bool? unlockPartUpgrades, Dictionary<string, string> unlockFilters)
        {
            ptn.state = RDTech.State.Available;
            string techID = ptn.techID;

            if (!HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
            {
                ptn.partsPurchased = MatchingParts(techID, unlockParts, unlockFilters);
            }
            else
            {
                ptn.partsPurchased = new List<AvailablePart>();
            }

            ResearchAndDevelopment.Instance.SetTechState(techID, ptn);
            RP0Debug.Log($"Unlocked tech: {techID}");
            
            if (unlockPartUpgrades ?? unlockParts)
            {
                var upgrades = PartUpgradeManager.Handler.GetUpgradesForTech(techID);
                foreach (var up in upgrades)
                {
                    PartUpgradeManager.Handler.SetUnlocked(up.name, true);
                }
            }
        }

        /// <summary>
        /// Unlock the parts contained in a tech node, blacklisting or whitelisting them based on input arguments.
        /// </summary>
        /// <param name="techID"> The techID of the node containing the parts.</param>
        /// <param name="defaultUnlockParts"> Whether the default behaviour is to buy or not buy parts.</param>
        /// <param name="unlockFilters"> The dictionary&lt;string, string&gt; of field,value kvp that either selects the parts to buy or to not buy,
        /// depending on default behaviour.</param>
        /// <returns></returns>
        public List<AvailablePart> MatchingParts(string techID, bool defaultUnlockParts, Dictionary<string, string> unlockFilters)
        {
            var parts = new List<AvailablePart>();
            var apType = typeof(AvailablePart);
            unlockFilters ??= new Dictionary<string, string>(0);

            if (defaultUnlockParts)
            {
                parts = PartLoader.Instance.loadedParts.Where(p => p.TechRequired == techID).ToList();

                foreach (var filterName in unlockFilters.Keys)
                {
                    string fieldValue = unlockFilters[filterName];

                    if (fieldValue != null)
                    {
                        if (filterName == "tags")
                            parts.RemoveAll(p => (apType.GetField(filterName)?.GetValue(p)
                            .ToString()
                            .Contains(fieldValue.ToLower())) ?? false);
                        else
                            parts.RemoveAll(p => (apType.GetField(filterName)?.GetValue(p)
                            .ToString()
                            .Contains(fieldValue)) ?? false);
                    }
                    else
                        parts.RemoveAll(p => p.partConfig.HasNode(filterName));
                }
            }
            else
            {
                foreach (var filterName in unlockFilters.Keys)
                {
                    string fieldValue = unlockFilters[filterName];

                    if (fieldValue != null)
                    {
                        if (filterName == "tags")
                            parts.AddRange(PartLoader.Instance.loadedParts
                            .Where(p => p.TechRequired == techID)
                            .Where(p => apType.GetField(filterName)?.GetValue(p).ToString()
                            .Contains(fieldValue.ToLower()) ?? false));
                        else
                            parts.AddRange(PartLoader.Instance.loadedParts
                            .Where(p => p.TechRequired == techID)
                            .Where(p => apType.GetField(filterName)?.GetValue(p).ToString()
                            .Contains(fieldValue) ?? false));
                    }
                    else
                        parts.AddRange(PartLoader.Instance.loadedParts
                            .Where(p => p.TechRequired == techID)
                            .Where(p => p.partConfig.HasNode(filterName)));
                }
            }

            return parts;
        }

        /// <summary>
        /// Generates and accepts/completes an array of ContractConfigurator contracts
        /// </summary>
        /// <param name="names"> Array of contract names that will be completed.</param>
        /// <param name="complete"> Whether to complete or just accept the contracts.</param>
        public void HandleContracts(string[] names, bool complete)
        {
            List<ContractType> contractTypes = ContractType.AllValidContractTypes.ToList();

            foreach (var subType in contractTypes)
            {
                foreach (string contractName in names)
                {
                    if (contractName == subType.name)
                    {
                        var contract = ForceGenerate(subType, 0, new System.Random().Next(), Contracts.Contract.State.Generated);

                        if (contract is null)
                        {
                            RP0Debug.LogWarning($"Couldn't complete contract {contractName}");
                            continue;
                        }

                        StartCoroutine(HandleContractCoroutine(contract, complete));
                        RP0Debug.Log($"{(complete ? "Completed" : "Accepted" )} contract {contractName}");
                    }
                }
            }
        }

        private static ConfiguredContract ForceGenerate(ContractType contractType, Contracts.Contract.ContractPrestige difficulty, int seed, Contracts.Contract.State state)
        {
            var contract = (ConfiguredContract)Activator.CreateInstance(typeof(ConfiguredContract));

            Type baseT = contract.GetType().BaseType;
            FieldInfo[] fields = baseT.GetFields(BindingFlags.FlattenHierarchy
                | BindingFlags.Instance
                | BindingFlags.NonPublic
                | BindingFlags.Public);

            // generate and set guid
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(Guid))
                {
                    f.SetValue(contract, Guid.NewGuid());
                    break;
                }
            }
            // set necessary base contract fields
            if (baseT.GetField("prestige", BindingFlags.NonPublic | BindingFlags.Instance) is FieldInfo fi)
                fi.SetValue(contract, difficulty);
            if ((fi = baseT.GetField("state", BindingFlags.NonPublic | BindingFlags.Instance)) is FieldInfo)
                fi.SetValue(contract, state);
            if ((fi = baseT.GetField("seed", BindingFlags.NonPublic | BindingFlags.Instance)) is FieldInfo)
                fi.SetValue(contract, seed);
            if ((fi = baseT.GetField("agent", BindingFlags.NonPublic | BindingFlags.Instance)) is FieldInfo)
                fi.SetValue(contract, Contracts.Agents.AgentList.Instance.GetSuitableAgentForContract(contract));
            contract.FundsFailure = Math.Max(contract.FundsFailure, contract.FundsAdvance);
            contract.GetType().GetMethod("SetupID", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(contract, null);

            // set CC contract subtype
            contract.contractType = contractType;
            contract.subType = contractType.name;

            // Copy text from contract type
            Type t = contract.GetType();
            if ((fi = t.GetField("title", BindingFlags.NonPublic | BindingFlags.Instance)) is FieldInfo)
                fi.SetValue(contract, contractType.title);
            if ((fi = t.GetField("synopsis", BindingFlags.NonPublic | BindingFlags.Instance)) is FieldInfo)
                fi.SetValue(contract, contractType.synopsis);
            if ((fi = t.GetField("completedMessage", BindingFlags.NonPublic | BindingFlags.Instance)) is FieldInfo)
                fi.SetValue(contract, contractType.completedMessage);
            if ((fi = t.GetField("notes", BindingFlags.NonPublic | BindingFlags.Instance)) is FieldInfo)
                fi.SetValue(contract, contractType.notes);

            // set expiry and deadline type to none
            if ((fi = t.GetField("expiryType", BindingFlags.NonPublic | BindingFlags.Instance)) is FieldInfo)
                fi.SetValue(contract, Contracts.Contract.DeadlineType.Floating);
            if ((fi = t.GetField("deadlineType", BindingFlags.NonPublic | BindingFlags.Instance)) is FieldInfo)
                fi.SetValue(contract, Contracts.Contract.DeadlineType.Floating);

            contract.TimeDeadline = contractType.deadline;

            return contract;
        }

        private IEnumerator HandleContractCoroutine(ConfiguredContract c, bool complete)
        {
            var ignoreCareerEventsScope = new CareerEventScope(CareerEventType.Ignore);
            _runningContractCoroutines++;
            
            // cache contract nodes
            if (_contractNodes.Count == 0)
            {
                var cfgNodes = GameDatabase.Instance.GetConfigNodes("CONTRACT_TYPE");

                foreach (var node in cfgNodes)
                {
                    if (node.GetValue("name") is string subT)
                        _contractNodes[subT] = node;
                }
            }

            // load behaviours so that they're correctly fired
            if (_contractNodes.ContainsKey(c.subType))
            {
                ConfigNode cfgNode = _contractNodes[c.subType];

                if (cfgNode.GetNodes("BEHAVIOUR") is var bNodes)
                {
                    var behaviourFactories = new List<BehaviourFactory>();

                    foreach (var bNode in bNodes)
                    {
                        BehaviourFactory.GenerateBehaviourFactory(bNode, c.contractType, out var behaviourFactory);
                        if (behaviourFactory != null)
                        {
                            behaviourFactories.Add(behaviourFactory);
                        }
                    }

                    if (BehaviourFactory.GenerateBehaviours(c, behaviourFactories))
                        RP0Debug.Log($"Generated Behaviours for contract {c.subType}");
                }
            }

            // now, complete the contract step by step
            if (c.Offer())
                yield return new WaitForFixedUpdate();

            if (c.Accept())
            {
                yield return new WaitForFixedUpdate();
            }

            if (complete)
            {
                if (c.Complete())
                {
                    //yield return new WaitForFixedUpdate();
                    Contracts.ContractSystem.Instance.ContractsFinished.Add(c);
                }
                else
                    RP0Debug.LogWarning($"Couldn't complete contract {c.subType}");
            }
            else
            {
                Contracts.ContractSystem.Instance.Contracts.Add(c);
            }

            _runningContractCoroutines--;
            ignoreCareerEventsScope.Dispose();
        }

        private void ShowScenarioFailedToApplyMessage()
        {
            string msg = $"An error occurred while trying to apply custom scenario parameters. " +
                         $"The state of the game in now in an invalid state and continuing is not recommended.";
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "ShowScenarioFailedToApplyMessage",
                                         "Scenario start failed",
                                         msg,
                                         KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                         false,
                                         HighLogic.UISkin,
                                         false);
        }

        private static void ResetLastMaintenanceUpdate(double newUT)
        {
            try
            {
                MaintenanceHandler.Instance.lastUpdate = newUT;
                MaintenanceHandler.Instance.lastRepUpdate = newUT;
            }
            catch (Exception ex)
            {
                RP0Debug.LogError($"Couldn't update last RP0 maintenance");
                RP0Debug.LogException(ex);
            }
        }

        public void ClobberNewGameUI(PopupDialog newGameDlg)
        {
            var hzGroups = newGameDlg.GetComponentsInChildren<HorizontalLayoutGroup>();
            var hzGroup = hzGroups.Last(g => g.name == "UIHorizontalLayoutPrefab(Clone)");

            var uiItem = Instantiate(UISkinManager.GetPrefab("UIVerticalLayoutPrefab"));
            uiItem.SetActive(true);
            uiItem.transform.SetParent(hzGroup.transform.parent, worldPositionStays: false);

            var btnPrefab = hzGroup.transform.FindDeepChild("UIButtonPrefab(Clone)");
            var newBtn = Instantiate(btnPrefab.gameObject, uiItem.transform);
            newBtn.SetActive(true);

            _btnText = newBtn.GetComponentInChildren<TextMeshProUGUI>();

            var btn = newBtn.GetComponentInChildren<Button>();
            btn.onClick.AddListener(() =>
            {
                PresetPickerGUI.Instance.ToggleVisible();
            });

            hzGroup.transform.SetParent(uiItem.transform);

            UpdateUI();
        }

        private void UpdateUI()
        {
            _btnText.text = $"Scenario: <b><color=#b4d455>{_curScenarioName}</color></b>";
        }
    }
}
