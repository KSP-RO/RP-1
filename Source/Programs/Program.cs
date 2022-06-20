using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using static ConfigNode;

namespace RP0.Programs
{
    public class Program : IConfigNode
    {
        private const string CN_CompleteContract = "COMPLETE_CONTRACT";
        private const double secsPerYear = 3600 * 24 * 365.25;

        [Persistent]
        public string name;

        [Persistent(isPersistant = false)]    // Loaded from cfg but not persisted into the sfs
        public string title;

        [Persistent(isPersistant = false)]
        public string description;

        [Persistent(isPersistant = false)]
        public string requirementsPrettyText;

        [Persistent(isPersistant = false)]
        public string objectivesPrettyText;

        [Persistent(isPersistant = false)]
        public double nominalDurationYears;

        /// <summary>
        /// The amount of funds that will be paid out over the nominal duration of the program.
        /// Doesn't factor in the funds multiplier.
        /// </summary>
        [Persistent(isPersistant = false)]
        public double baseFunding;

        [Persistent]
        public double acceptedUT;

        [Persistent]
        public double objectivesCompletedUT;

        [Persistent]
        public double completedUT;

        [Persistent]
        public double lastPaymentUT;

        /// <summary>
        /// The amount of funds that will be paid out over the nominal duration of the program.
        /// Also factors in the funds multiplier and will not change after the program has been accepted.
        /// </summary>
        [Persistent]
        public double totalFunding;

        [Persistent]
        public double fundsPaidOut;

        [Persistent]
        public double repPenaltyAssessed;

        [Persistent]
        public DoubleCurve overrideFundingCurve = new DoubleCurve();

        /// <summary>
        /// Texture URL
        /// </summary>
        [Persistent]
        public string icon;

        [Persistent(isPersistant = false)]
        public double repDeltaOnCompletePerYearEarly;

        [Persistent(isPersistant = false)]
        public double repPenaltyPerYearLate;

        public RequirementBlock RequirementsBlock;
        public RequirementBlock ObjectivesBlock;

        private Func<bool> _requirementsPredicate;
        private Func<bool> _objectivesPredicate;

        public double TotalFunding => totalFunding > 0 ? totalFunding : baseFunding * HighLogic.CurrentGame.Parameters.Career.FundsGainMultiplier;

        public bool IsComplete => completedUT != 0;

        public bool IsActive => !IsComplete && acceptedUT != 0;

        public bool CanAccept => !IsComplete && !IsActive && AllRequirementsMet;

        public bool CanComplete => !IsComplete && IsActive && objectivesCompletedUT != 0;

        public bool AllRequirementsMet => _requirementsPredicate == null || _requirementsPredicate();

        public bool AllObjectivesMet => _objectivesPredicate == null || _objectivesPredicate();

        public Program()
        {
        }

        public Program(ConfigNode n) : this()
        {
            Load(n);
        }

        public Program(Program toCopy) : this()
        {
            name = toCopy.name;
            title = toCopy.title;
            icon = toCopy.icon;
            description = toCopy.description;
            requirementsPrettyText = toCopy.requirementsPrettyText;
            objectivesPrettyText = toCopy.objectivesPrettyText;
            nominalDurationYears = toCopy.nominalDurationYears;
            baseFunding = toCopy.baseFunding;
            RequirementsBlock = toCopy.RequirementsBlock;
            ObjectivesBlock = toCopy.ObjectivesBlock;
            _requirementsPredicate = toCopy._requirementsPredicate;
            _objectivesPredicate = toCopy._objectivesPredicate;
        }

        public void Load(ConfigNode node)
        {
            LoadObjectFromConfig(this, node);

            ConfigNode cn = node.GetNode("REQUIREMENTS");
            if (cn != null)
            {
                RequirementBlock reqBlock = ParseRequirementBlock(cn);
                RequirementsBlock = reqBlock;
                _requirementsPredicate = reqBlock?.Expression.Compile();
            }

            cn = node.GetNode("OBJECTIVES");
            if (cn != null)
            {
                RequirementBlock reqBlock = ParseRequirementBlock(cn);
                ObjectivesBlock = reqBlock;
                _objectivesPredicate = reqBlock?.Expression.Compile();
            }
        }

        public void Save(ConfigNode node)
        {
            CreateConfigFromObject(this, node);
        }

        public Program Accept()
        {
            var p = new Program(this)
            {
                acceptedUT = KSPUtils.GetUT(),
                lastPaymentUT = KSPUtils.GetUT(),
                totalFunding = TotalFunding,
                fundsPaidOut = 0,
                repPenaltyAssessed = 0
            };
            CareerLog.Instance?.ProgramAccepted(p);

            return p;
        }

