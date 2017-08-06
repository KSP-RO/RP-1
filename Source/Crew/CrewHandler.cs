using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
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

        [KSPField(isPersistant = true)]
        public bool firstLoad = true;

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
                                                        msgStr,
                                                        "OK",
                                                        false,
                                                        HighLogic.UISkin);
                }
            }

            // Retirements
            double time = Planetarium.GetUniversalTime();
            foreach (KeyValuePair<string, double> kvp in kerbalRetireTimes)
            {
                ProtoCrewMember pcm = HighLogic.CurrentGame.CrewRoster[kvp.Key];
                if(pcm == null)
                    toRemove.Add(kvp.Key);
                else
                {
                    if (pcm.rosterStatus != ProtoCrewMember.RosterStatus.Available)
                    {
                        if (pcm.rosterStatus != ProtoCrewMember.RosterStatus.Assigned)
                            toRemove.Add(kvp.Key);

                        continue;
                    }

                    if (time > kvp.Value)
                    {
                        toRemove.Add(kvp.Key);
                        retirees.Add(kvp.Key);
                        pcm.rosterStatus = ProtoCrewMember.RosterStatus.Missing;


                        PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                                    new Vector2(0.5f, 0.5f),
                                                    "Crew Retirement",
                                                    kvp.Key + " has retired.",
                                                    "OK",
                                                    true,
                                                    HighLogic.UISkin);
                    }
                }
            }
            foreach (string s in toRemove)
                kerbalRetireTimes.Remove(s);

            toRemove.Clear();


            // UI fixing
            /*if (inAC)
            {
                if (astronautComplex == null)
                {
                    KSP.UI.Screens.AstronautComplex[] mbs = Resources.FindObjectsOfTypeAll<KSP.UI.Screens.AstronautComplex>();
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
                            ProtoCrewMember pcm = cli.GetCrewRef();
                            double retTime;
                            if(kerbalRetireTimes.TryGetValue(pcm.name, out retTime))
                            {
                                cli.SetTooltip(
                        }
                    }
                }
            }*/
        }

        public void OnDestroy()
        {
            GameEvents.OnVesselRecoveryRequested.Remove(VesselRecoveryRequested);
            GameEvents.OnCrewmemberHired.Remove(OnCrewHired);
            GameEvents.onGUIAstronautComplexSpawn.Remove(ACSpawn);
            GameEvents.onGUIAstronautComplexDespawn.Remove(ACDespawn);
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

        public void VesselRecoveryRequested(Vessel v)
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
                double constant = 0d;
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
                    double offset = constant * 86400d * 100d / (1 + curFlight * curFlight);
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
                                                        pcm.name + " will retire no earlier than " + KSPUtil.PrintDate(retireTime, false),
                                                        "OK",
                                                        false,
                                                        HighLogic.UISkin);
            }

        }

        protected double GetServiceTime(ProtoCrewMember pcm)
        {
            return 86400d * 365d * (5d + pcm.courage * 3d + (1d - pcm.stupidity));
        }

        #endregion
    }
}
