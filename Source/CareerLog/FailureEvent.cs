using System;
using System.Collections.Generic;
using UniLinq;

namespace RP0
{
    public class FailureEvent : CareerEvent
    {
        [Persistent]
        public string VesselUID;

        [Persistent]
        public string LaunchID;

        [Persistent]
        public string Part;

        [Persistent]
        public string Type;

        public FailureEvent(double UT) : base(UT)
        {
        }

        public FailureEvent(ConfigNode n) : base(n)
        {
        }
    }
}
