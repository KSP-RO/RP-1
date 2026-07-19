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
    }
}
