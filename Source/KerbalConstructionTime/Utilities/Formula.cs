using MagiCore;
using System.Collections.Generic;
using System;

namespace KerbalConstructionTime
{
    public class Formula
    {
        public static double GetConstructionBP(double cost, SpaceCenterFacility? facilityType)
        {
            //int isAdm = 0, isAC = 0, isLP = 0, isMC = 0, isRD = 0, isRW = 0, isTS = 0, isSPH = 0, isVAB = 0, isOther = 0;
            //switch (facilityType)
            //{
            //    case SpaceCenterFacility.Administration:
            //        isAdm = 1;
            //        break;
            //    case SpaceCenterFacility.AstronautComplex:
            //        isAC = 1;
            //        break;
            //    case SpaceCenterFacility.LaunchPad:
            //        isLP = 1;
            //        break;
            //    case SpaceCenterFacility.MissionControl:
            //        isMC = 1;
            //        break;
            //    case SpaceCenterFacility.ResearchAndDevelopment:
            //        isRD = 1;
            //        break;
            //    case SpaceCenterFacility.Runway:
            //        isRW = 1;
            //        break;
            //    case SpaceCenterFacility.TrackingStation:
            //        isTS = 1;
            //        break;
            //    case SpaceCenterFacility.SpaceplaneHangar:
            //        isSPH = 1;
            //        break;
            //    case SpaceCenterFacility.VehicleAssemblyBuilding:
            //        isVAB = 1;
            //        break;
            //    default:
            //        isOther = 1;
            //        break;
            //}

            //var variables = new Dictionary<string, string>()
            //{
            //    { "C", cost.ToString() },
            //    { "O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString() },
            //    { "Adm", isAdm.ToString() },
            //    { "AC", isAC.ToString() },
            //    { "LP", isLP.ToString() },
            //    { "MC", isMC.ToString() },
            //    { "RD", isRD.ToString() },
            //    { "RW", isRW.ToString() },
            //    { "TS", isTS.ToString() },
            //    { "SPH", isSPH.ToString() },
            //    { "VAB", isVAB.ToString() },
            //    { "Other", isOther.ToString() }
            //};

            //double bp = MathParser.GetStandardFormulaValue("KSCUpgrade", variables);
            // max(1000,([C]-2000))^0.5
            double effCost = cost - 2000d;
            if (effCost < 1000d)
                effCost = 1000d;
            double bp = System.Math.Sqrt(effCost);

            return bp;
        }

        public static double GetVesselBuildRate(int index, LCItem LC, bool isHumanRatedCapped, int persDelta)
        {
            if (index > 0)
                return 0d;

            //N = num upgrades, I = rate index, L = VAB/SPH upgrade level, R = R&D level
            int personnel = Math.Max(0, LC.Engineers + persDelta);
            if (isHumanRatedCapped)
                personnel = Math.Min(personnel, LC.MaxEngineersNonHR);

            //var variables = new Dictionary<string, string>();

            //int level = Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.VehicleAssemblyBuilding);
            //variables.Add("L", level.ToString());
            //variables.Add("LM", level.ToString());
            //variables.Add("N", personnel.ToString());
            //variables.Add("I", index.ToString());
            //variables.Add("R", Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.ResearchAndDevelopment).ToString());
            //int numNodes = 0;
            //if (ResearchAndDevelopment.Instance != null)
            //    numNodes = ResearchAndDevelopment.Instance.snapshot.GetData().GetNodes("Tech").Length;
            //variables.Add("S", numNodes.ToString());

            //AddCrewVariables(variables);

            //return GetStandardFormulaValue("BuildRate", variables);
            //(([I] + 1) *[N] * 0.0025) * sign([L] -[I])
            return personnel * 0.0025d;
        }

        public static double GetConstructionBuildRate(int index, KSCItem KSC, SpaceCenterFacility? facilityType)
        {
            //N = num upgrades, I = rate index, L = VAB/SPH upgrade level, R = R&D level
            //if (KSC == null)
            //    KSC = KCTGameStates.ActiveKSC;

            //var variables = new Dictionary<string, string>();
            //variables.Add("I", index.ToString());
            //AddCrewVariables(variables);

            //return GetStandardFormulaValue("ConstructionRate", variables);
            double rate = 1d / 86400d;
            RP0.TransactionReasonsRP0 reason = facilityType == SpaceCenterFacility.LaunchPad ? RP0.TransactionReasonsRP0.StructureConstructionLC : RP0.TransactionReasonsRP0.StructureConstruction;
            return rate * RP0.CurrencyUtils.Rate(reason);
        }

