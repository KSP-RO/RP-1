using RealFuels.Tanks;
using System.Collections.Generic;
using System.Reflection;

namespace RP0.ProceduralAvionics
{
    static class ProceduralAvionicsUtils
    {
        private static bool _enableLogging = true;

        private const string LogPrefix = "[ProcAvi] ";

        private static readonly FieldInfo _tanksDictField = typeof(ModuleFuelTanks).GetField("tanksDict", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

        public static Dictionary<string, FuelTank> GetTanks(ModuleFuelTanks rfPM) => (Dictionary<string, FuelTank>)_tanksDictField.GetValue(rfPM);

        public static void Log(params string[] message)
        {
            if (_enableLogging)
            {
                var builder = StringBuilderCache.Acquire();
                builder.Append(LogPrefix);
                foreach (string part in message)
                {
                    builder.Append(part);
                }
                RP0Debug.Log(builder.ToStringAndRelease());
            }
        }

        public static void Log(params object[] parts)
        {
            if (_enableLogging)
            {
                var builder = StringBuilderCache.Acquire();
                builder.Append(LogPrefix);
                foreach (object part in parts)
                {
                    builder.Append(part.ToString());
                }
                RP0Debug.Log(builder.ToStringAndRelease());
            }
        }
    }
}
