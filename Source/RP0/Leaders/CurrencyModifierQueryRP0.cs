using System;
using KSP.Localization;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;
using UnityEngine;
using ROUtils;

namespace RP0
{
    public class CurrencyArray : IEnumerable
    {
        private const int size = (int)CurrencyRP0.MAX + 1;
        public double[] values = new double[size];

        IEnumerator IEnumerable.GetEnumerator()
        {
            return values.GetEnumerator();
        }

        public int Length => size;

        public CurrencyArray()
        {
            int c = size;
            while (c-- > 0)
            {
                values[c] = 0d;
            }
            this[CurrencyRP0.Rate] = 1d;
        }

        public CurrencyArray(double val)
        {
            int c = size;
            while (c-- > 0)
            {
                values[c] = val;
            }
        }

        public double this[CurrencyRP0 c]
        {
            get
            {
                return values[(int)c];
            }
            set
            {
                values[(int)c] = value;
            }
        }

        public double this[int i]
        {
            get
            {
                return values[i];
            }
            set
            {
                values[i] = value;
            }
        }
    }

    public class CurrencyModifierQueryRP0 : CurrencyModifierQuery
    {
        const int LastCurrencyForAffordChecks = (int)CurrencyRP0.Time - 1;

        public TransactionReasonsRP0 reasonRP0;

        CurrencyArray inputs = new CurrencyArray();

        CurrencyArray multipliers = new CurrencyArray(1d);

        CurrencyArray postMultiplierDeltas = new CurrencyArray(0d);
        CurrencyArray postMultiplierDeltasHidden = new CurrencyArray(0d);

        public CurrencyModifierQueryRP0(TransactionReasons reason, double f0, float s0, float r0)
            : base(reason, (float)f0, s0, r0)
        {
            reasonRP0 = reason.RP0();
            inputs[CurrencyRP0.Funds] = f0;
            inputs[CurrencyRP0.Science] = s0;
            inputs[CurrencyRP0.Reputation] = r0;
        }

        public CurrencyModifierQueryRP0(TransactionReasonsRP0 reason, double f0, double s0, double r0, double c0, double t0)
            : base(reason.Stock(), (float)f0, (float)s0, (float)r0)
        {
            reasonRP0 = reason;
            inputs[CurrencyRP0.Funds] = f0;
            inputs[CurrencyRP0.Science] = s0;
            inputs[CurrencyRP0.Reputation] = r0;
            inputs[CurrencyRP0.Confidence] = c0;
            inputs[CurrencyRP0.Time] = t0;
        }

        public double GetInput(CurrencyRP0 c) => inputs[c];

        public void AddDelta(CurrencyRP0 c, double val)
        {
            RP0Debug.LogError($"CurrencyModifierQuery: Something tried to AddDelta! Currency {c} and values {val} for reason {reasonRP0}");
            if (inputs[c] == 0d)
                return;

            AddDeltaAuthorized(c, val);
        }

        public void AddDeltaAuthorized(CurrencyRP0 c, double val)
        {
            if (inputs[c] == 0d)
                return;

            multipliers[c] = (multipliers[c] * inputs[c] + val) / inputs[c];
        }

        public void AddPostDelta(CurrencyRP0 c, double val, bool hidden)
        {
            (hidden ? postMultiplierDeltasHidden : postMultiplierDeltas)[c] += val;
        }

        public void Multiply(CurrencyRP0 c, double mult)
        {
            multipliers[c] = multipliers[c] * mult;
        }

        public void Multiply(double mult)
        {
            for (int i = multipliers.values.Length; i-- > 0;)
                multipliers[i] = multipliers[i] * mult;
        }

        /// <summary>
        /// This ignores hidden deltas unless specified
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public double GetEffectDelta(CurrencyRP0 c, bool includeHidden = true)
        {
            double total = inputs[c] * (multipliers[c] - 1d) + postMultiplierDeltas[c];
            if(includeHidden)
                total += postMultiplierDeltasHidden[c];

            return total;
        }