        public static double GetResearchRate(double ScienceValue, int index, int upgradeDelta)
        {
            //int RnDLvl = Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.ResearchAndDevelopment);
            //int RnDMax = Utilities.GetBuildingUpgradeMaxLevel(SpaceCenterFacility.ResearchAndDevelopment);
            int Personnel = KCTGameStates.Researchers + upgradeDelta;
            //var variables = new Dictionary<string, string>
            //{
            //    { "S", ScienceValue.ToString() },
            //    { "N", Personnel.ToString() },
            //    { "R", RnDLvl.ToString() },
            //    { "RM", RnDMax.ToString() },
            //    { "O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString() },
            //    { "I", index.ToString() }
            //};
            // ((max(0.001,min([N],1)*1.0))+([N]*0.1)) / 86400 / 365 * sign(-[I])

            if (index > 0)
                return 0d;

            double rate = Personnel > 0 ? 1 + Personnel * 0.075d : 0.001d;
            const double yearToSec = 1d / (86400d * 365d);
            return rate * yearToSec;
        }

        public static double GetRolloutCost(BuildListVessel vessel)
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return 0;

            //Dictionary<string, string> variables = GetIntegrationRolloutVariables(vessel);
            //return GetStandardFormulaValue("RolloutCost", variables);
            // (([E] - (0.9 * [C])) * 0.6)+((1.0+([LH]*0.25)+([VH]*0.75))*([LT]^0.75)*20)
            LCItem vLC = vessel.LC;
            if (vLC == null)
                vLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;

            double multHR = 1d;
            if (vLC.IsHumanRated)
                multHR += 0.25d;
            if (vessel.IsHumanRated)
                multHR += 0.75d;
            double vesselPortion = (vessel.EffectiveCost - (vessel.Cost * 0.9d)) * 0.6;
            double massToUse = vLC.LCType == LaunchComplexType.Pad ? vLC.MassMax : vessel.GetTotalMass();
            double lcPortion = Math.Pow(massToUse, 0.75d) * 20d * multHR;
            return vesselPortion + lcPortion;
        }

        public static double GetIntegrationCost(BuildListVessel vessel)
        {
            //if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled ||
            //    string.IsNullOrEmpty(PresetManager.Instance.ActivePreset.FormulaSettings.IntegrationCostFormula) ||
            //    PresetManager.Instance.ActivePreset.FormulaSettings.IntegrationCostFormula == "0")
            //{
            //    return 0;
            //}

            //Dictionary<string, string> variables = GetIntegrationRolloutVariables(vessel);
            //return GetStandardFormulaValue("IntegrationCost", variables);
            
            // set to 0
            return 0d;
        }

        public static double GetIntegrationBP(BuildListVessel vessel, List<BuildListVessel> mergedVessels = null)
        {
            // IntegrationTimeFormula = [BP]
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return 0d;
            
            double BP = vessel.BuildPoints;
            if (mergedVessels != null)
            {
                foreach (var v in mergedVessels)
                    BP += v.BuildPoints;
            }
            return BP;
        }

        public static double GetAirlaunchCost(BuildListVessel vessel)
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return 0;

            //Dictionary<string, string> variables = GetIntegrationRolloutVariables(vessel);
            //return GetStandardFormulaValue("AirlaunchCost", variables);

