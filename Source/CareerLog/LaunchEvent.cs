namespace RP0
{
    public class LaunchEvent : CareerEvent
    {
        [Persistent]
        public string VesselName;

        [Persistent]
        public string VesselUID;

        [Persistent]
        public string LaunchID;

        [Persistent]
        public EditorFacility BuiltAt;

        [Persistent]
        public string LCID;

        [Persistent]
        public string LCModID;

        /// <summary>
        /// Used only for deserialization.
        /// </summary>
        public LaunchEvent()
        {
        }

        public LaunchEvent(double UT) : base(UT)
        {
        }

        public LaunchEvent(ConfigNode n) : base(n)
        {
        }
    }
}
