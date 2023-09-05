using System.Collections.Generic;
using RP0.DataTypes;

namespace RP0.Crew
{
    public class TrainingExpiration : ConfigNodePersistenceBase, IConfigNode
    {
        [Persistent]
        public string pcmName;

        [Persistent]
        public TrainingFlightEntry training = new TrainingFlightEntry();

        [Persistent]
        public double expiration;

        public TrainingExpiration() { }

        public TrainingExpiration(string pcmName, double expireTime, TrainingFlightEntry entry)
        {
            this.pcmName = pcmName;
            expiration = expireTime;
            training = entry;
        }

        public TrainingExpiration(ConfigNode node)
        {
            Load(node);
        }

        public bool Compare(FlightLog.Entry e)
        {
            return e.type == training.type && e.target == training.target;
        }

        public bool Compare(string pcmName, FlightLog.Entry e)
        {
            return this.pcmName == pcmName && Compare(e);
        }
    }
}
