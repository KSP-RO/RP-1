using System;
using System.Collections.Generic;
using UniLinq;

namespace RP0
{
    public class TechResearchEvent : CareerEvent
    {
        [Persistent]
        public string NodeName;

        [Persistent]
        public double YearMult;

        [Persistent]
        public double ResearchRate;

        public TechResearchEvent(double UT) : base(UT)
        {
        }

        public TechResearchEvent(ConfigNode n) : base(n)
        {
        }
    }
}
