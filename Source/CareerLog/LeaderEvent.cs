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

        /// <summary>
        /// Used only for deserialization.
        /// </summary>
        public LeaderEvent()
        {
        }

        public LeaderEvent(double UT) : base(UT)
        {
        }

        public LeaderEvent(ConfigNode n) : base(n)
        {
        }
    }
}
