using System;
using System.Collections.Generic;
using UniLinq;

namespace RP0
{
    public class LeaderEvent : CareerEvent
    {
        [Persistent]
        public string LeaderName;

        [Persistent]
        public double Cost;

        [Persistent]
        public bool IsAdd;

        public LeaderEvent(double UT) : base(UT)
        {
        }

        public LeaderEvent(ConfigNode n) : base(n)
        {
        }
    }
}
