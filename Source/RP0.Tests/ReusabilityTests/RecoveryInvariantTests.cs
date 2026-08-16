using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RP0.Tests.ReusabilityTests.Config;
using RP0.Tests.ReusabilityTests.Fixtures;

namespace RP0.Tests.ReusabilityTests
{
    /// <summary>
    /// The relationships the recovery/refurbishment formulas are supposed to hold, encoded so
    /// a change that stays numerically plausible but breaks the intent still fails. These are
    /// structural: they say nothing about whether the absolute numbers are well balanced -
    /// that is what the tables and BalanceAnchorTests are for.
    /// </summary>
    [TestFixture]
    public class RecoveryInvariantTests
    {
        /// <summary>
        /// Relative tolerance, as a percentage, for the exact-multiplier assertions. These
        /// compare products taken in different orders on values up to ~1e8, where one ULP is
        /// already ~1.5e-8, so an absolute tolerance is meaningless; 1e-9 percent is 1e-11
        /// relative, still five orders tighter than any real change to a multiplier.
        /// </summary>
        private const double RelTolPercent = 1e-9;

        /// <summary>Absolute tolerance, for comparisons against exact zero.</summary>
        private const double AbsTol = 1e-9;

        public static IEnumerable<TestCaseData> Cases =>
            from a in Vessels.All
            from t in TechTier.All
            select new TestCaseData(a, t).SetName($"{{m}}({a.Name}, {t.Name})");

        public static IEnumerable<VesselArchetype> Archetypes => Vessels.All;
        public static IEnumerable<TechTier> Tiers => TechTier.All;

        // ---- well-formedness -----------------------------------------------------------

        [TestCaseSource(nameof(Cases))]
        public void AllOutputsAreFiniteAndNonNegative(VesselArchetype a, TechTier tier)
        {
            FormulaInputs v = a.At(tier);

            Assert.Multiple(() =>
            {
                AssertSane(Formula.RolloutBP(v), "rollout BP");
                AssertSane(Formula.RolloutCost(v), "rollout cost");
                AssertSane(Formula.RefurbishmentBP(v), "refurbishment BP");
                AssertSane(Formula.RefurbishmentCost(v), "refurbishment cost");
                AssertSane(Formula.RecoveryBP(v), "recovery BP");
                AssertSane(Formula.RecoveryCost(v), "recovery cost");
                AssertSane(v.BuildPoints, "build points");
            });
        }

        private static void AssertSane(double value, string what)
        {
            Assert.That(value, Is.Not.NaN, what);
            Assert.That(double.IsInfinity(value), Is.False, what);
            Assert.That(value, Is.GreaterThanOrEqualTo(0d), what);
        }

        // ---- structural multipliers ----------------------------------------------------

        /// <summary>
        /// An SPH vessel refurbishes at a fixed multiple of the same vessel treated as VAB,
        /// independent of every other input. Currently 2.15x.
        /// </summary>
        [TestCaseSource(nameof(Cases))]
        public void SPHRefurbishmentIsAFixedMultipleOfVAB(VesselArchetype a, TechTier tier)
        {
            FormulaInputs v = a.At(tier);

            Assert.That(Formula.RefurbishmentBP(v.With(isSPH: true)),
                        Is.EqualTo(Formula.RefurbishmentBP(v.With(isSPH: false)) * 2.15d).Within(RelTolPercent).Percent);
        }

        /// <summary>
        /// Human rating applies a fixed multiplier to refurbishment BP, independent of every
        /// other input. Currently 3x.
        /// </summary>
        [TestCaseSource(nameof(Cases))]
        public void HumanRatingAppliesAFixedRefurbishmentMultiplier(VesselArchetype a, TechTier tier)
        {
            FormulaInputs v = a.At(tier);

            Assert.That(Formula.RefurbishmentBP(v.With(humanRated: true)),
                        Is.EqualTo(Formula.RefurbishmentBP(v.With(humanRated: false)) * 3d).Within(RelTolPercent).Percent);
        }

        [TestCaseSource(nameof(Cases))]
        public void SplashdownAppliesThePenaltyScaledByTech(VesselArchetype a, TechTier tier)
        {
            FormulaInputs dry = a.At(tier).With(splashed: false, atKSC: false);
            FormulaInputs wet = dry.With(splashed: true);

            double expected = Formula.RefurbishmentBP(dry) * 1.5d * tier.Settings.SplashdownPenaltyMult;

            Assert.That(Formula.RefurbishmentBP(wet), Is.EqualTo(expected).Within(RelTolPercent).Percent);
        }

        /// <summary>
        /// Coming down at KSC discounts refurbishment by a fixed factor. Currently 0.8x.
        /// </summary>
        [TestCaseSource(nameof(Cases))]
        public void RecoveringAtKSCDiscountsRefurbishment(VesselArchetype a, TechTier tier)
        {
            FormulaInputs away = a.At(tier).With(splashed: false, atKSC: false);
            FormulaInputs home = away.With(atKSC: true);

            Assert.That(Formula.RefurbishmentBP(home),
                        Is.EqualTo(Formula.RefurbishmentBP(away) * 0.8d).Within(RelTolPercent).Percent);
        }

