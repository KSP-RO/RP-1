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
        #region Overrides and Monobehaviour methods
        public override void OnAwake()
        {
            base.OnAwake();
            
            EntryCostDatabase.Instance.Initialize(); // should not be needed though.

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
                    string name = EntryCostDatabase.GetPartName(ap);
                    PartEntryCostHolder ec;
                    if (EntryCostDatabase.partlist.TryGetValue(name, out ec))
                        ec.UpdateCost();
                }
            }
        }
        public static double PartEntryCost(string partName)
        {
            PartEntryCostHolder ec = null;
            if (EntryCostDatabase.partlist.TryGetValue(partName, out ec))
                return ec.EntryCost();

            Debug.LogError("*RP-0 EC: ERROR: entry cost modifier for " + partName + " does not exist!");
            return 0d;
        }

        public static bool IsUnlocked(string partname)
        {
            PartEntryCostHolder partEC = null;
            if (ResearchAndDevelopment.Instance != null)
                if (EntryCostDatabase.partlist.TryGetValue(partname, out partEC))
                    return ResearchAndDevelopment.PartModelPurchased(partEC.ap);

            return false;
        }
        #endregion
    }
}
