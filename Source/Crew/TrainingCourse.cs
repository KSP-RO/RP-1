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

        [Persistent(name = "STUDENTS")]
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
        public ConfigNode RewardLog => template.RewardLog;

        public double baseCourseTime = 0d;

        public TrainingCourse(TrainingTemplate template)
        {
            id = template.id;
        }

        public TrainingCourse(ConfigNode node)
        {
            Load(node);
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
            template = CrewHandler.Instance.TrainingTemplates.Find(c => c.id == id);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }

        public bool MeetsStudentReqs(ProtoCrewMember student)
        {
            if (!(student.type == ProtoCrewMember.KerbalType.Crew && (template.seatMax <= 0 || Students.Count < template.seatMax) && !student.inactive 
                && student.rosterStatus == ProtoCrewMember.RosterStatus.Available && student.experienceLevel >= template.minLevel && student.experienceLevel <= template.maxLevel 
                && (template.classes.Length == 0 || template.classes.Contains(student.trait)) && !Students.Contains(student)))
                return false;

            int pCount = template.preReqs.GetLength(0);
            int pACount = template.preReqsAny.GetLength(0);
            int cCount = template.conflicts.GetLength(0);
            if (pCount > 0 || cCount > 0 || pACount > 0)
            {
                for (int i = pCount; i-- > 0;)
                    template.pChecker[i] = true;

                int needCount = pCount;
                bool needAnyStill = pACount > 0;

                for (int entryIdx = student.careerLog.Count; entryIdx-- > 0 && (needCount > 0 || cCount > 0 || needAnyStill);)
                {
                    FlightLog.Entry e = student.careerLog.Entries[entryIdx];

                    string tgt = string.IsNullOrEmpty(e.target) ? string.Empty : e.target;

                    for (int preIdx = pCount; preIdx-- > 0 && needCount > 0;)
                    {
                        if (template.pChecker[preIdx] && (e.type == template.preReqs[preIdx, 0] && tgt == template.preReqs[preIdx, 1]))
                        {
                            template.pChecker[preIdx] = false;
                            --needCount;
                        }
                    }

                    for (int anyIdx = pACount; anyIdx-- > 0 && needAnyStill;)
                    {
                        if (e.type == template.preReqsAny[anyIdx, 0] && tgt == template.preReqsAny[anyIdx, 1])
                            needAnyStill = false;
                    }

                    for (int conIdx = cCount; conIdx-- > 0;)
                    {
                        if (e.type == template.conflicts[conIdx, 0] && tgt == template.conflicts[conIdx, 1])
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

                    double retireTimeOffset = 0d;
                    
                    if (template.ExpireLog != null)
                    {
                        foreach (ConfigNode.Value v in template.ExpireLog.values)
                        {
                            for (int i = student.careerLog.Count; i-- > 0;)
                            {
                                FlightLog.Entry e = student.careerLog.Entries[i];
                                if (TrainingExpiration.Compare(v.value, e))
                                {
                                    e.type = "expired_" + e.type;
                                    CrewHandler.Instance.RemoveExpiration(student.name, v.value);
                                    break;
                                }
                            }
                        }
                    }

                    if (template.RewardLog != null)
                    {
                        if (student.flightLog.Count > 0)
                            student.ArchiveFlightLog();

                        TrainingExpiration exp = null;
                        if (template.expiration > 0d)
                        {
                            exp = new TrainingExpiration();
                            exp.PcmName = student.name;
                            exp.Expiration = template.expiration;
                            if (template.expirationUseStupid)
                                exp.Expiration *= UtilMath.Lerp(CrewHandler.Settings.trainingProficiencyStupidMin,
                                    CrewHandler.Settings.trainingProficiencyStupidMax,
                                    student.stupidity);
                            exp.Expiration += KSPUtils.GetUT();
                        }

                        bool prevMissionsAlreadyExpired = false;
                        int lastEntryIndex = student.careerLog.Entries.Count - 1;
                        foreach (ConfigNode.Value v in template.RewardLog.values)
                        {
                            string[] s = v.value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            string trainingType = s[0];
                            string trainingTarget = s.Length == 1 ? null : s[1];

                            retireTimeOffset = Math.Max(retireTimeOffset, CrewHandler.Instance.GetRetirementOffsetForTraining(student, length, trainingType, trainingTarget, lastEntryIndex));

                            if (!prevMissionsAlreadyExpired && trainingType == CrewHandler.TrainingType_Mission)
                            {
                                // Expire any previous mission trainings because only 1 should be active at a time
                                for (int i = student.careerLog.Count; i-- > 0;)
                                {
                                    FlightLog.Entry e = student.careerLog.Entries[i];
                                    if (e.type == CrewHandler.TrainingType_Mission)
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
                            if (template.expiration > 0d)
                                exp.Entries.Add(v.value);
                        }

                        if (template.expiration > 0d)
                            CrewHandler.Instance.AddExpiration(exp);
                    }

                    if (template.rewardXP != 0)
                        student.ExtraExperience += template.rewardXP;

                    double retireOffset = CrewHandler.Instance.IncreaseRetireTime(student.name, retireTimeOffset);
                    if(retireOffset > 0d)
                        retirementChanges.Add($"\n{student.name}, +{KSPUtil.PrintDateDelta(retireOffset, false, false)}, no earlier than {KSPUtil.PrintDate(CrewHandler.Instance.GetRetireTime(student.name), false)}");
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
    }
}