            // [E]*0.25
            return vessel.EffectiveCost * 0.25d;
        }

        public static double GetAirlaunchBP(BuildListVessel vessel)
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return 0;

            //Dictionary<string, string> variables = GetIntegrationRolloutVariables(vessel);
            //return GetStandardFormulaValue("AirlaunchTime", variables);

            // ([E] - (0.5 * [C])) * 12
            return (vessel.EffectiveCost - (vessel.Cost * 0.5d)) * 12d;
        }

        public static double GetEngineRefurbBPMultiplier(double runTime)
        {
            //Dictionary<string, string> variables = new Dictionary<string, string>
            //{
            //    { "RT", runTime.ToString() }
            //};
            //return GetStandardFormulaValue("EngineRefurb", variables);
            //0.5*(1+max(0,1-([RT]/10)))

            double runFactor = 1d - runTime * 0.1d;
            if (runFactor < 0d)
                runFactor = 0d;
            return 0.5d * (1 + runFactor);
        }

        //private static Dictionary<string, string> GetIntegrationRolloutVariables(BuildListVessel vessel, List<BuildListVessel> mergedVessels = null)
        //{
        //    double loadedMass, emptyMass, loadedCost, emptyCost, effectiveCost, BP;
        //    loadedCost = vessel.Cost;
        //    emptyCost = vessel.EmptyCost;
        //    loadedMass = vessel.GetTotalMass();
        //    emptyMass = vessel.EmptyMass;
        //    effectiveCost = vessel.EffectiveCost;

        //    if (mergedVessels?.Count > 0)
        //    {
        //        foreach (BuildListVessel v in mergedVessels)
        //        {
        //            loadedCost += v.Cost;
        //            emptyCost += v.EmptyCost;
        //            loadedMass += v.GetTotalMass();
        //            emptyMass += v.EmptyMass;
        //            effectiveCost += v.EffectiveCost;
        //        }
        //        BP = Utilities.GetVesselBuildPoints(effectiveCost);
        //    }
        //    else
        //    {
        //        BP = vessel.BuildPoints;
        //    }

        //    float LaunchSiteLvl = 0;
        //    int EditorLevel = 0, EditorMax = 0, LaunchSiteMax = 0;
        //    int isVABVessel = 0;
        //    if (vessel.Type == BuildListVessel.ListType.None)
        //        vessel.FindTypeFromLists();
        //    if (vessel.Type == BuildListVessel.ListType.VAB)
        //    {
        //        EditorLevel = Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.VehicleAssemblyBuilding);
        //        if (KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LCType == LaunchComplexType.Pad)
        //            LaunchSiteLvl = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.ActiveLPInstance.fractionalLevel;
        //        EditorMax = Utilities.GetBuildingUpgradeMaxLevel(SpaceCenterFacility.VehicleAssemblyBuilding);
        //        LaunchSiteMax = Utilities.GetBuildingUpgradeMaxLevel(SpaceCenterFacility.LaunchPad);
        //        isVABVessel = 1;
        //    }
        //    else if (vessel.Type == BuildListVessel.ListType.SPH)
        //    {
        //        EditorLevel = Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.SpaceplaneHangar);
        //        LaunchSiteLvl = Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.Runway);
        //        EditorMax = Utilities.GetBuildingUpgradeMaxLevel(SpaceCenterFacility.SpaceplaneHangar);
        //        LaunchSiteMax = Utilities.GetBuildingUpgradeMaxLevel(SpaceCenterFacility.Runway);
        //    }

        //    double OverallMult = PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier;

        //    LCItem vLC = vessel.LC;
        //    if (vLC == null)
        //        vLC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;

        //    var variables = new Dictionary<string, string>
        //    {
        //        { "M", loadedMass.ToString() },
        //        { "m", emptyMass.ToString() },
        //        { "C", loadedCost.ToString() },
        //        { "c", emptyCost.ToString() },
        //        { "VAB", isVABVessel.ToString() },
        //        { "E", vessel.EffectiveCost.ToString() },
        //        { "BP", BP.ToString() },
        //        { "L", LaunchSiteLvl.ToString() },
        //        { "LM", LaunchSiteMax.ToString() },
        //        { "EL", EditorLevel.ToString() },
        //        { "ELM", EditorMax.ToString() },
        //        { "O", OverallMult.ToString() },
        //        { "SN", vessel.NumStages.ToString() },
        //        { "SP", vessel.NumStageParts.ToString() },
        //        { "SC", vessel.StagePartCost.ToString() },
        //        { "LT", vLC.LCType == LaunchComplexType.Pad ? vLC.MassMax.ToString() : loadedMass.ToString() },
        //        { "LH", vLC.IsHumanRated ? "1" : "0" },
        //        { "VH", vessel.IsHumanRated ? "1" : "0" }
        //    };

        //    AddCrewVariables(variables);

        //    return variables;
        //}

        public static double GetRolloutBP(BuildListVessel vessel)
        {
            // (((max(0.001, ([EC]-[C]))^1.12)*12)+((max(0.001, ([EC]-[C]-30000))^1.5)*0.35)))
            double costDeltaHighPow;
            double costDelta = vessel.EffectiveCost - vessel.Cost;
            if (costDelta < 0.001d)
            {
                costDelta = 0.001d;
                costDeltaHighPow = 0.001d;
            }
            else
            {
                costDeltaHighPow = costDelta - 30000d;
                if (costDeltaHighPow < 0.001d)
                    costDeltaHighPow = 0.001d;
            }
            return Math.Pow(costDelta, 1.12d) * 12d + Math.Pow(costDeltaHighPow, 1.5d) * 0.35d;

        }

        public static double GetReconditioningBP(BuildListVessel vessel)
        {
            //(([BP]*0.01) + (max(1, [M]-20)*2000))
            return vessel.BuildPoints * 0.01d + Math.Max(1, vessel.GetTotalMass() - 20d) * 2000d;
        }

        public static double GetRecoveryBPSPH(BuildListVessel vessel)
        {
            //((1+((1-[VAB])*1.15)) * (((max(0.001, ([EC]-[C]))^1.12)*12)+((max(0.001, ([EC]-[C]-30000))^1.5)*0.35)))
            double costDeltaHighPow;
            double costDelta = vessel.EffectiveCost - vessel.Cost;
            if (costDelta < 0.001d)
            {
                costDelta = 0.001d;
                costDeltaHighPow = 0.001d;
            }
            else
            {
                costDeltaHighPow = costDelta - 30000d;
                if (costDeltaHighPow < 0.001d)
                    costDeltaHighPow = 0.001d;
            }
            double bp = Math.Pow(costDelta, 1.12d) * 12d + Math.Pow(costDeltaHighPow, 1.5d) * 0.35d;
            return bp * 2.15d;
        }

        public static double GetRecoveryBPVAB(BuildListVessel vessel)
        {
            // (((max(0.001, ([EC]-[C]))^1.12)*12)+((max(0.001, ([EC]-[C]-30000))^1.5)*0.35)))
            double costDeltaHighPow;
            double costDelta = vessel.EffectiveCost - vessel.Cost;
            if (costDelta < 0.001d)
            {
                costDelta = 0.001d;
                costDeltaHighPow = 0.001d;
            }
            else
            {
                costDeltaHighPow = costDelta - 30000d;
                if (costDeltaHighPow < 0.001d)
                    costDeltaHighPow = 0.001d;
            }
            return Math.Pow(costDelta, 1.12d) * 12d + Math.Pow(costDeltaHighPow, 1.5d) * 0.35d;
        }

        //public static double ParseReconditioningFormula(BuildListVessel vessel, bool isReconditioning)
        //{

        //    double loadedMass, emptyMass, loadedCost, emptyCost, effectiveCost;
        //    loadedCost = vessel.Cost;
        //    emptyCost = vessel.EmptyCost;
        //    loadedMass = vessel.GetTotalMass();
        //    emptyMass = vessel.EmptyMass;
        //    effectiveCost = vessel.EffectiveCost;

        //    float LaunchSiteLvl = 0;
        //    int EditorLevel, EditorMax, LaunchSiteMax;
        //    int isVABVessel = 0;
        //    if (vessel.Type == BuildListVessel.ListType.VAB)
        //    {
        //        EditorLevel = Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.VehicleAssemblyBuilding);
        //        if (KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LCType == LaunchComplexType.Pad)
        //            LaunchSiteLvl = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.ActiveLPInstance.fractionalLevel;
        //        EditorMax = Utilities.GetBuildingUpgradeMaxLevel(SpaceCenterFacility.VehicleAssemblyBuilding);
        //        LaunchSiteMax = Utilities.GetBuildingUpgradeMaxLevel(SpaceCenterFacility.LaunchPad);
        //        isVABVessel = 1;
        //    }
        //    else
        //    {
        //        EditorLevel = Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.SpaceplaneHangar);
        //        LaunchSiteLvl = Utilities.GetBuildingUpgradeLevel(SpaceCenterFacility.Runway);
        //        EditorMax = Utilities.GetBuildingUpgradeMaxLevel(SpaceCenterFacility.SpaceplaneHangar);
        //        LaunchSiteMax = Utilities.GetBuildingUpgradeMaxLevel(SpaceCenterFacility.Runway);
        //    }
        //    double BP = vessel.BuildPoints;
        //    double OverallMult = PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier;

        //    var variables = new Dictionary<string, string>
        //    {
        //        { "M", loadedMass.ToString() },
        //        { "m", emptyMass.ToString() },
        //        { "C", loadedCost.ToString() },
        //        { "c", emptyCost.ToString() },
        //        { "EC", effectiveCost.ToString() },
        //        { "VAB", isVABVessel.ToString() },
        //        { "BP", BP.ToString() },
        //        { "L", LaunchSiteLvl.ToString() },
        //        { "LM", LaunchSiteMax.ToString() },
        //        { "EL", EditorLevel.ToString() },
        //        { "ELM", EditorMax.ToString() },
        //        { "O", OverallMult.ToString() },
        //        { "RE", (isReconditioning ? 1 : 0).ToString() },
        //        { "SN", vessel.NumStages.ToString() },
        //        { "SP", vessel.NumStageParts.ToString() },
        //        { "SC", vessel.StagePartCost.ToString() }
        //    };

        //    AddCrewVariables(variables);

        //    return GetStandardFormulaValue("Reconditioning", variables);
        //}

        //public static void AddCrewVariables(Dictionary<string, string> crewVars)
        //{
        //    int pilots = 0, engineers = 0, scientists = 0;
        //    int pLevels = 0, eLevels = 0, sLevels = 0;
        //    int pilots_total = 0, engineers_total = 0, scientists_total = 0;
        //    int pLevels_total = 0, eLevels_total = 0, sLevels_total = 0;

        //    foreach (ProtoCrewMember pcm in HighLogic.CurrentGame.CrewRoster.Crew)
        //    {
        //        if (pcm.rosterStatus == ProtoCrewMember.RosterStatus.Available || pcm.rosterStatus == ProtoCrewMember.RosterStatus.Assigned)
        //        {
        //            if (pcm.trait == "Pilot")
        //            {
        //                if (pcm.rosterStatus == ProtoCrewMember.RosterStatus.Available)
        //                {
        //                    pilots++;
        //                    pLevels += pcm.experienceLevel;
        //                }
        //                pilots_total++;
        //                pLevels_total += pcm.experienceLevel;
        //            }
        //            else if (pcm.trait == "Engineer")
        //            {
        //                if (pcm.rosterStatus == ProtoCrewMember.RosterStatus.Available)
        //                {
        //                    engineers++;
        //                    eLevels += pcm.experienceLevel;
        //                }
        //                engineers_total++;
        //                eLevels_total += pcm.experienceLevel;
        //            }
        //            else if (pcm.trait == "Scientist")
        //            {
        //                if (pcm.rosterStatus == ProtoCrewMember.RosterStatus.Available)
        //                {
        //                    scientists++;
        //                    sLevels += pcm.experienceLevel;
        //                }
        //                scientists_total++;
        //                sLevels_total += pcm.experienceLevel;
        //            }
        //        }
        //    }

        //    crewVars.Add("PiK", pilots.ToString());
        //    crewVars.Add("PiL", pLevels.ToString());

        //    crewVars.Add("EnK", engineers.ToString());
        //    crewVars.Add("EnL", eLevels.ToString());

        //    crewVars.Add("ScK", scientists.ToString());
        //    crewVars.Add("ScL", sLevels.ToString());

        //    crewVars.Add("TPiK", pilots_total.ToString());
        //    crewVars.Add("TPiL", pLevels_total.ToString());

        //    crewVars.Add("TEnK", engineers_total.ToString());
        //    crewVars.Add("TEnL", eLevels_total.ToString());

        //    crewVars.Add("TScK", scientists_total.ToString());
        //    crewVars.Add("TScL", sLevels_total.ToString());
        //}
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
