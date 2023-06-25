using System.Collections.Generic;

namespace KerbalConstructionTime
{
    public class FundTarget : IKCTBuildItem
    {
        private double targetFunds;
        private const int MaxIterations = 256;
        private const double EpsilonFunds = 0.1d;
        private const double EpsilonTime = 60d;
        private const double MinTime = 0d;
        public const double MaxTime = 2d * 365.25d * 86400d;

        public FundTarget(double funds)
        {
            targetFunds = funds;
        }

        public double GetBuildRate()
        {
            return 0d;
        }

        public double GetFractionComplete()
        {
            return 0d;
        }

        public string GetItemName()
        {
            return "Fund Target";
        }

        public BuildListVessel.ListType GetListType()
        {
            return BuildListVessel.ListType.None;
        }

        public double GetTimeLeft()
        {
            double baseFunds = Funding.Instance.Funds;

            if (System.Math.Abs(targetFunds - baseFunds) <= 0d)
                return 0d;

            double timeLower = MinTime;
            double timeUpper = MaxTime;
            
            double totalFunds = 0d; // outside the loop since we'll error-check this value.
            double time = -1; // outside the loop, this is what we'll return.
            double bestTime = -1;
            for (int i = MaxIterations; i-- > 0 && timeUpper - timeLower > EpsilonTime;)
            {
                time = (timeUpper + timeLower) * 0.5d;
                // This is the post-CMQ delta.
                double fundDelta = KCTGameStates.GetBudgetDelta(time);
                totalFunds = baseFunds + fundDelta;

                if (totalFunds >= targetFunds)
                {
                    timeUpper = time;
                    bestTime = time;
                }
                else
                {
                    timeLower = time;
                }
            }

            if (bestTime > 0)
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
