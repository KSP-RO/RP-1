using Strategies;
using UnityEngine;
using System.Collections.Generic;
using RP0.DataTypes;

namespace RP0
{
    public class StrategyConfigRP0 : StrategyConfig
    {
        public static Dictionary<string, double> ActivatedStrategies = new Dictionary<string, double>();

        /// <summary>
        /// Some leaders (Contractors, e.g.) can appear in two departments at once. If this is set,
        /// the strategy will appear in both the main department and this one.
        /// </summary>
        [Persistent]
        protected string departmentNameAlt;
        public string DepartmentNameAlt => departmentNameAlt;

        /// <summary>
        /// The image to use when the strategy is used as the department image.
        /// Resource URL.
        /// </summary>
        [Persistent]
        protected string iconDepartment;
        public string IconDepartment => iconDepartment;

        protected Texture2D iconDepartmentImage;
        public Texture2D IconDepartmentImage => iconDepartmentImage;

        /// <summary>
        /// Costs to activate, in the form of <Currency> = <amount>
        /// </summary>
        [Persistent]
        protected PersistentDictionaryValueTypes<CurrencyRP0, double> setupCosts = new PersistentDictionaryValueTypes<CurrencyRP0, double>();
        public PersistentDictionaryValueTypes<CurrencyRP0, double> SetupCosts => setupCosts;

        /// <summary>
        /// The player must have this much of each currency to activate, but none is spent due to this.
        /// </summary>
        [Persistent]
        protected PersistentDictionaryValueTypes<CurrencyRP0, double> setupRequirements = new PersistentDictionaryValueTypes<CurrencyRP0, double>();
        public PersistentDictionaryValueTypes<CurrencyRP0, double> SetupRequirements => setupRequirements;

        //[Persistent]
        //protected PersistentDictionaryValueTypes<CurrencyRP0, double> endCosts = new PersistentDictionaryValueTypes<CurrencyRP0, double>();
        //public PersistentDictionaryValueTypes<CurrencyRP0, double> EndCosts => endCosts;

        /// <summary>
        /// The strategy only becomes available when the given contract(s) complete.
        /// If any of these contracts are complete, the strategy becomes available.
        /// This is not exclusive with Program unlocks; a strategy with both set becomes available
        /// once the first condition is met.
        /// </summary>
        [Persistent]
        protected PersistentListValueType<string> unlockByContractComplete = new PersistentListValueType<string>();
        public PersistentListValueType<string> UnlockByContractComplete => unlockByContractComplete;

        /// <summary>
        /// The strategy only becomes available when the given program(s) complete.
        /// If any of these programs are complete, the strategy becomes available.
        /// This is not exclusive with Contract unlocks; a strategy with both set becomes available
        /// once the first condition is met.
        /// </summary>
        [Persistent]
        protected PersistentListValueType<string> unlockByProgramComplete = new PersistentListValueType<string>();
        public PersistentListValueType<string> UnlockByProgramComplete => unlockByProgramComplete;

        /// <summary>
        /// Does the strategy disappear from the list when it is deactivated?
        /// </summary>
        [Persistent]
        protected bool removeOnDeactivate;
        public bool RemoveOnDeactivate => removeOnDeactivate;

        /// <summary>
        /// This tag is set when a strategy is deactivated. If any strategy shares this tag, it too will disappear.
        /// </summary>
        [Persistent]
        protected string removeOnDeactivateTag;
        public string RemoveOnDeactivateTag => removeOnDeactivateTag;

        /// <summary>
        /// The cooldown after which the strategy becomes available again (if it is removed per above)
        /// </summary>
        [Persistent]
        protected double reactivateCooldown;
        public double ReactivateCooldown => reactivateCooldown;

        /// <summary>
        /// The cost (as a multiplier, i.e. 0.1 = 10%) to rep when instantly deactivating a strategy.
        /// This cost lerps downward over time.
        /// See Load() for default value.
        /// </summary>
        [Persistent]
        protected double removalCostRepPercent;
        public double RemovalCostRepPercent => removalCostRepPercent;

        /// <summary>
        /// The power to use when lerping down rep cost on deactivating early
        /// See Load() for default value.
        /// </summary>
        [Persistent]
        protected double removalCostLerpPower;
        public double RemovalCostLerpPower => removalCostLerpPower;

        // Will be called by transpiler of stock StrategyConfig.Create()
        // This is needed due to how transpilers work.
        protected static StrategyConfig NewBaseConfig() { return new StrategyConfigRP0(); }

        public void Load(ConfigNode node)
        {
            if (setupCosts == null)
                setupCosts = new PersistentDictionaryValueTypes<CurrencyRP0, double>();
            if (setupRequirements == null)
                setupRequirements = new PersistentDictionaryValueTypes<CurrencyRP0, double>();
            //if (endCosts == null)
            //    endCosts = new PersistentDictionaryValueTypes<CurrencyRP0, double>();
            if (unlockByContractComplete == null)
                unlockByContractComplete = new PersistentListValueType<string>();
            if (unlockByProgramComplete == null)
                unlockByProgramComplete = new PersistentListValueType<string>();

            // For some reason need to set here, not in ctor.
            removalCostRepPercent = 0.1d;
            removalCostLerpPower = 3d;


            ConfigNode.LoadObjectFromConfig(this, node);

            if (!string.IsNullOrEmpty(iconDepartment))
                iconDepartmentImage = GameDatabase.Instance.GetTexture(iconDepartment, false);
        }

        new public static StrategyConfigRP0 Create(ConfigNode node, List<DepartmentConfig> departments)
        {
            StrategyConfigRP0 cfg = StrategyConfig.Create(node, departments) as StrategyConfigRP0;
            cfg.Load(node);

            return cfg;
        }

        /// <summary>
        /// Is the strategy unlocked?
        /// Contract and program requirements can be set, but the first valid one
        /// makes the strategy unlocked.
        /// </summary>
        /// <returns></returns>
        public virtual bool IsUnlocked()
        {
            if (unlockByContractComplete.Count == 0 && unlockByProgramComplete.Count == 0)
                return true;

            foreach (string s in unlockByContractComplete)
                if (Programs.ProgramHandler.Instance.CompletedCCContracts.Contains(s))
                    return true;

            foreach (string s in unlockByProgramComplete)
                if (Programs.ProgramHandler.Instance.CompletedPrograms.Find(p => p.name == s) != null)
                    return true;

            return false;
        }

        /// <summary>
        /// Check to see if the Strategy is available to be listed (and activated) in the Admin UI.
        /// This involves seeing if the strategy was previously active and, if so, how long it's been since
        /// it was deactivated. It also involves checking tag as well as name.
        /// </summary>
        /// <param name="dateDeactivated"></param>
        /// <returns></returns>
        public virtual bool IsAvailable(double dateDeactivated)
        {
            if (dateDeactivated < 0d)
                return false;

            // Check deactivation
            double nameDeactivate = ActivatedStrategies.ValueOrDefault(Name);
            double tagDeactivate = 0d;
            if (!string.IsNullOrEmpty(removeOnDeactivateTag))
                tagDeactivate = ActivatedStrategies.ValueOrDefault(removeOnDeactivateTag);

            // we are skipping the case where the strategy or its tag is active, but 
            // groupTags will take care of that.
            double lastActive = System.Math.Max(nameDeactivate, tagDeactivate);
            if (lastActive > 0d)
            {
                if (reactivateCooldown == 0d || lastActive + reactivateCooldown > KSPUtils.GetUT())
                    return false;
            }

            return IsUnlocked();
        }
    }
}
