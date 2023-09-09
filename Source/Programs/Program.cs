using KSP.Localization;
using RP0.Requirements;
using Strategies;
using System;
using System.Collections.Generic;
using UniLinq;
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
        public bool isDisabled;

        [Persistent]
        public double nominalDurationYears;
        public double DurationYears => DurationYearsCalc(speed, nominalDurationYears);
        public double EffectiveDurationYears => acceptedUT == 0d ? DurationYears : ElapsedYears + RemainingDurationYears;
        public double ElapsedYears => acceptedUT == 0d ? 0d : (Planetarium.GetUniversalTime() - acceptedUT) / secsPerYear;

        public double RemainingDurationYears => (1d - fracElapsed) * DurationYears;

        [Persistent]
        public double fracElapsed = -1d;
        public double FracElapsed => fracElapsed;

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
        public double deadlineUT;

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

        [Persistent(isPersistant = false)]
        private float repToConfidence = -1f;
        public float RepToConfidence => repToConfidence >= 0f ? repToConfidence : ProgramHandler.Settings.repToConfidence;

        [Persistent(isPersistant = false)]
        public int slots = 2;

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

        public bool CanAccept => !IsComplete && !IsActive && !isDisabled && AllRequirementsMet;

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
            isDisabled = toCopy.isDisabled;
            baseFunding = toCopy.baseFunding;
            fundingCurve = toCopy.fundingCurve;
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
            repToConfidence = toCopy.repToConfidence;
            slots = toCopy.slots;
        }

        public override string ToString() => $"{name} ({(IsComplete ? "Complete" : IsActive ? "Active" : "Inactive")})";

        public void Load(ConfigNode node)
        {
            LoadObjectFromConfig(this, node);

            ConfigNode cn = node.GetNode("REQUIREMENTS");
            if (cn != null)
            {
                try
                {
                    RequirementBlock reqBlock = RequirementBlock.Load(cn);
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
                    RequirementBlock reqBlock = RequirementBlock.Load(cn);
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
                acceptedUT = Planetarium.GetUniversalTime(),
                lastPaymentUT = Planetarium.GetUniversalTime(),
                totalFunding = TotalFunding,
                fundsPaidOut = 0,
                repPenaltyAssessed = 0,
                fracElapsed = 0,
                deadlineUT = acceptedUT + DurationYears * secsPerYear
            };
            CareerLog.Instance?.ProgramAccepted(p);

            return p;
        }

        public double GetFundsForFutureTimestamp(double ut)
        {
            double frac2 = fracElapsed + (ut - Planetarium.GetUniversalTime()) / (secsPerYear * DurationYears);
            double funds2 = GetFundsAtFrac(frac2);
            return Math.Max(0, funds2 - fundsPaidOut);
        }

        public void OnLeaderChange()
        {
            deadlineUT = Planetarium.GetUniversalTime() + (1d - FracElapsed) * DurationYears * secsPerYear;
        }

        public void ProcessFunding()
        {
            if (TotalFunding < 1)
                return;

            if (!ProgramHandler.Instance.Ready)
                return;

            double duration = DurationYears;
            double nowUT = Planetarium.GetUniversalTime();
            double frac2 = fracElapsed + (nowUT - lastPaymentUT) / (secsPerYear * duration);
            if (fracElapsed == frac2)
                return;

            // update deadline
            if (frac2 < 1d)
            {
                // we're still in the future, recompute based on remaining duration
                deadlineUT = nowUT + (1d - frac2) * duration * secsPerYear;
            }
            else if (fracElapsed < 1d)
            {
                // compute when deadline should have been
                deadlineUT = lastPaymentUT + (1d - fracElapsed) * duration * secsPerYear;
            }

            // First, handle rep loss
            double repLost = GetRepLossAtFrac(frac2);
            if (repLost > 0d)
            {
                if (repPenaltyAssessed <= 0)
                {
                    if (KSP.UI.Screens.MessageSystem.Instance != null)
                    {
                        KSP.UI.Screens.MessageSystem.Instance.AddMessage(new KSP.UI.Screens.MessageSystem.Message("Program Duration Expired",
                            $"The duration of the {title} program has expired.\n\n"
                            + (CanComplete ? "It should be completed in the Administration Building before we lose any more reputation."
                                : "We need to finish its objectives as soon as possible!"),
                            KSP.UI.Screens.MessageSystemButton.MessageButtonColor.ORANGE, KSP.UI.Screens.MessageSystemButton.ButtonIcons.DEADLINE));
                    }
                }
                double repLossToApply = repLost - repPenaltyAssessed;
                if (repLossToApply > 0d)
                {
                    repPenaltyAssessed += repLossToApply;
                    Reputation.Instance.AddReputation((float)-repLossToApply, TransactionReasons.Mission);
                    RP0Debug.Log($"[RP-0] Penalizing rep by {repLossToApply} for program {name}");
                }
            }

            double funds2 = GetFundsAtFrac(frac2);
            double fundsToAdd = funds2 - fundsPaidOut;
            if (fundsToAdd <= 0d)
                return;

            lastPaymentUT = nowUT;
            fracElapsed = frac2;

            RP0Debug.Log($"[RP-0] Adding {fundsToAdd} funds for program {name} - amount at time {nowUT / DurationYears / (86400d * 365.25d)} should be {funds2} but is {fundsPaidOut}");
            fundsPaidOut += fundsToAdd;
            Funding.Instance.AddFunds(fundsToAdd, TransactionReasons.Mission);
        }

        public void MarkObjectivesComplete()
        {
            objectivesCompletedUT = Planetarium.GetUniversalTime();
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
            double duration = DurationYears;
            double newFracElapsed = fracElapsed + (ut - lastPaymentUT) / (duration * secsPerYear);
            if (newFracElapsed >= 1d)
                return 0d;

            double yearDelta = (1d - newFracElapsed) * duration;
            double repDelta = yearDelta * repDeltaOnCompletePerYearEarly;
            return repDelta;
        }

        public void Complete()
        {
            completedUT = Planetarium.GetUniversalTime();
            float repDelta = (float)RepForComplete(completedUT);
            if (repDelta > 0)
            {
                Reputation.Instance.AddReputation(repDelta, TransactionReasonsRP0.ProgramCompletion.Stock());
            }
            Debug.Log($"[RP-0] Completed program {name} at time {completedUT} ({KSPUtil.PrintDateCompact(completedUT, false)}), duration {(completedUT - acceptedUT) / secsPerYear}. Adding {repDelta} rep.");

            CareerLog.Instance?.ProgramCompleted(this);
            Milestones.MilestoneHandler.Instance.OnProgramComplete(name);
        }

        public double GetFundsAtTime(double time)
        {
            return GetFundsAtFrac(time / DurationYears / secsPerYear);
        }

        public double GetFundsAtFrac(double fractionOfTotalDuration)
        {
            DoubleCurve curve = ProgramHandler.Settings.FundingCurve(fundingCurve);
            double curveFactor = curve.Evaluate(fractionOfTotalDuration);
            return curveFactor * TotalFunding;
        }

        private double GetRepLossAtFrac(double frac)
        {
            if (frac <= 1d)
                return 0d;

            return RepPenaltyPerYearLate * (1d - frac) * DurationYears;
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
                        text += $"Deadline: {KSPUtil.dateTimeFormatter.PrintDate(deadlineUT, false, false)}";
                    else
                        text += $"Deadline: {KSPUtil.dateTimeFormatter.PrintDateCompact(deadlineUT, false, false)}";
                }
            }
            else
            {
                text = $"{requirements}\n\n{text}Nominal Duration: {duration:0.##} years\nDeadline if accepted now: {KSPUtil.dateTimeFormatter.PrintDate(Planetarium.GetUniversalTime() + duration * 365.25d * 86400d, false, false)}";
            }

            if (extendedInfo)
            {
                if (wasAccepted)
                {
                    text += $"\n\nProgram Speed: {Localizer.GetStringByTag("#rp0_Admin_Program_Speed" + (int)speed)}";
                }
                else
                {
                    if (programsToDisableOnAccept.Count > 0)
                    {
                        text += "\nWill disable the following on accept:";
                        foreach (var s in programsToDisableOnAccept)
                            text += $"\n{ProgramHandler.PrettyPrintProgramName(s)}";
                    }

                    text += $"\n\n{Localizer.Format("#rp0_Admin_Program_ConfidenceRequired", DisplayConfidenceCost.ToString("N0"))}";
                }

                text = $"<b>Slots Taken: {slots}</b>\n\n{text}";

                var leadersUnlockedByThis = StrategySystem.Instance.SystemConfig.Strategies
                    .OfType<StrategyConfigRP0>()
                    .Where(s => s.DepartmentName != "Programs" &&
                                s.RequirementsBlock != null &&
                                (s.RequirementsBlock.Op is Any ||
                                 s.RequirementsBlock.Op is All && s.RequirementsBlock.Reqs.Count == 1) &&
                                s.RequirementsBlock.ChildBlocks.Count == 0 &&
                                s.RequirementsBlock.Reqs.Any(r => !r.IsInverted &&
                                                                  r is ProgramRequirement pr &&
                                                                  pr.ProgramName == name))
                    .Select(s => s.title);

                string leaderString = string.Join("\n", leadersUnlockedByThis);
                if (!string.IsNullOrEmpty(leaderString))
                    text += "\n\n" + Localizer.Format("#rp0_Leaders_UnlocksLeader") + leaderString;

                if (!IsComplete)
                {
                    text += "\n\nFunding Summary:";
                    double totalPaid;
                    int startYear;
                    int lastYear = (int)Math.Ceiling(duration) + 1;
                    if (IsActive)
                    {
                        startYear = (int)(fracElapsed * duration) + 1;
                        totalPaid = fundsPaidOut;
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
                        text += $"\nNominal Year {(lastYear > 10 ? " " : string.Empty)}{i}:  {CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.ProgramFunding, paidThisYear, 0d, 0d).GetCostLineOverride(false, false, false, true)}";
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

        public ProgramStrategy GetStrategy() => StrategySystem.Instance.Strategies.Find(s => s.Config.Name == name) as ProgramStrategy;
    }
}
