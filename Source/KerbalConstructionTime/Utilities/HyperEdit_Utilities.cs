using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    // From the HyperEdit mod created by khyperia and Ezriilc.
    // The Lander code has been modified to simulate doing airlaunches from a carrier plane.
    public static class HyperEdit_Utilities
    {
        public static void PutInOrbitAround(CelestialBody body, double altitude)
        {
            PutInOrbitAround(body, altitude, 0);
        }

        public static void PutInOrbitAround(CelestialBody body, double altitude, double inclination)
        {
            FlightGlobals.fetch.SetShipOrbit(body.flightGlobalsIndex, 0, altitude + body.Radius, inclination, 0, 0, 0, 0);
            FloatingOrigin.ResetTerrainShaderOffset();
        }

        public static Orbit Clone(this Orbit o)
        {
            return new Orbit(o.inclination, o.eccentricity, o.semiMajorAxis, o.LAN,
                o.argumentOfPeriapsis, o.meanAnomalyAtEpoch, o.epoch, o.referenceBody);
        }

        public static void SetOrbit(this Vessel vessel, Orbit newOrbit)
        {
            var destinationMagnitude = newOrbit.getRelativePositionAtUT(Planetarium.GetUniversalTime()).magnitude;
            if (destinationMagnitude > newOrbit.referenceBody.sphereOfInfluence)
            {
                Debug.LogError("Destination position was above the sphere of influence");
                return;
            }
            if (destinationMagnitude < newOrbit.referenceBody.Radius)
            {
                Debug.LogError("Destination position was below the surface");
                return;
            }

            vessel.PrepVesselTeleport();

            try
            {
                OrbitPhysicsManager.HoldVesselUnpack(60);
            }
            catch (NullReferenceException)
            {
                Debug.LogError("HyperEdit_Utilities HoldVesselUnpack threw NullReferenceException");
            }

            var allVessels = FlightGlobals.fetch?.vessels ?? (IEnumerable<Vessel>)new[] { vessel };
            foreach (var v in allVessels)
                v.GoOnRails();

            var oldBody = vessel.orbitDriver.orbit.referenceBody;

            HardsetOrbit(vessel.orbitDriver, newOrbit);

            vessel.orbitDriver.pos = vessel.orbit.pos.xzy;
            vessel.orbitDriver.vel = vessel.orbit.vel;

            var newBody = vessel.orbitDriver.orbit.referenceBody;
            if (newBody != oldBody)
            {
                var evnt = new GameEvents.HostedFromToAction<Vessel, CelestialBody>(vessel, oldBody, newBody);
                GameEvents.onVesselSOIChanged.Fire(evnt);
            }
        }

        private static void HardsetOrbit(OrbitDriver orbitDriver, Orbit newOrbit)
        {
            var orbit = orbitDriver.orbit;
            HardsetOrbit(orbit, newOrbit);
            if (orbit.referenceBody != newOrbit.referenceBody)
            {
                orbitDriver.OnReferenceBodyChange?.Invoke(newOrbit.referenceBody);
            }
        }

        private static void HardsetOrbit(Orbit orbit, Orbit newOrbit)
        {
            orbit.inclination = newOrbit.inclination;
            orbit.eccentricity = newOrbit.eccentricity;
            orbit.semiMajorAxis = newOrbit.semiMajorAxis;
            orbit.LAN = newOrbit.LAN;
            orbit.argumentOfPeriapsis = newOrbit.argumentOfPeriapsis;
            orbit.meanAnomalyAtEpoch = newOrbit.meanAnomalyAtEpoch;
            orbit.epoch = newOrbit.epoch;
            orbit.referenceBody = newOrbit.referenceBody;
            orbit.Init();
            orbit.UpdateFromUT(Planetarium.GetUniversalTime());
        }

        public static void PrepVesselTeleport(this Vessel vessel)
        {
            if (vessel.Landed)
            {
                vessel.Landed = false;
            }
            if (vessel.Splashed)
            {
                vessel.Splashed = false;
            }
            if (vessel.landedAt != string.Empty)
            {
                vessel.landedAt = string.Empty;
            }
            var parts = vessel.parts;
            if (parts != null)
            {
                foreach (var part in parts)
                {
                    if (part.Modules.Contains<LaunchClamp>() || part.HasTag("PadInfrastructure"))
                    {
                        part.Die();
                    }
                }
            }
        }

        public static void DoAirlaunch(AirlaunchParams launchParams)
        {
            CelestialBody body = FlightGlobals.currentMainBody;
            Vessel vessel = FlightGlobals.ActiveVessel;

            // SpaceCenter.Instance.transform doesn't work correctly with RSS + KSCSwitcher installed
            PQSCity ksc = Utilities.FindKSC(body);
            Vector3d kscPosition = ksc.transform.position - body.position;
            Vector3d kscUp = kscPosition.normalized;
            Vector3d kscEast = Vector3d.Cross(body.angularVelocity, kscPosition).normalized;
            Vector3d kscNorth = Vector3d.Cross(kscEast, kscUp);
            double kscAltitude = kscPosition.magnitude - body.Radius;

            // From https://www.movable-type.co.uk/scripts/latlong-vectors.html#midpoint
            var δ = launchParams.KscDistance / body.Radius;
            var d = kscNorth * Math.Cos(Mathf.Deg2Rad * launchParams.KscAzimuth) + kscEast * Math.Sin(Mathf.Deg2Rad * launchParams.KscAzimuth);
            var teleportPosition = kscPosition * Math.Cos(δ) + body.Radius * d * Math.Sin(δ);
            teleportPosition += teleportPosition.normalized * (launchParams.Altitude - kscAltitude);

            Vector3d up = teleportPosition.normalized;
            Vector3d east = Vector3d.Cross(body.angularVelocity, teleportPosition).normalized;
            Vector3d north = Vector3d.Cross(east, up);
            Vector3d teleportVelocity = Vector3d.Cross(body.angularVelocity, teleportPosition);
            teleportVelocity += (Math.Sin(Mathf.Deg2Rad * launchParams.LaunchAzimuth) * east + Math.Cos(Mathf.Deg2Rad * launchParams.LaunchAzimuth) * north) * launchParams.Velocity;

            // counter for the momentary fall when on rails
            teleportVelocity += up * (body.gravParameter / teleportPosition.sqrMagnitude / 2);

            teleportPosition = teleportPosition.xzy;
            teleportVelocity = teleportVelocity.xzy;

            Vector3d to = teleportPosition.xzy.normalized;
            Quaternion rotation = Quaternion.LookRotation(-to);
            rotation *= Quaternion.AngleAxis((float)launchParams.LaunchAzimuth, Vector3.back);

            Orbit orbit = vessel.orbitDriver.orbit.Clone();
            orbit.UpdateFromStateVectors(teleportPosition, teleportVelocity, body, Planetarium.GetUniversalTime());
            vessel.SetOrbit(orbit);
            vessel.SetRotation(rotation);

            ResetGroundContact(vessel);
        }

        /// <summary>
        /// Makes sure that all parts reset their ground contact status.
        /// This can sometimes stick when airlaunching and in turn cause issues.
        /// </summary>
        /// <param name="vessel"></param>
        private static void ResetGroundContact(Vessel vessel)
        {
            if (vessel.parts == null) return;

            for (int index = vessel.parts.Count - 1; index >= 0; --index)
            {
                var part = vessel.parts[index];
                part.GroundContact = false;   // For stock

                if (part.Modules.Contains("KSPWheelBase"))
                {
                    // With KSPWheel the landing gear status may not get properly reset on airlaunch
                    var pm = part.Modules["KSPWheelBase"];
                    pm.Fields.SetValue("grounded", false);
                }
            }
        }
    }
}
