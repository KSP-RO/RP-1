using System;
using Strategies;
using System.Reflection;
using KSP.Localization;

namespace RP0
{
    /// <summary>
    /// We use Harmony to replace all base Strategy objects created by stock with this class instead.
    /// This extends and improves the base Strategy class and includes
    /// those bits needed to support Leaders.
    /// </summary>
    public class StrategyRP0 : Strategies.Strategy
    {
        public bool ShowExtendedInfo = false;

        /// <summary>
        /// A direct getter for the enhanced StrategyConfigRP0 since Strategy(RP0).Config
        /// is a downcasted StrategyConfig even though the underlying object will be
        /// StrategyConfigRP0.
        /// </summary>
        public StrategyConfigRP0 ConfigRP0 { get; protected set; }

        protected double dateDeactivated;
        public double DateDeactivated => dateDeactivated;

        /// <summary>
        /// A new function that can be selectively overridden and is called when SetupConfig runs
        /// At base it just links to the StrategyConfigRP0
        /// </summary>
        public virtual void OnSetupConfig()
        {
            ConfigRP0 = Config as StrategyConfigRP0;
        }

        // For now we don't need to make overridable versions of CanBe(De)Activated,
        // so these are commented out.
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

        public override void OnLoad(ConfigNode node)
        {
            node.TryGetValue("dateDeactivated", ref dateDeactivated);
        }

        public override void OnSave(ConfigNode node)
        {
            node.AddValue("dateDeactivated", dateDeactivated.ToString("G17"));
        }

