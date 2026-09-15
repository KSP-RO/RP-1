using NUnit.Framework;
using RP0.Tests.ReusabilityTests.Config;
using RP0.Tests.ReusabilityTests.Fixtures;

namespace RP0.Tests.ReusabilityTests
{
    /// <summary>
    /// Guards the extraction of the arithmetic out of Formula's VesselProject-taking methods
    /// into FormulaCore.cs. Expected values were computed independently from the
    /// pre-extraction expressions, not by running this code, so a transcription slip during
    /// the move fails here.
    ///
    /// Canonical input: effectiveCost 50000, cost 20000, mass 100 t, kscDistance 500 km,
    /// non-human-rated vessel on a non-human-rated 120 t pad, salary 1000, all tech
    /// multipliers at 1 except RecoveryCostMult at 1e-6.
    /// </summary>
    [TestFixture]
    public class FormulaEquivalenceTests
    {
        private const double Tolerance = 1e-9;

        private static FormulaInputs Canonical()
        {
            var settings = new RecoverySettings(
                salaryEngineers: 1000d,
                recoveryRateMult: 1d,
                recoveryCostMult: 1e-6d,
                refurbishmentRateMult: 1d,
                refurbishmentCostMult: 1d,
                splashdownPenaltyMult: 1d);

            return new FormulaInputs(
                effectiveCost: 50000d,
                cost: 20000f,
                buildPoints: Formula.VesselBuildPoints(50000d),
                mass: 100f,
                kscDistance: 500000f,
                humanRated: false,
                isSPH: false,
                splashed: false,
                atKSC: false,
                lcIsHumanRated: false,
                lcIsPad: true,
                lcMassMax: 120f,
                settings: settings);
        }

        [Test]
        public void VesselBuildPoints_MatchesPreExtractionValue()
        {
            Assert.That(Formula.VesselBuildPoints(50000d),
                        Is.EqualTo(14753546.926684607d).Within(Tolerance));
        }

        [Test]
        public void RolloutBP_MatchesPreExtractionValue()
        {
            Assert.That(Formula.RolloutBP(Canonical()),
                        Is.EqualTo(1240386.558490759d).Within(Tolerance));
        }

        [Test]
        public void RolloutCost_MatchesPreExtractionValue()
        {
            Assert.That(Formula.RolloutCost(Canonical()),
                        Is.EqualTo(4981.282523841406d).Within(Tolerance));
        }

        [Test]
        public void ReconditioningBP_MatchesPreExtractionValue()
        {
            Assert.That(Formula.ReconditioningBP(Canonical()),
                        Is.EqualTo(307535.4692668461d).Within(Tolerance));
        }

        [Test]
        public void VesselRepairBP_MatchesPreExtractionValue()
        {
            Assert.That(Formula.VesselRepairBP(Canonical()),
                        Is.EqualTo(165384.87446543452d).Within(Tolerance));
        }

        [Test]
        public void RecoveryCost_MatchesPreExtractionValue()
        {
            // mass and kscDistance are float in VesselProject, so the game computes
            // (float * float) * double. FormulaInputs keeps those widths for this reason.
            Assert.That(Formula.RecoveryCost(Canonical()),
                        Is.EqualTo(50d).Within(Tolerance));
        }

        [Test]
        public void LegacyRecoveryBP_MatchesMasterFormula()
        {
            FormulaInputs v = Canonical().With(kscDistance: 0f);

            Assert.Multiple(() =>
            {
                Assert.That(LegacyFormula.RecoveryBP(v, "Splashdown"),
                            Is.EqualTo(1240386.558490759d).Within(Tolerance), "VAB");
                Assert.That(LegacyFormula.RecoveryBP(v.With(isSPH: true), "Splashdown"),
                            Is.EqualTo(2666831.100755132d).Within(Tolerance), "SPH");
            });
        }

        [Test]
        public void VesselBuildPoints_ClampsBpScalarAtBothEnds()
        {
            // bpScalar = clamp((ec - 200) / 4000, 0.5, 1). Low end binds below ec = 2200,
            // high end at and above ec = 4200.
            Assert.Multiple(() =>
            {
                Assert.That(Formula.VesselBuildPoints(200d),
                            Is.EqualTo(1000d + System.Math.Pow(200d, 1.1d) * 100d * 0.5d).Within(Tolerance),
                            "low clamp");
                Assert.That(Formula.VesselBuildPoints(10000d),
                            Is.EqualTo(1000d + System.Math.Pow(10000d, 1.1d) * 100d * 1d).Within(Tolerance),
                            "high clamp");
            });
        }
    }
}
