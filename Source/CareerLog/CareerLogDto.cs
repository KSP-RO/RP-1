using System;

namespace RP0
{
    [Serializable]
    internal class CareerLogDto
    {
        public string careerUuid;
        public string startDate;
        public string endDate;
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

        public override string ToString()
        {
            return
                $"{nameof(careerUuid)}: {careerUuid}, " +
                $"{nameof(startDate)}: {startDate}, " +
                $"{nameof(endDate)}: {endDate}, " +
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
                $"{nameof(otherFees)}: {otherFees}";
        }
    }

    [Serializable]
    internal class ContractEventDto
    {
        public string internalName;
        public string date;
        public double fundsChange;
        public double repChange;
        public ContractEventType type;

        public ContractEventDto()
        {
        }

        public ContractEventDto(ContractEvent ce)
        {
            internalName = ce.InternalName;
            date = CareerLog.UTToDate(ce.UT).ToString("o");
            fundsChange = ce.FundsChange;
            repChange = ce.RepChange;
            type = ce.Type;
        }

        public override string ToString()
        {
            return
                $"{nameof(internalName)}: {internalName}, " +
                $"{nameof(date)}: {date}, " +
                $"{nameof(fundsChange)}: {fundsChange}, " +
                $"{nameof(repChange)}: {repChange}, " +
                $"{nameof(type)}: {type}";
        }
    }

    [Serializable]
    internal class FacilityConstructionEventDto
    {
        public string date;
        public SpaceCenterFacility facility;
        public int newLevel;
        public double cost;
        public ConstructionState state;

        public FacilityConstructionEventDto()
        {
        }

        public FacilityConstructionEventDto(FacilityConstructionEvent fce)
        {
            date = CareerLog.UTToDate(fce.UT).ToString("o");
            facility = fce.Facility;
            newLevel = fce.NewLevel;
            cost = fce.Cost;
            state = fce.State;
        }

        public override string ToString()
        {
            return
                $"{nameof(date)}: {date}, " +
                $"{nameof(facility)}: {facility}, " +
                $"{nameof(newLevel)}: {newLevel}, " +
                $"{nameof(cost)}: {cost}, " +
                $"{nameof(state)}: {state}";
        }
    }

    [Serializable]
    internal class TechResearchEventDto
    {
        public string date;
        public string nodeName;

        public TechResearchEventDto()
        {
        }

        public TechResearchEventDto(TechResearchEvent tre)
        {
            date = CareerLog.UTToDate(tre.UT).ToString("o");
            nodeName = tre.NodeName;
        }

        public override string ToString()
        {
            return
                $"{nameof(date)}: {date}, " +
                $"{nameof(nodeName)}: {nodeName}";
        }
    }

    [Serializable]
    internal class LaunchEventDto
    {
        public string date;
        public string vesselName;

        public LaunchEventDto()
        {
        }

        public LaunchEventDto(LaunchEvent le)
        {
            date = CareerLog.UTToDate(le.UT).ToString("o");
            vesselName = le.VesselName;
        }

        public override string ToString()
        {
            return
                $"{nameof(date)}: {date}, " +
                $"{nameof(vesselName)}: {vesselName}";
        }
    }
}