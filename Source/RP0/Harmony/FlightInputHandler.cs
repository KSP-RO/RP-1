using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(FlightInputHandler))]
    internal class PatchFlightInputHandler
    {
        /// <summary>
        /// Makes sure that throttle stays at 0 when repairs were done and vessel comes off rails.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch("SetLaunchCtrlState")]
        internal static void Postfix_SetLaunchCtrlState()
        {
            bool b = SpaceCenterManagement.Instance?.DoingVesselRepair ?? false;
            if (b)
            {
                FlightInputHandler.state.mainThrottle = 0;
            }
        }
    }
}
