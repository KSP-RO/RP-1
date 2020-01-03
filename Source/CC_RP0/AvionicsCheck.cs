using ContractConfigurator.Parameters;
using Contracts;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class AvionicsCheckFactory : ParameterFactory
    {
        protected bool continuousControlRequired;

        public override bool Load(ConfigNode configNode)
        {
            bool valid = base.Load(configNode);

            valid &= ConfigNodeUtil.ParseValue<bool>(configNode, "continuousControlRequired", x => continuousControlRequired = x, this, false);

            return valid;
        }

        public override ContractParameter Generate(Contract contract)
        {
            return new AvionicsCheckParameter(title, continuousControlRequired);
        }
    }
    public class AvionicsCheckParameter : VesselParameter
    {
        protected bool parameterIsSatisified = true;
        protected bool controlHasLapsed = false;
        protected bool continuousControlRequired = false;

        public AvionicsCheckParameter() : base(null) { }

        public AvionicsCheckParameter(string title, bool continuousControlRequired) : base(title)
        {
            this.continuousControlRequired = continuousControlRequired;
            this.title = GetParameterTitle();
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("continuousControlRequired", continuousControlRequired);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            node.TryGetValue("continuousControlRequired", ref continuousControlRequired);
        }
        protected override void OnRegister()
        {
            base.OnRegister();
            GameEvents.onInputLocksModified.Add(OnInputLocksModified);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.onInputLocksModified.Remove(OnInputLocksModified);
        }
        protected override string GetParameterTitle()
        {
            return continuousControlRequired ? 
                controlHasLapsed ? $"Maintain sufficient avionics (failed, control was lost)"
                    : $"Maintain sufficient avionics (do not lose control)" 
                : $"Have sufficient avionics for control";
        }

        private void OnInputLocksModified(GameEvents.FromToAction<ControlTypes, ControlTypes> data)
        {
            bool controlIsLocked = InputLockManager.GetControlLock("RP0ControlLocker") != 0;

            controlHasLapsed |= controlIsLocked;
            parameterIsSatisified = continuousControlRequired ? !controlHasLapsed && !controlIsLocked : !controlIsLocked;

            GetTitle();

            CheckVessel(FlightGlobals.ActiveVessel);
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return parameterIsSatisified;
        }
    }
}
