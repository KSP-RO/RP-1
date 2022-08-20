using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class PadConstruction : ConstructionBuildItem
    {
        [Persistent]
        public Guid id;

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
            base.name = name;
        }

        public override string GetItemName() => $"{LC.Name}: {name}";

        protected override void ProcessCancel()
        {
            KCT_LaunchPad lp = LC.LaunchPads.Find(p => p.id == id);
            int index = LC.LaunchPads.IndexOf(lp);
            LC.LaunchPads.RemoveAt(index);
            if (LC.ActiveLaunchPadIndex >= index)
                LC.ActiveLaunchPadIndex = Math.Max(0, LC.ActiveLaunchPadIndex - 1); // should not change active pad.

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
                KCT_LaunchPad lp = LC.LaunchPads.Find(p => p.id == id);
                lp.isOperational = true;
                lp.DestructionNode = new ConfigNode("DestructionState");
                upgradeProcessed = true;

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
