using System.Collections.Generic;
using System.Linq;
using RP0.Tests.ReusabilityTests.Config;

namespace RP0.Tests.ReusabilityTests.Fixtures
{
    /// <summary>
    /// The refurbishment durations PR #2825 states it is aiming at, quoted from the PR's own
    /// sources rather than invented here:
    ///
    ///   - the header comment of GameData/RP-1/SCMData/RefurbishmentLevels.cfg
    ///   - the doc comment on Formula.GetRefurbishmentBP
    ///
    /// These are reported in the tables as a target-versus-actual comparison rather than
    /// asserted as tests. They are aspirational numbers for a balance pass in progress, so a
    /// red test would say "broken" when what is meant is "not tuned yet"; the table says the
    /// latter, and the gap moves visibly as the formulas are tuned.
    /// </summary>
    public static class DesignTargets
    {
        public readonly struct Target
        {
            public readonly double Days;
            public readonly string Source;

            public Target(double days, string source)
            {
                Days = days;
                Source = source;
            }
        }

        private sealed class Key
        {
            public string Vessel;
            public string Tier;
            public Target Target;
        }

        private static readonly Key[] _targets =
        {
            new Key
            {
                Vessel = "Shuttle orbiter", Tier = "Base",
                Target = new Target(180d, "STS early program (OV-102, 1981): ~6 months"),
            },
            new Key
            {
                Vessel = "Shuttle orbiter", Tier = "Modern",
                Target = new Target(90d, "STS mature program (OV-105, 1992): ~3 months"),
            },
            new Key
            {
                Vessel = "Reusable booster, droneship", Tier = "Mid",
                Target = new Target(90d, "F9S1 first reuse (2016): ~3 months turnaround"),
            },
            new Key
            {
                Vessel = "Reusable booster, droneship", Tier = "Modern",
                Target = new Target(30d, "F9S1 (2020+): ~30 day turnaround"),
            },
        };

        public static Target? For(VesselArchetype archetype, TechTier tier)
        {
            Key hit = _targets.FirstOrDefault(k => k.Vessel == archetype.Name && k.Tier == tier.Name);
            return hit == null ? (Target?)null : hit.Target;
        }

        /// <summary>Every archetype/target pair defined for a tier, in table order.</summary>
        public static IEnumerable<KeyValuePair<VesselArchetype, Target>> ForTier(TechTier tier)
        {
            foreach (VesselArchetype a in Vessels.All)
            {
                Target? t = For(a, tier);
                if (t.HasValue)
                    yield return new KeyValuePair<VesselArchetype, Target>(a, t.Value);
            }
        }

        /// <summary>
        /// Guards against a target naming a vessel or tier that no longer exists - a silently
        /// dropped target would quietly stop being reported.
        /// </summary>
        public static IEnumerable<string> DanglingReferences()
        {
            var vessels = new HashSet<string>(Vessels.All.Select(v => v.Name));
            var tiers = new HashSet<string>(TechTier.All.Select(t => t.Name));

            foreach (Key k in _targets)
            {
                if (!vessels.Contains(k.Vessel))
                    yield return $"target references unknown vessel '{k.Vessel}'";
                if (!tiers.Contains(k.Tier))
                    yield return $"target references unknown tier '{k.Tier}'";
            }
        }
    }
}
