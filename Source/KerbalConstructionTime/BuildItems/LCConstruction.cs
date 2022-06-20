using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class LCConstruction : ConstructionBuildItem
    {
        public bool IsModify;
        public Guid ModID;
        public Guid LCID;

        public LCItem.LCData LCData;

        

        public LCConstruction()
        {
        }

        protected override void ProcessComplete()
        {
            if (!KCTGameStates.ErroredDuringOnLoad)
            {
                LCItem lc = KSC.LaunchComplexes.Find(l => l.ID == LCID);
                lc.IsOperational = true;
                UpgradeProcessed = true;
                if (IsModify)
                    lc.Modify(LCData, ModID);

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
                if (lc.ID == LCID)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                Debug.LogError($"[RP-0]: Error! Can't find LC from LCC, LC ID {LCID}");
                return;
            }
            if (IsModify)
            {
                lc.IsOperational = true;
            }
            else
            {
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
