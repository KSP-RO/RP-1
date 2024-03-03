using ROUtils.DataTypes;
using ROUtils;

namespace RP0
{
    public abstract class ConstructionProject : ConfigNodePersistenceBase, ISpaceCenterProject, IConfigNode
    {
        [Persistent]
        public double progress = 0;
        [Persistent]
        public double BP = 0;
        [Persistent]
        public double cost = 0;
        [Persistent]
        public double spentCost = 0;
        [Persistent]
        public double spentRushCost = 0;
        [Persistent]
        public string name;
        [Persistent]
        public bool upgradeProcessed = false;
        [Persistent]
        public float workRate = 1f;

        private double _buildRate = -1d;

        public double RushMultiplier => workRate > 1f ? Database.SettingsSC.ConstructionRushCost.Evaluate(workRate) : 1d;

        public double RemainingCost => -CurrencyUtils.Funds(
            FacilityType == SpaceCenterFacility.LaunchPad ? TransactionReasonsRP0.StructureConstructionLC : TransactionReasonsRP0.StructureConstruction,
            -(cost - spentCost) * RushMultiplier);

        public virtual SpaceCenterFacility FacilityType
        {
            get { return SpaceCenterFacility.LaunchPad; }
            set { }
        }


        public double EstimatedTimeLeft => GetTimeLeft();

        private LCSpaceCenter _ksc = null;

        public LCSpaceCenter KSC
        {
            get
            {
                if (_ksc == null)
                {
                    _ksc = SpaceCenterManagement.Instance.KSCs.Find(ksc => ksc.Constructions.Contains(this));
                }
                return _ksc;
            }
        }

        public override string ToString() => name;

        public virtual string GetItemName() => name;
        public double GetFractionComplete() => progress / BP;
        public double GetTimeLeft() => (BP - progress) / GetBuildRate();
        public double GetTimeLeftEst(double offset) => GetTimeLeft();
        public ProjectType GetProjectType() => ProjectType.KSC;
        public bool IsComplete() => progress >= BP;
        public virtual double IncrementProgress(double UTDiff)
        {
            double excessTime = 0d;
            if (!IsComplete())
            {
                double bR = GetBuildRate();
                excessTime = AddProgress(bR * UTDiff) / bR;
            }

            if (IsComplete() || !PresetManager.Instance.ActivePreset.GeneralSettings.KSCUpgradeTimes)
            {
                ProcessComplete();
            }

            return excessTime;
        }

        protected abstract void ProcessComplete();

        public double GetConstructionCostOverTime(double time)
        {
            double left = GetTimeLeft();
            if (GetBuildRate() == 0d)
                return 0d;

            double val = -RemainingCost;
            if (left > time)
                val *= (time / left);

            return val;
        }

        public double GetBuildRate()
        {
            if (_buildRate < 0)
                UpdateBuildRate(KSC.Constructions.IndexOf(this));
            return _buildRate * workRate;
        }

        public double UpdateBuildRate(int index)
        {
            double rate = KCTUtilities.GetConstructionRate(index, KSC, FacilityType);
            if (rate < 0)
                rate = 0;

            _buildRate = rate;
            return _buildRate;
        }

        public virtual void Cancel()
        {
            if (KSPUtils.CurrentGameIsCareer())
            {
                // Nothing to reimburse - you don't get back what you've already paid.

                //if (SpentCost > 0d)
                //    Utilities.AddFunds(SpentCost, TransactionReasons.StructureConstruction);
            }

            ProcessCancel();
        }
        protected abstract void ProcessCancel();

        public void SetBP(double cost, double oldCost)
        {
            BP = Formula.GetConstructionBP(cost, oldCost, FacilityType);
        }

        public static double CalculateBuildTime(double cost, double oldCost, SpaceCenterFacility facilityType, LCSpaceCenter KSC = null)
        {
            double bp = Formula.GetConstructionBP(cost, oldCost, facilityType);
            double rateTotal = KCTUtilities.GetConstructionRate(0, KSC, facilityType);

            return bp / rateTotal;
        }


        private double AddProgress(double amt)
        {
            if (amt == 0d)
                return 0d;
            double newProgress = progress + amt;
            double extraProgress = 0d;
            if (newProgress > BP)
            {
                extraProgress = newProgress - BP;
                newProgress = BP;
            }
            double costDelta = newProgress / BP * cost - spentCost;
            if (costDelta > 1d)
            {
                double rushCostDelta = costDelta * RushMultiplier;

                if (KSPUtils.CurrentGameIsCareer() && !CurrencyModifierQuery.RunQuery(TransactionReasons.StructureConstruction, -(float)rushCostDelta, 0f, 0f).CanAfford())
                {
                    if (TimeWarp.CurrentRate > 1f && KCTWarpController.Instance is KCTWarpController)
                    {
                        ScreenMessages.PostScreenMessage("Timewarp was stopped because there's insufficient funds to continue the construction");
                        KCTWarpController.Instance.StopWarp();
                    }
                    return 0d;
                }

                KCTUtilities.SpendFunds(rushCostDelta, TransactionReasons.StructureConstruction);
                spentCost += costDelta;
                spentRushCost += rushCostDelta;
            }
            progress = newProgress;
            return extraProgress;
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
