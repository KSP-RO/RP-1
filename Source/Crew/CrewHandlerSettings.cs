using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;
using System.Reflection;

namespace RP0.Crew
{
    public class CrewHandlerSettings : IConfigNode
    {
        [Persistent]
        public Vector2 recSpace = new Vector2(2, 2), recOrbit = new Vector2(5, 10), recOtherBody = new Vector2(10, 10),
            recEVA = new Vector2(15, 20), recEVAOther = new Vector2(20, 25), recOrbitOther = new Vector2(15, 10), recLandOther = new Vector2(30, 30);

        [Persistent]
        public double retireOffsetBaseMult = 100d, retireOffsetFlightNumPow = 1.5d, retireOffsetFlightNumOffset = -3d, retireOffsetStupidMin = 1.4d, retireOffsetStupidMax = 0.8d;

        [Persistent]
        public double retireBaseYears = 5d, retireCourageMin = 0d, retireCourageMax = 3d, retireStupidMin = 1d, retireStupidMax = 0d;

        [Persistent]
        public double trainingProficiencyStupidMin = 1.5d, trainingProficiencyStupidMax = 0.5d, trainingProficiencyRefresherTimeMult = 0.25d;

        [Persistent]
        public int trainingProficiencyXP = 1;

        [Persistent]
        public double trainingMissionExpirationDays = 120d, trainingMissionStupidMin = 0.5d, trainingMissionStupidMax = 1.5d;

        [Persistent]
        public double minFlightDurationSecondsForTrainingExpire = 30d;


        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
