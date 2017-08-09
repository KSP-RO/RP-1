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
        public Rect MainGUIPos = new Rect(200, 200, 750, 500);
        public void SetGUIPositions(GUI.WindowFunction OnWindow)
        {
            if (showMain) MainGUIPos = GUILayout.Window(7349, MainGUIPos, DrawMainGUI, "Training");
        }

        public void DrawGUIs(int windowID)
        {
            if (showMain) DrawMainGUI(windowID);
        }

        int offeredActiveToolbar = 0;
        Vector2 offeredActiveScroll = new Vector2();
        ActiveCourse selectedCourse = null;
        Vector2 currentStudentList = new Vector2();
        Vector2 availableKerbalList = new Vector2();
        protected void DrawMainGUI(int windowID)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(250)); //offered/active list
            int oldStatus = offeredActiveToolbar;
            offeredActiveToolbar = GUILayout.Toolbar(offeredActiveToolbar, new string[] { "Offered", "Active" });
            if (offeredActiveToolbar != oldStatus)
            {
                selectedCourse = null;
            }
            offeredActiveScroll = GUILayout.BeginScrollView(offeredActiveScroll);
            if (offeredActiveToolbar == 0) //offered list
            {
                foreach (CourseTemplate template in CrewHandler.Instance.OfferedCourses)
                {
                    if (GUILayout.Button(template.id + " - " + template.name))
                    {
                        selectedCourse = new ActiveCourse(template);
                    }
                }
            }
            else //active list
            {
                foreach (ActiveCourse course in CrewHandler.Instance.ActiveCourses)
                {
                    if (GUILayout.Button(course.id + " - " + course.name)) //show percent complete?
                    {
                        selectedCourse = course;
                    }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(); //Selected info
            if (offeredActiveToolbar == 0)
            {
                if (selectedCourse != null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(selectedCourse.id + " - " + selectedCourse.name);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Label(selectedCourse.description);

                    GUILayout.Label("Course length: " + KSPUtil.PrintDateDelta(selectedCourse.time, true));

                    //select the kerbals. Two lists, the current Students and the available ones
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical(GUILayout.Width(250));
                    GUILayout.Label("Enrolled:");
                    currentStudentList = GUILayout.BeginScrollView(currentStudentList);
                    for (int i = 0; i < selectedCourse.Students.Count; i++ )
                    {
                        ProtoCrewMember student = selectedCourse.Students[i];
                        if (GUILayout.Button(student.name+": "+student.trait+" "+student.experienceLevel))
                        {
                            selectedCourse.Students.RemoveAt(i);
                            --i;
                        }

                    }
                    GUILayout.EndScrollView();
                    if (selectedCourse.seatMax > 0)
                    {
                        GUILayout.Label(selectedCourse.seatMax - selectedCourse.Students.Count + " remaining seat(s).");
                    }
                    if (selectedCourse.seatMin > 0)
                    {
                        GUILayout.Label(Math.Max(0, selectedCourse.seatMin - selectedCourse.Students.Count) + " student(s) required.");
                    }
                    GUILayout.EndVertical();

                    GUILayout.BeginVertical(GUILayout.Width(250));
                    GUILayout.Label("Available:");
                    availableKerbalList = GUILayout.BeginScrollView(availableKerbalList);
                    for (int i = 0; i < HighLogic.CurrentGame.CrewRoster.Count; i++)
                    {
                        ProtoCrewMember student = HighLogic.CurrentGame.CrewRoster[i];
                        if (selectedCourse.MeetsStudentReqs(student))
                        {
                            if (GUILayout.Button(student.name + ": " + student.trait + " " + student.experienceLevel))
                            {
                                selectedCourse.AddStudent(student);
                            }
                        }

                    }
                    GUILayout.EndScrollView();
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();

                    
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Start Course", GUILayout.ExpandWidth(false)))
                    {
                        selectedCourse.StartCourse();
                        CrewHandler.Instance.ActiveCourses.Add(selectedCourse);
                        selectedCourse = null;
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                }
            }
            else
            {
                //An active course has been selected
                if (selectedCourse != null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(selectedCourse.id + " - " + selectedCourse.name);
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Label(selectedCourse.description);

                    GUILayout.Label("Time remaining: "+KSPUtil.PrintDateDelta(selectedCourse.time-selectedCourse.elapsedTime, true));
                    GUILayout.Label(Math.Round(100*selectedCourse.elapsedTime/selectedCourse.time, 1) + "% complete");

                    //scroll list of all students
                    GUILayout.Label("Students:");
                    currentStudentList = GUILayout.BeginScrollView(currentStudentList);
                    for (int i = 0; i < selectedCourse.Students.Count; i++ )
                    {
                        ProtoCrewMember student = selectedCourse.Students[i];
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(student.name+": "+student.trait+" "+student.experienceLevel);
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                        {
                            DialogGUIBase[] options = new DialogGUIBase[3];
                            options[0] = new DialogGUIFlexibleSpace();
                            options[1] = new DialogGUIButton("Yes", () =>
                                {
                                    selectedCourse.Students.Remove(student);
                                    student.inactive = false;
                                });
                            options[2] = new DialogGUIButton("No", () => { });
                            
                            MultiOptionDialog diag = new MultiOptionDialog("Are you sure you want "+student.name+ " to drop this course?", "Drop Course?",
                                HighLogic.UISkin,
                                new Rect(0.5f, 0.5f, 150f, 60f),
                                new DialogGUIFlexibleSpace(),
                                new DialogGUIVerticalLayout(options));
                            PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin);

                            i--;
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Cancel Course", GUILayout.ExpandWidth(false)))
                    {
                        DialogGUIBase[] options = new DialogGUIBase[3];
                        options[0] = new DialogGUIFlexibleSpace();
                        options[1] = new DialogGUIButton("Yes", () => { selectedCourse.CompleteCourse(); CrewHandler.Instance.ActiveCourses.Remove(selectedCourse); selectedCourse = null; });
                        options[2] = new DialogGUIButton("No", () => { });

                        MultiOptionDialog diag = new MultiOptionDialog("Are you sure you want to cancel this course?", "Cancel Course?",
                            HighLogic.UISkin,
                            new Rect(0.5f, 0.5f, 150f, 60f),
                            new DialogGUIFlexibleSpace(),
                            new DialogGUIVerticalLayout(options));
                        PopupDialog.SpawnPopupDialog(diag, false, HighLogic.UISkin);
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            if (!Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                GUI.DragWindow();
        }
    }
}
