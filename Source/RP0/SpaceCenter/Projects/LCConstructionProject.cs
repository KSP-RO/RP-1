using System;
using UnityEngine;

namespace RP0
{
    public class LCConstructionProject : ConstructionProject
    {
        [Persistent]
        public bool isModify = false;

        [Persistent]
        public Guid modId;

        [Persistent]
        public Guid lcID;

        [Persistent]
        public LCData lcData = new LCData();

        [Persistent]
        public int engineersToReadd = 0;


        public LCConstructionProject()
        {
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
        }

        protected override void ProcessComplete()
        {
            if (!KerbalConstructionTimeData.Instance.ErroredDuringOnLoad)
            {
                LaunchComplex lc = KSC.LaunchComplexes.Find(l => l.ID == lcID);
                lc.IsOperational = true;
                upgradeProcessed = true;
                if (isModify)
                    lc.Modify(lcData, modId);

                ReassignEngineers(lc);

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
            LaunchComplex lc = null;
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
                RP0Debug.LogError($"Can't find LC from LCC, LC ID {lcID}");
                return;
            }
            if (isModify)
            {
                lc.IsOperational = true;
                lc.RecalculateBuildRates();
                lc.Stats.GetCostStats(out double padCost, out _, out _);
                padCost *= Database.SettingsSC.AdditionalPadCostMult;
                foreach (var pc in lc.PadConstructions)
                {
                    pc.SetBP(padCost, 0d);
                    pc.cost = padCost;
                }

                ReassignEngineers(lc);
            }
            else
            {
                KSC.LaunchComplexes[index].Delete();
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

        private void ReassignEngineers(LaunchComplex lc)
        {
            if (engineersToReadd == 0)
                return;

            int engToAssign = Math.Min(engineersToReadd, Math.Min(lc.MaxEngineers, lc.KSC.UnassignedEngineers));
            if (engToAssign > 0)
            {
                KCTUtilities.ChangeEngineers(lc, engToAssign);
                lc.RecalculateBuildRates();
            }
        }
    }
}
