using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UniLinq;
using static ConfigNode;

namespace RP0.Requirements
{
    public class RequirementBlock
    {
        private const string CN_CompleteContract = "COMPLETE_CONTRACT";
        private const string CN_FacilityLevel = "FACILITY_LEVEL";

        public Expression<Func<bool>> Expression { get; set; }

        public LogicOp Op { get; set; }

        public List<Requirement> Reqs { get; set; }

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
                    s = Requirement.SurroundWithConditionalColorTags(s, isMet);
                }
                s = $"{sIndent}{s}\n";
                sIndent += "    ";
            }

            if (reqCount > 0) s += string.Join("\n", Reqs.Select(r => $"{sIndent}{r.ToString(doColoring, prefix: "- ")}"));
            if (reqCount > 0 && childCount > 0) s += "\n";
            if (childCount > 0) s += string.Join("\n", ChildBlocks.Select(b => b.ToString(indent: indent + 4, doColoring)));

            return s;
        }

        public static RequirementBlock Load(ConfigNode cn)
        {
            List<Requirement> reqs = ParseRequirements(cn);
            var expressions = new List<Expression<Func<bool>>>();
            if (reqs != null)
            {
                foreach (var r in reqs)
                {
                    expressions.Add(() => r.IsMet);
                }
            }

            var childBlocks = new List<RequirementBlock>();
            foreach (ConfigNode innerCn in cn.nodes)
            {
                RequirementBlock block = Load(innerCn);
                if (block == null) continue;

                int bCount = block.ChildBlocks?.Count ?? 0;
                int rCount = block.Reqs?.Count ?? 0;
                if (bCount == 0 && rCount == 1)
                {
                    Requirement req = block.Reqs[0];
                    reqs ??= new List<Requirement>();
                    reqs.Add(req);
                    expressions.Add(() => req.IsMet);
                }
                else
                {
                    childBlocks.Add(block);
                    expressions.Add(block.Expression);
                }
            }

            if (expressions == null || expressions.Count == 0) return null;

            if (childBlocks.Count == 1 && (reqs == null || reqs.Count == 0)) return childBlocks[0];

            var op = LogicOp.Parse(cn);

            return new RequirementBlock
            {
                Expression = op.CombineExpressions(expressions),
                Op = op,
                Reqs = reqs,
                ChildBlocks = childBlocks
            };
        }

        private static List<Requirement> ParseRequirements(ConfigNode cn)
        {
            if (cn == null || (cn.values.Count == 0 
                && !cn.name.Equals(CN_CompleteContract, StringComparison.OrdinalIgnoreCase)
                && !cn.name.Equals(CN_FacilityLevel, StringComparison.OrdinalIgnoreCase))) return null;

            var reqs = new List<Requirement>();

            if (cn.name.Equals(CN_CompleteContract, StringComparison.OrdinalIgnoreCase))
            {
                reqs.Add(new ContractRequirement(cn));
            }
            else if (cn.name.Equals(CN_FacilityLevel, StringComparison.OrdinalIgnoreCase))
            {
                reqs.Add(new FacilityRequirement(cn));
            }
            else
            {
                foreach (Value cnVal in cn.values)
                {
                    Requirement req = ParseRequirementAsExpression(cnVal);
                    if (req != null)
                    {
                        reqs.Add(req);
                    }
                }
            }

            return reqs;
        }

        private static Requirement ParseRequirementAsExpression(Value cnVal)
        {
            Requirement req = null;
            switch (cnVal.name)
            {
                case "complete_program":
                case "not_complete_program":
                case "active_program":
                case "not_active_program":
                    req = new ProgramRequirement(cnVal);
                    break;
                case "complete_contract":
                case "not_complete_contract":
                    req = new ContractRequirement(cnVal);
                    break;
                case "research_tech":
                case "not_research_tech":
                    req = new TechRequirement(cnVal);
                    break;
                default:
                    break;
            }

            return req;
        }
    }
}
