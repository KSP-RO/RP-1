using System.Collections.Generic;
using UnityEngine;

namespace RP0.Crew
{
    public class CrewHandlerSettings : IConfigNode
    {
        [Persistent]
        public double retireOffsetBaseMult = 100, retireOffsetFlightNumPow = 1.5, retireOffsetStupidMin = 1.4, retireOffsetStupidMax = 0.8, retireIncreaseCap = 473040000;

        [Persistent]
        public double inactivityMinFlightDurationDays = 10, inactivityFlightDurationExponent = 0.7, inactivityMaxSituationMult = 20;

        [Persistent]
        public double retireBaseYears = 5, retireCourageMin = 0, retireCourageMax = 3, retireStupidMin = 1, retireStupidMax = 0;

        [Persistent]
        public double trainingProficiencyStupidMin = 1.5, trainingProficiencyStupidMax = 0.5, trainingProficiencyRefresherTimeMult = 0.25;

        [Persistent]
        public int trainingProficiencyXP = 1;

        [Persistent]
        public double trainingMissionExpirationDays = 120, trainingMissionStupidMin = 0.5, trainingMissionStupidMax = 1.5;

        [Persistent]
        public double minFlightDurationSecondsForTrainingExpire = 30;

        [Persistent]
        public double retireIncreaseMultiplierToTrainingLengthProficiency = 0.125d;

        [Persistent]
        public double retireIncreaseMultiplierToTrainingLengthMission = 0.25d;

        [Persistent]
        public double flightHighAltitude = 40000;

        public Dictionary<string, double> situationValues = new Dictionary<string, double>();

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);

            var subNodes = node.GetNodes("SITUATIONVALUES");
            foreach (ConfigNode subNode in subNodes)
            {
                foreach (ConfigNode.Value configVal in subNode.values)
                {
                    if (situationValues.ContainsKey(configVal.name))
                    {
                        Debug.LogError("[RP-0] Duplicate SITUATIONVALUE " + configVal.name);
                    }
                    else
                    {
                        situationValues.Add(configVal.name, double.Parse(configVal.value));
                    }
                }
            }
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
