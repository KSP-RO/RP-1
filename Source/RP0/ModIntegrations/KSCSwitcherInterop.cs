using System;
using System.Linq;
using System.Reflection;

namespace RP0
{
    public static class KSCSwitcherInterop
    {
        private static bool? _isKSCSwitcherInstalled = null;
        private static FieldInfo _fiKSCSwInstance;
        private static FieldInfo _fiKSCSwSites;
        private static FieldInfo _fiKSCSwLastSite;
        private static FieldInfo _fiKSCSwDefaultSite;

        public const string LegacyDefaultKscId = "Stock";
        public const string DefaultKscId = "us_cape_canaveral";

        public static bool IsKSCSwitcherInstalled
        {
            get
            {
                if (!_isKSCSwitcherInstalled.HasValue)
                {
                    Assembly a = AssemblyLoader.loadedAssemblies.FirstOrDefault(la => string.Equals(la.name, "KSCSwitcher", StringComparison.OrdinalIgnoreCase))?.assembly;
                    _isKSCSwitcherInstalled = a != null;
                    if (_isKSCSwitcherInstalled.Value)
                    {
                        Type t = a.GetType("regexKSP.KSCLoader");
                        _fiKSCSwInstance = t?.GetField("instance", BindingFlags.Public | BindingFlags.Static);
                        _fiKSCSwSites = t?.GetField("Sites", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                        t = a.GetType("regexKSP.KSCSiteManager");
                        _fiKSCSwLastSite = t?.GetField("lastSite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                        _fiKSCSwDefaultSite = t?.GetField("defaultSite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                        if (_fiKSCSwInstance == null || _fiKSCSwSites == null || _fiKSCSwLastSite == null || _fiKSCSwDefaultSite == null)
                        {
                            RP0Debug.LogError("Failed to bind to KSCSwitcher");
                            _isKSCSwitcherInstalled = false;
                        }
                    }
                }
                return _isKSCSwitcherInstalled.Value;
            }
        }

        public static string GetActiveRSSKSC()
        {
            if (!IsKSCSwitcherInstalled) return null;

            // get the LastKSC.KSCLoader.instance object
            // check the Sites object (KSCSiteManager) for the lastSite, if "" then get defaultSite

            object loaderInstance = _fiKSCSwInstance.GetValue(null);
            if (loaderInstance == null)
                return null;
            object sites = _fiKSCSwSites.GetValue(loaderInstance);
            string lastSite = _fiKSCSwLastSite.GetValue(sites) as string;

            if (lastSite == string.Empty)
                lastSite = _fiKSCSwDefaultSite.GetValue(sites) as string;
            return lastSite;
        }
    }
}
