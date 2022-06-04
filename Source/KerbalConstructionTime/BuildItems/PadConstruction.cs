using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class PadConstruction : ConstructionBuildItem
    {
        /// <summary>
        /// Index of the pad in KSCItem.LaunchPads.
        /// </summary>
        public int LaunchpadIndex = 0;

        public Guid ID;

        private LCItem _lc = null;

        public LCItem LC
        {
            get
            {
                if (_lc == null)
                {
                    foreach (var ksc in KCTGameStates.KSCs)
                    {
                        foreach (var lc in ksc.LaunchComplexes)
                        {
                            if (lc.PadConstructions.Contains(this))
                            {
                                _lc = lc;
                                break;
                            }
                        }
                    }
                }
                return _lc;
            }
        }

        public PadConstruction()
        {
        }

        public PadConstruction(string name)
        {
            Name = name;
        }

        public override string GetItemName() => $"{LC.Name}: {Name}";

        protected override void ProcessCancel()
        {
            KCT_LaunchPad lp = LC.LaunchPads[LaunchpadIndex];
            int index = LC.LaunchPads.IndexOf(lp);
            LC.LaunchPads.RemoveAt(index);
            if (LC.ActiveLaunchPadIndex >= index)
                --LC.ActiveLaunchPadIndex; // should not change active pad.

            foreach (var pc in LC.PadConstructions)
                if (pc != this && pc.LaunchpadIndex >= index)
                    --pc.LaunchpadIndex;

            LC.PadConstructions.Remove(this);

            try
            {
                KCTEvents.OnPadConstructionCancel?.Fire(this, lp);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            LC.KSC.RecalculateBuildRates(false);
        }

        protected override void ProcessComplete()
        {

            if (ScenarioUpgradeableFacilities.Instance != null && !KCTGameStates.ErroredDuringOnLoad)
            {
                KCT_LaunchPad lp = LC.LaunchPads[LaunchpadIndex];
                lp.isOperational = true;
                lp.DestructionNode = new ConfigNode("DestructionState");
                UpgradeProcessed = true;

                try
                {
                    KCTEvents.OnPadConstructionComplete?.Fire(this, lp);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
