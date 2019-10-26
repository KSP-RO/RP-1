using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using KSP.UI.Screens;

namespace RP0.Crew
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class CrewHandler : ScenarioModule
    {
        #region TrainingExpiration

        public class TrainingExpiration : IConfigNode
        {
            public string pcmName;

            public List<string> entries = new List<string>();

            public double expiration;

            public TrainingExpiration() { }

            public TrainingExpiration(ConfigNode node)
            {
                Load(node);
            }

            public static bool Compare(string str, FlightLog.Entry e)
            {
                int tyLen = (string.IsNullOrEmpty(e.type) ? 0 : e.type.Length);
                int tgLen = (string.IsNullOrEmpty(e.target) ? 0 : e.target.Length);
                int iC = str.Length;
                if (iC != 1 + tyLen + tgLen)
                    return false;
                int i = 0;
                for (; i < tyLen; ++i)
                {
                    if (str[i] != e.type[i])
                        return false;
                }

                if (str[i] != ',')
                    return false;
                ++i;
                for (int j = 0; j < tgLen && i < iC; ++j)
                {
                    if (str[i] != e.target[j])
                        return false;
                    ++i;
                }

                return true;
            }

            public bool Compare(int idx, FlightLog.Entry e)
            {
                return Compare(entries[idx], e);
            }

            public void Load(ConfigNode node)
            {
                foreach (ConfigNode.Value v in node.values)
                {
                    switch (v.name)
                    {
                        case "pcmName":
                            pcmName = v.value;
                            break;
                        case "expiration":
                            double.TryParse(v.value, out expiration);
                            break;

                        default:
                        case "entry":
                            entries.Add(v.value);
                            break;
                    }
                }
            }

            public void Save(ConfigNode node)
            {
                node.AddValue("pcmName", pcmName);
                node.AddValue("expiration", expiration);
                foreach (string s in entries)
                    node.AddValue("entry", s);
            }
        }

        #endregion

        #region Fields

        public CrewHandlerSettings settings = new CrewHandlerSettings();

        public Dictionary<string, double> kerbalRetireTimes = new Dictionary<string, double>();

        public bool retirementEnabled = true;

        protected HashSet<string> retirees = new HashSet<string>();

        protected static HashSet<string> toRemove = new HashSet<string>();

        protected List<TrainingExpiration> expireTimes = new List<TrainingExpiration>();

        protected bool inAC = false;

        protected KSP.UI.Screens.AstronautComplex astronautComplex = null;

        protected int countAvailable, countAssigned, countKIA;

        protected bool firstLoad = true;

        protected FieldInfo cliTooltip;

        [KSPField(isPersistant = true)]
        public double nextUpdate = -1d;

        public double updateInterval = 3600d;
        
        public List<CourseTemplate> CourseTemplates = new List<CourseTemplate>();
        public List<CourseTemplate> OfferedCourses = new List<CourseTemplate>();
        public List<ActiveCourse> ActiveCourses = new List<ActiveCourse>();
        protected HashSet<string> partSynsHandled = new HashSet<string>();
        protected TrainingDatabase trainingDatabase = new TrainingDatabase();

        public FSGUI fsGUI = new FSGUI();

        #region Instance

        private static CrewHandler _instance = null;
        public static CrewHandler Instance
        {
            get
            {
                return _instance;
            }
        }

        #endregion

        #endregion

        #region Overrides and Monobehaviour methods

        public override void OnAwake()
        {

            if (_instance != null)
            {
                GameObject.Destroy(_instance);
            }
            _instance = this;

            GameEvents.onVesselRecoveryProcessing.Add(VesselRecoveryProcessing);
            GameEvents.OnCrewmemberHired.Add(OnCrewHired);
            GameEvents.onGUIAstronautComplexSpawn.Add(ACSpawn);
            GameEvents.onGUIAstronautComplexDespawn.Add(ACDespawn);
            GameEvents.OnPartPurchased.Add(new EventData<AvailablePart>.OnEvent(onPartPurchased));
            GameEvents.OnGameSettingsApplied.Add(LoadSettings);
            GameEvents.onGameStateLoad.Add(LoadSettings);

            cliTooltip = typeof(KSP.UI.CrewListItem).GetField("tooltipController", BindingFlags.NonPublic | BindingFlags.Instance);
            
            FindAllCourseConfigs(); //find all applicable configs
            GenerateOfferedCourses(); //turn the configs into offered courses
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            foreach (ConfigNode stg in GameDatabase.Instance.GetConfigNodes("CREWHANDLERSETTINGS"))
                settings.Load(stg);

            kerbalRetireTimes.Clear();
            ConfigNode n = node.GetNode("RETIRETIMES");
            if (n != null)
            {
                foreach (ConfigNode.Value v in n.values)
                    kerbalRetireTimes[v.name] = double.Parse(v.value);
            }

            retirees.Clear();
            n = node.GetNode("RETIREES");
            if (n != null)
            {
                foreach (ConfigNode.Value v in n.values)
                    retirees.Add(v.value);
            }

            expireTimes.Clear();
            n = node.GetNode("EXPIRATIONS");
            if (n != null)
            {
                foreach (ConfigNode eN in n.nodes)
                {
                    expireTimes.Add(new TrainingExpiration(eN));
                }
            }

            ConfigNode FSData = node.GetNode("FlightSchoolData");

            if (FSData == null)
                return;

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

            TrainingDatabase.Initialize();
            KACWrapper.InitKACWrapper();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            ConfigNode n = node.AddNode("RETIRETIMES");
            foreach (KeyValuePair<string, double> kvp in kerbalRetireTimes)
                n.AddValue(kvp.Key, kvp.Value);

            n = node.AddNode("RETIREES");
            foreach (string s in retirees)
                n.AddValue("retiree", s);

            n = node.AddNode("EXPIRATIONS");
            foreach (TrainingExpiration e in expireTimes)
                e.Save(n.AddNode("Expiration"));

            ConfigNode FSData = new ConfigNode("FlightSchoolData");
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
            if (firstLoad)
            {
                firstLoad = false;
                List<string> newHires = new List<string>();

                foreach (ProtoCrewMember pcm in HighLogic.CurrentGame.CrewRoster.Crew)
                {
                    if ((pcm.rosterStatus == ProtoCrewMember.RosterStatus.Assigned || pcm.rosterStatus == ProtoCrewMember.RosterStatus.Available) && !kerbalRetireTimes.ContainsKey(pcm.name))
                    {
                        newHires.Add(pcm.name);
                        OnCrewHired(pcm, int.MinValue);
                    }
                }
                if (newHires.Count > 0)
                {
                    string msgStr = "Crew will retire as follows:";
                    foreach (string s in newHires)
                        msgStr += "\n" + s + ", no earlier than " + KSPUtil.PrintDate(kerbalRetireTimes[s], false);

                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                                        new Vector2(0.5f, 0.5f),
                                                        "InitialRetirementDateNotification",
                                                        "Initial Retirement Date",
                                                        msgStr
                                                        + "\n(Retirement will be delayed the more interesting flights they fly.)",
                                                        "OK",
                                                        false,
                                                        HighLogic.UISkin);
                }
            }

            // Retirements
            double time = Planetarium.GetUniversalTime();
            if (nextUpdate < time)
            {
                // Ensure that CrewHandler updates happen at predictable times so that accurate KAC alarms can be set.
                do
                {
                    nextUpdate += updateInterval;
                }
                while (nextUpdate < time);

                if (retirementEnabled)
                {
                    foreach (KeyValuePair<string, double> kvp in kerbalRetireTimes)
                    {
                        ProtoCrewMember pcm = HighLogic.CurrentGame.CrewRoster[kvp.Key];
                        if (pcm == null)
                            toRemove.Add(kvp.Key);
                        else
                        {
                            if (pcm.rosterStatus != ProtoCrewMember.RosterStatus.Available)
                            {
                                if (pcm.rosterStatus != ProtoCrewMember.RosterStatus.Assigned)
                                    toRemove.Add(kvp.Key);

                                continue;
                            }

                            if (pcm.inactive)
                                continue;

                            if (time > kvp.Value)
                            {
                                toRemove.Add(kvp.Key);
                                retirees.Add(kvp.Key);
                                pcm.rosterStatus = ProtoCrewMember.RosterStatus.Dead;
                            }
                        }
                    }
                }

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

                for (int i = expireTimes.Count; i-- > 0;)
                {
                    TrainingExpiration e = expireTimes[i];
                    if (time > e.expiration)
                    {
                        ProtoCrewMember pcm = HighLogic.CurrentGame.CrewRoster[e.pcmName];
                        if (pcm != null)
                        {
                            for (int j = pcm.careerLog.Entries.Count; j-- > 0;)
                            {
                                int eC = e.entries.Count;
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
                                        e.entries.RemoveAt(k);
                                    }
                                }
                            }
                        }
                        expireTimes.RemoveAt(i);
                    }
                }

                // TODO remove from courses? Except I think they won't retire if inactive either so that's ok.
                if (toRemove.Count > 0)
                {
                    string msgStr = string.Empty;
                    foreach (string s in toRemove)
                    {
                        kerbalRetireTimes.Remove(s);
                        if (HighLogic.CurrentGame.CrewRoster[s] != null)
                            msgStr += "\n" + s;
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

                    toRemove.Clear();
                }

                if (anyCourseEnded || toRemove.Count > 0)
                {
                    MaintenanceHandler.Instance.UpdateUpkeep();
                }
            }

            // UI fixing
            if (inAC)
            {
                if (astronautComplex == null)
                {
                    KSP.UI.Screens.AstronautComplex[] mbs = GameObject.FindObjectsOfType<KSP.UI.Screens.AstronautComplex>();
                    int maxCount = -1;
                    foreach (KSP.UI.Screens.AstronautComplex c in mbs)
                    {
                        int count = c.ScrollListApplicants.Count + c.ScrollListAssigned.Count + c.ScrollListAvailable.Count + c.ScrollListKia.Count;
                        if (count > maxCount)
                        {
                            maxCount = count;
                            astronautComplex = c;
                        }
                    }

                    if (astronautComplex == null)
                        return;
                }
                int newAv = astronautComplex.ScrollListAvailable.Count;
                int newAsgn = astronautComplex.ScrollListAssigned.Count;
                int newKIA = astronautComplex.ScrollListKia.Count;
                if (newAv != countAvailable || newKIA != countKIA || newAsgn != countAssigned)
                {
                    countAvailable = newAv;
                    countAssigned = newAsgn;
                    countKIA = newKIA;

                    foreach (KSP.UI.UIListData<KSP.UI.UIListItem> u in astronautComplex.ScrollListAvailable)
                    {
                        KSP.UI.CrewListItem cli = u.listItem.GetComponent<KSP.UI.CrewListItem>();
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

                    foreach (KSP.UI.UIListData<KSP.UI.UIListItem> u in astronautComplex.ScrollListAssigned)
                    {
                        KSP.UI.CrewListItem cli = u.listItem.GetComponent<KSP.UI.CrewListItem>();
                        if (cli != null)
                        {
                            FixTooltip(cli);
                        }
                    }

                    foreach (KSP.UI.UIListData<KSP.UI.UIListItem> u in astronautComplex.ScrollListKia)
                    {
                        KSP.UI.CrewListItem cli = u.listItem.GetComponent<KSP.UI.CrewListItem>();
                        if (cli != null)
                        {
                            if (retirees.Contains(cli.GetName()))
                            {
                                cli.SetLabel("Retired");
                                cli.MouseoverEnabled = false;
                            }
                        }
                    }
                }
            }
        }

        public void OnDestroy()
        {
            GameEvents.onVesselRecoveryProcessing.Remove(VesselRecoveryProcessing);
            GameEvents.OnCrewmemberHired.Remove(OnCrewHired);
            GameEvents.onGUIAstronautComplexSpawn.Remove(ACSpawn);
            GameEvents.onGUIAstronautComplexDespawn.Remove(ACDespawn);
            GameEvents.OnPartPurchased.Remove(new EventData<AvailablePart>.OnEvent(onPartPurchased));
            GameEvents.OnGameSettingsApplied.Remove(LoadSettings);
            GameEvents.onGameStateLoad.Remove(LoadSettings);
        }

        #endregion

        #region Interfaces

        public void AddExpiration(TrainingExpiration e)
        {
            expireTimes.Add(e);
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
            if (!partSynsHandled.Contains(name))
            {
                partSynsHandled.Add(name);
                bool isPartUnlocked = ResearchAndDevelopment.PartModelPurchased(ap);

                GenerateCourseProf(ap, !isPartUnlocked);
                if (isPartUnlocked)
                {
                    GenerateCourseMission(ap);
                }
            }
        }

        #endregion

        #region Methods

        protected void ACSpawn()
        {
            inAC = true;
            countAvailable = countKIA = -1;
        }

        protected void ACDespawn()
        {
            inAC = false;
            astronautComplex = null;
        }

        protected void LoadSettings(ConfigNode n)
        {
            LoadSettings();
        }

        protected void LoadSettings()
        {
            retirementEnabled = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsRetirementEnabled;
        }

        private void VesselRecoveryProcessing(ProtoVessel v, MissionRecoveryDialog mrDialog, float data)
        {
            Debug.Log("[VR] - Vessel recovery processing");

            List<string> retirementChanges = new List<string>();
            List<string> inactivity = new List<string>();

            double UT = Planetarium.GetUniversalTime();

            // normally we would use v.missionTime, but that doesn't seem to update
            // when you're not actually controlling the vessel
            double elapsedTime = UT - v.launchTime;

            // When flight duration was too short, mission training should not be set as expired.
            // This can happen when an on-the-pad failure occurs and the vessel is recovered.
            // We could perhaps override this if they're not actually in flight
            // (if the user didn't recover right from the pad I think this is a fair assumption)
            if (elapsedTime < settings.minFlightDurationSecondsForTrainingExpire)
            {
                Debug.Log("[VR] - mission time too short for crew to be inactive (elapsed time was " + elapsedTime + ", settings set for " + settings.minFlightDurationSecondsForTrainingExpire + ")");
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
                foreach (FlightLog.Entry e in pcm.careerLog.Entries)
                {
                    if (e.type == "TRAINING_mission")
                        SetExpiration(pcm.name, e, Planetarium.GetUniversalTime());

                    if (e.flight != curFlight)
                        continue;

                    bool isOther = false;
                    if (!string.IsNullOrEmpty(e.target) && e.target != Planetarium.fetch.Home.name)
                        isOther = hasOther = true;

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
                    multiplier += settings.recSpace.x;
                    constant += settings.recSpace.y;
                }
                if (hasOrbit)
                {
                    multiplier += settings.recOrbit.x;
                    constant += settings.recOrbit.y;
                }
                if (hasOther)
                {
                    multiplier += settings.recOtherBody.x;
                    constant += settings.recOtherBody.y;
                }
                if (hasOrbit && hasEVA)    // EVA should only count while in orbit, not when walking on Earth
                {
                    multiplier += settings.recEVA.x;
                    constant += settings.recEVA.y;
                }
                if (hasEVAOther)
                {
                    multiplier += settings.recEVAOther.x;
                    constant += settings.recEVAOther.y;
                }
                if (hasOrbitOther)
                {
                    multiplier += settings.recOrbitOther.x;
                    constant += settings.recOrbitOther.y;
                }
                if (hasLandOther)
                {
                    multiplier += settings.recLandOther.x;
                    constant += settings.recLandOther.y;
                }

                double retTime;
                if (kerbalRetireTimes.TryGetValue(pcm.name, out retTime))
                {
                    double offset = constant * 86400d * settings.retireOffsetBaseMult / (1 + Math.Pow(Math.Max(curFlight + settings.retireOffsetFlightNumOffset, 0d), settings.retireOffsetFlightNumPow)
                        * UtilMath.Lerp(settings.retireOffsetStupidMin, settings.retireOffsetStupidMax, pcm.stupidity));
                    if (offset > 0d)
                    {
                        retTime += offset;
                        kerbalRetireTimes[pcm.name] = retTime;
                        retirementChanges.Add("\n" + pcm.name + ", no earlier than " + KSPUtil.PrintDate(retTime, false));
                    }
                }

                multiplier /= (ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex) + 1d);

                double inactiveTime = elapsedTime * multiplier + constant * 86400d;
                pcm.SetInactive(inactiveTime, false);
                inactivity.Add("\n" + pcm.name + ", until " + KSPUtil.PrintDate(inactiveTime + UT, true, false));
            }
            if (inactivity.Count > 0)
            {
                Debug.Log("[VR] - showing on leave message");

                string msgStr = "The following crew members will be on leave:";
                foreach (string s in inactivity)
                {
                    msgStr += s;
                }

                if (retirementEnabled && retirementChanges.Count > 0)
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

        protected void OnCrewHired(ProtoCrewMember pcm, int idx)
        {
            double retireTime = Planetarium.GetUniversalTime() + GetServiceTime(pcm);
            kerbalRetireTimes[pcm.name] = retireTime;

            if (retirementEnabled && idx != int.MinValue)
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                                        new Vector2(0.5f, 0.5f),
                                                        "InitialRetirementDateNotification",
                                                        "Initial Retirement Date",
                                                        pcm.name + " will retire no earlier than " + KSPUtil.PrintDate(retireTime, false)
                                                        + "\n(Retirement will be delayed the more interesting flights they fly.)",
                                                        "OK",
                                                        false,
                                                        HighLogic.UISkin);
            }
        }

        protected double GetServiceTime(ProtoCrewMember pcm)
        {
            return 86400d * 365d * (settings.retireBaseYears
                + UtilMath.Lerp(settings.retireCourageMin, settings.retireCourageMax, pcm.courage)
                + UtilMath.Lerp(settings.retireStupidMin, settings.retireStupidMax, pcm.stupidity));
        }

        protected void FixTooltip(KSP.UI.CrewListItem cli)
        {
            ProtoCrewMember pcm = cli.GetCrewRef();
            double retTime;
            if (retirementEnabled && kerbalRetireTimes.TryGetValue(pcm.name, out retTime))
            {
                cli.SetTooltip(pcm);
                KSP.UI.TooltipTypes.TooltipController_CrewAC ttc = cliTooltip.GetValue(cli) as KSP.UI.TooltipTypes.TooltipController_CrewAC;
                ttc.descriptionString += "\n\nRetires no earlier than " + KSPUtil.PrintDate(retTime, false);

                // Training

                string trainingStr = GetTrainingString(pcm);
                if (!string.IsNullOrEmpty(trainingStr))
                    ttc.descriptionString += trainingStr;
            }
        }

        public string GetTrainingString(ProtoCrewMember pcm)
        {
            bool found = false;
            string trainingStr = "\n\nTraining:";
            int lastFlight = pcm.careerLog.Last() == null ? 0 : pcm.careerLog.Last().flight;
            foreach (FlightLog.Entry ent in pcm.careerLog.Entries)
            {
                string pretty = GetPrettyCourseName(ent.type);
                if (!string.IsNullOrEmpty(pretty))
                {
                    if (ent.type == "TRAINING_proficiency")
                    {
                        found = true;
                        trainingStr += "\n  " + pretty + ent.target;
                    }
                    else if (ent.type == "TRAINING_mission")
                    {
                        double exp = GetExpiration(pcm.name, ent);
                        if (exp > 0d)
                        {
                            trainingStr += "\n  " + pretty + ent.target + ". Expires " + KSPUtil.PrintDate(exp, false);
                        }
                    }
                }
            }

            if (found)
                return trainingStr;
            else
                return string.Empty;
        }

        public double GetExpiration(string pcmName, FlightLog.Entry ent)
        {
            for (int i = expireTimes.Count; i-- > 0;)
            {
                TrainingExpiration e = expireTimes[i];
                if (e.pcmName == pcmName)
                {
                    for (int j = e.entries.Count; j-- > 0;)
                    {
                        if (e.Compare(j, ent))
                            return e.expiration;
                    }
                }
            }

            return 0d;
        }

        protected bool SetExpiration(string pcmName, FlightLog.Entry ent, double expirationUT)
        {
            for (int i = expireTimes.Count; i-- > 0;)
            {
                TrainingExpiration e = expireTimes[i];
                if (e.pcmName == pcmName)
                {
                    for (int j = e.entries.Count; j-- > 0;)
                    {
                        if (e.Compare(j, ent))
                        {
                            e.expiration = expirationUT;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        protected void FindAllCourseConfigs()
        {
            CourseTemplates.Clear();
            //find all configs and save them
            foreach (ConfigNode course in GameDatabase.Instance.GetConfigNodes("FS_COURSE"))
            {
                CourseTemplates.Add(new CourseTemplate(course));
            }
            Debug.Log("[FS] Found " + CourseTemplates.Count + " courses.");
            //fire an event to let other mods add their configs
        }
        
        protected void GenerateOfferedCourses() //somehow provide some variable options here?
        {
            //convert the saved configs to course offerings
            foreach (CourseTemplate template in CourseTemplates)
            {
                CourseTemplate duplicate = new CourseTemplate(template.sourceNode, true); //creates a duplicate so the initial template is preserved
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

            Debug.Log("[FS] Offering " + OfferedCourses.Count + " courses.");
            //fire an event to let other mods add available courses (where they can pass variables through then)
        }

        protected void GenerateCourseProf(AvailablePart ap, bool isTemporary)
        {
            ConfigNode n = new ConfigNode("FS_COURSE");
            string name = TrainingDatabase.SynonymReplace(ap.name);

            n.AddValue("id", "prof_" + name);
            n.AddValue("name", "Proficiency: " + name);
            n.AddValue("time", 1d + (TrainingDatabase.GetTime(name) * 86400d));
            n.AddValue("isTemporary", isTemporary);

            n.AddValue("conflicts", "TRAINING_proficiency:" + name);

            ConfigNode r = n.AddNode("REWARD");
            r.AddValue("XPAmt", settings.trainingProficiencyXP);
            ConfigNode l = r.AddNode("FLIGHTLOG");
            l.AddValue("0", "TRAINING_proficiency," + name);

            CourseTemplate c = new CourseTemplate(n);
            c.PopulateFromSourceNode();
            OfferedCourses.Add(c);
        }

        protected void GenerateCourseMission(AvailablePart ap)
        {
            ConfigNode n = new ConfigNode("FS_COURSE");
            string name = TrainingDatabase.SynonymReplace(ap.name);

            n.AddValue("id", "msn_" + name);
            n.AddValue("name", "Mission: " + name);
            n.AddValue("time", 1d + TrainingDatabase.GetTime(name + "-Mission") * 86400d);
            n.AddValue("isTemporary", false);
            n.AddValue("timeUseStupid", true);
            n.AddValue("seatMax", ap.partPrefab.CrewCapacity * 2);
            n.AddValue("expiration", settings.trainingMissionExpirationDays * 86400d);

            n.AddValue("preReqs", "TRAINING_proficiency:" + name);
            n.AddValue("conflicts", "TRAINING_mission:" + name);

            ConfigNode r = n.AddNode("REWARD");
            ConfigNode l = r.AddNode("FLIGHTLOG");
            l.AddValue("0", "TRAINING_mission," + name);

            CourseTemplate c = new CourseTemplate(n);
            c.PopulateFromSourceNode();
            OfferedCourses.Add(c);
        }

        protected void onPartPurchased(AvailablePart ap)
        {
            if (ap.partPrefab.CrewCapacity > 0)
            {
                AddPartCourses(ap);
            }
        }

        protected string GetPrettyCourseName(string str)
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

        public bool RemoveExpiration(string pcmName, string entry)
        {
            for (int i = expireTimes.Count; i-- > 0;)
            {
                TrainingExpiration e = expireTimes[i];

                if (e.pcmName != pcmName)
                    continue;

                for (int j = e.entries.Count; j-- > 0;)
                {
                    if (e.entries[j] == entry)
                    {
                        e.entries.RemoveAt(j);

                        if (e.entries.Count == 0)
                            expireTimes.RemoveAt(i);

                        return true;
                    }
                }
            }

            return false;
        }

        #endregion
    }
}
