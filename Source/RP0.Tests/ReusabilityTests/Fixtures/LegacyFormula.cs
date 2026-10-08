using System;

namespace RP0.Tests.ReusabilityTests.Fixtures
{
    /// <summary>
    /// The pre-#2825 recovery model, reproduced here so the tables can show before/after.
    /// It is deliberately NOT kept in production code - master's Formula.GetRecoveryBPSPH /
    /// GetRecoveryBPVAB and the distance/runway adjustment ReconRolloutProject applied to
    /// them are deleted by the PR, and should stay deleted.
    ///
    /// Master, Formula.cs:
    ///     GetRecoveryBPVAB(v) == GetRolloutBP(v)
    ///     GetRecoveryBPSPH(v) == GetRolloutBP(v) * 2.15
    /// Master, ReconRolloutProject.InitRecovery:
    ///     BP += BP * (kscDistance / (cb.Radius * PI))
    ///     BP *= LandedAt.Contains("Runway") ? 0.75 : 1
    /// and the result was consumed at the LC engineer build rate, unlike the PR's recovery
    /// phase which runs at a fixed m/s transit rate.
    /// </summary>
    public static class LegacyFormula
    {
        /// <summary>RSS Earth radius in metres; SpaceCenter.Instance.cb.Radius in game.</summary>
        public const double EarthRadius = 6_371_000d;

        public static double MaxDistance => EarthRadius * Math.PI;

        public static double RecoveryBP(in FormulaInputs v, string landedAt)
        {
            double bp = Formula.RolloutBP(v) * (v.IsSPH ? 2.15d : 1d);
            bp += bp * (v.KscDistance / MaxDistance);
            if (landedAt != null && landedAt.Contains("Runway"))
                bp *= 0.75d;
            return bp;
        }
    }
}
