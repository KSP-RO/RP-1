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

        /// <summary>
        /// Used only for deserialization.
        /// </summary>
        public TechResearchEvent()
        {
        }

        public TechResearchEvent(double UT) : base(UT)
        {
        }

        public TechResearchEvent(ConfigNode n) : base(n)
        {
        }
    }
}
