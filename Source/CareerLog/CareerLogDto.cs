using System;

namespace RP0
{
    [Serializable]
    internal class CareerLogDto
    {
        public string careerUuid;
        public string epoch;
        public string vabUpgrades;
        public string sphUpgrades;
        public string rndUpgrades;
        public string currentFunds;
        public string currentSci;
        public string scienceEarned;
        public string advanceFunds;
        public string rewardFunds;
        public string failureFunds;
        public string otherFundsEarned;
        public string launchFees;
        public string maintenanceFees;
        public string toolingFees;
        public string entryCosts;
        public string constructionFees;

        public string otherFees;
        public string[] launchedVessels;
        public string[] contractEvents;
        public string[] techEvents;
        public string[] facilityConstructions;

        public override string ToString()
        {
            return
                $"{nameof(careerUuid)}: {careerUuid}, " +
                $"{nameof(epoch)}: {epoch}, " +
                $"{nameof(vabUpgrades)}: {vabUpgrades}, " +
                $"{nameof(sphUpgrades)}: {sphUpgrades}, " +
                $"{nameof(rndUpgrades)}: {rndUpgrades}, " +
                $"{nameof(currentFunds)}: {currentFunds}, " +
                $"{nameof(currentSci)}: {currentSci}, " +
                $"{nameof(scienceEarned)}: {scienceEarned}, " +
                $"{nameof(advanceFunds)}: {advanceFunds}, " +
                $"{nameof(rewardFunds)}: {rewardFunds}, " +
                $"{nameof(failureFunds)}: {failureFunds}, " +
                $"{nameof(otherFundsEarned)}: {otherFundsEarned}, " +
                $"{nameof(launchFees)}: {launchFees}, " +
                $"{nameof(maintenanceFees)}: {maintenanceFees}, " +
                $"{nameof(toolingFees)}: {toolingFees}, " +
                $"{nameof(entryCosts)}: {entryCosts}, " +
                $"{nameof(constructionFees)}: {constructionFees}, " +
                $"{nameof(otherFees)}: {otherFees}, " +
                $"{nameof(launchedVessels)}: {launchedVessels}, " +
                $"{nameof(contractEvents)}: {contractEvents}, " +
                $"{nameof(techEvents)}: {techEvents}, " +
                $"{nameof(facilityConstructions)}: {facilityConstructions}";
        }
    }
}