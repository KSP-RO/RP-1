using System;
using System.Collections.Generic;
using UnityEngine;

namespace RP0.Crew
{
    public class TrainingDatabase
    {
        public class TrainingHolder
        {
            #region Fields

            public string name;

            public double days = 0;

            public List<string> children = new List<string>();

            #endregion

            #region Constructors

            public TrainingHolder(string name, string val)
            {
                this.name = name;

                double tmp;
                string[] split = val.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string s in split)
                {
                    if (double.TryParse(s, out tmp))
                        days += tmp;
                    else
                        children.Add(s);
                }
            }

            #endregion

            #region Methods

            public double GetTime()
            {
                double c = days;

                foreach (string s in children)
                    c += _GetTime(s);

                return c;
            }

            public bool HasName(string name)
            {
                if (this.name == name)
                    return true;
                foreach (string s in children)
                    if (holders[s].HasName(name))
                        return true;

                return false;
            }

            public void FillBools(List<string> items, List<bool> bools)
            {
                for (int i = items.Count; i-- > 0;)
                {
                    if (items[i] == name)
                    {
                        bools[i] = true;
                        break;
                    }
                }
                foreach (string s in children)
                    _FillBools(s, items, bools);
            }

            #endregion
        }

        #region Fields

        protected static bool isInitialized = false;
        protected static Dictionary<string, TrainingHolder> holders = null;
        protected static HashSet<string> unlockPathTracker = new HashSet<string>();

        #endregion

        #region Setup

        public TrainingDatabase()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
                isInitialized = true;
            }
        }

        public static void Initialize()
        {
            if (holders == null)
                holders = new Dictionary<string, TrainingHolder>();

            FillHolders();
        }

        protected static void FillHolders()
        {
            holders.Clear();

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("TRAININGTIMES"))
            {
                foreach (ConfigNode.Value v in node.values)
                {
                    TrainingHolder p = new TrainingHolder(v.name, v.value);
                    holders[p.name] = p;
                }
            }
        }
        #endregion

        #region Interface

        public static double GetTime(string name)
        {
            ClearTracker();
            return _GetTime(Sanitize(name));
        }
        
        public static double GetProficiencyTime(string name, ProtoCrewMember pcm)
        {
            ClearTracker();
            double mult = 1d;
            string expired = "expired_" + CrewHandler.TrainingType_Proficiency;
            string sanName = Sanitize(name);
            for (int i = pcm.careerLog.Entries.Count - 1; i >= 0; --i)
            {
                var entry = pcm.careerLog.Entries[i];
                if (entry.type == CrewHandler.TrainingType_Proficiency)
                {
                    if (holders.TryGetValue(entry.target, out var h))
                    {
                        FillTrackerFromHolder(h);
                    }
                    else
                    {
                        RP0Debug.LogError($"Couldn't find TrainingHolder for {entry.target}");
                    }
                }
                else if (entry.type == expired && entry.target == sanName)
                {
                    mult = 0.5d;
                }
            }

            return _GetTime(sanName) * mult;
        }

        protected static double _GetTime(string name)
        {
            if (unlockPathTracker.Contains(name))
            {
                /*string msg = "[TrainingDatabase]: Circular reference on " + name;
                foreach (string s in unlockPathTracker)
                    msg += "\n" + s;

                RP0Debug.LogError(msg);*/
                return 0;
            }

            unlockPathTracker.Add(name);

            if (holders.TryGetValue(name, out var h))
                return h.GetTime();

            return 0d;
        }

        public static int GetACRequirement(string name)
        {
            ClearTracker();
            string sanName = Sanitize(name);
            if (holders.TryGetValue(sanName, out var h))
                FillTrackerFromHolder(h);
            else
                unlockPathTracker.Add(sanName);

            int maxLevel = 0;
            foreach (var s in unlockPathTracker)
            {
                if (Database.SettingsCrew.ACLevelsForTraining.TryGetValue(s, out var lvl) && lvl > maxLevel)
                    maxLevel = lvl;
            }

            return maxLevel;
        }

        public static bool HasName(string training, string name)
        {
            // Don't have to guard against repeats because we're not summing,
            // just getting existence.
            if (holders.TryGetValue(training, out var h))
                return h.HasName(name);

            return false;
        }

        public static void FillBools(string name, List<string> items, List<bool> bools)
        {
            ClearTracker();
            string sanName = Sanitize(name);
            _FillBools(sanName, items, bools);
        }

        protected static void _FillBools(string name, List<string> items, List<bool> bools)
        {
            if (unlockPathTracker.Contains(name))
                return;
            unlockPathTracker.Add(name);

            if (holders.TryGetValue(name, out var h))
                h.FillBools(items, bools);
        }

        protected static void FillTrackerFromHolder(TrainingHolder h)
        {
            if (unlockPathTracker.Contains(h.name))
                return;

            unlockPathTracker.Add(h.name);
            
            foreach (var child in h.children)
                if (holders.TryGetValue(child, out var hc))
                    FillTrackerFromHolder(hc);
        }

        public static bool TrainingExists(string name, out string newName)
        {
            if (SynonymReplace(name, out newName))
                return true;

            return holders.ContainsKey(newName);
        }

        public static bool SynonymReplace(string name, out string result)
        {
            EnsureInitialized();

            result = Sanitize(name);
            if (holders.TryGetValue(result, out TrainingHolder h))
            {
                if (h.days == 0 && h.children.Count == 1)
                {
                    result = h.children[0];
                    return true;
                }
            }
            return false;
        }

        protected static string Sanitize(string partName)
        {
            partName = partName.Replace(".", "-");
            return partName.Replace("_", "-");
        }

        protected static void ClearTracker()
        {
            unlockPathTracker.Clear();
        }

        #endregion
    }
}
