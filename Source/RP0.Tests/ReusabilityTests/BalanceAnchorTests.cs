using System.Collections.Generic;
using NUnit.Framework;
using RP0.Tests.ReusabilityTests.Config;
using RP0.Tests.ReusabilityTests.Fixtures;

namespace RP0.Tests.ReusabilityTests
{
    /// <summary>
    /// Design properties the reuse system needs but does not yet have. Unlike
    /// RecoveryInvariantTests - which check relationships that hold - the tests here FAIL by
    /// design, as acceptance criteria for the balance pass, and are tagged so the mechanical
    /// suite can be run on its own:
    ///
    ///     dotnet test --filter TestCategory!=BalanceAnchor
    ///
    /// The PR's absolute duration targets (STS ~6 months early / ~3 months mature; F9 booster
    /// ~3 months in 2016 / ~30 days 2020+) are NOT asserted here - they depend on the fixture
    /// estimates and staffing assumption, and are reported instead as a target-versus-actual
    /// section in each generated table (see Fixtures/DesignTargets.cs).
    ///
    /// Several properties that once failed here now pass and have moved to RecoveryInvariantTests:
    /// refurbishing is cheaper than building new, and net refurbishment cost falls with tech
    /// (the charged cost alone can rise, but the salary it offsets falls faster - see
    /// AdvancingTechNeverRaisesNetRefurbishmentCost). What remains is the one property that is
    /// still genuinely violated.
    /// </summary>
    [TestFixture]
    [Category("BalanceAnchor")]
    public class BalanceAnchorTests
    {
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
                                  $"({tier.Settings.SplashdownPenaltyMult:N3} vs 0.8)");
            }

            Assert.That(offenders, Is.Empty,
                        "landing back at KSC should never be worse than a sea recovery:\n  "
                        + string.Join("\n  ", offenders));
        }
    }
}
