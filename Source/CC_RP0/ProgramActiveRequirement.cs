using RP0.Programs;
using System.Collections.Generic;
using System.Linq;

namespace ContractConfigurator.RP0
{
    public class ProgramActiveRequirement : ContractRequirement
    {
        protected List<string> programs;

        public override bool LoadFromConfig(ConfigNode configNode)
        {
            // Load base class
            bool valid = base.LoadFromConfig(configNode);

            // Check on active contracts too
            checkOnActiveContract = configNode.HasValue("checkOnActiveContract") ? checkOnActiveContract : true;

            valid &= ConfigNodeUtil.ParseValue(configNode, "program", x => programs = x, this, new List<string>());

            return valid;
        }

        public override void OnLoad(ConfigNode configNode)
        {
            programs = ConfigNodeUtil.ParseValue(configNode, "program", new List<string>());
        }

        public override void OnSave(ConfigNode configNode)
        {
            foreach (string p in programs)
            {
                configNode.AddValue("program", p);
            }
        }

        public override bool RequirementMet(ConfiguredContract contract)
        {
            return programs.All(p => ProgramHandler.Instance.ActivePrograms.Any(p2 => p2.name == p));
        }

        protected override string RequirementText()
        {
            IEnumerable<string> prettyNames = programs.Select(name => ProgramHandler.PrettyPrintProgramName(base.name));
            if (invertRequirement)
            {
                return $"Must NOT have active program{(programs.Count > 1 ? "s" : "")}: {string.Join(", ", prettyNames)}";
            }
            else
            {
                return $"Have active program{(programs.Count > 1 ? "s" : "")}: {string.Join(", ", prettyNames)}";
            }
        }
    }
}
