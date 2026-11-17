using System;

namespace RP0.Tests.ReusabilityTests.Fixtures
{
    /// <summary>
    /// Converts build points into durations. This is presentation logic for the tables, not
    /// something FormulaCore is responsible for, but it mirrors the game's rules exactly:
    /// LaunchComplex.RawMaxEngineers / MaxEngineersCalc for staffing, and
    /// Formula.GetVesselBuildRate's engineers * _EngineerBPRate * BuildRate for the rate.
    /// </summary>
    public static class Staffing
    {
        /// <summary>LaunchComplex.EngineersPerPacket.</summary>
        public const int EngineersPerPacket = 10;
        /// <summary>LaunchComplex.MinEngineersConst.</summary>
        public const int MinEngineers = 1;
        /// <summary>Formula._EngineerBPRate.</summary>
        public const double EngineerBPRate = 0.0025d;

        /// <summary>
        /// RP0Settings.BuildRate at Normal difficulty. Held at 1.0 so the tables isolate the
        /// recovery/refurbishment changes rather than difficulty scaling.
        /// </summary>
        public const double BuildRateSetting = 1.0d;

        public const double SecondsPerDay = 86400d;

        /// <summary>
        /// LaunchComplex.MaxEngineers: the fully staffed complex. Ops projects can be assigned
        /// fewer, so every duration in the tables is a best case.
        /// </summary>
        public static int MaxEngineers(in FormulaInputs v, double hangarSizeSqrMagnitude)
        {
            // LaunchComplex.RawMaxEngineers keys on massMax == float.MaxValue, not on LCType.
            double raw = v.LCMassMax != float.MaxValue
                ? Math.Pow(v.LCMassMax, 0.75d)
                : hangarSizeSqrMagnitude * 0.01d;

            double scaled = raw * (v.LCIsHumanRated ? 1.5d : 1d) * EngineersPerPacket;
            return Math.Max(MinEngineers, (int)Math.Ceiling(scaled));
        }

        /// <summary>BP per second at the given staffing.</summary>
        public static double BuildRate(int engineers) => engineers * EngineerBPRate * BuildRateSetting;

        public static double DaysAt(double bp, int engineers)
        {
            double rate = BuildRate(engineers);
            return rate <= 0d ? double.PositiveInfinity : bp / rate / SecondsPerDay;
        }

        /// <summary>
        /// The PR's recovery phase is not engineer-driven: BP is a distance in metres and
        /// RecoveryRateMult is a transit speed in m/s.
        /// </summary>
        public static double RecoveryDays(in FormulaInputs v)
        {
            double rate = v.Settings.RecoveryRateMult;
            if (rate <= 0d)
                return double.PositiveInfinity;
            return Formula.RecoveryBP(v) / rate / SecondsPerDay;
        }
    }
}
