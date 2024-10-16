using ROUtils.DataTypes;
using RP0.Requirements;
using Strategies;
using System;
using System.Collections.Generic;
using UnityEngine;
using ROUtils;

namespace RP0
{
    public class StrategyConfigRP0 : StrategyConfig
    {
        [Persistent]
        protected bool isDisabled;
        public bool IsDisabled => isDisabled;

        [Persistent]
        protected bool cannotActivative;
        public bool CannotActivative => cannotActivative;

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
        /// The strategy only becomes available when conditions in the predicate are met.
        /// </summary>
        private Func<bool> requirementsPredicate;

        public RequirementBlock RequirementsBlock => requirementsBlock;
        private RequirementBlock requirementsBlock;

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

            ConfigNode cn = node.GetNode("REQUIREMENTS");
            if (cn != null)
            {
                try
                {
                    RequirementBlock reqBlock = RequirementBlock.Load(cn);
                    requirementsBlock = reqBlock;
                    requirementsPredicate = reqBlock?.Expression.Compile();
                }
                catch (Exception ex)
                {
                    RP0Debug.LogError($"Exception loading requirements for strategy {name}: {ex}");
                }
            }

            // For some reason members need to be set here, not in ctor.
            //removalCostRepPercent = 0.15d;


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
#if DEBUG
            return true;
#else
            return requirementsPredicate == null || requirementsPredicate();
#endif
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
            double nameDeactivate = removeOnDeactivate ? Programs.ProgramHandler.Instance.ActivatedStrategies.ValueOrDefault(Name) : 0d;
            double tagDeactivate = 0d;
            if (!string.IsNullOrEmpty(removeOnDeactivateTag))
                tagDeactivate = Programs.ProgramHandler.Instance.ActivatedStrategies.ValueOrDefault(removeOnDeactivateTag);

            // we are skipping the case where the strategy or its tag is active, but 
            // groupTags will take care of that.
            double lastActive = Math.Max(nameDeactivate, tagDeactivate);
            if (lastActive > 0d)
            {
                if (reactivateCooldown == 0d || lastActive + reactivateCooldown > Planetarium.GetUniversalTime())
                    return false;
            }

            return IsUnlocked();
        }
    }
}
