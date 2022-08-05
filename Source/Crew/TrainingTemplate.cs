using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.Crew
{
    public class TrainingTemplate
    {
        public string id;
        public string name;
        public string description;

        public FlightLog.Entry prereq;
        public FlightLog.Entry conflict;

        public double time = 0; //how much time the course takes (in seconds)
        public bool timeUseStupid = false;

        public double expiration = 0d;
        public bool expirationUseStupid = false;

        public int seatMax = -1; //maximum number of kerbals allowed in the course at once
        public int seatMin = 0; //minimum number of kerbals required to start the course

        public FlightLog.Entry training;
        
        public bool isTemporary = false;

        internal string PartsTooltip;

        public TrainingTemplate()
        {
        }

        public double GetBaseTime(List<ProtoCrewMember> students)
        {
            double curTime = time;
            if (training != null && students.Count > 0)
            {
                if (training.type == CrewHandler.TrainingType_Proficiency)
                {
                    double sumTime = 0d;
                    foreach(var pcm in students)
                        sumTime += TrainingDatabase.GetProficiencyTime(training.target, pcm);
                    curTime = 1d + (sumTime / students.Count) * 86400d;
                }
            }

            if (students == null || students.Count == 0 || !timeUseStupid)
                return curTime;

            double averageStupid = 0d;
            int sC = students.Count;
            for(int i = sC; i-- > 0;)
                averageStupid += students[i].stupidity;

            averageStupid /= sC;

            return curTime * UtilMath.Lerp(CrewHandler.Settings.trainingMissionStupidMin, CrewHandler.Settings.trainingMissionStupidMax, averageStupid);
        }

        public double GetTimeMultiplierFacility()
        {
            double level = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
            return (1d - level * 0.5d);
        }

        public virtual double GetTime(List<ProtoCrewMember> students)
        {
            return GetBaseTime(students) * GetTimeMultiplierFacility() / CurrencyUtils.Rate(TransactionReasonsRP0.RateTraining);
        }

        public double GetExpiration(ProtoCrewMember pcm)
        {
            if (pcm == null || !expirationUseStupid)
                return expiration;

            return expiration * (1.5d - pcm.stupidity);
        }
    }
}
