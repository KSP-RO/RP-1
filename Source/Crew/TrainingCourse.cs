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
        private double progress = 0d;

        [Persistent]
        private double BP = 0d;

        [Persistent]
        private double startTime;

        [Persistent]
        public bool Started = false;

        [Persistent]
        public bool Completed = false;

        private TrainingTemplate template;
        public int seatMin => template.seatMin;
        public int seatMax => template.seatMax;
        public string description => template.description;
        public bool isTemporary => template.isTemporary;

        protected double _buildRate = 1d;

        public TrainingCourse()
        {
        }

        public TrainingCourse(TrainingTemplate template)
        {
            id = template.id;
            this.template = template;
            RecalculateBP();
        }

        public TrainingCourse(ConfigNode node)
        {
            Load(node);
            if (CrewHandler.Instance.saveVersion < 3)
            {
                node.TryGetValue("elapsedTime", ref progress);
            }
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
                {
                    Students.Add(student);
                    RecalculateBP();
                }
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
                    student.inactive = false;
                    if (Students.Count == 0)
                    {
                        CompleteCourse();   // cancel the course
                    }
                }
                else
                {
                    RecalculateBP();
                }
            }
        }
        public void RemoveStudent(string student)
        {
            RemoveStudent(HighLogic.CurrentGame.CrewRoster[student]);
        }

        public void CompleteCourse()
        {
            //assign rewards to all kerbals and set them to free
            if (Completed)
            {
                double length = KSPUtils.GetUT() - startTime;
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
                student.SetInactive(template.GetBaseTime(Students) * 1.2d);

            return true;
            //fire an event
        }

        public string GetItemName()
        {
            return template.name;
        }

        public double GetFractionComplete()
        {
            return progress / BP;
        }

        public double GetTimeLeft()
        {
            return (BP - progress) / GetBuildRate();
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

        public static double FacilityTrainingRate(double fracLevel) => 1d / (1d - fracLevel * 0.5);

        public static double CalculateBuildRate()
        {
            double r = 1d;
            r *= FacilityTrainingRate(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex));
            r *= CurrencyUtils.Rate(TransactionReasonsRP0.RateTraining);
            return r;
        }

        public double GetBuildRate()
        {
            if (_buildRate < 0d)
                _buildRate = CalculateBuildRate();

            return _buildRate;
        }

        public void RecalculateBuildRate()
        {
            _buildRate = CalculateBuildRate();
        }

        public double IncrementProgress(double UTDiff)
        {
            // back-compat
            if (BP == 0)
                BP = template.GetBaseTime(Students);

            double increment = UTDiff * GetBuildRate();
            progress += increment;
            if (progress < BP)
                return 0d;

            Completed = true;
            CompleteCourse();

            double remainder = progress - BP;
            return remainder / increment * UTDiff;
        }

        private void RecalculateBP() { BP = template.GetBaseTime(Students); }

        public double AverageRetireExtension()
        {
            double count = Students.Count;
            if (count == 0d)
                return 0d;

            double sumOffset = 0d;
            double trainingLength = BP / GetBuildRate();
            foreach (var pcm in Students)
            {
                sumOffset += CrewHandler.Instance.GetRetirementOffsetForTraining(pcm, trainingLength, template.training.type, template.training.target);
            }
            return sumOffset / count;
        }
    }
}
