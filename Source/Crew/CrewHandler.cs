using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using KSP.UI;
using KSP.UI.Screens;
using KSP.UI.TooltipTypes;

namespace RP0.Crew
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class CrewHandler : ScenarioModule
    {
        public static CrewHandler Instance { get; private set; } = null;

        [KSPField(isPersistant = true)]
        public double NextUpdate = -1d;

        public CrewHandlerSettings Settings = new CrewHandlerSettings();
        public Dictionary<string, double> KerbalRetireTimes = new Dictionary<string, double>();
        public List<CourseTemplate> CourseTemplates = new List<CourseTemplate>();
        public List<CourseTemplate> OfferedCourses = new List<CourseTemplate>();
        public List<ActiveCourse> ActiveCourses = new List<ActiveCourse>();
        public bool RetirementEnabled = true;
        public double UpdateInterval = 3600d;

        private HashSet<string> _toRemove = new HashSet<string>();
        private HashSet<string> _retirees = new HashSet<string>();
        private HashSet<string> _partSynsHandled = new HashSet<string>();
        private List<TrainingExpiration> _expireTimes = new List<TrainingExpiration>();
        private bool _firstLoad = true;
        private bool _inAC = false;
        private int _countAvailable, _countAssigned, _countKIA;
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

            _cliTooltip = typeof(CrewListItem).GetField("tooltipController", BindingFlags.NonPublic | BindingFlags.Instance);
            
            FindAllCourseConfigs(); //find all applicable configs
            GenerateOfferedCourses(); //turn the configs into offered courses
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
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            foreach (ConfigNode stg in GameDatabase.Instance.GetConfigNodes("CREWHANDLERSETTINGS"))
                Settings.Load(stg);

            KerbalRetireTimes.Clear();
            ConfigNode n = node.GetNode("RETIRETIMES");
            if (n != null)
            {
                foreach (ConfigNode.Value v in n.values)
                    KerbalRetireTimes[v.name] = double.Parse(v.value);
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
            KACWrapper.InitKACWrapper();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            ConfigNode n = node.AddNode("RETIRETIMES");
            foreach (KeyValuePair<string, double> kvp in KerbalRetireTimes)
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

            // Catch earlies
            if (_firstLoad)
            {
                _firstLoad = false;
                ProcessFirstLoad();
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
                bool isPartUnlocked = ResearchAndDevelopment.PartModelPurchased(ap);

                GenerateCourseProf(ap, !isPartUnlocked);
                if (isPartUnlocked)
                {
                    GenerateCourseMission(ap);
                }
            }
        }

        public string GetTrainingString(ProtoCrewMember pcm)
        {
            bool found = false;
            string trainingStr = "\n\nTraining:";
            foreach (FlightLog.Entry ent in pcm.careerLog.Entries)
            {
                string pretty = GetPrettyCourseName(ent.type);
                if (!string.IsNullOrEmpty(pretty))
                {
                    if (ent.type == "TRAINING_proficiency")
                    {
                        found = true;
                        trainingStr += $"\n  {pretty}{ent.target}";
                    }
                    else if (ent.type == "TRAINING_mission")
                    {
                        double exp = GetExpiration(pcm.name, ent);
                        if (exp > 0d)
                        {
                            trainingStr += $"\n  {pretty}{ent.target}. Expires {KSPUtil.PrintDate(exp, false)}";
                        }
                    }
                }
            }

            if (found)
                return trainingStr;
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
            Debug.Log("[VR] - Vessel recovery processing");

            var retirementChanges = new List<string>();
            var inactivity = new List<string>();

            double UT = Planetarium.GetUniversalTime();

            // normally we would use v.missionTime, but that doesn't seem to update
            // when you're not actually controlling the vessel
            double elapsedTime = UT - v.launchTime;

            Debug.Log($"[VR] mission elapsedTime: {KSPUtil.PrintDateDeltaCompact(elapsedTime, true, true)}");

            // When flight duration was too short, mission training should not be set as expired.
            // This can happen when an on-the-pad failure occurs and the vessel is recovered.
            // We could perhaps override this if they're not actually in flight
            // (if the user didn't recover right from the pad I think this is a fair assumption)
            if (elapsedTime < Settings.minFlightDurationSecondsForTrainingExpire)
            {
                Debug.Log("[VR] - mission time too short for crew to be inactive (elapsed time was " + elapsedTime + ", settings set for " + Settings.minFlightDurationSecondsForTrainingExpire + ")");
                return;
            }

            foreach (ProtoCrewMember pcm in v.GetVesselCrew())
            {
                Debug.Log("[VR] - Found ProtoCrewMember: " + pcm.displayName);

                bool hasSpace = false;
                bool hasOrbit = false;
                bool hasEVA = false;
                bool hasEVAOther = false;
                bool hasOther = false;
                bool hasOrbitOther = false;
                bool hasLandOther = false;
                int curFlight = pcm.careerLog.Last().flight;
                int numFlightsDone = pcm.careerLog.Entries.Count(e => e.type == "Recover");
                foreach (FlightLog.Entry e in pcm.careerLog.Entries)
                {
                    if (e.type == "TRAINING_mission")
                        SetExpiration(pcm.name, e, Planetarium.GetUniversalTime());

                    if (e.flight != curFlight || e.type == "Nationality")
                        continue;

                    Debug.Log($"[VR]  processing flight entry: {e.type}; {e.target}");

                    bool isOther = false;
                    if (!string.IsNullOrEmpty(e.target) && e.target != Planetarium.fetch.Home.name)
                    {
                        Debug.Log($"[VR]    flight is beyond Earth");
                        isOther = hasOther = true;
                    }

                    if (!string.IsNullOrEmpty(e.type))
                    {
                        switch (e.type)
                        {
                            case "Suborbit":
                                hasSpace = true;
                                break;
                            case "Orbit":
                                if (isOther)
                                    hasOrbitOther = true;
                                else
                                    hasOrbit = true;
                                break;
                            case "ExitVessel":
                                if (isOther)
                                    hasEVAOther = true;
                                else
                                    hasEVA = true;
                                break;
                            case "Land":
                                if (isOther)
                                    hasLandOther = true;
                                break;
                            default:
                                break;
                        }
                    }
                }
                double multiplier = 1d;
                double constant = 0.5d;
                if (hasSpace)
                {
                    multiplier += Settings.recSpace.x;
                    constant += Settings.recSpace.y;
                    Debug.Log($"[VR]  has space, mult {Settings.recSpace.x}; constant {Settings.recSpace.y}");
                }
                if (hasOrbit)
                {
                    multiplier += Settings.recOrbit.x;
                    constant += Settings.recOrbit.y;
                    Debug.Log($"[VR]  has orbit, mult {Settings.recOrbit.x}; constant {Settings.recOrbit.y}");
                }
                if (hasOther)
                {
                    multiplier += Settings.recOtherBody.x;
                    constant += Settings.recOtherBody.y;
                    Debug.Log($"[VR]  has other body, mult {Settings.recOtherBody.x}; constant {Settings.recOtherBody.y}");
                }
                if (hasOrbit && hasEVA)    // EVA should only count while in orbit, not when walking on Earth
                {
                    multiplier += Settings.recEVA.x;
                    constant += Settings.recEVA.y;
                    Debug.Log($"[VR]  has EVA, mult {Settings.recEVA.x}; constant {Settings.recEVA.y}");
                }
                if (hasEVAOther)
                {
                    multiplier += Settings.recEVAOther.x;
                    constant += Settings.recEVAOther.y;
                    Debug.Log($"[VR]  has EVA at another body, mult {Settings.recEVAOther.x}; constant {Settings.recEVAOther.y}");
                }
                if (hasOrbitOther)
                {
                    multiplier += Settings.recOrbitOther.x;
                    constant += Settings.recOrbitOther.y;
                    Debug.Log($"[VR]  has orbit around another body, mult {Settings.recOrbitOther.x}; constant {Settings.recOrbitOther.y}");
                }
                if (hasLandOther)
                {
                    multiplier += Settings.recLandOther.x;
                    constant += Settings.recLandOther.y;
                    Debug.Log($"[VR]  has landed on another body, mult {Settings.recLandOther.x}; constant {Settings.recLandOther.y}");
                }

                Debug.Log("[VR]  multiplier: " + multiplier);
                Debug.Log("[VR]  AC multiplier: " + (ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) + 1d));
                Debug.Log("[VR]  constant: " + constant);

                if (KerbalRetireTimes.TryGetValue(pcm.name, out double retTime))
                {
                    double offset = constant * 86400d * Settings.retireOffsetBaseMult / 
                        (1 + Math.Pow(Math.Max(numFlightsDone + Settings.retireOffsetFlightNumOffset, 0d), Settings.retireOffsetFlightNumPow) * 
                         UtilMath.Lerp(Settings.retireOffsetStupidMin, Settings.retireOffsetStupidMax, pcm.stupidity));

                    if (offset > 0d)
                    {
                        Debug.Log("[VR] retire date increased by: " + KSPUtil.PrintDateDeltaCompact(offset, true, false));
                        Debug.Log($"[VR]  constant: {constant}; curFlight: {numFlightsDone}; stupidity: {pcm.stupidity}");

                        retTime += offset;
                        KerbalRetireTimes[pcm.name] = retTime;
                        retirementChanges.Add($"\n{pcm.name}, no earlier than {KSPUtil.PrintDate(retTime, false)}");
                    }
                }

                multiplier /= ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) + 1d;

                double inactiveTime = elapsedTime * multiplier + constant * 86400d;
                Debug.Log("[VR] inactive for: " + KSPUtil.PrintDateDeltaCompact(inactiveTime, true, false));

                pcm.SetInactive(inactiveTime, false);
                inactivity.Add($"\n{pcm.name}, until {KSPUtil.PrintDate(inactiveTime + UT, true, false)}");
            }
            if (inactivity.Count > 0)
            {
                Debug.Log("[VR] - showing on leave message");

                string msgStr = "The following crew members will be on leave:";
                foreach (string s in inactivity)
                {
                    msgStr += s;
                }

                if (RetirementEnabled && retirementChanges.Count > 0)
                {
                    msgStr += "\n\nThe following retirement changes have occurred:";
                    foreach (string s in retirementChanges)
                        msgStr += s;
                }

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "CrewUpdateNotification",
                                             "Crew Updates",
                                             msgStr,
                                             "OK",
                                             true,
                                             HighLogic.UISkin);
            }
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
                string msgStr = "Crew will retire as follows:";
                foreach (string s in newHires)
                    msgStr += $"\n{s}, no earlier than {KSPUtil.PrintDate(KerbalRetireTimes[s], false)}";

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "InitialRetirementDateNotification",
                                             "Initial Retirement Date",
                                             $"{msgStr}\n(Retirement will be delayed the more interesting flights they fly.)",
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
                            int eC = e.Entries.Count;
                            if (eC == 0)
                                break;
                            FlightLog.Entry ent = pcm.careerLog[j];
                            for (int k = eC; k-- > 0;)
                            {
                                // Allow only mission trainings to expire. 
                                // This check is actually only needed for old savegames as only these can have expirations on proficiencies.
                                if (ent.type == "TRAINING_mission" && e.Compare(k, ent))
                                {
                                    ScreenMessages.PostScreenMessage(pcm.name + ": Expired: " + GetPrettyCourseName(ent.type) + ent.target);
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
                AstronautComplex[] mbs = GameObject.FindObjectsOfType<KSP.UI.Screens.AstronautComplex>();
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
                    if (cli != null)
                    {
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
            Debug.Log($"[FS] Found {CourseTemplates.Count} courses.");
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
                if (ap.partPrefab.CrewCapacity > 0 && /*&& ap.TechRequired != "start"*/
                    ResearchAndDevelopment.PartModelPurchased(ap))
                {
                    AddPartCourses(ap);
                }
            }

            Debug.Log($"[FS] Offering {OfferedCourses.Count} courses.");
        }

        private void GenerateCourseProf(AvailablePart ap, bool isTemporary)
        {
            var n = new ConfigNode("FS_COURSE");
            string name = TrainingDatabase.SynonymReplace(ap.name);

            n.AddValue("id", "prof_" + name);
            n.AddValue("name", "Proficiency: " + name);
            n.AddValue("time", 1d + (TrainingDatabase.GetTime(name) * 86400d));
            n.AddValue("isTemporary", isTemporary);
            n.AddValue("conflicts", "TRAINING_proficiency:" + name);

            ConfigNode r = n.AddNode("REWARD");
            r.AddValue("XPAmt", Settings.trainingProficiencyXP);
            ConfigNode l = r.AddNode("FLIGHTLOG");
            l.AddValue("0", "TRAINING_proficiency," + name);

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
            n.AddValue("time", 1d + TrainingDatabase.GetTime(name + "-Mission") * 86400d);
            n.AddValue("isTemporary", false);
            n.AddValue("timeUseStupid", true);
            n.AddValue("seatMax", ap.partPrefab.CrewCapacity * 2);
            n.AddValue("expiration", Settings.trainingMissionExpirationDays * 86400d);
            n.AddValue("preReqs", "TRAINING_proficiency:" + name);

            ConfigNode r = n.AddNode("REWARD");
            ConfigNode l = r.AddNode("FLIGHTLOG");
            l.AddValue("0", "TRAINING_mission," + name);

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
                case "TRAINING_proficiency":
                    return "Proficiency with ";
                case "TRAINING_mission":
                    return "Mission training for ";
                /*case "expired_TRAINING_mission":
                    return "(Expired) Mission training for ";*/
                default:
                    return string.Empty;
            }
        }
    }
}
