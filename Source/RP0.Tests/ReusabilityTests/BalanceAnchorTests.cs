using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RP0.Tests.ReusabilityTests.Config;
using RP0.Tests.ReusabilityTests.Fixtures;

namespace RP0.Tests.ReusabilityTests
{
    /// <summary>
    /// Structural properties the reuse system needs in order to do what PR #2825 says it is
    /// for - giving the player a real choice about whether to invest in reuse.
    ///
    /// Unlike RecoveryInvariantTests - which check relationships the formulas do hold - these
    /// currently FAIL. They are acceptance criteria for the balance work, not a description of
    /// today's behaviour, and are tagged so the mechanical suite can be run on its own:
    ///
    ///     dotnet test --filter TestCategory!=BalanceAnchor
    ///
    /// The PR's absolute duration targets (STS ~6 months early / ~3 months mature; F9 booster
    /// ~3 months in 2016 / ~30 days 2020+) are deliberately NOT asserted here. They depend on
    /// the fixture estimates in Vessels.cs and on the staffing assumption in Staffing.cs, and
    /// they are aspirational numbers for a tuning pass in progress - a red test would read as
    /// "broken" when what is meant is "not tuned yet". They are reported instead, as a
    /// target-versus-actual section in each generated table; see Fixtures/DesignTargets.cs.
    ///
    /// What remains here are properties that hold or fail regardless of the exact fixture
    /// numbers, because they compare two rows against each other rather than against a target.
    /// </summary>
    [TestFixture]
    [Category("BalanceAnchor")]
    public class BalanceAnchorTests
    {
        private static double RefurbDays(VesselArchetype a, TechTier tier)
        {
            FormulaInputs v = a.At(tier);
            return Staffing.DaysAt(Formula.RefurbishmentBP(v),
                                   Staffing.MaxEngineers(v, Vessels.HangarSizeSqrMagnitude));
        }

