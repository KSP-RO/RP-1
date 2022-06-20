using KerbalConstructionTime;
using System;
using UnityEngine;

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
        public double programFunds;
        public double otherFundsEarned;
        public double launchFees;
        public double maintenanceFees;
        public double toolingFees;
        public double entryCosts;
        public double constructionFees;
        public double otherFees;
        public double subsidySize;
        public double subsidyPaidOut;
        public double repFromPrograms;
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
                $"{nameof(programFunds)}: {programFunds}, " +
                $"{nameof(otherFundsEarned)}: {otherFundsEarned}, " +
                $"{nameof(launchFees)}: {launchFees}, " +
                $"{nameof(maintenanceFees)}: {maintenanceFees}, " +
                $"{nameof(toolingFees)}: {toolingFees}, " +
                $"{nameof(entryCosts)}: {entryCosts}, " +
                $"{nameof(constructionFees)}: {constructionFees}, " +
                $"{nameof(otherFees)}: {otherFees}, " +
                $"{nameof(subsidySize)}: {subsidySize}, " +
                $"{nameof(subsidyPaidOut)}: {subsidyPaidOut}, " +
                $"{nameof(repFromPrograms)}: {repFromPrograms}, " +
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
    internal class FacilityConstructionDto
    {
        public SpaceCenterFacility facility;
        public int newLevel;
        public double cost;
        public string facilityID;

        public FacilityConstructionDto()
        {
        }

        public FacilityConstructionDto(FacilityConstruction fc)
        {
            facility = fc.Facility;
            facilityID = fc.FacilityID.ToString();
            newLevel = fc.NewLevel;
            cost = fc.Cost;
        }

        public override string ToString()
        {
            return
                $"{nameof(facility)}: {facility}, " +
                $"{nameof(facilityID)}: {facilityID}, " +
                $"{nameof(newLevel)}: {newLevel}, " +
                $"{nameof(cost)}: {cost}";
        }
    }

    [Serializable]
    internal class LPConstructionDto
    {
        public double cost;
        public string lpId;
        public string lcId;
        public string lcModId;

        public LPConstructionDto()
        {
        }

        public LPConstructionDto(LPConstruction lpc)
        {
            lpId = lpc.LPID.ToString();
            lcId = lpc.LCID.ToString();
            lcModId = lpc.LCModID.ToString();
            cost = lpc.Cost;
        }

        public override string ToString()
        {
            return
                $"{nameof(lpId)}: {lpId}, " +
                $"{nameof(lcId)}: {lcId}, " +
                $"{nameof(lcModId)}: {lcModId}, " +
                $"{nameof(cost)}: {cost}";
        }
    }

    [Serializable]
    internal class LCDto
    {
        public string id;
        public string modId;
        public double modCost;
        public string name;
        public LaunchComplexType lcType;
        public float massMax;
        public float massOrig;
        public Vector3 sizeMax;
        public bool isHumanRated;

        public LCDto()
        {
        }

        public LCDto(LCLogItem lc)
        {
            name = lc.Name;
            lcType = lc.LcType;
            massMax = lc.MassMax;
            massOrig = lc.MassOrig;
            sizeMax = lc.SizeMax;
            isHumanRated = lc.IsHumanRated;
            id = lc.ID.ToString();
            modId = lc.ModID.ToString();
            modCost = lc.ModCost;
        }

        public override string ToString()
        {
            return
                $"{nameof(id)}: {id}, " +
                $"{nameof(modId)}: {modId}, " +
                $"{nameof(modCost)}: {modCost}, " +
                $"{nameof(name)}: {name}, " +
                $"{nameof(lcType)}: {lcType}, " +
                $"{nameof(massMax)}: {massMax}, " +
                $"{nameof(massOrig)}: {massOrig}, " +
                $"{nameof(sizeMax)}: {sizeMax}, " +
                $"{nameof(isHumanRated)}: {isHumanRated}";
        }
    }

    [Serializable]
    internal class FacilityConstructionEventDto
    {
        public string date;
        public FacilityType facility;
        public string facilityID;
        public ConstructionState state;

        public FacilityConstructionEventDto()
        {
        }

        public FacilityConstructionEventDto(FacilityConstructionEvent fce)
        {
            date = CareerLog.UTToDate(fce.UT).ToString("o");
            facility = fce.Facility;
            facilityID = fce.FacilityID.ToString();
            state = fce.State;
        }

        public override string ToString()
        {
            return
                $"{nameof(date)}: {date}, " +
                $"{nameof(facility)}: {facility}, " +
                $"{nameof(facilityID)}: {facilityID}, " +
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
        public string lcID;
        public string lcModID;
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
            lcID = le.LCID;
            lcModID = le.LCModID;
            builtAt = le.BuiltAt;
        }

        public override string ToString()
        {
            return
                $"{nameof(date)}: {date}, " +
                $"{nameof(vesselName)}: {vesselName}, " +
                $"{nameof(vesselUID)}: {vesselUID}, " +
                $"{nameof(launchID)}: {launchID}, " +
                $"{nameof(lcID)}: {lcID}, " +
                $"{nameof(lcModID)}: {lcModID}, " +
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