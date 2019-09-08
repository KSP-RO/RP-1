using System;
using System.Collections.Generic;
using UniLinq;
using System.Text;

namespace RP0.Crew
{
    public class ActiveCourse : CourseTemplate
    {
        public List<ProtoCrewMember> Students = new List<ProtoCrewMember>();

        public double elapsedTime = 0;
        public double startTime = 0d;
        public bool Started = false, Completed = false;


        public ActiveCourse(CourseTemplate template)
        {
            sourceNode = template.sourceNode;
            PopulateFromSourceNode(template.sourceNode);
        }

        public ActiveCourse(ConfigNode node)
        {
            FromConfigNode(node);
        }

        public ConfigNode AsConfigNode()
        {
            //save the source node, the variables, the teacher name, the student names, Started/Completed and the elapsed time
            ConfigNode node = new ConfigNode("ACTIVE_COURSE");
            node.AddValue("id", id);
            node.AddValue("name", name);
            node.AddValue("startTime", startTime);
            node.AddValue("elapsedTime", elapsedTime);
            node.AddValue("Started", Started);
            node.AddValue("Completed", Completed);
            ConfigNode studentNode = new ConfigNode("STUDENTS");
            foreach (ProtoCrewMember student in Students)
                studentNode.AddValue("student", student.name);
            node.AddNode("STUDENTS", studentNode);

            node.AddNode("SOURCE_NODE", sourceNode);

            return node;
        }

        public void FromConfigNode(ConfigNode node)
        {
            node.TryGetValue("startTime", ref startTime);
            node.TryGetValue("elapsedTime", ref elapsedTime);
            node.TryGetValue("Started", ref Started);
            node.TryGetValue("Completed", ref Completed);

            //load students
            ConfigNode studentNode = node.GetNode("STUDENTS");
            if (studentNode != null)
            {
                Students.Clear();
                foreach (ConfigNode.Value val in studentNode.values)
                {
                    if (HighLogic.CurrentGame.CrewRoster.Exists(val.value))
                    {
                        Students.Add(HighLogic.CurrentGame.CrewRoster[val.value]);
                    }
                }
            }

            sourceNode = node.GetNode("SOURCE_NODE");

            PopulateFromSourceNode(sourceNode);
        }

        public bool MeetsStudentReqs(ProtoCrewMember student)
        {
            if (!((student.type == (ProtoCrewMember.KerbalType.Crew) && (seatMax <= 0 || Students.Count < seatMax) && !student.inactive 
                && student.rosterStatus == ProtoCrewMember.RosterStatus.Available && student.experienceLevel >= minLevel && student.experienceLevel <= maxLevel 
                && (classes.Length == 0 || classes.Contains(student.trait)) && !Students.Contains(student))))
                return false;

            int pCount = preReqs.GetLength(0);
            int pACount = preReqsAny.GetLength(0);
            int cCount = conflicts.GetLength(0);
            if (pCount > 0 || cCount > 0 || pACount > 0)
            {
                for (int i = pCount; i-- > 0;)
                    pChecker[i] = true;

                int needCount = pCount;
                bool needAnyStill = pACount > 0;

                for (int entryIdx = student.careerLog.Count; entryIdx-- > 0 && (needCount > 0 || cCount > 0 || needAnyStill);)
                {
                    FlightLog.Entry e = student.careerLog.Entries[entryIdx];

                    string tgt = string.IsNullOrEmpty(e.target) ? string.Empty : e.target;

                    for (int preIdx = pCount; preIdx-- > 0 && needCount > 0;)
                    {
                        if (pChecker[preIdx] && (e.type == preReqs[preIdx, 0] && tgt == preReqs[preIdx, 1]))
                        {
                            pChecker[preIdx] = false;
                            --needCount;
                        }
                    }

                    for (int anyIdx = pACount; anyIdx-- > 0 && needAnyStill;)
                    {
                        if (e.type == preReqsAny[anyIdx, 0] && tgt == preReqsAny[anyIdx, 1])
                            needAnyStill = false;
                    }

                    for (int conIdx = cCount; conIdx-- > 0;)
                    {
                        if (e.type == conflicts[conIdx, 0] && tgt == conflicts[conIdx, 1])
                            return false;
                    }
                }

                if (needCount > 0 || needAnyStill)
                    return false;
            }
            return true;
        }
        public void AddStudent(ProtoCrewMember student)
        {
            if (seatMax <= 0 || Students.Count < seatMax)
            {
                if (!Students.Contains(student))
                    Students.Add(student);
            }
        }
        public void AddStudent(string student)
        {
            AddStudent(HighLogic.CurrentGame.CrewRoster[student]);
        }
        
