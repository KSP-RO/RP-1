using MagiCore;
using System.Collections.Generic;
using System;

namespace KerbalConstructionTime
{
    public class Formula
    {
        public const double ResourceValidationRatioOfVesselMassMin = 0.005d;
        public const double ResourceValidationAbsoluteMassMin = 0.0d;
        private static HashSet<string> _resourceKeys = new HashSet<string>();
        private const double _EngineerBPRate = 0.0025d;
        private const double _RolloutCostBasePortion = 0.5d;
        private const double _RolloutCostSubsidyPortion = 1d - _RolloutCostBasePortion;

        private static RealFuels.Tanks.TankDefinition _tankDefSMIV = null;
        public static RealFuels.Tanks.TankDefinition TankDefSMIV
        {
            get
            {
                if (_tankDefSMIV == null)
                    _tankDefSMIV = RealFuels.MFSSettings.tankDefinitions["SM-IV"];

                return _tankDefSMIV;
            }
        }

        public static double GetConstructionBP(double cost, SpaceCenterFacility facilityType)
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
            double bp = Math.Sqrt(cost);

            const double minDays = 3d;
            return Math.Max(bp, minDays);
        }

        public static double GetVesselBuildRate(int index, LCItem LC, bool isHumanRatedCapped, int persDelta)
        {
            if (index > 0 || !LC.IsOperational)
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
            return personnel * _EngineerBPRate;
        }

        public static double GetConstructionBuildRate(int index, KSCItem KSC, SpaceCenterFacility facilityType)
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
            int Personnel = KerbalConstructionTimeData.Instance.Researchers + upgradeDelta;
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

        public static double GetVesselBuildPoints(double totalEffectiveCost)
        {
            //var formulaParams = new Dictionary<string, string>()
            //{
            //    { "E", totalEffectiveCost.ToString() },
            //    { "O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString() }
            //};
            //double finalBP = MathParser.GetStandardFormulaValue("BP", formulaParams);
            // 1000 + (([E]^0.95)*216*min(1,max(0.5,([E]-500)/1500))) + ((max(0,[E]-50000)^1.4)*0.864)
            double bpScalar = UtilMath.Clamp((totalEffectiveCost - 500d) / 1500d, 0.5d, 1d);
            double finalBP = 1000d + Math.Pow(totalEffectiveCost, 0.95) * 216 * bpScalar;
            double powScalar = totalEffectiveCost - 50000d;
            if (powScalar > 0)
                finalBP += Math.Pow(totalEffectiveCost, 1.4d) * 0.864d;

            KCTDebug.Log($"BP: {finalBP}");
            return finalBP;
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
            if (vessel.humanRated)
                multHR += 0.75d;
            double vesselPortion = (vessel.effectiveCost - (vessel.cost * 0.9d)) * 0.6;
            double massToUse = vLC.LCType == LaunchComplexType.Pad ? vLC.MassMax : vessel.GetTotalMass();
            double lcPortion = Math.Pow(massToUse, 0.75d) * 20d * multHR;
            double result = vesselPortion + lcPortion;
            return result * _RolloutCostBasePortion + Math.Max(0d, result * _RolloutCostSubsidyPortion - GetRolloutBP(vessel) * RP0.MaintenanceHandler.Settings.salaryEngineers / (365.25d * 86400d * _EngineerBPRate));
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
            
            double BP = vessel.buildPoints;
            if (mergedVessels != null)
            {
                foreach (var v in mergedVessels)
                    BP += v.buildPoints;
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
            double result = vessel.effectiveCost * 0.25d;
            return result * _RolloutCostBasePortion + Math.Max(0d, result * _RolloutCostSubsidyPortion - GetAirlaunchBP(vessel) * RP0.MaintenanceHandler.Settings.salaryEngineers / (365.25d * 86400d * _EngineerBPRate));
        }

        public static double GetAirlaunchBP(BuildListVessel vessel)
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return 0;

            //Dictionary<string, string> variables = GetIntegrationRolloutVariables(vessel);
            //return GetStandardFormulaValue("AirlaunchTime", variables);

            // ([E] - (0.5 * [C])) * 12
            return (vessel.effectiveCost - (vessel.cost * 0.5d)) * 12d;
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
            double costDelta = vessel.effectiveCost - vessel.cost;
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
            return vessel.buildPoints * 0.01d + Math.Max(1, vessel.GetTotalMass() - 20d) * 2000d;
        }

