using KerbalConstructionTime;
using RP0.DataTypes;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RP0.Crew
{
    public class TrainingCourse : ConfigNodePersistenceBase, IKCTBuildItem, IConfigNode
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

        private TrainingTemplate _template;
        public bool FromTemplate(TrainingTemplate t) => t == _template;

        public int SeatMin => _template?.seatMin ?? 1;
        public int SeatMax => _template?.seatMax ?? 0;
        public string Description => _template?.description;
        public bool IsTemporary => _template?.isTemporary ?? false;
        public TrainingTemplate.TrainingType Type => _template?.type ?? TrainingTemplate.TrainingType.Proficiency;

        public List<AvailablePart> PartsCovered => _template?.partsCovered;

        protected double _buildRate = 1d;

        public TrainingCourse()
        {
        }

        public TrainingCourse(TrainingTemplate template)
        {
            id = template.id;
            _template = template;
            RecalculateBP();
        }

        public TrainingCourse(ConfigNode node)
        {
            Load(node);
        }

        public void LinkTemplate()
        {
            _template = CrewHandler.Instance.TrainingTemplates.Find(c => c.id == id);
            if (_template == null)
                Debug.LogWarning($"[RP-0] Template not found for linking: {id}");
        }

        public bool MeetsStudentReqs(ProtoCrewMember student)
        {
            if (!(student.type == ProtoCrewMember.KerbalType.Crew && (_template.seatMax <= 0 || Students.Count < _template.seatMax) && !student.inactive 
                && student.rosterStatus == ProtoCrewMember.RosterStatus.Available && !Students.Contains(student)))
                return false;

            bool checkPrereq = _template.prereq != null;
            bool checkConflict = _template.conflict != null;
            for (int entryIdx = student.careerLog.Count; entryIdx-- > 0 && (checkPrereq || checkConflict);)
            {
                FlightLog.Entry e = student.careerLog.Entries[entryIdx];

                if (checkPrereq && e.type == _template.prereq.type && e.target == _template.prereq.target)
                    checkPrereq = false;

                if (checkConflict && e.type == _template.conflict.type && e.target == _template.conflict.target)
                    return false;
            }

            return !checkPrereq;
        }

        public void AddStudent(ProtoCrewMember student)
        {
            if (_template.seatMax <= 0 || Students.Count < _template.seatMax)
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

        public void AbortCourse()
        {
            if (!Started)
                return;


            var sb = StringBuilderCache.Acquire();
            
            foreach (var student in Students)
            {
                student.inactive = false;
            }
            Students.Clear();
            Completed = true;
        }

        public void CompleteCourse()
        {
            //assign rewards to all kerbals and set them to free
            if (Completed && _template != null)
            {
                double length = Planetarium.GetUniversalTime() - startTime;
                List<string> retirementChanges = new List<string>();
                foreach (ProtoCrewMember student in Students)
                {
                    if (student == null)
                        continue;

                    if (student.flightLog.Count > 0)
                        student.ArchiveFlightLog();

                    // First, expire any old mission trainings.
                    if (_template.training.type == CrewHandler.TrainingType_Mission)
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
                    if (_template.expiration > 0d)
                    {
                        double expireTime = _template.expiration;
                        if (_template.expirationUseStupid)
                            expireTime *= UtilMath.Lerp(CrewHandler.Settings.trainingProficiencyStupidMin,
                                CrewHandler.Settings.trainingProficiencyStupidMax,
                                student.stupidity);
                        expireTime += Planetarium.GetUniversalTime();

                        CrewHandler.Instance.AddExpiration(new TrainingExpiration(student.name, expireTime, new TrainingFlightEntry(_template.training.type, _template.training.target)));
                    }

                    double retireTimeOffset = CrewHandler.Instance.GetRetirementOffsetForTraining(student, length, _template.training.type, _template.training.target, student.careerLog.Entries.Count - 1);

                    student.flightLog.AddEntry(_template.training.type, _template.training.target);
                    student.ArchiveFlightLog();

                    double actualRetireOffset = CrewHandler.Instance.IncreaseRetireTime(student.name, retireTimeOffset);
                    if (actualRetireOffset > 0d)
                        retirementChanges.Add($"\n{student.name}, +{KSPUtil.PrintDateDelta(actualRetireOffset, false, false)}, no earlier than {KSPUtil.PrintDate(CrewHandler.Instance.GetRetireTime(student.name), false)}");
                }

                if (CrewHandler.Instance.RetirementEnabled && retirementChanges.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append($"{_template.name} training completed! The following retirement changes have occurred:");
                    foreach (string s in retirementChanges)
                        sb.Append(s);

                    PopupDialog.SpawnPopupDialog(new UnityEngine.Vector2(0.5f, 0.5f),
                                             new UnityEngine.Vector2(0.5f, 0.5f),
                                             "CrewUpdateNotification",
                                             "Crew Updates",
                                             sb.ToString(),
                                             KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
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
            if (_template.seatMax > 0 && studentCount > _template.seatMax)
                return false;
            if (_template.seatMin > 0 && studentCount < _template.seatMin)
                return false;

            Started = true;
            startTime = Planetarium.GetUniversalTime();

            foreach (ProtoCrewMember student in Students)
                student.SetInactive(_template.GetBaseTime(Students) * 1.2d);

            return true;
            //fire an event
        }

        public string GetItemName()
        {
            return _template?.name ?? "Unknown training course";
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
            double increment = UTDiff * GetBuildRate();
            progress += increment;
            if (progress < BP)
                return 0d;

            Completed = true;
            CompleteCourse();

            double remainder = progress - BP;
            return remainder / increment * UTDiff;
        }

        private void RecalculateBP()
        {
            if (_template == null)
            {
                Debug.LogWarning($"[RP-0] TrainingCourse RecalculateBP not possible because template is empty: {id}");
                return;
            }
            BP = _template.GetBaseTime(Students);
        }

        public double AverageRetireExtension()
        {
            double count = Students.Count;
            if (count == 0d)
                return 0d;

            double sumOffset = 0d;
            double trainingLength = BP / GetBuildRate();
            foreach (var pcm in Students)
            {
                sumOffset += CrewHandler.Instance.GetRetirementOffsetForTraining(pcm, trainingLength, _template.training.type, _template.training.target);
            }
            return sumOffset / count;
        }
    }
}
