using System;
using System.Collections.Generic;
using UniLinq;
using RP0;

namespace KerbalConstructionTime
{
    public abstract class LCProject : IKCTBuildItem
    {
        public virtual string Name => "Null";
        public double BP = 0, Progress = 0, Cost = 0, Mass = 0, VesselBP;
        public string AssociatedID = string.Empty;
        public bool IsHumanRated;
        protected double _buildRate = -1;

        protected abstract TransactionReasonsRP0 transactionReason { get; }
        protected abstract TransactionReasonsRP0 transactionReasonTime { get; }

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

        public double GetBuildRate() => GetBaseBuildRate()
            * LC.Efficiency * LC.RushRate * (IsReversed ? -1d : 1d);

        public void UpdateBuildRate()
        {
            _buildRate = CalculateBuildRate(0);
        }

        protected double GetBaseBuildRate()
        {
            if (_buildRate < 0d)
                _buildRate = CalculateBuildRate(0);

            return _buildRate;
        }

        protected double CalculateBuildRate(int delta)
        {
            double rate;
            if (IsCapped)
                rate = Utilities.GetBuildRate(LC, Mass, VesselBP, IsHumanRated, delta);
            else
                rate = delta == 0 ? Utilities.GetBuildRate(0, LC, IsHumanRated, false)
                    : Utilities.GetBuildRate(0, LC.LCType == LaunchComplexType.Pad ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH, LC, IsHumanRated, delta);
            
            rate *= CurrencyUtils.Rate(transactionReasonTime);

            return rate;
        }

        public double GetBuildRate(int delta)
        {
            double buildRate = CalculateBuildRate(delta);
            buildRate *= LC.Efficiency * LC.RushRate * CurrencyUtils.Rate(transactionReasonTime);

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

        public double IncrementProgress(double UTDiff)
        {
            double progBefore = Progress;
            double bR = GetBuildRate();
            if (bR == 0d)
                return 0d;

            double toGo = BP - Progress;
            double incBP = bR * UTDiff;
            Progress += incBP;
            if (Progress > BP) Progress = BP;
            else if (Progress < 0) Progress = 0;

            double cost = (Progress - progBefore) / BP * Cost;

            if (Utilities.CurrentGameIsCareer() && HasCost && Cost > 0)
            {
                var reason = transactionReason;
                if (!CurrencyModifierQueryRP0.RunQuery(reason, -cost, 0d, 0d).CanAfford()) //If they can't afford to continue the rollout, progress stops
                {
                    Progress = progBefore;
                    if (TimeWarp.CurrentRate > 1f && KCTWarpController.Instance is KCTWarpController)
                    {
                        ScreenMessages.PostScreenMessage($"Timewarp was stopped because there's insufficient funds to continue the {Name}");
                        KCTWarpController.Instance.StopWarp();
                    }
                    return UTDiff;
                }
                else
                {
                    Utilities.SpendFunds(cost, reason);
                }
            }
            if (IsComplete())
            {
                return (1d - Math.Abs(toGo) / Math.Abs(incBP)) * UTDiff;
            }

            return 0d;
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
