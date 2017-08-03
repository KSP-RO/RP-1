using System;
using System.Collections.Generic;
using System.Text;

namespace RP0
{
    public class PartEntryCostHolder
    {
        #region Fields
        public string name;

        public int cost = 0;

        public List<string> children = new List<string>();
        #endregion

        #region Constructors
        public PartEntryCostHolder(string name, string val)
        {
            this.name = name;

            int tmp;
            string[] split = val.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in split)
            {
                if (int.TryParse(s, out tmp))
                    cost += tmp;
                else
                    children.Add(s);
            }
        }
        #endregion

        #region Methods
        public int GetCost()
        {
            if (EntryCostDatabase.IsUnlocked(name))
                return 0;

            int c = cost;

            foreach (string s in children)
                c += EntryCostDatabase.GetCost(s);

            return c;
        }
        #endregion
    }
}
