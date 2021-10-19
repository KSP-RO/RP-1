using System;
using System.Collections.Generic;
using System.Linq;

namespace RP0
{
    public class TechResearchEvent : CareerEvent
    {
        [Persistent]
        public string NodeName;

        public TechResearchEvent(double UT) : base(UT)
        {
        }

        public TechResearchEvent(ConfigNode n) : base(n)
        {
        }
    }
}
