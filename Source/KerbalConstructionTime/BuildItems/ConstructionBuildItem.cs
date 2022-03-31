using System.Collections.Generic;

namespace KerbalConstructionTime
{
    public abstract class ConstructionBuildItem : IKCTBuildItem
    {
        public virtual string GetItemName() => Name;
        public double GetFractionComplete() => Progress / BP;
        public double GetTimeLeft() => (BP - Progress) / GetBuildRate();
        public double GetTimeLeftEst(double offset) => EstimatedTimeLeft;
        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.KSC;
        public bool IsComplete() => Progress >= BP;
        public virtual void IncrementProgress(double UTDiff)
        {
            if (!IsComplete()) AddProgress(GetBuildRate() * UTDiff);
            if (IsComplete() || !PresetManager.Instance.ActivePreset.GeneralSettings.KSCUpgradeTimes)
            {
                ProcessComplete();
            }
        }

        protected abstract void ProcessComplete();

        public double Progress = 0, BP = 0, Cost = 0, SpentCost = 0;
        private double _buildRate = -1d;
        public string Name;
        public int BuildListIndex { get; set; }
        public bool UpgradeProcessed = false;

        public virtual SpaceCenterFacility? FacilityType
        {
            get { return SpaceCenterFacility.LaunchPad; }
            set { }
        }


        public double EstimatedTimeLeft
        {
            get
            {
                if (_buildRate > 0)
                {
                    return GetTimeLeft();
                }
                else
                {
                    double rate = Utilities.GetConstructionRate(KSC) * KCTGameStates.EfficiencyEngineers;
                    return (BP - Progress) / rate;
                }
            }
        }

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

        public double GetBuildRate()
        {
            if (_buildRate < 0)
                UpdateBuildRate(KSC.Constructions.IndexOf(this));
            return _buildRate * KCTGameStates.EfficiencyEngineers;
        }

        public double UpdateBuildRate(int index)
        {
            double rate = Utilities.GetConstructionRate(index, KSC, 0);
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
            BP = CalculateBP(cost, FacilityType);
        }

        public static double CalculateBP(double cost, SpaceCenterFacility? facilityType)
        {
            int isAdm = 0, isAC = 0, isLP = 0, isMC = 0, isRD = 0, isRW = 0, isTS = 0, isSPH = 0, isVAB = 0, isOther = 0;
            switch (facilityType)
            {
                case SpaceCenterFacility.Administration:
                    isAdm = 1;
                    break;
                case SpaceCenterFacility.AstronautComplex:
                    isAC = 1;
                    break;
                case SpaceCenterFacility.LaunchPad:
                    isLP = 1;
                    break;
                case SpaceCenterFacility.MissionControl:
                    isMC = 1;
                    break;
                case SpaceCenterFacility.ResearchAndDevelopment:
                    isRD = 1;
                    break;
                case SpaceCenterFacility.Runway:
                    isRW = 1;
                    break;
                case SpaceCenterFacility.TrackingStation:
                    isTS = 1;
                    break;
                case SpaceCenterFacility.SpaceplaneHangar:
                    isSPH = 1;
                    break;
                case SpaceCenterFacility.VehicleAssemblyBuilding:
                    isVAB = 1;
                    break;
                default:
                    isOther = 1;
                    break;
            }

            var variables = new Dictionary<string, string>()
            {
                { "C", cost.ToString() },
                { "O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString() },
                { "Adm", isAdm.ToString() },
                { "AC", isAC.ToString() },
                { "LP", isLP.ToString() },
                { "MC", isMC.ToString() },
                { "RD", isRD.ToString() },
                { "RW", isRW.ToString() },
                { "TS", isTS.ToString() },
                { "SPH", isSPH.ToString() },
                { "VAB", isVAB.ToString() },
                { "Other", isOther.ToString() }
            };

            double bp = MathParser.GetStandardFormulaValue("KSCUpgrade", variables);
            if (bp <= 0) { bp = 1; }

            return bp;
        }

        public static double CalculateBuildTime(double cost, SpaceCenterFacility? facilityType, KSCItem KSC = null)
        {
            double bp = CalculateBP(cost, facilityType);
            double rateTotal = Utilities.GetConstructionRate(KSC) * KCTGameStates.EfficiencyEngineers;

            return bp / rateTotal;
        }


        private void AddProgress(double amt)
        {
            double newProgress = Progress + amt;
            int newFrac = (int)(newProgress / BP * 10d);
            int oldFrac = (int)(Progress / BP * 10d);
            // back-compat
            if (newFrac > oldFrac && SpentCost != Cost)
            {
                double spend = Cost * 0.1d;
                if (Utilities.CurrentGameIsCareer() && !Funding.CanAfford((float)spend))
                {
                    if (TimeWarp.CurrentRate > 1f && KCTWarpController.Instance is KCTWarpController)
                    {
                        ScreenMessages.PostScreenMessage("Timewarp was stopped because there's insufficient funds to continue the construction");
                        KCTWarpController.Instance.StopWarp();
                    }
                    return;
                }

                Utilities.SpendFunds(spend, TransactionReasons.StructureConstruction);
                SpentCost += spend;
            }
            Progress = newProgress;
            if (Progress > BP) Progress = BP;
        }
    }

    public abstract class ConstructionStorage
    {
        [Persistent]
        public string name;

        [Persistent]
        public double progress = 0, BP = 0, cost = 0, spentCost = 0;

        [Persistent]
        public bool upgradeProcessed = false;

        [Persistent]
        public int buildListIndex = -1;

        protected void SaveFields(ConstructionBuildItem b)
        {
            name = b.Name;
            progress = b.Progress;
            BP = b.BP;
            cost = b.Cost;
            spentCost = b.SpentCost;
            upgradeProcessed = b.UpgradeProcessed;
            buildListIndex = b.BuildListIndex;
        }

        protected void LoadFields(ConstructionBuildItem b)
        {
            b.Name = name;
            b.Progress = progress;
            b.BP = BP;
            b.Cost = cost;
            b.SpentCost = spentCost;
            // back-compat
            if (b.SpentCost == 0d && b.Progress >= b.BP * 0.1d)
                b.SpentCost = b.Cost;
            b.UpgradeProcessed = upgradeProcessed;
            b.BuildListIndex = buildListIndex;
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
