using KerbalConstructionTime;
using KSP.UI;
using KSP.UI.Screens;
using KSP.UI.TooltipTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using KCTUtils = KerbalConstructionTime.Utilities;
using RP0.DataTypes;

namespace RP0.Crew
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class CrewHandler : ScenarioModule
    {
        public const string Situation_FlightHigh = "Flight-High";
        public const string TrainingType_Proficiency = "TRAINING_proficiency";
        public const string TrainingType_Mission = "TRAINING_mission";
        public static Dictionary<string, string> TrainingTypesDict;
        private static bool _ComputedTrainingTypes = ComputeTrainingTypes();
        private static bool ComputeTrainingTypes()
        {
            TrainingTypesDict = new Dictionary<string, string>();
            var lst = new List<string>() { TrainingType_Mission, TrainingType_Proficiency };
            foreach (var s in lst)
                TrainingTypesDict[s] = "expired_" + s;

            return true;
        }

        public static CrewHandler Instance { get; private set; } = null;
        public static CrewHandlerSettings Settings { get; private set; } = null;

        [KSPField(isPersistant = true)]
        public int saveVersion;
        public const int VERSION = 3;

        [KSPField(isPersistant = true)]
        private PersistentDictionaryValueTypes<string, double> _retireTimes = new PersistentDictionaryValueTypes<string, double>();
        
        [KSPField(isPersistant = true)]
        private PersistentDictionaryValueTypes<string, double> _retireIncreases = new PersistentDictionaryValueTypes<string, double>();

        [KSPField(isPersistant = true)]
        private PersistentList<TrainingExpiration> _expireTimes = new PersistentList<TrainingExpiration>();

        [KSPField(isPersistant = true)]
        private PersistentHashSetValueType<string> _retirees = new PersistentHashSetValueType<string>();

        [KSPField(isPersistant = true)]
        public PersistentList<TrainingCourse> TrainingCourses = new PersistentList<TrainingCourse>();

        
        public List<TrainingTemplate> TrainingTemplates = new List<TrainingTemplate>();
        
        public bool RetirementEnabled = true;
        public bool IsMissionTrainingEnabled;
        private EventData<RDTech> onKctTechQueuedEvent;
        private HashSet<string> _toRemove = new HashSet<string>();
        private Dictionary<string, Tuple<TrainingTemplate, TrainingTemplate>> _partSynsHandled = new Dictionary<string, Tuple<TrainingTemplate, TrainingTemplate>>();
        private bool _isFirstLoad = true;    // true if it's a freshly started career
        private bool _inAC = false;
        private int _countAvailable, _countAssigned, _countKIA;
        private AstronautComplex _astronautComplex = null;
        private FieldInfo _cliTooltip;

        private static readonly Dictionary<CrewListItem, bool> _storedCrewListItemMouseovers = new Dictionary<CrewListItem, bool>();
        private static readonly List<UIListItem> _crewList = new List<UIListItem>();
        private Coroutine _lockRoutine = null;
        private void OnDialogSpawn()
        {
            if (_lockRoutine != null)
                StopCoroutine(_lockRoutine);

            _lockRoutine = StartCoroutine(OnDialogSpawnRoutine());
        }
        private IEnumerator OnDialogSpawnRoutine()
        {
            yield return null;
            LockCrewItems(true);
        }

        private void OnDialogDismiss()
        {
            LockCrewItems(false);
        }

        private void LockCrewItems(bool shouldLock)
        {
            AstronautComplex[] mbs = FindObjectsOfType<AstronautComplex>();
            if (mbs.Length != 1)
                return;

            AstronautComplex ac = mbs[0];

            if (shouldLock)
            {
                if (_storedCrewListItemMouseovers.Count > 0)
                    return;
            }
            else
            {
                if (_storedCrewListItemMouseovers.Count == 0)
                    return;
            }

            _crewList.AddRange(ac.ScrollListApplicants.GetUiListItems());
            _crewList.AddRange(ac.ScrollListAvailable.GetUiListItems());
            foreach (UIListItem item in _crewList)
            {
                CrewListItem cic;
                cic = item.GetComponent<CrewListItem>();
                if (cic != null)
                {
                    if (shouldLock)
                    {
                        _storedCrewListItemMouseovers[cic] = cic.MouseoverEnabled;
                        cic.MouseoverEnabled = false;
                    }
                    else if (_storedCrewListItemMouseovers.TryGetValue(cic, out bool lockState))
                    {
                        cic.MouseoverEnabled = lockState;
                    }
                }
            }
            _crewList.Clear();
            if (!shouldLock)
            {
                _storedCrewListItemMouseovers.Clear();
            }
        }

        public override void OnAwake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;

            GameEvents.onVesselRecoveryProcessing.Add(VesselRecoveryProcessing);
            GameEvents.OnCrewmemberHired.Add(OnCrewHired);
            GameEvents.onGUIAstronautComplexSpawn.Add(ACSpawn);
            GameEvents.onGUIAstronautComplexDespawn.Add(ACDespawn);
            GameEvents.OnPartPurchased.Add(new EventData<AvailablePart>.OnEvent(OnPartPurchased));
            GameEvents.OnGameSettingsApplied.Add(LoadSettings);
            GameEvents.onGameStateLoad.Add(LoadSettings);

            KCT_GUI.UseAvailabilityChecker = true;
            KCT_GUI.AvailabilityChecker = CheckCrewForPart;

            _cliTooltip = typeof(CrewListItem).GetField("tooltipController", BindingFlags.NonPublic | BindingFlags.Instance);

            if (Settings == null)
            {
                Settings = new CrewHandlerSettings();
                foreach (ConfigNode stg in GameDatabase.Instance.GetConfigNodes("CREWHANDLERSETTINGS"))
                    Settings.Load(stg);
            }

            TrainingDatabase.EnsureInitialized();
        }

        public void Start()
        {
            onKctTechQueuedEvent = GameEvents.FindEvent<EventData<RDTech>>("OnKctTechQueued");
            if (onKctTechQueuedEvent != null)
            {
                onKctTechQueuedEvent.Add(AddCoursesForQueuedTechNode);
            }

            StartCoroutine(EnsureActiveCrewInSimulationRoutine());
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (saveVersion < 1)
            {
                _retireTimes.Clear();
                ConfigNode n = node.GetNode("RETIRETIMES");
                if (n != null)
                {
                    _isFirstLoad = false;
                    foreach (ConfigNode.Value v in n.values)
                        _retireTimes[v.name] = double.Parse(v.value);
                }

                _retireIncreases.Clear();
                n = node.GetNode("RETIREINCREASES");
                if (n != null)
                {
                    foreach (ConfigNode.Value v in n.values)
                        _retireIncreases[v.name] = double.Parse(v.value);
                }

                _retirees.Clear();
                n = node.GetNode("RETIREES");
                if (n != null)
                {
                    foreach (ConfigNode.Value v in n.values)
                        _retirees.Add(v.value);
                }

                _expireTimes.Clear();
                n = node.GetNode("EXPIRATIONS");
                if (n != null)
                {
                    foreach (ConfigNode eN in n.nodes)
                    {
                        _expireTimes.Add(new TrainingExpiration(eN));
                    }
                }

                ConfigNode FSData = node.GetNode("FlightSchoolData");
                if (FSData != null)
                {
                    //load all the active courses
                    TrainingCourses.Clear();
                    foreach (ConfigNode courseNode in FSData.GetNodes("ACTIVE_COURSE"))
                    {
                        try
                        {
                            TrainingCourses.Add(new TrainingCourse(courseNode));
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                        }
                    }
                }
            }

            saveVersion = VERSION;

            KACWrapper.InitKACWrapper();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }

        public void Process(double UTDiff)
        {
            if (_isFirstLoad)
                return;

            double time = KSPUtils.GetUT();
            ProcessRetirements(time);
            ProcessCourses(UTDiff);
            ProcessExpirations(time);
        }

        public void Update()
        {
            if (HighLogic.CurrentGame == null || HighLogic.CurrentGame.CrewRoster == null)
                return;

            if (_isFirstLoad)
            {
                _isFirstLoad = false;
                ProcessFirstLoad();
            }

            if (_inAC)
            {
                FixAstronauComplexUI();
            }
        }

        public void OnDestroy()
        {
            GameEvents.onVesselRecoveryProcessing.Remove(VesselRecoveryProcessing);
            GameEvents.OnCrewmemberHired.Remove(OnCrewHired);
            GameEvents.onGUIAstronautComplexSpawn.Remove(ACSpawn);
            GameEvents.onGUIAstronautComplexDespawn.Remove(ACDespawn);
            GameEvents.OnPartPurchased.Remove(new EventData<AvailablePart>.OnEvent(OnPartPurchased));
            GameEvents.OnGameSettingsApplied.Remove(LoadSettings);
            GameEvents.onGameStateLoad.Remove(LoadSettings);

            if (onKctTechQueuedEvent != null) onKctTechQueuedEvent.Remove(AddCoursesForQueuedTechNode);
        }

        public void AddExpiration(TrainingExpiration e)
        {
            _expireTimes.Add(e);
        }

        public void AddCoursesForQueuedTechNode(RDTech tech)
        {
            for (int i = 0; i < tech.partsAssigned.Count; i++)
            {
                AvailablePart ap = tech.partsAssigned[i];
                if (!ap.TechHidden && ap.partPrefab.CrewCapacity > 0)
                {
                    // KSP thinks that the node is actually unlocked at this point. Use a flag to indicate that KCT will override it later on.
                    AddPartCourses(ap, isKCTExperimentalNode: true);
                }
            }
        }

        public void AddPartCourses(AvailablePart ap, bool isKCTExperimentalNode = false)
        {
            if (ap.partPrefab.isVesselEVA || ap.name.StartsWith("kerbalEVA", StringComparison.OrdinalIgnoreCase) ||
                ap.partPrefab.Modules.Contains<KerbalSeat>() ||
                ap.partPrefab.Modules.Contains<LaunchClamp>() || ap.partPrefab.HasTag("PadInfrastructure")) return;

            TrainingDatabase.SynonymReplace(ap.name, out string name);
            if (!_partSynsHandled.TryGetValue(name, out var coursePair))
            {
                bool isPartUnlocked = !isKCTExperimentalNode && ResearchAndDevelopment.GetTechnologyState(ap.TechRequired) == RDTech.State.Available;

                TrainingTemplate profCourse = GenerateCourseProf(ap, !isPartUnlocked);
                profCourse.partsCovered.Add(ap);
                AppendToPartTooltip(ap, profCourse);
                TrainingTemplate missionCourse = null;
                if (isPartUnlocked && IsMissionTrainingEnabled)
                {
                    missionCourse = GenerateCourseMission(ap);
                    missionCourse.partsCovered.Add(ap);
                    AppendToPartTooltip(ap, missionCourse);
                }
                _partSynsHandled.Add(name, new Tuple<TrainingTemplate, TrainingTemplate>(profCourse, missionCourse));
            }
            else
            {
                TrainingTemplate pc = coursePair.Item1;
                TrainingTemplate mc = coursePair.Item2;
                pc.partsCovered.Add(ap);
                mc.partsCovered.Add(ap);
                AppendToPartTooltip(ap, pc);
                if (mc != null) AppendToPartTooltip(ap, mc);
            }
        }

        public static bool CheckCrewForPart(ProtoCrewMember pcm, string partName)
        {
            // lolwut. But just in case.
            if (pcm == null)
                return false;

            bool requireTraining = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsTrainingEnabled;

            if (!requireTraining || EntryCostStorage.GetCost(partName) == 1)
                return true;

            return Instance.NautHasTrainingForPart(pcm, partName);
        }

        public bool NautHasTrainingForPart(ProtoCrewMember pcm, string partName)
        {
            TrainingDatabase.SynonymReplace(partName, out partName);

            FlightLog.Entry ent = pcm.careerLog.Last();
            if (ent == null)
                return false;

            bool lacksMission = IsMissionTrainingEnabled;
            for (int i = pcm.careerLog.Entries.Count; i-- > 0;)
            {
                FlightLog.Entry e = pcm.careerLog.Entries[i];
                if (lacksMission)
                {
                    if (string.IsNullOrEmpty(e.type) || string.IsNullOrEmpty(e.target))
                        continue;

                    if (e.type == TrainingType_Mission && e.target == partName)
                    {
                        double exp = GetExpiration(pcm.name, e);
                        lacksMission = exp == 0d || exp < KSPUtils.GetUT();
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(e.type) || string.IsNullOrEmpty(e.target))
                        continue;

                    if (e.type == TrainingType_Proficiency && e.target == partName)
                        return true;
                }
            }
            return false;
        }

        public string GetTrainingString(ProtoCrewMember pcm)
        {
            bool found = false;
            StringBuilder sb = new StringBuilder();
            sb.Append("\n\nTraining:");
            foreach (FlightLog.Entry ent in pcm.careerLog.Entries)
            {
                string pretty = GetPrettyCourseName(ent.type);
                if (!string.IsNullOrEmpty(pretty))
                {
                    if (ent.type == TrainingType_Proficiency)
                    {
                        found = true;
                        sb.Append($"\n  {pretty}{ent.target}");
                    }
                    else if (ent.type == TrainingType_Mission)
                    {
                        double exp = GetExpiration(pcm.name, ent);
                        if (exp > 0d)
                        {
                            sb.Append($"\n  {pretty}{ent.target}. Expires {KSPUtil.PrintDate(exp, false)}");
                        }
                    }
                }
            }

            if (found)
                return sb.ToString();
            else
                return string.Empty;
        }

        public bool RemoveExpiration(string pcmName, FlightLog.Entry entry)
        {
            for (int i = _expireTimes.Count; i-- > 0;)
            {
                TrainingExpiration e = _expireTimes[i];
                if (e.pcmName != pcmName)
                    continue;


                if (!e.Compare(entry))
                    continue;

                _expireTimes.RemoveAt(i);
                return true;
            }

            return false;
        }

        public double GetLatestRetireTime(string pcmName)
        {
            double retTime = GetRetireTime(pcmName);
            double retIncreaseTotal = GetRetireIncreaseTime(pcmName);
            if (retTime > 0d)
            {
                double retIncreaseLeft = Settings.retireIncreaseCap - retIncreaseTotal;
                return retTime + retIncreaseLeft;
            }

            return 0d;
        }

        public double GetRetireTime(string pcmName)
        {
            _retireTimes.TryGetValue(pcmName, out double retTime);
            return retTime;
        }

        public double GetRetireIncreaseTime(string pcmName)
        {
            _retireIncreases.TryGetValue(pcmName, out double retTime);
            return retTime;
        }

        public double IncreaseRetireTime(string pcmName, double retireOffset)
        {
            if (retireOffset <= 0d)
                return 0d;

            double retIncreaseTotal = GetRetireIncreaseTime(pcmName);
            double newTotal = retIncreaseTotal + retireOffset;
            if (newTotal > Settings.retireIncreaseCap)
            {
                // Cap the total retirement increase at a specific number of years
                retireOffset = retIncreaseTotal - Settings.retireIncreaseCap;
                newTotal = Settings.retireIncreaseCap;
            }
            _retireIncreases[pcmName] = newTotal;

            string sRetireOffset = KSPUtil.PrintDateDelta(retireOffset, false, false);
            Debug.Log("[RP-0] retire date increased by: " + sRetireOffset);

            _retireTimes[pcmName] = GetRetireTime(pcmName) + retireOffset;
            return retireOffset;
        }

        private void ACSpawn()
        {
            _inAC = true;
            _countAvailable = _countKIA = -1;
        }

        private void ACDespawn()
        {
            _inAC = false;
            _astronautComplex = null;
        }

        private void LoadSettings(ConfigNode n)
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            RetirementEnabled = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsRetirementEnabled;
            IsMissionTrainingEnabled = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsMissionTrainingEnabled;
            GenerateTrainingTemplates();
        }

        private void VesselRecoveryProcessing(ProtoVessel v, MissionRecoveryDialog mrDialog, float data)
        {
            Debug.Log("[RP-0] - Vessel recovery processing");

            var retirementChanges = new List<string>();
            var inactivity = new List<string>();

            double retireCMQmult = CurrencyUtils.Time(TransactionReasonsRP0.TimeRetirement, 1d);
            double inactiveCMQmult = -CurrencyUtils.Time(TransactionReasonsRP0.TimeInactive, -1d); // how we signal this is a cost not a reward

            double UT = KSPUtils.GetUT();

            // normally we would use v.missionTime, but that doesn't seem to update
            // when you're not actually controlling the vessel
            double elapsedTime = UT - v.launchTime;

            Debug.Log($"[RP-0] mission elapsedTime: {KSPUtil.PrintDateDeltaCompact(elapsedTime, true, true)}");

            // When flight duration was too short, mission training should not be set as expired.
            // This can happen when an on-the-pad failure occurs and the vessel is recovered.
            // We could perhaps override this if they're not actually in flight
            // (if the user didn't recover right from the pad I think this is a fair assumption)
            if (elapsedTime < Settings.minFlightDurationSecondsForTrainingExpire)
            {
                Debug.Log($"[RP-0] - mission time too short for crew to be inactive (elapsed time was {elapsedTime}, settings set for {Settings.minFlightDurationSecondsForTrainingExpire})");
                return;
            }

            var validStatuses = new List<string>
            {
                FlightLog.EntryType.Flight.ToString(), Situation_FlightHigh, FlightLog.EntryType.Suborbit.ToString(),
                FlightLog.EntryType.Orbit.ToString(), FlightLog.EntryType.ExitVessel.ToString(),
                FlightLog.EntryType.Land.ToString(), FlightLog.EntryType.Flyby.ToString()
            };


            double acMult = RnRMultiplierFromACLevel(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex));
            var allFlightsDict = new Dictionary<string, int>();
            foreach (ProtoCrewMember pcm in v.GetVesselCrew())
            {
                Debug.Log("[RP-0] - Found ProtoCrewMember: " + pcm.displayName);

                allFlightsDict.Clear();

                int curFlight = pcm.careerLog.Last().flight;
                double inactivityMult = 0;
                double retirementMult = 0;
                int situations = 0;

                foreach (FlightLog.Entry e in pcm.careerLog.Entries)
                {
                    if (e.type == "Nationality")
                        continue;
                    if (e.type == TrainingType_Mission)
                        SetExpiration(pcm.name, e, KSPUtils.GetUT());

                    if (validStatuses.Contains(e.type))
                    {
                        int situationCount;
                        ++situations;

                        var key = $"{e.target}-{e.type}";
                        if (allFlightsDict.ContainsKey(key))
                        {
                            situationCount = allFlightsDict[key];
                            allFlightsDict[key] = ++situationCount;
                        }
                        else
                        {
                            situationCount = 1;
                            allFlightsDict.Add(key, situationCount);
                        }

                        if (e.flight != curFlight)
                            continue;

                        if (TryGetBestSituationMatch(e.target, e.type, "Retire", out double situationMult))
                        {
                            double countMult = 1 + Math.Pow(situationCount - 1, Settings.retireOffsetFlightNumPow);
                            retirementMult += situationMult / countMult;
                        }

                        if (TryGetBestSituationMatch(e.target, e.type, "Inactive", out double inactivMult))
                        {
                            inactivityMult += inactivMult;
                        }
                    }
                }

                Debug.Log($"[RP-0]  retirementMult: {retirementMult}, inactivityMult: {inactivityMult}, number of valid situations: {situations}");

                if (GetRetireTime(pcm.name) > 0d)
                {
                    double stupidityPenalty = UtilMath.Lerp(Settings.retireOffsetStupidMin, Settings.retireOffsetStupidMax, pcm.stupidity);
                    Debug.Log($"[RP-0]  stupidityPenalty for {pcm.stupidity}: {stupidityPenalty}");
                    double retireOffset = retirementMult * 86400 * Settings.retireOffsetBaseMult / stupidityPenalty * retireCMQmult;

                    retireOffset = IncreaseRetireTime(pcm.name, retireOffset);
                    retirementChanges.Add($"\n{pcm.name}, +{KSPUtil.PrintDateDelta(retireOffset, false, false)}, no earlier than {KSPUtil.PrintDate(GetRetireTime(pcm.name), false)}");
                }

                inactivityMult = Math.Max(1, inactivityMult);
                double elapsedTimeDays = elapsedTime / 86400;
                double inactiveTimeDays = Math.Max(Settings.inactivityMinFlightDurationDays, Math.Pow(elapsedTimeDays, Settings.inactivityFlightDurationExponent)) *
                                          Math.Min(Settings.inactivityMaxSituationMult, inactivityMult) * acMult;
                double inactiveTime = inactiveTimeDays * 86400d * inactiveCMQmult;
                Debug.Log($"[RP-0] inactive for: {KSPUtil.PrintDateDeltaCompact(inactiveTime, true, false)} via AC mult {acMult}");

                pcm.SetInactive(inactiveTime, false);
                inactivity.Add($"\n{pcm.name}, until {KSPUtil.PrintDate(inactiveTime + UT, true, false)}");
            }

            if (inactivity.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("The following crew members will be on leave:");
                foreach (string s in inactivity)
                {
                    sb.Append(s);
                }

                if (RetirementEnabled && retirementChanges.Count > 0)
                {
                    sb.Append("\n\nThe following retirement changes have occurred:");
                    foreach (string s in retirementChanges)
                        sb.Append(s);
                }

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "CrewUpdateNotification",
                                             "Crew Updates",
                                             sb.ToString(),
                                             "OK",
                                             true,
                                             HighLogic.UISkin).PrePostActions(ControlTypes.KSC_ALL | ControlTypes.UI_MAIN, "crewUpdate", OnDialogSpawn, OnDialogDismiss);
            }
        }

        private bool TryGetBestSituationMatch(string body, string situation, string type, out double situationMult)
        {
            var key = $"{body}-{situation}-{type}";
            if (Settings.situationValues.TryGetValue(key, out situationMult))
                return true;

            if (body != FlightGlobals.GetHomeBodyName())
            {
                key = $"Other-{situation}-{type}";
                if (Settings.situationValues.TryGetValue(key, out situationMult))
                    return true;
            }

            situationMult = 0;
            return false;
        }

        private void OnCrewHired(ProtoCrewMember pcm, int idx)
        {
            double retireTime;
            // Skip updating the retirement time if this is an existing kerbal.
            if (_retireTimes.ContainsKey(pcm.name))
            {
                retireTime = _retireTimes[pcm.name];
            }
            else
            {
                retireTime = KSPUtils.GetUT() + GetServiceTime(pcm);
                _retireTimes[pcm.name] = retireTime;
            }

            if (RetirementEnabled && idx != int.MinValue)
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "InitialRetirementDateNotification",
                                             "Initial Retirement Date",
                                             $"{pcm.name} will retire no earlier than {KSPUtil.PrintDate(retireTime, false)}\n(Retirement will be delayed the more interesting training they undergo and flights they fly.)",
                                             "OK",
                                             false,
                                             HighLogic.UISkin).PrePostActions(ControlTypes.KSC_ALL | ControlTypes.UI_MAIN, "crewUpdate", OnDialogSpawn, OnDialogDismiss);
            }
        }

        private void ProcessFirstLoad()
        {
            var newHires = new List<string>();
            foreach (ProtoCrewMember pcm in HighLogic.CurrentGame.CrewRoster.Crew)
            {
                if ((pcm.rosterStatus == ProtoCrewMember.RosterStatus.Assigned || pcm.rosterStatus == ProtoCrewMember.RosterStatus.Available) &&
                    !_retireTimes.ContainsKey(pcm.name))
                {
                    if (pcm.trait != KerbalRoster.pilotTrait)
                    {
                        KerbalRoster.SetExperienceTrait(pcm, KerbalRoster.pilotTrait);
                    }

                    newHires.Add(pcm.name);
                    OnCrewHired(pcm, int.MinValue);
                }
            }

            if (newHires.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Earliest crew retirement dates:");
                foreach (string s in newHires)
                    sb.Append($"\n{s}, {KSPUtil.PrintDate(GetRetireTime(s), false)}");

                sb.Append($"\n\nInteresting flights and training will delay retirement up to an additional {Math.Round(Settings.retireIncreaseCap / (365.25d * 86400d))} years.");
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "InitialRetirementDateNotification",
                                             "Initial Retirement Dates",
                                             sb.ToString(),
                                             "OK",
                                             false,
                                             HighLogic.UISkin).PrePostActions(ControlTypes.KSC_ALL | ControlTypes.UI_MAIN, "crewUpdate", OnDialogSpawn, OnDialogDismiss);
            }
        }

        private void ProcessRetirements(double time)
        {
            if (RetirementEnabled)
            {
                foreach (KeyValuePair<string, double> kvp in _retireTimes)
                {
                    ProtoCrewMember pcm = HighLogic.CurrentGame.CrewRoster[kvp.Key];
                    if (pcm == null || pcm.rosterStatus == ProtoCrewMember.RosterStatus.Dead || pcm.rosterStatus == ProtoCrewMember.RosterStatus.Missing)
                    {
                        _toRemove.Add(kvp.Key);
                        continue;
                    }

                    if (pcm.inactive)
                        continue;

                    if (pcm.type == ProtoCrewMember.KerbalType.Applicant
                        || (pcm.type == ProtoCrewMember.KerbalType.Crew && pcm.rosterStatus == ProtoCrewMember.RosterStatus.Available))
                    {
                        if (time > kvp.Value)
                        {
                            _toRemove.Add(kvp.Key);
                            _retirees.Add(kvp.Key);
                            pcm.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                        }
                    }
                }
            }

            if (_toRemove.Count > 0)
            {
                string msgStr = string.Empty;
                foreach (string s in _toRemove)
                {
                    _retireTimes.Remove(s);
                    ProtoCrewMember pcm = HighLogic.CurrentGame.CrewRoster[s];
                    if (pcm != null && _retirees.Contains(s) && pcm.type == ProtoCrewMember.KerbalType.Crew)
                    {
                        msgStr = $"{msgStr}\n{s}";
                    }
                }
                if (!string.IsNullOrEmpty(msgStr))
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                                 new Vector2(0.5f, 0.5f),
                                                 "CrewRetirementNotification",
                                                 "Crew Retirement",
                                                 "The following retirements have occurred:\n" + msgStr,
                                                 "OK",
                                                 true,
                                                 HighLogic.UISkin).PrePostActions(ControlTypes.KSC_ALL | ControlTypes.UI_MAIN, "crewUpdate", OnDialogSpawn, OnDialogDismiss);
                }

                _toRemove.Clear();
                MaintenanceHandler.OnRP0MaintenanceChanged.Fire();
            }
        }

        private void ProcessExpirations(double time)
        {
            for (int i = _expireTimes.Count; i-- > 0;)
            {
                TrainingExpiration exp = _expireTimes[i];
                if (time > exp.expiration)
                {
                    ProtoCrewMember pcm = HighLogic.CurrentGame.CrewRoster[exp.pcmName];
                    if (pcm != null)
                    {
                        for (int j = pcm.careerLog.Entries.Count; j-- > 0;)
                        {
                            FlightLog.Entry ent = pcm.careerLog[j];
                            if (exp.Compare(ent))
                            {
                                ScreenMessages.PostScreenMessage($"{pcm.name}: Expired: {GetPrettyCourseName(ent.type)}{ent.target}");
                                ExpireFlightLogEntry(ent);
                            }
                        }
                    }
                    _expireTimes.RemoveAt(i);
                }
            }
        }

        private void ProcessCourses(double UTDiff)
        {
            bool anyCourseEnded = false;
            for (int i = TrainingCourses.Count; i-- > 0;)
            {
                TrainingCourse course = TrainingCourses[i];
                course.IncrementProgress(UTDiff);
                if (course.Completed)
                {
                    TrainingCourses.RemoveAt(i);
                    anyCourseEnded = true;
                }
            }

            if (anyCourseEnded)
            {
                MaintenanceHandler.OnRP0MaintenanceChanged.Fire();
            }
        }

        private double GetServiceTime(ProtoCrewMember pcm)
        {
            return CurrencyUtils.Time(TransactionReasonsRP0.TimeRetirement, 86400d * 365.25d *
                (Settings.retireBaseYears +
                 UtilMath.Lerp(Settings.retireCourageMin, Settings.retireCourageMax, pcm.courage) +
                 UtilMath.Lerp(Settings.retireStupidMin, Settings.retireStupidMax, pcm.stupidity)));
        }

        private void FixAstronauComplexUI()
        {
            if (_astronautComplex == null)
            {
                AstronautComplex[] mbs = FindObjectsOfType<AstronautComplex>();
                int maxCount = -1;
                foreach (AstronautComplex c in mbs)
                {
                    int count = c.ScrollListApplicants.Count + c.ScrollListAssigned.Count + c.ScrollListAvailable.Count + c.ScrollListKia.Count;
                    if (count > maxCount)
                    {
                        maxCount = count;
                        _astronautComplex = c;
                    }
                }

                if (_astronautComplex == null)
                    return;
            }
            int newAv = _astronautComplex.ScrollListAvailable.Count;
            int newAsgn = _astronautComplex.ScrollListAssigned.Count;
            int newKIA = _astronautComplex.ScrollListKia.Count;
            if (newAv != _countAvailable || newKIA != _countKIA || newAsgn != _countAssigned)
            {
                _countAvailable = newAv;
                _countAssigned = newAsgn;
                _countKIA = newKIA;

                foreach (UIListData<UIListItem> u in _astronautComplex.ScrollListAvailable)
                {
                    CrewListItem cli = u.listItem.GetComponent<CrewListItem>();
                    if (cli == null) continue;

                    FixTooltip(cli);
                    if (cli.GetCrewRef().inactive)
                    {
                        cli.MouseoverEnabled = false;
                        bool notTraining = true;
                        for (int i = TrainingCourses.Count; i-- > 0 && notTraining;)
                        {
                            foreach (ProtoCrewMember pcm in TrainingCourses[i].Students)
                            {
                                if (pcm == cli.GetCrewRef())
                                {
                                    notTraining = false;
                                    cli.SetLabel("Training, done " + KSPUtil.PrintDate(TrainingCourses[i].GetTimeLeft() + KSPUtils.GetUT(), false));
                                    break;
                                }
                            }
                        }
                        if (notTraining)
                            cli.SetLabel("Recovering");
                    }
                }

                foreach (UIListData<UIListItem> u in _astronautComplex.ScrollListAssigned)
                {
                    CrewListItem cli = u.listItem.GetComponent<CrewListItem>();
                    if (cli != null)
                    {
                        FixTooltip(cli);
                    }
                }

                foreach (UIListData<UIListItem> u in _astronautComplex.ScrollListKia)
                {
                    CrewListItem cli = u.listItem.GetComponent<CrewListItem>();
                    if (cli != null)
                    {
                        if (_retirees.Contains(cli.GetName()))
                        {
                            cli.SetLabel("Retired");
                            cli.MouseoverEnabled = false;
                        }
                    }
                }
            }
        }

        private void FixTooltip(CrewListItem cli)
        {
            ProtoCrewMember pcm = cli.GetCrewRef();
            double retTime;
            if (RetirementEnabled && (retTime = GetRetireTime(pcm.name)) > 0d)
            {
                cli.SetTooltip(pcm);
                var ttc = _cliTooltip.GetValue(cli) as TooltipController_CrewAC;
                // TODO: add flight-high entries here
                ttc.descriptionString += $"\n\nRetires no earlier than {KSPUtil.PrintDate(retTime, false)}";

                // Training
                string trainingStr = GetTrainingString(pcm);
                if (!string.IsNullOrEmpty(trainingStr))
                    ttc.descriptionString += trainingStr;
            }
        }

        private double GetExpiration(string pcmName, FlightLog.Entry ent)
        {
            for (int i = _expireTimes.Count; i-- > 0;)
            {
                TrainingExpiration e = _expireTimes[i];
                if (e.Compare(pcmName, ent))
                    return e.expiration;
            }

            return 0d;
        }

        private bool SetExpiration(string pcmName, FlightLog.Entry ent, double expirationUT)
        {
            for (int i = _expireTimes.Count; i-- > 0;)
            {
                TrainingExpiration e = _expireTimes[i];
                if (e.Compare(pcmName, ent))
                {
                    e.expiration = expirationUT;
                    return true;
                }
            }

            return false;
        }

        public string GetTrainingCoursesForTech(string techID)
        {
            var sb = StringBuilderCache.Acquire();
            bool anyFound = false;
            foreach(var course in TrainingCourses)
            {
                bool found = false;
                foreach (var ap in course.partsCovered)
                {
                    if (ap.TechRequired == techID)
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    if (course.Students.Count > 0)
                    {
                        anyFound = true;
                        sb.Append("\n\n").Append(course.GetItemName()).Append(": ").Append(course.Students[0].displayName);
                        for (int i = 1; i < course.Students.Count; ++i)
                            sb.Append(", ").Append(course.Students[i].displayName);
                    }
                }
            }
            if (!anyFound)
            {
                sb.Release();
                return string.Empty;
            }

            return "\nThis will cancel the following training courses and return their astronauts to duty:" + sb.ToStringAndRelease();
        }

        public void OnTechCanceled(string techID)
        {
            for(int i = TrainingCourses.Count; i-- > 0;)
            {
                var course = TrainingCourses[i];
                bool found = false;
                foreach (var ap in course.partsCovered)
                {
                    if (ap.TechRequired == techID)
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    course.AbortCourse();
                    TrainingCourses.RemoveAt(i);
                }
            }
        }

        private void GenerateTrainingTemplates()
        {
            Profiler.BeginSample("RP0 GenerateTrainingTemplates");
            TrainingTemplates.Clear();
            _partSynsHandled.Clear();

            foreach (AvailablePart ap in PartLoader.LoadedPartsList)
            {
                if (!ap.TechHidden && ap.partPrefab.CrewCapacity > 0
                    && (ResearchAndDevelopment.GetTechnologyState(ap.TechRequired) == RDTech.State.Available
                        || KCTGameStates.TechList.Find(t => t.TechID == ap.TechRequired) != null))
                    AddPartCourses(ap);
            }

            foreach (var c in TrainingCourses)
            {
                c.LinkTemplate();
            }
            Profiler.EndSample();
        }

        private TrainingTemplate GenerateCourseProf(AvailablePart ap, bool isTemporary)
        {
            bool found = TrainingDatabase.SynonymReplace(ap.name, out string name);

            var c = new TrainingTemplate();

            c.id = "prof_" + name;
            c.name = "Proficiency: " + (found ? name : ap.title);
            c.time = 1d + (TrainingDatabase.GetTime(name) * 86400);
            c.isTemporary = isTemporary;
            c.conflict = new TrainingFlightEntry(TrainingType_Proficiency, name);
            c.training = new TrainingFlightEntry(TrainingType_Proficiency, name);
            TrainingTemplates.Add(c);

            return c;
        }

        private TrainingTemplate GenerateCourseMission(AvailablePart ap)
        {
            bool found = TrainingDatabase.SynonymReplace(ap.name, out string name);

            var c = new TrainingTemplate();

            c.id = "msn_" + name;
            c.name = "Mission: " + (found ? name : ap.title);
            c.time = 1 + TrainingDatabase.GetTime(name + "-Mission") * 86400d;
            c.isTemporary = false;
            c.timeUseStupid = true;
            c.seatMax = ap.partPrefab.CrewCapacity * 2;
            c.expiration = Settings.trainingMissionExpirationDays * 86400d;
            c.prereq = new TrainingFlightEntry(TrainingType_Proficiency, name);
            c.training = new TrainingFlightEntry(TrainingType_Mission, name);

            TrainingTemplates.Add(c);

            return c;
        }

        private void OnPartPurchased(AvailablePart ap)
        {
            if (ap.partPrefab.CrewCapacity > 0)
            {
                AddPartCourses(ap);
            }
        }

        private string GetPrettyCourseName(string str)
        {
            switch (str)
            {
                case TrainingType_Proficiency:
                    return "Proficiency with ";
                case TrainingType_Mission:
                    return "Mission training for ";
                default:
                    return string.Empty;
            }
        }

        private IEnumerator EnsureActiveCrewInSimulationRoutine()
        {
            if (!HighLogic.LoadedSceneIsFlight) yield return null;
            yield return new WaitForFixedUpdate();

            if (KCTUtils.IsSimulationActive && FlightGlobals.ActiveVessel != null)
            {
                foreach (ProtoCrewMember pcm in FlightGlobals.ActiveVessel.GetVesselCrew())
                {
                    pcm.inactive = false;
                }
            }
        }

        private static void AppendToPartTooltip(AvailablePart ap, TrainingTemplate ct)
        {
            ct.PartsTooltip = ct.PartsTooltip == null ? $"Applies to parts: {ap.title}" :
                                                        $"{ct.PartsTooltip}, {ap.title}";
        }

        private bool IsTraining(FlightLog.Entry entry, string trainingType = null, string trainingTarget = null)
        {
            if (trainingType != null)
                return (entry.type == trainingType || entry.type == TrainingTypesDict[trainingType]) &&
                    (trainingTarget == null || entry.target == trainingTarget);

            foreach (var kvp in TrainingTypesDict)
                if ((kvp.Key == entry.type || kvp.Value == entry.type) && (trainingTarget == null || trainingTarget == entry.target))
                    return true;

            return false;
        }

        /// <summary>
        /// Looks for the specific training type and returns false if
        /// it is the last entry, or if there has been no intervening
        /// flight since the last training of that type. Returns true
        /// if there has been no training of that type.
        /// Can optionally be limited to a specific target.
        /// Can optionally have a number allowed > 0
        /// </summary>
        /// <param name="pcm"></param>
        /// <param name="trainingType">a key from TrainingTypesDict. Will find both active and expired trainings. Null means any training.</param>
        /// <param name="trainingTarget">default: null (ignored)</param>
        /// <param name="numAllowed">default: 0</param>
        /// <returns></returns>
        public bool HasFlightSinceLastTraining(ProtoCrewMember pcm, string trainingType, string trainingTarget = null, int numAllowed = 0)
        {
            int count = pcm.careerLog.Entries.Count;
            if (count == 0)
                return true;
            var entry = pcm.careerLog.Entries[count - 1];
            int numFound = 0;
            if (IsTraining(entry, trainingType, trainingTarget))
                ++numFound;

            for (int i = count - 2; i >= 0 && numFound <= numAllowed; --i)
            {
                entry = pcm.careerLog.Entries[i];
                if (IsTraining(entry, trainingType, trainingTarget))
                    ++numFound;
                else if (!IsTraining(entry))
                    return true;
            }

            return numFound <= numAllowed;
        }

        public int TrainingsSinceLastFlight(ProtoCrewMember pcm, string trainingType = null, string trainingTarget = null, int startAtIndex = -1)
        {
            int trainings = 0;
            int lastIndex = pcm.careerLog.Entries.Count - 1;
            int startIndex = startAtIndex < 0 ? lastIndex : Math.Min(startAtIndex, lastIndex);
            for (int i = startIndex; i >= 0; --i)
            {
                var entry = pcm.careerLog.Entries[i];
                if (IsTraining(entry, trainingType, trainingTarget))
                    ++trainings;
                if (!IsTraining(entry))
                    return trainings;
            }

            return trainings;
        }

        public double GetRetirementOffsetForTraining(ProtoCrewMember pcm, double courseLength, string trainingType, string trainingTarget, int startAtIndex = -1)
        {
            int specificTrainingsSinceFlight = TrainingsSinceLastFlight(pcm, trainingType, trainingTarget, startAtIndex);
            switch (trainingType)
            {
                case TrainingType_Mission:
                    if (specificTrainingsSinceFlight > 1)
                        return 0;
                    if (specificTrainingsSinceFlight == 1)
                    {
                        if (TrainingsSinceLastFlight(pcm, trainingType, null, startAtIndex) > 1)
                            return courseLength * 0.5d;
                        else
                            return courseLength;
                    }

                    return courseLength * (1d + Settings.retireIncreaseMultiplierToTrainingLengthMission);

                case TrainingType_Proficiency:
                    if (specificTrainingsSinceFlight > 0)
                        return courseLength * 0.5d;
                    int anyTrainingsCount = TrainingsSinceLastFlight(pcm, null, null, startAtIndex);
                    if (anyTrainingsCount > 2)
                        return courseLength / anyTrainingsCount + 0.25d;
                    if (anyTrainingsCount > 1)
                        return courseLength;

                    return courseLength * (1d + Settings.retireIncreaseMultiplierToTrainingLengthProficiency);
            }

            return 0d;
        }

        public static void ExpireFlightLogEntry(FlightLog.Entry entry)
        {
            entry.type = "expired_" + entry.type;
        }

        public static double RnRMultiplierFromACLevel(double fracLevel) => 1d - fracLevel * 0.5d;
    }
}
