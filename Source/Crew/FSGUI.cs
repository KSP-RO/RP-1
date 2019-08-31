using System;
using System.Collections.Generic;
using System.Text;

using KSP;
using UnityEngine;

namespace RP0.Crew
{
    public class FSGUI : UIBase
    {
        private ActiveCourse selectedCourse = null;
        private ProtoCrewMember selectedNaut = null;
        private Vector2 nautListScroll = new Vector2();
        private Dictionary<ProtoCrewMember, ActiveCourse> activeMap = new Dictionary<ProtoCrewMember, ActiveCourse>();
        private Vector2 courseSelectorScroll = new Vector2();
        private GUIStyle courseBtnStyle = null;
        private GUIStyle tempCourseLblStyle = null;
        private GUIContent nautRowAlarmBtnContent = new GUIContent(GameDatabase.Instance.GetTexture("RP-0/KACIcon15", false));
        
        protected void nautListHeading()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("", GUILayout.Width(24));
                GUILayout.Label("Name", boldLabel, GUILayout.Width(144));
                GUILayout.Label("Course", boldLabel, GUILayout.Width(96));
                GUILayout.Label("Complete", boldLabel, GUILayout.Width(80));
                GUILayout.Label("Retires NET", boldLabel, GUILayout.Width(80));
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        private void leaveCourse(ActiveCourse course, ProtoCrewMember student)
        {
            DialogGUIBase[] options = new DialogGUIBase[3];
            options[0] = new DialogGUIFlexibleSpace();
            options[1] = new DialogGUIButton("Yes", () => 
            {
                course.RemoveStudent(student);
                if (course.Students.Count == 0)
                {
                    CrewHandler.Instance.ActiveCourses.Remove(course);
                    MaintenanceHandler.Instance.UpdateUpkeep();
                }
            });
            options[2] = new DialogGUIButton("No", () => { });
            MultiOptionDialog diag = new MultiOptionDialog("ConfirmStudentDropCourse", "Are you sure you want "+student.name+ " to drop this course?", "Drop Course?",
                HighLogic.UISkin,
                new Rect(0.5f, 0.5f, 150f, 60f),
                new DialogGUIFlexibleSpace(),
                new DialogGUIHorizontalLayout(options));
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin);
        }

