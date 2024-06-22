using ContractConfigurator;
using ROUtils.DataTypes;
using RP0.Requirements;
using Strategies;
using System.Collections.Generic;
using UniLinq;

namespace RP0.Leaders
{
    public static class LeaderUtils
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

        public static IEnumerable<StrategyConfigRP0> GetLeadersUnlockedByTech(string techID)
        {
            return GetAllLeaderStrategies()
                .Where(s => s.RequirementsBlock != null &&
                            (s.RequirementsBlock.Op is Any ||
                             s.RequirementsBlock.Op is All && s.RequirementsBlock.Reqs.Count == 1) &&
                            s.RequirementsBlock.ChildBlocks.Count == 0 &&
                            s.RequirementsBlock.Reqs.Any(r => !r.IsInverted &&
                                                              r is TechRequirement cr &&
                                                              cr.TechName == techID));
        }

        public static IEnumerable<StrategyConfigRP0> GetLeadersUnlockedByContract(ConfiguredContract cc)
        {
            return GetAllLeaderStrategies()
                .Where(s => s.RequirementsBlock != null &&
                            (s.RequirementsBlock.Op is Any ||
                             s.RequirementsBlock.Op is All && s.RequirementsBlock.Reqs.Count == 1) &&
                            s.RequirementsBlock.ChildBlocks.Count == 0 &&
                            s.RequirementsBlock.Reqs.Any(r => !r.IsInverted &&
                                                              r is Requirements.ContractRequirement cr &&
                                                              cr.ContractName == cc.contractType.name));
        }

        public static IEnumerable<StrategyConfigRP0> GetLeadersUnlockedByProgram(string programName)
        {
            return GetAllLeaderStrategies()
                .Where(s => s.RequirementsBlock != null &&
                            (s.RequirementsBlock.Op is Any ||
                             s.RequirementsBlock.Op is All && s.RequirementsBlock.Reqs.Count == 1) &&
                            s.RequirementsBlock.ChildBlocks.Count == 0 &&
                            s.RequirementsBlock.Reqs.Any(r => !r.IsInverted &&
                                                              r is ProgramRequirement pr &&
                                                              pr.ProgramName == programName));
        }

        public static IEnumerable<StrategyConfigRP0> GetAllUnlockedLeaders()
        {
            return GetAllLeaderStrategies().Where(s => s.IsUnlocked());
        }

        private static IEnumerable<StrategyConfigRP0> GetAllLeaderStrategies()
        {
            return StrategySystem.Instance.SystemConfig.Strategies
                .OfType<StrategyConfigRP0>()
                .Where(s => s.DepartmentName != "Programs");
        }
    }
}
