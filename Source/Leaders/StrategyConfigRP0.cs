using Strategies;
using UnityEngine;
using System.Collections.Generic;
using RP0.DataTypes;

namespace RP0
{
    public class StrategyConfigRP0 : StrategyConfig
    {
        public static Dictionary<string, double> ActivatedStrategies = new Dictionary<string, double>();

        [Persistent]
        protected string iconDepartment;
        public string IconDepartment => iconDepartment;

        protected Texture2D iconDepartmentImage;
        public Texture2D IconDepartmentImage => iconDepartmentImage;

        [Persistent]
        protected PersistentDictionaryValueTypes<CurrencyRP0, double> setupCosts = new PersistentDictionaryValueTypes<CurrencyRP0, double>();
        public PersistentDictionaryValueTypes<CurrencyRP0, double> SetupCosts => setupCosts;

        [Persistent]
        protected PersistentDictionaryValueTypes<CurrencyRP0, double> setupRequirements = new PersistentDictionaryValueTypes<CurrencyRP0, double>();
        public PersistentDictionaryValueTypes<CurrencyRP0, double> SetupRequirements => setupRequirements;

        //[Persistent]
        //protected PersistentDictionaryValueTypes<CurrencyRP0, double> endCosts = new PersistentDictionaryValueTypes<CurrencyRP0, double>();
        //public PersistentDictionaryValueTypes<CurrencyRP0, double> EndCosts => endCosts;

        [Persistent]
        protected PersistentListValueType<string> unlockByContractComplete = new PersistentListValueType<string>();
        public PersistentListValueType<string> UnlockByContractComplete => unlockByContractComplete;

        [Persistent]
        protected PersistentListValueType<string> unlockByProgramComplete = new PersistentListValueType<string>();
        public PersistentListValueType<string> UnlockByProgramComplete => unlockByProgramComplete;

        [Persistent]
        protected bool removeOnDeactivate;
        public bool RemoveOnDeactivate => removeOnDeactivate;

        [Persistent]
        protected string removeOnDeactivateTag;
        public string RemoveOnDeactivateTag => removeOnDeactivateTag;

        [Persistent]
        protected double reactivateCooldown;
        public double ReactivateCooldown => reactivateCooldown;

        [Persistent]
        protected double removalCostRepPercent;
        public double RemovalCostRepPercent => removalCostRepPercent;

        [Persistent]
        protected double removalCostLerpPower;
        public double RemovalCostLerpPower => removalCostLerpPower;

        // Will be called by transpiler of stock StrategyConfig.Create()
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
    }
}