        private void cancelCourse(ActiveCourse course)
        {
            DialogGUIBase[] options = new DialogGUIBase[3];
            options[0] = new DialogGUIFlexibleSpace();
            options[1] = new DialogGUIButton("Yes", () =>
                {
                    // We "complete" the course but we didn't mark it as Completed, so it just releases the students and doesn't apply rewards
                    course.CompleteCourse();
                    CrewHandler.Instance.ActiveCourses.Remove(course);
                    MaintenanceHandler.Instance.UpdateUpkeep();
                });
            options[2] = new DialogGUIButton("No", () => { });
            StringBuilder msg = new StringBuilder("Are you sure you want to cancel this course? The following students will cease study:");
            foreach (ProtoCrewMember stud in course.Students) {
                msg.AppendLine();
                msg.Append(stud.name);
            }
            MultiOptionDialog diag = new MultiOptionDialog("ConfirmCancelCourse", msg.ToStringAndRelease(), "Stop Course?",
                HighLogic.UISkin,
                new Rect(0.5f, 0.5f, 150f, 60f),
                new DialogGUIFlexibleSpace(),
                new DialogGUIHorizontalLayout(options));
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin);
        }

        protected void nautListRow(tabs currentTab, ProtoCrewMember student)
        {
            GUIStyle style = HighLogic.Skin.label;
            ActiveCourse currentCourse = null;
            if (activeMap.ContainsKey(student))
                currentCourse = activeMap[student];
            bool onSelectedCourse = selectedCourse != null && currentCourse != null && currentCourse.id == selectedCourse.id;
            if (onSelectedCourse)
                style = boldLabel;
            bool selectedForCourse = selectedCourse != null && selectedCourse.Students.Contains(student);
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label(String.Format("{0} {1}", student.trait.Substring(0, 1), student.experienceLevel), GUILayout.Width(24));
                if (currentCourse == null && selectedCourse != null && (selectedForCourse || selectedCourse.MeetsStudentReqs(student))) {
                    if (toggleButton(student.name, selectedForCourse, GUILayout.Width(144))) {
                        if (selectedForCourse)
                            selectedCourse.RemoveStudent(student);
                        else
                            selectedCourse.AddStudent(student);
                    }
                } else if (currentTab == tabs.Training) {
                    if (GUILayout.Button(student.name, GUILayout.Width(144))) {
                        selectedNaut = student;
                    }
                } else {
                    GUILayout.Label(student.name, GUILayout.Width(144));
                }
                string course, complete, retires;
                if (currentCourse == null) {
                    if (student.rosterStatus == ProtoCrewMember.RosterStatus.Assigned)
                    {
                        course = "(in-flight)";
                        complete = KSPUtil.PrintDate(student.inactiveTimeEnd, false);
                    }
                    else if (student.inactive) {
                        course = "(inactive)";
                        complete = KSPUtil.PrintDate(student.inactiveTimeEnd, false);
                    }
                    else {
                        course = "(free)";
                        complete = "(n/a)";
                    }
                } else {
                    course = currentCourse.name;
                    complete = KSPUtil.PrintDate(currentCourse.CompletionTime(), false);
                }
                GUILayout.Label(course, GUILayout.Width(96));
                GUILayout.Label(complete, GUILayout.Width(80));
                if (CrewHandler.Instance.kerbalRetireTimes.ContainsKey(student.name))
                {
                    retires = CrewHandler.Instance.retirementEnabled ? KSPUtil.PrintDate(CrewHandler.Instance.kerbalRetireTimes[student.name], false) : "(n/a)";
                }
                else
                {
                    retires = "(unknown)";
                }
                GUILayout.Label(retires, GUILayout.Width(80));
                if (currentCourse != null) {
                    if (currentCourse.seatMin > 1) {
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                            cancelCourse(currentCourse);
                    } else {
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                            leaveCourse(currentCourse, student);
                    }

                    if (KACWrapper.APIReady && GUILayout.Button(nautRowAlarmBtnContent, GUILayout.ExpandWidth(false)))
                    {
                        // CrewHandler processes trainings every 3600 seconds. Need to account for that to set up accurate KAC alarms.
                        double completeUT = currentCourse.CompletionTime();
                        double timeDiff = completeUT - CrewHandler.Instance.nextUpdate;
                        double timesChRun = Math.Ceiling(timeDiff / CrewHandler.Instance.updateInterval);
                        double alarmUT = CrewHandler.Instance.nextUpdate + timesChRun * CrewHandler.Instance.updateInterval;
                        string alarmTxt = $"{currentCourse.name} - {student.name}";
                        KACWrapper.KAC.CreateAlarm(KACWrapper.KACAPI.AlarmTypeEnum.Crew, alarmTxt, alarmUT);
                    }
                }
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        private void summaryBody(tabs currentTab)
        {
            updateActiveMap();
            float scrollHeight = currentTab == tabs.Training ? 420 : 305;
            nautListScroll = GUILayout.BeginScrollView(nautListScroll, GUILayout.Width(505), GUILayout.Height(scrollHeight));
            try {
                nautListHeading();
                for (int i = 0; i < HighLogic.CurrentGame.CrewRoster.Count; i++)
                {
                    ProtoCrewMember student = HighLogic.CurrentGame.CrewRoster[i];
                    if (student.type == ProtoCrewMember.KerbalType.Crew && 
                        (student.rosterStatus == ProtoCrewMember.RosterStatus.Available ||
                         (currentTab == tabs.Training && student.rosterStatus == ProtoCrewMember.RosterStatus.Assigned)))
                    {
                        nautListRow(currentTab, student);
                    }
                }
            } finally {
                GUILayout.EndScrollView();
            }
        }

        public tabs summaryTab()
        {
            selectedCourse = null;
            selectedNaut = null;
            summaryBody(tabs.Training);
            return selectedNaut == null ? tabs.Training : tabs.Naut;
        }

        protected void courseSelector()
        {
            if (courseBtnStyle == null)
            {
                courseBtnStyle = new GUIStyle(GUI.skin.button);
                courseBtnStyle.normal.textColor = Color.yellow;
            }
            
            courseSelectorScroll = GUILayout.BeginScrollView(courseSelectorScroll, GUILayout.Width(505), GUILayout.Height(430));
            try {
                foreach (CourseTemplate course in CrewHandler.Instance.OfferedCourses) {
                    var style = course.isTemporary ? courseBtnStyle : GUI.skin.button;
                    if (GUILayout.Button(course.name, style))
                        selectedCourse = new ActiveCourse(course);
                }
            } finally {
                GUILayout.EndScrollView();
            }
        }

        public tabs coursesTab()
        {
            selectedCourse = null;
            courseSelector();
            return selectedCourse == null ? tabs.Courses : tabs.NewCourse;
        }

        public tabs newCourseTab()
        {
            if (tempCourseLblStyle == null)
            {
                tempCourseLblStyle = new GUIStyle(GUI.skin.label);
                tempCourseLblStyle.normal.textColor = Color.yellow;
            }

            GUILayout.BeginHorizontal();
            try {
                GUILayout.FlexibleSpace();
                GUILayout.Label(selectedCourse.name);
                GUILayout.FlexibleSpace();
            } finally {
                GUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(selectedCourse.description))
                GUILayout.Label(selectedCourse.description);
            if (selectedCourse.isTemporary)
                GUILayout.Label("Tech for this part is still being researched", tempCourseLblStyle);
            summaryBody(tabs.NewCourse);
            if (selectedCourse.seatMax > 0)
                GUILayout.Label(selectedCourse.seatMax - selectedCourse.Students.Count + " remaining seat(s).");
            if (selectedCourse.seatMin > selectedCourse.Students.Count)
                GUILayout.Label(selectedCourse.seatMin - selectedCourse.Students.Count + " more student(s) required.");
            GUILayout.Label("Will take " + KSPUtil.PrintDateDeltaCompact(selectedCourse.GetTime(), false, false));
            GUILayout.Label("and finish on " + KSPUtil.PrintDate(selectedCourse.CompletionTime(), false));
            if (GUILayout.Button("Start Course", GUILayout.ExpandWidth(false))) {
                if (selectedCourse.StartCourse()) {
                    CrewHandler.Instance.ActiveCourses.Add(selectedCourse);
                    selectedCourse = null;
                    MaintenanceHandler.Instance.UpdateUpkeep();
                }
            }
            return selectedCourse == null ? tabs.Training : tabs.NewCourse;
        }

        public void nautTab()
        {
            updateActiveMap();
            GUILayout.BeginHorizontal();
            try {
                GUILayout.FlexibleSpace();
                GUILayout.Label(selectedNaut.name);
                GUILayout.FlexibleSpace();
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label(String.Format("{0} {1:D}", selectedNaut.trait, selectedNaut.experienceLevel.ToString()));
                if (CrewHandler.Instance.retirementEnabled && CrewHandler.Instance.kerbalRetireTimes.ContainsKey(selectedNaut.name)) {
                    GUILayout.Space(8);
                    GUILayout.Label(String.Format("Retires NET {0}", KSPUtil.PrintDate(CrewHandler.Instance.kerbalRetireTimes[selectedNaut.name], false)),
                                    rightLabel);
                }
            } finally {
                GUILayout.EndHorizontal();
            }
            if (activeMap.ContainsKey(selectedNaut)) {
                ActiveCourse currentCourse = activeMap[selectedNaut];
                GUILayout.BeginHorizontal();
                try {
                    GUILayout.Label($"Studying {currentCourse.name} until {KSPUtil.PrintDate(currentCourse.CompletionTime(), false)}");
                    if (currentCourse.seatMin > 1) {
                        if (GUILayout.Button("Cancel", GUILayout.ExpandWidth(false)))
                            cancelCourse(currentCourse);
                    } else {
                        if (GUILayout.Button("Remove", GUILayout.ExpandWidth(false)))
                            leaveCourse(currentCourse, selectedNaut);
                    }
                } finally {
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.Label(CrewHandler.Instance.GetTrainingString(selectedNaut));
        }

        private void updateActiveMap()
        {
            activeMap.Clear();
            foreach (ActiveCourse course in CrewHandler.Instance.ActiveCourses) {
                foreach (ProtoCrewMember student in course.Students) {
                    activeMap[student] = course;
                }
            }
        }
    }
}
