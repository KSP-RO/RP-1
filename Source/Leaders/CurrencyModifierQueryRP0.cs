using System;
using KSP.Localization;
using System.Collections.Generic;
using System.ComponentModel;

namespace RP0
{
    public class CurrencyModifierQueryRP0 : CurrencyModifierQuery
    {
        public TransactionReasonsRP0 reasonRP0;

        private double inputFunds;

        private double inputScience;

        private double inputRep;

        private double inputConf;

        private double inputTime;

        private double deltaFunds;

        private double deltaScience;

        private double deltaRep;

        private double deltaConf;

        private double deltaTime;

        private double deltaRate;

        public CurrencyModifierQueryRP0(TransactionReasons reason, double f0, float s0, float r0)
            : base(reason, (float)f0, s0, r0)
        {
            this.reasonRP0 = (TransactionReasonsRP0)reason;
            inputFunds = f0;
            inputScience = s0;
            inputRep = r0;
            inputConf = 0d;
            inputTime = 0d;
            deltaFunds = 0d;
            deltaScience = 0d;
            deltaRep = 0d;
            deltaConf = 0d;
            deltaTime = 0d;
            deltaRate = 0d;
        }

        public CurrencyModifierQueryRP0(TransactionReasonsRP0 reason, double f0, double s0, double r0, double c0, double t0)
            : base((TransactionReasons)reason, (float)f0, (float)s0, (float)r0)
        {
            this.reasonRP0 = reason;
            this.reason = (TransactionReasons)reason;
            inputFunds = f0;
            inputScience = s0;
            inputRep = r0;
            inputConf = c0;
            inputTime = t0;
            deltaFunds = 0d;
            deltaScience = 0d;
            deltaRep = 0d;
            deltaConf = 0d;
            deltaTime = 0d;
            deltaRep = 0d;
        }

        public double GetInput(CurrencyRP0 c)
        {
            return c switch
            {
                CurrencyRP0.Funds => inputFunds,
                CurrencyRP0.Science => inputScience,
                CurrencyRP0.Reputation => inputRep,
                CurrencyRP0.Confidence => inputConf,
                CurrencyRP0.Time => inputTime,
                _ => 1d,
            };
        }

        public void AddDelta(CurrencyRP0 c, double val)
        {
            switch (c)
            {
                case CurrencyRP0.Funds:
                    deltaFunds += val;
                    break;
                case CurrencyRP0.Science:
                    deltaScience += val;
                    break;
                case CurrencyRP0.Reputation:
                    deltaRep += val;
                    break;
                case CurrencyRP0.Confidence:
                    deltaConf += val;
                    break;
                case CurrencyRP0.Time:
                    deltaTime += val;
                    break;
                case CurrencyRP0.Rate:
                    deltaRate += val;
                    break;
            }
        }

        public double GetEffectDelta(CurrencyRP0 c)
        {
            return c switch
            {
                CurrencyRP0.Funds => deltaFunds,
                CurrencyRP0.Science => deltaScience,
                CurrencyRP0.Reputation => deltaRep,
                CurrencyRP0.Confidence => deltaConf,
                CurrencyRP0.Time => deltaTime,
                CurrencyRP0.Rate => deltaRate,
                _ => 0f,
            };
        }

        public double GetTotal(CurrencyRP0 c)
        {
            return GetInput(c) + GetEffectDelta(c);
        }

        public static bool ApproximatelyZero(double a)
        {
            double absA = Math.Abs(a);
            return absA < Math.Max(1E-06d * absA, UnityEngine.Mathf.Epsilon * 8d);
        }

