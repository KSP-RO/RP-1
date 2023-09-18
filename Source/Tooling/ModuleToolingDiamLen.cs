using System;
using System.Collections.Generic;

namespace RP0
{
    public class ModuleToolingDiamLen : ModuleTooling
    {
        [KSPField]
        public float partDiameter;

        [KSPField]
        public float partLength;

        /// <summary>
        /// Used for increasing costs a bit based on diameter and length (thereby discounting bigger-diameter parts).
        /// </summary>
        [KSPField]
        public float costMultiplierDL = 0f;

        public virtual void GetDimensions(out float diam, out float len)
        {
            diam = partDiameter;
            len = partLength;
        }

        public override string GetToolingParameterInfo()
        {
            GetDimensions(out var d, out var l);
            if (l != 0f)
                return d.ToString("F3") + "m x " + l.ToString("F3") + "m";
            else
                return d.ToString("F3") + "m";
        }

        public override float GetToolingCost()
        {
            GetDimensions(out var d, out var l);
            float cost = GetLengthToolingCost(d, l);
            if (ToolingDatabase.GetToolingLevel(ToolingType, d, l) == 0)
            {
                var reductionFactor = GetCostReductionFactor(d, l);
                cost += reductionFactor * GetDiameterToolingCost(d);
            }

            return cost * finalToolingCostMultiplier;
        }

        protected override void ApplyToolingDefinition(ToolingDefinition toolingDef)
        {
            base.ApplyToolingDefinition(toolingDef);

            if (toolingDef.costMultiplierDL != default)
                costMultiplierDL = toolingDef.costMultiplierDL;
        }

        private float GetCostReductionFactor(float d, float l)
        {
            float factor = 1;
            foreach (KeyValuePair<string, float> reducer in CostReducers)
            {
                if (ToolingDatabase.GetToolingLevel(reducer.Key, d, l) > 0)
                {
                    factor = Math.Min(reducer.Value, factor);
                }
            }

            return factor;
        }

        protected virtual float GetDiameterToolingCost(float diameter) => diameterToolingCost.x * diameter * diameter + diameterToolingCost.y * diameter + diameterToolingCost.z;
        protected virtual float GetLengthToolingCost(float diameter, float length) => lengthToolingCost.x * diameter * diameter + lengthToolingCost.y * diameter + lengthToolingCost.z * length + lengthToolingCost.w;

        public override void PurchaseTooling()
        {
            GetDimensions(out var d, out var l);
            ToolingDatabase.UnlockTooling(ToolingType, d, l);
        }

        public override bool IsUnlocked()
        {
            GetDimensions(out var d, out var l);
            if (d < minDiameter)
                return true;

            return ToolingDatabase.GetToolingLevel(ToolingType, d, l) == 2;
        }

        public override float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (!onStartFinished || HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return 0f;

            float baseCost = base.GetModuleCost(defaultCost, sit);
            GetDimensions(out var d, out var l);
            return baseCost + GetDimensionModuleCost(d, l, costMultiplierDL);
        }

        protected float GetDimensionModuleCost(float diameter, float length, float costMultiplierDL) => (diameter * length * costMultiplierDL);
    }
}
