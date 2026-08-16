using System.Collections.Generic;
using System.Linq;

namespace RP0.Tests.ReusabilityTests.Config
{
    /// <summary>
    /// Reimplements Database.RecoveryTechSettings (Source/RP0/Singletons/Database.cs) against
    /// the parser above, with the researched-tech set injected instead of being queried from
    /// ResearchAndDevelopment. Loading and stacking behaviour is deliberately identical,
    /// quirks included - see RecoveryTechLevelsTests for the assertions that pin that down.
    ///
    /// Because it reads the shipped cfgs rather than a mirrored copy of their numbers, editing
    /// GameData/RP-1/SCMData/*.cfg moves the golden tables. That is the point: the balance
    /// data is under test, not just the C#.
    /// </summary>
    public sealed class RecoveryTechLevels
    {
        public struct RefurbTechEntry
        {
            public string techID;
            public double rateRefurbishment;
            public double costRefurbishment;
            public double splashdownPenaltyMult;
        }

        public struct RecoveryTechEntry
        {
            public string techID;
            public double rateRecovery;
            public double costRecovery;
        }

        public double RefurbishmentRateBase { get; private set; } = 1.0d;
        public double RecoveryRateBase { get; private set; } = 1.0d;
        public double RecoveryCostBase { get; private set; } = 1.0d;

        public List<RefurbTechEntry> RefurbEntries { get; } = new List<RefurbTechEntry>();
        public List<RecoveryTechEntry> RecoveryEntries { get; } = new List<RecoveryTechEntry>();

        public static RecoveryTechLevels LoadFromGameData()
        {
            var levels = new RecoveryTechLevels();
            levels.LoadRefurb(CfgNode.Load(TestPaths.SCMData("RefurbishmentLevels.cfg")).GetNode("SCMREFURBTECHS"));
            levels.LoadRecovery(CfgNode.Load(TestPaths.SCMData("RecoveryLevels.cfg")).GetNode("SCMRECOVERYTECHS"));
            return levels;
        }

        public void LoadRefurb(CfgNode refurbNode)
        {
            RefurbEntries.Clear();
            if (refurbNode == null) return;

            if (refurbNode.HasNode("BASE"))
            {
                var b = refurbNode.GetNode("BASE");
                double tempBase = RefurbishmentRateBase;
                if (b.TryGetValue("RateRefurbishment", ref tempBase))
                    RefurbishmentRateBase = tempBase;

                // NOTE: Database.LoadRefurb reads only RateRefurbishment from BASE, so
                // RefurbishmentLevels.cfg's BASE { CostRefurbishment = 1.0 } is a no-op.
                // Mirrored deliberately; see RecoveryTechLevelsTests.
            }

            foreach (CfgNode tech in refurbNode.GetNodes("TECH"))
            {
                var e = new RefurbTechEntry { techID = tech.GetValue("id") };

                double tRate = 1.0d;
                double tCost = 1.0d;
                double tSplash = 1.0d;

                tech.TryGetValue("RateRefurbishment", ref tRate);
                tech.TryGetValue("CostRefurbishment", ref tCost);
                tech.TryGetValue("SplashdownPenaltyMult", ref tSplash);

                e.rateRefurbishment = tRate;
                e.costRefurbishment = tCost;
                e.splashdownPenaltyMult = tSplash;

                if (!string.IsNullOrEmpty(e.techID))
                    RefurbEntries.Add(e);
            }
        }

        public void LoadRecovery(CfgNode recNode)
        {
            RecoveryEntries.Clear();
            if (recNode == null) return;

            if (recNode.HasNode("BASE"))
            {
                var b = recNode.GetNode("BASE");
                double tRate = RecoveryRateBase;
                double tCost = RecoveryCostBase;

                b.TryGetValue("RateRecovery", ref tRate);
                b.TryGetValue("CostRecovery", ref tCost);

                RecoveryRateBase = tRate;
                RecoveryCostBase = tCost;
            }

            foreach (CfgNode tech in recNode.GetNodes("TECH"))
            {
                var e = new RecoveryTechEntry { techID = tech.GetValue("id") };

                double tRate = 1.0d;
                double tCost = 1.0d;

                tech.TryGetValue("RateRecovery", ref tRate);
                tech.TryGetValue("CostRecovery", ref tCost);

                e.rateRecovery = tRate;
                e.costRecovery = tCost;

                if (!string.IsNullOrEmpty(e.techID))
                    RecoveryEntries.Add(e);
            }
        }

        /// <summary>
        /// The stacking half of Database.RecoveryTechSettings.RecalculateAndApply, with the
        /// researched-tech set passed in. Note the asymmetry, faithfully reproduced: the
        /// refurbishment cost multiplier starts at 1.0 while the recovery cost multiplier
        /// starts at RecoveryCostBase.
        /// </summary>
        public RecoverySettings Resolve(IEnumerable<string> researchedTechs, double salaryEngineers)
        {
            var researched = new HashSet<string>(researchedTechs);

            double refurbRate = RefurbishmentRateBase;
            double refurbCost = 1.0d;
            double splashdown = 1.0d;

            foreach (var e in RefurbEntries)
            {
                if (researched.Contains(e.techID))
                {
                    refurbRate *= e.rateRefurbishment;
                    refurbCost *= e.costRefurbishment;
                    splashdown *= e.splashdownPenaltyMult;
                }
            }

            double recRate = RecoveryRateBase;
            double recCost = RecoveryCostBase;

            foreach (var e in RecoveryEntries)
            {
                if (researched.Contains(e.techID))
                {
                    recRate *= e.rateRecovery;
                    recCost *= e.costRecovery;
                }
            }

            return new RecoverySettings(salaryEngineers, recRate, recCost, refurbRate, refurbCost, splashdown);
        }

        public IEnumerable<string> AllTechIDs =>
            RefurbEntries.Select(e => e.techID).Concat(RecoveryEntries.Select(e => e.techID));
    }
}
