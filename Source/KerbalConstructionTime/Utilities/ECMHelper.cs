using System;
using UnityEngine;
using System.Linq;
using System.Reflection;

namespace KerbalConstructionTime
{
    internal static class ECMHelper
    {
        public static string GetEcmNameFromPartModule(PartModule pm)
        {
            if (pm is RP0.ProceduralAvionics.ModuleProceduralAvionics pa)
                return GetEcmNameFromModuleProceduralAvionics(pa);

            if (pm is RealFuels.ModuleEngineConfigsBase)
                return GetEcmNameFromModuleEngineConfigsBase(pm);

            if (pm is RealFuels.Tanks.ModuleFuelTanks)
                return GetEcmNameFromModuleFuelTanks(pm);

            return null;
        }

        internal static string GetEcmNameFromModuleProceduralAvionics(RP0.ProceduralAvionics.ModuleProceduralAvionics pm)
        {
            return RP0.ProceduralAvionics.ProceduralAvionicsTechManager.GetEcmName(pm.CurrentProceduralAvionicsConfig.name, pm.CurrentProceduralAvionicsTechNode);
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
    }
}
