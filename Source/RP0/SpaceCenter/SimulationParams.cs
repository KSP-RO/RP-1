using UniLinq;
using ROUtils.DataTypes;

namespace RP0
{
    public enum SimOrbitMode { Circular, Elliptical, Hyperbolic }

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

        public CelestialBody SimOriginBody
        {
            get
            {
                if (_simOriginBody == null && !string.IsNullOrEmpty(simOriginBodyName))
                    _simOriginBody = FlightGlobals.Bodies.FirstOrDefault(b => b.bodyName == simOriginBodyName);
                return _simOriginBody;
            }
            set
            {
                _simOriginBody = value;
                simOriginBodyName = value == null ? string.Empty : value.bodyName;
            }
        }

        private CelestialBody _simOriginBody = null;

        [Persistent]
        private string simOriginBodyName = string.Empty;

        [Persistent]
        public bool SimulateInOrbit, DisableFailures;
        public bool IsVesselMoved;
        [Persistent]
        public double SimulationUT, SimOrbitAltitude, SimOrbitPe, SimOrbitAp, SimInclination, SimLAN, SimMNA, SimArgPe;
        [Persistent]
        public int DelayMoveSeconds;

        [Persistent]
        public SimOrbitMode SimOrbitMode = SimOrbitMode.Circular;

        // Hyperbolic-approach parameters.
        // SimHyperbolicInsertionDV: Insertion ΔV at the target body (m/s), as reported by a transfer
        //   window planner. Internally translated to v∞ using the periapsis altitude.
        // SimHyperbolicPeAlt: periapsis altitude above the target body, in meters.
        // SimHypTimeToPe: seconds before periapsis at which the vessel is placed on the approach.
        // SimHypTransferTime: heliocentric transfer duration (seconds) from the origin body to the
        //   target. Departure UT = arrival UT − this; used to Lambert-solve the arrival v∞ direction,
        //   which orients the approach hyperbola. Arrival UT is the sim's SimulationUT.
        [Persistent]
        public double SimHyperbolicInsertionDV, SimHyperbolicPeAlt, SimHypTimeToPe, SimHypTransferTime;

        // Autostage: -1 disables. Otherwise shed (decouple) every stage above SimAutostageTarget at sim start.
        [Persistent]
        public int SimAutostageTarget = -1;

        public void Reset()
        {
            IsVesselMoved = false;
        }
    }
}
