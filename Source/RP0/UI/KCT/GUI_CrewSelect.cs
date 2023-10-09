using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace RP0
{
    public static partial class KCT_GUI
    {
        private const string JetpackPartName = "evaJetpack";
        private const string ChutePartName = "evaChute";

        internal enum SortBy { Name, Type, Level };

        public static bool AssignRandomCrew;

        private static Rect _crewListWindowPosition = new Rect((Screen.width - 400) / 2, (Screen.height / 4), 400, 1);
        private static int _partIndexToCrew;
        private static int _indexToCrew;
        private static List<ProtoCrewMember> _availableCrew;
        private static List<ProtoCrewMember> _possibleCrewForParts = new List<ProtoCrewMember>();
        private static List<ProtoCrewMember> _possibleCrewForPart = new List<ProtoCrewMember>();
        private static List<ProtoCrewMember> _rosterForCrewSelect;
        private static List<PseudoPart> _pseudoParts;
        private static List<Part> _parts = new List<Part>();
        private static bool _chutePartAvailable;
        private static bool _jetpackPartAvailable;

        private static readonly string[] _sortNames = { "Name", "Type", "Level" };
        private static SortBy _first = SortBy.Name;
        private static SortBy _second = SortBy.Level;
        private static SortBy _third;

        private static System.Random _rand = new System.Random();

        // Find out if the Community Trait Icons are installed
        private static readonly bool _useCTI = CTIWrapper.initCTIWrapper() && CTIWrapper.CTI.Loaded;

        public static void DrawShipRoster(int windowID)
        {
            // This window can stay alive during scene transition
            if (SpaceCenterManagement.Instance == null)
                return;

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.MaxHeight(Screen.height / 2));
            GUILayout.BeginHorizontal();
            AssignRandomCrew = GUILayout.Toggle(AssignRandomCrew, " Randomize Filling");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (_availableCrew == null)
            {
                _availableCrew = GetAvailableCrew();
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

            _possibleCrewForParts.Clear();
            foreach (ProtoCrewMember pcm in _availableCrew)
                if (Crew.CrewHandler.CanCrewLaunchOnVessel(pcm, _parts))
                    _possibleCrewForParts.Add(pcm);

            bool foundAssignableCrew = false;

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(numberItems * 25 + 10), GUILayout.MaxHeight(Screen.height / 2));
            
            for (int j = 0; j < _parts.Count; j++)
            {
                Part p = _parts[j];
                if (p.CrewCapacity == 0) continue;

                List<CrewMemberAssignment> launchedCrew = SpaceCenterManagement.Instance.LaunchedCrew[j]?.CrewList;
                if (launchedCrew == null)
                {
                    launchedCrew = new List<CrewMemberAssignment>();
                    SpaceCenterManagement.Instance.LaunchedCrew.Add(new PartCrewAssignment(p.craftID, launchedCrew));
                }

                
                _possibleCrewForPart.Clear();
                foreach (ProtoCrewMember pcm in _possibleCrewForParts)
                    if (Crew.CrewHandler.CheckCrewForPart(pcm, p.partInfo.name, false, true))
                        _possibleCrewForPart.Add(pcm);
                

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
                                int index = AssignRandomCrew ? _rand.Next(_possibleCrewForPart.Count) : 0;
                                var crewMember = _possibleCrewForPart[index];
                                if (crewMember != null)
                                {
                                    launchedCrew.Add(new CrewMemberAssignment(crewMember));
                                    _possibleCrewForPart.RemoveAt(index);
                                    _availableCrew.Remove(crewMember);
                                    _possibleCrewForParts.Remove(crewMember);
                                }
                            }
                        }
                        else if (launchedCrew[i].PCM == null)
                        {
                            if (_possibleCrewForPart.Count > 0)
                            {
                                int index = AssignRandomCrew ? _rand.Next(_possibleCrewForPart.Count) : 0;
                                var crewMember = _possibleCrewForPart[index];
                                launchedCrew[i] = new CrewMemberAssignment(crewMember);
                                _possibleCrewForPart.RemoveAt(index);
                                _availableCrew.Remove(crewMember);
                                _possibleCrewForParts.Remove(crewMember);
                            }
                        }
                    }
                }

                if (GUILayout.Button("Clear", GUILayout.Width(75)))
                {
                    launchedCrew.Clear();
                    _possibleCrewForParts.Clear();
                    _possibleCrewForPart.Clear();
                    _availableCrew = GetAvailableCrew();
                }
                GUILayout.EndHorizontal();

                for (int i = 0; i < p.CrewCapacity; i++)
                {
                    GUILayout.BeginHorizontal();
                    if (i < launchedCrew.Count && launchedCrew[i].PCM != null)
                    {
                        foundAssignableCrew = true;
                        ProtoCrewMember kerbal = launchedCrew[i].PCM;
                        GUILayout.Label($"{kerbal.name}, {kerbal.experienceTrait.Title} {kerbal.experienceLevel}");    //Display the kerbal currently in the seat, followed by occupation and level

                        if (_chutePartAvailable)
                        {
                            launchedCrew[i].HasChute = GUILayout.Toggle(launchedCrew[i].HasChute, new GUIContent("Chute", "Include Parachute"), GUILayout.ExpandWidth(false), GUILayout.Height(20));
                        }

                        if (_jetpackPartAvailable)
                        {
                            launchedCrew[i].HasJetpack = GUILayout.Toggle(launchedCrew[i].HasJetpack, new GUIContent("EVA", "Include EVA pack"), GUILayout.ExpandWidth(false), GUILayout.Height(20));
                        }

                        if (GUILayout.Button("Remove", GUILayout.Width(120)))
                        {
                            launchedCrew[i].PCM.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                            launchedCrew[i].PCM = null;
                            _availableCrew = GetAvailableCrew();
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
                            _rosterForCrewSelect = new List<ProtoCrewMember>(_possibleCrewForPart.Where(c => !launchedCrew.Any(c2 => c2?.PCM == c)));
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

            if (!foundAssignableCrew)
            {
                if (_orangeText == null)
                {
                    _orangeText = new GUIStyle(GUI.skin.label);
                    _orangeText.normal.textColor = XKCDColors.Orange;
                }
                GUILayout.Label("No trained crew found for this vessel. Check proficiency and mission training status of your astronauts.", _orangeText);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Launch"))
            {
                CheckTanksAndLaunch(false);
            }
            if (SpaceCenterManagement.Instance.LaunchedVessel.IsValid && !SpaceCenterManagement.Instance.LaunchedVessel.AreTanksFull())
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
                SpaceCenterManagement.Instance.LaunchedVessel = new VesselProject();
                SpaceCenterManagement.Instance.LaunchedCrew.Clear();
                _crewListWindowPosition.height = 1;
                _availableCrew = null;
                _possibleCrewForParts.Clear();
                _possibleCrewForPart.Clear();

                KCTSettings.Instance.RandomizeCrew = AssignRandomCrew;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            CenterWindow(ref _crewListWindowPosition);
        }

        private static void RemoveAllCrewFromPods()
        {
            foreach (PartCrewAssignment cp in SpaceCenterManagement.Instance.LaunchedCrew)
            {
                cp.CrewList.Clear();
            }
            _possibleCrewForParts.Clear();
            _possibleCrewForPart.Clear();
            _availableCrew = GetAvailableCrew();
        }

        private static bool FillAllPodsWithCrew()
        {
            bool anyFound = false;
            _possibleCrewForParts.Clear();
            foreach (ProtoCrewMember pcm in _availableCrew)
                if (Crew.CrewHandler.CanCrewLaunchOnVessel(pcm, _parts))
                    _possibleCrewForParts.Add(pcm);

            for (int j = 0; j < _parts.Count; j++)
            {
                Part p = _parts[j];
                if (p.CrewCapacity > 0)
                {
                    _possibleCrewForPart.Clear();
                    foreach (ProtoCrewMember pcm in _possibleCrewForParts)
                        if (Crew.CrewHandler.CheckCrewForPart(pcm, p.partInfo.name, false, true))
                            _possibleCrewForPart.Add(pcm);

                    for (int i = 0; i < p.CrewCapacity; i++)
                    {
                        if (SpaceCenterManagement.Instance.LaunchedCrew[j].CrewList.Count <= i)
                        {
                            if (_possibleCrewForPart.Count > 0)
                            {
                                anyFound = true;
                                int index = AssignRandomCrew ? new System.Random().Next(_possibleCrewForPart.Count) : 0;
                                ProtoCrewMember crewMember = _possibleCrewForPart[index];
                                if (crewMember != null)
                                {
                                    SpaceCenterManagement.Instance.LaunchedCrew[j].CrewList.Add(new CrewMemberAssignment(crewMember));
                                    _possibleCrewForPart.RemoveAt(index);
                                    _availableCrew.Remove(crewMember);
                                    _possibleCrewForParts.Remove(crewMember);
                                }
                            }
                        }
                        else if (SpaceCenterManagement.Instance.LaunchedCrew[j].CrewList[i].PCM == null)
                        {
                            if (_possibleCrewForPart.Count > 0)
                            {
                                anyFound = true;
                                int index = AssignRandomCrew ? new System.Random().Next(_possibleCrewForPart.Count) : 0;
                                ProtoCrewMember crewMember = _possibleCrewForPart[index];
                                if (crewMember != null)
                                {
                                    SpaceCenterManagement.Instance.LaunchedCrew[j].CrewList[i] = new CrewMemberAssignment(crewMember);
                                    _possibleCrewForPart.RemoveAt(index);
                                    _availableCrew.Remove(crewMember);
                                    _possibleCrewForParts.Remove(crewMember);
                                }
                            }
                        }
                    }
                }
            }

            return anyFound;
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
                    List<CrewMemberAssignment> activeCrew = SpaceCenterManagement.Instance.LaunchedCrew[_partIndexToCrew].CrewList;
                    if (activeCrew.Count > _indexToCrew)
                    {
                        activeCrew.Insert(_indexToCrew, new CrewMemberAssignment(crew));
                        if (activeCrew[_indexToCrew + 1] == null)
                            activeCrew.RemoveAt(_indexToCrew + 1);
                    }
                    else
                    {
                        for (int i = activeCrew.Count; i < _indexToCrew; i++)
                        {
                            activeCrew.Insert(i, null);
                        }
                        activeCrew.Insert(_indexToCrew, new CrewMemberAssignment(crew));
                    }
                    _rosterForCrewSelect.Remove(crew);
                    SpaceCenterManagement.Instance.LaunchedCrew[_partIndexToCrew].CrewList = activeCrew;
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
            RefreshInventoryAvailability();
            SpaceCenterManagement.Instance.LaunchedCrew.Clear();
            _pseudoParts = SpaceCenterManagement.Instance.LaunchedVessel.GetPseudoParts();
            _parts.Clear();
            foreach (PseudoPart pp in _pseudoParts)
            {
                Part p = KCTUtilities.GetAvailablePartByName(pp.Name).partPrefab;
                p.craftID = pp.Uid;
                _parts.Add(p);
                SpaceCenterManagement.Instance.LaunchedCrew.Add(new PartCrewAssignment(pp.Uid, new List<CrewMemberAssignment>()));
            }

            //get all available crew from the roster and then fill all crewable parts with 'nauts that finished proficiency and mission training
            _availableCrew = GetAvailableCrew();
            bool anyFound = FillAllPodsWithCrew();
            // Only bother to test for no trained crew if we weren't able to assign any--if we were,
            // then obviously we had crew trained.
            if (!anyFound)
            {
                SpaceCenterManagement.Instance.StartCoroutine(CallbackUtil.DelayedCallback(0.001f, ShowUntrainedTip));
            }
        }

        private static void ShowUntrainedTip()
        {
            GameplayTips.Instance.ShowUntrainedTip(_parts);
        }


        private static void RefreshInventoryAvailability()
        {
            AvailablePart ap = PartLoader.getPartInfoByName(ChutePartName);
            _chutePartAvailable = ResearchAndDevelopment.GetTechnologyState(ap.TechRequired) == RDTech.State.Available;

            ap = PartLoader.getPartInfoByName(JetpackPartName);
            _jetpackPartAvailable = ResearchAndDevelopment.GetTechnologyState(ap.TechRequired) == RDTech.State.Available;
        }

        /// <summary>
        /// Get all available (not inactive) crew from the CrewRoster for the given partName, or all available crew in general if partName isn't passed 
        /// </summary>
        /// <param name="partName"></param>
        /// <returns></returns>
        private static List<ProtoCrewMember> GetAvailableCrew()
        {
            List<ProtoCrewMember> availableCrew = new List<ProtoCrewMember>();
            List<ProtoCrewMember> roster = HighLogic.CurrentGame.CrewRoster.Crew.ToList();

            foreach (ProtoCrewMember crewMember in roster) //Initialize available crew list
            {
                bool available = true;
                if (crewMember.rosterStatus == ProtoCrewMember.RosterStatus.Available && !crewMember.inactive)
                {
                    foreach (PartCrewAssignment cP in SpaceCenterManagement.Instance.LaunchedCrew)
                    {
                        if (cP.CrewList.Any(c => c?.PCM == crewMember))
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

            foreach (ProtoCrewMember crewMember in HighLogic.CurrentGame.CrewRoster.Tourist) //Get tourists
            {
                bool available = true;
                if (crewMember.rosterStatus == ProtoCrewMember.RosterStatus.Available && !crewMember.inactive)
                {
                    foreach (PartCrewAssignment cP in SpaceCenterManagement.Instance.LaunchedCrew)
                    {
                        if (cP.CrewList.Any(c => c?.PCM == crewMember))
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
            foreach (PartCrewAssignment crewedPart in SpaceCenterManagement.Instance.LaunchedCrew)
            {
                foreach (CrewMemberAssignment assign in crewedPart.CrewList)
                {
                    ProtoCrewMember pcm = assign?.PCM;
                    if (pcm == null) continue;
                    ModuleInventoryPart inv = pcm.KerbalInventoryModule;
                    inv.storedParts.Clear();

                    if (assign.HasJetpack)
                        AddPartToInventory(JetpackPartName, inv);
                    if (assign.HasChute)
                        AddPartToInventory(ChutePartName, inv);

                    pcm.SaveInventory(pcm.KerbalInventoryModule);
                }
            }

            KCTSettings.Instance.RandomizeCrew = AssignRandomCrew;
            SpaceCenterManagement.Instance.LaunchedVessel.Launch(fillTanks);

            GUIStates.ShowShipRoster = false;
            _crewListWindowPosition.height = 1;
        }

        private static void AddPartToInventory(string partName, ModuleInventoryPart inv)
        {
            AvailablePart ap = PartLoader.getPartInfoByName(partName);
            var pp = new ProtoPartSnapshot(ap.partPrefab, null);
            int slotIdx = inv.FirstEmptySlot();
            if (slotIdx < 0)
            {
                RP0Debug.LogError($"Part {inv.part.name} does not have inventory space to add {partName}");
                return;
            }

            StoredPart storedPart = new StoredPart(partName, slotIdx)
            {
                snapshot = pp,
                variantName = pp.moduleVariantName,
                quantity = 1,
                stackCapacity = pp.moduleCargoStackableQuantity
            };

            inv.storedParts.Add(storedPart.slotIndex, storedPart);
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
