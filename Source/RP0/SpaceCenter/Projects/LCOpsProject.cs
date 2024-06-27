using System;
using System.Collections.Generic;
using ROUtils.DataTypes;
using ROUtils;

namespace RP0
{
    public abstract class LCOpsProject : ConfigNodePersistenceBase, ISpaceCenterProject, IConfigNode
    {
        public virtual string Name => "Null";
        [Persistent]
        public double BP = 0, progress = 0, cost = 0, mass = 0, vesselBP;
        [Persistent]
        public string associatedID = string.Empty;
        [Persistent]
        public bool isHumanRated;
        [Persistent]
        private bool _wasComplete;
        protected double _buildRate = -1;

        public Guid AssociatedIdAsGuid => new Guid(associatedID);

        public abstract TransactionReasonsRP0 TransactionReason { get; }
        protected abstract TransactionReasonsRP0 transactionReasonTime { get; }

        public VesselProject AssociatedVP => KCTUtilities.FindVPByID(LC, AssociatedIdAsGuid);

        protected LaunchComplex _lc = null;
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
                            if (this is ReconRolloutProject r)
                            {
                                if (lc.Recon_Rollout.Contains(r))
                                {
                                    _lc = lc;
                                    break;
                                }
                            }
                            else if (this is VesselRepairProject vr)
                            {
                                if (lc.VesselRepairs.Contains(vr))
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

        public LCOpsProject()
        {
        }

        public override string ToString() => Name;

        public string GetItemName() => Name;

        public virtual bool IsCapped => true;

        public virtual bool IsReversed => false;

        public virtual bool HasCost => false;

        public virtual bool IsBlocking => true;

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
                rate = KCTUtilities.GetBuildRate(LC, mass, vesselBP, isHumanRated, delta);
            else
                rate = delta == 0 ? KCTUtilities.GetBuildRate(0, LC, isHumanRated, false)
                    : KCTUtilities.GetBuildRate(0, LC.LCType == LaunchComplexType.Pad ? ProjectType.VAB : ProjectType.SPH, LC, isHumanRated, delta);

            rate *= CurrencyUtils.Rate(transactionReasonTime);

            return rate;
        }

        public double GetBuildRate(int delta)
        {
            double buildRate = CalculateBuildRate(delta);
            buildRate *= LC.Efficiency * LC.RushRate;

            if (IsReversed)
                buildRate *= -1;
            return buildRate;
        }

        public double GetFractionComplete() => IsReversed ? (BP - progress) / BP : progress / BP;

        public double GetBaseTimeLeft()
        {
            if (IsComplete())
                return 0d;

            double rMult = 1d;
            if (IsBlocking && _lc != null)
                rMult = Math.Abs(BP) / _lc.ProjectBPTotal;

            double n = IsReversed ? 0 : BP;
            return (n - progress) / (rMult * GetBuildRate());
        }

        public double GetTimeLeft()
        {
            if (IsComplete())
                return 0d;

            if (GetBuildRate() == 0d)
                return double.PositiveInfinity;

            double timeLeft = IsBlocking && _lc != null ? GetTimeLeftEstAll() : GetBaseTimeLeft();
            return TimeLeftWithEfficiencyIncrease(timeLeft);
        }
        public double GetTimeLeftEst(double offset) => GetTimeLeft();

        private double TimeLeftWithEfficiencyIncrease(double timeLeft)
        {
            if (LC.Efficiency == LCEfficiency.MaxEfficiency || timeLeft < 86400d)
                return timeLeft;

            double bpDivRate = timeLeft * LC.Efficiency;
            double newEff = LC.Efficiency;
            double portion = LC.Engineers / (double)LC.MaxEngineers;
            for (int i = 0; i < 4; ++i)
            {
                timeLeft =  bpDivRate / LC.EfficiencySource.PredictWeightedEfficiency(timeLeft, portion, out newEff, LC.Efficiency);
            }
            return timeLeft;
        }

