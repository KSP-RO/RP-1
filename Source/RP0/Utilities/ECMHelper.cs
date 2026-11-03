using System.Collections.Generic;

namespace RP0
{
    internal static class ECMHelper
    {
        public static int FindUnlockCost(List<AvailablePart> availableParts)
        {
            return (int)RealFuels.EntryCostManager.Instance.EntryCostForParts(availableParts);
        }

        public static string GetEcmNameFromPartModule(PartModule pm)
        {
            if (pm is ProceduralAvionics.ModuleProceduralAvionics pa)
                return GetEcmNameFromModuleProceduralAvionics(pa);

            if (pm is RealFuels.ModuleEngineConfigsBase)
                return GetEcmNameFromModuleEngineConfigsBase(pm);

            if (pm is RealFuels.Tanks.ModuleFuelTanks)
                return GetEcmNameFromModuleFuelTanks(pm);
            
            if (pm is RealAntennas.ModuleRealAntenna ra)
                return GetEcmNameFromModuleRealAntenna(ra);

            if (pm is ROLib.ModuleROSolar solar)
                return GetEcmNameFromModuleROSolar(solar);

            return null;
        }

        internal static string GetEcmNameFromModuleProceduralAvionics(ProceduralAvionics.ModuleProceduralAvionics pm)
        {
            return ProceduralAvionics.ProceduralAvionicsTechManager.GetPartUpgradeName(pm.CurrentProceduralAvionicsConfig.name, pm.CurrentProceduralAvionicsTechNode);
        }

        internal static string GetEcmNameFromModuleEngineConfigsBase(PartModule pm)
        {
            return ((RealFuels.ModuleEngineConfigsBase)pm).configuration;
        }

        internal static string GetEcmNameFromModuleFuelTanks(PartModule pm)
        {
            PartUpgradeHandler.Upgrade upgrade = RealFuels.Tanks.ModuleFuelTanks.GetUpgradeForType((RealFuels.Tanks.ModuleFuelTanks)pm);
            return upgrade?.name;
        }

        internal static string GetEcmNameFromModuleRealAntenna(RealAntennas.ModuleRealAntenna pm)
        {
            return pm.RAAntenna.TechLevelInfo.name;
        }

        internal static string GetEcmNameFromModuleROSolar(ROLib.ModuleROSolar solar)
        {
            return $"solarTL{solar.TechLevel}";
        }

        /// <summary>
        /// Gets the setups that are currently selected in a Kerbalism Configure module.
        /// </summary>
        internal static IEnumerable<KERBALISM.ConfigureSetup> GetSelectedSetups(KERBALISM.Configure configure)
        {
            List<KERBALISM.ConfigureSetup> setups = configure.Setups();
            if (setups == null || configure.selected == null || !configure.enabled)
                yield break;

            foreach (KERBALISM.ConfigureSetup setup in setups)
            {
                if (configure.selected.Contains(setup.name))
                    yield return setup;
            }
        }

        /// <summary>
        /// Gets the ECM names of the experiments that a Kerbalism Configure setup installs.
        /// The ECM entry of an experiment is named Experiment-[experiment id].
        /// Experiments without an ECM entry are skipped.
        /// </summary>
        internal static IEnumerable<string> GetEcmNamesFromConfigureSetup(KERBALISM.Configure configure, KERBALISM.ConfigureSetup setup)
        {
            foreach (KERBALISM.ConfigureModule cm in setup.modules)
            {
                if (configure.Find_module(cm) is KERBALISM.Experiment experiment)
                {
                    string ecmName = RealFuels.Utilities.SanitizeName($"Experiment-{experiment.experiment_id}");
                    if (RealFuels.EntryCostDatabase.GetHolder(ecmName) != null)
                        yield return ecmName;
                }
            }
        }
    }
}
