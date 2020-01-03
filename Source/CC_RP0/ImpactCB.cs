using ContractConfigurator.Parameters;
using System.Collections.Generic;
using UnityEngine;
using Contracts;

namespace ContractConfigurator.RP0
{
    public class ImpactCBFactory : ParameterFactory
    {
        protected double minSrfVel;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<double>(configNode, "minSrfVel", x => minSrfVel = x, this, 0, x => Validation.GE(x, 0));
            valid &= ConfigNodeUtil.ParseValue(configNode, "targetBody", x => _targetBody = x, this, (CelestialBody)null);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new ImpactCB(title, minSrfVel, _targetBody);
        }
    }

    public class ImpactCB : VesselParameter
    {
        private const int VelQueueSize = 50;

        protected double minSrfVel = 0;

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

            Debug.Log($"[ImpactCB] VesselMeetsCondition vel: {srfVelQueue.Peek()}; isDestroyed: {isDestroyed}; isCorrectBody: {isCorrectBody}; radarAltitude: {vessel.radarAltitude}");

            return isDestroyed && isCorrectBody && isValidVel && isValidAlt;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("minSrfVel", minSrfVel);
            node.AddValue("targetBody", targetBody.name);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            node.TryGetValue("minSrfVel", ref minSrfVel);
            targetBody = ConfigNodeUtil.ParseValue<CelestialBody>(node, "targetBody", null);
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            GameEvents.onCollision.Add(new EventData<EventReport>.OnEvent(OnVesselAboutToBeDestroyed));
            GameEvents.onCrash.Add(new EventData<EventReport>.OnEvent(OnVesselAboutToBeDestroyed));
            GameEvents.onVesselDestroy.Add(new EventData<Vessel>.OnEvent(OnVesselDestroy));
            GameEvents.onVesselWillDestroy.Add(new EventData<Vessel>.OnEvent(OnVesselDestroy));
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.onCollision.Remove(new EventData<EventReport>.OnEvent(OnVesselAboutToBeDestroyed));
            GameEvents.onCrash.Remove(new EventData<EventReport>.OnEvent(OnVesselAboutToBeDestroyed));
            GameEvents.onVesselDestroy.Remove(new EventData<Vessel>.OnEvent(OnVesselDestroy));
            GameEvents.onVesselWillDestroy.Remove(new EventData<Vessel>.OnEvent(OnVesselDestroy));
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
