using System;
using UnityEngine;
using System.Linq;
using System.Reflection;

namespace KerbalConstructionTime
{
    internal static class ECMHelper
    {
        internal static Assembly _rp0Assembly;
        internal static Type _ProceduralAvionicsTechManagerType;
        internal static Type _ProceduralAvionicsTechNodeType;
        internal static Type _ModuleProceduralAvionicsType;

        public static string GetEcmNameFromPartModule(PartModule pm)
        {
            if (!CheckLoadEcmRelatedTypes())
            {
                // necessary asseemblies and types could not be loaded
                return null;
            }

            if (_ModuleProceduralAvionicsType.IsInstanceOfType(pm))
                return GetEcmNameFromModuleProceduralAvionics(pm);

            if (pm is RealFuels.ModuleEngineConfigsBase)
                return GetEcmNameFromModuleEngineConfigsBase(pm);

            if (pm is RealFuels.Tanks.ModuleFuelTanks)
                return GetEcmNameFromModuleFuelTanks(pm);

            return null;
        }

        internal static string GetEcmNameFromModuleProceduralAvionics(PartModule pm)
        {
            // we want the value of:
            // string ProceduralAvionicsTechManager.GetEcmName(pm.CurrentProceduralAvionicsConfig.name, pm.CurrentProceduralAvionicsTechNode);
            // but since we don't have a hard reference to RP0 (and it would be circular refs), we have to use reflection calls to get it

            string currentProceduralAvionicsConfigName = null;
            object currentProceduralAvionicsTechNode = null;

            // get the current config object, use that to get the config name
            PropertyInfo pi = pm.GetType().GetProperty("CurrentProceduralAvionicsConfig");
            if (pi != null)
            {
                object currentProceduralAvionicsConfig = pi.GetValue(pm);
                if (currentProceduralAvionicsConfig != null)
                {
                    FieldInfo fi = currentProceduralAvionicsConfig.GetType().GetField("name");
                    if (fi != null)
                    {
                        currentProceduralAvionicsConfigName = (string)fi.GetValue(currentProceduralAvionicsConfig);
                    }
                }
            }

            // get the current tech node
            pi = pm.GetType().GetProperty("CurrentProceduralAvionicsTechNode");
            if (pi != null)
            {
                currentProceduralAvionicsTechNode = pi.GetValue(pm);
            }

            if (currentProceduralAvionicsTechNode == null || currentProceduralAvionicsConfigName == null)
            {
                // we didn't get either the pm.CurrentProceduralAvionicsConfig.name or pm.CurrentProceduralAvionicsTechNode
                // property value, we can't make the GetEcmName call
                KCTDebug.LogError($"PartModule {pm.name} did not return valid CurrentProceduralAvionicsTechNode or CurrentProceduralAvionicsConfig.name values");
                return null;
            }
            var types = new[] { typeof(string), _ProceduralAvionicsTechNodeType };
            var mi = _ProceduralAvionicsTechManagerType.GetMethod("GetEcmName", BindingFlags.Static | BindingFlags.Public, null, types, null);
            if (mi != null)
            {
                var parameters = new object[] { currentProceduralAvionicsConfigName, currentProceduralAvionicsTechNode };
                string resultString = null;
                try
                {
                    resultString = (string)mi.Invoke(null, parameters);
                }
                catch (Exception ex)
                {
                    KCTDebug.LogError($"GetEcmName invoke failed for partmodule {pm.name}");
                    Debug.LogException(ex);
                }
                return resultString;
            }

            return null;
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

        internal static bool CheckLoadEcmRelatedTypes()
        {
            if (_rp0Assembly == null)
            {
                _rp0Assembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(la => string.Equals(la.name, "RP-0", StringComparison.OrdinalIgnoreCase))?.assembly;

                if (_rp0Assembly != null)
                {
                    _ProceduralAvionicsTechManagerType = _rp0Assembly.GetType("RP0.ProceduralAvionics.ProceduralAvionicsTechManager");
                    _ProceduralAvionicsTechNodeType = _rp0Assembly.GetType("RP0.ProceduralAvionics.ProceduralAvionicsTechNode");
                    _ModuleProceduralAvionicsType = _rp0Assembly.GetType("RP0.ProceduralAvionics.ModuleProceduralAvionics");
                }
            }
            return _rp0Assembly != null && _ProceduralAvionicsTechManagerType != null && _ProceduralAvionicsTechNodeType != null && _ModuleProceduralAvionicsType != null;
        }
    }
}
