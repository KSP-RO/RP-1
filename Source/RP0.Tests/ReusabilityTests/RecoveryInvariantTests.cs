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
        /// Which editor a vessel was built in does not affect refurbishment: the work is a
        /// share of the vessel's own build BP, which already reflects its complexity.
        /// </summary>
        [TestCaseSource(nameof(Cases))]
        public void SPHDoesNotChangeRefurbishment(VesselArchetype a, TechTier tier)
        {
            FormulaInputs v = a.At(tier);

            Assert.Multiple(() =>
            {
                Assert.That(Formula.RefurbishmentBP(v.With(isSPH: true)),
                            Is.EqualTo(Formula.RefurbishmentBP(v.With(isSPH: false))).Within(RelTolPercent).Percent, "BP");
                Assert.That(Formula.RefurbishmentCost(v.With(isSPH: true)),
                            Is.EqualTo(Formula.RefurbishmentCost(v.With(isSPH: false))).Within(RelTolPercent).Percent, "cost");
            });
        }

        /// <summary>
        /// Human rating applies a fixed multiplier to refurbishment BP, independent of every
        /// other input. Currently 1.5x.
        /// </summary>
        [TestCaseSource(nameof(Cases))]
        public void HumanRatingAppliesAFixedRefurbishmentMultiplier(VesselArchetype a, TechTier tier)
        {
            FormulaInputs v = a.At(tier);

            Assert.That(Formula.RefurbishmentBP(v.With(humanRated: true)),
                        Is.EqualTo(Formula.RefurbishmentBP(v.With(humanRated: false)) * 1.5d).Within(RelTolPercent).Percent);
        }

        /// <summary>
        /// Refurbishment work is a fixed share of build work for a vessel landed away from KSC,
        /// neither splashed nor human-rated, before any tech. Currently 10%.
        /// </summary>
        [TestCaseSource(nameof(Archetypes))]
        public void BaseRefurbishmentIsAFixedShareOfBuildBP(VesselArchetype a)
        {
            FormulaInputs v = a.At(TechTier.Base).With(humanRated: false, splashed: false, atKSC: false);

            Assert.That(Formula.RefurbishmentBP(v),
                        Is.EqualTo(v.BuildPoints * 0.10d).Within(RelTolPercent).Percent);
        }

        [TestCaseSource(nameof(Cases))]
        public void SplashdownAppliesThePenaltyScaledByTech(VesselArchetype a, TechTier tier)
        {
            FormulaInputs dry = a.At(tier).With(splashed: false, atKSC: false);
            FormulaInputs wet = dry.With(splashed: true);

            double expected = Formula.RefurbishmentBP(dry) * tier.Settings.SplashdownPenaltyMult;

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
            double dearEC = cheap.EffectiveCost * 1.1d;
            // Build BP derives from effective cost in game, so move them together.
            FormulaInputs dear = cheap.With(effectiveCost: dearEC, buildPoints: Formula.VesselBuildPoints(dearEC));

            Assert.Multiple(() =>
            {
                Assert.That(Formula.RefurbishmentBP(dear),
                            Is.GreaterThan(Formula.RefurbishmentBP(cheap)), "refurbishment BP");
                Assert.That(Formula.RefurbishmentCost(dear),
                            Is.GreaterThan(Formula.RefurbishmentCost(cheap)), "refurbishment cost");
                Assert.That(Formula.RolloutBP(dear),
                            Is.GreaterThan(Formula.RolloutBP(cheap)), "rollout BP");
            });
        }

        /// <summary>
        /// Running an engine longer must never make it cheaper to refurbish, and there is no
        /// point past which extra run time is free (the old run-time discount bottomed out at
        /// sqrt(1.5) x rated burn time).
        /// </summary>
        [Test]
        public void EngineWearNeverFallsWithRunTimeAndHasNoCap()
        {
            const double rated = 500d;
            double previous = Formula.EngineRefurbishmentWear(0d, rated);
            Assert.That(previous, Is.EqualTo(0d), "an unfired engine has no wear");

            for (double runTime = 1d; runTime <= 3d * rated; runTime += 1d)
            {
                double wear = Formula.EngineRefurbishmentWear(runTime, rated);
                Assert.That(wear, Is.GreaterThan(previous), $"wear at {runTime} s");
                previous = wear;
            }

            Assert.Multiple(() =>
            {
                Assert.That(Formula.EngineRefurbishmentWear(rated, rated), Is.EqualTo(0.5d).Within(AbsTol), "one rated burn");
                Assert.That(Formula.EngineRefurbishmentWear(10d, 0d), Is.EqualTo(0.5d).Within(AbsTol), "no rated burn time: one rated burn");
            });
        }

        [TestCaseSource(nameof(Cases))]
        public void RefurbishmentRisesWithEngineWear(VesselArchetype a, TechTier tier)
        {
            FormulaInputs fresh = a.At(tier).With(engineWearEC: 0d);
            FormulaInputs worn = fresh.With(engineWearEC: fresh.EffectiveCost * 0.2d);

            Assert.Multiple(() =>
            {
                Assert.That(Formula.RefurbishmentBP(worn), Is.GreaterThan(Formula.RefurbishmentBP(fresh)), "refurbishment BP");
                Assert.That(Formula.RefurbishmentCost(worn), Is.GreaterThan(Formula.RefurbishmentCost(fresh)), "refurbishment cost");
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

        /// <summary>
        /// A vessel's NET refurbishment cost - the charge plus the engineer salary paid over
        /// the refurbishment - should never rise with tech. Net is what a player actually
        /// spends, so this holds even if a future change trades charge against time.
        /// </summary>
        [TestCaseSource(nameof(Archetypes))]
        public void AdvancingTechNeverRaisesNetRefurbishmentCost(VesselArchetype a)
        {
            double bas = NetRefurbishmentCost(a.At(TechTier.Base));
            double mid = NetRefurbishmentCost(a.At(TechTier.Mid));
            double mod = NetRefurbishmentCost(a.At(TechTier.Modern));

            Assert.Multiple(() =>
            {
                Assert.That(mid, Is.LessThanOrEqualTo(bas), "Base -> Mid");
                Assert.That(mod, Is.LessThanOrEqualTo(mid), "Mid -> Modern");
            });
        }

        /// <summary>
        /// Reuse only makes sense if refurbishing is less work than building new: the PR frames
        /// the whole feature as giving the player that choice, which needs refurbishment BP to
        /// sit under the vessel's integration BP.
        /// </summary>
        [TestCaseSource(nameof(Cases))]
        public void RefurbishingIsCheaperThanBuildingNew(VesselArchetype a, TechTier tier)
        {
            FormulaInputs v = a.At(tier);
            double ratio = Formula.RefurbishmentBP(v) / v.BuildPoints;

            Assert.That(ratio, Is.LessThan(1d),
                        $"refurbishment BP is {ratio:N2}x integration BP - refurbishing should never " +
                        "cost more work than building the vessel from scratch");
        }

        /// <summary>
        /// The charge plus the engineer salary paid over the refurbishment. The salary term
        /// is bp / EngineerBPRate engineer-seconds of work, priced at the annual salary, and is
        /// independent of how many engineers are assigned (more engineers finish proportionally
        /// sooner).
        /// </summary>
        private static double NetRefurbishmentCost(in FormulaInputs v)
        {
            const double secondsPerYear = 365.25d * Staffing.SecondsPerDay;
            double salaryCost = Formula.RefurbishmentBP(v) * TechTier.SalaryEngineers
                                / (secondsPerYear * Staffing.EngineerBPRate);
            return Formula.RefurbishmentCost(v) + salaryCost;
        }
    }
}
