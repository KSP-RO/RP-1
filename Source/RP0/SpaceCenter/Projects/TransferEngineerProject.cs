using ROUtils.DataTypes;
using System;

namespace RP0
{
    public class TransferEngineerProject : ConfigNodePersistenceBase, ISpaceCenterProject, IConfigNode
    {
        public enum EngineerTransferType
        {
            None,
            OutTransfer,
            InTransfer
        }

        [Persistent]
        private double reserveFunds = 0d;

        [Persistent]
        private int targetAmount = 0;

        [Persistent]
        private int transferredAmount = 0;

        [Persistent]
        private string originalSCID;

        [Persistent]
        private string targetSCID;

        [Persistent]
        private double transferCost; // per engineer

        private LCSpaceCenter _originalSC = null;
        public LCSpaceCenter OriginalSC
        {
            get
            {
                if (_originalSC == null && !string.IsNullOrEmpty(originalSCID))
                {
                    _originalSC = SpaceCenterManagement.Instance.FindKSC(originalSCID);
                }
                return _originalSC;
            }
            set
            {
                _originalSC = value;
                originalSCID = _originalSC.KSCName;
            }
        }

        private LCSpaceCenter _targetSC = null;
        public LCSpaceCenter TargetSC
        {
            get 
            {
                if (_targetSC == null && !string.IsNullOrEmpty(targetSCID))
                {
                    _targetSC = SpaceCenterManagement.Instance.FindKSC(targetSCID);
                }
                return _targetSC;
            }
            set
            {
                _targetSC = value;
                targetSCID = _targetSC.KSCName;
            }
        }

        public bool IsValid => targetAmount > 0;

        public TransferEngineerProject() { }

        public TransferEngineerProject(int targetAmount, double reserveFunds, LCSpaceCenter originalSC, LCSpaceCenter targetSC)
        {
            this.targetAmount = targetAmount;
            this.reserveFunds = reserveFunds;
            OriginalSC = originalSC;
            TargetSC = targetSC;
            originalSC.EngineerTransferType = EngineerTransferType.OutTransfer;
            targetSC.EngineerTransferType = EngineerTransferType.InTransfer;
            double distanceFraction = KSCSwitcherInterop.GreatCircleDistance(originalSCID, targetSCID) / (Math.PI * FlightGlobals.GetHomeBody().Radius); // divide by half-circumference
            transferCost = Math.Max(distanceFraction * Database.SettingsSC.HireCost / 10d, 5d); // max cost of 30 funds per engineer, min of 5
        }

        public void Clear()
        {
            targetAmount = transferredAmount = 0;
            if (OriginalSC != null) OriginalSC.EngineerTransferType = EngineerTransferType.None;
            if (TargetSC != null) TargetSC.EngineerTransferType = EngineerTransferType.None;
        }

        public double GetBuildRate()
        {
            return 1d;
        }

        public double GetFractionComplete()
        {
            double targetAmountD = targetAmount;
            if (targetAmountD <= 0d) return 0d;

            return transferredAmount / targetAmountD;
        }

        public string GetItemName()
        {
            return $"Transfer {transferredAmount}/{targetAmount} engineers from {OriginalSC.DisplayName} to {TargetSC.DisplayName}";
        }

        public ProjectType GetProjectType()
        {
            return ProjectType.None;
        }

        public double GetTimeLeft()
        {
            double modifiedTransferCost = -CurrencyUtils.Funds(TransactionReasonsRP0.TransferringEngineers, -transferCost);
            double curFunds = Funding.Instance.Funds;
            double fundsNeeded = reserveFunds + NumLeftToTransfer * modifiedTransferCost;
            return FundTargetProject.EstimateTimeToFunds(curFunds, fundsNeeded, 60);
        }

        public double GetTimeLeftEst(double offset)
        {
            return GetTimeLeft();
        }

        public double IncrementProgress(double UTDiff)
        {
            double modifiedTransferCost = -CurrencyUtils.Funds(TransactionReasonsRP0.TransferringEngineers, -transferCost);
            double nextTransferAt = reserveFunds + modifiedTransferCost;
            if (Funding.Instance.Funds > nextTransferAt)
            {
                int numCanTransfer = (int)((Funding.Instance.Funds - reserveFunds) / modifiedTransferCost);
                numCanTransfer = Math.Max(0, numCanTransfer);
                numCanTransfer = Math.Min(numCanTransfer, NumLeftToTransfer);
                numCanTransfer = Math.Min(numCanTransfer, OriginalSC.UnassignedEngineers);
                KCTUtilities.SpendFunds(modifiedTransferCost, TransactionReasonsRP0.TransferringEngineers);
                KCTUtilities.ChangeEngineers(OriginalSC, -numCanTransfer);
                KCTUtilities.ChangeEngineers(TargetSC, numCanTransfer);
                OriginalSC.RecalculateBuildRates(false);
                TargetSC.RecalculateBuildRates(false);
                transferredAmount += numCanTransfer;
            }

            return 0d;
        }

        public bool IsComplete() => NumLeftToTransfer <= 0 || OriginalSC.UnassignedEngineers <= 0;

        public int NumLeftToTransfer => targetAmount - transferredAmount;
    }
}
