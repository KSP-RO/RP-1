using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RP0.Crew
{
    public class FSGUI : UIBase
    {
        private ActiveCourse _selectedCourse = null;
        private ProtoCrewMember _selectedNaut = null;
        private Vector2 _nautListScroll = new Vector2();
        private readonly Dictionary<ProtoCrewMember, ActiveCourse> _activeMap = new Dictionary<ProtoCrewMember, ActiveCourse>();
        private Vector2 _courseSelectorScroll = new Vector2();
        private GUIStyle _courseBtnStyle = null;
        private GUIStyle _tempCourseLblStyle = null;
        private readonly GUIContent _nautRowAlarmBtnContent = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/KACIcon15", false), "Add alarm");

        protected void RenderNautListHeading()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(24));
            GUILayout.Label("Name", BoldLabel, GUILayout.Width(144));
            GUILayout.Label("Course", BoldLabel, GUILayout.Width(96));
            GUILayout.Label("Complete", BoldLabel, GUILayout.Width(80));
            GUILayout.Label("Retires NET", BoldLabel, GUILayout.Width(80));
            GUILayout.EndHorizontal();
        }

        protected void RenderNautListRow(UITab currentTab, ProtoCrewMember student)
        {
            ActiveCourse currentCourse = null;
            if (_activeMap.ContainsKey(student))
                currentCourse = _activeMap[student];
            bool selectedForCourse = _selectedCourse != null && _selectedCourse.Students.Contains(student);
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label($"{student.trait.Substring(0, 1)} {student.experienceLevel}", GUILayout.Width(24));
                if (currentCourse == null && _selectedCourse != null && (selectedForCourse || _selectedCourse.MeetsStudentReqs(student)))
                {
                    var c = new GUIContent(student.name, "Select for course");
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
                    course = currentCourse.name;
                    complete = KSPUtil.PrintDate(currentCourse.CompletionTime(), false);
                }
                GUILayout.Label(course, GUILayout.Width(96));
                GUILayout.Label(complete, GUILayout.Width(80));

                if (CrewHandler.Instance.KerbalRetireTimes.ContainsKey(student.name))
                {
                    retires = CrewHandler.Instance.RetirementEnabled ? KSPUtil.PrintDate(CrewHandler.Instance.KerbalRetireTimes[student.name], false) : "(n/a)";
                }
                else
                {
                    retires = "(unknown)";
                }
                GUILayout.Label(retires, GUILayout.Width(80));

                if (currentCourse != null)
                {
                    if (currentCourse.seatMin > 1)
                    {
                        if (GUILayout.Button(new GUIContent("X", "Cancel course"), HighLogic.Skin.button, GUILayout.ExpandWidth(false)))
                            CancelCourse(currentCourse);
                    }
                    else
                    {
                        if (GUILayout.Button(new GUIContent("X", "Remove from course"), HighLogic.Skin.button, GUILayout.ExpandWidth(false)))
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

            _courseSelectorScroll = GUILayout.BeginScrollView(_courseSelectorScroll, GUILayout.Width(505), GUILayout.Height(430));
            try
            {
                foreach (CourseTemplate course in CrewHandler.Instance.OfferedCourses)
                {
                    var style = course.isTemporary ? _courseBtnStyle : HighLogic.Skin.button;
                    var c = new GUIContent(course.name, course.PartsTooltip);
                    if (GUILayout.Button(c, style))
                        _selectedCourse = new ActiveCourse(course);
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
            return _selectedCourse == null ? UITab.Training : UITab.NewCourse;
        }

        public UITab RenderNewCourseTab()
        {
            if (_tempCourseLblStyle == null)
            {
                _tempCourseLblStyle = new GUIStyle(GUI.skin.label);
                _tempCourseLblStyle.normal.textColor = Color.yellow;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_selectedCourse.name);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_selectedCourse.description))
                GUILayout.Label(_selectedCourse.description);
            if (_selectedCourse.isTemporary)
                GUILayout.Label("Tech for this part is still being researched", _tempCourseLblStyle);

            RenderSummaryBody(UITab.NewCourse);
            if (_selectedCourse.seatMax > 0)
                GUILayout.Label($"{_selectedCourse.seatMax - _selectedCourse.Students.Count} remaining seat(s).");
            if (_selectedCourse.seatMin > _selectedCourse.Students.Count)
                GUILayout.Label($"{_selectedCourse.seatMin - _selectedCourse.Students.Count} more student(s) required.");
            GUILayout.Label($"Will take {KSPUtil.PrintDateDeltaCompact(_selectedCourse.GetTime(), true, false)}");
            GUILayout.Label($"and finish on {KSPUtil.PrintDate(_selectedCourse.CompletionTime(), false)}");
            if (GUILayout.Button("Start Course", HighLogic.Skin.button, GUILayout.ExpandWidth(false)))
            {
                if (_selectedCourse.StartCourse())
                {
                    CrewHandler.Instance.ActiveCourses.Add(_selectedCourse);
                    _selectedCourse = null;
                    MaintenanceHandler.OnRP0MaintenanceChanged.Fire();
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
                if (CrewHandler.Instance.RetirementEnabled && CrewHandler.Instance.KerbalRetireTimes.ContainsKey(_selectedNaut.name))
                {
                    GUILayout.Space(8);
                    GUILayout.Label($"Retires NET {KSPUtil.PrintDate(CrewHandler.Instance.KerbalRetireTimes[_selectedNaut.name], false)}", RightLabel);
                }
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndHorizontal();

            double nlt = CrewHandler.Instance.GetLatestRetireTime(_selectedNaut);
            if (nlt > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Empty, GUILayout.ExpandWidth(true));
                GUILayout.Label($"Retires NLT {KSPUtil.PrintDate(nlt, false)}", RightLabel);
                GUILayout.EndHorizontal();
            }

            if (_activeMap.ContainsKey(_selectedNaut))
            {
                ActiveCourse currentCourse = _activeMap[_selectedNaut];
                GUILayout.BeginHorizontal();
                try
                {
                    GUILayout.Label($"Studying {currentCourse.name} until {KSPUtil.PrintDate(currentCourse.CompletionTime(), false)}");
                    if (currentCourse.seatMin > 1)
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

        private void LeaveCourse(ActiveCourse course, ProtoCrewMember student)
        {
            DialogGUIBase[] options = new DialogGUIBase[3];
            options[0] = new DialogGUIFlexibleSpace();
            options[1] = new DialogGUIButton("Yes", () =>
            {
                course.RemoveStudent(student);
                if (course.Students.Count == 0)
                {
                    CrewHandler.Instance.ActiveCourses.Remove(course);
                    MaintenanceHandler.OnRP0MaintenanceChanged.Fire();
                }
            });
            options[2] = new DialogGUIButton("No", () => { });

            var diag = new MultiOptionDialog("ConfirmStudentDropCourse", $"Are you sure you want {student.name} to drop this course?", "Drop Course?",
                HighLogic.UISkin,
                new Rect(0.5f, 0.5f, 150f, 60f),
                new DialogGUIFlexibleSpace(),
                new DialogGUIHorizontalLayout(options));
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin);
        }

        private void CancelCourse(ActiveCourse course)
        {
            DialogGUIBase[] options = new DialogGUIBase[3];
            options[0] = new DialogGUIFlexibleSpace();
            options[1] = new DialogGUIButton("Yes", () =>
            {
                // We "complete" the course but we didn't mark it as Completed, so it just releases the students and doesn't apply rewards
                course.CompleteCourse();
                CrewHandler.Instance.ActiveCourses.Remove(course);
                MaintenanceHandler.OnRP0MaintenanceChanged.Fire();
            });
            options[2] = new DialogGUIButton("No", () => { });
            var sb = new StringBuilder("Are you sure you want to cancel this course? The following students will cease study:");
            foreach (ProtoCrewMember stud in course.Students)
            {
                sb.AppendLine();
                sb.Append(stud.name);
            }
            var diag = new MultiOptionDialog("ConfirmCancelCourse", sb.ToStringAndRelease(), "Stop Course?",
                HighLogic.UISkin,
                new Rect(0.5f, 0.5f, 150f, 60f),
                new DialogGUIFlexibleSpace(),
                new DialogGUIHorizontalLayout(options));
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin);
        }

        private static void CreateCourseFinishAlarm(ProtoCrewMember student, ActiveCourse currentCourse)
        {
            // CrewHandler processes trainings every 3600 seconds. Need to account for that to set up accurate KAC alarms.
            double completeUT = currentCourse.CompletionTime();
            double timeDiff = completeUT - CrewHandler.Instance.NextUpdate;
            double timesChRun = Math.Ceiling(timeDiff / CrewHandler.UpdateInterval);
            double alarmUT = CrewHandler.Instance.NextUpdate + timesChRun * CrewHandler.UpdateInterval;
            string alarmTxt = $"{currentCourse.name} - {student.name}";
            KACWrapper.KAC.CreateAlarm(KACWrapper.KACAPI.AlarmTypeEnum.Crew, alarmTxt, alarmUT);
        }

        private static void CreateReturnToDutyAlarm(ProtoCrewMember crew)
        {
            string alarmTxt = $"Return to duty - {crew.name}";
            KACWrapper.KAC.CreateAlarm(KACWrapper.KACAPI.AlarmTypeEnum.Crew, alarmTxt, crew.inactiveTimeEnd);
        }

        private void UpdateActiveCourseMap()
        {
            _activeMap.Clear();
            foreach (ActiveCourse course in CrewHandler.Instance.ActiveCourses)
            {
                foreach (ProtoCrewMember student in course.Students)
                {
                    _activeMap[student] = course;
                }
            }
        }
    }
}
