using ROUtils.DataTypes;
using System;

namespace RP0
{
    public class FundTargetProject : ConfigNodePersistenceBase, ISpaceCenterProject, IConfigNode
    {
        [Persistent]
        public bool includeCredit;

        [Persistent]
        private double targetFunds = 0d;

        [Persistent]
        private double origFunds = 0d;

        [Persistent]
        double epsilonTime = 0.25d;

        private const int MaxIterations = 256;
        private const double MinTime = 0d;
        public const double MaxTime = 2d * 365.25d * 86400d;
        public const double EpsilonTimeAutoWarp = 60d;
        public const double EpsilonTimeTarget = 0.25d;

        public bool IsValid => targetFunds != origFunds && targetFunds > 0d;

        public static double GetCurrentFunds(bool includeCredit) => (Funding.Instance?.Funds ?? 0d) + (includeCredit ? (UnlockCreditHandler.Instance?.TotalCredit ?? 0d) : 0d);

        public FundTargetProject() { }

        public FundTargetProject(double fundTarget, bool includeCredit)
        {
            targetFunds = fundTarget;
            origFunds = GetCurrentFunds(includeCredit);
            this.includeCredit = includeCredit;
            SetAutoWarp(true);
        }

        public void Clear()
        {
            origFunds = targetFunds = 0d;
            SetAutoWarp(true);
        }

        public void SetAutoWarp(bool val)
        {
            epsilonTime = val ? EpsilonTimeAutoWarp : EpsilonTimeTarget;
        }

        public double GetBuildRate()
        {
            return 1d;
        }

        public double GetFractionComplete()
        {
            if (targetFunds != origFunds)
            {
                return (GetCurrentFunds(includeCredit) - origFunds) / (targetFunds - origFunds);
            }

            return 0d;
        }

        public string GetItemName()
        {
            if (includeCredit) return $"Fund + Credit Target: {targetFunds:N0}";
            else return $"Fund Target: {targetFunds:N0}";
        }

        public ProjectType GetProjectType()
        {
            return ProjectType.None;
        }

        public double GetTimeLeft()
        {
            if (includeCredit) return EstimateTimeToCombinedFunds(GetCurrentFunds(true), targetFunds, epsilonTime);
            else return EstimateTimeToFunds(Funding.Instance.Funds, targetFunds, epsilonTime);
        }

        private static double GetTimeToFunds(double baseFunds, double targetFunds, double epsilonTime, Func<double, double> GetFundsDelta)
        {
            if (targetFunds - baseFunds <= 0.001d)
                return 0d;

            double timeLower = MinTime;
            double timeUpper = MaxTime;

            double bestTime = -1d;
            double lastFunds = 0d;
            bool lastDir = false;
            for (int i = MaxIterations; i-- > 0 && timeUpper - timeLower > epsilonTime;)
            {
                double time = (timeUpper + timeLower) * 0.5d;
                double fundsDelta = GetFundsDelta(time);
                double totalFunds = baseFunds + fundsDelta;

                if (totalFunds >= targetFunds)
                {
                    timeUpper = time;
                    bestTime = time;
                }
                else
                {
                    // On the first iteration, check if delta
                    // is negative and if so lower upper bound
                    if (i == (MaxIterations - 1))
                    {
                        if (fundsDelta < 0d)
                            timeUpper = time;
                        else
                            timeLower = time;
                    }
                    else
                    {
                        // moving in right direction
                        if (lastFunds < fundsDelta)
                        {
                            if (lastDir)
                                timeLower = time;
                            else
                                timeUpper = time;
                        }
                        else
                        {
                            // moving in wrong direction--last iteration
                            // was better
                            if (lastDir)
                            {
                                timeUpper = time;
                                timeLower = timeLower - (time - timeLower) * 0.75d;
                            }
                            else
                            {
                                timeLower = time;
                                timeUpper = timeUpper + (timeUpper - time) * 0.75d;
                            }
                        }
                    }
                }
                lastFunds = fundsDelta;
                lastDir = timeLower == time;
            }

            if (bestTime > 0d)
                return bestTime;

            return double.PositiveInfinity;
        }

        public static double EstimateTimeToFunds(double baseFunds, double targetFunds, double epsilonTime)
        {
            return GetTimeToFunds(baseFunds, targetFunds, epsilonTime, SpaceCenterManagement.Instance.GetBudgetDelta);
        }

        public static double EstimateTimeToCombinedFunds(double baseFunds, double targetFunds, double epsilonTime)
        {
            return GetTimeToFunds(baseFunds, targetFunds, epsilonTime, GetCombinedFunds);
        }

        private static double GetCombinedFunds(double time)
        {
            return SpaceCenterManagement.Instance.GetBudgetDelta(time) + (CurrencyUtils.Rate(TransactionReasonsRP0.RateUnlockCreditIncrease) * UnlockCreditHandler.Instance.TotalCreditForTime(time));
        }

        public double GetTimeLeftEst(double offset)
        {
            return GetTimeLeft();
        }

        public double IncrementProgress(double UTDiff)
        {
            return 0d;
        }

        public bool IsComplete()
        {
            return GetCurrentFunds(includeCredit) >= targetFunds;
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