        public double GetTotal(CurrencyRP0 c, bool includeHidden = false)
        {
            double total = inputs[c] * multipliers[c] + postMultiplierDeltas[c];
            if(includeHidden)
                total += postMultiplierDeltasHidden[c];

            return total;
        }

        public static bool ApproximatelyZero(double a)
        {
            double absA = Math.Abs(a);
            return absA < Math.Max(1E-06d * absA, Mathf.Epsilon * 8d);
        }

        public static string SpriteString(CurrencyRP0 c) => c switch
        {
            CurrencyRP0.Funds => "<sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1>",
            CurrencyRP0.Science => "<sprite=\"CurrencySpriteAsset\" name=\"Science\" tint=1>",
            CurrencyRP0.Reputation => "<sprite=\"CurrencySpriteAsset\" name=\"Reputation\" tint=1>",
            CurrencyRP0.Confidence => "<sprite=\"CurrencySpriteAsset\" name=\"Flask\" tint=1>",
            _ => throw new ArgumentOutOfRangeException(nameof(c))
        };

        static readonly string[] currencyColors = {
            "#B4D455", // funds
            "#6DCFF6", // sci
            "#E0D503", // rep
            "#ff48c5", // conf
            "#B5B536", // time
            "#efa4e2" // rate
        };

        public static string CurrencyColor(CurrencyRP0 c) => currencyColors[(int)c];

        static readonly string[] currencyFormats = {
            "N2",
            "0.#",
            "N1",
            "N1"
        };

        public string GetCostLineOverride(bool displayInverted = true, bool useCurrencyColors = false, bool useInsufficientCurrencyColors = true, bool includePercentage = false, bool showHidden = false, string seperator = ", ", bool flipRateDeltaColoring = false)
        {
            CurrencyArray outputs = new CurrencyArray();
            bool[] canAffords = new bool[outputs.Length];
            double invMult = displayInverted ? -1d : 1d;
            int rateIndex = (int)CurrencyRP0.Rate;
            for (int i = 0; i < outputs.Length; ++i)
            {
                double amount = inputs[i] * multipliers[i] + postMultiplierDeltas[i];
                if(showHidden)
                    amount += postMultiplierDeltasHidden[i];
                if (i > LastCurrencyForAffordChecks)
                {
                    canAffords[i] = true;
                }
                else
                {
                    canAffords[i] = CanAfford((CurrencyRP0)i);
                }

                if (i != rateIndex)
                    amount *= invMult;

                outputs[i] = amount;
            }

            string resultText = "";
            for (int i = 0; i < outputs.Length; ++i)
            {
                if (i == rateIndex)
                {
                    if (ApproximatelyZero(multipliers[i] + postMultiplierDeltas[i] + postMultiplierDeltasHidden[i] - 1d))
                        continue;
                }
                else if (ApproximatelyZero(inputs[i]) && ApproximatelyZero(outputs[i]))
                    continue;

                if (!string.IsNullOrEmpty(resultText))
                    resultText += seperator;

                double amount = outputs[i];
                string amountText;
                CurrencyRP0 c = (CurrencyRP0)i;
                if (i > LastCurrencyForAffordChecks)
                {
                    if (c == CurrencyRP0.Time)
                    {
                        if (double.IsInfinity(amount) || double.IsNaN(amount) || amount > (100 * 365.25d * 86400d))
                            amountText = Localizer.GetStringByTag("#autoLOC_462439");
                        else
                            amountText = KSPUtil.PrintDateDeltaCompact(amount, amount < 7d * 86400d, amount < 600);
                    }
                    else
                    {
                        amountText = Localizer.Format("#rp0_Currency_Format_Rate", amount.ToString("N2"));
                    }
                }
                else
                {
                    amountText = $"{SpriteString(c)} {amount.ToString(currencyFormats[i])}";
                }

                if (useInsufficientCurrencyColors && !canAffords[i])
                {
                    amountText = $"<color={XKCDColors.HexFormat.BrightOrange}>{amountText}</color>";
                }
                else if (useCurrencyColors)
                {
                    amountText = $"<color={CurrencyColor(c)}>{amountText}</color>";
                }

                resultText += amountText;

                double effectiveMult = postMultiplierDeltas[i] != 0d && inputs[i] != 0d ? amount / inputs[i] : multipliers[i];
                if (includePercentage && effectiveMult != 1d)
                {
                    // Normally, for a currency less is good if it's a cost.
                    // For time, use same positive/negative logic
                    // for rate, more is good unless we're flipping.
                    bool lessGood = c == CurrencyRP0.Rate ? flipRateDeltaColoring : inputs[i] < 0;
                    resultText += $" <color={TextStylingColor(effectiveMult < 1, lessGood)}>({LocalizationHandler.FormatRatioAsPercent(effectiveMult)})</color>";
                }
            }

            return resultText;
        }

