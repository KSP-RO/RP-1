using System;

// The arithmetic half of Formula, kept free of KSP, Unity, ROUtils and RP-1 singleton
// references so the recovery/refurbishment math can be exercised against the plain
// FormulaInputs struct rather than a fully-constructed VesselProject. Compiled into RP0.dll
// as part of the Formula partial class. Keep it dependency-free: the VesselProject-facing
// wrappers in Formula.cs are where singleton and KSP-type access belongs.

namespace RP0
{
    /// <summary>
    /// The tech- and settings-driven multipliers that the recovery/refurbishment formulas read.
    /// In game these come from Database.SettingsSC and Database.SettingsRecovery; see
    /// Formula.CurrentSettings(). Tests build them by parsing the shipped SCMData cfgs.
    /// </summary>
    public readonly struct RecoverySettings
    {
        /// <summary>Database.SettingsSC.salaryEngineers.</summary>
        public readonly double SalaryEngineers;
        /// <summary>Recovery transit speed in m/s. Recovery BP is a distance, so this is a rate, not a divisor.</summary>
        public readonly double RecoveryRateMult;
        /// <summary>Funds per tonne-metre of recovery transit.</summary>
        public readonly double RecoveryCostMult;
        /// <summary>Higher = faster refurbishment. Divides refurbishment BP.</summary>
        public readonly double RefurbishmentRateMult;
        /// <summary>Scales refurbishment cost before the engineer subsidy is applied.</summary>
        public readonly double RefurbishmentCostMult;
        /// <summary>Scales the 1.5x splashdown penalty. 1.0 = full penalty, 0.0 = none.</summary>
        public readonly double SplashdownPenaltyMult;

        public RecoverySettings(double salaryEngineers,
                                double recoveryRateMult,
                                double recoveryCostMult,
                                double refurbishmentRateMult,
                                double refurbishmentCostMult,
                                double splashdownPenaltyMult)
        {
            SalaryEngineers = salaryEngineers;
            RecoveryRateMult = recoveryRateMult;
            RecoveryCostMult = recoveryCostMult;
            RefurbishmentRateMult = refurbishmentRateMult;
            RefurbishmentCostMult = refurbishmentCostMult;
            SplashdownPenaltyMult = splashdownPenaltyMult;
        }

        public RecoverySettings With(double? salaryEngineers = null,
                                     double? recoveryRateMult = null,
                                     double? recoveryCostMult = null,
                                     double? refurbishmentRateMult = null,
                                     double? refurbishmentCostMult = null,
                                     double? splashdownPenaltyMult = null)
        {
            return new RecoverySettings(
                salaryEngineers ?? SalaryEngineers,
                recoveryRateMult ?? RecoveryRateMult,
                recoveryCostMult ?? RecoveryCostMult,
                refurbishmentRateMult ?? RefurbishmentRateMult,
                refurbishmentCostMult ?? RefurbishmentCostMult,
                splashdownPenaltyMult ?? SplashdownPenaltyMult);
        }
    }

