using ContractConfigurator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using UnityEngine;
using static ConfigNode;

namespace RP0.Programs
{
    public class RequirementBlock
    {
        public abstract class LogicOp
        {
            public abstract override string ToString();

            public abstract Expression<Func<bool>> CombineExpressions(IEnumerable<Expression<Func<bool>>> expressions);

            public static LogicOp Parse(ConfigNode cn)
            {
                string s = cn.name;
                if (s.Equals("any", StringComparison.OrdinalIgnoreCase))
                {
                    return new Any();
                }
                else if (s.Equals("atleast", StringComparison.OrdinalIgnoreCase))
                {
                    return new AtLeast(cn);
                }
                else
                {
                    return new All();
                }
            }
        }

        public class All : LogicOp
        {
            public override string ToString() => "All";

            public override Expression<Func<bool>> CombineExpressions(IEnumerable<Expression<Func<bool>>> expressions)
            {
                if (expressions == null || expressions.Count() == 0)
                {
                    return () => true;
                }
                return expressions.Aggregate((a, b) => a.And(b));
            }
        }

        public class Any : LogicOp
        {
            public override string ToString() => "Any";

            public override Expression<Func<bool>> CombineExpressions(IEnumerable<Expression<Func<bool>>> expressions)
            {
                if (expressions == null || expressions.Count() == 0)
                {
                    return () => true;
                }
                return expressions.Aggregate((a, b) => a.Or(b));
            }
        }

        public class AtLeast : LogicOp
        {
            public uint Count { get; private set; }

            public AtLeast(uint count)
            {
                Count = count;
            }

            public AtLeast(ConfigNode cn)
            {
                uint count = 0;
                if (!cn.TryGetValue("count", ref count))
                {
                    Debug.LogError("[RP-0] Invalid RequirementBlock logic operator: " + cn);
                }
                Count = count;
            }

            public override string ToString() => $"At least {Count}";

            public override Expression<Func<bool>> CombineExpressions(IEnumerable<Expression<Func<bool>>> expressions)
            {
                if (expressions == null || expressions.Count() == 0)
                {
                    return () => true;
                }
                Func<bool>[] compiledExprs = expressions.Select(x => x.Compile()).ToArray();
                return () => compiledExprs.Count(expr => expr.Invoke()) >= Count;
            }
        }

        public Expression<Func<bool>> Expression { get; set; }

        public LogicOp Op { get; set; }

        public List<ProgramRequirement> Reqs { get; set; }

        public List<RequirementBlock> ChildBlocks { get; set; }

        public override string ToString()
        {
            return ToString(indent: 0);
        }

        public string ToString(int indent = 0, bool doColoring = false)
        {
            string sIndent = string.Join("", Enumerable.Repeat(" ", indent));
            int reqCount = Reqs?.Count ?? 0;
            int childCount = ChildBlocks?.Count ?? 0;

            string s = null;
            bool omitOpHeader = childCount + reqCount < 2;
            if (!omitOpHeader)
            {
                s = $"* {Op}";
                if (doColoring)
                {
                    bool isMet = Expression.Compile().Invoke();
                    s = ProgramRequirement.SurroundWithConditionalColorTags(s, isMet);
                }
                s = $"{sIndent}{s}\n";
                sIndent += "    ";
            }

            if (reqCount > 0) s += string.Join("\n", Reqs.Select(r => $"{sIndent}{r.ToString(doColoring, prefix: "- ")}"));
            if (reqCount > 0 && childCount > 0) s += "\n";
            if (childCount > 0) s += string.Join("\n", ChildBlocks.Select(b => b.ToString(indent: indent + 4, doColoring)));

            return s;
        }
    }

    public abstract class ProgramRequirement
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

    public class ContractRequirement : ProgramRequirement
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
            else
            {
                s = IsInverted ? $"Haven't completed contract {ContractTitle}" :
                                 $"Complete contract {ContractTitle}";
            }

            if (prefix != null) s = prefix + s;

            return doColoring ? SurroundWithConditionalColorTags(s, IsMet) : s;
        }
    }

    public class OtherProgramRequirement : ProgramRequirement
    {
        public string ProgramName { get; set; }

        public string ProgramTitle => ProgramHandler.ProgramDict.TryGetValue(ProgramName, out Program p) ? p.title : ProgramName;

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

        public override string ToString()
        {
            return ToString(doColoring: false, prefix: null);
        }

        public override string ToString(bool doColoring = false, string prefix = null)
        {
            string s = IsInverted ? $"Haven't completed program {ProgramTitle}" :
                                    $"Complete program {ProgramTitle}";
            if (prefix != null) s = prefix + s;
            return doColoring ? SurroundWithConditionalColorTags(s, IsMet) : s;
        }
    }
}
