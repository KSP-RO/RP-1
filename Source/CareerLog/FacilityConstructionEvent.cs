using System;

namespace RP0
{
    public class FacilityConstructionEvent : CareerEvent
    {
        [Persistent]
        public FacilityType Facility;

        [Persistent]
        public ConstructionState State;

        [Persistent]
        public Guid FacilityID;

        public FacilityConstructionEvent(double UT) : base(UT)
        {
        }

        public FacilityConstructionEvent(ConfigNode n) : base(n)
        {
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            node.TryGetValue(nameof(FacilityID), ref FacilityID);
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            node.AddValue(nameof(FacilityID), FacilityID);
        }

        public static FacilityType ParseFacilityType(SpaceCenterFacility scf)
        {
            return (FacilityType)Enum.Parse(typeof(FacilityType), scf.ToString());
        }
    }

    public enum ConstructionState
    {
        Started = 1,
        Cancelled = 1 << 1,
        Completed = 1 << 2,
        Dismantled = 1 << 3
    }

    /// <summary>
    /// RP-1 custom facility type enum. Has all the KSP builtin facilities + our custom ones.
    /// Supports bitwise operations.
    /// </summary>
    public enum FacilityType
    {
        Administration = 1,
        AstronautComplex = 1 << 1,
        LaunchPad = 1 << 2,
        MissionControl = 1 << 3,
        ResearchAndDevelopment = 1 << 4,
        Runway = 1 << 5,
        TrackingStation = 1 << 6,
        SpaceplaneHangar = 1 << 7,
        VehicleAssemblyBuilding = 1 << 8,
        LaunchComplex = 1 << 9
    }
}
