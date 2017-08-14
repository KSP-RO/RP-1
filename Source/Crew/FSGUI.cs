using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using KSP;
using UnityEngine;

namespace RP0.Crew
{
    public class FSGUI
    {
        public bool showMain = false;
        public Rect MainGUIPos = new Rect(200, 200, 540, 360);
        public void SetGUIPositions(GUI.WindowFunction OnWindow)
        {
            if (showMain) MainGUIPos = GUILayout.Window(7349, MainGUIPos, DrawMainGUI, "Training");
        }

        public void DrawGUIs(int windowID)
        {
            if (showMain) DrawMainGUI(windowID);
        }

        private enum tabs { Summary, Courses, NewCourse, Naut };
        private tabs currentTab = tabs.Summary;
        ActiveCourse selectedCourse = null;
        ProtoCrewMember selectedNaut = null;
        Vector2 nautListScroll = new Vector2();
        private GUIStyle rightLabel, boldLabel, pressedButton;
        private Dictionary<ProtoCrewMember, ActiveCourse> activeMap = new Dictionary<ProtoCrewMember, ActiveCourse>();

        public FSGUI()
        {
            rightLabel = new GUIStyle(HighLogic.Skin.label);
            rightLabel.alignment = TextAnchor.MiddleRight;
            boldLabel = new GUIStyle(HighLogic.Skin.label);
            boldLabel.fontStyle = FontStyle.Bold;
            pressedButton = new GUIStyle(HighLogic.Skin.button);
            pressedButton.normal = pressedButton.active;
        }

        private bool toggleButton(string text, bool selected, params GUILayoutOption[] options)
        {
            return GUILayout.Button(text, selected ? pressedButton : HighLogic.Skin.button, options);
        }

        private void tabSelector()
        {
            GUILayout.BeginHorizontal();
            try {
                if (toggleButton("Summary", currentTab == tabs.Summary))
                    currentTab = tabs.Summary;
                if (toggleButton("Courses", currentTab == tabs.Courses))
                    currentTab = tabs.Courses;
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        protected void nautListHeading()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.Label("", GUILayout.Width(24));
                GUILayout.Label("Name", boldLabel, GUILayout.Width(96));
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
            options[1] = new DialogGUIButton("Yes", () => { course.RemoveStudent(student); });
            options[2] = new DialogGUIButton("No", () => { });
            MultiOptionDialog diag = new MultiOptionDialog("Are you sure you want "+student.name+ " to drop this course?", "Drop Course?",
                HighLogic.UISkin,
                new Rect(0.5f, 0.5f, 150f, 60f),
                new DialogGUIFlexibleSpace(),
                new DialogGUIVerticalLayout(options));
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin);
        }

        private void cancelCourse(ActiveCourse course)
        {
            DialogGUIBase[] options = new DialogGUIBase[3];
            options[0] = new DialogGUIFlexibleSpace();
            options[1] = new DialogGUIButton("Yes", () =>
                {
                    /* We "complete" the course but we didn't mark it as Completed, so it just releases the students and doesn't apply rewards */
                    course.CompleteCourse();
                });
            options[2] = new DialogGUIButton("No", () => { });
            StringBuilder msg = new StringBuilder("Are you sure you want to cancel this course?  The following students will cease study:");
            foreach (ProtoCrewMember stud in course.Students) {
                msg.AppendLine();
                msg.Append(stud.name);
            }
            MultiOptionDialog diag = new MultiOptionDialog(msg.ToStringAndRelease(), "Stop Course?",
                HighLogic.UISkin,
                new Rect(0.5f, 0.5f, 150f, 60f),
                new DialogGUIFlexibleSpace(),
                new DialogGUIVerticalLayout(options));
            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin);
        }

        protected void nautListRow(ProtoCrewMember student)
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
                    if (toggleButton(student.name, selectedForCourse, GUILayout.Width(96))) {
                        if (selectedForCourse)
                            selectedCourse.RemoveStudent(student);
                        else
                            selectedCourse.AddStudent(student);
                    }
                } else if (currentTab == tabs.Summary) {
                    if (GUILayout.Button(student.name, GUILayout.Width(96))) {
                        currentTab = tabs.Naut;
                        selectedNaut = student;
                    }
                } else {
                    GUILayout.Label(student.name, GUILayout.Width(96));
                }
                string course, complete, retires;
                if (currentCourse == null) {
                    if (student.inactive) {
                        course = "(inactive)";
                        complete = KSPUtil.PrintDate(student.inactiveTimeEnd, false);
                    } else {
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
                    retires = KSPUtil.PrintDate(CrewHandler.Instance.kerbalRetireTimes[student.name], false);
                else
                    retires = "(unknown)";
                GUILayout.Label(retires, GUILayout.Width(80));
                if (currentCourse != null) {
                    if (currentCourse.seatMin > 1) {
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                            cancelCourse(currentCourse);
                    } else {
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                            leaveCourse(currentCourse, student);
                    }
                }
            } finally {
                GUILayout.EndHorizontal();
            }
        }

        protected void summaryTab()
        {
            nautListScroll = GUILayout.BeginScrollView(nautListScroll, GUILayout.Width(480), GUILayout.Height(144));
            try {
                nautListHeading();
                for (int i = 0; i < HighLogic.CurrentGame.CrewRoster.Count; i++)
                {
                    ProtoCrewMember student = HighLogic.CurrentGame.CrewRoster[i];
                    if (student.type == ProtoCrewMember.KerbalType.Crew)
                        nautListRow(student);
                }
            } finally {
                GUILayout.EndScrollView();
            }
        }

        protected void courseSelector()
        {
            foreach (CourseTemplate course in CrewHandler.Instance.OfferedCourses) {
                if (GUILayout.Button(course.name)) {
                    selectedCourse = new ActiveCourse(course);
                    currentTab = tabs.NewCourse;
                }
            }
        }

        protected void coursesTab()
        {
            courseSelector();
        }

        protected void newCourseTab()
        {
            GUILayout.BeginHorizontal();
            try {
                GUILayout.FlexibleSpace();
                GUILayout.Label(selectedCourse.name);
                GUILayout.FlexibleSpace();
            } finally {
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(selectedCourse.description);
            summaryTab();
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
                    currentTab = tabs.Summary;
                }
            }
        }

        protected void nautTab()
        {
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
                if (CrewHandler.Instance.kerbalRetireTimes.ContainsKey(selectedNaut.name)) {
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
                    GUILayout.Label("Studying "+currentCourse.name+" until "+KSPUtil.PrintDate(currentCourse.CompletionTime(), false));
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

        protected void DrawMainGUI(int windowID)
        {
            updateActiveMap();
            try {
                GUILayout.BeginVertical();
                try {
                    tabSelector();
                    switch (currentTab) {
                    case tabs.Summary:
                        selectedCourse = null;
                        summaryTab();
                        break;
                    case tabs.Courses:
                        coursesTab();
                        break;
                    case tabs.NewCourse:
                        newCourseTab();
                        break;
                    case tabs.Naut:
                        nautTab();
                        break;
                    default: // can't happen
                        break;
                    }
                } finally {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndVertical();
                }
            } finally {
                GUI.DragWindow();
            }
        }
    }
}
