using ROUtils.DataTypes;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RP0.Crew
{
    public class TrainingCourse : ConfigNodePersistenceBase, ISpaceCenterProject, IConfigNode
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

        /// <summary>
        /// False when the course's template could not be relinked (e.g. the part was never
        /// purchased and its experimental tech node's template wasn't regenerated). Such a
        /// course is stalled: it makes no progress and cannot complete until the template
        /// comes back, which happens once the player purchases the part.
        /// </summary>
        public bool HasTemplate => _template != null;

        public int SeatMin => _template?.seatMin ?? 1;
        public int SeatMax => _template?.seatMax ?? 0;
        public string Description => _template?.description;
        public bool IsTemporary => _template?.isTemporary ?? false;
        public TrainingTemplate.TrainingType Type => _template?.type ?? TrainingTemplate.TrainingType.Proficiency;
        public string Target => _template?.training?.target ?? string.Empty;
        public int ACLevelRequirement => _template?.ACLevelRequirement ?? 0;

        public List<AvailablePart> PartsCovered => _template?.partsCovered;

        protected double _buildRate = -1d;

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
                RP0Debug.LogWarning($"Template not found for linking: {id}");
        }

        /// <summary>
        /// Whether a student may join this course. Pass allowInactive when assembling a course to
        /// queue: crew currently on R&R can be selected and the course will wait for them to return.
        /// </summary>
        public bool MeetsStudentReqs(ProtoCrewMember student, bool allowInactive = false)
        {
            if (!(student.type == ProtoCrewMember.KerbalType.Crew && (_template.seatMax <= 0 || Students.Count < _template.seatMax) && (allowInactive || !student.inactive)
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

        /// <summary>
        /// True when every enrolled student is on active duty and can begin training right now
        /// (not on R&R, not flying, not departed). Used to decide when a queued course starts.
        /// </summary>
        public bool AllStudentsReady()
        {
            if (Students.Count == 0)
                return false;

            foreach (ProtoCrewMember student in Students)
            {
                if (student == null || student.inactive ||
                    student.rosterStatus != ProtoCrewMember.RosterStatus.Available)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Remove students who can never return to this queued course - dead, MIA, or retired
        /// (retirement sets the kerbal to Dead). Crew who are merely flying (Assigned) or on R&R
        /// (inactive) are kept, since wait-for-all waits for them. Returns the display names of
        /// those dropped. Only meaningful before the course has started.
        /// </summary>
        public List<string> PruneDepartedStudents()
        {
            var dropped = new List<string>();
            var toRemove = new List<ProtoCrewMember>();
            foreach (ProtoCrewMember student in Students)
            {
                if (student == null ||
                    student.rosterStatus == ProtoCrewMember.RosterStatus.Dead ||
                    student.rosterStatus == ProtoCrewMember.RosterStatus.Missing)
                {
                    if (student != null)
                        dropped.Add(student.displayName);
                    toRemove.Add(student);
                }
            }

            foreach (ProtoCrewMember student in toRemove)
                Students.Remove(student);

            if (toRemove.Count > 0)
                RecalculateBP();

            return dropped;
        }

        /// <summary>
        /// Estimated UT this queued course will begin: when its last on-leave student returns to
        /// duty. Returns now if no student is currently on R&R.
        /// </summary>
        public double GetProjectedStartUT()
        {
            double t = Planetarium.GetUniversalTime();
            foreach (ProtoCrewMember student in Students)
            {
                if (student != null && student.inactive && student.inactiveTimeEnd > t)
                    t = student.inactiveTimeEnd;
            }
            return t;
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
                    else
                    {
                        // Increase the training time if more proficient nauts are removed from course.
                        // Do not decrease training time if less proficient nauts are removed.
                        // Course completing instantly in the second case wouldn't be ideal.
                        double befBP = BP;
                        RecalculateBP();
                        BP = Math.Max(BP, befBP);
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
                    else if (_template.training.type != CrewHandler.TrainingType_Proficiency)
                    {
                        RP0Debug.LogError($"Unknown training type {_template.training.type} for course {_template.name} of student {student.name}!");
                        return;
                    }

                    // Create a new TrainingExpiration if needed
                    if (_template.expiration > 0d)
                    {
                        double expireTime = _template.expiration;
                        if (_template.expirationUseStupid)
                            expireTime *= UtilMath.Lerp(Database.SettingsCrew.trainingProficiencyStupidMin,
                                Database.SettingsCrew.trainingProficiencyStupidMax,
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

                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "CrewUpdateNotification",
                                             "Crew Updates",
                                             sb.ToString(),
                                             KSP.Localization.Localizer.GetStringByTag("#autoLOC_190905"),
                                             true,
                                             HighLogic.UISkin,
                                             !HighLogic.LoadedSceneIsFlight).HideGUIsWhilePopupNonFlight();
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

        public ProjectType GetProjectType()
        {
            return ProjectType.Crew;
        }

        public bool IsComplete()
        {
            return Completed;
        }

        public double CalculateBuildRate()
        {
            double r = 1d;
            r *= Database.SettingsCrew.ACTrainingRates[KCTUtilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex)];
            r *= CurrencyUtils.Rate(TransactionReasonsRP0.RateTraining);
            r *= Type == TrainingTemplate.TrainingType.Proficiency ? CrewHandler.Instance.ProfTrainRate : CrewHandler.Instance.MissionTrainRate;
            return r;
        }

        public double GetBuildRate()
        {
            // A stalled course (missing template) isn't progressing. Reporting a zero rate
            // keeps it out of the warp-to-next-completion logic and sorts it to the bottom
            // of the build list.
            if (_template == null)
                return 0d;

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
            // Stall the course if its template is missing rather than completing it with no
            // reward (CompleteCourse skips its reward logic when _template is null). Progress
            // and BP are persisted, so the course resumes where it left off once relinked.
            if (_template == null)
                return 0d;

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
                RP0Debug.LogWarning($"TrainingCourse RecalculateBP not possible because template is empty: {id}");
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