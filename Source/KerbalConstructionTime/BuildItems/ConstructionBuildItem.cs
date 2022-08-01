using System.Collections.Generic;

namespace KerbalConstructionTime
{
    public abstract class ConstructionBuildItem : IKCTBuildItem
    {
        public virtual string GetItemName() => Name;
        public double GetFractionComplete() => Progress / BP;
        public double GetTimeLeft() => (BP - Progress) / GetBuildRate();
        public double GetTimeLeftEst(double offset) => GetTimeLeft();
        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.KSC;
        public bool IsComplete() => Progress >= BP;
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

        public double Progress = 0, BP = 0, Cost = 0, SpentCost = 0, SpentRushCost = 0;
        private double _buildRate = -1d;
        public string Name;
        public int BuildListIndex { get; set; }
        public bool UpgradeProcessed = false;
        public float WorkRate = 1f;
        public double RushMultiplier => WorkRate > 1f ? PresetManager.Instance.ActivePreset.GeneralSettings.ConstructionRushCost.Evaluate(WorkRate) : 1d;

        public double RemainingCost => -RP0.CurrencyUtils.Funds(
            FacilityType == SpaceCenterFacility.LaunchPad ? RP0.TransactionReasonsRP0.StructureConstructionLC : RP0.TransactionReasonsRP0.StructureConstruction,
            -(Cost - SpentCost) * RushMultiplier);

        public virtual SpaceCenterFacility? FacilityType
        {
            get { return SpaceCenterFacility.LaunchPad; }
            set { }
        }


        public double EstimatedTimeLeft => GetTimeLeft();

        private KSCItem _ksc = null;

        public KSCItem KSC
        {
            get
            {
                if (_ksc == null)
                {
                    _ksc = KCTGameStates.KSCs.Find(ksc => ksc.Constructions.Contains(this));
                }
                return _ksc;
            }
        }

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
            return _buildRate * WorkRate;
        }

        public double UpdateBuildRate(int index)
        {
            double rate = Utilities.GetConstructionRate(index, KSC, FacilityType);
            if (rate < 0)
                rate = 0;

            _buildRate = rate;
            return _buildRate;
        }

        public virtual void Cancel()
        {
            if (Utilities.CurrentGameIsCareer())
            {
                // Nothing to reimburse - you don't get back what you've already paid.

                //if (SpentCost > 0d)
                //    Utilities.AddFunds(SpentCost, TransactionReasons.StructureConstruction);
            }

            ProcessCancel();
        }
        protected abstract void ProcessCancel();

        public void SetBP(double cost)
        {
            BP = Formula.GetConstructionBP(cost, FacilityType);
        }

        public static double CalculateBuildTime(double cost, SpaceCenterFacility? facilityType, KSCItem KSC = null)
        {
            double bp = Formula.GetConstructionBP(cost, facilityType);
            double rateTotal = Utilities.GetConstructionRate(0, KSC, facilityType);

            return bp / rateTotal;
        }


        private double AddProgress(double amt)
        {
            if (amt == 0d)
                return 0d;
            double newProgress = Progress + amt;
            double extraProgress = 0d;
            if (newProgress > BP)
            {
                extraProgress = newProgress - BP;
                newProgress = BP;
            }
            double costDelta = newProgress / BP * Cost - SpentCost;
            if (costDelta > 1d)
            {
                double rushCostDelta = costDelta * RushMultiplier;

                if (Utilities.CurrentGameIsCareer() && !CurrencyModifierQuery.RunQuery(TransactionReasons.StructureConstruction, -(float)rushCostDelta, 0f, 0f).CanAfford())
                {
                    if (TimeWarp.CurrentRate > 1f && KCTWarpController.Instance is KCTWarpController)
                    {
                        ScreenMessages.PostScreenMessage("Timewarp was stopped because there's insufficient funds to continue the construction");
                        KCTWarpController.Instance.StopWarp();
                    }
                    return 0d;
                }

                Utilities.SpendFunds(rushCostDelta, TransactionReasons.StructureConstruction);
                SpentCost += costDelta;
                SpentRushCost += rushCostDelta;
            }
            Progress = newProgress;
            return extraProgress;
        }
    }

    public abstract class ConstructionStorage : IConfigNode
    {
        [Persistent]
        public string name;

        [Persistent]
        public double progress = 0, BP = 0, cost = 0, spentCost = 0, spentRushCost = 0;

        [Persistent]
        public bool upgradeProcessed = false;

        [Persistent]
        public int buildListIndex = -1;

        [Persistent]
        public float workRate;

        protected void SaveFields(ConstructionBuildItem b)
        {
            name = b.Name;
            progress = b.Progress;
            BP = b.BP;
            cost = b.Cost;
            spentCost = b.SpentCost;
            spentRushCost = b.SpentRushCost;
            upgradeProcessed = b.UpgradeProcessed;
            buildListIndex = b.BuildListIndex;
            workRate = b.WorkRate;
        }

        protected void LoadFields(ConstructionBuildItem b)
        {
            b.Name = name;
            b.Progress = progress;
            b.BP = BP;
            b.Cost = cost;
            b.SpentCost = spentCost;
            b.SpentRushCost = spentRushCost;
            b.UpgradeProcessed = upgradeProcessed;
            b.BuildListIndex = buildListIndex;
            b.WorkRate = workRate;
            if (KCTGameStates.LoadedSaveVersion < 4)
            {
                b.WorkRate = 1f;
                b.SpentRushCost = spentCost;

                // old formula was ([C]+10000)*36 so let's get Cost out.
                b.BP /= 36d;
                b.BP -= 10000d;

                b.BP = Formula.GetConstructionBP(b.BP, null); // we're not using facilityType anyway.
                b.Progress = progress / BP * b.BP;
            }
        }

        public virtual void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public virtual void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
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
