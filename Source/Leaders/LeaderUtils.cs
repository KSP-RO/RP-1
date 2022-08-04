using System.Collections.Generic;
using KerbalConstructionTime;
using RP0.DataTypes;

namespace RP0.Leaders
{
    public class LeaderUtils
    {
        private static Boxed<double> _boxedDouble = new Boxed<double>();

        public static double GetResearchRateEffect(NodeType type, string nodeID)
        {
            _boxedDouble.value = 1d;
            KCTEvents.ApplyResearchRateMultiplier.Fire(_boxedDouble, type, nodeID);
            return _boxedDouble.value;
        }

        public static double GetPartEffectiveCostEffect(IEnumerable<string> tags, Dictionary<string, double> resources, string partName)
        {
            _boxedDouble.value = 1d;
            KCTEvents.ApplyPartEffectiveCostMultiplier.Fire(_boxedDouble, tags, resources, partName);
            return _boxedDouble.value;
        }

        public static double GetGlobalEffectiveCostEffect(IEnumerable<string> tags, Dictionary<string, double> resources)
        {
            _boxedDouble.value = 1d;
            KCTEvents.ApplyGlobalEffectiveCostMultiplier.Fire(_boxedDouble, tags, resources);
            return _boxedDouble.value;
        }
    }
}
