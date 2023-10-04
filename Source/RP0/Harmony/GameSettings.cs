using HarmonyLib;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(GameSettings))]
    internal class PatchGameSettings
    {
        internal static bool DVSet = false;
        internal static bool DELTAV_APP_ENABLED = true;
        internal static bool DELTAV_CALCULATIONS_ENABLED = true;

        public static void FixDV()
        {
            if (DVSet)
                return;

            DVSet = true;

            DELTAV_APP_ENABLED = GameSettings.DELTAV_APP_ENABLED;
            DELTAV_CALCULATIONS_ENABLED = GameSettings.DELTAV_CALCULATIONS_ENABLED;

            GameSettings.DELTAV_APP_ENABLED = false;
            GameSettings.DELTAV_CALCULATIONS_ENABLED = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("WriteCfg")]
        internal static void Prefix_WriteCfg()
        {   
            FixDV();
            
            GameSettings.DELTAV_APP_ENABLED = DELTAV_APP_ENABLED;
            GameSettings.DELTAV_CALCULATIONS_ENABLED = DELTAV_CALCULATIONS_ENABLED;
        }

        [HarmonyPostfix]
        [HarmonyPatch("WriteCfg")]
        internal static void Postfix_WriteCfg()
        {
            GameSettings.DELTAV_APP_ENABLED = false;
            GameSettings.DELTAV_CALCULATIONS_ENABLED = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("ParseCfg")]
        internal static void Postfix_ParseCfg()
        {
            FixDV();
        }

        [HarmonyPostfix]
        [HarmonyPatch("SetDefaultValues")]
        internal static void Postfix_SetDefaultValues()
        {
            FixDV();
        }
    }
}