        public static double GetRecoveryBPSPH(BuildListVessel vessel)
        {
            //((1+((1-[VAB])*1.15)) * (((max(0.001, ([EC]-[C]))^1.12)*12)+((max(0.001, ([EC]-[C]-30000))^1.5)*0.35)))
            double costDeltaHighPow;
            double costDelta = vessel.effectiveCost - vessel.cost;
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
            double costDelta = vessel.effectiveCost - vessel.cost;
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

        public static double ResourceTankCost(string res, double amount, bool isModify, LaunchComplexType type)
        {
            var def = TankDefSMIV;
            const double overallMultiplier = 1.0d;
            const double amountMultiplier = 60d;
            const double tankMultiplier = 1.0d;
            const double baseTankCostPerL = 0.5d;
            const double rfTankCostPerLMultiplier = 20d;
            const double modifyMultiplier = 0.6d;

            HashSet<string> ignoredRes = type == LaunchComplexType.Hangar ? GuiDataAndWhitelistItemsDatabase.HangarIgnoreRes : GuiDataAndWhitelistItemsDatabase.PadIgnoreRes;

            if (ignoredRes.Contains(res)
                || !GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(res))
                return 0d;

            if (def.tankList.TryGetValue(res, out var tank) && PartResourceLibrary.Instance.GetDefinition(res) is PartResourceDefinition resDef)
            {
                double tankVol = amount / tank.utilization;
                double cost = (baseTankCostPerL + tank.cost * rfTankCostPerLMultiplier) * tankVol * tankMultiplier + amount * resDef.unitCost * amountMultiplier;
                if (PresetManager.Instance.ActivePreset.PartVariables.Resource_Variables.TryGetValue(res, out double mult))
                    cost *= mult;

                if (isModify)
                    cost = modifyMultiplier;

                return cost * overallMultiplier;
            }

            return 0d;
        }

        /// <summary>
        /// Note this is NOT bidirectional.
        /// ourStats can be closer to 'stats'
        /// than 'stats' is to ourStats.
        /// Always run this on the *destination* LC.
        /// i.e. if you are removing HR from an LC,
        /// make a new LCData with it off, and call
        /// GetCloseness(newLCData, orig)
        /// </summary>
        /// </summary>
        /// <param name="ourStats"></param>
        /// <param name="otherStats"></param>
        /// <returns></returns>
        public static double GetLCCloseness(LCData ourStats, LCData otherStats)
        {
            if (ourStats.Compare(otherStats))
                return 1d;

            if (otherStats.lcType != ourStats.lcType)
                return 0d;

            if (ourStats.lcType == LaunchComplexType.Hangar)
                return 1d;

            LCData bigger, smaller;
            if (otherStats.massMax > ourStats.massMax)
            {
                bigger = otherStats;
                smaller = ourStats;
            }
            else
            {
                smaller = otherStats;
                bigger = ourStats;
            }

            double minMassDiff = Math.Max(1d, smaller.massMax * 0.05d);
            double massFactor = 1d;
            if (bigger.massMax > smaller.massMax + minMassDiff)
            {
                if (bigger.massMax > 2d * smaller.massMax)
                    return 0d;
                if (smaller.massMax < 0.5d * bigger.massMax)
                    return 0d;

                massFactor = (smaller.massMax + minMassDiff) / bigger.massMax;
                massFactor *= massFactor * massFactor;
            }

            if (otherStats.sizeMax.y > ourStats.sizeMax.y)
            {
                bigger = otherStats;
                smaller = ourStats;
            }
            else
            {
                smaller = otherStats;
                bigger = ourStats;
            }

            double sizeFactor = 1d;

            double minHeightDiff = Math.Max(smaller.sizeMax.y * 0.1d, 2d);
            if (bigger.sizeMax.y - smaller.sizeMax.y > minHeightDiff)
            {
                sizeFactor = (smaller.sizeMax.y + minHeightDiff) / bigger.sizeMax.y;
                sizeFactor *= sizeFactor * sizeFactor;
            }

            double biggerXZ = Math.Max(bigger.sizeMax.x, bigger.sizeMax.z);
            double smallerXZ = Math.Max(smaller.sizeMax.x, smaller.sizeMax.z);
            if (smallerXZ > biggerXZ)
            {
                double t = biggerXZ;
                biggerXZ = smallerXZ;
                smallerXZ = t;
            }

            if (smallerXZ < biggerXZ - Math.Max(smallerXZ * 0.1d, 0.2d))
            {
                // Add the height in so the ratio is much closer to 1.
                smallerXZ += smaller.sizeMax.y;
                biggerXZ += smaller.sizeMax.y;
                sizeFactor *= (smallerXZ / biggerXZ);
            }

            double hrFactor = 1d;
            if (ourStats.isHumanRated && !otherStats.isHumanRated)
                hrFactor = 0.7d;
            else if (ourStats.isHumanRated != otherStats.isHumanRated)
                hrFactor = 0.9d;

            // compare the resources handled at each complex
            double resFactor = 1d;
            double resTotal = 0d;
            double resDiffs = 0d;
            foreach (var r in ourStats.resourcesHandled.Keys)
                _resourceKeys.Add(r);
            foreach (var r in otherStats.resourcesHandled.Keys)
                _resourceKeys.Add(r);

            var def = TankDefSMIV;
            foreach (string key in _resourceKeys)
            {
                if (GuiDataAndWhitelistItemsDatabase.PadIgnoreRes.Contains(key)
                    || !GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(key))
                    continue;

                ourStats.resourcesHandled.TryGetValue(key, out double ours);
                otherStats.resourcesHandled.TryGetValue(key, out double other);
                double rescaledOurs = ours;
                double rescaledTheirs = other;
                if (def.tankList.TryGetValue(key, out var tank))
                {
                    double mult = 1d / tank.utilization;
                    rescaledOurs *= mult;
                    rescaledTheirs *= mult;
                }
                else
                {
                    PartResourceDefinition resDef = PartResourceLibrary.Instance.GetDefinition(key);
                    if (resDef != null)
                    {
                        // convert to kg to be comparable with the corrected volumes from RF
                        rescaledOurs *= resDef.density * 1000d;
                        rescaledTheirs *= resDef.density * 1000d;
                    }
                    else
                    {
                        KCTDebug.Log($"Unable to find resource definition for {key}");
                    }
                }
                if (rescaledTheirs == 0) rescaledOurs *= 2;
                if (rescaledOurs == 0) rescaledTheirs *= 2;

                resTotal += (rescaledOurs + rescaledTheirs);
                resDiffs += Math.Abs(rescaledOurs - rescaledTheirs);
            }
            if (resTotal > 0)
                resFactor = 0.5d + ((resTotal - resDiffs) / resTotal) * 0.5d;

            _resourceKeys.Clear();

            return massFactor * sizeFactor * hrFactor * resFactor;
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