        public void RemoveStudent(ProtoCrewMember student)
        {
            if (Students.Contains(student))
            {
                Students.Remove(student);
                if (Started)
                {
                    UnityEngine.Debug.Log("[FS] Kerbal removed from in-progress class!");
                    //TODO: Assign partial rewards, based on what the REWARD nodes think
                    student.inactive = false;
                    if (Students.Count == 0)
                    {
                        CompleteCourse();   // cancel the course
                    }
                }
            }
        }
        public void RemoveStudent(string student)
        {
            RemoveStudent(HighLogic.CurrentGame.CrewRoster[student]);
        }

        public double GetTime()
        {
            return GetTime(Students);
        }

        /* Returns time at which this course will complete */
        public double CompletionTime()
        {
            double start, length;
            if (Started)
                start = startTime;
            else
                start = Planetarium.GetUniversalTime();
            length = GetTime();
            return start + length;
        }

        public bool ProgressTime(double curT)
        {
            if (!Started)
                return false;
            if (!Completed)
            {
                elapsedTime = curT - startTime;
                Completed = curT > startTime + GetTime(Students);
                if (Completed) //we finished the course!
                {
                    CompleteCourse();
                }
            }
            return Completed;
        }

        public void CompleteCourse()
        {
            //assign rewards to all kerbals and set them to free
            if (Completed)
            {
                foreach (ProtoCrewMember student in Students)
                {
                    if (student == null)
                        continue;
                    
                    if (ExpireLog != null)
                    {
                        foreach (ConfigNode.Value v in ExpireLog.values)
                        {
                            for (int i = student.careerLog.Count; i-- > 0;)
                            {
                                FlightLog.Entry e = student.careerLog.Entries[i];
                                if (CrewHandler.TrainingExpiration.Compare(v.value, e))
                                {
                                    e.type = "expired_" + e.type;
                                    CrewHandler.Instance.RemoveExpiration(student.name, v.value);
                                    break;
                                }
                            }
                        }
                    }

                    if (RewardLog != null)
                    {
                        if (student.flightLog.Count > 0)
                            student.ArchiveFlightLog();

                        CrewHandler.TrainingExpiration exp = null;
                        if (expiration > 0d)
                        {
                            exp = new CrewHandler.TrainingExpiration();
                            exp.pcmName = student.name;
                            exp.expiration = expiration;
                            if (expirationUseStupid)
                                exp.expiration *= UtilMath.Lerp(CrewHandler.Instance.settings.trainingProficiencyStupidMin,
                                    CrewHandler.Instance.settings.trainingProficiencyStupidMax,
                                    student.stupidity);
                            exp.expiration += Planetarium.GetUniversalTime();
                        }

                        bool prevMissionsAlreadyExpired = false;
                        foreach (ConfigNode.Value v in RewardLog.values)
                        {
                            string[] s = v.value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            string trainingType = s[0];
                            string trainingTarget = s.Length == 1 ? null : s[1];

                            if (!prevMissionsAlreadyExpired && trainingType == "TRAINING_mission")
                            {
                                // Expire any previous mission trainings because only 1 should be active at a time
                                for (int i = student.careerLog.Count; i-- > 0;)
                                {
                                    FlightLog.Entry e = student.careerLog.Entries[i];
                                    if (e.type == "TRAINING_mission")
                                    {
                                        e.type = "expired_" + e.type;
                                        CrewHandler.Instance.RemoveExpiration(student.name, v.value);
                                        student.ArchiveFlightLog();
                                        prevMissionsAlreadyExpired = true;
                                    }
                                }
                            }

                            student.flightLog.AddEntry(trainingType, trainingTarget);
                            student.ArchiveFlightLog();
                            if (expiration > 0d)
                                exp.entries.Add(v.value);
                        }

                        if (expiration > 0d)
                            CrewHandler.Instance.AddExpiration(exp);
                    }

                    if (rewardXP != 0)
                        student.ExtraExperience += rewardXP;
                }
            }

            foreach (ProtoCrewMember student in Students)
                student.inactive = false;

            //fire an event
        }

        public bool StartCourse()
        {
            //set all the kerbals to unavailable and begin tracking time
            if (Started)
                return true;

            //ensure we have more than the minimum number of students and not more than the maximum number
            int studentCount = Students.Count;
            if (seatMax > 0 && studentCount > seatMax)
                return false;
            if (seatMin > 0 && studentCount < seatMin)
                return false;

            Started = true;

            startTime = Planetarium.GetUniversalTime();

            foreach (ProtoCrewMember student in Students)
                student.SetInactive(GetTime(Students) + 1d);

            return true;
            //fire an event
        }
    }
}
