using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalConstructionTime
{
    public abstract class LCProject : IKCTBuildItem
    {
        public virtual string Name => "Null";
        public double BP = 0, Progress = 0, Cost = 0, Mass = 0, VesselBP;
        public string AssociatedID = string.Empty;
        public bool IsHumanRated;

        public BuildListVessel AssociatedBLV => Utilities.FindBLVesselByID(LC, new Guid(AssociatedID));

        protected LCItem _lc = null;
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
                            if (this is ReconRollout r)
                            {
                                if (lc.Recon_Rollout.Contains(r))
                                {
                                    _lc = lc;
                                    break;
                                }
                            }
                            else if(this is AirlaunchPrep a)
                            {
                                if (lc.AirlaunchPrep.Contains(a))
                                {
                                    _lc = lc;
                                    break;
                                }
                            }
                        }
                    }
                }
                
                return _lc;
            }
            set
            {
                _lc = value;
            }
        }

        public LCProject()
        {
            Progress = 0;
            BP = 0;
            Cost = 0;
            Mass = 0;
            VesselBP = 0;
            AssociatedID = "";
            IsHumanRated = false;
        }

        public string GetItemName() => Name;

        public virtual bool IsCapped => true;

        public virtual bool IsReversed => false;

        public virtual bool HasCost => false;

        public double GetBuildRate() => GetBuildRate(0);

        public double GetBuildRate(int delta = 0)
        {
            double buildRate;
            if (IsCapped)
                buildRate = Utilities.GetBuildRate(LC, Mass, VesselBP, IsHumanRated, delta);
            else
                buildRate = delta == 0 ? Utilities.GetBuildRate(0, LC, IsHumanRated, false)
                : Utilities.GetBuildRate(0, LC.LCType == LaunchComplexType.Pad ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH, LC, IsHumanRated, delta);
            buildRate *= Utilities.GetEngineerEfficiencyMultipliers(LC) * LC.RushRate;

            if (IsReversed)
                buildRate *= -1;
            return buildRate;
        }

        public double GetFractionComplete() => IsReversed ? (BP - Progress) / BP : Progress / BP;

        public double GetTimeLeft()
        {
            double n = IsReversed ? 0 : BP;
            return (n - Progress) / GetBuildRate();
        }
        public double GetTimeLeftEst(double offset) => GetTimeLeft();

        public virtual BuildListVessel.ListType GetListType() => BuildListVessel.ListType.Reconditioning;

        public bool IsComplete() => IsReversed ? Progress <= 0 : Progress >= BP;

        public void IncrementProgress(double UTDiff)
        {
            double progBefore = Progress;
            Progress += GetBuildRate() * UTDiff;
            if (Progress > BP) Progress = BP;
            else if (Progress < 0) Progress = 0;

            int prevStep = (int)Math.Floor(10 * progBefore / BP);
            int curStep = (int)Math.Floor(10 * Progress / BP);

            if (Utilities.CurrentGameIsCareer() && HasCost && Cost > 0)
            {
                int steps = curStep - prevStep;
                if (steps > 0) //  Pay or halt at 10% intervals
                {
                    if (Funding.Instance.Funds < Cost / 10) //If they can't afford to continue the rollout, progress stops
                    {
                        Progress = progBefore;
                        if (TimeWarp.CurrentRate > 1f && KCTWarpController.Instance is KCTWarpController)
                        {
                            ScreenMessages.PostScreenMessage($"Timewarp was stopped because there's insufficient funds to continue the {Name}");
                            KCTWarpController.Instance.StopWarp();
                        }
                    }
                    else
                        Utilities.SpendFunds(steps * Cost / 10, TransactionReasons.VesselRollout);
                }
            }
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