        /// <summary>
        /// This overrides base to add support for some leader specifc bits
        /// like supporting both costs and requirements natively with their new loc strings
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public override bool CanActivate(ref string reason)
        {
            if (!CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupCosts, true).CanAfford())
            {
                reason = Localizer.Format("#rp0_Leaders_Appoint_CannotAfford");
                return false;
            }
            if (!CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupRequirements, true).CanAfford())
            {
                reason = Localizer.Format("#rp0_Leaders_HiringRequirements_Unmet");
                return false;
            }
            return true;
        }

        /// <summary>
        /// This overrides base to check strategy deactivate cost only if the strategy (leader) isn't auto-expiring
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public override bool CanDeactivate(ref string reason)
        {
            if (KSPUtils.GetUT() - DateActivated < LongestDuration &&    // Leader retirement is free
                !CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.LeaderRemove, 0d, 0d, -DeactivateCost()).CanAfford())
            {
                reason = Localizer.GetStringByTag("#rp0_Leaders_Remove_CannotAfford");
                return false;
            }

            return base.CanDeactivate(ref reason);
        }

        public override void OnRegister()
        {
            base.OnRegister();
        }

        /// <summary>
        /// We use Harmony to call this instead of the base, non-overridable, Activate.
        /// This replaces base.Activate
        /// </summary>
        /// <returns></returns>
        public virtual bool ActivateOverride()
        {
            if (!CanBeActivated(out _))
                return false;

            PerformActivate(true, true);

            

            return true;
        }

        /// <summary>
        /// Teh
        /// </summary>
        public void PerformActivate(bool useCurrency, bool recalcRates)
        {
            isActive = true;
            Register();
            dateActivated = KSPUtils.GetUT();

            // Update ActivatedStrategies to show that this strategy is currently active (and clobber
            // the UT it was last deactivated, if any).
            dateDeactivated = -1d;
            StrategyConfigRP0.ActivatedStrategies[ConfigRP0.Name] = -1d;
            // If there's a tag for this strategy, do the same for that tag as well.
            // Some sets of strategies have the same tag.
            if (!string.IsNullOrEmpty(ConfigRP0.RemoveOnDeactivateTag))
                StrategyConfigRP0.ActivatedStrategies[ConfigRP0.RemoveOnDeactivateTag] = -1d;

            if (useCurrency)
                CurrencyUtils.ProcessCurrency(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupCosts, true);

            if (recalcRates)
            {
                KerbalConstructionTime.KCTGameStates.RecalculateBuildRates();
                Programs.ProgramHandler.Instance.ClampFunding();
            }
        }

        /// <summary>
        /// We use Harmony to call this instead of the base, non-overridable, Deactivate.
        /// This replaces base.Deactivate
        /// </summary>
        /// <returns></returns>
        public virtual bool DeactivateOverride()
        {
            if (!CanBeDeactivated(out _))
                return false;

            // If there's a deactivate cost to rep, pay the cost.
            // FIXME this is a hardcode for leaders.
            float deactivateRep = (float)DeactivateCost();
            if (deactivateRep != 0f)
                Reputation.Instance.AddReputation(-deactivateRep, TransactionReasonsRP0.LeaderRemove.Stock());

            isActive = false;

            // Update the UT at which this strategy (and this strategy group) was deactivated
            dateDeactivated = KSPUtils.GetUT();
            StrategyConfigRP0.ActivatedStrategies[ConfigRP0.Name] = dateDeactivated;
            if (!string.IsNullOrEmpty(ConfigRP0.RemoveOnDeactivateTag))
                StrategyConfigRP0.ActivatedStrategies[ConfigRP0.RemoveOnDeactivateTag] = dateDeactivated; // will stomp the previous one.


            Unregister();

            KerbalConstructionTime.KCTGameStates.RecalculateBuildRates();
            Programs.ProgramHandler.Instance.ClampFunding();

            return true;
        }

        /// <summary>
        /// The rep cost to pay when firing a leader, given the length of time the leader has been active
        /// vs the total possible time the leader can be active
        /// </summary>
        /// <returns></returns>
        public virtual double DeactivateCost()
        {
            return UtilMath.LerpUnclamped(Reputation.Instance.reputation * ConfigRP0.RemovalCostRepPercent,
                                          0d,
                                          Math.Pow(UtilMath.InverseLerp(LeastDuration, LongestDuration, KSPUtils.GetUT() - DateActivated), ConfigRP0.RemovalCostLerpPower));
        }

        public virtual string DeactivateCostString()
        {
            return CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.LeaderRemove, 0d, 0d, -DeactivateCost(), 0d, 0d).GetCostLineOverride(true, false, true, true);
        }

        /// <summary>
        /// Overridable method to handle pretty-printing the strategy description
        /// </summary>
        /// <param name="extendedInfo"></param>
        /// <param name="useDescriptionHeader"></param>
        /// <param name="showDescriptionInNonExtended"></param>
        /// <returns></returns>
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
            {
                if (ConfigRP0.ReactivateCooldown > 0d)
                    text += $"<b><color=#{RUIutils.ColorToHex(XKCDColors.KSPNotSoGoodOrange)}>{Localizer.Format("#rp0_Leaders_Deactivates_WithCooldown", KSPUtil.PrintDateDelta(ConfigRP0.ReactivateCooldown, false))}</color>\n\n";
                else
                    text += $"<b><color=#{RUIutils.ColorToHex(XKCDColors.KSPNotSoGoodOrange)}>{Localizer.GetStringByTag("#rp0_Leaders_Deactivates")}</color>\n\n";
            }

            // We'll use the ShowExtendedInfo field to signal we need to print title too
            bool wasExtended = ShowExtendedInfo;
            ShowExtendedInfo = extendedInfo;
            text += RichTextUtil.Title(Localizer.Format("#autoLOC_304560"));
            foreach (StrategyEffect strategyEffect in Effects)
            {
                text += "<b><color=#" + RUIutils.ColorToHex(RichTextUtil.colorParams) + ">* " + strategyEffect.Description + "</color></b>\n";
            }
            ShowExtendedInfo = wasExtended;

            text += "\n";
            if (IsActive)
            {
                string costStr = DeactivateCostString();
                if (LeastDuration > 0 || !string.IsNullOrEmpty(costStr))
                {
                    if (DateActivated + LeastDuration <= KSPUtils.GetUT())
                    {
                        text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_CanRemove"), string.IsNullOrEmpty(costStr) ? Localizer.Format("#autoLOC_6002417") : Localizer.Format("#rp0_Generic_Cost", costStr));
                    }
                    else
                    {
                        if (GameSettings.SHOW_DEADLINES_AS_DATES)
                            text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_CanRemoveOn"), KSPUtil.PrintDate(LeastDuration + KSPUtils.GetUT(), false, false));
                        else
                            text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_CanRemoveIn"),
                                extendedInfo ? KSPUtil.PrintDateDelta(LeastDuration, false, false)
                                : KSPUtil.PrintDateDeltaCompact(LeastDuration, false, false));
                    }
                }
                if (LongestDuration > 0)
                {
                    if (GameSettings.SHOW_DEADLINES_AS_DATES)
                        text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_RetiresOn"), KSPUtil.PrintDate(LongestDuration + KSPUtils.GetUT(), false, false));
                    else
                        text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_RetiresIn"), 
                            extendedInfo ? KSPUtil.PrintDateDelta(LongestDuration + KSPUtils.GetUT() - DateActivated, false, false)
                            : KSPUtil.PrintDateDeltaCompact(LongestDuration + KSPUtils.GetUT() - DateActivated, false, false));
                }
            }
            else
            {
                if (LeastDuration > 0)
                {
                    text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_CanRemoveAfter"),
                        extendedInfo ? KSPUtil.PrintDateDelta(LeastDuration, false, false)
                            : KSPUtil.PrintDateDeltaCompact(LeastDuration, false, false));
                }
                if (LongestDuration > 0)
                {
                    text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_RetiresAfter"),
                        extendedInfo ? KSPUtil.PrintDateDelta(LongestDuration, false, false)
                            : KSPUtil.PrintDateDeltaCompact(LongestDuration, false, false));
                }

                string costString = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupCosts, true).GetCostLineOverride(true, true, true, false, "   ");
                if (costString != string.Empty)
                {
                    text += RichTextUtil.TextAdvance(Localizer.Format("#autoLOC_304612"), costString);
                }
                string requireString = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupRequirements, true).GetCostLineOverride(true, true, true, false, "   ");
                if (requireString != string.Empty)
                {
                    text += RichTextUtil.TextAdvance(Localizer.Format("#rp0_Leaders_HiringRequirements"), requireString);
                }
            }

            return text;
        }

        public override string GetText()
        {
            bool extendedInfo = ShowExtendedInfo;
            ShowExtendedInfo = false;
            return ConstructText(extendedInfo, true, true);
        }

        public override string GetEffectText()
        {
            bool extendedInfo = ShowExtendedInfo;
            ShowExtendedInfo = false;
            return ConstructText(extendedInfo, false, false);
        }
        public override string ToString() => ConfigRP0?.Name;
    }
}
