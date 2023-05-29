using System;
using System.Collections;
using UniLinq;
using System.Reflection;
using UnityEngine;

namespace RP0
{
    /// <summary>
    /// This class will lock controls if and only if avionics requirements exist and are not met
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class ControlLocker : MonoBehaviour
    {
        internal const string ModTag = "[RP-1 ControlLocker]";
        private const ControlTypes Lockmask = ControlTypes.YAW | ControlTypes.PITCH | ControlTypes.ROLL | ControlTypes.SAS |
                                              ControlTypes.THROTTLE | ControlTypes.WHEEL_STEER | ControlTypes.WHEEL_THROTTLE;

        public Vessel Vessel = null;
        private ControlLockerUtils.LockLevel _oldLockLevel = ControlLockerUtils.LockLevel.Unlocked;
        private bool _requested = false;
        private bool _onRails = false;
        private ControlLockerUtils.LockLevel _cachedLockResult = ControlLockerUtils.LockLevel.Unlocked;
        private const string LockID = "RP0ControlLocker";
        private float _maxMass, _vesselMass;
        private bool _isLimitedByNonInterplanetary;
        private bool _isStartFinished;

        private const float UpdateFrequency = 1; // Default check interval

        private readonly ScreenMessage _message = new ScreenMessage("", 8f, ScreenMessageStyle.UPPER_CENTER);

        // For locking MJ.
        private static bool _isFirstLoad = true;
        private static MethodInfo _getMasterMechJeb = null;
        private static PropertyInfo _mjDeactivateControl = null;
        private object _masterMechJeb = null;

        private void Awake()
        {
            if (!_isFirstLoad) return;
            _isFirstLoad = false;

            if (AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == "MechJeb2") is var mechJebAssembly &&
               Type.GetType("MuMech.MechJebCore, MechJeb2") is Type mechJebCore &&
               Type.GetType("MuMech.VesselExtensions, MechJeb2") is Type mechJebVesselExtensions)
            {
                _mjDeactivateControl = mechJebCore.GetProperty("DeactivateControl", BindingFlags.Public | BindingFlags.Instance);
                _getMasterMechJeb = mechJebVesselExtensions.GetMethod("GetMasterMechJeb", BindingFlags.Public | BindingFlags.Static);
            }
            if (_mjDeactivateControl != null && _getMasterMechJeb != null)
                Debug.Log($"{ModTag} MechJeb methods found");
            else
                Debug.Log($"{ModTag} MJ assembly or methods NOT found");
        }

        private void Start()
        {
            GameEvents.onVesselWasModified.Add(OnVesselModifiedHandler);
            GameEvents.onVesselSwitching.Add(OnVesselSwitchingHandler);
            GameEvents.onVesselGoOnRails.Add(OnRailsHandler);
            GameEvents.onVesselGoOffRails.Add(OffRailsHandler);
            Vessel = FlightGlobals.ActiveVessel;
            if (Vessel && Vessel.loaded) Vessel.OnPostAutopilotUpdate += FlightInputModifier;
            StartCoroutine(CheckLockCR());
        }

        protected void OnVesselSwitchingHandler(Vessel v1, Vessel v2)
        {
            // Apply only when switching does not result in new scene load.
            if (v1 && v2 && v2.loaded) v2.OnPostAutopilotUpdate += FlightInputModifier;
        }

        protected void OnVesselModifiedHandler(Vessel v) => _requested = _isStartFinished;    // Other mods (looking at you B9PS!) can fire this event before everything has finished initializing. Need to ignore those until we have done our own first avionics check.
        private void OnRailsHandler(Vessel v) => _onRails = true;
        private void OffRailsHandler(Vessel v)
        {
            _onRails = false;
            if (!CheatOptions.InfiniteElectricity && ControlLockerUtils.ShouldLock(Vessel.Parts, true, out _, out _, out _) != ControlLockerUtils.LockLevel.Unlocked)
                DisableAutopilot();
        }

