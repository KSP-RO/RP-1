using HarmonyLib;

namespace RP0.Harmony
{
    /// <summary>
    /// Strips flight-only PartModule state (engine wear, TestFlight bookkeeping, ...) out of the ship
    /// config right before it is written to a .craft file. This state is deliberately carried over to
    /// the editor by RP-1's vessel recovery, but must not leak into craft files: see <see cref="CraftFileSanitizer"/>.
    ///
    /// Only the stock craft-file writers are patched, and they write <see cref="ShipConstruction.ShipConfig"/>.
    /// That node is NOT discarded after saving - it is the live editor ship node, reused for undo/restore,
    /// flight revert and re-launch (<c>ShipConstruct.LoadShip</c>) - so we must not mutate it in place.
    /// Instead we temporarily swap in a sanitized copy for the duration of the write and restore the
    /// original afterwards. The build list / warehouse serialization goes through <c>ShipConstruct.SaveShip()</c>
    /// directly and is intentionally left untouched so engine refurbishment keeps being costed.
    /// </summary>
    [HarmonyPatch(typeof(ShipConstruction))]
    internal class PatchShipConstruction
    {
        // Editor "Save" / "Overwrite" craft button.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ShipConstruction.SaveShipToPath), new[] { typeof(string), typeof(string) })]
        internal static void Prefix_SaveShipToPath_Local(out ConfigNode __state) => __state = SwapInSanitizedConfig();

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(ShipConstruction.SaveShipToPath), new[] { typeof(string), typeof(string) })]
        internal static void Finalizer_SaveShipToPath_Local(ConfigNode __state) => RestoreConfig(__state);

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ShipConstruction.SaveShipToPath), new[] { typeof(string), typeof(EditorFacility), typeof(string), typeof(string) })]
        internal static void Prefix_SaveShipToPath_Full(out ConfigNode __state) => __state = SwapInSanitizedConfig();

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(ShipConstruction.SaveShipToPath), new[] { typeof(string), typeof(EditorFacility), typeof(string), typeof(string) })]
        internal static void Finalizer_SaveShipToPath_Full(ConfigNode __state) => RestoreConfig(__state);

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ShipConstruction.SaveShip), new[] { typeof(string) })]
        internal static void Prefix_SaveShip(out ConfigNode __state) => __state = SwapInSanitizedConfig();

        [HarmonyFinalizer]
        [HarmonyPatch(nameof(ShipConstruction.SaveShip), new[] { typeof(string) })]
        internal static void Finalizer_SaveShip(ConfigNode __state) => RestoreConfig(__state);

        /// <summary>
        /// Replaces <see cref="ShipConstruction.ShipConfig"/> with a sanitized copy so the .craft file on
        /// disk is clean, while leaving the in-memory node untouched. Returns the original node for the
        /// finalizer to restore, or null when there was nothing to swap.
        /// </summary>
        private static ConfigNode SwapInSanitizedConfig()
        {
            ConfigNode original = ShipConstruction.ShipConfig;
            if (original == null)
                return null;

            ConfigNode sanitized = original.CreateCopy();
            CraftFileSanitizer.StripFlightData(sanitized);
            ShipConstruction.ShipConfig = sanitized;
            return original;
        }

        // Runs whether or not the save threw, so the shared ShipConfig is always restored.
        private static void RestoreConfig(ConfigNode original)
        {
            if (original != null)
                ShipConstruction.ShipConfig = original;
        }
    }
}