        /// <summary>
        /// Reuse only makes sense if refurbishing is cheaper than building new. The PR frames
        /// the whole feature as giving the player that choice, which requires refurbishment BP
        /// to sit well under the vessel's integration BP.
        /// </summary>
        [Test]
        public void RefurbishingIsAlwaysCheaperThanBuildingNew()
        {
            var offenders = new List<string>();

            foreach (VesselArchetype a in Vessels.All)
            {
                foreach (TechTier tier in TechTier.All)
                {
                    FormulaInputs v = a.At(tier);
                    double ratio = Formula.RefurbishmentBP(v) / v.BuildPoints;
                    if (ratio >= 1d)
                        offenders.Add($"{a.Name} @ {tier.Name}: refurb BP is {ratio:N2}x integration BP");
                }
            }

            Assert.That(offenders, Is.Empty,
                        "refurbishment should never cost more work than building the vessel from scratch:\n  "
                        + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// Recovering at KSC should always beat coming down at sea. The two adjustments are
        /// applied independently - x0.8 for at-KSC, x1.5*SplashdownPenaltyMult for splashdown -
        /// so once SplashdownPenaltyMult drops below 0.5333 the splashdown branch becomes the
        /// cheaper one and landing back at the pad is a penalty.
        ///
        /// This is about INTENDED behaviour. As shipped neither branch fires for these two
        /// vessels: LandedAt never contains "Splashdown", and the at-KSC test looks for
        /// "Launchpad" where KSP writes "LaunchPad". See the intent-vs-shipped section at the
        /// top of any generated table. The inversion becomes reachable the moment those two
        /// string comparisons are corrected, which is why it is asserted now rather than after.
        /// </summary>
        [Test]
        public void SplashdownIsNeverCheaperThanRecoveringAtKSC()
        {
            var offenders = new List<string>();

            foreach (TechTier tier in TechTier.All)
            {
                FormulaInputs atKSC = Vessels.BoosterRTLS.At(tier);
                FormulaInputs splashed = Vessels.BoosterDroneship.At(tier);

                double home = Formula.RefurbishmentBP(atKSC);
                double sea = Formula.RefurbishmentBP(splashed);

                if (sea < home)
                    offenders.Add($"{tier.Name}: splashdown {sea:N0} BP < at-KSC {home:N0} BP " +
                                  $"(1.5 * SplashdownPenaltyMult = {1.5d * tier.Settings.SplashdownPenaltyMult:N3} vs 0.8)");
            }

            Assert.That(offenders, Is.Empty,
                        "landing back at KSC should never be worse than a sea recovery:\n  "
                        + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// Researching a refurbishment tech should never make refurbishment cost more.
        ///
        /// It can. Cost is `result*0.5 + max(0, result*0.5 - refurbBP*salary/78894)`, so the
        /// engineer subsidy shrinks with refurbishment BP. Advancing a tier cuts BP by far more
        /// than it cuts RefurbishmentCostMult (RefurbishmentRateMult goes 1.0 -> 1.3 -> 3.12
        /// while CostMult only goes 1.0 -> 0.95 -> 0.6056), so the subsidy collapses faster than
        /// the charge it is deducted from and the player ends up paying more for the same work.
        ///
        /// Only bites where the subsidy has not already clamped to zero, i.e. cheaper vessels.
        /// </summary>
        [Test]
        public void AdvancingTechNeverRaisesRefurbishmentCost()
        {
            var offenders = new List<string>();

            foreach (VesselArchetype a in Vessels.All)
            {
                double bas = Formula.RefurbishmentCost(a.At(TechTier.Base));
                double mid = Formula.RefurbishmentCost(a.At(TechTier.Mid));
                double mod = Formula.RefurbishmentCost(a.At(TechTier.Modern));

                if (mid > bas)
                    offenders.Add($"{a.Name}: Base {bas:N1} -> Mid {mid:N1} (+{(mid / bas - 1d):P1})");
                if (mod > mid)
                    offenders.Add($"{a.Name}: Mid {mid:N1} -> Modern {mod:N1} (+{(mod / mid - 1d):P1})");
            }

            Assert.That(offenders, Is.Empty,
                        "researching refurbishment tech makes refurbishment more expensive:\n  "
                        + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// GetRefurbishmentCost subtracts an engineer-salary subsidy from the non-base portion
        /// of the cost, clamped at zero. Refurbishment BP is large enough that the subsidy
        /// always exceeds what it is subtracted from, so the clamp always wins and the whole
        /// branch is inert - refurbishment cost collapses to
        ///
        ///     (vesselPortion + lcPortion) * RefurbishmentCostMult / 16
        ///
        /// which is linear in effective cost and carries no BP term at all. A subsidy that
        /// never applies to any vessel cannot be doing the job it was written for.
        /// </summary>
        [Test]
        public void EngineerSubsidyAffectsRefurbishmentCostForSomeVessel()
        {
            var saturated = new List<string>();
            bool anyActive = false;

            foreach (VesselArchetype a in Vessels.All)
            {
                foreach (TechTier tier in TechTier.All)
                {
                    FormulaInputs v = a.At(tier);

                    // The value the formula degenerates to once max(0, ...) clamps: the base
                    // portion only, halved again by the trailing * 0.5.
                    double floor = Formula.RolloutCost(v) * tier.Settings.RefurbishmentCostMult * 0.25d;
                    double actual = Formula.RefurbishmentCost(v);

                    if (actual > floor * (1d + 1e-9))
                        anyActive = true;
                    else
                        saturated.Add($"{a.Name} @ {tier.Name}");
                }
            }

            Assert.That(anyActive, Is.True,
                        $"the engineer subsidy is clamped to zero for all {saturated.Count} " +
                        "archetype/tier combinations, so refurbishment cost is a fixed fraction " +
                        "of effective cost with no dependence on refurbishment BP");
        }

        /// <summary>
        /// Refurbishment duration should track how complex the vessel is, not how big its pad
        /// is. Today it does not: BP scales with effective cost while the engineer cap scales
        /// with the launch complex, so a capsule recovered to a small pad can take longer than
        /// a far larger stage recovered to a big one.
        /// </summary>
        [Test]
        public void RefurbishmentDurationTracksVesselNotPadSize()
        {
            double capsule = RefurbDays(Vessels.ApolloCM, TechTier.Base);
            double heavyStage = RefurbDays(Vessels.HeavyFirstStage, TechTier.Base);

            Assert.That(capsule, Is.LessThan(heavyStage),
                        $"an Apollo CM ({capsule:N0} d) should not take longer to refurbish than an " +
                        $"S-IC class first stage ({heavyStage:N0} d); the difference is pad staffing, " +
                        "not the vessels");
        }
    }
}
