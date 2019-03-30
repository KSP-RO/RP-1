using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.Crew
{
    public enum RepeatMode {NEVER, EXPIRED, ALWAYS}; //is the course repeatable?

    public class CourseTemplate
    {
        public ConfigNode sourceNode = new ConfigNode();
        
        public string id = "";
        public string name = "";
        public string description = "";

        public bool Available = true; //Whether the course is currently being offered

        public string[,] preReqsAny = { };
        public string[,] preReqs = { };
        public bool[] pChecker = { }; // prereq checker;
        public string[,] conflicts = { }; //course IDs that cannot be taken while this course is not expired

        public double time = 0; //how much time the course takes (in seconds)
        public bool timeUseStupid = false;
        public bool required = false; //whether the course is required for the kerbal to be usable
        public RepeatMode repeatable = RepeatMode.NEVER; //whether the course can be repeated

        public string[] classes = { }; //which classes can take this course (empty == all)
        public int minLevel = 0; //minimum kerbal level required to take the course
        public int maxLevel = 99; //max kerbal level allowed to take the course

        public double expiration = 0d;
        public bool expirationUseStupid = false;

        public int seatMax = -1; //maximum number of kerbals allowed in the course at once
        public int seatMin = 0; //minimum number of kerbals required to start the course
        
        public int rewardXP = 0; //pure XP reward
        public ConfigNode RewardLog = null; //the flight log to insert
        public ConfigNode ExpireLog = null; // expire all these on complete

        public bool isTemporary = false;

        public CourseTemplate(ConfigNode source)
        {
            sourceNode = source;
        }

        public CourseTemplate(ConfigNode source, bool copy)
        {
            if (copy)
                sourceNode = source.CreateCopy();
            else
                sourceNode = source;
        }

        public CourseTemplate()
        {

        }

        /// <summary>
        /// Populates all fields from the provided ConfigNode, parsing all formulas
        /// </summary>
        /// <param name="variables"></param>
        public void PopulateFromSourceNode(ConfigNode source = null)
        {
            if (source == null)
                source = sourceNode;

            id = source.GetValue("id");
            name = source.GetValue("name");
            description = source.GetValue("description");

            source.TryGetValue("Available", ref Available);

            string tmpStr = source.GetValue("preReqsAny");
            if (!string.IsNullOrEmpty(tmpStr))
            {
                string[] split1 = tmpStr.Split(',');
                int iC = split1.Length;
                preReqsAny = new string[iC, 2];

                for (int i = 0; i < iC; ++i)
                {
                    string[] split2 = split1[i].Split(':');
                    preReqsAny[i, 0] = split2[0];
                    if (split2.Length > 1)
                        preReqsAny[i, 1] = split2[1];
                    else
                        preReqsAny[i, 1] = string.Empty;
                }
            }

            tmpStr = source.GetValue("preReqs");
            if (!string.IsNullOrEmpty(tmpStr))
            {
                string[] split1 = tmpStr.Split(',');
                int iC = split1.Length;
                preReqs = new string[iC, 2];
                pChecker = new bool[iC];

                for (int i = 0; i < iC; ++i)
                {
                    string[] split2 = split1[i].Split(':');
                    preReqs[i, 0] = split2[0];
                    if (split2.Length > 1)
                        preReqs[i, 1] = split2[1];
                    else
                        preReqs[i, 1] = string.Empty;
                }
            }

            tmpStr = source.GetValue("conflicts");
            if (!string.IsNullOrEmpty(tmpStr))
            {
                string[] split1 = tmpStr.Split(',');
                int iC = split1.Length;
                conflicts = new string[iC, 2];

                for (int i = 0; i < iC; ++i)
                {
                    string[] split2 = split1[i].Split(':');
                    conflicts[i, 0] = split2[0];
                    if (split2.Length > 1)
                        conflicts[i, 1] = split2[1];
                    else
                        conflicts[i, 1] = string.Empty;
                }
            }

            source.TryGetValue("time", ref time);
            source.TryGetValue("timeUseStupid", ref timeUseStupid);
            source.TryGetValue("expiration", ref expiration);
            source.TryGetValue("expirationUseStupid", ref expirationUseStupid);

            source.TryGetValue("required", ref required);
            source.TryGetValue("isTemporary", ref isTemporary);

            string repeatStr = source.GetValue("repeatable");
            if (!string.IsNullOrEmpty(repeatStr))
            {
                switch (repeatStr)
                {
                    case "NEVER": repeatable = RepeatMode.NEVER; break;
                    case "EXPIRED": repeatable = RepeatMode.EXPIRED; break;
                    case "ALWAYS": repeatable = RepeatMode.ALWAYS; break;
                    default: repeatable = RepeatMode.NEVER; break;
                }
            }

            tmpStr = source.GetValue("classes");
            if (!string.IsNullOrEmpty(tmpStr))
                classes = tmpStr.Split(',');

            source.TryGetValue("minLevel", ref minLevel);
            source.TryGetValue("maxLevel", ref maxLevel);

            source.TryGetValue("seatMax", ref seatMax);
            source.TryGetValue("seatMin", ref seatMin);
            
            //get the REWARD nodes and replace any variables in there too
            ConfigNode r = source.GetNode("REWARD");
            if (r != null)
            {
                RewardLog = r.GetNode("FLIGHTLOG");
                ExpireLog = r.GetNode("EXPIRELOG");
                r.TryGetValue("XPAmt", ref rewardXP);
            }
        }

        public double GetTime(List<ProtoCrewMember> students)
        {
            double curTime = time;

            double level = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
            curTime *= (1d - level * 0.5d);

            if (students == null || students.Count == 0 || !timeUseStupid)
                return curTime;

            double averageStupid = 0d;
            int sC = students.Count;
            for(int i = sC; i-- > 0;)
                averageStupid += students[i].stupidity;

            averageStupid /= sC;

            return curTime * UtilMath.Lerp(CrewHandler.Instance.settings.trainingMissionStupidMin, CrewHandler.Instance.settings.trainingMissionStupidMax, averageStupid);
        }

        public double GetExpiration(ProtoCrewMember pcm)
        {
            if (pcm == null || !expirationUseStupid)
                return expiration;

            return expiration * (1.5d - pcm.stupidity);
        }
    }
}
