using System.Collections.Generic;

namespace RP0.Crew
{
    public class TrainingExpiration : IConfigNode
    {
        public string PcmName;
        public List<string> Entries = new List<string>();
        public double Expiration;

        public TrainingExpiration() { }

        public TrainingExpiration(ConfigNode node)
        {
            Load(node);
        }

        public static bool Compare(string str, FlightLog.Entry e)
        {
            int tyLen = string.IsNullOrEmpty(e.type) ? 0 : e.type.Length;
            int tgLen = string.IsNullOrEmpty(e.target) ? 0 : e.target.Length;
            int iC = str.Length;
            if (iC != 1 + tyLen + tgLen)
                return false;
            int i = 0;
            for (; i < tyLen; ++i)
            {
                if (str[i] != e.type[i])
                    return false;
            }

            if (str[i] != ',')
                return false;
            ++i;
            for (int j = 0; j < tgLen && i < iC; ++j)
            {
                if (str[i] != e.target[j])
                    return false;
                ++i;
            }

            return true;
        }

        public bool Compare(int idx, FlightLog.Entry e)
        {
            return Compare(Entries[idx], e);
        }

        public void Load(ConfigNode node)
        {
            foreach (ConfigNode.Value v in node.values)
            {
                switch (v.name)
                {
                    case "pcmName":
                        PcmName = v.value;
                        break;
                    case "expiration":
                        double.TryParse(v.value, out Expiration);
                        break;

                    default:
                    case "entry":
                        Entries.Add(v.value);
                        break;
                }
            }
        }

        public void Save(ConfigNode node)
        {
            node.AddValue("pcmName", PcmName);
            node.AddValue("expiration", Expiration);
            foreach (string s in Entries)
                node.AddValue("entry", s);
        }
    }
}
