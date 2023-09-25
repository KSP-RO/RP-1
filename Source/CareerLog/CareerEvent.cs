﻿namespace RP0
{
    public abstract class CareerEvent : IConfigNode
    {
        [Persistent]
        public double UT;

        /// <summary>
        /// Used only for deserialization.
        /// </summary>
        public CareerEvent()
        {
        }

        public CareerEvent(double ut)
        {
            UT = ut;
        }

        public CareerEvent(ConfigNode n)
        {
            Load(n);
        }

        public virtual void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public virtual void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }

        public bool IsInPeriod(LogPeriod p)
        {
            return UT >= p.StartUT && UT < p.EndUT;
        }
    }
}
