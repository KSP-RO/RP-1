using System;
using UnityEngine;

namespace RP0
{
    public class PadConstructionProject : ConstructionProject
    {
        [Persistent]
        public Guid id;

        private LaunchComplex _lc = null;

        public LaunchComplex LC
        {
            get
            {
                if (_lc == null)
                {
                    foreach (var ksc in SpaceCenterManagement.Instance.KSCs)
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

        public PadConstructionProject()
        {
        }

        public PadConstructionProject(string name)
        {
            base.name = name;
        }

        public override string GetItemName() => $"{LC.Name}: {name}";

        protected override void ProcessCancel()
        {
            LCLaunchPad lp = LC.LaunchPads.Find(p => p.id == id);
            int index = LC.LaunchPads.IndexOf(lp);
            LC.LaunchPads.RemoveAt(index);
            if (LC.ActiveLaunchPadIndex >= index)
                LC.ActiveLaunchPadIndex = Math.Max(0, LC.ActiveLaunchPadIndex - 1); // should not change active pad.

            LC.PadConstructions.Remove(this);

            try
            {
                SCMEvents.OnPadConstructionCancel?.Fire(this, lp);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            LC.KSC.RecalculateBuildRates(false);
        }

        protected override void ProcessComplete()
        {

            if (ScenarioUpgradeableFacilities.Instance != null && !SpaceCenterManagement.Instance.ErroredDuringOnLoad)
            {
                LCLaunchPad lp = LC.LaunchPads.Find(p => p.id == id);
                lp.isOperational = true;
                lp.DestructionNode = new ConfigNode("DestructionState");
                upgradeProcessed = true;

                try
                {
                    SCMEvents.OnPadConstructionComplete?.Fire(this, lp);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
