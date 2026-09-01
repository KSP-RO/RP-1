using System.Linq;
using NUnit.Framework;
using RP0.Tests.ReusabilityTests.Config;

namespace RP0.Tests.ReusabilityTests
{
    /// <summary>
    /// Exercises the real <see cref="RP0.RecoveryTechSettings"/> from RP0.dll - loaded from the
    /// shipped cfgs via KSP's ConfigNode and stacked by RecalculateAndApply(predicate) - and
    /// checks the cfgs still contain the techs the tiers assume.
    /// </summary>
    [TestFixture]
    public class RecoveryTechSettingsTests
    {
        [Test]
        public void Cfgs_ParseIntoTheExpectedNumberOfTechs()
        {
            Assert.Multiple(() =>
            {
                Assert.That(TechTier.Levels.RecoveryEntries, Is.Not.Empty, "SCMRECOVERYTECHS TECH nodes");
                Assert.That(TechTier.Levels.RefurbEntries, Is.Not.Empty, "SCMREFURBTECHS TECH nodes");
            });
        }

        [Test]
        public void EveryStatedDesignTargetNamesARealVesselAndTier()
        {
            // A target pointing at a renamed vessel or tier would silently stop being reported
            // in the tables, quietly removing the comparison it exists to provide.
            Assert.That(Fixtures.DesignTargets.DanglingReferences(), Is.Empty);
        }

        [Test]
        public void EveryTechInTheCfgsIsCoveredByTheModernTier()
        {
            // Guards against a tech being added to the cfgs without the Modern tier - which is
            // meant to be "everything researched" - being updated to include it.
            var uncovered = TechTier.AllKnownTechs
                                    .Except(TechTier.Modern.ResearchedTechs)
                                    .ToList();

            Assert.That(uncovered, Is.Empty,
                        "Modern tier does not research every tech defined in the SCMData cfgs");
        }

        [Test]
        public void BaseTier_UsesTheCfgBaseValues()
        {
            RecoverySettings s = TechTier.Base.Settings;

            Assert.Multiple(() =>
            {
                Assert.That(s.RecoveryRateMult, Is.EqualTo(TechTier.Levels.RecoveryRateBase),
                            "recovery rate falls back to SCMRECOVERYTECHS BASE");
                Assert.That(s.RecoveryCostMult, Is.EqualTo(TechTier.Levels.RecoveryCostBase),
                            "recovery cost falls back to SCMRECOVERYTECHS BASE");
                Assert.That(s.RefurbishmentRateMult, Is.EqualTo(TechTier.Levels.RefurbishmentRateBase),
                            "refurbishment rate falls back to SCMREFURBTECHS BASE");
                Assert.That(s.SplashdownPenaltyMult, Is.EqualTo(1d),
                            "no splashdown relief before any materials tech");
            });
        }

        /// <summary>
        /// RecoveryTechSettings.LoadRefurb reads only RateRefurbishment out of the BASE node,
        /// and RecalculateAndApply seeds the refurbishment cost multiplier from a hardcoded 1.0,
        /// so RefurbishmentLevels.cfg's BASE { CostRefurbishment = 1.0 } never reaches anything.
        ///
        /// Asserted against the real code rather than fixed: whether that value should be read
        /// is a balance decision for the PR author. Today the cfg happens to say 1.0 too, so
        /// nothing is observably wrong; if the cfg line is ever changed, this test fails and
        /// says why the change had no effect.
        /// </summary>
        [Test]
        public void BaseCostRefurbishmentInCfgIsNotRead()
        {
            var settings = new RecoveryTechSettings();
            ConfigNode node = ConfigNode.Load(TestPaths.SCMData("RefurbishmentLevels.cfg")).GetNode("SCMREFURBTECHS");
            settings.LoadRefurb(node);
            settings.RecalculateAndApply(_ => false);   // nothing researched

            string cfgValue = node.GetNode("BASE")?.GetValue("CostRefurbishment");

            Assert.Multiple(() =>
            {
                Assert.That(cfgValue, Is.Not.Null,
                            "BASE { CostRefurbishment } is present in the cfg");
                Assert.That(settings.RefurbishmentCostMult, Is.EqualTo(1d),
                            "but LoadRefurb ignores it and the multiplier starts at a hardcoded 1.0");
            });
        }

        [Test]
        public void TechProgressionIsMonotonic()
        {
            RecoverySettings b = TechTier.Base.Settings;
            RecoverySettings m = TechTier.Mid.Settings;
            RecoverySettings x = TechTier.Modern.Settings;

            Assert.Multiple(() =>
            {
                Assert.That(m.RecoveryRateMult, Is.GreaterThanOrEqualTo(b.RecoveryRateMult), "recovery rate Base->Mid");
                Assert.That(x.RecoveryRateMult, Is.GreaterThanOrEqualTo(m.RecoveryRateMult), "recovery rate Mid->Modern");

                Assert.That(m.RecoveryCostMult, Is.LessThanOrEqualTo(b.RecoveryCostMult), "recovery cost Base->Mid");
                Assert.That(x.RecoveryCostMult, Is.LessThanOrEqualTo(m.RecoveryCostMult), "recovery cost Mid->Modern");

                Assert.That(m.RefurbishmentRateMult, Is.GreaterThanOrEqualTo(b.RefurbishmentRateMult), "refurb rate Base->Mid");
                Assert.That(x.RefurbishmentRateMult, Is.GreaterThanOrEqualTo(m.RefurbishmentRateMult), "refurb rate Mid->Modern");

                Assert.That(m.RefurbishmentCostMult, Is.LessThanOrEqualTo(b.RefurbishmentCostMult), "refurb cost Base->Mid");
                Assert.That(x.RefurbishmentCostMult, Is.LessThanOrEqualTo(m.RefurbishmentCostMult), "refurb cost Mid->Modern");

                Assert.That(m.SplashdownPenaltyMult, Is.LessThanOrEqualTo(b.SplashdownPenaltyMult), "splashdown Base->Mid");
                Assert.That(x.SplashdownPenaltyMult, Is.LessThanOrEqualTo(m.SplashdownPenaltyMult), "splashdown Mid->Modern");
            });
        }

        [Test]
        public void CfgValuesParseWithCommentsAndWhitespaceStripped()
        {
            // RefurbishmentLevels.cfg has "SplashdownPenaltyMult   = 0.5 // Drone ships, etc"
            // and "RateRefurbishment   = 1.0 " with a trailing space. This checks the shipped
            // cfg content survives KSP's ConfigNode parse as the tiers expect.
            var nf = TechTier.Levels.RefurbEntries.First(e => e.techID == "materialsScienceNF");

            Assert.Multiple(() =>
            {
                Assert.That(nf.splashdownPenaltyMult, Is.EqualTo(0.5d), "trailing // comment stripped");
                Assert.That(TechTier.Levels.RefurbishmentRateBase, Is.EqualTo(1.0d), "trailing whitespace trimmed");
            });
        }
    }
}
