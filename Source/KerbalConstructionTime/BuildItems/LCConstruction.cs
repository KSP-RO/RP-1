using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class LCConstruction : ConstructionBuildItem
    {
        [Persistent]
        public bool isModify = false;

        [Persistent]
        public Guid modId;

        [Persistent]
        public Guid lcID;

        [Persistent]
        public LCItem.LCData lcData = new LCItem.LCData();


        public LCConstruction()
        {
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);

            if (KCTGameStates.LoadedSaveVersion < KCTGameStates.VERSION)
            {
                if (KCTGameStates.LoadedSaveVersion < 8)
                {
                    if (modId == Guid.Empty)
                    {
                        modId = Guid.NewGuid();
                    }
                    else
                    {
                        modId = lcID;
                    }
                }
                if (KCTGameStates.LoadedSaveVersion < 12)
                {
                    lcData.Name = KCTGameStates.FindLCFromID(lcID)?.Name ?? lcData.Name;
                    if (string.IsNullOrEmpty(name))
                        name = lcData.Name;
                }
            }
        }

        protected override void ProcessComplete()
        {
            if (!KCTGameStates.ErroredDuringOnLoad)
            {
                LCItem lc = KSC.LaunchComplexes.Find(l => l.ID == lcID);
                lc.IsOperational = true;
                upgradeProcessed = true;
                if (isModify)
                    lc.Modify(lcData, modId);

                try
                {
                    KCTEvents.OnLCConstructionComplete?.Fire(this, lc);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        protected override void ProcessCancel()
        {
            int index = -1;
            LCItem lc = null;
            for (int i = 0; i < KSC.LaunchComplexes.Count; ++i)
            {
                lc = KSC.LaunchComplexes[i];
                if (lc.ID == lcID)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                Debug.LogError($"[RP-0]: Error! Can't find LC from LCC, LC ID {lcID}");
                return;
            }
            if (isModify)
            {
                lc.IsOperational = true;
                lc.RecalculateBuildRates();
                lc.Stats.GetCostStats(out double padCost, out _, out _);
                padCost *= PresetManager.Instance.ActivePreset.GeneralSettings.AdditionalPadCostMult;
                foreach (var pc in lc.PadConstructions)
                {
                    pc.SetBP(padCost);
                    pc.cost = padCost;
                }
            }
            else
            {
                KSC.LaunchComplexes[index].OnRemove();
                KSC.LaunchComplexes.RemoveAt(index);
                if (KSC.ActiveLaunchComplexIndex >= index)
                    --KSC.ActiveLaunchComplexIndex; // should not change active LC.
            }

            KSC.LCConstructions.Remove(this);

            try
            {
                KCTEvents.OnLCConstructionCancel?.Fire(this, lc);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            KSC.RecalculateBuildRates(false);
        }
    }
}
