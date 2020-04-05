using System;
using System.Collections.Generic;
using System.Linq;

namespace RP0
{
    public abstract class CareerEvent : IConfigNode
    {
        [Persistent]
        public double UT;

        public CareerEvent(double ut)
        {
            UT = ut;
        }

        public CareerEvent(ConfigNode n)
        {
            Load(n);
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
