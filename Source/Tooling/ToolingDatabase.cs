using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    public struct ToolingDiameter
    {
        public double diameter;

        public List<double> lengths;

        public ToolingDiameter(double d)
        {
            diameter = d;
            lengths = new List<double>();
        }

        public ToolingDiameter(double d, double l)
        {
            diameter = d;
            lengths = new List<double>();
            lengths.Add(l);
        }
    }
    public class ToolingDatabase
    {
        protected static double comparisonEpsilonHigh = 1.04d;
        protected static double comparisonEpsilonLow = 0.96d;

        protected static int EpsilonCompare(double a, double b)
        {
            if (a > b * comparisonEpsilonLow && a < b * comparisonEpsilonHigh)
                return 0;

            return a.CompareTo(b);
        }

        protected static Dictionary<string, List<ToolingDiameter>> toolings = new Dictionary<string, List<ToolingDiameter>>();

        protected static int DiamIndex(double diam, List<ToolingDiameter> lst, out int min)
        {
            min = 0;
            int max = lst.Count - 1;
            do
            {
                int mid = (min + max) / 2;
                switch (EpsilonCompare(diam, lst[mid].diameter))
                {
                    case 0:
                        return mid;

                    case 1:
                        min = mid + 1;
                        break;

                    default:
                    case -1:
                        max = mid - 1;
                        break;
                }
            }
            while (min <= max);
            return -1;
        }

        protected static int LengthIndex(double length, List<double> lst, out int min)
        {
            min = 0;
            int max = lst.Count - 1;
            do
            {
                int mid = (min + max) / 2;
                switch (EpsilonCompare(length, lst[mid]))
                {
                    case 0:
                        return mid;

                    case 1:
                        min = mid + 1;
                        break;

                    default:
                    case -1:
                        max = mid - 1;
                        break;
                }
            }
            while (min <= max);
            return -1;
        }

        public enum ToolingLevel
        {
            None = 0,
            Diameter = 1,
            Full = 2
        };

        public static ToolingLevel HasTooling(string type, double diam, double len)
        {
            List<ToolingDiameter> lst;
            if (toolings.TryGetValue(type, out lst))
            {
                int tmp;
                int dIndex = DiamIndex(diam, lst, out tmp);
                if (dIndex == -1)
                    return ToolingLevel.None;

                int lIndex = LengthIndex(len, lst[dIndex].lengths, out tmp);

                return lIndex == -1 ? ToolingLevel.Diameter : ToolingLevel.Full;
            }

            return ToolingLevel.None;
        }

        public static bool UnlockTooling(string type, double diam, double len)
        {
            List<ToolingDiameter> lst;
            if (toolings.TryGetValue(type, out lst))
            {
                int insertionIdx;
                int dIndex = DiamIndex(diam, lst, out insertionIdx);
                if (dIndex == -1)
                {
                    ToolingDiameter d = new ToolingDiameter(diam, len);
                    lst.Insert(insertionIdx, d);
                    return true;
                }

                int lIndex = LengthIndex(len, lst[dIndex].lengths, out insertionIdx);

                if (lIndex != -1)
                {
                    return false;
                }
                else
                {
                    ToolingDiameter d = lst[dIndex];
                    d.lengths.Insert(insertionIdx, len);
                    lst[dIndex] = d;
                    return true;
                }
            }
            else
                return false;
        }


        public static void Load(ConfigNode node)
        {
            toolings.Clear();
            foreach (ConfigNode c in node.GetNodes("TYPE"))
            {
                string type = c.GetValue("type");
                if (string.IsNullOrEmpty(type))
                    continue;

                List<ToolingDiameter> lst = new List<ToolingDiameter>();

                foreach (ConfigNode n in c.GetNodes("DIAMETER"))
                {
                    double tmp = 0d;
                    if (!n.TryGetValue("diameter", ref tmp))
                        continue;

                    ToolingDiameter d = new ToolingDiameter(tmp);

                    ConfigNode len = n.GetNode("LENGTHS");
                    if (len != null)
                        foreach (ConfigNode.Value v in len.values)
                            if (double.TryParse(v.value, out tmp))
                                d.lengths.Add(tmp);

                    lst.Add(d);
                }

                toolings[type] = lst;
            }
        }

        public static void Save(ConfigNode node)
        {
            foreach (KeyValuePair<string, List<ToolingDiameter>> kvp in toolings)
            {
                ConfigNode c = node.AddNode("TYPE");
                c.AddValue("type", kvp.Key);

                foreach (ToolingDiameter d in kvp.Value)
                {
                    ConfigNode n = c.AddNode("DIAMETER");
                    n.AddValue("diameter", d.diameter.ToString("G17"));

                    ConfigNode len = n.AddNode("LENGTHS");
                    for (int i = 0, iC = d.lengths.Count; i < iC; ++i)
                        len.AddValue("length", d.lengths[i].ToString("G17"));
                }
            }
        }
    }
}
