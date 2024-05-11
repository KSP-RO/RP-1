using ROUtils.DataTypes;

namespace RP0
{
    public class FundTargetProject : ConfigNodePersistenceBase, ISpaceCenterProject, IConfigNode
    {
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

        public FundTargetProject() { }

        public FundTargetProject(double funds)
        {
            targetFunds = funds;
            origFunds = Funding.Instance?.Funds ?? 0d;
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
            if (Funding.Instance != null && targetFunds != origFunds)
                return (Funding.Instance.Funds - origFunds) / (targetFunds - origFunds);

            return 0d;
        }

        public string GetItemName()
        {
            return $"Fund Target: {targetFunds:N0}";
        }

        public ProjectType GetProjectType()
        {
            return ProjectType.None;
        }

        public double GetTimeLeft()
        {
            return EstimateTimeToFunds(Funding.Instance.Funds, targetFunds, epsilonTime);
        }

        public static double EstimateTimeToFunds(double baseFunds, double targetFunds, double epsilonTime)
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
                // This is the post-CMQ delta.
                double fundDelta = SpaceCenterManagement.Instance.GetBudgetDelta(time);
                double totalFunds = baseFunds + fundDelta;

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
                        if (fundDelta < 0d)
                            timeUpper = time;
                        else
                            timeLower = time;
                    }
                    else
                    {
                        // moving in right direction
                        if (lastFunds < fundDelta)
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
                lastFunds = fundDelta;
                lastDir = timeLower == time;
            }

            if (bestTime > 0d)
                return bestTime;

            return -1d;
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
            return Funding.Instance.Funds >= targetFunds;
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
