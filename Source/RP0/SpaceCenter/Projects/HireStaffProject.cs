using ROUtils.DataTypes;
using System;

namespace RP0
{
    public class HireStaffProject : ConfigNodePersistenceBase, ISpaceCenterProject, IConfigNode
    {
        [Persistent]
        private double reserveFunds = 0d;

        [Persistent]
        private int startCrewCount = 0;

        [Persistent]
        private int targetCrewCount = 0;

        [Persistent]
        private Guid _lcID = Guid.Empty;
        public Guid LCID
        {
            get
            {
                return _lcID;
            }
            set
            {
                _lcID = value;
                if (_lcID == Guid.Empty)
                    _lc = null;
                else
                    _lc = SpaceCenterManagement.Instance.LC(_lcID);
            }
        }

        private LaunchComplex _lc = null;
        public LaunchComplex LC
        {
            get
            {
                if (_lc == null && LCID != Guid.Empty)
                {
                    _lc = SpaceCenterManagement.Instance.LC(_lcID);
                }
                return _lc;
            }
            set
            {
                _lc = value;
                if (_lc == null)
                    _lcID = Guid.Empty;
                else
                    _lcID = _lc.ID;
            }
        }

        public bool IsValid => targetCrewCount > 0;

        public HireStaffProject() { }

        public HireStaffProject(int startCrewCount, int targetCrewCount, double reserveFunds, LaunchComplex lc = null)
        {
            LC = lc;
            this.startCrewCount = startCrewCount;
            this.targetCrewCount = targetCrewCount;
            this.reserveFunds = reserveFunds;
        }

        public void Clear()
        {
            reserveFunds = 0d;
            startCrewCount = targetCrewCount = 0;
            LCID = Guid.Empty;
        }

        public double GetBuildRate()
        {
            return 1d;
        }

        public double GetFractionComplete()
        {
            int total = targetCrewCount - startCrewCount;
            if (total <= 0) return 0d;

            return (CurrentAmount - startCrewCount) / total;
        }

        public string GetItemName()
        {
            return $"Reach {targetCrewCount} {(IsResearch ? "researchers" : "engineers")}";
        }

        public ProjectType GetProjectType()
        {
            return ProjectType.None;
        }

        public double GetTimeLeft()
        {
            double modifiedHireCost = -CurrencyUtils.Funds(IsResearch ? TransactionReasonsRP0.HiringResearchers : TransactionReasonsRP0.HiringEngineers, -Database.SettingsSC.HireCost);
            double curFunds = Funding.Instance.Funds;
            double fundsNeeded = reserveFunds + NumLeftToHire * modifiedHireCost;
            return FundTargetProject.EstimateTimeToFunds(curFunds, fundsNeeded, 60);
        }

        public double GetTimeLeftEst(double offset)
        {
            return GetTimeLeft();
        }

        public double IncrementProgress(double UTDiff)
        {
            double modifiedHireCost = -CurrencyUtils.Funds(IsResearch ? TransactionReasonsRP0.HiringResearchers : TransactionReasonsRP0.HiringEngineers, -Database.SettingsSC.HireCost);
            double nextHireAt = reserveFunds + modifiedHireCost;
            if (SpaceCenterManagement.Instance.Applicants > 0 || Funding.Instance.Funds > nextHireAt)
            {
                int numCanHire = (int)((Funding.Instance.Funds - reserveFunds) / modifiedHireCost);
                numCanHire = SpaceCenterManagement.Instance.Applicants + Math.Max(0, numCanHire);
                KCTUtilities.HireStaff(IsResearch, Math.Min(numCanHire, NumLeftToHire), LC);
            }

            return 0d;
        }

        public bool IsResearch => LCID == Guid.Empty;

        public bool IsComplete() => NumLeftToHire <= 0;

        public int NumLeftToHire => targetCrewCount - CurrentAmount;

        public int CurrentAmount => IsResearch ? SpaceCenterManagement.Instance.Researchers : LC.Engineers;
    }
}
