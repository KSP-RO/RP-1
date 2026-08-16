using System.Collections.Generic;
using RP0.Tests.ReusabilityTests.Config;

namespace RP0.Tests.ReusabilityTests.Fixtures
{
    /// <summary>
    /// Representative RP-1 vessels, as scalars.
    ///
    /// PROVENANCE. cost and effectiveCost are not invented. Each archetype was composed from
    /// a manifest of real RP-1 parts, taking each part's %cost from GameData/RP-1/Tree/
    /// TREE-Parts.cfg and TREE-Engines.cfg and its tags from the same %MODULE[ModuleTagList]
    /// blocks, then applying the tag multipliers from GameData/RP-1/SCMData/CostModifiers.cfg
    /// the way VesselProject.GetEffectiveCostInternal does:
    ///
    ///     effectiveCost = SUM(part.cost * PRODUCT(tag.partMult)) * PRODUCT(tag.globalMult)
    ///
    /// Anchor parts actually used (name, cost, tags):
    ///   ROC-MercuryCM      2468   HumanRated, Reentry      ROE-RS25          5704  EngineLiquidTurbo, Hydrolox
    ///   ROC-GeminiCM       8000   HumanRated, Reentry      ROE-F1            1855  EngineLiquidTurbo
    ///   APOLLO_CM         35000   HumanRated, Reentry      ROE-NERVA         7304  EngineLiquidTurbo, Nuclear
    ///   RO-Mk1Cockpit      9000   Cockpit, HumanRated, Reentry               ROE-Merlin1D 327 EngineLiquidTurbo
    ///   Mark1Cockpit       3000   Cockpit (NOT human-rated) ROE-XLR99          333  EngineLiquidTurbo
    ///   bluedog_redstone    270   EngineLiquidTurbo        RP0probeAvionics66m 4000 Avionics
    ///   rn_titaniv_fs     11000   -                        rn_aerobee150_sas   450  Avionics
    ///   RSBtankSaturn10m7m 14375  -                        benjee10_shuttle_* (full SOCK orbiter, 15 parts)
    ///
    /// Note that in RP-1 only reentry-capable crewed parts carry the HumanRated tag. Plain
    /// aircraft cockpits are tagged Cockpit only, which is why the jet X-plane below is not
    /// human-rated while the X-15 (RO-Mk1Cockpit) is.
    ///
    /// The Shuttle orbiter and the S-IC class stage are composed entirely of real parts (0%
    /// placeholder). Elsewhere, generic tankage/airframe with no single canonical part uses
    /// round figures consistent with the tree's cost distribution (median part 260, p95 10000);
    /// the share of effective cost coming from those is noted per archetype below and ranges
    /// from 0% to 39%.
    ///
    /// TWO KNOWN GAPS, both of which understate effectiveCost:
    ///   1. Resource effective cost (VesselProject.GetResourceEffectiveCost:
    ///      EffectiveCostPerLiterPerResourceMult * (Resource_Variables[res] - 1) * litres)
    ///      is not modelled, so vessels with exotic propellants are cheaper here than in game.
    ///   2. Engine run-time refurbishment (Formula.GetEngineRefurbBPMultiplier) is not applied;
    ///      these are all first-flight vessels, where runTime is 0 and the multiplier is 1.
    ///
    /// Masses are real-world wet masses in tonnes. LC massMax is the smallest pad that accepts
    /// the vessel (LCData.MassMin is floor(massMax * 0.75), so massMax is set just above the
    /// vessel mass). SPH rows use the starting Hangar, whose massMax is float.MaxValue.
    ///
    /// These numbers are good to about the nearest 10-20%. They are not a substitute for
    /// exporting real values from a running game, and the table should be read as showing
    /// shape and order of magnitude rather than exact in-game figures.
    /// </summary>
    public static class Vessels
    {
        /// <summary>
        /// LCData.StartingHangar has sizeMax (40, 10, 40); LaunchComplex.RawMaxEngineers uses
        /// sizeMax.sqrMagnitude * 0.01 when massMax is float.MaxValue.
        /// </summary>
        public const double HangarSizeSqrMagnitude = 40d * 40d + 10d * 10d + 40d * 40d;