        public bool CanAffordOverride(Action<Currency> onInsufficientCurrency = null)
        {
            bool canAffordFunds = CanAfford(CurrencyRP0.Funds);
            bool canAffordSci = CanAfford(CurrencyRP0.Science);
            bool canAffordRep = CanAfford(CurrencyRP0.Reputation);
            bool canAffordConf = CanAfford(CurrencyRP0.Confidence);
            bool canAfford = canAffordFunds && canAffordSci && canAffordRep && canAffordConf;
            if (onInsufficientCurrency != null && !canAfford)
            {
                if (!canAffordFunds)
                {
                    onInsufficientCurrency(Currency.Funds);
                }
                else if (!canAffordSci)
                {
                    onInsufficientCurrency(Currency.Science);
                }
                else if (!canAffordRep)
                {
                    onInsufficientCurrency(Currency.Reputation);
                }
            }
            return canAfford;
        }

        public bool CanAfford(Action<CurrencyRP0> onInsufficientCurrency = null)
        {
            bool canAffordFunds = CanAfford(CurrencyRP0.Funds);
            bool canAffordSci = CanAfford(CurrencyRP0.Science);
            bool canAffordRep = CanAfford(CurrencyRP0.Reputation);
            bool canAffordConf = CanAfford(CurrencyRP0.Confidence);
            bool canAfford = canAffordFunds && canAffordSci && canAffordRep && canAffordConf;
            if (onInsufficientCurrency != null && !canAfford)
            {
                if (!canAffordFunds)
                {
                    onInsufficientCurrency(CurrencyRP0.Funds);
                }
                else if (!canAffordSci)
                {
                    onInsufficientCurrency(CurrencyRP0.Science);
                }
                else if (!canAffordRep)
                {
                    onInsufficientCurrency(CurrencyRP0.Reputation);
                }
                else if (!canAffordConf)
                {
                    onInsufficientCurrency(CurrencyRP0.Confidence);
                }
            }
            return canAfford;
        }

        public bool CanAfford(CurrencyRP0 c)
        {
            double amount = -(inputs[c] * multipliers[c] + postMultiplierDeltas[c] + postMultiplierDeltasHidden[c]);
            if (ApproximatelyZero(amount))
                return true;

            switch (c)
            {
                default:
                    return true;

                case CurrencyRP0.Funds:
                    if (Funding.Instance != null)
                    {
                        return amount <= Funding.Instance.Funds;
                    }
                    return true;
                case CurrencyRP0.Science:
                    if (ResearchAndDevelopment.Instance != null)
                    {
                        return UtilMath.RoundToPlaces((double)ResearchAndDevelopment.Instance.Science, 1) >= UtilMath.RoundToPlaces(amount, 1);
                    }
                    return true;
                case CurrencyRP0.Reputation:
                    if (Reputation.Instance != null)
                    {
                        return UtilMath.RoundToPlaces((double)Reputation.Instance.reputation, 1) >= UtilMath.RoundToPlaces(amount, 1);
                    }
                    return true;

                case CurrencyRP0.Confidence:
                    if (Confidence.Instance != null)
                    {
                        return UtilMath.RoundToPlaces((double)Confidence.CurrentConfidence, 1) >= UtilMath.RoundToPlaces(amount, 1);
                    }
                    return true;
            }
        }

