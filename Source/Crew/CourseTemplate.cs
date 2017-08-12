using System;
using System.Collections.Generic;
using System.Linq;
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

        public string[] activePreReqs = { }; //prereqs that must not be expired
        public string[,] preReqs = { }; //prereqs that must be taken, but can be expired
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
       // public ConfigNode[] Expiry = { }; //The list of ways that course experience can be lost

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

            string tmpStr = source.GetValue("activePreReqs");
            if (!string.IsNullOrEmpty(tmpStr))
                activePreReqs = tmpStr.Split(',');

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
                r.TryGetValue("XPAmt", ref rewardXP);
            }

            /*  Expiry = source.GetNodes("EXPIRY");
              foreach (ConfigNode node in Expiry)
                  ConfigNodeExtensions.ReplaceValuesInNode(node, variables);*/

            /*string logStr = "Course created";
            logStr += "\nID: " + id;
            logStr += "\nName: " + name;
            logStr += "\nAvailable: " + Available;
            logStr += "\nprereqs: " + preReqs.GetLength(0);
            logStr += "\ntime: " + time;
            logStr += "\nrepeatable: " + repeatable;
            logStr += "\nXP: " + rewardXP;
            logStr += "\nLog: ";
            if (RewardLog != null)
                foreach (ConfigNode.Value v in RewardLog.values)
                    logStr += "\n" + v.value;

            UnityEngine.Debug.Log(logStr);*/
        }

        public double GetTime(List<ProtoCrewMember> students)
        {
            if (students == null || !timeUseStupid)
                return time;

            double averageStupid = 0d;
            int sC = students.Count;
            for(int i = sC; i-- > 0;)
                averageStupid += students[i].stupidity;

            averageStupid /= sC;

            return time * UtilMath.Lerp(CrewHandler.Instance.settings.trainingMissionStupidMin, CrewHandler.Instance.settings.trainingMissionStupidMax, averageStupid);
        }

        public double GetExpiration(ProtoCrewMember pcm)
        {
            if (pcm == null || !expirationUseStupid)
                return expiration;

            return expiration * (1.5d - pcm.stupidity);
        }
    }
}
