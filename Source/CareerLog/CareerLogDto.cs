using System;

namespace RP0
{
    [Serializable]
    internal class CareerLogDto
    {
        public string careerUuid;
        public string epoch;
        public int vabUpgrades;
        public int sphUpgrades;
        public int rndUpgrades;
        public double currentFunds;
        public double currentSci;
        public double scienceEarned;
        public double advanceFunds;
        public double rewardFunds;
        public double failureFunds;
        public double otherFundsEarned;
        public double launchFees;
        public double maintenanceFees;
        public double toolingFees;
        public double entryCosts;
        public double constructionFees;
        public double otherFees;

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