        public double GetFundsForFutureTimestamp(double ut)
        {
            double time2 = ut - acceptedUT;
            double funds2 = GetFundsAtTime(time2);
            return Math.Max(0, funds2 - fundsPaidOut);
        }

        public void ProcessFunding()
        {
            if (TotalFunding < 1) return;

            double nowUT = KSPUtils.GetUT();
            double time2 = nowUT - acceptedUT;
            double funds2 = GetFundsAtTime(time2);
            double fundsToAdd = funds2 - fundsPaidOut;
            lastPaymentUT = nowUT;

            Debug.Log($"[RP-0] Adding {fundsToAdd} funds for program {name} - amount at time {nowUT / nominalDurationYears / (86400d * 365.25d)} should be {funds2} but is {fundsPaidOut}");
            fundsPaidOut += fundsToAdd;
            Funding.Instance.AddFunds(fundsToAdd, TransactionReasons.Mission);

            double repLost = GetRepLossAtTime(time2);
            if (repLost > 0d)
            {
                double repLossToApply = repLost - repPenaltyAssessed;
                repPenaltyAssessed += repLossToApply;
                Reputation.Instance.AddReputation((float)-repLossToApply, TransactionReasons.Mission);
                Debug.Log($"[RP-0] Penalizing rep by {repLossToApply} for program {name}");
            }
        }

        public void MarkObjectivesComplete()
        {
            objectivesCompletedUT = KSPUtils.GetUT();
            CareerLog.Instance?.ProgramObjectivesMet(this);
        }

        public void Complete()
        {
            completedUT = KSPUtils.GetUT();
            double timeDeltaYears = nominalDurationYears * secsPerYear - (completedUT - acceptedUT);
            if (timeDeltaYears > 0)
                Reputation.Instance.AddReputation((float)(timeDeltaYears * repDeltaOnCompletePerYearEarly), TransactionReasons.Mission);

            CareerLog.Instance?.ProgramCompleted(this);
        }

        private double GetFundsAtTime(double time)
        {
            double fractionOfTotalDuration = time / nominalDurationYears / secsPerYear;
            DoubleCurve curve = overrideFundingCurve.keys.Count > 0 ? overrideFundingCurve : ProgramHandler.Settings.paymentCurve;
            double curveFactor = curve.Evaluate(fractionOfTotalDuration);
            return curveFactor * TotalFunding;
        }

        private double GetRepLossAtTime(double time)
        {
            const double recip = 1d / secsPerYear;
            double extraTime = time - nominalDurationYears * secsPerYear;
            if (extraTime > 0d)
            {
                return repPenaltyPerYearLate * (extraTime * recip);
            }
            return 0d;
        }

        private RequirementBlock ParseRequirementBlock(ConfigNode cn)
        {
            List<ProgramRequirement> reqs = ParseRequirements(cn);
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
                RequirementBlock block = ParseRequirementBlock(innerCn);
                if (block == null) continue;

                int bCount = block.ChildBlocks?.Count ?? 0;
                int rCount = block.Reqs?.Count ?? 0;
                if (bCount == 0 && rCount == 1)
                {
                    ProgramRequirement req = block.Reqs[0];
                    reqs ??= new List<ProgramRequirement>();
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

            var op = RequirementBlock.LogicOp.Parse(cn);

            return new RequirementBlock
            {
                Expression = op.CombineExpressions(expressions),
                Op = op,
                Reqs = reqs,
                ChildBlocks = childBlocks
            };
        }

        private List<ProgramRequirement> ParseRequirements(ConfigNode cn)
        {
            if (cn == null || (cn.values.Count == 0 && !cn.name.Equals(CN_CompleteContract, StringComparison.OrdinalIgnoreCase))) return null;

            var reqs = new List<ProgramRequirement>();

            if (cn.name.Equals(CN_CompleteContract, StringComparison.OrdinalIgnoreCase))
            {
                reqs.Add(new ContractRequirement(cn));
            }
            else
            {
                foreach (Value cnVal in cn.values)
                {
                    ProgramRequirement req = ParseRequirementAsExpression(cnVal);
                    if (req != null)
                    {
                        reqs.Add(req);
                    }
                }
            }

            return reqs;
        }

        private ProgramRequirement ParseRequirementAsExpression(Value cnVal)
        {
            ProgramRequirement req = null;
            switch (cnVal.name)
            {
                case "complete_program":
                case "not_complete_program":
                    req = new OtherProgramRequirement(cnVal);
                    break;
                case "complete_contract":
                case "not_complete_contract":
                    req = new ContractRequirement(cnVal);
                    break;
                default:
                    break;
            }

            return req;
        }
    }
}
