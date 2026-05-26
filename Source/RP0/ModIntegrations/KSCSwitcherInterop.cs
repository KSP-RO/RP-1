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

        private static List<(string id, string displayName, double latitude, double longitude)> _allSites = null;
        public static List<(string id, string displayName, double latitude, double longitude)> AllSites
        {
            get
            {
                if (_allSites == null)
                {
                    if (!IsKSCSwitcherInstalled) return null;

                    var rawList = _fiKSCSwSitesList.GetValue(_sites) as List<ConfigNode>;
                    if (rawList == null) return null;

                    _allSites = new List<(string id, string displayName, double latitude, double longitude)>(rawList.Count);
                    foreach (ConfigNode site in rawList)
                    {
                        string id = site.GetValue("name");
                        if (string.IsNullOrEmpty(id)) continue;
                        string displayName = site.GetValue("displayName");
                        if (string.IsNullOrEmpty(displayName)) displayName = id;
                        ConfigNode PQSCity = site.GetNode("PQSCity");
                        double.TryParse(PQSCity?.GetValue("latitude"), out double latitude);
                        double.TryParse(PQSCity?.GetValue("longitude"), out double longitude);

                        _allSites.Add((id, displayName, latitude, longitude));
                    }
                    _allSites.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
                }
                return _allSites;
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

        public static string GetSiteDisplayName(string siteName)
        {
            if (!IsKSCSwitcherInstalled || string.IsNullOrEmpty(siteName)) return siteName;

            string displayName = AllSites.Find(s => s.id == siteName).displayName;
            return string.IsNullOrEmpty(displayName) ? siteName : displayName;
        }

        public static (double latitude, double longitude) GetSiteLatLong(string siteName)
        {
            if (!IsKSCSwitcherInstalled || string.IsNullOrEmpty(siteName)) return (0d, 0d);

            var site = AllSites.Find(s => s.id == siteName);
            return (site.latitude, site.longitude);
        }

        public static double GreatCircleDistance(string siteA, string siteB)
        {
            (double latA, double longA) = GetSiteLatLong(siteA);
            (double latB, double longB) = GetSiteLatLong(siteB);
            CelestialBody home = FlightGlobals.GetHomeBody();

            Vector3d NVectorA = home.GetRelSurfaceNVector(latA, longA);
            Vector3d NVectorB = home.GetRelSurfaceNVector(latB, longB);

            return home.Radius * Math.Acos(Vector3d.Dot(NVectorA, NVectorB));
        }
    }
}
