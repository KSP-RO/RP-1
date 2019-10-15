﻿using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

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

        private float GetCostReductionFactor(float d, float l)
        {
            foreach (string s in CostReducers)
            {
                if (ToolingDatabase.GetToolingLevel(s, d, l) > 0)
                {
                    return costReductionMult;
                }
            }

            return 1;
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
            if (!onStartFinished) return 0f;

            float baseCost = base.GetModuleCost(defaultCost, sit);
            GetDimensions(out var d, out var l);
            return baseCost + GetDimensionModuleCost(d, l, costMultiplierDL);
        }

        protected float GetDimensionModuleCost(float diameter, float length, float costMultiplierDL) => (diameter * length * costMultiplierDL);
    }
}
