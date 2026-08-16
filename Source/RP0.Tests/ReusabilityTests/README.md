# Reusability formula tests

Covers the recovery and refurbishment formulas in `Source/RP0/Utilities/FormulaCore.cs`
(PR #2825), and generates a balance table so the numbers can be judged rather than guessed at.

## Running

```
dotnet test Source/RP0.Tests                                       # everything
dotnet test Source/RP0.Tests --filter "TestCategory!=BalanceAnchor" # regression suite only
dotnet test Source/RP0.Tests -l "console;verbosity=detailed"        # print the tables
```

No KSP install or assemblies are needed. The project compiles `FormulaCore.cs` directly as a
linked source file; that file is deliberately free of KSP, Unity and RP-1 singleton references.
If it ever fails to compile here, the extraction has regressed - fix `FormulaCore.cs` rather
than adding an assembly reference.

## What's here

| File | Purpose |
| --- | --- |
| `Config/CfgNode.cs` | Minimal KSP-cfg parser (no `ConfigNode` available) |
| `Config/RecoveryTechLevels.cs` | Mirror of `Database.RecoveryTechSettings`, tech set injected |
| `Config/TechTier.cs` | The Base / Mid / Modern points on the tech progression |
| `Fixtures/Vessels.cs` | 13 vessel archetypes, with provenance for every number |
| `Fixtures/LegacyFormula.cs` | Pre-#2825 recovery model, for before/after columns |
| `Fixtures/Staffing.cs` | BP to days, mirroring `LaunchComplex` staffing rules |
| `ReusabilityTable.cs` | Markdown table renderer and diff |
| `FormulaEquivalenceTests.cs` | Guards the extraction against transcription slips |
| `RecoveryTechLevelsTests.cs` | Cfg parsing and tech stacking fidelity |
| `RecoveryInvariantTests.cs` | Relationships the formulas do hold |
| `Fixtures/DesignTargets.cs` | The PR's stated durations, reported in the tables rather than asserted |
| `RecoveryTableTests.cs` | Golden-table regression |
| `BalanceAnchorTests.cs` | Structural properties reuse needs. **Currently failing by design.** |

## Targets: reported, not asserted

The PR's absolute duration goals — STS ~6 months early / ~3 months mature, F9 booster
~3 months in 2016 / ~30 days from 2020 — are **not** tests. They depend on the fixture
estimates and the staffing assumption, and they are aspirational numbers for a tuning pass in
progress, so a red test would read as "broken" when what is meant is "not tuned yet". Each
generated table ends with a target-versus-actual section instead, and the ratio there moves
visibly as the formulas are tuned.

What `BalanceAnchorTests` still asserts are properties that compare two rows against each
other, so they hold or fail regardless of the exact fixture numbers: refurbishing should cost
less work than building new, splashdown should never beat recovering at KSC, and refurbishment
duration should track the vessel rather than the size of its launch complex.

The tech multipliers are parsed from `GameData/RP-1/SCMData/RecoveryLevels.cfg` and
`RefurbishmentLevels.cfg` rather than mirrored, so editing those cfgs moves the tables.

## Approving a table change

When a formula, fixture or cfg changes, `RecoveryTableTests` fails with a line-by-line diff and
writes `Approved/RecoveryTable.<tier>.received.md`. Read it, and if the change is intended:

```
cd Source/RP0.Tests/ReusabilityTests/Approved
mv RecoveryTable.Base.received.md RecoveryTable.Base.md      # etc.
```

Commit the approved tables with the change that caused them. `.received.md` files are gitignored.

## Intent vs shipped behaviour

The tables model the formulas as **intended**, not as they currently behave. `Formula.VesselInputs`
derives `splashed` and `atKSC` by substring-matching `VesselProject.LandedAt`, which comes from
`Vessel.landedAt` — a field holding launch-site names, not situation names. Measured over a real
career save (55,603 values): `"Splashdown"` never appears, and pad landings write `LaunchPad`
where the code tests for `"Launchpad"`. So the 1.5x splashdown penalty, the whole
`SplashdownPenaltyMult` tech knob, and the at-KSC discount for pad landings are all inert today.

Each generated table leads with the evidence. Keeping the fixtures on intent is deliberate: it
keeps the tables usable for balance discussion, and the gap is documented rather than silently
baked into the numbers.

## Caveats

- Vessel `cost` / `effectiveCost` are composed from real RP-1 part costs and tag multipliers,
  but resource effective cost and engine run-time refurbishment are not modelled, so both
  understate the in-game figure. See the header comment in `Fixtures/Vessels.cs`.
- Durations assume a fully staffed complex (`LaunchComplex.MaxEngineers`), so every
  engineer-driven duration is a best case.
- These are estimates, good to perhaps 10-20%. The tables show shape and order of magnitude;
  they are not a substitute for exporting real values from a running game.
