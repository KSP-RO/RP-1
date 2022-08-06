using System;
using System.Collections.Generic;
using UniLinq;
using System.Text;
using KerbalConstructionTime;
using RP0.DataTypes;

namespace RP0.Crew
{
    public class TrainingCourse : IKCTBuildItem, IConfigNode
    {
        [Persistent]
        public string id;

        [Persistent]
        public PersistentParsableList<ProtoCrewMember> Students = new PersistentParsableList<ProtoCrewMember>();

        [Persistent]
        public double elapsedTime = 0;

        [Persistent]
        public double startTime = 0d;

        [Persistent]
        public bool Started = false;

        [Persistent]
        public bool Completed = false;

        private TrainingTemplate template;
        public int seatMin => template.seatMin;
        public int seatMax => template.seatMax;
        public string description => template.description;
        public bool isTemporary => template.isTemporary;

        public double baseCourseTime = 0d;

        public TrainingCourse()
        {
        }

        public TrainingCourse(TrainingTemplate template)
        {
            id = template.id;
            this.template = template;
        }

        public TrainingCourse(ConfigNode node)
        {
            Load(node);
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
            if (CrewHandler.Instance.saveVersion < 1)
            {
                Students.Load(node.GetNode("STUDENTS"));
            }
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
            // TEMP until KSPCF releases
            ConfigNode n = node.GetNode("Students");
            if (n == null)
            {
                n = new ConfigNode("Students");
                Students.Save(n);
                node.AddNode(n);
            }
        }

        public void LinkTemplate()
        {
            template = CrewHandler.Instance.TrainingTemplates.Find(c => c.id == id);
        }

        public bool MeetsStudentReqs(ProtoCrewMember student)
        {
            if (!(student.type == ProtoCrewMember.KerbalType.Crew && (template.seatMax <= 0 || Students.Count < template.seatMax) && !student.inactive 
                && student.rosterStatus == ProtoCrewMember.RosterStatus.Available && !Students.Contains(student)))
                return false;

            bool checkPrereq = template.prereq != null;
            bool checkConflict = template.conflict != null;
            for (int entryIdx = student.careerLog.Count; entryIdx-- > 0 && (checkPrereq || checkConflict);)
            {
                FlightLog.Entry e = student.careerLog.Entries[entryIdx];

                if (checkPrereq && e.type == template.prereq.type && e.target == template.prereq.target)
                    checkPrereq = false;

                if (checkConflict && e.type == template.conflict.type && e.target == template.conflict.target)
                    return false;
            }

            return !checkPrereq;
        }
        public void AddStudent(ProtoCrewMember student)
        {
            if (template.seatMax <= 0 || Students.Count < template.seatMax)
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

        public double GetTime(List<ProtoCrewMember> students)
        {
            if (Started)
            {
                if (baseCourseTime == 0d)
                    baseCourseTime = template.GetBaseTime(students);

                return baseCourseTime * template.GetTimeMultiplierFacility() / CurrencyUtils.Rate(TransactionReasonsRP0.RateTraining);
            }

            return template.GetTime(students);
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
                start = KSPUtils.GetUT();
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
                double length = GetTime(Students);
                List<string> retirementChanges = new List<string>();
                foreach (ProtoCrewMember student in Students)
                {
                    if (student == null)
                        continue;

                    if (student.flightLog.Count > 0)
                        student.ArchiveFlightLog();

                    // First, expire any old mission trainings.
                    if (template.training.type == CrewHandler.TrainingType_Mission)
                    {
                        // Expire any previous mission trainings because only 1 should be active at a time
                        for (int i = student.careerLog.Count; i-- > 0;)
                        {
                            FlightLog.Entry e = student.careerLog.Entries[i];
                            if (e.type == CrewHandler.TrainingType_Mission)
                            {
                                CrewHandler.Instance.RemoveExpiration(student.name, e);
                                CrewHandler.ExpireFlightLogEntry(e);
                            }
                        }
                    }

                    // Create a new TrainingExpiration if needed
                    if (template.expiration > 0d)
                    {
                        double expireTime = template.expiration;
                        if (template.expirationUseStupid)
                            expireTime *= UtilMath.Lerp(CrewHandler.Settings.trainingProficiencyStupidMin,
                                CrewHandler.Settings.trainingProficiencyStupidMax,
                                student.stupidity);
                        expireTime += KSPUtils.GetUT();

                        CrewHandler.Instance.AddExpiration(new TrainingExpiration(student.name, expireTime, new TrainingFlightEntry(template.training.type, template.training.target)));
                    }

                    double retireTimeOffset = CrewHandler.Instance.GetRetirementOffsetForTraining(student, length, template.training.type, template.training.target, student.careerLog.Entries.Count - 1);

                    student.flightLog.AddEntry(template.training.type, template.training.target);
                    student.ArchiveFlightLog();

                    double actualRetireOffset = CrewHandler.Instance.IncreaseRetireTime(student.name, retireTimeOffset);
                    if (actualRetireOffset > 0d)
                        retirementChanges.Add($"\n{student.name}, +{KSPUtil.PrintDateDelta(actualRetireOffset, false, false)}, no earlier than {KSPUtil.PrintDate(CrewHandler.Instance.GetRetireTime(student.name), false)}");
                }

                if (CrewHandler.Instance.RetirementEnabled && retirementChanges.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append($"{template.name} training completed! The following retirement changes have occurred:");
                    foreach (string s in retirementChanges)
                        sb.Append(s);

                    PopupDialog.SpawnPopupDialog(new UnityEngine.Vector2(0.5f, 0.5f),
                                             new UnityEngine.Vector2(0.5f, 0.5f),
                                             "CrewUpdateNotification",
                                             "Crew Updates",
                                             sb.ToString(),
                                             "OK",
                                             true,
                                             HighLogic.UISkin).PrePostActions(ControlTypes.KSC_ALL | ControlTypes.UI_MAIN);
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
            if (template.seatMax > 0 && studentCount > template.seatMax)
                return false;
            if (template.seatMin > 0 && studentCount < template.seatMin)
                return false;

            Started = true;

            startTime = KSPUtils.GetUT();


            foreach (ProtoCrewMember student in Students)
                student.SetInactive(GetTime(Students) + 1d);

            return true;
            //fire an event
        }

        public string GetItemName()
        {
            return template.name;
        }

        public double GetBuildRate()
        {
            return 1d;
        }

        public double GetFractionComplete()
        {
            return (KSPUtils.GetUT() - startTime) / GetTime();
        }

        public double GetTimeLeft()
        {
            return GetTime() - (KSPUtils.GetUT() - startTime);
        }

        public double GetTimeLeftEst(double offset)
        {
            return GetTimeLeft();
        }

        public KerbalConstructionTime.BuildListVessel.ListType GetListType()
        {
            return KerbalConstructionTime.BuildListVessel.ListType.Crew;
        }

        public bool IsComplete()
        {
            return Completed;
        }

        public double IncrementProgress(double UTDiff)
        {
            return 0d;
        }

        public double AverageRetireExtension()
        {
            double count = Students.Count;
            if (count == 0d)
                return 0d;

            double sumOffset = 0d;
            double trainingLength = GetTime();
            foreach (var pcm in Students)
            {
                sumOffset += CrewHandler.Instance.GetRetirementOffsetForTraining(pcm, trainingLength, template.training.type, template.training.target);
            }
            return sumOffset / count;
        }
    }
}
