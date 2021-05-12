using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public static partial class KCT_GUI
    {
        internal enum SortBy { Name, Type, Level };

        public delegate bool boolDelegatePCMString(ProtoCrewMember pcm, string partName);
        public static boolDelegatePCMString AvailabilityChecker;
        public static bool UseAvailabilityChecker = false;
        public static bool AssignRandomCrew;

        private static Rect _crewListWindowPosition = new Rect((Screen.width - 400) / 2, (Screen.height / 4), 400, 1);
        private static int _partIndexToCrew;
        private static int _indexToCrew;
        private static List<ProtoCrewMember> _availableCrew;
        private static List<ProtoCrewMember> _possibleCrewForPart = new List<ProtoCrewMember>();
        private static List<ProtoCrewMember> _rosterForCrewSelect;
        private static List<PseudoPart> _pseudoParts;
        private static List<Part> _parts;

        private static readonly string[] _sortNames = { "Name", "Type", "Level" };
        private static SortBy _first = SortBy.Name;
        private static SortBy _second = SortBy.Level;
        private static SortBy _third;

        // Find out if the Community Trait Icons are installed
        private static readonly bool _useCTI = CTIWrapper.initCTIWrapper() && CTIWrapper.CTI.Loaded;

        public static void DrawShipRoster(int windowID)
        {
            System.Random rand = new System.Random();
            GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.MaxHeight(Screen.height / 2));
            GUILayout.BeginHorizontal();
            AssignRandomCrew = GUILayout.Toggle(AssignRandomCrew, " Randomize Filling");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (_availableCrew == null)
            {
                _availableCrew = GetAvailableCrew(string.Empty);
            }

            if (GUILayout.Button("Fill All"))
            {
                FillAllPodsWithCrew();
            }

            if (GUILayout.Button("Clear All"))
            {
                RemoveAllCrewFromPods();
            }
            GUILayout.EndHorizontal();
            int numberItems = 0;
            for (int i = _parts.Count - 1; i >= 0; i--)
            {
                Part p = _parts[i];
                if (p.CrewCapacity > 0)
                {
                    numberItems += 1 + p.CrewCapacity;
                }
            }

            bool foundAssignableCrew = false;
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(numberItems * 25 + 10), GUILayout.MaxHeight(Screen.height / 2));
            for (int j = 0; j < _parts.Count; j++)
            {
                Part p = _parts[j];
                if (p.CrewCapacity == 0) continue;

                List<ProtoCrewMember> launchedCrew = KCTGameStates.LaunchedCrew.Find(part => part.PartID == p.craftID)?.CrewList;
                if (launchedCrew == null)
                {
                    launchedCrew = new List<ProtoCrewMember>();
                    KCTGameStates.LaunchedCrew.Add(new CrewedPart(p.craftID, launchedCrew));
                }

                if (UseAvailabilityChecker)
                {
                    _possibleCrewForPart.Clear();
                    foreach (ProtoCrewMember pcm in _availableCrew)
                        if (AvailabilityChecker(pcm, p.partInfo.name))
                            _possibleCrewForPart.Add(pcm);
                }
                else
                    _possibleCrewForPart = _availableCrew;

                foundAssignableCrew |= _possibleCrewForPart.Count > 0;

                GUILayout.BeginHorizontal();
                GUILayout.Label(p.partInfo.title.Length <= 25 ? p.partInfo.title : p.partInfo.title.Substring(0, 25));
                if (GUILayout.Button("Fill", GUILayout.Width(75)))
                {
                    for (int i = 0; i < p.CrewCapacity; i++)
                    {
                        if (launchedCrew.Count <= i)
                        {
                            if (_possibleCrewForPart.Count > 0)
                            {
                                int index = AssignRandomCrew ? new System.Random().Next(_possibleCrewForPart.Count) : 0;
                                ProtoCrewMember crewMember = _possibleCrewForPart[index];
                                if (crewMember != null)
                                {
                                    launchedCrew.Add(crewMember);
                                    _possibleCrewForPart.RemoveAt(index);
                                    if (_possibleCrewForPart != _availableCrew)
                                        _availableCrew.Remove(crewMember);
                                }
                            }
                        }
                        else if (launchedCrew[i] == null)
                        {
                            if (_possibleCrewForPart.Count > 0)
                            {
                                int index = AssignRandomCrew ? new System.Random().Next(_possibleCrewForPart.Count) : 0;
                                launchedCrew[i] = _possibleCrewForPart[index];
                                if (_possibleCrewForPart != _availableCrew)
                                    _availableCrew.Remove(_possibleCrewForPart[index]);
                                _possibleCrewForPart.RemoveAt(index);
                            }
                        }
                    }
                }

                if (GUILayout.Button("Clear", GUILayout.Width(75)))
                {
                    launchedCrew.Clear();
                    _possibleCrewForPart.Clear();
                    _availableCrew = GetAvailableCrew(string.Empty);
                }
                GUILayout.EndHorizontal();

                for (int i = 0; i < p.CrewCapacity; i++)
                {
                    GUILayout.BeginHorizontal();
                    if (i < launchedCrew.Count && launchedCrew[i] != null)
                    {
                        foundAssignableCrew = true;
                        ProtoCrewMember kerbal = launchedCrew[i];
                        GUILayout.Label($"{kerbal.name}, {kerbal.experienceTrait.Title} {kerbal.experienceLevel}");    //Display the kerbal currently in the seat, followed by occupation and level
                        if (GUILayout.Button("Remove", GUILayout.Width(120)))
                        {
                            launchedCrew[i].rosterStatus = ProtoCrewMember.RosterStatus.Available;
                            launchedCrew[i] = null;
                            _availableCrew = GetAvailableCrew(string.Empty);
                        }
                    }
                    else
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Empty");
                        if (_possibleCrewForPart.Count > 0 && GUILayout.Button("Add", GUILayout.Width(120)))
                        {
                            GUIStates.ShowShipRoster = false;
                            GUIStates.ShowCrewSelect = true;
                            _rosterForCrewSelect = new List<ProtoCrewMember>(_possibleCrewForPart.Where(c => !launchedCrew.Contains(c)));
                            _partIndexToCrew = j;
                            _indexToCrew = i;
                            _crewListWindowPosition.height = 1;
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();

            if (UseAvailabilityChecker && !foundAssignableCrew)
            {
                if (_orangeText == null)
                {
                    _orangeText = new GUIStyle(GUI.skin.label);
                    _orangeText.normal.textColor = XKCDColors.Orange;
                }
                GUILayout.Label("No trained crew found for this cockpit or capsule. Check proficiency and mission training status of your astronauts.", _orangeText);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Launch"))
            {
                CheckTanksAndLaunch(false);
            }
            if (KCTGameStates.LaunchedVessel != null && !KCTGameStates.LaunchedVessel.AreTanksFull())
            {
                if (GUILayout.Button("Fill Tanks & Launch"))
                {
                    CheckTanksAndLaunch(true);
                }
            }

            if (GUILayout.Button("Cancel"))
            {
                GUIStates.ShowShipRoster = false;
                GUIStates.ShowBuildList = true;
                KCTGameStates.LaunchedVessel = null;
                KCTGameStates.LaunchedCrew.Clear();
                _crewListWindowPosition.height = 1;
                _availableCrew = null;
                _possibleCrewForPart.Clear();

                KCTGameStates.Settings.RandomizeCrew = AssignRandomCrew;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            CenterWindow(ref _crewListWindowPosition);
        }

        private static void RemoveAllCrewFromPods()
        {
            foreach (CrewedPart cp in KCTGameStates.LaunchedCrew)
            {
                cp.CrewList.Clear();
            }
            _possibleCrewForPart.Clear();
            _availableCrew = GetAvailableCrew(string.Empty);
        }

        private static void FillAllPodsWithCrew()
        {
            for (int j = 0; j < _parts.Count; j++)
            {
                Part p = _parts[j];
                if (p.CrewCapacity > 0)
                {
                    if (UseAvailabilityChecker)
                    {
                        _possibleCrewForPart.Clear();
                        foreach (ProtoCrewMember pcm in _availableCrew)
                            if (AvailabilityChecker(pcm, p.partInfo.name))
                                _possibleCrewForPart.Add(pcm);
                    }
                    else
                        _possibleCrewForPart = _availableCrew;

                    for (int i = 0; i < p.CrewCapacity; i++)
                    {
                        if (KCTGameStates.LaunchedCrew[j].CrewList.Count <= i)
                        {
                            if (_possibleCrewForPart.Count > 0)
                            {
                                int index = AssignRandomCrew ? new System.Random().Next(_possibleCrewForPart.Count) : 0;
                                ProtoCrewMember crewMember = _possibleCrewForPart[index];
                                if (crewMember != null)
                                {
                                    KCTGameStates.LaunchedCrew[j].CrewList.Add(crewMember);
                                    _possibleCrewForPart.RemoveAt(index);
                                    if (_possibleCrewForPart != _availableCrew)
                                        _availableCrew.Remove(crewMember);
                                }
                            }
                        }
                        else if (KCTGameStates.LaunchedCrew[j].CrewList[i] == null)
                        {
                            if (_possibleCrewForPart.Count > 0)
                            {
                                int index = AssignRandomCrew ? new System.Random().Next(_possibleCrewForPart.Count) : 0;
                                ProtoCrewMember crewMember = _possibleCrewForPart[index];
                                if (crewMember != null)
                                {
                                    KCTGameStates.LaunchedCrew[j].CrewList[i] = crewMember;
                                    _possibleCrewForPart.RemoveAt(index);
                                    if (_possibleCrewForPart != _availableCrew)
                                        _availableCrew.Remove(crewMember);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void DrawCrewSelect(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.MaxHeight(Screen.height / 2));
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            GUILayout.Label("Sort:");

            if (GUILayout.Button("▲", GUILayout.Width(20)))
            {
                _third = _first;
                _first--;
                if (_first < 0)
                    _first = SortBy.Level;
                if (_first == _second)
                    _second = _third;
                SortPossibleCrew();
            }
            GUILayout.Label(_sortNames[(int)_first]);
            if (GUILayout.Button("▼", GUILayout.Width(20)))
            {
                _third = _first;
                _first++;
                if (_first > SortBy.Level)
                    _first = SortBy.Name;
                if (_first == _second)
                    _second = _third;
                SortPossibleCrew();
            }
            GUILayout.Space(10);
            if (GUILayout.Button("▲", GUILayout.Width(20)))
            {
                _second--;
                if (_second < 0)
                    _second = SortBy.Level;
                if (_second == _first)
                {
                    _second--;
                    if (_second < 0)
                        _second = SortBy.Level;
                }
                SortPossibleCrew();
            }
            GUILayout.Label(_sortNames[(int)_second]);
            if (GUILayout.Button("▼", GUILayout.Width(20)))
            {
                _second++;
                if (_second > SortBy.Level)
                    _second = SortBy.Name;
                if (_second == _first)
                {
                    _second++;
                    if (_second > SortBy.Level)
                        _second = SortBy.Name;
                }
                SortPossibleCrew();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(_rosterForCrewSelect.Count * 28 * 2 + 35), GUILayout.MaxHeight(Screen.height / 2));

            float cWidth = 80;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:");
            GUILayout.Label("Courage:", GUILayout.Width(cWidth));
            GUILayout.Label("Stupidity:", GUILayout.Width(cWidth));
            //GUILayout.Space(cWidth/2);
            GUILayout.EndHorizontal();


            var oldBtnAlignment = GUI.skin.button.alignment;
            foreach (ProtoCrewMember crew in _rosterForCrewSelect)
            {
                GUILayout.BeginHorizontal();
                //GUILayout.Label(crew.name);

                // Use Community Trait Icons if available
                string traitInfo = $"{crew.experienceTrait.Title} ({crew.experienceLevel}) {new string('★', crew.experienceLevel)}";
                string name = crew.name;

                float traitWidth = GetStringSize(traitInfo);
                while (GetStringSize(name) < traitWidth)
                    name += " ";

                string btnTxt = name + "\n" + traitInfo;


                bool clickedNautButton;
                GUIContent gc;
                if (_useCTI)
                {
                    var t = CTIWrapper.CTI.getTrait(crew.experienceTrait﻿.Config.Name﻿);
                    if (t != null)
                        gc = new GUIContent(btnTxt, t.Icon);
                    else
                        gc = new GUIContent(btnTxt);

                    GUI.skin.button.alignment = TextAnchor.MiddleLeft;
                    clickedNautButton = GUILayout.Button(gc, GUILayout.Height(56));
                    GUI.skin.button.alignment = oldBtnAlignment;
                }
                else
                {
                    gc = new GUIContent(btnTxt);
                    clickedNautButton = GUILayout.Button(gc, GUILayout.Height(56));
                }


                if (clickedNautButton)
                {
                    List<ProtoCrewMember> activeCrew = KCTGameStates.LaunchedCrew[_partIndexToCrew].CrewList;
                    if (activeCrew.Count > _indexToCrew)
                    {
                        activeCrew.Insert(_indexToCrew, crew);
                        if (activeCrew[_indexToCrew + 1] == null)
                            activeCrew.RemoveAt(_indexToCrew + 1);
                    }
                    else
                    {
                        for (int i = activeCrew.Count; i < _indexToCrew; i++)
                        {
                            activeCrew.Insert(i, null);
                        }
                        activeCrew.Insert(_indexToCrew, crew);
                    }
                    _rosterForCrewSelect.Remove(crew);
                    KCTGameStates.LaunchedCrew[_partIndexToCrew].CrewList = activeCrew;
                    GUIStates.ShowCrewSelect = false;
                    GUIStates.ShowShipRoster = true;
                    _crewListWindowPosition.height = 1;
                    break;
                }
                GUILayout.HorizontalSlider(crew.courage, 0, 1, HighLogic.Skin.horizontalSlider, HighLogic.Skin.horizontalSliderThumb, GUILayout.Width(cWidth));
                GUILayout.HorizontalSlider(crew.stupidity, 0, 1, HighLogic.Skin.horizontalSlider, HighLogic.Skin.horizontalSliderThumb, GUILayout.Width(cWidth));

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("Cancel"))
            {
                GUIStates.ShowCrewSelect = false;
                GUIStates.ShowShipRoster = true;
                _crewListWindowPosition.height = 1;
            }
            GUILayout.EndVertical();
            CenterWindow(ref _crewListWindowPosition);
        }

        public static Texture2D GetKerbalIcon(ProtoCrewMember pcm)
        {
            string type = "suit";
            switch (pcm.type)
            {
                case (ProtoCrewMember.KerbalType.Applicant):
                    type = "recruit";
                    break;
                case (ProtoCrewMember.KerbalType.Tourist):
                    type = "tourist";
                    break;
                default:
                    if (pcm.rosterStatus == ProtoCrewMember.RosterStatus.Assigned && pcm.KerbalRef.InVessel.vesselType == VesselType.EVA)
                        type = "eva";
                    else if (pcm.veteran)
                        type += "_orange";
                    break;
            }
            string textureName = "kerbalicon_" + type + (pcm.gender == ProtoCrewMember.Gender.Female ? "_female" : string.Empty);

            string suffix = pcm.GetKerbalIconSuitSuffix();
            if (string.IsNullOrEmpty(suffix))
                return AssetBase.GetTexture(textureName);
            else
                return Expansions.Missions.MissionsUtils.METexture("Kerbals/Textures/kerbalIcons/" + textureName + suffix + ".tif");
        }

        private static void SortPossibleCrew()
        {
            _rosterForCrewSelect.Sort(
                delegate (ProtoCrewMember p1, ProtoCrewMember p2)
                {
                    int c1 = 0;
                    switch (_first)
                    {
                        case SortBy.Name:
                            c1 = p1.name.CompareTo(p2.name);
                            break;
                        case SortBy.Level:
                            c1 = p1.experienceLevel.CompareTo(p2.experienceLevel);
                            break;
                        case SortBy.Type:
                            c1 = p1.experienceTrait﻿.Config.Name﻿.CompareTo(p2.experienceTrait﻿.Config.Name﻿);
                            break;
                    }
                    if (c1 == 0)
                    {
                        switch (_second)
                        {
                            case SortBy.Name:
                                c1 = p1.name.CompareTo(p2.name);
                                break;
                            case SortBy.Level:
                                c1 = p1.experienceLevel.CompareTo(p2.experienceLevel);
                                break;
                            case SortBy.Type:
                                c1 = p1.experienceTrait﻿.Config.Name﻿.CompareTo(p2.experienceTrait﻿.Config.Name﻿);
                                break;
                        }
                    }
                    return c1;
                }
            );
        }

        private static float GetStringSize(string s)
        {
            GUIContent content = new GUIContent(s);

            GUIStyle style = GUI.skin.box;
            style.alignment = TextAnchor.MiddleLeft;

            // Compute how large the button needs to be.
            Vector2 size = style.CalcSize(content);

            return size.x;
        }
        private static bool IsCrewable(List<Part> ship)
        {
            foreach (Part p in ship)
                if (p.CrewCapacity > 0) return true;
            return false;
        }

        private static int GetFirstCrewableIndex(List<Part> ship)
        {
            for (int i = 0; i < ship.Count; i++)
            {
                if (ship[i].CrewCapacity > 0) return i;
            }
            return -1;
        }

        /// <summary>
        /// Assigns the initial crew to the roster, based on desired roster in the editor
        /// </summary>
        public static void AssignInitialCrew()
        {
            KCTGameStates.LaunchedCrew.Clear();
            _pseudoParts = KCTGameStates.LaunchedVessel.GetPseudoParts();
            _parts = KCTGameStates.LaunchedVessel.ExtractedParts;
            KCTGameStates.LaunchedCrew = new List<CrewedPart>();
            foreach (PseudoPart pp in _pseudoParts)
                KCTGameStates.LaunchedCrew.Add(new CrewedPart(pp.Uid, new List<ProtoCrewMember>()));
            //try to assign kerbals from the desired manifest
            if (!UseAvailabilityChecker && KCTGameStates.LaunchedVessel.DesiredManifest?.Count > 0 && KCTGameStates.LaunchedVessel.DesiredManifest.Exists(c => c != null))
            {
                KCTDebug.Log("Assigning desired crew manifest.");
                List<ProtoCrewMember> available = GetAvailableCrew(string.Empty);
                Queue<ProtoCrewMember> finalCrew = new Queue<ProtoCrewMember>();
                //try to assign crew from the desired manifest
                foreach (string name in KCTGameStates.LaunchedVessel.DesiredManifest)
                {
                    //assign the kerbal with that name to each seat, in order. Let's try that
                    ProtoCrewMember crew = null;
                    if (!string.IsNullOrEmpty(name))
                    {
                        crew = available.Find(c => c.name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
                        if (crew != null && crew.rosterStatus != ProtoCrewMember.RosterStatus.Available) //only take those that are available
                        {
                            crew = null;
                        }
                    }

                    finalCrew.Enqueue(crew);
                }

                //check if any of these crew are even available, if not then go back to CrewFirstAvailable
                if (finalCrew.FirstOrDefault(c => c != null) == null)
                {
                    KCTDebug.Log("Desired crew not available, falling back to default.");
                    CrewFirstAvailable();
                    return;
                }

                //Put the crew where they belong
                for (int i = 0; i < _parts.Count; i++)
                {
                    Part part = _parts[i];
                    for (int seat = 0; seat < part.CrewCapacity; seat++)
                    {
                        if (finalCrew.Count > 0)
                        {
                            ProtoCrewMember crewToInsert = finalCrew.Dequeue();
                            KCTDebug.Log("Assigning " + (crewToInsert?.name ?? "null"));
                            KCTGameStates.LaunchedCrew[i].CrewList.Add(crewToInsert); //even add the nulls, then they should match 1 to 1
                        }
                    }
                }
            }
            else
            {
                CrewFirstAvailable();
            }
        }

        public static void CrewFirstAvailable()
        {
            int partIndex = GetFirstCrewableIndex(_parts);
            if (partIndex > -1)
            {
                Part p = _parts[partIndex];
                if (KCTGameStates.LaunchedCrew.Find(part => part.PartID == p.craftID) == null)
                    KCTGameStates.LaunchedCrew.Add(new CrewedPart(p.craftID, new List<ProtoCrewMember>()));
                _availableCrew = GetAvailableCrew(p.partInfo.name);
                for (int i = 0; i < p.CrewCapacity; i++)
                {
                    if (KCTGameStates.LaunchedCrew[partIndex].CrewList.Count <= i)
                    {
                        if (_availableCrew.Count > 0)
                        {
                            int index = AssignRandomCrew ? new System.Random().Next(_availableCrew.Count) : 0;
                            ProtoCrewMember crewMember = _availableCrew[index];
                            if (crewMember != null)
                            {
                                KCTGameStates.LaunchedCrew[partIndex].CrewList.Add(crewMember);
                                _availableCrew.RemoveAt(index);
                            }
                        }
                    }
                    else if (KCTGameStates.LaunchedCrew[partIndex].CrewList[i] == null)
                    {
                        if (_availableCrew.Count > 0)
                        {
                            int index = AssignRandomCrew ? new System.Random().Next(_availableCrew.Count) : 0;
                            KCTGameStates.LaunchedCrew[partIndex].CrewList[i] = _availableCrew[index];
                            _availableCrew.RemoveAt(index);
                        }
                    }
                }
            }
        }

        private static List<ProtoCrewMember> GetAvailableCrew(string partName)
        {
            List<ProtoCrewMember> availableCrew = new List<ProtoCrewMember>();
            List<ProtoCrewMember> roster = HighLogic.CurrentGame.CrewRoster.Crew.ToList();

            foreach (ProtoCrewMember crewMember in roster) //Initialize available crew list
            {
                bool available = true;
                if (!UseAvailabilityChecker || string.IsNullOrEmpty(partName) || AvailabilityChecker(crewMember, partName))
                {
                    if (crewMember.rosterStatus == ProtoCrewMember.RosterStatus.Available && !crewMember.inactive)
                    {
                        foreach (CrewedPart cP in KCTGameStates.LaunchedCrew)
                        {
                            if (cP.CrewList.Contains(crewMember))
                            {
                                available = false;
                                break;
                            }
                        }
                    }
                    else
                        available = false;
                    if (available)
                        availableCrew.Add(crewMember);
                }
            }

            foreach (ProtoCrewMember crewMember in HighLogic.CurrentGame.CrewRoster.Tourist) //Get tourists
            {
                bool available = true;
                if (crewMember.rosterStatus == ProtoCrewMember.RosterStatus.Available && !crewMember.inactive)
                {
                    foreach (CrewedPart cP in KCTGameStates.LaunchedCrew)
                    {
                        if (cP.CrewList.Contains(crewMember))
                        {
                            available = false;
                            break;
                        }
                    }
                }
                else
                    available = false;
                if (available)
                    availableCrew.Add(crewMember);
            }

            return availableCrew;
        }

        private static void CheckTanksAndLaunch(bool fillTanks)
        {
            KCTGameStates.Settings.RandomizeCrew = AssignRandomCrew;
            KCTGameStates.LaunchedVessel.Launch(fillTanks);

            GUIStates.ShowShipRoster = false;
            _crewListWindowPosition.height = 1;
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
