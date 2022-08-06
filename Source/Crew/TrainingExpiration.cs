using System.Collections.Generic;

namespace RP0.Crew
{
    public class TrainingExpiration : IConfigNode
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

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);

            if (CrewHandler.Instance.saveVersion < 2)
            {
                string entry = node.GetValue("entry");
                if (entry != null)
                {
                    var split = entry.Split(',');
                    training = new TrainingFlightEntry(split[0], split[1]);
                }
            }
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
