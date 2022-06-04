using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class LCConstruction : ConstructionBuildItem
    {
        /// <summary>
        /// Index of the LC in KSCItem.LaunchComplexes.
        /// </summary>
        public int LaunchComplexIndex = 0;
        public bool IsModify;
        public Guid ModID;

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
                LCItem lc = KSC.LaunchComplexes[LaunchComplexIndex];
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
            LCItem lc = KSC.LaunchComplexes[LaunchComplexIndex];
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

                // Also fix LCCs
                foreach (var lcc in KSC.LCConstructions)
                    if (lcc != this && lcc.LaunchComplexIndex >= index)
                        --lcc.LaunchComplexIndex;
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