        public string GetCostLineOverride(bool displayInverted = true, bool useCurrencyColors = false, bool useInsufficientCurrencyColors = true, bool includePercentage = false, string seperator = ", ")
        {
            double funds = GetTotal(CurrencyRP0.Funds);
            double sci = GetTotal(CurrencyRP0.Science);
            double rep = GetTotal(CurrencyRP0.Reputation);
            double conf = GetTotal(CurrencyRP0.Confidence);
            double time = GetTotal(CurrencyRP0.Time);
            double rate = GetTotal(CurrencyRP0.Rate);
            if (displayInverted)
            {
                funds = -funds;
                sci = -sci;
                rep = -rep;
                conf = -conf;
                time = -time;
                // rate can't be inverted
            }
            bool canAffordFunds = CanAfford(CurrencyRP0.Funds);
            bool canAffordSci = CanAfford(CurrencyRP0.Science);
            bool canAffordRep = CanAfford(CurrencyRP0.Reputation);
            bool canAffordConf = CanAfford(CurrencyRP0.Confidence);
            string textFunds = ((!ApproximatelyZero(funds)) ? ("<sprite=\"CurrencySpriteAsset\" name=\"Funds\" tint=1> " + KSPUtil.LocalizeNumber(funds, "N2")) : "");
            string textSci = ((!ApproximatelyZero(sci)) ? ("<sprite=\"CurrencySpriteAsset\" name=\"Science\" tint=1> " + KSPUtil.LocalizeNumber(sci, "N0")) : "");
            string textRep = ((!ApproximatelyZero(rep)) ? ("<sprite=\"CurrencySpriteAsset\" name=\"Reputation\" tint=1> " + KSPUtil.LocalizeNumber(rep, "F2")) : "");
            string textConf = ((!ApproximatelyZero(conf)) ? ("<sprite=\"CurrencySpriteAsset\" name=\"Flask\" tint=1> " + KSPUtil.LocalizeNumber(conf, "F2")) : "");
            string textTime = ((!ApproximatelyZero(time)) ? (!double.IsInfinity(time) && !double.IsNaN(time) && time < (100 * 365.25d * 86400d) ? KSPUtil.PrintDateDeltaCompact(time, time < 7d * 86400d, time < 600) : Localizer.GetStringByTag("#autoLOC_462439")) : "");
            string textRate = ((!ApproximatelyZero(rate - 1d)) ? LocalizationHandler.FormatRatioAsPercent(rate) : "");
            string resultText = "";
            if (!string.IsNullOrEmpty(textFunds))
            {
                resultText = ((!((useInsufficientCurrencyColors && !canAffordFunds) || useCurrencyColors)) ? textFunds : StringBuilderCache.Format("<color={0}>{1}</color>", (!useInsufficientCurrencyColors || canAffordFunds) ? "#B4D455" : XKCDColors.HexFormat.BrightOrange, textFunds));
                if (includePercentage && deltaFunds != 0f)
                {
                    resultText = resultText + " " + GetEffectPercentageText(Currency.Funds, "N1", GetTextStyleFromInput(inputFunds));
                }
            }
            if (!string.IsNullOrEmpty(textSci))
            {
                if (!string.IsNullOrEmpty(resultText))
                {
                    resultText += seperator;
                }
                resultText = ((!((useInsufficientCurrencyColors && !canAffordSci) || useCurrencyColors)) ? (resultText + textSci) : (resultText + StringBuilderCache.Format("<color={0}>{1}</color>", (!useInsufficientCurrencyColors || canAffordSci) ? "#6DCFF6" : XKCDColors.HexFormat.BrightOrange, textSci)));
                if (includePercentage && deltaScience != 0f)
                {
                    resultText = resultText + " " + GetEffectPercentageText(CurrencyRP0.Science, "N1", GetTextStyleFromInput(inputScience));
                }
            }
            if (!string.IsNullOrEmpty(textRep))
            {
                if (!string.IsNullOrEmpty(resultText))
                {
                    resultText += seperator;
                }
                resultText = ((!((useInsufficientCurrencyColors && !canAffordRep) || useCurrencyColors)) ? (resultText + textRep) : (resultText + StringBuilderCache.Format("<color={0}>{1}</color>", (!useInsufficientCurrencyColors || canAffordRep) ? "#E0D503" : XKCDColors.HexFormat.BrightOrange, textRep)));
                if (includePercentage && deltaRep != 0f)
                {
                    resultText = resultText + " " + GetEffectPercentageText(CurrencyRP0.Reputation, "N1", GetTextStyleFromInput(inputRep));
                }
            }
            if (!string.IsNullOrEmpty(textConf))
            {
                if (!string.IsNullOrEmpty(resultText))
                {
                    resultText += seperator;
                }
                resultText = ((!((useInsufficientCurrencyColors && !canAffordConf) || useCurrencyColors)) ? (resultText + textConf) : (resultText + StringBuilderCache.Format("<color={0}>{1}</color>", (!useInsufficientCurrencyColors || canAffordConf) ? $"#{RUIutils.ColorToHex(XKCDColors.KSPBadassGreen)}" : XKCDColors.HexFormat.BrightOrange, textConf)));
                if (includePercentage && deltaConf != 0f)
                {
                    resultText = resultText + " " + GetEffectPercentageText(CurrencyRP0.Confidence, "N1", GetTextStyleFromInput(inputConf));
                }
            }
            if (!string.IsNullOrEmpty(textTime))
            {
                if (!string.IsNullOrEmpty(resultText))
                {
                    resultText += seperator;
                }
                resultText += textTime;
                if (includePercentage && deltaTime != 0f)
                {
                    resultText = resultText + " " + GetEffectPercentageText(CurrencyRP0.Time, "N1", GetTextStyleFromInput(inputTime));
                }
            }
            if (!string.IsNullOrEmpty(textRate))
            {
                if (!string.IsNullOrEmpty(resultText))
                {
                    resultText += seperator;
                }
                resultText += textRate;
                if (includePercentage && deltaRate != 0f)
                {
                    resultText = resultText + " " + GetEffectPercentageText(CurrencyRP0.Rate, "N1", TextStyling.OnGUI);
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
            double amount = -(GetInput(c) + GetEffectDelta(c));
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
                        return UtilMath.RoundToPlaces(ResearchAndDevelopment.Instance.Science, 1) >= UtilMath.RoundToPlaces(amount, 1);
                    }
                    return true;
                case CurrencyRP0.Reputation:
                    if (Reputation.Instance != null)
                    {
                        return UtilMath.RoundToPlaces(Reputation.Instance.reputation, 1) >= UtilMath.RoundToPlaces(amount, 1);
                    }
                    return true;

                case CurrencyRP0.Confidence:
                    if (Confidence.Instance != null)
                    {
                        return UtilMath.RoundToPlaces(Confidence.CurrentConfidence, 1) >= UtilMath.RoundToPlaces(amount, 1);
                    }
                    return true;
            }
        }

