using System;
using System.Collections.Generic;
using UniLinq;
using System.Text;
using KerbalConstructionTime;
using RP0.DataTypes;

namespace RP0.Crew
{
    public class TrainingFlightEntry
    {
        [Persistent]
        public string type = string.Empty;

        [Persistent]
        public string target = string.Empty;

        public TrainingFlightEntry() { }

        public TrainingFlightEntry(string typ, string tgt)
        {
            type = typ;
            target = tgt;
        }
    }
}
