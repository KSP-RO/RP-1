using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RP0
{
    public static class KSCSwitcherInterop
    {
        private static bool? _isKSCSwitcherInstalled = null;
        private static FieldInfo _fiKSCSwLastSite;
        private static FieldInfo _fiKSCSwDefaultSite;
        private static FieldInfo _fiKSCSwSitesList;
        private static MethodInfo _miKSCSwGetSiteByName;
        private static MethodInfo _miKSCSwSetSiteAndResetCamera;
        private static Type _tLastKSC;
        private static FieldInfo _fiLastKSCLastSite;
        private static object _kscLoaderInstance;    // this is a static singleton
        private static object _sites;                // instance field in KSCLoader instance but in practice is never changed after parent object creation
        private static Dictionary<string, string> _groundStationNameDict = new Dictionary<string, string>();

        public const string LegacyDefaultKscId = "Stock";
        public const string DefaultKscId = "us_cape_canaveral";

        public static Type LastKSCType => _tLastKSC;

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
                         var fiKSCSwInstance = t?.GetField("instance", BindingFlags.Public | BindingFlags.Static);
                        _kscLoaderInstance = fiKSCSwInstance?.GetValue(null);
                        var fiKSCSwSites = t?.GetField("Sites", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                        _sites = fiKSCSwSites.GetValue(_kscLoaderInstance);

                        t = a.GetType("regexKSP.KSCSiteManager");
                        _fiKSCSwLastSite = t?.GetField("lastSite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                        _fiKSCSwDefaultSite = t?.GetField("defaultSite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                        _fiKSCSwSitesList = t?.GetField("Sites", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                        _miKSCSwGetSiteByName = t?.GetMethod("GetSiteByName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                        Type tSwitcher = a.GetType("regexKSP.KSCSwitcher");
                        _miKSCSwSetSiteAndResetCamera = tSwitcher?.GetMethod("SetSiteAndResetCamera", BindingFlags.Public | BindingFlags.Static);
                        _tLastKSC = a.GetType("regexKSP.LastKSC");
                        _fiLastKSCLastSite = _tLastKSC?.GetField("lastSite", BindingFlags.Public | BindingFlags.Instance);

                        if (_kscLoaderInstance == null || _sites == null || _fiKSCSwLastSite == null || _fiKSCSwDefaultSite == null || _fiKSCSwSitesList == null ||
                            _miKSCSwGetSiteByName == null || _miKSCSwSetSiteAndResetCamera == null || _tLastKSC == null || _fiLastKSCLastSite == null)
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

            // check the Sites object (KSCSiteManager) for the lastSite, if "" then get defaultSite
            string lastSite = _fiKSCSwLastSite.GetValue(_sites) as string;

            if (lastSite == string.Empty)
                lastSite = _fiKSCSwDefaultSite.GetValue(_sites) as string;
            return lastSite;
        }

        public static string GetGroundStationForKSC(string kscName)
        {
            if (!IsKSCSwitcherInstalled) return null;

            if (!_groundStationNameDict.TryGetValue(kscName, out string groundStationName))
            {
                var cn = _miKSCSwGetSiteByName.Invoke(_sites, new object[] { kscName }) as ConfigNode;
                groundStationName = cn?.GetValue("groundStation");
                _groundStationNameDict[kscName] = groundStationName;
            }

            return groundStationName;
        }

        public static List<(string id, string displayName)> GetAvailableSites()
        {
            if (!IsKSCSwitcherInstalled) return null;

            var rawList = _fiKSCSwSitesList.GetValue(_sites) as List<ConfigNode>;
            if (rawList == null) return null;

            var result = new List<(string id, string displayName)>(rawList.Count);
            foreach (ConfigNode site in rawList)
            {
                string id = site.GetValue("name");
                if (string.IsNullOrEmpty(id)) continue;
                string displayName = site.GetValue("displayName");
                if (string.IsNullOrEmpty(displayName)) displayName = id;
                result.Add((id, displayName));
            }
            result.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static void SetLastKSCSite(string siteName)
        {
            if (!IsKSCSwitcherInstalled || string.IsNullOrEmpty(siteName)) return;
            _fiKSCSwLastSite.SetValue(_sites, siteName);
        }

        public static void AddLastKSCToProto(ProtoScenarioModule proto, string siteName)
        {
            if (!IsKSCSwitcherInstalled || string.IsNullOrEmpty(siteName) || proto == null) return;
            proto.moduleValues?.SetValue("LastLaunchSite", siteName, true);
            ScenarioModule moduleRef = proto.moduleRef;
            if (moduleRef != null && _fiLastKSCLastSite != null)
                _fiLastKSCLastSite.SetValue(moduleRef, siteName);
        }

        public static void ApplySite(string siteName)
        {
            if (!IsKSCSwitcherInstalled || string.IsNullOrEmpty(siteName) || _miKSCSwSetSiteAndResetCamera == null) return;
            var cn = _miKSCSwGetSiteByName.Invoke(_sites, new object[] { siteName }) as ConfigNode;
            if (cn == null) return;
            _miKSCSwSetSiteAndResetCamera.Invoke(null, new object[] { cn, false });
        }
    }
}
