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
        public static Dictionary<string, PartEntryCostHolder> partlist = null;
        #region Instance
        private static EntryCostDatabase _instance = null;
        public static EntryCostDatabase Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new EntryCostDatabase();

                return _instance;
            }
        }
        #endregion
        #endregion

        #region Setup
        public EntryCostDatabase()
        {
            Initialize();
        }
        public void Initialize()
        {
            if (partlist == null)
            {
                partlist = new Dictionary<string, PartEntryCostHolder>();
                FillPartList();
            }
        }
        public void FillPartList()
        {
            // precalc our node dictionary
            Dictionary<string, ConfigNode> partnodes = new Dictionary<string, ConfigNode>();
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("ENTRYCOSTMODS"))
                foreach (ConfigNode n in node.GetNodes("PART"))
                    if (n.HasValue("name"))
                        partnodes[n.GetValue("name")] = n;

            // now fill our dictionary of parts
            if (PartLoader.Instance == null || PartLoader.LoadedPartsList == null)
            {
                Debug.LogError("*RP-0 EC: ERROR: Partloader instance null or list null!");
                return;
            }
            for (int a = PartLoader.LoadedPartsList.Count - 1; a >= 0; --a)
            {
                AvailablePart ap = PartLoader.LoadedPartsList[a];
                if (ap == null)
                {
                    continue;
                }
                Part part = ap.partPrefab;
                if (part != null)
                {
                    ConfigNode partnode = null;
                    string name = GetPartName(ap);
                    if (partnodes.TryGetValue(name, out partnode))
                        partlist[name] = new PartEntryCostHolder(partnode, ap);
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
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class EntryCostInitializer
    {
        public void Start()
        {
            EntryCostDatabase.Instance.Initialize();
        }
    }
}