        void FlightInputModifier(FlightCtrlState state)
        {
            if (_oldLockLevel != ControlLockerUtils.LockLevel.Unlocked)
            {
                state.X = state.Y = 0;                                      // Disable X/Y translation
                if (_oldLockLevel == ControlLockerUtils.LockLevel.Locked)   // Allow Z(fwd/ reverse) only if science core allows it
                    state.Z = 0;
                state.yaw = state.pitch = state.roll = 0;                   // Disable roll control
            }
        }

        public ControlLockerUtils.LockLevel ShouldLock()
        {
            // if we have no active vessel, undo locks
            if (Vessel is null)
                return _cachedLockResult = ControlLockerUtils.LockLevel.Unlocked;
            if (_requested)
                _cachedLockResult = CheatOptions.InfiniteElectricity ? ControlLockerUtils.LockLevel.Unlocked : ControlLockerUtils.ShouldLock(Vessel.Parts, true, out _maxMass, out _vesselMass, out _isLimitedByNonInterplanetary);
            _requested = false;
            return _cachedLockResult;
        }

        private IEnumerator CheckLockCR()
        {
            const int maxFramesWaited = 250;
            int i = 0;
            do
            {
                yield return new WaitForFixedUpdate();
            } while ((FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.packed) && i++ < maxFramesWaited);

            _isStartFinished = true;

            while (HighLogic.LoadedSceneIsFlight)
            {
                yield return new WaitForSeconds(UpdateFrequency);
                _cachedLockResult = CheatOptions.InfiniteElectricity ? ControlLockerUtils.LockLevel.Unlocked : ControlLockerUtils.ShouldLock(Vessel.Parts, true, out _maxMass, out _vesselMass, out _isLimitedByNonInterplanetary);
            }
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready)
            {
                InputLockManager.RemoveControlLock(LockID);
                return;
            }
            if (Vessel != FlightGlobals.ActiveVessel)
            {
                Vessel = FlightGlobals.ActiveVessel;
                _masterMechJeb = null;
            }

            ControlLockerUtils.LockLevel lockLevel = ShouldLock();

            if (_isLimitedByNonInterplanetary)
                GameplayTips.Instance.ShowInterplanetaryAvionicsReminder();

            if (lockLevel != _oldLockLevel)
            {
                if (_oldLockLevel != ControlLockerUtils.LockLevel.Unlocked)
                {
                    InputLockManager.RemoveControlLock(LockID);
                    _message.message = "Avionics: Unlocking Controls";
                }
                else
                {
                    InputLockManager.SetControlLock(Lockmask, LockID);
                    if (!_onRails) 
                        DisableAutopilot();
                    _message.message = $"Insufficient Avionics, Locking Controls (supports {_maxMass:N3}t, vessel {_vesselMass:N3}t)";
                }
                ScreenMessages.PostScreenMessage(_message);
                FlightLogger.fetch.LogEvent(_message.message);
                _oldLockLevel = lockLevel;
            }

            if (_masterMechJeb == null && _getMasterMechJeb != null)
            {
                _masterMechJeb = _getMasterMechJeb.Invoke(null, new object[] { FlightGlobals.ActiveVessel });
            }
            if (_masterMechJeb != null && _mjDeactivateControl != null)
            {
                // Update MJ every tick, to make sure it gets correct state after separation / docking.
                _mjDeactivateControl.SetValue(_masterMechJeb, lockLevel != ControlLockerUtils.LockLevel.Unlocked, index: null);
            }
        }

        public void OnDestroy()
        {
            InputLockManager.RemoveControlLock(LockID);
            GameEvents.onVesselWasModified.Remove(OnVesselModifiedHandler);
            GameEvents.onVesselSwitching.Remove(OnVesselSwitchingHandler);
            GameEvents.onVesselGoOnRails.Remove(OnRailsHandler);
            GameEvents.onVesselGoOffRails.Remove(OffRailsHandler);
        }

        private void DisableAutopilot()
        {
            Vessel.Autopilot.Disable();
            Vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
        }
    }
}
