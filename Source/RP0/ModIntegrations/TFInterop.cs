using System;
using System.Linq;
using System.Reflection;

namespace RP0
{
    public static class TFInterop
    {
        private static bool? _isTestFlightInstalled = null;
        private static bool? _hasSupportForReset = null;
        private static Assembly _assembly;
        private static MethodInfo _miGetVesselStatus;
        private static MethodInfo _miResetAllFailuresOnVessel;
        private static MethodInfo _miResetAllRunTimesOnVessel;

        public static bool IsTestFlightInstalled
        {
            get
            {
                EnsureReflectionInitialized();
                return _isTestFlightInstalled.Value;
            }
        }
        public static bool HasSupportForReset
        {
            get
            {
                EnsureReflectionInitialized();
                return _hasSupportForReset.Value;
            }
        }

        public static bool VesselHasFailedParts(Vessel v)
        {
            if (v == null) return false;

            EnsureReflectionInitialized();
            var res = (int)_miGetVesselStatus.Invoke(null, new object[] { v });
            // 0 = OK, 1 = Has failure, -1 = Could not find TestFlight Core on Part
            return res > 0;
        }

        public static void ResetAllFailures(Vessel v)
        {
            if (v == null) return;

            EnsureReflectionInitialized();
            _miResetAllFailuresOnVessel.Invoke(null, new object[] { v });
            _miResetAllRunTimesOnVessel.Invoke(null, new object[] { v });
        }

        private static void EnsureReflectionInitialized()
        {
            if (_isTestFlightInstalled.HasValue) return;

            _assembly = AssemblyLoader.loadedAssemblies.FirstOrDefault((AssemblyLoader.LoadedAssembly la) => string.Equals(la.name, "TestFlightCore", StringComparison.OrdinalIgnoreCase))?.assembly;
            _isTestFlightInstalled = _assembly != null;
            _hasSupportForReset = false;

            if (_isTestFlightInstalled.Value)
            {
                var type = _assembly.GetType("TestFlightCore.TestFlightInterface");
                _miGetVesselStatus = type.GetMethod("GetVesselStatus", BindingFlags.Public | BindingFlags.Static);
                _miResetAllFailuresOnVessel = type.GetMethod("ResetAllFailuresOnVessel", BindingFlags.Public | BindingFlags.Static);
                _miResetAllRunTimesOnVessel = type.GetMethod("ResetAllRunTimesOnVessel", BindingFlags.Public | BindingFlags.Static);
                _hasSupportForReset = _miResetAllRunTimesOnVessel != null;
            }
        }
    }
}
