using System;
using KSP.Localization;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections;
using UnityEngine;

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
            Debug.LogError($"[RP-0]: CurrencyModifierQuery: Something tried to AddDelta! Currency {c} and values {val} for reason {reasonRP0}");
            if (inputs[c] == 0d)
                return;

            multipliers[c] = multipliers[c] * (inputs[c] + val) / inputs[c];
        }

        public void Multiply(CurrencyRP0 c, double mult)
        {
            multipliers[c] = multipliers[c] * mult;
        }

        public double GetEffectDelta(CurrencyRP0 c)
        {
            //return c switch
            //{
            //    CurrencyRP0.Funds => deltaFunds,
            //    CurrencyRP0.Science => deltaScience,
            //    CurrencyRP0.Reputation => deltaRep,
            //    CurrencyRP0.Confidence => deltaConf,
            //    CurrencyRP0.Time => deltaTime,
            //    CurrencyRP0.Rate => deltaRate,
            //    _ => 0f,
            //};
            return inputs[c] * (multipliers[c] - 1d);
        }

        public double GetTotal(CurrencyRP0 c)
        {
            return inputs[c] * multipliers[c];
        }

        public static bool ApproximatelyZero(double a)
        {
            double absA = Math.Abs(a);
            return absA < Math.Max(1E-06d * absA, UnityEngine.Mathf.Epsilon * 8d);
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
            "#C8D986", // conf
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

        public string GetCostLineOverride(bool displayInverted = true, bool useCurrencyColors = false, bool useInsufficientCurrencyColors = true, bool includePercentage = false, string seperator = ", ", bool flipRateDeltaColoring = false)
        {
            CurrencyArray outputs = new CurrencyArray();
            bool[] canAffords = new bool[outputs.Length];
            double invMult = displayInverted ? -1d : 1d;
            int rateIndex = (int)CurrencyRP0.Rate;
            for (int i = 0; i < outputs.Length; ++i)
            {
                double amount = inputs[i] * multipliers[i];
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
                    if (multipliers[i] == 1d)
                        continue;
                }
                else if (ApproximatelyZero(outputs[i]))
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
                        amountText = Localizer.Format("#rp0CurrencyRateFormat", amount.ToString("N2"));
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

                if (includePercentage && multipliers[i] != 1d)
                {
                    // Normally, for a currency less is good if it's a cost.
                    // For time, less is good unless we're flipping
                    // for rate, more is good unless we're flipping.
                    bool lessGood = c == CurrencyRP0.Rate ? flipRateDeltaColoring : inputs[i] < 0;
                    resultText += $" <color={TextStylingColor(inputs[i] < 0, lessGood)}>({LocalizationHandler.FormatRatioAsPercent(multipliers[i])})</color>";
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
            double amount = -inputs[c] * multipliers[c];
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
            double delta = inputs[c] * (multipliers[c] - 1d);
            
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
                double percent = (multipliers[c] - 1d) * 100d;
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

        public static string TextStylingColor(bool negative, bool negativeGood) => negative ^ negativeGood ? "#caff00" : "#feb200";

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

    [System.Flags]
    public enum TransactionReasonsRP0 : long
    {
        None = 0,

        ContractAdvance = 1 << 1,
        ContractReward = 1 << 2,
        ContractPenalty = 1 << 3,
        Contracts = ContractAdvance | ContractPenalty | ContractReward /*| ContractDecline*/,

        VesselRollout = 1 << 4,
        VesselRecovery = 1 << 5,
        VesselLoss = 1 << 6,
        Vessels = VesselRollout | VesselRecovery | VesselLoss,

        //StrategyInput = 1 << 7,
        //StrategyOutput = 1 << 8,
        StrategySetup = 1 << 9,
        //Strategies = StrategyInput | StrategyOutput | StrategySetup,

        ScienceTransmission = 1 << 10,

        StructureRepair = 1 << 11, // will be used for maintenance too
        //StructureCollapse = 1 << 12,
        StructureConstruction = 1 << 13,
        Structures = StructureRepair /*| StructureCollapse*/ | StructureConstruction,

        RnDTechResearch = 1 << 14,
        RnDPartPurchase = 1 << 15,
        RnDs = RnDTechResearch | RnDPartPurchase,

        Cheating = 1 << 16,
        CrewRecruited = 1 << 17,
        //ContractDecline = 1 << 18,
        //Progression = 1 << 19, -- We'll hijack this

        ProgramFunding = 1 << 20, // was Mission
        ProgramActivation = 1 << 21,

        RocketRollout = 1 << 22,
        AirLaunchRollout = 1 << 23,

        LeaderAppoint = 1 << 24,
        LeaderRemove = 1 << 25,
        Leaders = LeaderAppoint | LeaderRemove,

        RnDUpgradePurchase = 1 << 26,

        SalaryEngineers = 1 << 27,
        SalaryResearchers = 1 << 28,
        SalaryCrew = 1 << 29,
        Salary = SalaryEngineers | SalaryResearchers | SalaryCrew,

        CrewTraining = 1 << 30,
        Crew = CrewRecruited | SalaryCrew | CrewTraining,

        HiringEngineers = 1 << 31,
        HiringResearchers = 1 << 19,
        Hiring = HiringEngineers | HiringResearchers,

        Personnel = Salary | Crew | Hiring,

        StructureConstructionLC = 1 << 7,
        StructureRepairLC = 1 << 8,
        StructureConstructionAll = StructureConstruction | StructureConstructionLC,
        StructureRepairAll = StructureRepair | StructureRepairLC,

        Subsidy = 1 << 12,
        RepDecline = 1 << 18,

        // Time

        TimeProgramDeadline = 1 << 33,

        RateIntegrationVAB = 1 << 34,
        RateIntegrationSPH = 1 << 35,
        RateIntegration = RateIntegrationVAB | RateIntegrationSPH,

        RateRollout = 1 << 36,
        RateAirlaunch = 1 << 37,
        RateVesselPrep = RateRollout | RateAirlaunch,
        RateReconditioning = 1 << 38,
        RateRecovery = 1 << 39,

        RateManufacturing = 1 << 40,

        RateVessel = RateIntegration | RateVesselPrep | RateRecovery | RateManufacturing,

        EfficiencyGainLC = 1 << 41,

        MaxEfficiencyLC = 1 << 42,

        RateResearch = 1 << 43,

        RateTraining = 1 << 44,

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
        [Description("#rp0CurrencyConfidence")]
        Confidence = 3,
        [Description("#rp0CurrencyTime")]
        Time = 4,
        [Description("#rp0CurrencyRate")]
        Rate = 5,

        MAX = Rate,

        Invalid = 99,
    }
}
