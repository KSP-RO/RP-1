using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    public class ToolingDiameter
    {
        public float diameter;

        public List<float> lengths;

        public ToolingDiameter(float d)
        {
            diameter = d;
            lengths = new List<float>();
        }

        public ToolingDiameter(float d, float l)
        {
            diameter = d;
            lengths = new List<float>();
            lengths.Add(l);
        }
    }
    public class ToolingDatabase
    {
        protected static float comparisonEpsilonHigh = 1.04f;
        protected static float comparisonEpsilonLow = 0.96f;

        protected static int EpsilonCompare(float a, float b)
        {
            if (a > b * comparisonEpsilonLow && a < b * comparisonEpsilonHigh)
                return 0;

            return a.CompareTo(b);
        }

        public static Dictionary<string, List<ToolingDiameter>> toolings = new Dictionary<string, List<ToolingDiameter>>();

        protected static int DiamIndex(float diam, List<ToolingDiameter> lst, out int min)
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

        protected static int LengthIndex(float length, List<float> lst, out int min)
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

        /// <summary>
        /// Compares two tooling sizes and checks whether their diameter and length are within a predefined epsilon (currently 4%).
        /// </summary>
        /// <param name="diam1">Diameter of tooling 1</param>
        /// <param name="len1">Length of tooling 1</param>
        /// <param name="diam2">Diameter of tooling 2</param>
        /// <param name="len2">Length of tooling 2</param>
        /// <returns>True if the two tooling sizes are considered the same.</returns>
        public static bool IsSameSize(float diam1, float len1, float diam2, float len2)
        {
            return EpsilonCompare(diam1, diam2) == 0 && EpsilonCompare(len1, len2) == 0;
        }

        public static ToolingLevel HasTooling(string type, float diam, float len)
        {
            List<ToolingDiameter> lst;
            if (toolings.TryGetValue(type, out lst))
            {
                if (lst.Count == 0)
                    return ToolingLevel.None;

                int tmp;
                int dIndex = DiamIndex(diam, lst, out tmp);
                if (dIndex == -1)
                    return ToolingLevel.None;

                int lIndex = LengthIndex(len, lst[dIndex].lengths, out tmp);

                if (lIndex == -1)
                    return ToolingLevel.Diameter;
                else
                    return ToolingLevel.Full;
            }

            return ToolingLevel.None;
        }

        public static bool UnlockTooling(string type, float diam, float len)
        {
            List<ToolingDiameter> lst;
            if (!toolings.TryGetValue(type, out lst))
            {
                lst = new List<ToolingDiameter>();
                toolings[type] = lst;
            }
            if (lst.Count == 0)
            {
                lst.Add(new ToolingDiameter(diam, len));
                return true;
            }
            else
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
                    lst[dIndex].lengths.Insert(insertionIdx, len);
                    return true;
                }
            }
        }


        public static void Load(ConfigNode node)
        {
            toolings.Clear();

            if (node == null)
                return;

            foreach (ConfigNode c in node.GetNodes("TYPE"))
            {
                string type = c.GetValue("type");
                if (string.IsNullOrEmpty(type))
                    continue;

                List<ToolingDiameter> lst = new List<ToolingDiameter>();

                foreach (ConfigNode n in c.GetNodes("DIAMETER"))
                {
                    float tmp = 0f;
                    if (!n.TryGetValue("diameter", ref tmp))
                        continue;

                    ToolingDiameter d = new ToolingDiameter(tmp);

                    ConfigNode len = n.GetNode("LENGTHS");
                    if (len != null)
                        foreach (ConfigNode.Value v in len.values)
                            if (float.TryParse(v.value, out tmp))
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
