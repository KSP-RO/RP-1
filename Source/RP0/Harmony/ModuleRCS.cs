using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ModuleRCS))]
    internal class PatchModuleRCS
    {
        private static bool _resetState = false;
        private static readonly FlightCtrlState _state = new FlightCtrlState();

        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        internal static void Prefix_Update(ModuleRCS __instance)
        {
            if (!HighLogic.LoadedSceneIsFlight || __instance.vessel == null || __instance.vessel != FlightGlobals.ActiveVessel)
                return;

            if (ControlLocker.Instance == null)
                return;

            var level = ControlLocker.Instance.ShouldLock();
            if (level == ControlLockerUtils.LockLevel.Unlocked)
                return;

            _resetState = true;
            _state.X = __instance.vessel.ctrlState.X;
            _state.Y = __instance.vessel.ctrlState.Y;
            _state.Z = __instance.vessel.ctrlState.Z;
            _state.pitch = __instance.vessel.ctrlState.pitch;
            _state.yaw = __instance.vessel.ctrlState.yaw;
            _state.roll = __instance.vessel.ctrlState.roll;
            _state.mainThrottle = __instance.vessel.ctrlState.mainThrottle;

            __instance.vessel.ctrlState.X = __instance.vessel.ctrlState.Y = __instance.vessel.ctrlState.pitch = __instance.vessel.ctrlState.yaw = __instance.vessel.ctrlState.roll = 0f;

            if (level == ControlLockerUtils.LockLevel.Axial)
            {
                if (__instance.vessel.ctrlState.Z < 0f)
                    __instance.vessel.ctrlState.Z = -1f;
                else if (__instance.vessel.ctrlState.Z > 0f)
                    __instance.vessel.ctrlState.Z = 1f;

                if (__instance.vessel.ctrlState.mainThrottle > 0f)
                    __instance.vessel.ctrlState.mainThrottle = 1f;
            }
            else
            {
                __instance.vessel.ctrlState.Z = 0f;
                __instance.vessel.ctrlState.mainThrottle = 0f;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        internal static void Postfix_Update(ModuleRCS __instance)
        {
            if (!_resetState)
                return;

            _resetState = false;
            __instance.vessel.ctrlState.X = _state.X;
            __instance.vessel.ctrlState.Y = _state.Y;
            __instance.vessel.ctrlState.Z = _state.Z;
            __instance.vessel.ctrlState.pitch = _state.pitch;
            __instance.vessel.ctrlState.yaw = _state.yaw;
            __instance.vessel.ctrlState.roll = _state.roll;
            __instance.vessel.ctrlState.mainThrottle = _state.mainThrottle;
        }
    }
}
