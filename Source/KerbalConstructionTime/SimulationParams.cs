using UniLinq;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public class SimulationParams : ConfigNodePersistenceBase, IConfigNode
    {
        public CelestialBody SimulationBody
        {
            get
            {
                if (_simulationBody == null)
                {
                    if (simulationBodyName != null && simulationBodyName != string.Empty)
                        _simulationBody = FlightGlobals.Bodies.FirstOrDefault(b => b.bodyName == simulationBodyName);
                }
                return _simulationBody;
            }
            set
            {
                _simulationBody = value;
                simulationBodyName = value.bodyName;
            }
        }

        private CelestialBody _simulationBody = null;

        [Persistent]
        private string simulationBodyName = string.Empty;

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
    }
}