        private struct LCPData
        {
            public double rate;
            public double bp;
        }
        private static readonly List<LCPData> _lcpData = new List<LCPData>();
        // *(*%$@ you C# structs, I don't want ot clobber the whole struct in each element
        // so we're just storing a separate list of doubles.
        private static readonly List<double> _lcpDataBPRemaining = new List<double>();
        private static double _bpTotal = 0d;
        private static void AddLCP(LCOpsProject lcp)
        {
            double bp = Math.Abs(lcp.BP);
            _bpTotal += bp;
            _lcpData.Add(new LCPData()
            {
                rate = Math.Abs(lcp.GetBuildRate()),
                bp = bp
            });
            _lcpDataBPRemaining.Add(lcp.IsReversed ? lcp.progress : bp - lcp.progress);
        }
        public double GetTimeLeftEstAll()
        {
            if (GetBuildRate() == 0d)
                return double.NaN;

            if (!IsBlocking)
                return GetBaseTimeLeft();

            AddLCP(this);
            foreach (var r in _lc.Recon_Rollout)
            {
                if (r == this || !r.IsBlocking || r.IsComplete())
                    continue;

                AddLCP(r);
            }

            double accumTime = 0d;
            while (_lcpData.Count > 0)
            {
                double leastTime = double.MaxValue;
                double bpToRemove = 0d;
                int lcpIdx = 0;
                for (int i = _lcpData.Count; i-- > 0;)
                {
                    double time = _lcpDataBPRemaining[i] / (_lcpData[i].rate * (_lcpData[i].bp / _bpTotal));
                    if (time < leastTime)
                    {
                        leastTime = time;
                        bpToRemove = _lcpData[i].bp;
                        lcpIdx = i;
                    }
                }
                accumTime += leastTime;
                if (lcpIdx == 0) // the first one we added, i.e. us
                {
                    _bpTotal = 0d;
                    _lcpData.Clear();
                    _lcpDataBPRemaining.Clear();
                    return accumTime;
                }

                for (int i = _lcpData.Count; i-- > 0;)
                {
                    if (i == lcpIdx)
                    {
                        _lcpData.RemoveAt(i);
                        _lcpDataBPRemaining.RemoveAt(i);
                        continue;
                    }
                    _lcpDataBPRemaining[i] -= (_lcpData[i].rate * (_lcpData[i].bp / _bpTotal)) * leastTime;
                }
                _bpTotal -= bpToRemove;
            }

            _bpTotal = 0d;
            _lcpData.Clear();
            _lcpDataBPRemaining.Clear();
            return accumTime;
        }

        public static double GetTotalBlockingProjectTime(LaunchComplex lc)
        {
            foreach (var r in lc.GetAllLCOps())
            {
                if (r.IsBlocking && !r.IsComplete())
                    AddLCP(r);
            }

            double accumTime = 0d;
            while (_lcpData.Count > 0)
            {
                double leastTime = double.MaxValue;
                double bpToRemove = 0d;
                int lcpIdx = 0;
                for (int i = _lcpData.Count; i-- > 0;)
                {
                    double time = _lcpDataBPRemaining[i] / (_lcpData[i].rate * (_lcpData[i].bp / _bpTotal));
                    if (time < leastTime)
                    {
                        leastTime = time;
                        bpToRemove = _lcpData[i].bp;
                        lcpIdx = i;
                    }
                }
                
                for (int i = _lcpData.Count; i-- > 0;)
                {
                    if (i == lcpIdx)
                    {
                        _lcpData.RemoveAt(i);
                        _lcpDataBPRemaining.RemoveAt(i);
                        continue;
                    }
                    _lcpDataBPRemaining[i] -= (_lcpData[i].rate * (_lcpData[i].bp / _bpTotal)) * leastTime;
                }
                accumTime += leastTime;
                _bpTotal -= bpToRemove;
            }

            _bpTotal = 0d;
            _lcpData.Clear();
            _lcpDataBPRemaining.Clear();
            return accumTime;
        }

        public static LCOpsProject GetFirstCompleting(LaunchComplex lc)
        {
            double minTime = double.MaxValue;
            LCOpsProject lcp = null;
            // The blocking LCP with the lowest time left
            // doesn't have to worry about build rate changing
            foreach (var r in lc.GetAllLCOps())
            {
                if (r.IsComplete())
                    continue;

                double time = r.GetBaseTimeLeft();
                if (time < minTime)
                {
                    minTime = time;
                    lcp = r;
                }
            }
            return lcp;
        }

        public virtual ProjectType GetProjectType() => ProjectType.Reconditioning;

        public bool IsComplete() => IsReversed ? progress <= 0 : progress >= BP;

        public double IncrementProgress(double UTDiff)
        {
            double progBefore = progress;
            double bR = GetBuildRate();
            if (bR == 0d)
                return 0d;

            if (IsBlocking && _lc != null && _lc.ProjectBPTotal > 0d)
                bR *= (BP / _lc.ProjectBPTotal);

            double toGo = (IsReversed ? 0 : BP) - progress;
            double incBP = bR * UTDiff;
            progress += incBP;
            if (progress > BP) progress = BP;
            else if (progress < 0) progress = 0;

            double cost = Math.Abs(progress - progBefore) / BP * this.cost;

            if (KSPUtils.CurrentGameIsCareer() && HasCost && this.cost > 0)
            {
                TransactionReasonsRP0 reason = TransactionReason;
                if (!CurrencyModifierQueryRP0.RunQuery(reason, -cost, 0d, 0d).CanAfford()) //If they can't afford to continue the rollout, progress stops
                {
                    progress = progBefore;
                    if (TimeWarp.CurrentRate > 1f && KCTWarpController.Instance is KCTWarpController)
                    {
                        ScreenMessages.PostScreenMessage($"Timewarp was stopped because there's insufficient funds to continue the {Name}");
                        KCTWarpController.Instance.StopWarp();
                    }
                    return UTDiff;
                }
                else
                {
                    KCTUtilities.SpendFunds(cost, reason);
                }
            }

            if (IsComplete() != _wasComplete && _lc != null)
            {
                _wasComplete = !_wasComplete;
                _lc.RecalculateProjectBP();
                MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
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
