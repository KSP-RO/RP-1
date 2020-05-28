using System;

namespace RP0
{
    [Serializable]
    internal class CareerLogDto
    {
        public string CareerUuid { get; set; }
        public string Epoch { get; set; }
        public int VabUpgrades { get; set; }
        public int SphUpgrades { get; set; }
        public int RndUpgrades { get; set; }
        public double CurrentFunds { get; set; }
        public double CurrentSci { get; set; }
        public double ScienceEarned { get; set; }
        public double AdvanceFunds { get; set; }
        public double RewardFunds { get; set; }
        public double FailureFunds { get; set; }
        public double OtherFundsEarned { get; set; }
        public double LaunchFees { get; set; }
        public double MaintenanceFees { get; set; }
        public double ToolingFees { get; set; }
        public double EntryCosts { get; set; }
        public double ConstructionFees { get; set; }
        public double OtherFees { get; set; }
        public string[] LaunchedVessels { get; set; }
        public string[] ContractEvents { get; set; }
        public string[] TechEvents { get; set; }
        public string[] FacilityConstructions { get; set; }
    }
}