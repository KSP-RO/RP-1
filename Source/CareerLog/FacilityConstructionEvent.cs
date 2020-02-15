using System;
using System.Collections.Generic;
using System.Linq;

namespace RP0
{
    public class FacilityConstructionEvent : CareerEvent
    {
        [Persistent]
        public SpaceCenterFacility Facility;

        [Persistent]
        public int NewLevel;

        [Persistent]
        public double Cost;

        [Persistent]
        public ConstructionState State;

        public FacilityConstructionEvent(double UT) : base(UT)
        {
        }

        public FacilityConstructionEvent(ConfigNode n) : base(n)
        {
        }
    }

    public enum ConstructionState
    {
        Started, Completed
    }
}
