using KSP.UI;
using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using KCTUtils = RP0.KCTUtilities;
using ROUtils.DataTypes;
using ROUtils;

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

        public const int VERSION = 3;
        [KSPField(isPersistant = true)]
        public int LoadedSaveVersion = VERSION;

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
        public bool CrewRnREnabled = true;
        public bool IsMissionTrainingEnabled;
        private EventData<RDTech> onKctTechQueuedEvent;
        private HashSet<string> _toRemove = new HashSet<string>();
        private Dictionary<string, Tuple<TrainingTemplate, TrainingTemplate>> _partSynsHandled = new Dictionary<string, Tuple<TrainingTemplate, TrainingTemplate>>();
        private bool _isFirstLoad = true;    // true if it's a freshly started career

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
            AstronautComplex ac = Harmony.PatchAstronautComplex.Instance;

            if (ac == null)
                return;

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
            GameEvents.OnPartPurchased.Add(OnPartPurchased);
            GameEvents.OnGameSettingsApplied.Add(LoadSettings);
            GameEvents.onGameStateLoad.Add(LoadSettings);

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

            LoadedSaveVersion = VERSION;

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

            Profiler.BeginSample("RP0ProcessCrew");
            double time = Planetarium.GetUniversalTime();
            ProcessRetirements(time);
            ProcessCourses(UTDiff);
            ProcessExpirations(time);
            Profiler.EndSample();
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
        }

        public void OnDestroy()
        {
            GameEvents.onVesselRecoveryProcessing.Remove(VesselRecoveryProcessing);
            GameEvents.OnCrewmemberHired.Remove(OnCrewHired);
            GameEvents.OnPartPurchased.Remove(OnPartPurchased);
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
                    AddPartCourses(ap);
                }
            }
        }

        public void AddPartCourses(AvailablePart ap)
        {
            if (ap.partPrefab.isVesselEVA || ap.name.StartsWith("kerbalEVA", StringComparison.OrdinalIgnoreCase) ||
                ap.partPrefab.Modules.Contains<KerbalSeat>() || KCTUtils.IsClamp(ap.partPrefab)) return;

            bool hasTech = !string.IsNullOrEmpty(ap.TechRequired);
            bool isKCTExperimentalNode = hasTech && SpaceCenterManagement.Instance.TechListHas(ap.TechRequired);
            bool isPartUnlocked = !hasTech || (!isKCTExperimentalNode && ResearchAndDevelopment.GetTechnologyState(ap.TechRequired) == RDTech.State.Available);

            if (!isKCTExperimentalNode && !isPartUnlocked)
                return;

            bool isPartPurchased = ResearchAndDevelopment.PartModelPurchased(ap);

            TrainingDatabase.SynonymReplace(ap.name, out string name);
            if (!_partSynsHandled.TryGetValue(name, out var coursePair))
            {
                TrainingTemplate profCourse = GenerateCourseProf(ap, !isPartPurchased);
                profCourse.partsCovered.Add(ap);
                AppendToPartTooltip(ap, profCourse);
                TrainingTemplate missionCourse = null;

                // We have to generate the mission training now
                // though we will hide it unless the part is purchased
                if (IsMissionTrainingEnabled)
                {
                    missionCourse = GenerateCourseMission(ap, !isPartPurchased);
                    missionCourse.partsCovered.Add(ap);
                    AppendToPartTooltip(ap, missionCourse);
                }
                _partSynsHandled.Add(name, new Tuple<TrainingTemplate, TrainingTemplate>(profCourse, missionCourse));
            }
            else
            {
                TrainingTemplate pc = coursePair.Item1;
                TrainingTemplate mc = coursePair.Item2;

                // We might have generated as an experimental part
                // And now the node's completing.
                pc.isTemporary &= !isPartPurchased;
                if (!pc.partsCovered.Contains(ap))
                {
                    pc.partsCovered.Add(ap);
                    pc.UpdateFromPart(ap);
                    AppendToPartTooltip(ap, pc);
                }

                if (mc != null)
                {
                    mc.isTemporary &= !isPartPurchased;
                    if (!mc.partsCovered.Contains(ap))
                    {
                        mc.partsCovered.Add(ap);
                        mc.UpdateFromPart(ap);
                        AppendToPartTooltip(ap, mc);
                    }
                }
            }
        }

        public static bool CheckCrewForPart(ProtoCrewMember pcm, string partName, bool includeProf, bool includeMission)
        {
            // lolwut. But just in case.
            if (pcm == null)
                return false;

            if (!HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsTrainingEnabled
                || SpaceCenterManagement.Instance.IsSimulatedFlight)
                return true;

            // If part doesn't have a training associated with it, abort
            if (!TrainingDatabase.TrainingExists(partName, out string training))
                return true;

            return includeProf ? Instance.NautHasTrainingForPart(pcm, training, includeMission)
                : includeMission && Instance.IsMissionTrainingEnabled ? Instance.NautHasMissionTrainingForPart(pcm, training) : true;
        }

        public static bool CanCrewLaunchOnVessel(ProtoCrewMember pcm, List<Part> parts)
        {
            bool needsMission = Instance.IsMissionTrainingEnabled;
            foreach (var p in parts)
            {
                if (p.CrewCapacity == 0)
                    continue;

                if (!CheckCrewForPart(pcm, p.partInfo.name, true, false))
                    return false;
                if (needsMission && !KCTUtils.IsClampOrChild(p))
                    needsMission = !CheckCrewForPart(pcm, p.partInfo.name, false, true);
            }

            return !needsMission;
        }

        public bool NautHasTrainingForPart(ProtoCrewMember pcm, string partName, bool includeMission)
        {
            if (pcm.type == ProtoCrewMember.KerbalType.Tourist)
                return true;

            FlightLog.Entry ent = pcm.careerLog.Last();
            if (ent == null)
                return false;

            bool lacksMission = includeMission && IsMissionTrainingEnabled;
            for (int i = pcm.careerLog.Entries.Count; i-- > 0;)
            {
                FlightLog.Entry e = pcm.careerLog.Entries[i];
                if (string.IsNullOrEmpty(e.type) || string.IsNullOrEmpty(e.target))
                        continue;

                if (lacksMission && IsMissionTrainingEnabled)
                {
                    if (e.type == TrainingType_Mission && e.target == partName)
                    {
                        double exp = GetExpiration(pcm.name, e);
                        lacksMission = exp == 0d || exp < Planetarium.GetUniversalTime();
                    }
                }
                else
                {
                    if (e.type == TrainingType_Proficiency && e.target == partName)
                        return true;
                }
            }
            return false;
        }

        public bool NautHasMissionTrainingForPart(ProtoCrewMember pcm, string partName)
        {
            if (pcm.type == ProtoCrewMember.KerbalType.Tourist)
                return true;

            FlightLog.Entry ent = pcm.careerLog.Last();
            if (ent == null)
                return false;

            for (int i = pcm.careerLog.Entries.Count; i-- > 0;)
            {
                FlightLog.Entry e = pcm.careerLog.Entries[i];
                if (string.IsNullOrEmpty(e.type) || string.IsNullOrEmpty(e.target))
                    continue;

                if (e.type == TrainingType_Mission && e.target == partName)
                {
                    double exp = GetExpiration(pcm.name, e);
                    if (exp != 0d && exp >= Planetarium.GetUniversalTime())
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
                double retIncreaseLeft = Database.SettingsCrew.retireIncreaseCap - retIncreaseTotal;
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
            if (newTotal > Database.SettingsCrew.retireIncreaseCap)
            {
                // Cap the total retirement increase at a specific number of years
                retireOffset = retIncreaseTotal - Database.SettingsCrew.retireIncreaseCap;
                newTotal = Database.SettingsCrew.retireIncreaseCap;
            }
            _retireIncreases[pcmName] = newTotal;

            string sRetireOffset = KSPUtil.PrintDateDelta(retireOffset, false, false);
            RP0Debug.Log("retire date increased by: " + sRetireOffset);

            _retireTimes[pcmName] = GetRetireTime(pcmName) + retireOffset;
            return retireOffset;
        }

        private void LoadSettings(ConfigNode n)
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            RetirementEnabled = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsRetirementEnabled;
            CrewRnREnabled = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsCrewRnREnabled;
            IsMissionTrainingEnabled = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsMissionTrainingEnabled;
            GenerateTrainingTemplates();
        }

        private void VesselRecoveryProcessing(ProtoVessel v, MissionRecoveryDialog mrDialog, float data)
        {
            RP0Debug.Log("- Vessel recovery processing");

            var retirementChanges = new List<string>();
            var inactivity = new List<string>();

            double retireCMQmult = CurrencyUtils.Time(TransactionReasonsRP0.TimeRetirement, 1d);
            double inactiveCMQmult = -CurrencyUtils.Time(TransactionReasonsRP0.TimeInactive, -1d); // how we signal this is a cost not a reward

            double UT = Planetarium.GetUniversalTime();

            // normally we would use v.missionTime, but that doesn't seem to update
            // when you're not actually controlling the vessel
            double elapsedTime = UT - v.launchTime;

            RP0Debug.Log($"mission elapsedTime: {KSPUtil.PrintDateDeltaCompact(elapsedTime, true, true)}");

            // When flight duration was too short, mission training should not be set as expired.
            // This can happen when an on-the-pad failure occurs and the vessel is recovered.
            // We could perhaps override this if they're not actually in flight
            // (if the user didn't recover right from the pad I think this is a fair assumption)
            if (elapsedTime < Database.SettingsCrew.minFlightDurationSecondsForTrainingExpire)
            {
                RP0Debug.Log($"- mission time too short for crew to be inactive (elapsed time was {elapsedTime}, settings set for {Database.SettingsCrew.minFlightDurationSecondsForTrainingExpire})");
                return;
            }

            var validStatuses = new List<string>
            {
                FlightLog.EntryType.Flight.ToString(), Situation_FlightHigh, FlightLog.EntryType.Suborbit.ToString(),
                FlightLog.EntryType.Orbit.ToString(), FlightLog.EntryType.ExitVessel.ToString(),
                FlightLog.EntryType.Land.ToString(), FlightLog.EntryType.Flyby.ToString()
            };


            double acMult = Database.SettingsCrew.ACRnRMults[KCTUtils.GetFacilityLevel(SpaceCenterFacility.AstronautComplex)];
            var allFlightsDict = new Dictionary<string, int>();
            foreach (ProtoCrewMember pcm in v.GetVesselCrew())
            {
                RP0Debug.Log("- Found ProtoCrewMember: " + pcm.displayName);

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
                        SetExpiration(pcm.name, e, Planetarium.GetUniversalTime());

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
                            double countMult = 1 + Math.Pow(situationCount - 1, Database.SettingsCrew.retireOffsetFlightNumPow);
                            retirementMult += situationMult / countMult;
                        }

                        if (TryGetBestSituationMatch(e.target, e.type, "Inactive", out double inactivMult))
                        {
                            inactivityMult += inactivMult;
                        }
                    }
                }

                RP0Debug.Log($" retirementMult: {retirementMult}, inactivityMult: {inactivityMult}, number of valid situations: {situations}");

                if (GetRetireTime(pcm.name) > 0d)
                {
                    double stupidityPenalty = UtilMath.Lerp(Database.SettingsCrew.retireOffsetStupidMin, Database.SettingsCrew.retireOffsetStupidMax, pcm.stupidity);
                    RP0Debug.Log($" stupidityPenalty for {pcm.stupidity}: {stupidityPenalty}");
                    double retireOffset = retirementMult * 86400 * Database.SettingsCrew.retireOffsetBaseMult / stupidityPenalty * retireCMQmult;

                    retireOffset = IncreaseRetireTime(pcm.name, retireOffset);
                    retirementChanges.Add($"\n{pcm.name}, +{KSPUtil.PrintDateDelta(retireOffset, false, false)}, no earlier than {KSPUtil.PrintDate(GetRetireTime(pcm.name), false)}");
                }

                inactivityMult = Math.Max(1, inactivityMult);
                double elapsedTimeDays = elapsedTime / 86400;
                double inactiveTimeDays = Math.Max(Database.SettingsCrew.inactivityMinFlightDurationDays, Math.Pow(elapsedTimeDays, Database.SettingsCrew.inactivityFlightDurationExponent)) *
                                          Math.Min(Database.SettingsCrew.inactivityMaxSituationMult, inactivityMult) * acMult;
                double inactiveTime = inactiveTimeDays * 86400d * inactiveCMQmult;
                RP0Debug.Log($"inactive for: {KSPUtil.PrintDateDeltaCompact(inactiveTime, true, false)} via AC mult {acMult}");

                if (CrewRnREnabled)
                {
                    pcm.SetInactive(inactiveTime, false);
                    inactivity.Add($"\n{pcm.name}, until {KSPUtil.PrintDate(inactiveTime + UT, true, false)}");
                }
            }

            StringBuilder sb = new StringBuilder();
            if (inactivity.Count > 0)
            {
                sb.Append("The following crew members will be on leave:");
                foreach (string s in inactivity)
                {
                    sb.Append(s);
                }
                sb.Append("\n\n");
            }

            if (RetirementEnabled && retirementChanges.Count > 0)
            {
                sb.Append("The following retirement changes have occurred:");
                foreach (string s in retirementChanges)
                    sb.Append(s);
            }

            if (sb.Length > 0)
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "CrewUpdateNotification",
                                             "Crew Updates",
                                             sb.ToString(),
                                             KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                             true,
                                             HighLogic.UISkin,
                                             !HighLogic.LoadedSceneIsFlight)
                    .PrePostActionsNonFlight(ControlTypes.KSC_ALL | ControlTypes.UI_MAIN, "RP0CrewUpdate", OnDialogSpawn, OnDialogDismiss);
            }
        }

        private bool TryGetBestSituationMatch(string body, string situation, string type, out double situationMult)
        {
            var key = $"{body}-{situation}-{type}";
            if (Database.SettingsCrew.SituationValues.TryGetValue(key, out situationMult))
                return true;

            if (body != FlightGlobals.GetHomeBodyName())
            {
                key = $"Other-{situation}-{type}";
                if (Database.SettingsCrew.SituationValues.TryGetValue(key, out situationMult))
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
                retireTime = Planetarium.GetUniversalTime() + GetServiceTime(pcm);
                _retireTimes[pcm.name] = retireTime;
            }

            if (RetirementEnabled && idx != int.MinValue)
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "InitialRetirementDateNotification",
                                             "Initial Retirement Date",
                                             $"{pcm.name} will retire no earlier than {KSPUtil.PrintDate(retireTime, false)}\n(Retirement will be delayed the more interesting training they undergo and flights they fly.)",
                                             KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
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

                sb.Append($"\n\nInteresting flights and training will delay retirement up to an additional {Math.Round(Database.SettingsCrew.retireIncreaseCap / (365.25d * 86400d))} years.");
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "InitialRetirementDateNotification",
                                             "Initial Retirement Dates",
                                             sb.ToString(),
                                             KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
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
                            if (pcm.type == ProtoCrewMember.KerbalType.Applicant)
                                pcm.UTaR = Planetarium.GetUniversalTime();
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
                                                 KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                                 true,
                                                 HighLogic.UISkin,
                                                 !HighLogic.LoadedSceneIsFlight).PrePostActions(ControlTypes.KSC_ALL | ControlTypes.UI_MAIN, "crewUpdate", OnDialogSpawn, OnDialogDismiss);
                }

                _toRemove.Clear();
                MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
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
                MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
            }
        }

        private double GetServiceTime(ProtoCrewMember pcm)
        {
            return CurrencyUtils.Time(TransactionReasonsRP0.TimeRetirement, 86400d * 365.25d *
                (Database.SettingsCrew.retireBaseYears +
                 UtilMath.Lerp(Database.SettingsCrew.retireCourageMin, Database.SettingsCrew.retireCourageMax, pcm.courage) +
                 UtilMath.Lerp(Database.SettingsCrew.retireStupidMin, Database.SettingsCrew.retireStupidMax, pcm.stupidity)));
        }

        public double GetTrainingFinishTime(ProtoCrewMember pcm)
        {
            for (int i = TrainingCourses.Count; i-- > 0;)
            {
                if (TrainingCourses[i].Students.Contains(pcm))
                    return TrainingCourses[i].GetTimeLeft() + Planetarium.GetUniversalTime();
            }

            return -1d;
        }

        public bool IsRetired(ProtoCrewMember pcm) => _retirees.Contains(pcm.name);

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
                bool found = true;
                foreach (var ap in course.PartsCovered)
                {
                    if (ap.TechRequired != techID)
                    {
                        found = false;
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
            for(int j = TrainingTemplates.Count; j-- > 0;)
            {
                var t = TrainingTemplates[j];
                bool removeTemplate = true;
                foreach (var ap in t.partsCovered)
                {
                    if (ap.TechRequired != techID)
                    {
                        removeTemplate = false;
                        break;
                    }
                }
                if (removeTemplate)
                {
                    TrainingTemplates.RemoveAt(j);
                    foreach (var ap in t.partsCovered)
                    {
                        TrainingDatabase.SynonymReplace(ap.name, out string name);
                        _partSynsHandled.Remove(name);
                    }

                    // Clean up active courses
                    for (int i = TrainingCourses.Count; i-- > 0;)
                    {
                        var course = TrainingCourses[i];
                        if (course.FromTemplate(t))
                        {
                            course.AbortCourse();
                            TrainingCourses.RemoveAt(i);
                        }
                    }
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
                if (!ap.TechHidden && ap.partPrefab.CrewCapacity > 0)
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
            c.type = TrainingTemplate.TrainingType.Proficiency;
            c.time = 1d + (TrainingDatabase.GetTime(name) * 86400);
            c.isTemporary = isTemporary;
            c.conflict = new TrainingFlightEntry(TrainingType_Proficiency, name);
            c.training = new TrainingFlightEntry(TrainingType_Proficiency, name);
            TrainingTemplates.Add(c);

            return c;
        }

        private TrainingTemplate GenerateCourseMission(AvailablePart ap, bool isTemporary)
        {
            bool found = TrainingDatabase.SynonymReplace(ap.name, out string name);

            var c = new TrainingTemplate();

            c.id = "msn_" + name;
            c.name = "Mission: " + (found ? name : ap.title);
            c.type = TrainingTemplate.TrainingType.Mission;
            c.time = 1 + TrainingDatabase.GetTime(name + "-Mission") * 86400d;
            c.isTemporary = isTemporary;
            c.timeUseStupid = true;
            c.seatMax = ap.partPrefab.CrewCapacity * TrainingTemplate.SeatMultiplier;
            c.expiration = Database.SettingsCrew.trainingMissionExpirationDays * 86400d;
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

            if (SpaceCenterManagement.Instance.IsSimulatedFlight && FlightGlobals.ActiveVessel != null)
            {
                foreach (ProtoCrewMember pcm in FlightGlobals.ActiveVessel.GetVesselCrew())
                {
                    pcm.inactive = false;
                    if (pcm.type == ProtoCrewMember.KerbalType.Applicant)
                    {
                        pcm.type = ProtoCrewMember.KerbalType.Crew;
                    }
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

                    return courseLength * (1d + Database.SettingsCrew.retireIncreaseMultiplierToTrainingLengthMission);

                case TrainingType_Proficiency:
                    if (specificTrainingsSinceFlight > 0)
                        return courseLength * 0.5d;
                    int anyTrainingsCount = TrainingsSinceLastFlight(pcm, null, null, startAtIndex);
                    if (anyTrainingsCount > 2)
                        return courseLength / anyTrainingsCount + 0.25d;
                    if (anyTrainingsCount > 1)
                        return courseLength;

                    return courseLength * (1d + Database.SettingsCrew.retireIncreaseMultiplierToTrainingLengthProficiency);
            }

            return 0d;
        }

        public static void ExpireFlightLogEntry(FlightLog.Entry entry)
        {
            entry.type = "expired_" + entry.type;
        }

        public void RecalculateBuildRates()
        {
            foreach (var c in TrainingCourses)
                c.RecalculateBuildRate();
        }
    }
}
