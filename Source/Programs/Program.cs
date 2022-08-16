using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;
using static ConfigNode;

namespace RP0.Programs
{
    public class Program : IConfigNode
    {
        public enum Speed
        {
            Slow = 0,
            Normal,
            Fast,

            MAX
        }
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
        public double DurationYears => DurationYearsCalc(speed, nominalDurationYears);

        public static double DurationYearsCalc(Speed spd, double years)
        {
            double mult = 1d;
            switch (spd)
            {
                case Speed.Slow: mult = 1.5d; break;
                case Speed.Fast: mult = 0.75d; break;
            }
            double adjustedYears = Math.Round(years * mult * 4) * 0.25d;
            return CurrencyUtils.Time(TransactionReasonsRP0.TimeProgramDeadline, adjustedYears);
        }

        /// <summary>
        /// The amount of funds that will be paid out over the nominal duration of the program.
        /// Doesn't factor in the funds multiplier.
        /// </summary>
        [Persistent(isPersistant = false)]
        public double baseFunding;

        [Persistent(isPersistant = false)]
        public string fundingCurve;

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
        private Speed speed = Speed.Normal;
        public Speed ProgramSpeed => speed;
        public void SetSpeed(Speed spd)
        {
            if (!IsActive && !IsComplete)
                speed = spd;
        }

        private Dictionary<Speed, float> confidenceCosts = new Dictionary<Speed, float>();
        public float GetDisplayedConfidenceCostForSpeed(Speed spd) => -(float)CurrencyUtils.Conf(TransactionReasonsRP0.ProgramActivation, -confidenceCosts[spd]);
        public float ConfidenceCost => confidenceCosts[speed];
        public float DisplayConfidenceCost => GetDisplayedConfidenceCostForSpeed(speed);

        /// <summary>
        /// Texture URL
        /// </summary>
        [Persistent(isPersistant = false)]
        public string icon;

        [Persistent(isPersistant = false)]
        public double repDeltaOnCompletePerYearEarly;

        [Persistent(isPersistant = false)]
        public double repPenaltyPerYearLate;
        public double RepPenaltyPerYearLate => RepPenaltyPerYearLateCalc(speed, repPenaltyPerYearLate);
        public static double RepPenaltyPerYearLateCalc(Speed spd, double pen)
        {
            double mult = 1d;
            if (spd == Speed.Fast)
                mult = 1.5d;

            return pen * mult;
        }

        public List<string> programsToDisableOnAccept = new List<string>();

        public List<string> optionalContracts = new List<string>();

        public RequirementBlock RequirementsBlock;
        public RequirementBlock ObjectivesBlock;

        private Func<bool> _requirementsPredicate;
        private Func<bool> _objectivesPredicate;

        public static double TotalFundingCalc(Speed spd, double funds)
        {
            // For now, no change in funding.
            return funds * HighLogic.CurrentGame.Parameters.Career.FundsGainMultiplier;
        }
        public double TotalFunding => totalFunding > 0 ? totalFunding : TotalFundingCalc(speed, baseFunding);

        public bool IsComplete => completedUT != 0;

        public bool IsActive => !IsComplete && acceptedUT != 0;

        public bool CanAccept => !IsComplete && !IsActive && AllRequirementsMet;

        public bool CanComplete => !IsComplete && IsActive && objectivesCompletedUT != 0;

        public bool AllRequirementsMet => _requirementsPredicate == null || _requirementsPredicate();

        public bool AllObjectivesMet => _objectivesPredicate == null || _objectivesPredicate();

        public bool MeetsConfidenceThreshold => IsSpeedAllowed(speed);

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
            fundingCurve = toCopy.fundingCurve;
            ConfigNode n = new ConfigNode();
            repDeltaOnCompletePerYearEarly = toCopy.repDeltaOnCompletePerYearEarly;
            repPenaltyPerYearLate = toCopy.repPenaltyPerYearLate;
            RequirementsBlock = toCopy.RequirementsBlock;
            ObjectivesBlock = toCopy.ObjectivesBlock;
            _requirementsPredicate = toCopy._requirementsPredicate;
            _objectivesPredicate = toCopy._objectivesPredicate;
            programsToDisableOnAccept = toCopy.programsToDisableOnAccept;
            optionalContracts = toCopy.optionalContracts;
            speed = toCopy.speed;
            confidenceCosts = toCopy.confidenceCosts;
        }

        public void Load(ConfigNode node)
        {
            LoadObjectFromConfig(this, node);

            ConfigNode cn = node.GetNode("REQUIREMENTS");
            if (cn != null)
            {
                try
                {
                    RequirementBlock reqBlock = ParseRequirementBlock(cn);
                    RequirementsBlock = reqBlock;
                    _requirementsPredicate = reqBlock?.Expression.Compile();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RP-0] Exception loading requirements for program {name}: {e}");
                }
            }

