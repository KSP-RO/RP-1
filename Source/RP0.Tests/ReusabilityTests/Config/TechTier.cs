using System.Collections.Generic;
using System.Linq;

namespace RP0.Tests.ReusabilityTests.Config
{
    /// <summary>
    /// A named point on the tech progression: which recovery/refurbishment techs are
    /// researched, and therefore which multipliers apply.
    ///
    /// The multipliers come from the real <see cref="RP0.RecoveryTechSettings"/> in RP0.dll,
    /// loaded from the shipped cfgs via KSP's ConfigNode and stacked by the actual
    /// RecalculateAndApply(predicate) overload. There is no test-side mirror of that code.
    /// </summary>
    public sealed class TechTier
    {
        /// <summary>
        /// Database.SettingsSC.salaryEngineers, as shipped: GameData/RP-1/SpaceCenterSettings.cfg
        /// line 10 sets `salaryEngineers = 500`, overriding the 1000 field initialiser in
        /// SpaceCenterSettings.cs. The cfg value is what a real game runs with; take care not to
        /// read the C# default as authoritative.
        ///
        /// Fixed across tiers so the tables isolate the recovery/refurbishment techs.
        /// </summary>
        public const double SalaryEngineers = 500d;

        public string Name { get; }
        public string Era { get; }
        public IReadOnlyList<string> ResearchedTechs { get; }
        public RecoverySettings Settings { get; }

        private TechTier(string name, string era, IReadOnlyList<string> techs, RecoverySettings settings)
        {
            Name = name;
            Era = era;
            ResearchedTechs = techs;
            Settings = settings;
        }

        // The real settings object, loaded once from the shipped cfgs. RecalculateAndApply
        // mutates its multiplier properties in place, so Resolve() snapshots them immediately
        // into an immutable RecoverySettings.
        private static readonly RecoveryTechSettings _levels = LoadLevels();

        public static RecoveryTechSettings Levels => _levels;

        private static RecoveryTechSettings LoadLevels()
        {
            var s = new RecoveryTechSettings();
            s.LoadRefurb(ConfigNode.Load(TestPaths.SCMData("RefurbishmentLevels.cfg")).GetNode("SCMREFURBTECHS"));
            s.LoadRecovery(ConfigNode.Load(TestPaths.SCMData("RecoveryLevels.cfg")).GetNode("SCMRECOVERYTECHS"));
            return s;
        }

        private static RecoverySettings Resolve(params string[] techs)
        {
            var researched = new HashSet<string>(techs);
            _levels.RecalculateAndApply(researched.Contains);

            return new RecoverySettings(
                salaryEngineers: SalaryEngineers,
                recoveryRateMult: _levels.RecoveryRateMult,
                recoveryCostMult: _levels.RecoveryCostMult,
                refurbishmentRateMult: _levels.RefurbishmentRateMult,
                refurbishmentCostMult: _levels.RefurbishmentCostMult,
                splashdownPenaltyMult: _levels.SplashdownPenaltyMult);
        }

        private static TechTier Make(string name, string era, params string[] techs) =>
            new TechTier(name, era, techs, Resolve(techs));

        /// <summary>1951. No recovery or refurbishment techs researched.</summary>
        public static readonly TechTier Base = Make("Base", "1951, no techs");

        /// <summary>Gemini/Apollo era: barges, heavy airlift, advanced capsule materials.</summary>
        public static readonly TechTier Mid = Make("Mid", "Gemini/Apollo",
            "basicRocketry", "advancedJetEngines", "materialsScienceAdvCapsules");

        /// <summary>Everything researched: modern transport and Falcon 9 Block 5 materials.</summary>
        public static readonly TechTier Modern = Make("Modern", "F9 Block 5",
            "basicRocketry", "advancedJetEngines", "refinedTurbofans",
            "materialsScienceAdvCapsules", "materialsScienceCommercial", "materialsScienceNF");

        public static readonly TechTier[] All = { Base, Mid, Modern };

        public override string ToString() => Name;

        /// <summary>
        /// Every tech id the cfgs define, so a test can catch a tier that silently stops
        /// covering a newly added tech.
        /// </summary>
        public static IEnumerable<string> AllKnownTechs =>
            _levels.RefurbEntries.Select(e => e.techID)
                   .Concat(_levels.RecoveryEntries.Select(e => e.techID))
                   .Distinct();
    }
}
