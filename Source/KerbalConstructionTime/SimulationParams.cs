namespace KerbalConstructionTime
{
    public class SimulationParams
    {
        public CelestialBody SimulationBody;
        public bool SimulateInOrbit, IsVesselMoved, DisableFailures;
        public double SimulationUT, SimOrbitAltitude, SimInclination;
        public int DelayMoveSeconds;

        public void Reset()
        {
            IsVesselMoved = false;
        }
    }
}
