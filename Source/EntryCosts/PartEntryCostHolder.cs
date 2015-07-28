using System;
using System.Collections.Generic;
using System.Text;

namespace RP0
{
    public class PartEntryCostHolder
    {
        #region Fields
        public string name;

        public double entryCost = 0d;

        public AvailablePart ap;

        public Dictionary<string, double> entryCostMultipliers = new Dictionary<string, double>();
        public Dictionary<string, double> entryCostSubtractors = new Dictionary<string, double>();
        #endregion

        #region Constructors
        public PartEntryCostHolder(ConfigNode node, AvailablePart part, string Name = "")
        {
            Load(node);
            if(Name != "")
                name = Name;
            entryCost = part.entryCost;
            ap = part;
        }
        #endregion

        #region Methods
        public void LoadMultipliers(ConfigNode node)
        {
            if (node == null)
                return;

            double dtmp;
            foreach (ConfigNode.Value v in node.values)
            {
                if (double.TryParse(v.value, out dtmp))
                    entryCostMultipliers[v.name] = dtmp;
            }
        }
        public void LoadSubtractors(ConfigNode node)
        {
            if (node == null)
                return;

            double dtmp;
            foreach (ConfigNode.Value v in node.values)
            {
                if (double.TryParse(v.value, out dtmp))
                    entryCostSubtractors[v.name] = dtmp;
            }
        }

        #region ConfigNode methods
        public void Load(ConfigNode node)
        {
            double dtmp;

            if (node.HasValue("name"))
                name = node.GetValue("name");

            if (node.HasValue("entryCost"))
            {
                if (double.TryParse(node.GetValue("entryCost"), out dtmp))
                    entryCost = dtmp;
            }

            if (node.HasNode("entryCostMultipliers"))
                LoadMultipliers(node.GetNode("entryCostMultipliers"));

            if (node.HasNode("entryCostSubtractors"))
                LoadSubtractors(node.GetNode("entryCostSubtractors"));
        }
        #endregion

        protected double ModCost(double cost, double subtractMultipler = 1.0d)
        {
            foreach (KeyValuePair<string, double> kvp in entryCostMultipliers)
            {
                if(EntryCostModifier.Instance.IsUnlocked(kvp.Key))
                    cost *= kvp.Value;
            }

            foreach (KeyValuePair<string, double> kvp in entryCostSubtractors)
            {
                if (EntryCostModifier.Instance.IsUnlocked(kvp.Key))
                    cost -= kvp.Value * subtractMultipler;
            }
            if (cost > 0d)
                return cost;

            return 0d;
        }
        public double EntryCost()
        {
            return ModCost(entryCost);
        }
        public void UpdateCost()
        {
            if (ap != null)
                ap.entryCost = (int)EntryCost();
        }
        #endregion
    }
}
