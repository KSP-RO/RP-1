using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;
using System.Reflection;

namespace RP0.Crew
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class CrewHandler : ScenarioModule
    {
        #region Fields

        protected Dictionary<string, double> kerbalRetireTimes = new Dictionary<string, double>();

        protected HashSet<string> retirees = new HashSet<string>();

        protected static HashSet<string> toRemove = new HashSet<string>();

        protected bool inAC = false;

        protected KSP.UI.Screens.AstronautComplex astronautComplex = null;

        protected int countAvailable, countAssigned, countKIA;

        protected bool firstLoad = true;

        protected FieldInfo cliTooltip;

        [KSPField(isPersistant = true)]
        public double nextUpdate = -1d;

        protected double updateInterval = 3600d;



        public List<CourseTemplate> CourseTemplates = new List<CourseTemplate>();
        public List<CourseTemplate> OfferedCourses = new List<CourseTemplate>();
        public List<ActiveCourse> ActiveCourses = new List<ActiveCourse>();

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

            GameEvents.OnVesselRecoveryRequested.Add(VesselRecoveryRequested);
            GameEvents.OnCrewmemberHired.Add(OnCrewHired);
            GameEvents.onGUIAstronautComplexSpawn.Add(ACSpawn);
            GameEvents.onGUIAstronautComplexDespawn.Add(ACDespawn);
            GameEvents.OnPartPurchased.Add(new EventData<AvailablePart>.OnEvent(onPartPurchased));

            cliTooltip = typeof(KSP.UI.CrewListItem).GetField("tooltipController", BindingFlags.NonPublic | BindingFlags.Instance);

            FindAllCourseConfigs(); //find all applicable configs
            GenerateOfferedCourses(); //turn the configs into offered courses
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            kerbalRetireTimes.Clear();
            foreach (ConfigNode.Value v in node.GetNode("RETIRETIMES").values)
                kerbalRetireTimes[v.name] = double.Parse(v.value);

            retirees.Clear();
            foreach (ConfigNode.Value v in node.GetNode("RETIREES").values)
                retirees.Add(v.value);

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
                                                        "Initial Retirement Date",
                                                        msgStr
                                                        + "\n(Retirement will be delayed the more intersting flights they fly.)",
                                                        "OK",
                                                        false,
                                                        HighLogic.UISkin);
                }
            }

            // Retirements
            double time = Planetarium.GetUniversalTime();
            if (nextUpdate < time)
            {
                nextUpdate = time + updateInterval;

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
                if (toRemove.Count > 0)
                {
                    string msgStr = "The following retirements have occurred:\n";
                    foreach (string s in toRemove)
                    {
                        kerbalRetireTimes.Remove(s);
                        msgStr += "\n" + s;
                    }

                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                                        new Vector2(0.5f, 0.5f),
                                                        "Crew Retirement",
                                                        msgStr,
                                                        "OK",
                                                        true,
                                                        HighLogic.UISkin);

                    toRemove.Clear();
                }

                for (int i = ActiveCourses.Count; i-- > 0;)
                {
                    ActiveCourse course = ActiveCourses[i];
                    if (course.ProgressTime(time)) //returns true when the course completes
                    {
                        ActiveCourses.RemoveAt(i);
                    }
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
                            AddRetireTime(cli);
                            if (cli.GetCrewRef().inactive)
                            {
                                cli.MouseoverEnabled = false;
                                cli.SetLabel("Recovering");
                            }
                        }
                    }

                    foreach (KSP.UI.UIListData<KSP.UI.UIListItem> u in astronautComplex.ScrollListAssigned)
                    {
                        KSP.UI.CrewListItem cli = u.listItem.GetComponent<KSP.UI.CrewListItem>();
                        if (cli != null)
                        {
                            AddRetireTime(cli);
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
            GameEvents.OnVesselRecoveryRequested.Remove(VesselRecoveryRequested);
            GameEvents.OnCrewmemberHired.Remove(OnCrewHired);
            GameEvents.onGUIAstronautComplexSpawn.Remove(ACSpawn);
            GameEvents.onGUIAstronautComplexDespawn.Remove(ACDespawn);
            GameEvents.OnPartPurchased.Remove(new EventData<AvailablePart>.OnEvent(onPartPurchased));
        }

        #endregion

        #region Methods

        protected void ACSpawn()
        {
            inAC = true;
            countAvailable = countKIA = 0;
        }

        protected void ACDespawn()
        {
            inAC = false;
            astronautComplex = null;
        }

        protected void VesselRecoveryRequested(Vessel v)
        {
            double elapsedTime = v.missionTime;
            List<string> retirementChanges = new List<string>();
            List<string> inactivity = new List<string>();

            double UT = Planetarium.GetUniversalTime();

            foreach (ProtoCrewMember pcm in v.GetVesselCrew())
            {
                bool hasSpace = false;
                bool hasOrbit = false;
                bool hasEVA = false;
                bool hasEVAOther = false;
                bool hasOther = false;
                bool hasOrbitOther = false;
                bool hasLandOther = false;
                int curFlight = pcm.flightLog.Last().flight;
                foreach (FlightLog.Entry e in pcm.flightLog.Entries)
                {
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
                    multiplier += 2d;
                    constant += 2d;
                }
                if (hasOrbit)
                {
                    multiplier += 5d;
                    constant += 10d;
                }
                if (hasOther)
                {
                    multiplier += 10d;
                    constant += 10d;
                }
                if (hasEVA)
                {
                    multiplier += 15d;
                    constant += 20d;
                }
                if (hasEVAOther)
                {
                    multiplier += 20d;
                    constant += 25d;
                }
                if (hasOrbitOther)
                {
                    multiplier += 15d;
                    constant += 10d;
                }
                if (hasLandOther)
                {
                    multiplier += 30d;
                    constant += 30d;
                }

                double retTime;
                if (kerbalRetireTimes.TryGetValue(pcm.name, out retTime))
                {
                    double offset = constant * 86400d * 100d / (1 + curFlight * curFlight) * (0.8d + (1d - pcm.stupidity) * 0.6d);
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
                string msgStr = "The following crew members will be on leave:";
                foreach (string s in inactivity)
                {
                    msgStr += s;
                }


                if (retirementChanges.Count > 0)
                {
                    msgStr += "\n\nThe following retirement changes have occurred:";
                    foreach (string s in retirementChanges)
                        msgStr += s;
                }

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                                        new Vector2(0.5f, 0.5f),
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
            if (idx != int.MinValue)
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                                        new Vector2(0.5f, 0.5f),
                                                        "Initial Retirement Date",
                                                        pcm.name + " will retire no earlier than " + KSPUtil.PrintDate(retireTime, false)
                                                        + "\n(Retirement will be delayed the more intersting flights they fly.)",
                                                        "OK",
                                                        false,
                                                        HighLogic.UISkin);
            }

        }

        protected double GetServiceTime(ProtoCrewMember pcm)
        {
            return 86400d * 365d * (5d + pcm.courage * 3d + (1d - pcm.stupidity));
        }

        protected void AddRetireTime(KSP.UI.CrewListItem cli)
        {
            ProtoCrewMember pcm = cli.GetCrewRef();
            double retTime;
            if (kerbalRetireTimes.TryGetValue(pcm.name, out retTime))
            {
                cli.SetTooltip(pcm);
                KSP.UI.TooltipTypes.TooltipController_CrewAC ttc = cliTooltip.GetValue(cli) as KSP.UI.TooltipTypes.TooltipController_CrewAC;
                ttc.descriptionString += "\n\nRetires no earlier than " + KSPUtil.PrintDate(retTime, false);
            }
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
                if (ap.partPrefab.CrewCapacity > 0 /*&& ap.TechRequired != "start"*/)
                {
                    if (ResearchAndDevelopment.PartModelPurchased(ap))
                    {
                        OfferedCourses.Add(GenerateCourseForPart(ap));
                    }
                }
            }

            Debug.Log("[FS] Offering " + OfferedCourses.Count + " courses.");
            //fire an event to let other mods add available courses (where they can pass variables through then)
        }

        protected CourseTemplate GenerateCourseForPart(AvailablePart ap)
        {
            ConfigNode n = new ConfigNode("FS_COURSE");
            {
                n.AddValue("id", "prof_" + ap.name);
                n.AddValue("name", ap.title);
                n.AddValue("time", 3600d + EntryCostStorage.GetCost(ap.name) * 5177d);

                ConfigNode r = n.AddNode("REWARD");
                r.AddValue("XPAmt", "1");
                ConfigNode l = r.AddNode("FLIGHTLOG");
                l.AddValue("0", "TRAINING_proficiency," + ap.name);
            }
            CourseTemplate c = new CourseTemplate(n);
            c.PopulateFromSourceNode();

            return c;
        }

        protected void onPartPurchased(AvailablePart ap)
        {
            OfferedCourses.Add(GenerateCourseForPart(ap));
        }

        #endregion
    }
}