        [TestCaseSource(nameof(Cases))]
        public void RecoveryBPIsTheDistanceFromKSC(VesselArchetype a, TechTier tier)
        {
            FormulaInputs v = a.At(tier);

            Assert.That(Formula.RecoveryBP(v), Is.EqualTo((double)v.KscDistance).Within(AbsTol));
        }

        [Test]
        public void RecoveringAtKSCCostsNothingToTransport()
        {
            // Recovery cost is mass * distance * rate, so a vessel already at KSC is free to
            // recover however heavy it is - all of the reuse cost lands in refurbishment.
            FormulaInputs v = Vessels.BoosterRTLS.At(TechTier.Base);

            Assert.That(Formula.RecoveryCost(v), Is.EqualTo(0d).Within(AbsTol));
        }

        // ---- monotonicity --------------------------------------------------------------

        [TestCaseSource(nameof(Cases))]
        public void RefurbishmentRisesWithEffectiveCost(VesselArchetype a, TechTier tier)
        {
            FormulaInputs cheap = a.At(tier);
            FormulaInputs dear = cheap.With(effectiveCost: cheap.EffectiveCost * 1.1d);

            Assert.Multiple(() =>
            {
                Assert.That(Formula.RefurbishmentBP(dear),
                            Is.GreaterThan(Formula.RefurbishmentBP(cheap)), "refurbishment BP");
                Assert.That(Formula.RolloutBP(dear),
                            Is.GreaterThan(Formula.RolloutBP(cheap)), "rollout BP");
            });
        }

        [TestCaseSource(nameof(Cases))]
        public void RecoveryCostRisesWithMassAndDistance(VesselArchetype a, TechTier tier)
        {
            FormulaInputs v = a.At(tier).With(mass: 100f, kscDistance: 100_000f);

            Assert.Multiple(() =>
            {
                Assert.That(Formula.RecoveryCost(v.With(mass: 200f)),
                            Is.GreaterThan(Formula.RecoveryCost(v)), "mass");
                Assert.That(Formula.RecoveryCost(v.With(kscDistance: 200_000f)),
                            Is.GreaterThan(Formula.RecoveryCost(v)), "distance");
            });
        }

        [TestCaseSource(nameof(Archetypes))]
        public void RolloutCostRisesWithPadSize(VesselArchetype a)
        {
            FormulaInputs v = a.At(TechTier.Base).With(lcIsPad: true, lcMassMax: 100f);

            Assert.That(Formula.RolloutCost(v.With(lcMassMax: 400f)),
                        Is.GreaterThanOrEqualTo(Formula.RolloutCost(v)));
        }

        // ---- tech progression ----------------------------------------------------------

        [TestCaseSource(nameof(Archetypes))]
        public void AdvancingTechNeverSlowsRefurbishment(VesselArchetype a)
        {
            double baseBP = Formula.RefurbishmentBP(a.At(TechTier.Base));
            double midBP = Formula.RefurbishmentBP(a.At(TechTier.Mid));
            double modernBP = Formula.RefurbishmentBP(a.At(TechTier.Modern));

            Assert.Multiple(() =>
            {
                Assert.That(midBP, Is.LessThanOrEqualTo(baseBP), "Base -> Mid");
                Assert.That(modernBP, Is.LessThanOrEqualTo(midBP), "Mid -> Modern");
            });
        }

        [TestCaseSource(nameof(Archetypes))]
        public void AdvancingTechNeverRaisesRecoveryCostOrTime(VesselArchetype a)
        {
            Assert.Multiple(() =>
            {
                Assert.That(Formula.RecoveryCost(a.At(TechTier.Mid)),
                            Is.LessThanOrEqualTo(Formula.RecoveryCost(a.At(TechTier.Base))), "cost Base -> Mid");
                Assert.That(Formula.RecoveryCost(a.At(TechTier.Modern)),
                            Is.LessThanOrEqualTo(Formula.RecoveryCost(a.At(TechTier.Mid))), "cost Mid -> Modern");

                Assert.That(Staffing.RecoveryDays(a.At(TechTier.Mid)),
                            Is.LessThanOrEqualTo(Staffing.RecoveryDays(a.At(TechTier.Base))), "time Base -> Mid");
                Assert.That(Staffing.RecoveryDays(a.At(TechTier.Modern)),
                            Is.LessThanOrEqualTo(Staffing.RecoveryDays(a.At(TechTier.Mid))), "time Mid -> Modern");
            });
        }

        // Refurbishment COST is deliberately not asserted monotonic here - it is not, and that
        // is a balance defect rather than a mechanical property. See
        // BalanceAnchorTests.AdvancingTechNeverRaisesRefurbishmentCost.
    }
}
