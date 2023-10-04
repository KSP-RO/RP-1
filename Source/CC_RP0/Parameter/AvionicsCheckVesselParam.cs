using ContractConfigurator.Parameters;
using Contracts;

namespace ContractConfigurator.RP0
{
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

            // Save parameter options on a per-vessel basis
            node.AddValue("continuousControlRequired", continuousControlRequired);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            // Save parameter options on a per-vessel basis
            node.TryGetValue("continuousControlRequired", ref continuousControlRequired);
        }
        protected override void OnRegister()
        {
            base.OnRegister();

            // We will only check the status of the parameter when input locks have changed
            GameEvents.onInputLocksModified.Add(OnInputLocksModified);
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.onInputLocksModified.Remove(OnInputLocksModified);
        }

        protected override string GetParameterTitle()
        {
            // Title will change to reflect the state of the parameter, with special considerations if we
            // need the user to not lose control at all during flight
            return continuousControlRequired ?
                controlHasLapsed ? $"Maintain sufficient avionics (failed, control was lost)"
                    : $"Maintain sufficient avionics (do not lose control)"
                : $"Have sufficient avionics for control";
        }

        private void OnInputLocksModified(GameEvents.FromToAction<ControlTypes, ControlTypes> data)
        {
            // Detect if a control lock is active
            bool controlIsLocked = InputLockManager.GetControlLock("RP0ControlLocker") != 0;

            // Keeps track if we've ever lost control
            controlHasLapsed |= controlIsLocked;

            // Differing logic depending on parameter properties
            parameterIsSatisified = continuousControlRequired ? !controlHasLapsed && !controlIsLocked : !controlIsLocked;

            // Refresh title in contracts screen in case we've had a lapse of control with continousControlRequired true
            GetTitle();

            // Have CC re-evaluate the parameter state (will call VesselMeetsCondition() internally)
            CheckVessel(FlightGlobals.ActiveVessel);
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return parameterIsSatisified;
        }
    }
}