    /// <summary>
    /// Everything the vessel-cost formulas read off a VesselProject and its LaunchComplex,
    /// flattened to scalars. Built in game by Formula.Inputs(VesselProject).
    ///
    /// Field types deliberately mirror VesselProject/LCData exactly (cost, mass, kscDistance
    /// and massMax are float there, effectiveCost and buildPoints are double). Widening a
    /// float to double is exact, but float*float is not double*double, and RecoveryCost
    /// multiplies mass by kscDistance - so keeping the original widths makes these functions
    /// bit-identical to the pre-extraction code rather than merely close.
    /// </summary>
    public readonly struct FormulaInputs
    {
        // --- vessel ---
        public readonly double EffectiveCost;
        public readonly float Cost;
        public readonly double BuildPoints;
        public readonly float Mass;
        /// <summary>Great-circle distance from KSC in metres. Doubles as recovery BP.</summary>
        public readonly float KscDistance;
        public readonly bool HumanRated;
        /// <summary>VesselProject.Type == ProjectType.SPH.</summary>
        public readonly bool IsSPH;
        /// <summary>LandedAt contains "Splashdown".</summary>
        public readonly bool Splashed;
        /// <summary>LandedAt contains "Runway" or "Launchpad".</summary>
        public readonly bool AtKSC;

        // --- launch complex ---
        public readonly bool LCIsHumanRated;
        /// <summary>LCType == LaunchComplexType.Pad (false means Hangar).</summary>
        public readonly bool LCIsPad;
        public readonly float LCMassMax;

        // --- settings ---
        public readonly RecoverySettings Settings;

        public FormulaInputs(double effectiveCost,
                             float cost,
                             double buildPoints,
                             float mass,
                             float kscDistance,
                             bool humanRated,
                             bool isSPH,
                             bool splashed,
                             bool atKSC,
                             bool lcIsHumanRated,
                             bool lcIsPad,
                             float lcMassMax,
                             RecoverySettings settings)
        {
            EffectiveCost = effectiveCost;
            Cost = cost;
            BuildPoints = buildPoints;
            Mass = mass;
            KscDistance = kscDistance;
            HumanRated = humanRated;
            IsSPH = isSPH;
            Splashed = splashed;
            AtKSC = atKSC;
            LCIsHumanRated = lcIsHumanRated;
            LCIsPad = lcIsPad;
            LCMassMax = lcMassMax;
            Settings = settings;
        }

        /// <summary>
        /// Copy with individual fields replaced. Exists so tests can vary exactly one input
        /// at a time (the monotonicity and structural-multiplier assertions depend on that).
        /// </summary>
        public FormulaInputs With(double? effectiveCost = null,
                                  float? cost = null,
                                  double? buildPoints = null,
                                  float? mass = null,
                                  float? kscDistance = null,
                                  bool? humanRated = null,
                                  bool? isSPH = null,
                                  bool? splashed = null,
                                  bool? atKSC = null,
                                  bool? lcIsHumanRated = null,
                                  bool? lcIsPad = null,
                                  float? lcMassMax = null,
                                  RecoverySettings? settings = null)
        {
            return new FormulaInputs(
                effectiveCost ?? EffectiveCost,
                cost ?? Cost,
                buildPoints ?? BuildPoints,
                mass ?? Mass,
                kscDistance ?? KscDistance,
                humanRated ?? HumanRated,
                isSPH ?? IsSPH,
                splashed ?? Splashed,
                atKSC ?? AtKSC,
                lcIsHumanRated ?? LCIsHumanRated,
                lcIsPad ?? LCIsPad,
                lcMassMax ?? LCMassMax,
                settings ?? Settings);
        }
    }

    /// <summary>
    /// The KSP-free half of Formula: the arithmetic, with every input passed in.
    /// The VesselProject-taking Get* wrappers live in Formula.cs.
    /// </summary>
    public partial class Formula
    {
        internal const double _EngineerBPRate = 0.0025d;
        internal const double _RolloutCostBasePortion = 0.5d;
        internal const double _RolloutCostSubsidyPortion = 1d - _RolloutCostBasePortion;

        /// <summary>
        /// Seconds of engineer-time per year, times the BP rate: the divisor that converts
        /// a BP figure into the funds of engineer salary it represents. Kept as a divisor
        /// rather than a precomputed reciprocal so the arithmetic stays bit-identical to
        /// the original inline expressions.
        /// </summary>
        private const double _SalaryBPDivisor = 365.25d * 86400d * _EngineerBPRate;

        public static double VesselBuildPoints(double totalEffectiveCost)
        {
            // Equivalent to UtilMath.Clamp(x, 0.5d, 1d); inlined to keep this file KSP-free.
            double bpScalar = (totalEffectiveCost - 200d) / 4000d;
            if (bpScalar < 0.5d) bpScalar = 0.5d;
            else if (bpScalar > 1d) bpScalar = 1d;

            return 1000d + Math.Pow(totalEffectiveCost, 1.1d) * 100d * bpScalar;
        }

