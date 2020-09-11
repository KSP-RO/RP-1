﻿using KerbalConstructionTime;
using KSP.UI;
using KSP.UI.Screens;
using KSP.UI.TooltipTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace RP0.Crew
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class CrewHandler : ScenarioModule
    {
        public const string Situation_FlightHigh = "Flight-High";
        public const string TrainingType_Proficiency = "TRAINING_proficiency";
        public const string TrainingType_Mission = "TRAINING_mission";
        public const double UpdateInterval = 3600;
        public const int FlightLogUpdateInterval = 50;
        public const int KarmanAltitude = 100000;

        public static CrewHandler Instance { get; private set; } = null;
        public static CrewHandlerSettings Settings { get; private set; } = null;

        [KSPField(isPersistant = true)]
        public double NextUpdate = UpdateInterval;    // Get the first hour for free :)

        public Dictionary<string, double> KerbalRetireTimes = new Dictionary<string, double>();
        public Dictionary<string, double> KerbalRetireIncreases = new Dictionary<string, double>();
        public List<CourseTemplate> CourseTemplates = new List<CourseTemplate>();
        public List<CourseTemplate> OfferedCourses = new List<CourseTemplate>();
        public List<ActiveCourse> ActiveCourses = new List<ActiveCourse>();
        public bool RetirementEnabled = true;

        private EventData<RDTech> onKctTechQueuedEvent;
        private HashSet<string> _toRemove = new HashSet<string>();
        private HashSet<string> _retirees = new HashSet<string>();
        private HashSet<string> _partSynsHandled = new HashSet<string>();
        private List<TrainingExpiration> _expireTimes = new List<TrainingExpiration>();
        private bool _isFirstLoad = true;
        private bool _inAC = false;
        private int _flightLogUpdateCounter = 0;
        private int _countAvailable, _countAssigned, _countKIA;
        private Dictionary<Guid, double> _vesselAltitudes = new Dictionary<Guid, double>();
        private AstronautComplex _astronautComplex = null;
        private FieldInfo _cliTooltip;

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

            FindAllCourseConfigs();     //find all applicable configs
        }

        public void Start()
        {
            double ut = Planetarium.GetUniversalTime();
            if (NextUpdate > ut + UpdateInterval)
            {
                // KRASH has a bad habit of not reverting state properly when exiting sims.
                // This means that the updateInterval could end up years into the future.
                NextUpdate = ut + 5;
            }

            onKctTechQueuedEvent = GameEvents.FindEvent<EventData<RDTech>>("OnKctTechQueued");
            if (onKctTechQueuedEvent != null)
            {
                onKctTechQueuedEvent.Add(AddCoursesForTechNode);
            }

            StartCoroutine(CreateCoursesRoutine());
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (Settings == null)
            {
                Settings = new CrewHandlerSettings();
                foreach (ConfigNode stg in GameDatabase.Instance.GetConfigNodes("CREWHANDLERSETTINGS"))
                    Settings.Load(stg);
            }

            KerbalRetireTimes.Clear();
            ConfigNode n = node.GetNode("RETIRETIMES");
            if (n != null)
            {
                foreach (ConfigNode.Value v in n.values)
                    KerbalRetireTimes[v.name] = double.Parse(v.value);
            }

            KerbalRetireIncreases.Clear();
            n = node.GetNode("RETIREINCREASES");
            if (n != null)
            {
                foreach (ConfigNode.Value v in n.values)
                    KerbalRetireIncreases[v.name] = double.Parse(v.value);
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
                ActiveCourses.Clear();
                foreach (ConfigNode courseNode in FSData.GetNodes("ACTIVE_COURSE"))
                {
                    try
                    {
                        ActiveCourses.Add(new ActiveCourse(courseNode));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }

            TrainingDatabase.EnsureInitialized();
            GenerateOfferedCourses();
            KACWrapper.InitKACWrapper();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            ConfigNode n = node.AddNode("RETIRETIMES");
            foreach (KeyValuePair<string, double> kvp in KerbalRetireTimes)
                n.AddValue(kvp.Key, kvp.Value);

            n = node.AddNode("RETIREINCREASES");
            foreach (KeyValuePair<string, double> kvp in KerbalRetireIncreases)
                n.AddValue(kvp.Key, kvp.Value);

            n = node.AddNode("RETIREES");
            foreach (string s in _retirees)
                n.AddValue("retiree", s);

            n = node.AddNode("EXPIRATIONS");
            foreach (TrainingExpiration e in _expireTimes)
                e.Save(n.AddNode("Expiration"));

            var FSData = new ConfigNode("FlightSchoolData");
            //save all the active courses
            foreach (ActiveCourse course in ActiveCourses)
            {
                ConfigNode courseNode = course.AsConfigNode();
                FSData.AddNode("ACTIVE_COURSE", courseNode);
            }
            node.AddNode("FlightSchoolData", FSData);
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

            if (HighLogic.LoadedSceneIsFlight && _flightLogUpdateCounter++ >= FlightLogUpdateInterval)
            {
                _flightLogUpdateCounter = 0;
                UpdateFlightLog();
            }

            double time = Planetarium.GetUniversalTime();
            if (NextUpdate < time)
            {
                // Ensure that CrewHandler updates happen at predictable times so that accurate KAC alarms can be set.
                do
                {
                    NextUpdate += UpdateInterval;
                }
                while (NextUpdate < time);

                ProcessRetirements(time);
                ProcessCourses(time);
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

            if (onKctTechQueuedEvent != null) onKctTechQueuedEvent.Remove(AddCoursesForTechNode);
        }

        public void AddExpiration(TrainingExpiration e)
        {
            _expireTimes.Add(e);
        }

        public void AddCoursesForTechNode(RDTech tech)
        {
            for (int i = 0; i < tech.partsAssigned.Count; i++)
            {
                AvailablePart ap = tech.partsAssigned[i];
                if (ap.partPrefab.CrewCapacity > 0)
                {
                    AddPartCourses(ap);
                }
            }
        }

        public void AddPartCourses(AvailablePart ap)
        {
            string name = TrainingDatabase.SynonymReplace(ap.name);
            if (!_partSynsHandled.Contains(name))
            {
                _partSynsHandled.Add(name);
                bool isPartUnlocked = ResearchAndDevelopment.PartModelPurchased(ap) && !ResearchAndDevelopment.IsExperimentalPart(ap);

                GenerateCourseProf(ap, !isPartUnlocked);
                if (isPartUnlocked)
                {
                    GenerateCourseMission(ap);
                }
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
            partName = TrainingDatabase.SynonymReplace(partName);

            FlightLog.Entry ent = pcm.careerLog.Last();
            if (ent == null)
                return false;

            bool lacksMission = true;
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
                        lacksMission = exp == 0d || exp < Planetarium.GetUniversalTime();
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

        public bool RemoveExpiration(string pcmName, string entry)
        {
            for (int i = _expireTimes.Count; i-- > 0;)
            {
                TrainingExpiration e = _expireTimes[i];
                if (e.PcmName != pcmName)
                    continue;

                for (int j = e.Entries.Count; j-- > 0;)
                {
                    if (e.Entries[j] == entry)
                    {
                        e.Entries.RemoveAt(j);

                        if (e.Entries.Count == 0)
                            _expireTimes.RemoveAt(i);

                        return true;
                    }
                }
            }

            return false;
        }

        public double GetLatestRetireTime(ProtoCrewMember pcm)
        {
            if (KerbalRetireTimes.TryGetValue(pcm.name, out double retTime))
            {
                KerbalRetireIncreases.TryGetValue(pcm.name, out double retIncreaseTotal);
                double retIncreaseLeft = Settings.retireIncreaseCap - retIncreaseTotal;
                return retTime + retIncreaseLeft;
            }

            return 0;
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
        }

        private void VesselRecoveryProcessing(ProtoVessel v, MissionRecoveryDialog mrDialog, float data)
        {
            Debug.Log("[RP-0] - Vessel recovery processing");

            var retirementChanges = new List<string>();
            var inactivity = new List<string>();

            double UT = Planetarium.GetUniversalTime();

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

            foreach (ProtoCrewMember pcm in v.GetVesselCrew())
            {
                Debug.Log("[RP-0] - Found ProtoCrewMember: " + pcm.displayName);

                var allFlightsDict = new Dictionary<string, int>();
                int curFlight = pcm.careerLog.Last().flight;
                double inactivityMult = 0;
                double retirementMult = 0;

                foreach (FlightLog.Entry e in pcm.careerLog.Entries)
                {
                    if (e.type == "Nationality")
                        continue;
                    if (e.type == TrainingType_Mission)
                        SetExpiration(pcm.name, e, Planetarium.GetUniversalTime());

                    if (validStatuses.Contains(e.type))
                    {
                        int situationCount;
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

                Debug.Log("[RP-0]  retirementMult: " + retirementMult);
                Debug.Log("[RP-0]  inactivityMult: " + inactivityMult);

                double acMult = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) + 1;
                Debug.Log("[RP-0]  AC multiplier: " + acMult);

                if (KerbalRetireTimes.TryGetValue(pcm.name, out double retTime))
                {
                    double stupidityPenalty = UtilMath.Lerp(Settings.retireOffsetStupidMin, Settings.retireOffsetStupidMax, pcm.stupidity);
                    Debug.Log($"[RP-0]  stupidityPenalty for {pcm.stupidity}: {stupidityPenalty}");
                    double retireOffset = retirementMult * 86400 * Settings.retireOffsetBaseMult / stupidityPenalty;

                    if (retireOffset > 0)
                    {
                        KerbalRetireIncreases.TryGetValue(pcm.name, out double retIncreaseTotal);
                        retIncreaseTotal += retireOffset;
                        if (retIncreaseTotal > Settings.retireIncreaseCap)
                        {
                            // Cap the total retirement increase at a specific number of years
                            retireOffset -= retIncreaseTotal - Settings.retireIncreaseCap;
                            retIncreaseTotal = Settings.retireIncreaseCap;
                        }
                        KerbalRetireIncreases[pcm.name] = retIncreaseTotal;

                        string sRetireOffset = KSPUtil.PrintDateDelta(retireOffset, false, false);
                        Debug.Log("[RP-0] retire date increased by: " + sRetireOffset);

                        retTime += retireOffset;
                        KerbalRetireTimes[pcm.name] = retTime;
                        retirementChanges.Add($"\n{pcm.name}, +{sRetireOffset}, no earlier than {KSPUtil.PrintDate(retTime, false)}");
                    }
                }

                inactivityMult = Math.Max(1, inactivityMult);
                double elapsedTimeDays = elapsedTime / 86400;
                double inactiveTimeDays = Math.Pow(Math.Max(Settings.inactivityMinFlightDurationDays, elapsedTimeDays), Settings.inactivityFlightDurationExponent) *
                                          Math.Min(Settings.inactivityMaxSituationMult, inactivityMult) / acMult;
                double inactiveTime = inactiveTimeDays * 86400;
                Debug.Log("[RP-0] inactive for: " + KSPUtil.PrintDateDeltaCompact(inactiveTime, true, false));

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
                                             HighLogic.UISkin);
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
            double retireTime = Planetarium.GetUniversalTime() + GetServiceTime(pcm);
            KerbalRetireTimes[pcm.name] = retireTime;

            if (RetirementEnabled && idx != int.MinValue)
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "InitialRetirementDateNotification",
                                             "Initial Retirement Date",
                                             $"{pcm.name} will retire no earlier than {KSPUtil.PrintDate(retireTime, false)}\n(Retirement will be delayed the more interesting flights they fly.)",
                                             "OK",
                                             false,
                                             HighLogic.UISkin);
            }
        }

        private void UpdateFlightLog()
        {
            if (!FlightGlobals.currentMainBody.isHomeWorld) return;

            foreach (Vessel v in FlightGlobals.VesselsLoaded)
            {
                if (v.crewedParts == 0) continue;

                if (!_vesselAltitudes.TryGetValue(v.id, out double prevAltitude))
                {
                    prevAltitude = v.altitude;
                }
                _vesselAltitudes[v.id] = v.altitude;

                if (prevAltitude < Settings.flightHighAltitude && v.altitude >= Settings.flightHighAltitude)
                {
                    foreach (ProtoCrewMember c in v.GetVesselCrew())
                    {
                        c.flightLog.AddEntryUnique(new FlightLog.Entry(c.flightLog.Flight, Situation_FlightHigh, v.mainBody.name));
                    }
                }

                if (prevAltitude < KarmanAltitude && v.altitude >= KarmanAltitude)
                {
                    foreach (ProtoCrewMember c in v.GetVesselCrew())
                    {
                        c.flightLog.AddEntryUnique(FlightLog.EntryType.Suborbit, v.mainBody.name);
                    }
                }
            }
        }

        private void ProcessFirstLoad()
        {
            var newHires = new List<string>();
            foreach (ProtoCrewMember pcm in HighLogic.CurrentGame.CrewRoster.Crew)
            {
                if ((pcm.rosterStatus == ProtoCrewMember.RosterStatus.Assigned || pcm.rosterStatus == ProtoCrewMember.RosterStatus.Available) &&
                    !KerbalRetireTimes.ContainsKey(pcm.name))
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
                    sb.Append($"\n{s}, {KSPUtil.PrintDate(KerbalRetireTimes[s], false)}");

                sb.Append($"\n\nInteresting flights will delay retirement up to an additional {Math.Round(Settings.retireIncreaseCap / 31536000)} years.");
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "InitialRetirementDateNotification",
                                             "Initial Retirement Dates",
                                             sb.ToString(),
                                             "OK",
                                             false,
                                             HighLogic.UISkin);
            }
        }

        private void ProcessRetirements(double time)
        {
            if (RetirementEnabled)
            {
                foreach (KeyValuePair<string, double> kvp in KerbalRetireTimes)
                {
                    ProtoCrewMember pcm = HighLogic.CurrentGame.CrewRoster[kvp.Key];
                    if (pcm == null)
                        _toRemove.Add(kvp.Key);
                    else
                    {
                        if (pcm.rosterStatus != ProtoCrewMember.RosterStatus.Available)
                        {
                            if (pcm.rosterStatus != ProtoCrewMember.RosterStatus.Assigned)
                                _toRemove.Add(kvp.Key);

                            continue;
                        }

                        if (pcm.inactive)
                            continue;

                        if (time > kvp.Value)
                        {
                            _toRemove.Add(kvp.Key);
                            _retirees.Add(kvp.Key);
                            pcm.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                        }
                    }
                }
            }

            // TODO remove from courses? Except I think they won't retire if inactive either so that's ok.
            if (_toRemove.Count > 0)
            {
                string msgStr = string.Empty;
                foreach (string s in _toRemove)
                {
                    KerbalRetireTimes.Remove(s);
                    if (HighLogic.CurrentGame.CrewRoster[s] != null && _retirees.Contains(s))
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
                                                 HighLogic.UISkin);
                }

                _toRemove.Clear();
            }

            if (_toRemove.Count > 0)
            {
                MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
            }
        }

        private void ProcessCourses(double time)
        {
            bool anyCourseEnded = false;
            for (int i = ActiveCourses.Count; i-- > 0;)
            {
                ActiveCourse course = ActiveCourses[i];
                if (course.ProgressTime(time)) //returns true when the course completes
                {
                    ActiveCourses.RemoveAt(i);
                    anyCourseEnded = true;
                }
            }

            for (int i = _expireTimes.Count; i-- > 0;)
            {
                TrainingExpiration e = _expireTimes[i];
                if (time > e.Expiration)
                {
                    ProtoCrewMember pcm = HighLogic.CurrentGame.CrewRoster[e.PcmName];
                    if (pcm != null)
                    {
                        for (int j = pcm.careerLog.Entries.Count; j-- > 0;)
                        {
                            if (e.Entries.Count == 0)
                                break;
                            FlightLog.Entry ent = pcm.careerLog[j];
                            for (int k = e.Entries.Count; k-- > 0;)
                            {
                                // Allow only mission trainings to expire.
                                // This check is actually only needed for old savegames as only these can have expirations on proficiencies.
                                if (ent.type == TrainingType_Mission && e.Compare(k, ent))
                                {
                                    ScreenMessages.PostScreenMessage($"{pcm.name}: Expired: {GetPrettyCourseName(ent.type)}{ent.target}");
                                    ent.type = "expired_" + ent.type;
                                    e.Entries.RemoveAt(k);
                                }
                            }
                        }
                    }
                    _expireTimes.RemoveAt(i);
                }
            }

            if (anyCourseEnded)
            {
                MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
            }
        }

        private double GetServiceTime(ProtoCrewMember pcm)
        {
            return 86400d * 365d *
                (Settings.retireBaseYears +
                 UtilMath.Lerp(Settings.retireCourageMin, Settings.retireCourageMax, pcm.courage) +
                 UtilMath.Lerp(Settings.retireStupidMin, Settings.retireStupidMax, pcm.stupidity));
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
                        for (int i = ActiveCourses.Count; i-- > 0 && notTraining;)
                        {
                            foreach (ProtoCrewMember pcm in ActiveCourses[i].Students)
                            {
                                if (pcm == cli.GetCrewRef())
                                {
                                    notTraining = false;
                                    cli.SetLabel("Training, done " + KSPUtil.PrintDate(ActiveCourses[i].startTime + ActiveCourses[i].GetTime(ActiveCourses[i].Students), false));
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
            if (RetirementEnabled && KerbalRetireTimes.TryGetValue(pcm.name, out double retTime))
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
                if (e.PcmName == pcmName)
                {
                    for (int j = e.Entries.Count; j-- > 0;)
                    {
                        if (e.Compare(j, ent))
                            return e.Expiration;
                    }
                }
            }

            return 0d;
        }

        private bool SetExpiration(string pcmName, FlightLog.Entry ent, double expirationUT)
        {
            for (int i = _expireTimes.Count; i-- > 0;)
            {
                TrainingExpiration e = _expireTimes[i];
                if (e.PcmName == pcmName)
                {
                    for (int j = e.Entries.Count; j-- > 0;)
                    {
                        if (e.Compare(j, ent))
                        {
                            e.Expiration = expirationUT;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void FindAllCourseConfigs()
        {
            CourseTemplates.Clear();
            //find all configs and save them
            foreach (ConfigNode course in GameDatabase.Instance.GetConfigNodes("FS_COURSE"))
            {
                CourseTemplates.Add(new CourseTemplate(course));
            }
            Debug.Log($"[RP-0] Found {CourseTemplates.Count} courses.");
        }

        private void GenerateOfferedCourses()
        {
            //convert the saved configs to course offerings
            foreach (CourseTemplate template in CourseTemplates)
            {
                var duplicate = new CourseTemplate(template.sourceNode, true); //creates a duplicate so the initial template is preserved
                duplicate.PopulateFromSourceNode();
                if (duplicate.Available)
                    OfferedCourses.Add(duplicate);
            }

            foreach (AvailablePart ap in PartLoader.LoadedPartsList)
            {
                if (ap.partPrefab.CrewCapacity > 0 &&
                    ResearchAndDevelopment.PartModelPurchased(ap) &&
                    !ResearchAndDevelopment.IsExperimentalPart(ap))
                {
                    AddPartCourses(ap);
                }
            }
        }

        private void GenerateCourseProf(AvailablePart ap, bool isTemporary)
        {
            var n = new ConfigNode("FS_COURSE");
            string name = TrainingDatabase.SynonymReplace(ap.name);

            n.AddValue("id", "prof_" + name);
            n.AddValue("name", "Proficiency: " + name);
            n.AddValue("time", 1d + (TrainingDatabase.GetTime(name) * 86400));
            n.AddValue("isTemporary", isTemporary);
            n.AddValue("conflicts", $"{TrainingType_Proficiency}:{name}");

            ConfigNode r = n.AddNode("REWARD");
            r.AddValue("XPAmt", Settings.trainingProficiencyXP);
            ConfigNode l = r.AddNode("FLIGHTLOG");
            l.AddValue("0", $"{TrainingType_Proficiency},{name}");

            var c = new CourseTemplate(n);
            c.PopulateFromSourceNode();
            OfferedCourses.Add(c);
        }

        private void GenerateCourseMission(AvailablePart ap)
        {
            var n = new ConfigNode("FS_COURSE");
            string name = TrainingDatabase.SynonymReplace(ap.name);

            n.AddValue("id", "msn_" + name);
            n.AddValue("name", "Mission: " + name);
            n.AddValue("time", 1 + TrainingDatabase.GetTime(name + "-Mission") * 86400);
            n.AddValue("isTemporary", false);
            n.AddValue("timeUseStupid", true);
            n.AddValue("seatMax", ap.partPrefab.CrewCapacity * 2);
            n.AddValue("expiration", Settings.trainingMissionExpirationDays * 86400);
            n.AddValue("preReqs", $"{TrainingType_Proficiency}:{name}");

            ConfigNode r = n.AddNode("REWARD");
            ConfigNode l = r.AddNode("FLIGHTLOG");
            l.AddValue("0", $"{TrainingType_Mission},{name}");

            var c = new CourseTemplate(n);
            c.PopulateFromSourceNode();
            OfferedCourses.Add(c);
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

        private IEnumerator CreateCoursesRoutine()
        {
            yield return new WaitForFixedUpdate();

            for (int i = 0; i < PartLoader.LoadedPartsList.Count; i++)
            {
                var ap = PartLoader.LoadedPartsList[i];
                if (ap.partPrefab.CrewCapacity > 0)
                {
                    var kctTech = KCTGameStates.TechList.Find(t => t.TechID == ap.TechRequired);
                    if (kctTech != null)
                    {
                        AddPartCourses(ap);
                    }
                }
            }
        }
    }
}
