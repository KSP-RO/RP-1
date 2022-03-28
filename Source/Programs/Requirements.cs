using ContractConfigurator;
using System.Linq;
using static ConfigNode;

namespace RP0.Programs
{
    public abstract class ProgramRequirement
    {
        public bool IsInverted { get; set; }

        public abstract bool IsMet { get; }
    }

    public class ContractRequirement : ProgramRequirement
    {
        public string ContractName { get; set; }

        public uint? MinCount { get; set; }

        public override bool IsMet
        {
            get
            {
                bool isMet;
                if (MinCount.HasValue && MinCount > 1)
                {
                    int c = ConfiguredContract.CompletedContracts.Count(c => c.contractType?.name == ContractName);
                    isMet =  c >= MinCount;
                }
                else
                {
                    isMet = ConfiguredContract.CompletedContracts.Any(c => c.contractType?.name == ContractName);
                }

                return IsInverted ? !isMet : isMet;
            }
        }

        public ContractRequirement()
        {
        }

        public ContractRequirement(Value cnVal)
        {
            ContractName = cnVal.value;
            IsInverted = cnVal.name == "not_complete_contract";
        }

        public ContractRequirement(ConfigNode cn)
        {
            ContractName = cn.GetValue("name");
            bool b = false;
            IsInverted = cn.TryGetValue("inverted", ref b) && b;
            uint i = 0;
            MinCount = cn.TryGetValue("minCount", ref i) ? i : null;
        }
    }

    public class OtherProgramRequirement : ProgramRequirement
    {
        public string ProgramName { get; set; }

        public override bool IsMet
        {
            get
            {
                bool b = ProgramHandler.Instance.CompletedPrograms.Any(p => p.name == ProgramName);
                return IsInverted ? !b : b;
            }
        }

        public OtherProgramRequirement()
        {
        }

        public OtherProgramRequirement(Value cnVal)
        {

            ProgramName = cnVal.value;
            IsInverted = cnVal.name == "not_complete_program";
        }
    }
}
