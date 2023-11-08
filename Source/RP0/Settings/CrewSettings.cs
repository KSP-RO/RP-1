using ROUtils.DataTypes;

namespace RP0
{
    public class CrewSettings : ConfigNodePersistenceBase, IConfigNode
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
        public double trainingMissionExpirationDays = 120, trainingMissionStupidMin = 0.5, trainingMissionStupidMax = 1.5;

        [Persistent]
        public double minFlightDurationSecondsForTrainingExpire = 30;

        [Persistent]
        public double retireIncreaseMultiplierToTrainingLengthProficiency = 0.125d;

        [Persistent]
        public double retireIncreaseMultiplierToTrainingLengthMission = 0.25d;

        [Persistent]
        public double flightHighAltitude = 40000;

        [Persistent]
        public PersistentDictionaryValueTypes<string, int> ACLevelsForTraining = new PersistentDictionaryValueTypes<string, int>();

        [Persistent]
        public PersistentListValueType<double> ACTrainingRates = new PersistentListValueType<double>();

        [Persistent]
        public PersistentListValueType<double> ACRnRMults = new PersistentListValueType<double>();

        [Persistent]
        public PersistentDictionaryValueTypes<string, double> SituationValues = new PersistentDictionaryValueTypes<string, double>();
    }
}
