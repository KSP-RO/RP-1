using System;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public class AirlaunchParams : ConfigNodePersistenceBase, IConfigNode
    {
        [Persistent]
        public Guid KCTVesselId = Guid.Empty;
        [Persistent]
        public Guid KSPVesselId = Guid.Empty;
        [Persistent]
        public double Altitude;
        [Persistent]
        public double KscAzimuth = 90d;
        [Persistent]
        public double KscDistance;
        [Persistent]
        public double LaunchAzimuth = 270d;
        [Persistent]
        public double Velocity;

        public AirlaunchParams() { }
        public AirlaunchParams(AirlaunchParams src)
        {
            // Leave these empty
            //KCTVesselId = src.KCTVesselId;
            //KSPVesselId = src.KSPVesselId;

            Altitude = src.Altitude;
            KscAzimuth = src.KscAzimuth;
            LaunchAzimuth = src.LaunchAzimuth;
            KscDistance = src.KscDistance;
            Velocity = src.Velocity;
        }

        public bool Validate(out string errorMsg)
        {
            AirlaunchTechLevel lvl = KerbalConstructionTimeData.Instance.IsSimulatedFlight ? AirlaunchTechLevel.GetHighestLevelIncludingUnderResearch() :
                                                                    AirlaunchTechLevel.GetCurrentLevel();
            if (lvl == null)
            {
                errorMsg = "No valid airlaunch configuration found";
                return false;
            }

            double minKscDist = 0;

            if (KscAzimuth >= 360 || KscAzimuth < 0)
                errorMsg = "Invalid KSC azimuth";
            else if (LaunchAzimuth >= 360 || LaunchAzimuth < 0)
                errorMsg = "Invalid Launch azimuth";
            else if (Altitude > lvl.MaxAltitude || Altitude < lvl.MinAltitude)
                errorMsg = $"Altitude needs to be between {lvl.MinAltitude} and {lvl.MaxAltitude} m";
            else if (Velocity > lvl.MaxVelocity || Velocity < lvl.MinVelocity)
                errorMsg = $"Velocity needs to be between {lvl.MinVelocity} and {lvl.MaxVelocity} m/s";
            else if (KscDistance > lvl.MaxKscDistance || KscDistance < minKscDist)
                errorMsg = $"Distance from Space Center needs to be between {minKscDist / 1000:0.#} and {lvl.MaxKscDistance / 1000:0.#} km";
            else
                errorMsg = null;
            return errorMsg == null;
        }
    }
}