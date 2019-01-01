using ContractConfigurator.Parameters;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class DownrangeDistance : VesselParameter
    {
        protected bool triggered = false;
        protected double distance = 0;
        protected double curDist = 0;
        protected double markLatitude, markLongitude;

        private float lastUpdate = 0.0f;
        private const float UPDATE_FREQUENCY = 1f;

        public DownrangeDistance() : base(null)
        {
        }

        public DownrangeDistance(string title, double distance) : base(title)
        {
            this.distance = distance;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("distance", distance);
            node.AddValue("markLatitude", markLatitude);
            node.AddValue("markLongitude", markLongitude);
            node.AddValue("triggered", triggered);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            node.TryGetValue("distance", ref distance);
            node.TryGetValue("markLatitude", ref markLatitude);
            node.TryGetValue("markLongitude", ref markLongitude);
            node.TryGetValue("triggered", ref triggered);
        }

        protected override string GetParameterTitle()
        {
            if (FlightGlobals.ActiveVessel == null)
            {
                return $"Must achieve a downrange distance of at least {(distance / 1000.0):N0} km";
            }
            else
            {
                return $"Downrange distance {(curDist / 1000.0):N0} / {(distance / 1000.0):N0} km";
            }
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (!triggered) return false;

            // The following distance calculation code is from MechJebModuleFlightRecorder
            CelestialBody markBody = FlightGlobals.GetHomeBody();
            Vector3d markVector = markBody.GetSurfaceNVector(markLatitude, markLongitude);
            Vector3d vesselVector = vessel.CoMD - markBody.transform.position;
            curDist = markBody.Radius * Vector3d.Angle(markVector, vesselVector) * UtilMath.Deg2Rad;

            return curDist > distance;
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            GameEvents.onLaunch.Add(new EventData<EventReport>.OnEvent(OnLaunch));
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.onLaunch.Remove(new EventData<EventReport>.OnEvent(OnLaunch));
        }

        protected void OnLaunch(EventReport er)
        {
            markLatitude = FlightGlobals.ActiveVessel.latitude;
            markLongitude = FlightGlobals.ActiveVessel.longitude;
            triggered = true;
        }

        protected override void OnUpdate()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            base.OnUpdate();

            if (Time.fixedTime - lastUpdate > UPDATE_FREQUENCY)
            {
                lastUpdate = Time.fixedTime;

                CheckVessel(v);
                
                // Force a call to GetTitle to update the contracts app
                GetTitle();
            }
        }
    }
}