        private static FormulaInputs Rocket(double effectiveCost, float cost, float mass, float lcMassMax,
                                            bool humanRated, bool splashed, bool atKSC, float kscDistance)
        {
            return new FormulaInputs(
                effectiveCost: effectiveCost,
                cost: cost,
                buildPoints: Formula.VesselBuildPoints(effectiveCost),
                mass: mass,
                kscDistance: kscDistance,
                humanRated: humanRated,
                isSPH: false,
                splashed: splashed,
                atKSC: atKSC,
                lcIsHumanRated: humanRated,
                lcIsPad: true,
                lcMassMax: lcMassMax,
                settings: TechTier.Base.Settings);
        }

        private static FormulaInputs Plane(double effectiveCost, float cost, float mass,
                                           bool humanRated, bool splashed, bool atKSC, float kscDistance)
        {
            return new FormulaInputs(
                effectiveCost: effectiveCost,
                cost: cost,
                buildPoints: Formula.VesselBuildPoints(effectiveCost),
                mass: mass,
                kscDistance: kscDistance,
                humanRated: humanRated,
                isSPH: true,
                splashed: splashed,
                atKSC: atKSC,
                lcIsHumanRated: true,           // the starting Hangar is human-rated
                lcIsPad: false,
                lcMassMax: float.MaxValue,
                settings: TechTier.Base.Settings);
        }

        // ---- rockets -------------------------------------------------------------------

        /// <summary>Aerobee 150 sounding rocket. Floor case: tiny effective cost.</summary>
        public static readonly VesselArchetype SoundingRocket = new VesselArchetype(
            "Sounding rocket (Aerobee 150)", "Splashdown",
            Rocket(effectiveCost: 1910, cost: 1010f, mass: 0.7f, lcMassMax: 1f,
                   humanRated: false, splashed: true, atKSC: false, kscDistance: 80_000f));

        /// <summary>Redstone-class LV flown uncrewed.</summary>
        public static readonly VesselArchetype RedstoneLV = new VesselArchetype(
            "Redstone-class LV (uncrewed)", "Splashdown",
            Rocket(effectiveCost: 5580, cost: 2570f, mass: 28f, lcMassMax: 30f,
                   humanRated: false, splashed: true, atKSC: false, kscDistance: 320_000f));

        /// <summary>Mercury-Redstone. Pairs with RedstoneLV to isolate the human-rating effect.</summary>
        public static readonly VesselArchetype MercuryRedstone = new VesselArchetype(
            "Mercury-Redstone (crewed)", "Splashdown",
            Rocket(effectiveCost: 10614, cost: 5148f, mass: 30f, lcMassMax: 30f,
                   humanRated: true, splashed: true, atKSC: false, kscDistance: 480_000f));

        /// <summary>29% of effective cost is generic Atlas tankage (no canonical part).</summary>
        public static readonly VesselArchetype MercuryAtlas = new VesselArchetype(
            "Mercury-Atlas (crewed)", "Splashdown",
            Rocket(effectiveCost: 26269, cost: 13344f, mass: 118f, lcMassMax: 120f,
                   humanRated: true, splashed: true, atKSC: false, kscDistance: 1_300_000f));

        public static readonly VesselArchetype GeminiTitan = new VesselArchetype(
            "Gemini-Titan II (crewed)", "Splashdown",
            Rocket(effectiveCost: 56375, cost: 30450f, mass: 154f, lcMassMax: 160f,
                   humanRated: true, splashed: true, atKSC: false, kscDistance: 1_000_000f));

        /// <summary>Capsule alone: very high effective cost for very little mass, recovered far downrange.</summary>
        public static readonly VesselArchetype ApolloCM = new VesselArchetype(
            "Apollo CM alone", "Splashdown",
            Rocket(effectiveCost: 82031, cost: 35000f, mass: 5.6f, lcMassMax: 6f,
                   humanRated: true, splashed: true, atKSC: false, kscDistance: 3_000_000f));

        /// <summary>Mass-dominated case: an S-IC class first stage recovered downrange.
        /// All real parts (2 Saturn tanks, 5x ROE-F1, avionics); 0% placeholder.</summary>
        public static readonly VesselArchetype HeavyFirstStage = new VesselArchetype(
            "Heavy first stage (S-IC class)", "Splashdown",
            Rocket(effectiveCost: 74975, cost: 39150f, mass: 2290f, lcMassMax: 2300f,
                   humanRated: false, splashed: true, atKSC: false, kscDistance: 660_000f));

