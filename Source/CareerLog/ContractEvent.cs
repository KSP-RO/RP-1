namespace RP0
{
    public class ContractEvent : CareerEvent
    {
        [Persistent]
        public string InternalName;

        [Persistent]
        public string DisplayName;

        [Persistent]
        public double RepChange;

        [Persistent]
        public ContractEventType Type;

        /// <summary>
        /// Used only for deserialization.
        /// </summary>
        public ContractEvent()
        {
        }

        public ContractEvent(double UT) : base(UT)
        {
        }

        public ContractEvent(ConfigNode n) : base(n)
        {
        }
    }

    public enum ContractEventType
    {
        Accept, Complete, Fail, Cancel
    }
}
