using ContractConfigurator.Parameters;
using System.Collections.Generic;
using RP0;

namespace ContractConfigurator.RP0
{
    public class ImpactCB : VesselParameter
    {
        private const int VelQueueSize = 50;

        protected double minSrfVel { get; set; }

        private Queue<double> srfVelQueue = new Queue<double>(VelQueueSize);
        private Dictionary<Vessel, bool> destroyedVessels = new Dictionary<Vessel, bool>();

        public ImpactCB() : base(null)
        {
        }

        public ImpactCB(string title, double minSrfVel, CelestialBody targetBody) : base(title)
        {
            this.minSrfVel = minSrfVel;
            this.targetBody = targetBody;
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            bool isDestroyed = destroyedVessels.ContainsKey(vessel) || vessel.state == Vessel.State.DEAD;
            bool isCorrectBody = vessel.mainBody == targetBody;
            bool isValidVel = srfVelQueue.Count > 0 && srfVelQueue.Peek() >= minSrfVel;
            bool isValidAlt = vessel.radarAltitude < 100;

            RP0Debug.Log($"[ImpactCB] VesselMeetsCondition vel: {(srfVelQueue.Count > 0 ? srfVelQueue.Peek() : 0)}; isDestroyed: {isDestroyed}; isCorrectBody: {isCorrectBody}; radarAltitude: {vessel.radarAltitude}");

            return isDestroyed && isCorrectBody && isValidVel && isValidAlt;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("minSrfVel", minSrfVel);
            if (targetBody != null)    // to prevent an exception being thrown due to a mistake that made it into previous release
            {
                node.AddValue("targetBody", targetBody.name);
            }
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            minSrfVel = ConfigNodeUtil.ParseValue<double>(node, "minSrfVel");
            targetBody = ConfigNodeUtil.ParseValue<CelestialBody>(node, "targetBody", null);
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            GameEvents.onCollision.Add(OnVesselAboutToBeDestroyed);
            GameEvents.onCrash.Add(OnVesselAboutToBeDestroyed);
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);
            GameEvents.onVesselWillDestroy.Add(OnVesselDestroy);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.onCollision.Remove(OnVesselAboutToBeDestroyed);
            GameEvents.onCrash.Remove(OnVesselAboutToBeDestroyed);
            GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
            GameEvents.onVesselWillDestroy.Remove(OnVesselDestroy);
        }

        protected override void OnUpdate()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            base.OnUpdate();

            if (srfVelQueue.Count == VelQueueSize) srfVelQueue.Dequeue();
            srfVelQueue.Enqueue(v.srfSpeed);
        }

        protected virtual void OnVesselAboutToBeDestroyed(EventReport report)
        {
            Vessel v = report.origin.vessel;
            if (v == null) return;

            destroyedVessels[v] = true;
            CheckVessel(v);
        }

        protected virtual void OnVesselDestroy(Vessel v)
        {
            destroyedVessels[v] = true;
            CheckVessel(v);
        }
    }
}
