using ContractConfigurator;
using RP0.Programs;
using UniLinq;
using System.Collections.Generic;
using static ConfigNode;

namespace RP0.Requirements
{
    public abstract class Requirement
    {
        public bool IsInverted { get; set; }

        public abstract bool IsMet { get; }

        public abstract override string ToString();

        public abstract string ToString(bool doColoring = false, string prefix = null);

        public static string SurroundWithConditionalColorTags(string s, bool isMet)
        {
            string color = isMet ? "green" : "#fa8072";
            return $"<color={color}>{s}</color>";
        }
    }

    public class ContractRequirement : Requirement
    {
        public string ContractName { get; set; }

        public string ContractTitle => ContractType.GetContractType(ContractName)?.title ?? ContractName;

        public uint? MinCount { get; set; }

        public override bool IsMet
        {
            get
            {
                bool isMet;
                if (MinCount.HasValue && MinCount > 1)
                {
                    int c = ConfiguredContract.CompletedContractsByName.TryGetValue(ContractName, out var list) ? list.Count : 0;
                    isMet =  c >= MinCount;
                }
                else
                {
                    isMet = ConfiguredContract.CompletedContractsByName.TryGetValue(ContractName, out var list) && list.Count > 0;
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

        public override string ToString()
        {
            return ToString(doColoring: false, prefix: null);
        }

        public override string ToString(bool doColoring = false, string prefix = null)
        {
            string s;
            if (MinCount.HasValue && MinCount > 1)
            {
                s = IsInverted ? $"Haven't completed contract {ContractTitle} {MinCount} or more times" :
                                 $"Complete contract {ContractTitle} at least {MinCount} times";
            }
            else if (MinCount.HasValue && MinCount == 1)
            {
                s = IsInverted ? $"Haven't completed contract {ContractTitle} at least once" :
                                 $"Complete contract {ContractTitle} at least once";
            }
            else
            {
                s = IsInverted ? $"Haven't completed contract {ContractTitle}" :
                                 $"Complete contract {ContractTitle}";
            }

            if (prefix != null) s = prefix + s;

            return doColoring ? SurroundWithConditionalColorTags(s, IsMet) : s;
        }
    }

    public class ProgramRequirement : Requirement
    {
        public string ProgramName { get; set; }

        public string ProgramTitle => ProgramHandler.PrettyPrintProgramName(ProgramName);

        public bool IsActiveRequirement { get; set; }

        public override bool IsMet
        {
            get
            {
                List<Program> pList = IsActiveRequirement ? ProgramHandler.Instance.ActivePrograms : ProgramHandler.Instance.CompletedPrograms;
                bool b = pList.Any(p => p.name == ProgramName);
                return IsInverted ? !b : b;
            }
        }

        public ProgramRequirement()
        {
        }

        public ProgramRequirement(Value cnVal)
        {

            ProgramName = cnVal.value;
            IsInverted = cnVal.name == "not_complete_program" || cnVal.name == "not_active_program";
            IsActiveRequirement = cnVal.name == "active_program" || cnVal.name == "not_active_program";
        }

        public override string ToString()
        {
            return ToString(doColoring: false, prefix: null);
        }

        public override string ToString(bool doColoring = false, string prefix = null)
        {
            string s = IsActiveRequirement ?
                        (IsInverted ? $"Program {ProgramTitle} is not active" :
                                    $"Program {ProgramTitle} is active") :
                        (IsInverted ? $"Haven't completed program {ProgramTitle}" :
                                    $"Complete program {ProgramTitle}");
            if (prefix != null) s = prefix + s;
            return doColoring ? SurroundWithConditionalColorTags(s, IsMet) : s;
        }
    }

    public class TechRequirement : Requirement
    {
        public string TechName { get; set; }

        public string TechTitle => ResearchAndDevelopment.GetTechnologyTitle(TechName);

        public override bool IsMet
        {
            get
            {
                bool b = ResearchAndDevelopment.GetTechnologyState(TechName) == RDTech.State.Available;
                return IsInverted ? !b : b;
            }
        }

        public TechRequirement()
        {
        }

        public TechRequirement(Value cnVal)
        {

            TechName = cnVal.value;
            IsInverted = cnVal.name == "not_research_tech";
        }

        public override string ToString()
        {
            return ToString(doColoring: false, prefix: null);
        }

        public override string ToString(bool doColoring = false, string prefix = null)
        {
            string s = IsInverted ? $"Haven't researched tech {TechTitle}" :
                                    $"Research tech {TechTitle}";
            if (prefix != null) s = prefix + s;
            return doColoring ? SurroundWithConditionalColorTags(s, IsMet) : s;
        }
    }

    public class FacilityRequirement : Requirement
    {
        public SpaceCenterFacility Facility { get; set; }

        public string FacilityTitle => ScenarioUpgradeableFacilities.GetFacilityName(Facility);

        public uint Level { get; set; }

        public override bool IsMet
        {
            get
            {
                bool b = KCTUtilities.GetFacilityLevel(Facility) >= Level;
                return IsInverted ? !b : b;
            }
        }

        public FacilityRequirement()
        {
        }

        public FacilityRequirement(ConfigNode cn)
        {
            System.Enum.TryParse<SpaceCenterFacility>(cn.GetValue("facility"), out var fac);
            Facility = fac;
            bool b = false;
            IsInverted = cn.TryGetValue("inverted", ref b) && b;
            // 1-based in cfg
            uint i = 1;
            cn.TryGetValue("level", ref i);
            if (i > 0)
                --i;
            Level = i;
        }

        public override string ToString()
        {
            return ToString(doColoring: false, prefix: null);
        }

        public override string ToString(bool doColoring = false, string prefix = null)
        {
            string s = IsInverted ? $"{FacilityTitle} must be less than level {Level + 1}" :
                    $"Upgrade {FacilityTitle} to at least level {Level + 1}";

            if (prefix != null) s = prefix + s;

            return doColoring ? SurroundWithConditionalColorTags(s, IsMet) : s;
        }
    }
}
