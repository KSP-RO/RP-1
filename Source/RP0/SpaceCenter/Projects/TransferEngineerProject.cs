using ROUtils.DataTypes;
using System;
using UniLinq;

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
        public int TargetAmount = 0;

        [Persistent]
        public int TransferredAmount = 0;

        [Persistent]
        private string originalSCID;

        [Persistent]
        private string targetSCID;

        [Persistent]
        private double transferCost; // per engineer

        [Persistent]
        private double transferTime;

        [Persistent]
        public PersistentListValueType<double> engineerTransferTimes = new PersistentListValueType<double>();

        public int NumLeftToTransfer => TargetAmount - TransferredAmount;

        public int NumLeftToLeave => Math.Max(NumLeftToTransfer - engineerTransferTimes.Count, 0);

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

        public bool IsValid => TargetAmount > 0;

        public TransferEngineerProject() { }

        public TransferEngineerProject(int targetAmount, double reserveFunds, LCSpaceCenter originalSC, LCSpaceCenter targetSC)
        {
            this.TargetAmount = targetAmount;
            this.reserveFunds = reserveFunds;
            OriginalSC = originalSC;
            TargetSC = targetSC;
            originalSC.EngineerTransferType = EngineerTransferType.OutTransfer;
            targetSC.EngineerTransferType = EngineerTransferType.InTransfer;
            double distanceFraction = KSCSwitcherInterop.GreatCircleDistance(originalSCID, targetSCID) / (Math.PI * FlightGlobals.GetHomeBody().Radius); // divide by half-circumference
            transferCost = Math.Max(distanceFraction * Database.SettingsSC.HireCost / 10d, 5d); // max cost of 30 funds per engineer, min of 5
            transferTime = Math.Max(distanceFraction * Database.SettingsSC.TransferTime, 86400d); // min time of 1 day
        }

        public void Clear()
        {
            TargetAmount = TransferredAmount = 0;
            if (OriginalSC != null)
            {
                OriginalSC.EngineerTransferType = EngineerTransferType.None;
                if (engineerTransferTimes.Count > 0)
                {
                    KCTUtilities.ChangeEngineers(OriginalSC, engineerTransferTimes.Count);
                    OriginalSC.RecalculateBuildRates(false);
                    engineerTransferTimes.Clear();
                }
            }
            if (TargetSC != null) TargetSC.EngineerTransferType = EngineerTransferType.None;
        }

        public double GetBuildRate()
        {
            return 1d;
        }

        public double GetFractionComplete()
        {
            double targetAmountD = TargetAmount;
            if (targetAmountD <= 0d) return 0d;

            return TransferredAmount / targetAmountD;
        }

        public string GetItemName()
        {
            return $"Transfer {TransferredAmount:N0}/{TargetAmount:N0} engineers from {OriginalSC.DisplayName} to {TargetSC.DisplayName} ({engineerTransferTimes.Count:N0} in transit)";
        }

        public ProjectType GetProjectType()
        {
            return ProjectType.None;
        }

        public double GetTimeLeft()
        {
            double timeToFunds = 0d;
            if (NumLeftToLeave > 0)
            {
                double modifiedTransferCost = -CurrencyUtils.Funds(TransactionReasonsRP0.TransferringEngineers, -transferCost);
                double curFunds = Funding.Instance.Funds;
                double fundsNeeded = reserveFunds + NumLeftToLeave * modifiedTransferCost;
                timeToFunds = FundTargetProject.EstimateTimeToFunds(curFunds, fundsNeeded, 60);
            }
            
            double timeToTransfer = CurrencyUtils.Time(TransactionReasonsRP0.TransferringEngineers, transferTime);
            if (engineerTransferTimes.Count > 0)
            {
                timeToTransfer = Math.Min(timeToTransfer, engineerTransferTimes.Max() - Planetarium.GetUniversalTime());
            }
            return timeToFunds + timeToTransfer;
        }

        public double GetTimeLeftEst(double offset)
        {
            return GetTimeLeft();
        }

        public double IncrementProgress(double UTDiff)
        {
            double currentUT = Planetarium.GetUniversalTime();

            if (NumLeftToLeave > 0)
            {
                double modifiedTransferCost = -CurrencyUtils.Funds(TransactionReasonsRP0.TransferringEngineers, -transferCost);
                double nextTransferFunds = reserveFunds + modifiedTransferCost;
                if (Funding.Instance.Funds > nextTransferFunds)
                {
                    int numCanTransfer = (int)((Funding.Instance.Funds - reserveFunds) / modifiedTransferCost);
                    numCanTransfer = Math.Max(0, numCanTransfer);
                    numCanTransfer = Math.Min(numCanTransfer, NumLeftToLeave);
                    numCanTransfer = Math.Min(numCanTransfer, OriginalSC.UnassignedEngineers);
                    KCTUtilities.SpendFunds(modifiedTransferCost, TransactionReasonsRP0.TransferringEngineers);
                    KCTUtilities.ChangeEngineers(OriginalSC, -numCanTransfer);
                    OriginalSC.RecalculateBuildRates(false);

                    double modifiedTransferTime = CurrencyUtils.Time(TransactionReasonsRP0.TransferringEngineers, transferTime);
                    double nextTransferTime = currentUT + modifiedTransferTime;
                    for (int i = 0; i < numCanTransfer; i++)
                    {
                        engineerTransferTimes.Add(nextTransferTime);
                    }
                }
            }

            int transferAmount = 0;
            for (int i = 0; i < engineerTransferTimes.Count; i++)
            {
                if (currentUT >= engineerTransferTimes[i])
                {
                    transferAmount++;
                    TransferredAmount++;
                    engineerTransferTimes.RemoveAt(i);
                }
            }

            if (transferAmount > 0)
            {
                KCTUtilities.ChangeEngineers(TargetSC, transferAmount);
                TargetSC.RecalculateBuildRates(false);
            }

            return 0d;
        }

        public bool IsComplete() => NumLeftToTransfer <= 0 || OriginalSC.UnassignedEngineers <= 0;
    }
}
