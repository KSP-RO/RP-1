using System;
using Strategies;
using System.Reflection;
using KSP.Localization;

namespace RP0
{
    public class StrategyRP0 : Strategies.Strategy
    {
        public bool NextTextIsShowSelected = false;

        // Reflection of private fields
        private static FieldInfo isActive = typeof(Strategy).GetField("isActive", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo dateActivated = typeof(Strategy).GetField("dateActivated", BindingFlags.Instance | BindingFlags.NonPublic);

        public StrategyConfigRP0 ConfigRP0 { get; protected set; }

        public virtual void OnSetupConfig()
        {
            ConfigRP0 = Config as StrategyConfigRP0;
        }

        //public virtual bool CanBeActivatedOverride(out string reason)
        //{
        //    reason = string.Empty;
        //    return true;
        //}

        //public virtual bool CanBeDeactivatedOverride(out string reason)
        //{
        //    reason = string.Empty;
        //    return true;
        //}

        protected override bool CanActivate(ref string reason)
        {
            if (!CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupCosts, true).CanAfford())
            {
                reason = Localizer.GetStringByTag("#rp0LeaderCannotAffordAppoint");
                return false;
            }
            if (!CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupRequirements, true).CanAfford())
            {
                reason = Localizer.GetStringByTag("##rp0LeaderUnderReqAppoint");
                return false;
            }
            return true;
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            StrategyConfigRP0.ActivatedStrategies.Add(ConfigRP0.Name);
            if (!string.IsNullOrEmpty(ConfigRP0.RemoveOnDeactivateTag))
                StrategyConfigRP0.ActivatedStrategies.Add(ConfigRP0.RemoveOnDeactivateTag);
        }

        public virtual bool ActivateOverride()
        {
            if (!CanBeActivated(out _))
                return false;

            isActive.SetValue(this, true);
            Register();
            dateActivated.SetValue(this, KSPUtils.GetUT());

            CurrencyUtils.ProcessCurrency(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupCosts, true);

            KerbalConstructionTime.KCTGameStates.RecalculateBuildRates();

            return true;
        }

        public virtual bool DeactivateOverride()
        {
            if (!CanBeDeactivated(out _))
                return false;

            isActive.SetValue(this, false);
            Unregister();
            CurrencyUtils.ProcessCurrency(TransactionReasonsRP0.StrategySetup, ConfigRP0.EndCosts, true);

            KerbalConstructionTime.KCTGameStates.RecalculateBuildRates();

            return true;
        }

        protected virtual string ConstructText(bool extendedInfo, bool useDescriptionHeader = true, bool showDescriptionInNonExtended = false)
        {
            string text = "";
            if (useDescriptionHeader)
                text += RichTextUtil.Title(Localizer.GetStringByTag("#autoLOC_304558"));

            // this is handled as part of the Admin window's strategy window.
            // But if it's an active strat, append if desired.
            if (!extendedInfo && showDescriptionInNonExtended)
                text += Localizer.Format("#autoLOC_304559", Description);

            if (extendedInfo && ConfigRP0.RemoveOnDeactivate)
                text += $"<b><color=#{RUIutils.ColorToHex(XKCDColors.KSPNotSoGoodOrange)}>{Localizer.GetStringByTag("#rp0LeaderCantReappoint")}</color>\n\n";

            text += RichTextUtil.Title(Localizer.GetStringByTag("#autoLOC_304560"));

            foreach (StrategyEffect strategyEffect in Effects)
            {
                text += "<b><color=#" + RUIutils.ColorToHex(RichTextUtil.colorParams) + ">* " + strategyEffect.Description + "</color></b>\n";
            }

            text += "\n";
            if (IsActive)
            {
                if (LeastDuration > 0)
                {
                    if (DateActivated + LeastDuration <= KSPUtils.GetUT())
                    {
                        text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderCanRemove"), Localizer.GetStringByTag("#autoLOC_6002417"));
                    }
                    else
                    {
                        if (GameSettings.SHOW_DEADLINES_AS_DATES)
                            text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderCanRemoveOn"), KSPUtil.PrintDate(LeastDuration + KSPUtils.GetUT(), false, false));
                        else
                            text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderCanRemoveIn"), KSPUtil.PrintDateDeltaCompact(LeastDuration, false, false));
                    }
                }
                if (LongestDuration > 0)
                {
                    if (GameSettings.SHOW_DEADLINES_AS_DATES)
                        text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderRetiresOn"), KSPUtil.PrintDate(LongestDuration + KSPUtils.GetUT(), false, false));
                    else
                        text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderRetiresIn"), KSPUtil.PrintDateDeltaCompact(LongestDuration, false, false));
                }
            }
            else
            {
                if (LeastDuration > 0)
                {
                    text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderCanRemoveAfter"), KSPUtil.PrintDateDeltaCompact(LeastDuration, false, false));
                }
                if (LongestDuration > 0)
                {
                    text += RichTextUtil.TextParam(Localizer.GetStringByTag("#rp0LeaderRetiresAfter"), KSPUtil.PrintDateDeltaCompact(LongestDuration, false, false));
                }
            }


            string costString = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupCosts, true).GetCostLine(true, true, true, false, "   ");
            if (costString != string.Empty)
            {
                text += RichTextUtil.TextAdvance(Localizer.GetStringByTag("#autoLOC_304612"), costString);
            }
            string requireString = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupRequirements, true).GetCostLine(true, true, true, false, "   ");
            if (requireString != string.Empty)
            {
                text += RichTextUtil.TextAdvance(Localizer.GetStringByTag("#rp0LeaderHireRequirements"), requireString);
            }

            return text;
        }

        protected override string GetText()
        {
            bool extendedInfo = NextTextIsShowSelected;
            NextTextIsShowSelected = false;
            return ConstructText(extendedInfo, true, true);
        }

        protected override string GetEffectText()
        {
            bool extendedInfo = NextTextIsShowSelected;
            NextTextIsShowSelected = false;
            return ConstructText(extendedInfo, false, false);
        }
    }
}