        public static double RolloutBP(in FormulaInputs v)
        {
            double costDeltaHighPow;
            double costDelta = v.EffectiveCost - v.Cost;
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

        public static double RolloutCost(in FormulaInputs v)
        {
            double multHR = 1d;
            if (v.LCIsHumanRated)
                multHR += 0.25d;
            if (v.HumanRated)
                multHR += 0.75d;
            double vesselPortion = (v.EffectiveCost - (v.Cost * 0.9d)) * 0.6;
            double massToUse = v.LCIsPad ? v.LCMassMax : v.Mass;
            double lcPortion = Math.Pow(massToUse, 0.75d) * 20d * multHR;
            double result = vesselPortion + lcPortion;
            result = result * _RolloutCostBasePortion
                   + Math.Max(0d, result * _RolloutCostSubsidyPortion - RolloutBP(v) * v.Settings.SalaryEngineers / _SalaryBPDivisor);
            return result * 0.5d;
        }

        public static double ReconditioningBP(in FormulaInputs v)
        {
            return v.BuildPoints * 0.01d + Math.Max(1, v.Mass - 20d) * 2000d;
        }

        public static double VesselRepairBP(in FormulaInputs v)
        {
            return RolloutBP(v) / 7.5;
        }

        /// <summary>
        /// Recovery BP is the distance the vessel has to be hauled back to KSC, in metres.
        /// It is consumed at RecoveryRateMult m/s rather than at an engineer BP rate.
        /// </summary>
        public static double RecoveryBP(in FormulaInputs v)
        {
            return v.KscDistance;
        }

        /// <summary>
        /// Recovery cost scales with distance from KSC and with mass (a proxy for volume).
        /// Not subsidised by engineers - recovery is a non-engineer step.
        /// </summary>
        public static double RecoveryCost(in FormulaInputs v)
        {
            return v.Mass * v.KscDistance * v.Settings.RecoveryCostMult;
        }

        /// <summary>
        /// Refurbishment BP: corresponds to prior recovery BP.
        /// Penalises splashdown recoveries and human-rated craft.
        /// Tech progression reduces BP via RefurbishmentRateMult (higher = faster).
        ///
        /// Design anchors:
        ///   STS orbiter (human-rated, reentry, runway): ~6 months early, ~3 months mature.
        ///   F9 booster (non-human, propulsive landing): ~3 months 2016, ~30 days 2020+.
        /// </summary>
        public static double RefurbishmentBP(in FormulaInputs v)
        {
            double bp = RolloutBP(v) * (v.IsSPH ? 2.15 : 1);
            // Respect SPH being more expensive to recover for some reason?

            // Human-rated craft require far more rigorous inspection and certification
            if (v.HumanRated)
                bp *= 3.0d;

            // Splashdown: saltwater exposure, drying, decontamination.
            // SplashdownPenaltyMult is reduced by materials science techs via SCMREFURBTECHS.
            // 1.0 = full 1.5x penalty; 0.5 = halved penalty; 0.0 = no penalty.
            if (v.Splashed)
                bp *= 1.5d * v.Settings.SplashdownPenaltyMult;

            if (v.AtKSC)
                bp *= 0.8d; // Discount for skipping "transit overhead"?

            // Apply tech-driven rate base: higher value = less BP = faster refurbishment
            bp /= v.Settings.RefurbishmentRateMult;

            return bp;
        }

        /// <summary>
        /// Refurbishment cost: between reconditioning (0.2x rollout) and a full rollout (~40%).
        /// Uses the same engineer-subsidy pattern as other ops costs.
        /// Reduced by tech via RefurbishmentCostMult.
        /// </summary>
        public static double RefurbishmentCost(in FormulaInputs v)
        {
            double bp = RefurbishmentBP(v);
            double result = RolloutCost(v) * v.Settings.RefurbishmentCostMult;
            result = result * _RolloutCostBasePortion
                   + Math.Max(0d, result * _RolloutCostSubsidyPortion - bp * v.Settings.SalaryEngineers / _SalaryBPDivisor);
            return result * 0.5d;
        }
    }
}
