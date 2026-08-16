using System.Collections.Generic;
using System.Linq;

namespace RP0.Tests.ReusabilityTests.Config
{
    /// <summary>
    /// A named point on the tech progression: which recovery/refurbishment techs are
    /// researched, and therefore which multipliers apply.
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

        private static readonly RecoveryTechLevels _levels = RecoveryTechLevels.LoadFromGameData();

        public static RecoveryTechLevels Levels => _levels;

        private static TechTier Make(string name, string era, params string[] techs) =>
            new TechTier(name, era, techs, _levels.Resolve(techs, SalaryEngineers));

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
        public static IEnumerable<string> AllKnownTechs => _levels.AllTechIDs.Distinct();
    }
}