        /// <summary>F9 S1 class booster returned to the launch site. Pairs with BoosterDroneship.</summary>
        public static readonly VesselArchetype BoosterRTLS = new VesselArchetype(
            "Reusable booster, RTLS", "Launchpad",
            Rocket(effectiveCost: 26247, cost: 14518f, mass: 433f, lcMassMax: 440f,
                   humanRated: false, splashed: false, atKSC: true, kscDistance: 0f));

        /// <summary>The same booster recovered at sea: differs from BoosterRTLS only in where it landed.</summary>
        public static readonly VesselArchetype BoosterDroneship = new VesselArchetype(
            "Reusable booster, droneship", "Splashdown",
            Rocket(effectiveCost: 26247, cost: 14518f, mass: 433f, lcMassMax: 440f,
                   humanRated: false, splashed: true, atKSC: false, kscDistance: 630_000f));

        // ---- planes --------------------------------------------------------------------

        /// <summary>Mark1Cockpit is tagged Cockpit but NOT HumanRated, so a piloted jet is not
        /// human-rated for KCT purposes. 39% of effective cost is placeholder airframe.</summary>
        public static readonly VesselArchetype JetXPlane = new VesselArchetype(
            "Early jet X-plane", "Runway",
            Plane(effectiveCost: 15300, cost: 7100f, mass: 7f,
                  humanRated: false, splashed: false, atKSC: true, kscDistance: 0f));

        /// <summary>X-15 putting down on a lakebed: landed, but not at KSC. Uses RO-Mk1Cockpit
        /// (Cockpit + HumanRated + Reentry), so unlike the jet above it is human-rated.</summary>
        public static readonly VesselArchetype X15 = new VesselArchetype(
            "X-15 rocket plane", "Lakebed (off-site)",
            Plane(effectiveCost: 54228, cost: 15433f, mass: 15f,
                  humanRated: true, splashed: false, atKSC: false, kscDistance: 500_000f));

        /// <summary>SLAM/Pluto class. The Nuclear tag (partMult 3, globalMult 1.1) drives
        /// effectiveCost to ~5.7x cost, far above the ~2x typical of chemical vehicles.</summary>
        public static readonly VesselArchetype NuclearRamjet = new VesselArchetype(
            "Nuclear ramjet cruise vehicle", "Runway",
            Plane(effectiveCost: 120613, cost: 21304f, mass: 27f,
                  humanRated: false, splashed: false, atKSC: true, kscDistance: 0f));

        /// <summary>
        /// The PR's headline anchor: ~6 months refurbishment early, ~3 months mature.
        /// The full 15-part SOCK/benjee10 orbiter plus 3x ROE-RS25 and avionics; 0% placeholder.
        ///
        /// This is a VAB project, not SPH: the stack is assembled vertically and launched from
        /// a pad, so Type/FacilityBuiltIn are VAB and it is recovered to the VAB even though it
        /// lands on a runway. Consequences worth being explicit about:
        ///   - GetRefurbishmentBP's 2.15x SPH multiplier does NOT apply.
        ///   - Staffing comes from the pad, which must be sized for the whole ~2050 t stack,
        ///     not from the hangar's fixed 495.
        ///   - Only the orbiter comes back, so cost/effectiveCost/mass here are orbiter-only;
        ///     the ET is expended and the SRBs are recovered separately.
        ///   - atKSC still applies: the check is on LandedAt containing "Runway", independent
        ///     of which editor the vessel was built in.
        ///
        /// Note the pad's MassMin is floor(2050 * 0.75) = 1537 t, so the 110 t recovered orbiter
        /// on its own would fail MeetsFacilityRequirements - it has to be re-stacked with a new
        /// ET before it can fly again. That affects reuse in game but not these formulas.
        /// </summary>
        public static readonly VesselArchetype ShuttleOrbiter = new VesselArchetype(
            "Shuttle orbiter", "Runway",
            Rocket(effectiveCost: 432787, cost: 146459f, mass: 110f, lcMassMax: 2050f,
                   humanRated: true, splashed: false, atKSC: true, kscDistance: 0f));

        public static IReadOnlyList<VesselArchetype> All { get; } = new[]
        {
            SoundingRocket,
            RedstoneLV,
            MercuryRedstone,
            MercuryAtlas,
            GeminiTitan,
            ApolloCM,
            HeavyFirstStage,
            BoosterRTLS,
            BoosterDroneship,
            JetXPlane,
            X15,
            NuclearRamjet,
            ShuttleOrbiter,
        };
    }
}