        public string GetEffectDeltaText(CurrencyRP0 c, string format, TextStyling textStyle = TextStyling.None)
        {
            string text = "";
            double delta = 0d;
            switch (c)
            {
                case CurrencyRP0.Funds:
                    delta = deltaFunds;
                    break;
                case CurrencyRP0.Science:
                    delta = deltaScience;
                    break;
                case CurrencyRP0.Reputation:
                    delta = deltaRep;
                    break;
                case CurrencyRP0.Confidence:
                    delta = deltaConf;
                    break;
                case CurrencyRP0.Time:
                    delta = deltaTime;
                    break;
                case CurrencyRP0.Rate:
                    delta = deltaRate;
                    break;
            }
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
            double delta = 0d;
            double input = 0d;
            switch (c)
            {
                case CurrencyRP0.Funds:
                    delta = deltaFunds;
                    input = inputFunds;
                    break;
                case CurrencyRP0.Science:
                    delta = deltaScience;
                    input = inputScience;
                    break;
                case CurrencyRP0.Reputation:
                    delta = deltaRep;
                    input = inputRep;
                    break;
                case CurrencyRP0.Confidence:
                    delta = deltaConf;
                    input = inputConf;
                    break;
                case CurrencyRP0.Time:
                    delta = deltaTime;
                    input = inputTime;
                    break;
                case CurrencyRP0.Rate:
                    delta = 1d + deltaRate;
                    input = 1d;
                    break;
            }
            if (delta != 0f && input != 0f)
            {
                double percent = delta / input * 100d;
                string text = percent.ToString(format);
                return textStyle switch
                {
                    TextStyling.OnGUI => ((delta > 0f) ? "<color=#caff00>(+" : "<color=#feb200>(") + text + "%)</color>",
                    TextStyling.EzGUIRichText => ((delta > 0f) ? "<#caff00>(+" : "<#feb200>(") + text + "%)</>",
                    TextStyling.OnGUI_LessIsGood => ((delta > 0f) ? "<color=#feb200><+" : "<color=#caff00><") + text + "%></color>",
                    TextStyling.EzGUIRichText_LessIsGood => ((delta > 0f) ? "<#feb200>(+" : "<#caff00>(") + text + ")%</>",
                    _ => ((delta > 0f) ? "(+" : "(") + text + "%)",
                };
            }
            return "";
        }

        public static TextStyling GetTextStyleFromInput(double input)
        {
            if (input < 0)
                return TextStyling.OnGUI_LessIsGood;

            return TextStyling.OnGUI;
        }

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
                dict.ValueOrDefault(CurrencyRP0.Time));
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

        RateResearch = 1 << 42,

        RateTraining = 1 << 43,

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
    }
}
