using System;
using RP0.Tests.ReusabilityTests.Config;

namespace RP0.Tests.ReusabilityTests.Fixtures
{
    /// <summary>
    /// One row of the balance table: a representative vessel plus the presentation-only
    /// context needed to turn BP into a duration (staffing, where it came down).
    /// </summary>
    public sealed class VesselArchetype
    {
        public string Name { get; }
        /// <summary>Where the vessel ended up, for display. The formula-relevant part is
        /// already reduced to FormulaInputs.Splashed / FormulaInputs.AtKSC.</summary>
        public string LandedAt { get; }
        /// <summary>Vessel and LC scalars at the Base tech tier. Retier() swaps the settings.</summary>
        public FormulaInputs Inputs { get; }

        public VesselArchetype(string name, string landedAt, FormulaInputs inputs)
        {
            Name = name;
            LandedAt = landedAt;
            Inputs = inputs;
        }

        public bool IsSPH => Inputs.IsSPH;
        public bool HumanRated => Inputs.HumanRated;

        /// <summary>The same vessel evaluated with a different tech tier's multipliers.</summary>
        public FormulaInputs At(TechTier tier) => Inputs.With(settings: tier.Settings);

        public override string ToString() => Name;
    }
}
