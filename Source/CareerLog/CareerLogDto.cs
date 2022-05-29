using System;

namespace RP0
{
    [Serializable]
    internal class CareerLogDto
    {
        public string careerUuid;
        public string startDate;
        public string endDate;
        public int numEngineers;
        public int numResearchers;
        public double efficiencyEngineers;
        public double efficiencyResearchers;
        public int numNautsKilled;
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
        public double fundsGainMult;
        public double reputation;
        public double headlinesHype;

        public override string ToString()
        {
            return
                $"{nameof(careerUuid)}: {careerUuid}, " +
                $"{nameof(startDate)}: {startDate}, " +
                $"{nameof(endDate)}: {endDate}, " +
                $"{nameof(numEngineers)}: {numEngineers}, " +
                $"{nameof(numResearchers)}: {numResearchers}, " +
                $"{nameof(efficiencyEngineers)}: {efficiencyEngineers}, " +
                $"{nameof(efficiencyResearchers)}: {efficiencyResearchers}, " +
                $"{nameof(numNautsKilled)}: {numNautsKilled}, " +
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
                $"{nameof(fundsGainMult)}: {fundsGainMult}, " +
                $"{nameof(reputation)}: {reputation}, " +
                $"{nameof(headlinesHype)}: {headlinesHype}";
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
            newLevel = (int)fce.NewLevel;
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
        public double yearMult;
        public double researchRate;

        public TechResearchEventDto()
        {
        }

        public TechResearchEventDto(TechResearchEvent tre)
        {
            date = CareerLog.UTToDate(tre.UT).ToString("o");
            nodeName = tre.NodeName;
            yearMult = tre.YearMult;
            researchRate = tre.ResearchRate;
        }

        public override string ToString()
        {
            return
                $"{nameof(date)}: {date}, " +
                $"{nameof(nodeName)}: {nodeName}, " +
                $"{nameof(yearMult)}: {yearMult}, " +
                $"{nameof(researchRate)}: {researchRate}";
        }
    }

    [Serializable]
    internal class LaunchEventDto
    {
        public string date;
        public string vesselName;
        public string vesselUID;
        public string launchID;
        public EditorFacility builtAt;

        public LaunchEventDto()
        {
        }

        public LaunchEventDto(LaunchEvent le)
        {
            date = CareerLog.UTToDate(le.UT).ToString("o");
            vesselName = le.VesselName;
            vesselUID = le.VesselUID;
            launchID = le.LaunchID;
            builtAt = le.BuiltAt;
        }

        public override string ToString()
        {
            return
                $"{nameof(date)}: {date}, " +
                $"{nameof(vesselName)}: {vesselName}, " +
                $"{nameof(vesselUID)}: {vesselUID}, " +
                $"{nameof(launchID)}: {launchID}, " +
                $"{nameof(builtAt)}: {builtAt}";
        }
    }

    [Serializable]
    internal class FailureEventDto
    {
        public string date;
        public string vesselUID;
        public string launchID;
        public string part;
        public string type;

        public FailureEventDto()
        {
        }

        public FailureEventDto(FailureEvent fe)
        {
            date = CareerLog.UTToDate(fe.UT).ToString("o");
            vesselUID = fe.VesselUID;
            launchID = fe.LaunchID;
            part = fe.Part;
            type = fe.Type;
        }

        public override string ToString()
        {
            return
                $"{nameof(date)}: {date}, " +
                $"{nameof(part)}: {part}, " +
                $"{nameof(type)}: {type}, " +
                $"{nameof(vesselUID)}: {vesselUID}, " +
                $"{nameof(launchID)}: {launchID}";
        }
    }
}