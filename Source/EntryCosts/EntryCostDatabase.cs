using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RP0
{
    public class EntryCostDatabase
    {
        #region Fields
        protected static Dictionary<string, PartEntryCostHolder> holders = null;
        protected static Dictionary<string, AvailablePart> nameToPart = null;
        protected static HashSet<string> unlocks = null;
        #endregion

        #region Setup
        public EntryCostDatabase()
        {
            Initialize();
        }
        public static void Initialize()
        {
            if (nameToPart == null)
                FillPartList();

            if (holders == null)
                FillHolders();

            if (unlocks == null)
                unlocks = new HashSet<string>();
        }
        protected static void FillPartList()
        {
            nameToPart = new Dictionary<string, AvailablePart>();

            // now fill our dictionary of parts
            if (PartLoader.Instance == null || PartLoader.LoadedPartsList == null)
            {
                Debug.LogError("*RP-0 EC: ERROR: Partloader instance null or list null!");
                return;
            }
            for (int a = PartLoader.LoadedPartsList.Count; a-- > 0;)
            {
                AvailablePart ap = PartLoader.LoadedPartsList[a];
                if (ap == null)
                {
                    continue;
                }
                Part part = ap.partPrefab;
                if (part != null)
                {
                    string name = GetPartName(ap);
                    nameToPart[name] = ap;
                }
            }
        }

        protected static void FillHolders()
        {
            holders = new Dictionary<string, PartEntryCostHolder>();

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("ENTRYCOSTMODS"))
            {
                foreach (ConfigNode.Value v in node.values)
                {
                    PartEntryCostHolder p = new PartEntryCostHolder(v.name, v.value);
                    holders[p.name] = p;
                }
            }
        }
        #endregion

        #region Helpers
        // from RF
        public static string GetPartName(Part part)
        {
            if (part.partInfo != null)
                return GetPartName(part.partInfo);
            return GetPartName(part.name);
        }

        public static string GetPartName(AvailablePart ap)
        {
            return GetPartName(ap.name);
        }

        public static string GetPartName(string partName)
        {
            partName = partName.Replace(".", "-");
            return partName.Replace("_", "-");
        }
        #endregion

        #region Interface
        public static bool IsUnlocked(string name)
        {
            return unlocks.Contains(name) || RealFuels.RFUpgradeManager.Instance.ConfigUnlocked(name);
        }

        public static void SetUnlocked(string name)
        {
            // RF's current unlock system doesn't allow checking unlock status.
            // HOWEVER if a name is not in the database, ConfigUnlocked returns true.
            // That means we can know a config is present and locked by that
            // returning false. So we take advantage.
            if (!RealFuels.RFUpgradeManager.Instance.ConfigUnlocked(name))
                RealFuels.RFUpgradeManager.Instance.SetConfigUnlock(name, true);

            unlocks.Add(name); // add regardless
        }

        public static int GetCost(string name)
        {
            PartEntryCostHolder h;
            if (holders.TryGetValue(name, out h))
                return h.GetCost();

            return 0;
        }

        public static void UpdateEntryCost(AvailablePart ap)
        {
            string name = GetPartName(ap);
            PartEntryCostHolder h;
            if (holders.TryGetValue(name, out h))
                ap.SetEntryCost(h.GetCost());
        }

        public static void Save(ConfigNode node)
        {
            foreach (string s in unlocks)
            {
                node.AddValue(s, true);
            }
        }

        public static void Load(ConfigNode node)
        {
            unlocks.Clear();

            if (node == null)
                return;

            foreach (ConfigNode.Value v in node.values)
            {
                unlocks.Add(v.name);
            }
        }
        #endregion
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class EntryCostInitializer
    {
        public void Start()
        {
            EntryCostDatabase.Initialize();
        }
    }
}
