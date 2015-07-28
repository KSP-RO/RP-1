using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.SPACECENTER })]
    public class EntryCostModifier : ScenarioModule
    {
        #region Fields
        protected Dictionary<string, PartEntryCostHolder> partlist;
        #region Instance
        private static EntryCostModifier _instance = null;
        public static EntryCostModifier Instance
        {
            get
            {
                return _instance;
            }
        }
        #endregion
        #endregion

        #region Overrides and Monobehaviour methods
        public override void OnAwake()
        {
            base.OnAwake();

            if (_instance != null)
            {
                Object.Destroy(this);
                return;
            }
            _instance = this;

            partlist = new Dictionary<string, PartEntryCostHolder>();
            FillPartList();
            GameEvents.OnPartPurchased.Add(new EventData<AvailablePart>.OnEvent(onPartPurchased));
        }
        public void OnDestroy()
        {
            GameEvents.OnPartPurchased.Remove(new EventData<AvailablePart>.OnEvent(onPartPurchased));
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            UpdatePartEntryCosts();
        }
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }
        #endregion

        #region Methods
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
        public void onPartPurchased(AvailablePart ap)
        {
            UpdatePartEntryCosts();
        }
        public void UpdatePartEntryCosts()
        {
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
                    string name = GetPartName(ap);
                    PartEntryCostHolder ec;
                    if (partlist.TryGetValue(name, out ec))
                        ec.UpdateCost();
                }
            }
        }
        public double PartEntryCost(string partName)
        {
            PartEntryCostHolder ec = null;
            if (partlist.TryGetValue(partName, out ec))
                return ec.EntryCost();

            Debug.LogError("*RP-0 EC: ERROR: entry cost modifier for " + partName + " does not exist!");
            return 0d;
        }

        public bool IsUnlocked(string partname)
        {
            PartEntryCostHolder partEC = null;
            if (ResearchAndDevelopment.Instance != null)
                if (partlist.TryGetValue(partname, out partEC))
                    return ResearchAndDevelopment.PartModelPurchased(partEC.ap);

            return false;
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
}
