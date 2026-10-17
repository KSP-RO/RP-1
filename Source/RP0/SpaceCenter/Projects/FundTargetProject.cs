using ROUtils.DataTypes;
using System;

namespace RP0
{
    public class FundTargetProject : ConfigNodePersistenceBase, ISpaceCenterProject, IConfigNode
    {
        [Persistent]
        public bool includeCredit;

        [Persistent]
        private double targetMoney = 0d;

        [Persistent]
        private double origMoney = 0d;

        [Persistent]
        double epsilonTime = 0.25d;

        private const int MaxIterations = 256;
        private const double MinTime = 0d;
        public const double MaxTime = 2d * 365.25d * 86400d;
        public const double EpsilonTimeAutoWarp = 60d;
        public const double EpsilonTimeTarget = 0.25d;

        public bool IsValid => targetMoney != origMoney && targetMoney > 0d;

        public FundTargetProject() { }

        public FundTargetProject(double moneyTarget, bool includeCredit)
        {
            targetMoney = moneyTarget;
            origMoney = Funding.Instance?.Funds ?? 0d;
            origMoney = includeCredit ? origMoney + UnlockCreditHandler.Instance?.TotalCredit ?? 0d : origMoney;
            this.includeCredit = includeCredit;
            SetAutoWarp(true);
        }

        public void Clear()
        {
            origMoney = targetMoney = 0d;
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
            if (includeCredit)
            {
                if (Funding.Instance != null && UnlockCreditHandler.Instance != null && targetMoney != origMoney)
                    return (Funding.Instance.Funds + UnlockCreditHandler.Instance.TotalCredit - origMoney) / (targetMoney - origMoney);
            }
            else
            {
                if (Funding.Instance != null && targetMoney != origMoney)
                    return (Funding.Instance.Funds - origMoney) / (targetMoney - origMoney);
            }

            return 0d;
        }

        public string GetItemName()
        {
            if (includeCredit) return $"Fund + Credit Target: {targetMoney:N0}";
            else return $"Fund Target: {targetMoney:N0}";
        }

        public ProjectType GetProjectType()
        {
            return ProjectType.None;
        }

        public double GetTimeLeft()
        {
            if (includeCredit) return EstimateTimeToCombinedFunds(Funding.Instance.Funds + UnlockCreditHandler.Instance.TotalCredit, targetMoney, epsilonTime);
            else return EstimateTimeToFunds(Funding.Instance.Funds, targetMoney, epsilonTime);
        }

        private static double GetTimeToMoney(double baseMoney, double targetMoney, double epsilonTime, Func<double, double> GetMoneyDelta)
        {
            if (targetMoney - baseMoney <= 0.001d)
                return 0d;

            double timeLower = MinTime;
            double timeUpper = MaxTime;

            double bestTime = -1d;
            double lastMoney = 0d;
            bool lastDir = false;
            for (int i = MaxIterations; i-- > 0 && timeUpper - timeLower > epsilonTime;)
            {
                double time = (timeUpper + timeLower) * 0.5d;
                // This is the post-CMQ delta.
                double moneyDelta = GetMoneyDelta(time);
                double totalMoney = baseMoney + moneyDelta;

                if (totalMoney >= targetMoney)
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
                        if (moneyDelta < 0d)
                            timeUpper = time;
                        else
                            timeLower = time;
                    }
                    else
                    {
                        // moving in right direction
                        if (lastMoney < moneyDelta)
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
                lastMoney = moneyDelta;
                lastDir = timeLower == time;
            }

            if (bestTime > 0d)
                return bestTime;

            return double.PositiveInfinity;
        }

        public static double EstimateTimeToFunds(double baseFunds, double targetMoney, double epsilonTime)
        {
            return GetTimeToMoney(baseFunds, targetMoney, epsilonTime, SpaceCenterManagement.Instance.GetBudgetDelta);
        }

        public static double EstimateTimeToCombinedFunds(double baseMoney, double targetMoney, double epsilonTime)
        {
            return GetTimeToMoney(baseMoney, targetMoney, epsilonTime, GetCombinedFunds);
        }

        private static double GetCombinedFunds(double time)
        {
            return SpaceCenterManagement.Instance.GetBudgetDelta(time) + UnlockCreditHandler.Instance.CreditForTime(time);
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
            if (includeCredit) return Funding.Instance.Funds + UnlockCreditHandler.Instance.TotalCredit >= targetMoney;
            else return Funding.Instance.Funds >= targetMoney;
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