            cn = node.GetNode("OBJECTIVES");
            if (cn != null)
            {
                try
                {
                    RequirementBlock reqBlock = ParseRequirementBlock(cn);
                    ObjectivesBlock = reqBlock;
                    _objectivesPredicate = reqBlock?.Expression.Compile();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RP-0] Exception loading objectives for program {name}: {e}");
                }

            }

            cn = node.GetNode("DISABLE");
            if (cn != null)
            {
                foreach (Value v in cn.values)
                    programsToDisableOnAccept.Add(v.name);
            }

            cn = node.GetNode("OPTIONALS");
            if (cn != null)
            {
                foreach (Value v in cn.values)
                    optionalContracts.Add(v.name);
            }

            cn = node.GetNode("CONFIDENCECOSTS");
            if (cn != null)
            {
                for (int i = 0; i < (int)Speed.MAX; ++i)
                {
                    Speed spd = (Speed)i;
                    float cost = 0;
                    cn.TryGetValue(spd.ToString(), ref cost);
                    confidenceCosts[spd] = cost;
                }
            }
            // This is back-compat and can probably go away.
            // But maybe a program could be defined with no costs?
            else if (confidenceCosts.Count == 0)
            {
                for (int i = 0; i < (int)Speed.MAX; ++i)
                {
                    confidenceCosts[(Speed)i] = 0;
                }
            }
        }

        public void Save(ConfigNode node)
        {
            CreateConfigFromObject(this, node);
        }

        public Program Accept()
        {
            Confidence.Instance.AddConfidence(-confidenceCosts[speed], TransactionReasonsRP0.ProgramActivation.Stock());

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
            double fundsToAdd = Math.Max(0d, funds2 - fundsPaidOut);
            lastPaymentUT = nowUT;

            RP0Debug.Log($"[RP-0] Adding {fundsToAdd} funds for program {name} - amount at time {nowUT / DurationYears / (86400d * 365.25d)} should be {funds2} but is {fundsPaidOut}");
            fundsPaidOut += fundsToAdd;
            Funding.Instance.AddFunds(fundsToAdd, TransactionReasons.Mission);

            double repLost = GetRepLossAtTime(time2);
            if (repLost > 0d)
            {
                if (repPenaltyAssessed <= 0)
                {
                    if (KSP.UI.Screens.MessageSystem.Instance != null)
                    {
                        KSP.UI.Screens.MessageSystem.Instance.AddMessage(new KSP.UI.Screens.MessageSystem.Message("Program Duration Expired",
                            $"The duration of the {title} program has expired.\n\n"
                            + ( CanComplete ? "It should be completed in the Administration Building before we lose any more reputation."
                                : "We need to finish its objectives as soon as possible!"),
                            KSP.UI.Screens.MessageSystemButton.MessageButtonColor.ORANGE, KSP.UI.Screens.MessageSystemButton.ButtonIcons.DEADLINE));
                    }
                }
                double repLossToApply = repLost - repPenaltyAssessed;
                repPenaltyAssessed += repLossToApply;
                Reputation.Instance.AddReputation((float)-repLossToApply, TransactionReasons.Mission);
                RP0Debug.Log($"[RP-0] Penalizing rep by {repLossToApply} for program {name}");
            }
        }

        public void MarkObjectivesComplete()
        {
            objectivesCompletedUT = KSPUtils.GetUT();
            CareerLog.Instance?.ProgramObjectivesMet(this);
            if (KSP.UI.Screens.MessageSystem.Instance != null)
            {
                KSP.UI.Screens.MessageSystem.Instance.AddMessage(new KSP.UI.Screens.MessageSystem.Message("Program Complete", 
                    $"We've achieved the objectives set forth in the {title} program and it can now be completed in the Administration Building.",
                    KSP.UI.Screens.MessageSystemButton.MessageButtonColor.GREEN, KSP.UI.Screens.MessageSystemButton.ButtonIcons.COMPLETE));
            }
        }

        public double RepForComplete(double ut)
        {
            double timeDelta = DurationYears * secsPerYear - (ut - acceptedUT);
            double repDelta = 0d;
            if (timeDelta > 0)
            {
                repDelta = (timeDelta / secsPerYear * repDeltaOnCompletePerYearEarly);
            }
            return repDelta;
        }

        public void Complete()
        {
            completedUT = KSPUtils.GetUT();
            float repDelta = (float)RepForComplete(completedUT);
            if (repDelta > 0)
            {
                Reputation.Instance.AddReputation(repDelta, TransactionReasonsRP0.ProgramCompletion.Stock());
            }
            Debug.Log($"[RP-0] Completed program {name} at time {completedUT} ({KSPUtil.PrintDateCompact(completedUT, false)}), duration {(completedUT - acceptedUT)/secsPerYear}. Adding {repDelta} rep.");

            CareerLog.Instance?.ProgramCompleted(this);
        }

        public double GetFundsAtTime(double time)
        {
            double fractionOfTotalDuration = time / DurationYears / secsPerYear;
            DoubleCurve curve = ProgramHandler.Settings.FundingCurve(fundingCurve);
            double curveFactor = curve.Evaluate(fractionOfTotalDuration);
            return curveFactor * TotalFunding;
        }

        private double GetRepLossAtTime(double time)
        {
            const double recip = 1d / secsPerYear;
            double extraTime = time - DurationYears * secsPerYear;
            if (extraTime > 0d)
            {
                return RepPenaltyPerYearLate * (extraTime * recip);
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

        public string GetDescription(bool extendedInfo)
        {
            double duration = DurationYears;
            bool wasAccepted = IsActive || IsComplete;

            string objectives, requirements;
            if (extendedInfo)
            {
                var tmp = ObjectivesBlock?.ToString(doColoring: wasAccepted);
                objectives = $"<b>Objectives</b>:\n{(string.IsNullOrWhiteSpace(tmp) ? "None" : tmp)}";

                tmp = RequirementsBlock?.ToString(doColoring: !wasAccepted);
                requirements = $"<b>Requirements</b>:\n{(string.IsNullOrWhiteSpace(tmp) ? "None" : tmp)}";
            }
            else
            {
                objectives = $"<b>Objectives</b>: {objectivesPrettyText}";
                requirements = $"<b>Requirements</b>: {requirementsPrettyText}";
            }

            string text = $"{objectives}\n\nTotal Funds: <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>{TotalFunding:N0}\n";
            if (wasAccepted)
            {
                text += $"Funds Paid Out: <sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>{fundsPaidOut:N0}\nAccepted: ";
                if (extendedInfo)
                    text += KSPUtil.dateTimeFormatter.PrintDate(acceptedUT, false, false);
                else
                    text += KSPUtil.dateTimeFormatter.PrintDateCompact(acceptedUT, false, false);
                text += "\n";
                if (IsComplete)
                {
                    if (extendedInfo)
                        text += $"Completed: {KSPUtil.dateTimeFormatter.PrintDate(completedUT, false, false)}";
                    else
                        text += $"Completed: {KSPUtil.dateTimeFormatter.PrintDateCompact(completedUT, false, false)}";
                }
                else
                {
                    if (extendedInfo)
                        text += $"Deadline: {KSPUtil.dateTimeFormatter.PrintDate(acceptedUT + duration * 365.25d * 86400d, false, false)}";
                    else
                        text += $"Deadline: {KSPUtil.dateTimeFormatter.PrintDateCompact(acceptedUT + duration * 365.25d * 86400d, false, false)}";
                }
            }
            else
            {
                text = $"{requirements}\n\n{text}Nominal Duration: {duration:0.##} years";
            }

            if (extendedInfo)
            {
                if (wasAccepted)
                {
                    text += $"\n\nProgram Speed: {KSP.Localization.Localizer.GetStringByTag("#rp0ProgramSpeed" + (int)speed)}";
                }
                else
                {
                    if (programsToDisableOnAccept.Count > 0)
                    {
                        text += "\nWill disable the following on accept:";
                        foreach (var s in programsToDisableOnAccept)
                            text += $"\n{ProgramHandler.PrettyPrintProgramName(s)}";
                    }

                    text += $"\n\n{KSP.Localization.Localizer.Format("#rp0ProgramSpeedConfidenceRequired", DisplayConfidenceCost.ToString("N0"))}";
                }

                if (!IsComplete)
                {
                    text += "\n\nFunding Summary:";
                    double totalPaid;
                    int startYear;
                    int lastYear = (int)System.Math.Ceiling(duration) + 1;
                    if (IsActive)
                    {
                        double relativeUT = KSPUtils.GetUT() - acceptedUT;
                        startYear = (int)(relativeUT / (86400d * 365.25d)) + 1;
                        totalPaid = GetFundsAtTime(relativeUT);
                    }
                    else
                    {
                        startYear = 1;
                        totalPaid = 0d;
                    }
                    for (int i = startYear; i < lastYear; ++i)
                    {
                        const double secPerYear = 365.25d * 86400d;
                        double fundAtYear = GetFundsAtTime(Math.Min(i, duration) * secPerYear);
                        double paidThisYear = fundAtYear - totalPaid;
                        totalPaid = fundAtYear;
                        text += $"\nYear {(lastYear > 10 ? " " : string.Empty)}{i}:  {CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.ProgramFunding, paidThisYear, 0d, 0d).GetCostLineOverride(false, false, false, true)}";
                    }
                }
            }

            return text;
        }

        public bool IsSpeedAllowed(Speed s) => CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.ProgramActivation, 0d, 0d, 0d, -confidenceCosts[s], 0d).CanAfford();

        public void SetBestAllowableSpeed()
        {
            if (IsActive || IsComplete)
                return;

            int max = (int)Speed.MAX;
            speed = Speed.Slow;
            for (int i = 0; i < max; ++i)
            {
                Speed spd = (Speed)i;
                if (IsSpeedAllowed(spd))
                    speed = spd;
            }

            // Don't default to fast.
            if (speed > Speed.Normal)
                speed = Speed.Normal;
        }

        public ProgramStrategy GetStrategy() => Strategies.StrategySystem.Instance.Strategies.Find(s => s.Config.Name == name) as ProgramStrategy;
    }
}
