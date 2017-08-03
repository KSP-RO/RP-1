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

        public List<string> children;
        #endregion

        #region Constructors
        public PartEntryCostHolder(string name, string val)
        {
            this.name = name;

            int tmp;
            if (int.TryParse(val, out tmp))
                cost = tmp;
            else
                children = new List<string>(val.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }
        #endregion

        #region Methods
        public int GetCost()
        {
            if (EntryCostDatabase.IsUnlocked(name))
                return 0;

            int c = cost;

            if (children != null)
                foreach (string s in children)
                    c += EntryCostDatabase.GetCost(name);

            return c;
        }
        #endregion
    }
}
