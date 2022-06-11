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

        public LCConstruction(string name)
        {
            Name = name;
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
            LCItem lc = KSC.LaunchComplexes.Find(l => l.ID == LCID);
            if (IsModify)
            {
                lc.IsOperational = true;
            }
            else
            {
                int index = KSC.LaunchComplexes.IndexOf(lc);
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
