using System.Collections.Generic;
using ROUtils.DataTypes;

namespace RP0.Leaders
{
    public class LeaderUtils
    {
        private static Boxed<double> _boxedDouble = new Boxed<double>();

        public static double GetResearchRateEffect(NodeType type, string nodeID)
        {
            _boxedDouble.value = 1d;
            SCMEvents.ApplyResearchRateMultiplier.Fire(_boxedDouble, type, nodeID);
            return _boxedDouble.value;
        }

        public static double GetPartEffectiveCostEffect(IEnumerable<string> tags)
        {
            _boxedDouble.value = 1d;
            SCMEvents.ApplyPartEffectiveCostMultiplier.Fire(_boxedDouble, tags);
            return _boxedDouble.value;
        }

        public static double GetGlobalEffectiveCostEffect(IEnumerable<string> tags, Dictionary<string, double> resources)
        {
            _boxedDouble.value = 1d;
            SCMEvents.ApplyGlobalEffectiveCostMultiplier.Fire(_boxedDouble, tags, resources);
            return _boxedDouble.value;
        }
    }
}