        public string GetEffectDeltaText(CurrencyRP0 c, string format, TextStyling textStyle = TextStyling.None)
        {
            string text = "";
            double delta = inputs[c] * (multipliers[c] - 1d) + postMultiplierDeltas[c];
            
            if (delta == 0d)
            {
                return "";
            }
            text = delta.ToString(format);
            return textStyle switch
            {
                TextStyling.OnGUI => ((delta > 0d) ? "<color=#caff00>(+" : "<color=#feb200>(") + text + ")</color>",
                TextStyling.EzGUIRichText => ((delta > 0d) ? "<#caff00>(+" : "<#feb200>(") + text + ")</>",
                TextStyling.OnGUI_LessIsGood => ((delta > 0d) ? "<color=#feb200>(+" : "<color=#caff00>(") + text + ")</color>",
                TextStyling.EzGUIRichText_LessIsGood => ((delta > 0d) ? "<#feb200>(+" : "<#caff00>(") + text + ")</>",
                _ => ((delta > 0d) ? "(+" : "(") + text + ")",
            };
        }

        public string GetEffectPercentageText(CurrencyRP0 c, string format, TextStyling textStyle = TextStyling.None)
        {
            
            if (inputs[c] != 0d)
            {
                double mult = postMultiplierDeltas[c] == 0d ? multipliers[c] : (inputs[c] * multipliers[c] + postMultiplierDeltas[c]) / inputs[c];
                double percent = (mult - 1d) * 100d;
                string text = percent.ToString(format);
                return textStyle switch
                {
                    TextStyling.OnGUI => ((percent > 0f) ? "<color=#caff00>(+" : "<color=#feb200>(") + text + "%)</color>",
                    TextStyling.EzGUIRichText => ((percent > 0f) ? "<#caff00>(+" : "<#feb200>(") + text + "%)</>",
                    TextStyling.OnGUI_LessIsGood => ((percent > 0f) ? "<color=#feb200><+" : "<color=#caff00><") + text + "%></color>",
                    TextStyling.EzGUIRichText_LessIsGood => ((percent > 0f) ? "<#feb200>(+" : "<#caff00>(") + text + ")%</>",
                    _ => ((percent > 0f) ? "(+" : "(") + text + "%)",
                };
            }
            return "";
        }

        public static string TextStylingColor(bool negative, bool negativeGood) => negative ^ negativeGood ? "#feb200" : "#caff00";

        public static CurrencyModifierQueryRP0 RunQuery(TransactionReasonsRP0 reason, double f0, double s0, double r0)
        {
            return RunQuery(reason, f0, s0, r0, 0d, 0d);
        }

        public static CurrencyModifierQueryRP0 RunQuery(TransactionReasonsRP0 reason, double f0, double s0, double r0, double c0, double t0)
        {
            CurrencyModifierQueryRP0 currencyModifierQuery = new CurrencyModifierQueryRP0(reason, f0, s0, r0, c0, t0);
            GameEvents.Modifiers.OnCurrencyModifierQuery.Fire(currencyModifierQuery);
            return currencyModifierQuery;
        }

        public static CurrencyModifierQueryRP0 RunQuery(TransactionReasonsRP0 reason, Dictionary<CurrencyRP0, double> dict, bool invert = false)
        {
            double mult = invert ? -1d : 1d;
            return RunQuery(reason,
                mult * dict.ValueOrDefault(CurrencyRP0.Funds),
                mult * dict.ValueOrDefault(CurrencyRP0.Science),
                mult * dict.ValueOrDefault(CurrencyRP0.Reputation),
                mult * dict.ValueOrDefault(CurrencyRP0.Confidence),
                mult * dict.ValueOrDefault(CurrencyRP0.Time));
        }
    }

    [Flags]
    public enum TransactionReasonsRP0 : long
    {
        None = 0,
        //ContractAdvance = 1L << 1,
        ContractReward = 1L << 2,
        //ContractPenalty = 1L << 3,
        Contracts = ContractReward | ContractDecline,

