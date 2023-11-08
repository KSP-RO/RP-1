using System.Collections.Generic;
using System;
using ROUtils;

namespace RP0
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

        public static double GetConstructionBP(double cost, double oldCost, SpaceCenterFacility facilityType)
        {
            double bp = Math.Sqrt(cost + oldCost) - Math.Sqrt(oldCost);
            // Facility downgrades are handled here. LC reconstructions use their own logic.
            if (bp < 0)
                bp *= -0.5d;

            const double minDays = 3d;
            return Math.Max(bp, minDays);
        }

        public static double GetVesselBuildRate(int index, LaunchComplex LC, bool isHumanRatedCapped, int persDelta)
        {
            if (index > 0 || !LC.IsOperational)
                return 0d;

            //N = num upgrades, I = rate index, L = VAB/SPH upgrade level, R = R&D level
            int personnel = Math.Max(0, LC.Engineers + persDelta);
            if (isHumanRatedCapped)
                personnel = Math.Min(personnel, LC.MaxEngineersNonHR);

            return personnel * _EngineerBPRate * HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().BuildRate;
        }

        public static double GetConstructionBuildRate(int index, LCSpaceCenter KSC, SpaceCenterFacility facilityType)
        {
            double rate = 1d / 86400d;
            TransactionReasonsRP0 reason = facilityType == SpaceCenterFacility.LaunchPad ? TransactionReasonsRP0.StructureConstructionLC : TransactionReasonsRP0.StructureConstruction;
            return rate * CurrencyUtils.Rate(reason) * HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().BuildRate;
        }

        public static double GetResearchRate(double ScienceValue, int index, int upgradeDelta)
        {
            int Personnel = SpaceCenterManagement.Instance.Researchers + upgradeDelta;
            
            if (index > 0)
                return 0d;

            double rate = Personnel > 0 ? 0.5 + Personnel * 0.05d : 0.001d;
            const double yearToSec = 1d / (86400d * 365d);
            return rate * yearToSec * HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().ResearchRate;
        }

        public static double GetScienceResearchEfficiencyMult(double totalSci)
        {
            return (double)Database.SettingsSC.ScienceResearchEfficiency.Evaluate((float)totalSci);
        }

        public static double GetVesselBuildPoints(double totalEffectiveCost)
        {
            double bpScalar = UtilMath.Clamp((totalEffectiveCost - 200d) / 4000d, 0.5d, 1d);
            double finalBP = 1000d + Math.Pow(totalEffectiveCost, 1.1d) * 100d * bpScalar;

            RP0Debug.Log($"BP: {finalBP}");
            return finalBP;
        }

        public static double GetRolloutCost(VesselProject vessel)
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return 0;

            LaunchComplex vLC = vessel.LC;
            if (vLC == null)
                vLC = SpaceCenterManagement.Instance.ActiveSC.ActiveLC;

            double multHR = 1d;
            if (vLC.IsHumanRated)
                multHR += 0.25d;
            if (vessel.humanRated)
                multHR += 0.75d;
            double vesselPortion = (vessel.effectiveCost - (vessel.cost * 0.9d)) * 0.6;
            double massToUse = vLC.LCType == LaunchComplexType.Pad ? vLC.MassMax : vessel.GetTotalMass();
            double lcPortion = Math.Pow(massToUse, 0.75d) * 20d * multHR;
            double result = vesselPortion + lcPortion;
            result = result * _RolloutCostBasePortion + Math.Max(0d, result * _RolloutCostSubsidyPortion - GetRolloutBP(vessel) * Database.SettingsSC.salaryEngineers / (365.25d * 86400d * _EngineerBPRate));
            return result * 0.5d;
        }

        public static double GetAirlaunchCost(VesselProject vessel)
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return 0;

            double result = vessel.effectiveCost * 0.25d;
            result = result * _RolloutCostBasePortion + Math.Max(0d, result * _RolloutCostSubsidyPortion - GetAirlaunchBP(vessel) * Database.SettingsSC.salaryEngineers / (365.25d * 86400d * _EngineerBPRate));
            return result * 0.5d;
        }

        public static double GetAirlaunchBP(VesselProject vessel)
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return 0;

            return (vessel.effectiveCost - (vessel.cost * 0.5d)) * 12d;
        }

        public static double GetEngineRefurbBPMultiplier(double runTime, double ratedBurnTime)
        {
            double runFactor = 1d - runTime * 0.1d;
            if (runFactor < 0d)
                runFactor = 0d;
            double retVal = 0.5d * (1d + runFactor);

            if (ratedBurnTime > 0d && runFactor == 0d)
            {
                double ratio = runTime / ratedBurnTime;
                if (ratio > 1d)
                    ratio *= ratio;
                retVal *= 1d - Math.Min(0.75d, ratio * 0.5d);
            }
            return retVal;
        }

        public static double GetRolloutBP(VesselProject vessel)
        {
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

        public static double GetReconditioningBP(VesselProject vessel)
        {
            return vessel.buildPoints * 0.01d + Math.Max(1, vessel.GetTotalMass() - 20d) * 2000d;
        }

        public static double GetRecoveryBPSPH(VesselProject vessel)
        {
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

        public static double GetRecoveryBPVAB(VesselProject vessel)
        {
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
            const double amountMultiplier = 10d;
            const double tankMultiplier = 0.17d;
            const double baseTankCostPerL = 0.55d;
            const double rfTankCostPerLMultiplier = 10d;
            const double modifyMultiplier = 0.6d;

            LCResourceType ignoredResType = type == LaunchComplexType.Hangar ? LCResourceType.HangarIgnore : LCResourceType.PadIgnore;
            var rType = Database.ResourceInfo.LCResourceTypes.ValueOrDefault(res);
            if ((rType & ignoredResType) != 0 || (rType & LCResourceType.Fuel) == 0)
                return 0d;

            if (def.tankList.TryGetValue(res, out var tank) && PartResourceLibrary.Instance.GetDefinition(res) is PartResourceDefinition resDef)
            {
                double tankVol = amount / tank.utilization;
                double cost = (baseTankCostPerL + tank.cost * rfTankCostPerLMultiplier) * tankVol * tankMultiplier + amount * resDef.unitCost * amountMultiplier;
                if (Database.SettingsSC.Resource_Variables.TryGetValue(res, out double mult))
                    cost *= mult;

                if (isModify)
                    cost *= modifyMultiplier;

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
            if (ourStats.lcType == LaunchComplexType.Hangar)
                return 1d;

            if (otherStats.lcType != ourStats.lcType)
                return 0d;

            if (ourStats.Compare(otherStats))
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
                var rType = Database.ResourceInfo.LCResourceTypes.ValueOrDefault(key);
                if ((rType & LCResourceType.PadIgnore) != 0 || (rType & LCResourceType.Fuel) == 0)
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
                        RP0Debug.Log($"Unable to find resource definition for {key}");
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
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
