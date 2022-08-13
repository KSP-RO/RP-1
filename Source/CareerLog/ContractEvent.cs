using System;
using System.Collections.Generic;
using UniLinq;

namespace RP0
{
    public class ContractEvent : CareerEvent
    {
        [Persistent]
        public string InternalName;

        [Persistent]
        public string DisplayName;

        [Persistent]
        public double FundsChange;

        [Persistent]
        public double RepChange;

        [Persistent]
        public ContractEventType Type;

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