        VesselPurchase = 1L << 4, // VesselRollout
        VesselRecovery = 1L << 5,
        //used by ModuleExperienceManagement only - Vessels = VesselRollout | VesselRecovery,

        //StrategyInput = 1L << 7,
        //StrategyOutput = 1L << 8,
        StrategySetup = 1L << 9,
        //Strategies = StrategyInput | StrategyOutput | StrategySetup,

        ScienceTransmission = 1L << 10,

        StructureRepair = 1L << 11, // will be used for maintenance too
        //StructureCollapse = 1 << 12,
        StructureConstruction = 1L << 13,
        Structures = StructureRepair /*| StructureCollapse*/ | StructureConstruction,

        RnDTechResearch = 1L << 14,

        Cheating = 1L << 16,
        
        CrewRecruited = 1L << 17,
        LossOfCrew = 1L << 6,

        ContractDecline = 1L << 18,
        //Progression = 1L << 19,

        // Mission = 1L << 20,

        ProgramFunding = 1L << 20,
        ProgramActivation = 1L << 21,
        ProgramCompletion = 1L << 22,
        Programs = ProgramFunding | ProgramActivation | ProgramCompletion,

        RocketRollout = 1L << 23,
        AirLaunchRollout = 1L << 24,
        Rollouts = RocketRollout | AirLaunchRollout,

        LeaderRemove = 1L << 25,

        SalaryEngineers = 1L << 26,
        SalaryResearchers = 1L << 27,
        SalaryCrew = 1L << 28,
        Salary = SalaryEngineers | SalaryResearchers | SalaryCrew,

        CrewTraining = 1L << 29,
        Crew = CrewRecruited | SalaryCrew | CrewTraining,

        HiringEngineers = 1L << 30,
        HiringResearchers = 1L << 3,
        Hiring = HiringEngineers | HiringResearchers,

        Personnel = Salary | Crew | Hiring,

        // unused bits: 0,1,7,8,12,19

        StructureConstructionLC = 1L << 0,
        StructureRepairLC = 1L << 1,
        StructureConstructionAll = StructureConstruction | StructureConstructionLC,
        StructureRepairAll = StructureRepair | StructureRepairLC,

        Subsidy = 1L << 7,
        DailyRepDecline = 1L << 8, // RepDecline

        PartOrUpgradeUnlock = 1L << 15, // RnDPartPurchase
        ToolingPurchase = 1L << 12,
        ToolingUpkeep = 1L << 19,
        Tooling = ToolingPurchase | ToolingUpkeep,

        // Time

        TimeProgramDeadline = 1L << 33,

        RateIntegrationVAB = 1L << 34,
        RateIntegrationSPH = 1L << 35,
        RateIntegration = RateIntegrationVAB | RateIntegrationSPH,

        RateRollout = 1L << 36,
        RateAirlaunch = 1L << 37,
        RateVesselPrep = RateRollout | RateAirlaunch,
        RateReconditioning = 1L << 38,
        RateRecovery = 1L << 39,

        RateManufacturing = 1L << 40,

        RateVessel = RateIntegration | RateVesselPrep | RateRecovery | RateManufacturing,

        EfficiencyGainLC = 1L << 41,

        MaxEfficiencyLC = 1L << 42,

        RateResearch = 1L << 43,

        RateTraining = 1L << 44,
        TimeRetirement = 1L << 45,
        TimeInactive = 1L << 46,
        CrewTimes = RateTraining | TimeRetirement | TimeInactive,

        RateUnlockCreditIncrease = 1L << 47,

        Any = ~0
    }

    public enum CurrencyRP0
    {
        [Description("#autoLOC_7001031")]
        Funds = 0,
        [Description("#autoLOC_7001032")]
        Science = 1,
        [Description("#autoLOC_7001033")]
        Reputation = 2,
        [Description("#rp0_Currency_Confidence")]
        Confidence = 3,
        [Description("#rp0_Currency_Time")]
        Time = 4,
        [Description("#rp0_Currency_Rate")]
        Rate = 5,

        MAX = Rate,

        Invalid = 99,
    }
}
