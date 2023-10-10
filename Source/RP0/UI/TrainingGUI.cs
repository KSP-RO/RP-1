using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RP0.Crew
{
    public class TrainingGUI : UIBase
    {
        private TrainingCourse _selectedCourse = null;
        private ProtoCrewMember _selectedNaut = null;
        private Vector2 _nautListScroll = new Vector2();
        private readonly Dictionary<ProtoCrewMember, TrainingCourse> _activeMap = new Dictionary<ProtoCrewMember, TrainingCourse>();
        private Vector2 _courseSelectorScroll = new Vector2();
        private GUIStyle _courseBtnStyle = null;
        private GUIStyle _courseBtnUnavailStyle = null;
        private GUIStyle _tempCourseLblStyle = null;
        private GUIStyle _lockedCourseLblStyle = null;
        private readonly GUIContent _nautRowAlarmBtnContent = new GUIContent(GameDatabase.Instance.GetTexture("RP-1/KACIcon15", false), "Add alarm");
        private bool _showAllTrainings = false;
        private static readonly Color _colorUnavailable = new Color(1f, 176f / 255f, 153f / 255f);

        protected override void OnStart()
        {
            var evt = GameEvents.FindEvent<EventVoid>("OnKctRecalculateBuildRates");
            if (evt != null)
            {
                evt.Add(OnRecalcBuildRates);
            }
        }

        protected override void OnDestroy()
        {
            var evt = GameEvents.FindEvent<EventVoid>("OnKctRecalculateBuildRates");
            if (evt != null)
            {
                evt.Remove(OnRecalcBuildRates);
            }
        }

        private void OnRecalcBuildRates()
        {
            if (_selectedCourse != null)
                _selectedCourse.RecalculateBuildRate();
        }

        protected void RenderNautListHeading()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(24));
            GUILayout.Label("Name", BoldLabel, GUILayout.Width(144));
            GUILayout.Label("Training", BoldLabel, GUILayout.Width(96));
            GUILayout.Label("Completes", BoldLabel, GUILayout.Width(80));
            GUILayout.Label("Retires NET", BoldLabel, GUILayout.Width(80));
            GUILayout.EndHorizontal();
        }

        protected void RenderNautListRow(UITab currentTab, ProtoCrewMember student)
        {
            TrainingCourse currentCourse = null;
            if (_activeMap.ContainsKey(student))
                currentCourse = _activeMap[student];
            bool selectedForCourse = _selectedCourse != null && _selectedCourse.Students.Contains(student);
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label($"{student.trait.Substring(0, 1)} {student.experienceLevel}", GUILayout.Width(24));
                if (currentCourse == null && _selectedCourse != null && (selectedForCourse || _selectedCourse.MeetsStudentReqs(student)))
                {
                    var c = new GUIContent(student.name, "Select for training");
                    if (RenderToggleButton(c, selectedForCourse, GUILayout.Width(144)))
                    {
                        if (selectedForCourse)
                            _selectedCourse.RemoveStudent(student);
                        else
                            _selectedCourse.AddStudent(student);
                    }
                }
                else if (currentTab == UITab.Astronauts)
                {
                    if (GUILayout.Button(student.name, HighLogic.Skin.button, GUILayout.Width(144)))
                    {
                        _selectedNaut = student;
                    }
                }
                else
                {
                    GUILayout.Label(student.name, GUILayout.Width(144));
                }

                string course, complete, retires;
                bool isInactive = false;
                if (currentCourse == null)
                {
                    if (student.rosterStatus == ProtoCrewMember.RosterStatus.Assigned)
                    {
                        course = "(in-flight)";
                        complete = "(n/a)";
                    }
                    else if (student.inactive)
                    {
                        course = "(inactive)";
                        complete = KSPUtil.PrintDate(student.inactiveTimeEnd, false);
                        isInactive = true;
                    }
                    else
                    {
                        course = "(free)";
                        complete = "(n/a)";
                    }
                }
                else
                {
                    course = currentCourse.GetItemName();
                    complete = KSPUtil.PrintDate(Planetarium.GetUniversalTime() + currentCourse.GetTimeLeft(), false);
                }
                GUILayout.Label(course, GUILayout.Width(96));
                GUILayout.Label(complete, GUILayout.Width(80));

                double retireTime = CrewHandler.Instance.GetRetireTime(student.name);
                if (retireTime > 0d)
                {
                    retires = CrewHandler.Instance.RetirementEnabled ? KSPUtil.PrintDate(retireTime, false) : "(n/a)";
                }
                else
                {
                    retires = "(unknown)";
                }
                GUILayout.Label(retires, GUILayout.Width(80));

                if (currentCourse != null)
                {
                    if (currentCourse.SeatMin > 1)
                    {
                        if (GUILayout.Button(new GUIContent("X", "Cancel training"), HighLogic.Skin.button, GUILayout.ExpandWidth(false)))
                            CancelCourse(currentCourse);
                    }
                    else
                    {
                        if (GUILayout.Button(new GUIContent("X", "Remove from training"), HighLogic.Skin.button, GUILayout.ExpandWidth(false)))
                            LeaveCourse(currentCourse, student);
                    }

                    if (KACWrapper.APIReady && GUILayout.Button(_nautRowAlarmBtnContent, HighLogic.Skin.button, GUILayout.ExpandWidth(false)))
                    {
                        CreateCourseFinishAlarm(student, currentCourse);
                    }
                }
                else if (KACWrapper.APIReady && isInactive && GUILayout.Button(_nautRowAlarmBtnContent, HighLogic.Skin.button, GUILayout.ExpandWidth(false)))
                {
                    CreateReturnToDutyAlarm(student);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();
        }

        private void RenderSummaryBody(UITab currentTab)
        {
            UpdateActiveCourseMap();
            float scrollHeight = currentTab == UITab.Astronauts ? 420 : 305;
            _nautListScroll = GUILayout.BeginScrollView(_nautListScroll, GUILayout.Width(505), GUILayout.Height(scrollHeight));
            try
            {
                RenderNautListHeading();
                for (int i = 0; i < HighLogic.CurrentGame.CrewRoster.Count; i++)
                {
                    ProtoCrewMember student = HighLogic.CurrentGame.CrewRoster[i];
                    if (student.type == ProtoCrewMember.KerbalType.Crew &&
                        (student.rosterStatus == ProtoCrewMember.RosterStatus.Available ||
                         (currentTab == UITab.Astronauts && student.rosterStatus == ProtoCrewMember.RosterStatus.Assigned)))
                    {
                        RenderNautListRow(currentTab, student);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndScrollView();
        }

        public UITab RenderSummaryTab()
        {
            _selectedCourse = null;
            _selectedNaut = null;
            RenderSummaryBody(UITab.Astronauts);
            return _selectedNaut == null ? UITab.Astronauts : UITab.Naut;
        }

        protected void RenderCourseSelector()
        {
            if (_courseBtnStyle == null)
            {
                _courseBtnStyle = new GUIStyle(HighLogic.Skin.button);
                _courseBtnStyle.normal.textColor = Color.yellow;
            }

            if (_courseBtnUnavailStyle == null)
            {
                _courseBtnUnavailStyle = new GUIStyle(HighLogic.Skin.button);
                _courseBtnUnavailStyle.normal.textColor = _colorUnavailable;
            }

            _courseSelectorScroll = GUILayout.BeginScrollView(_courseSelectorScroll, GUILayout.Width(505), GUILayout.Height(430));
            try
            {
                int acLevel = KCTUtilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
                foreach (TrainingTemplate course in CrewHandler.Instance.TrainingTemplates)
                {
                    // Mission trainings are only available for purchased parts
                    if (course.type == TrainingTemplate.TrainingType.Mission && course.isTemporary)
                        continue;

                    if (!_showAllTrainings && course.isTemporary && course.IsUnlocked)
                        continue;

                    int reqLevel = course.ACLevelRequirement;
                    bool isLocked = reqLevel > acLevel;
                    var style = isLocked ? _courseBtnUnavailStyle : (course.isTemporary ? _courseBtnStyle : HighLogic.Skin.button);
                    var c = new GUIContent(course.name, isLocked ? $"Requires level {reqLevel + 1} AC.\n{course.PartsTooltip}" : course.PartsTooltip);
                    if (GUILayout.Button(c, style))
                    {
                        _selectedCourse = new TrainingCourse(course);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndScrollView();
        }

        public UITab RenderCoursesTab()
        {
            _selectedCourse = null;
            RenderCourseSelector();
            GUILayout.BeginHorizontal();
            _showAllTrainings = GUILayout.Toggle(_showAllTrainings, "Show all possible trainings");
            GUILayout.EndHorizontal();
            return _selectedCourse == null ? UITab.Training : UITab.NewCourse;
        }

        public UITab RenderNewCourseTab()
        {
            if (_tempCourseLblStyle == null)
            {
                _tempCourseLblStyle = new GUIStyle(GUI.skin.label);
                _tempCourseLblStyle.normal.textColor = Color.yellow;
            }

            if (_lockedCourseLblStyle == null)
            {
                _lockedCourseLblStyle = new GUIStyle(GUI.skin.label);
                _lockedCourseLblStyle.normal.textColor = _colorUnavailable;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_selectedCourse.GetItemName());
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_selectedCourse.Description))
                GUILayout.Label(_selectedCourse.Description);

            int acLevel = KCTUtilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
            int reqLevel = _selectedCourse.ACLevelRequirement;
            bool isLocked = reqLevel > acLevel;
            if (isLocked)
                GUILayout.Label($"This training requires an Astronaut Complex of level {reqLevel + 1} or higher. Upgrade the Astronaut Complex.", _lockedCourseLblStyle);

            if (_selectedCourse.IsTemporary)
                GUILayout.Label("Tech for this part is still being researched", _tempCourseLblStyle);

            RenderSummaryBody(UITab.NewCourse);
            if (_selectedCourse.SeatMax > 0)
                GUILayout.Label($"{_selectedCourse.SeatMax - _selectedCourse.Students.Count} remaining seat(s).");
            bool underMin = _selectedCourse.SeatMin > _selectedCourse.Students.Count;
            if (underMin)
                GUILayout.Label($"{_selectedCourse.SeatMin - _selectedCourse.Students.Count} more naut(s) required.");
            const string tooltipProf = "Time for Proficiency training varies\nbased on nauts' prior proficiencies";
            const string tooltipMission = "Time for Mission training varies\nbased on nauts' stupidity";
            double timeLeft = _selectedCourse.GetTimeLeft();
            GUILayout.Label(new GUIContent($"Will take {KSPUtil.PrintDateDeltaCompact(timeLeft, true, false)}", _selectedCourse.Type == TrainingTemplate.TrainingType.Proficiency ? tooltipProf : tooltipMission));
            GUILayout.Label(new GUIContent($"and finish on {KSPUtil.PrintDate(Planetarium.GetUniversalTime() + timeLeft, false)}", _selectedCourse.Type == TrainingTemplate.TrainingType.Proficiency ? tooltipProf : tooltipMission));
            if (CrewHandler.Instance.RetirementEnabled && _selectedCourse.Students.Count > 0)
            {
                GUILayout.Label($"Retirement increase (avg): {KSPUtil.PrintDateDeltaCompact(_selectedCourse.AverageRetireExtension(), true, false)}");
            }

            if (!isLocked && !underMin && GUILayout.Button("Start Training", HighLogic.Skin.button, GUILayout.ExpandWidth(false)))
            {
                if (_selectedCourse.StartCourse())
                {
                    CrewHandler.Instance.TrainingCourses.Add(_selectedCourse);
                    _selectedCourse = null;
                    MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
                }
            }
            return _selectedCourse == null ? UITab.Astronauts : UITab.NewCourse;
        }

        public void RenderNautTab()
        {
            UpdateActiveCourseMap();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_selectedNaut.name);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label($"{_selectedNaut.trait} {_selectedNaut.experienceLevel.ToString():D}");
                double retireTime = CrewHandler.Instance.GetRetireTime(_selectedNaut.name);
                if (CrewHandler.Instance.RetirementEnabled && retireTime > 0d)
                {
                    GUILayout.Space(8);
                    GUILayout.Label($"Retires NET {KSPUtil.PrintDate(retireTime, false)}", RightLabel);
                }
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            double nlt = CrewHandler.Instance.GetLatestRetireTime(_selectedNaut.name);
            if (nlt > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Empty, GUILayout.ExpandWidth(true));
                GUILayout.Label($"Retires NLT {KSPUtil.PrintDate(nlt, false)}", RightLabel);
                GUILayout.EndHorizontal();
            }

            if (_activeMap.ContainsKey(_selectedNaut))
            {
                TrainingCourse currentCourse = _activeMap[_selectedNaut];
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label($"Training for {currentCourse.GetItemName()} until {KSPUtil.PrintDate(Planetarium.GetUniversalTime() + currentCourse.GetTimeLeft(), false)}");
                    if (currentCourse.SeatMin > 1)
                    {
                        if (GUILayout.Button("Cancel", HighLogic.Skin.button, GUILayout.ExpandWidth(false)))
                            CancelCourse(currentCourse);
                    }
                    else
                    {
                        if (GUILayout.Button("Remove", HighLogic.Skin.button, GUILayout.ExpandWidth(false)))
                            LeaveCourse(currentCourse, _selectedNaut);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(CrewHandler.Instance.GetTrainingString(_selectedNaut));
        }

        private void LeaveCourse(TrainingCourse course, ProtoCrewMember student)
        {
            DialogGUIBase[] options = new DialogGUIBase[3];
            options[0] = new DialogGUIFlexibleSpace();
            options[1] = new DialogGUIButton("Yes", () =>
            {
                course.RemoveStudent(student);
                if (course.Students.Count == 0)
                {
                    CrewHandler.Instance.TrainingCourses.Remove(course);
                    MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
                }
            });
            options[2] = new DialogGUIButton("No", () => { });

            var diag = new MultiOptionDialog("ConfirmStudentDropCourse", $"Are you sure you want to remove {student.name} from training? They will lose any retirement benefit of the training as well.", "Remove from Training?",
                HighLogic.UISkin,
                new Rect(0.5f, 0.5f, 150f, 60f),
                new DialogGUIFlexibleSpace(),
                new DialogGUIHorizontalLayout(options));
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
        }

        private void CancelCourse(TrainingCourse course)
        {
            DialogGUIBase[] options = new DialogGUIBase[3];
            options[0] = new DialogGUIFlexibleSpace();
            options[1] = new DialogGUIButton("Yes", () =>
            {
                // We "complete" the course but we didn't mark it as Completed, so it just releases the students and doesn't apply rewards
                course.CompleteCourse();
                CrewHandler.Instance.TrainingCourses.Remove(course);
                MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
            });
            options[2] = new DialogGUIButton("No", () => { });
            var sb = new StringBuilder("Are you sure you want to cancel this training? The following students will be removed (losing all retirement benefit from the training):");
            foreach (ProtoCrewMember stud in course.Students)
            {
                sb.AppendLine();
                sb.Append(stud.name);
            }
            var diag = new MultiOptionDialog("ConfirmCancelCourse", sb.ToStringAndRelease(), "Cancel Training?",
                HighLogic.UISkin,
                new Rect(0.5f, 0.5f, 150f, 60f),
                new DialogGUIFlexibleSpace(),
                new DialogGUIHorizontalLayout(options));
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin).HideGUIsWhilePopup();
        }

        private static void CreateCourseFinishAlarm(ProtoCrewMember student, TrainingCourse currentCourse)
        {
            double completeUT = Planetarium.GetUniversalTime() + currentCourse.GetTimeLeft();
            string alarmTxt = $"{currentCourse.GetItemName()} - {student.name}";
            KACWrapper.KAC.CreateAlarm(KACWrapper.KACAPI.AlarmTypeEnum.Crew, alarmTxt, completeUT);
        }

        private static void CreateReturnToDutyAlarm(ProtoCrewMember crew)
        {
            string alarmTxt = $"Return to duty - {crew.name}";
            KACWrapper.KAC.CreateAlarm(KACWrapper.KACAPI.AlarmTypeEnum.Crew, alarmTxt, crew.inactiveTimeEnd);
        }

        private void UpdateActiveCourseMap()
        {
            _activeMap.Clear();
            foreach (TrainingCourse course in CrewHandler.Instance.TrainingCourses)
            {
                foreach (ProtoCrewMember student in course.Students)
                {
                    _activeMap[student] = course;
                }
            }
        }
    }
}
