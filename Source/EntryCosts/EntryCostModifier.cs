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
            
            EntryCostDatabase.Initialize(); // should not be needed though.

            GameEvents.OnPartPurchased.Add(new EventData<AvailablePart>.OnEvent(onPartPurchased));
        }
        public void OnDestroy()
        {
            GameEvents.OnPartPurchased.Remove(new EventData<AvailablePart>.OnEvent(onPartPurchased));
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            EntryCostDatabase.Load(node.GetNode("Unlocks"));
            UpdatePartEntryCosts();
        }
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            EntryCostDatabase.Save(node.AddNode("Unlocks"));
            
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
                    EntryCostDatabase.UpdateEntryCost(ap);
                }
            }
        }
        #endregion
    }
}
