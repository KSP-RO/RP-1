﻿using KSP.Localization;
using RP0.Programs;
using Strategies;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RP0
{
    /// <summary>
    /// We use Harmony to replace all base Strategy objects created by stock with this class instead.
    /// This extends and improves the base Strategy class and includes
    /// those bits needed to support Leaders.
    /// </summary>
    public class StrategyRP0 : Strategy
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

        public double RemovePenaltyDuration => ConfigRP0 != null && ConfigRP0.RemovePenaltyDuration != 0 ? ConfigRP0.RemovePenaltyDuration : LongestDuration;

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

            if (ConfigRP0.CannotActivative)
            {
                reason = string.Empty;
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
            if (Planetarium.GetUniversalTime() - DateActivated < RemovePenaltyDuration &&    // Free if leader is retiring or a predetermined duration has been reached
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

            PerformActivate(true);

            return true;
        }

        /// <summary>
        /// Teh
        /// </summary>
        public void PerformActivate(bool useCurrency)
        {
            isActive = true;
            Register();
            dateActivated = Planetarium.GetUniversalTime();

            // Update ActivatedStrategies to show that this strategy is currently active (and clobber
            // the UT it was last deactivated, if any).
            dateDeactivated = -1d;
            Programs.ProgramHandler.Instance.ActivatedStrategies[ConfigRP0.Name] = -1d;
            // If there's a tag for this strategy, do the same for that tag as well.
            // Some sets of strategies have the same tag.
            if (!string.IsNullOrEmpty(ConfigRP0.RemoveOnDeactivateTag))
                Programs.ProgramHandler.Instance.ActivatedStrategies[ConfigRP0.RemoveOnDeactivateTag] = -1d;

            if (useCurrency)
                CurrencyUtils.ProcessCurrency(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupCosts, true);

            UnityEngine.Debug.Log($"CLAYELADDEDLOGS ConfigRP0.Title: {ConfigRP0.Title}");
            if (this is Programs.ProgramStrategy ps)
            {
                CreateAlarm($"Deadline: {ConfigRP0.Title}", $"{ConfigRP0.Title} must be completed at this time to avoid penalties.", ps.Program.deadlineUT);
            }
            else
            {
                SpaceCenterManagement.Instance.RecalculateBuildRates();
                MaintenanceHandler.Instance?.UpdateUpkeep();
                Programs.ProgramHandler.Instance.OnLeaderChange();
                // FIXME add setup cost if we add setup costs to leaders
                CareerLog.Instance?.AddLeaderEvent(Config.Name, true, 0d);
                if (LeastDuration > 0 || RemovePenaltyDuration > 0 || LongestDuration > 0)
                {
                    ClearAlarms(ConfigRP0.Title);

                    if (LeastDuration > 0)
                    {
                        CreateAlarm($"Firing Cooldown Over: {ConfigRP0.Title}", $"{ConfigRP0.Title} can be removed at this time.", DateActivated + LeastDuration);
                    }
                    if (RemovePenaltyDuration > 0)
                    {
                        CreateAlarm($"Free to Terminate: {ConfigRP0.Title}", $"{ConfigRP0.Title} can be removed without paying a penalty fee at this time.", DateActivated + RemovePenaltyDuration);
                    }
                    if (LongestDuration > 0)
                    {
                        CreateAlarm($"Retirement: {ConfigRP0.Title}", $"{ConfigRP0.Title} will be removed at this time.", DateActivated + LongestDuration);
                    }
                }
            }
        }

        internal void CreateAlarm(string title, string description, double UT)
        {
            if (KACWrapper.KAC != null)
            {
                string alarmID = KACWrapper.KAC.CreateAlarm(KACWrapper.KACAPI.AlarmTypeEnum.Crew, title, UT);
                if (!string.IsNullOrEmpty(alarmID))
                {
                    KACWrapper.KACAPI.KACAlarm alarm = KACWrapper.KAC.Alarms.First(z => z.ID == alarmID);

                    alarm.AlarmAction = KACWrapper.KACAPI.AlarmActionEnum.KillWarp;
                    alarm.Notes = description;
                }
            }
            else
            {
                AlarmTypeRaw alarm = new AlarmTypeRaw
                {
                    title = title,
                    description = description,
                    ut = UT,
                };
                AlarmClockScenario.AddAlarm(alarm);
                alarm.actions.warp = AlarmActions.WarpEnum.KillWarp;
            }
        }

        internal void ClearAlarms(string title)
        {
            if (KACWrapper.KAC != null)
            {
                foreach (KACWrapper.KACAPI.KACAlarm alarm in KACWrapper.KAC.Alarms.Where(a => a.Name.Contains(title)).ToList())
                {
                    KACWrapper.KAC.DeleteAlarm(alarm.ID);
                }
            }
            else
            {
                List<uint> alarmsToRemove = [];

                foreach (uint id in AlarmClockScenario.Instance.alarms.Keys)
                {
                    if (AlarmClockScenario.Instance.alarms.TryGetValue(id, out AlarmTypeBase alarm) && alarm.title != null && alarm.title.Contains(title))
                    {
                        alarmsToRemove.Add(id);
                    }
                }

                foreach (uint id in alarmsToRemove)
                {
                    AlarmClockScenario.DeleteAlarm(id);
                }
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
            dateDeactivated = Planetarium.GetUniversalTime();
            Programs.ProgramHandler.Instance.ActivatedStrategies[ConfigRP0.Name] = dateDeactivated;
            if (!string.IsNullOrEmpty(ConfigRP0.RemoveOnDeactivateTag))
                Programs.ProgramHandler.Instance.ActivatedStrategies[ConfigRP0.RemoveOnDeactivateTag] = dateDeactivated; // will stomp the previous one.


            Unregister();

            if (!(this is Programs.ProgramStrategy))
            {
                SpaceCenterManagement.Instance.RecalculateBuildRates();
                MaintenanceHandler.Instance?.UpdateUpkeep();
                Programs.ProgramHandler.Instance.OnLeaderChange();
                CareerLog.Instance?.AddLeaderEvent(Config.Name, false, deactivateRep);
                if (ConfigRP0.ReactivateCooldown > 0)
                {
                    ClearAlarms(ConfigRP0.Title);

                    CreateAlarm($"Hiring Cooldown Over: {ConfigRP0.Title}", $"{ConfigRP0.Title} can be re-hired at this time.", dateDeactivated + ConfigRP0.ReactivateCooldown);
                }
            }

            return true;
        }

        /// <summary>
        /// The rep cost to pay when firing a leader, given the length of time the leader has been active
        /// vs the total possible time the leader can be active
        /// </summary>
        /// <returns></returns>
        public virtual double DeactivateCost()
        {
            double duration = Planetarium.GetUniversalTime() - DateActivated;
            if (duration >= RemovePenaltyDuration)
                return 0d;

            double repMult = 0.2d;
            if (duration > LeastDuration)
            {
                double frac = duration / (RemovePenaltyDuration - LeastDuration);
                repMult *= 0.1d / (frac * 0.5d + 0.1d) - 0.15d * frac;
            }

            return Reputation.Instance.reputation * repMult;
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
                    double deactivateDate = DateActivated + LeastDuration;
                    if (deactivateDate <= Planetarium.GetUniversalTime())
                    {
                        text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_CanRemove"), string.IsNullOrEmpty(costStr) ? Localizer.Format("#autoLOC_6002417") : Localizer.Format("#rp0_Generic_Cost", costStr));
                    }
                    else
                    {
                        if (GameSettings.SHOW_DEADLINES_AS_DATES)
                            text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_CanRemoveOn"), KSPUtil.PrintDate(deactivateDate, false, false));
                        else
                            text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_CanRemoveIn"),
                                extendedInfo ? KSPUtil.PrintDateDelta(deactivateDate - Planetarium.GetUniversalTime(), false, false)
                                : KSPUtil.PrintDateDeltaCompact(deactivateDate - Planetarium.GetUniversalTime(), false, false));
                    }
                }
                if (LongestDuration > 0)
                {
                    double retireDate = DateActivated + LongestDuration;
                    if (GameSettings.SHOW_DEADLINES_AS_DATES)
                        text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_RetiresOn"), KSPUtil.PrintDate(retireDate, false, false));
                    else
                        text += RichTextUtil.TextParam(Localizer.Format("#rp0_Leaders_RetiresIn"), 
                            extendedInfo ? KSPUtil.PrintDateDelta(retireDate - Planetarium.GetUniversalTime(), false, false)
                            : KSPUtil.PrintDateDeltaCompact(retireDate - Planetarium.GetUniversalTime(), false, false));
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

                string costString = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupCosts, true).GetCostLineOverride(true, true, true, false, false, "   ");
                if (costString != string.Empty)
                {
                    text += RichTextUtil.TextAdvance(Localizer.Format("#autoLOC_304612"), costString);
                }
                string requireString = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.StrategySetup, ConfigRP0.SetupRequirements, true).GetCostLineOverride(true, true, true, false, false, "   ");
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
