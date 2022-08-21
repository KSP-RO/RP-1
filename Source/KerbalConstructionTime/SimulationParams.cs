namespace KerbalConstructionTime
{
    public class SimulationParams : IConfigNode
    {
        public CelestialBody SimulationBody;
        [Persistent]
        public bool SimulateInOrbit, DisableFailures;
        public bool IsVesselMoved;
        [Persistent]
        public double SimulationUT, SimOrbitAltitude, SimInclination;
        [Persistent]
        public int DelayMoveSeconds;

        public void Reset()
        {
            IsVesselMoved = false;
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
