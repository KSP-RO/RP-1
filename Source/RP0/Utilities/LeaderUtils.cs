using ContractConfigurator;
using RP0.Requirements;
using Strategies;
using System.Collections.Generic;
using System.Linq;

namespace RP0
{
    public static class LeaderUtils
    {
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
