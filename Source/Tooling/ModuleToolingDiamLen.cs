using System;
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

        public virtual string GetDimensions()
        {
            float d, l;
            GetDimensions(out d, out l);
            if (l != 0f)
                return d.ToString("F3") + "m x " + l.ToString("F3") + "m";
            else
                return d.ToString("F3") + "m";
        }

        public override float GetToolingCost()
        {
            float d, l;
            GetDimensions(out d, out l);
            float cost = lengthToolingCost.x * d * d + lengthToolingCost.y * d + lengthToolingCost.z * l + lengthToolingCost.w;
            if (ToolingDatabase.HasTooling(ToolingType, d, l) == ToolingDatabase.ToolingLevel.None)
            {
                float mult = 1f;
                foreach (string s in CostReducers)
                {
                    if (ToolingDatabase.HasTooling(s, d, l) > ToolingDatabase.ToolingLevel.None)
                    {
                        mult = costReductionMult;
                        break;
                    }
                }
                cost += mult * (diameterToolingCost.x * d * d + diameterToolingCost.y * d + diameterToolingCost.z);
            }

            return cost * finalToolingCostMultiplier;
        }

        public override void PurchaseTooling()
        {
            float d, l;
            GetDimensions(out d, out l);
            ToolingDatabase.UnlockTooling(ToolingType, d, l);
        }

        public override bool IsUnlocked()
        {
            float d, l;
            GetDimensions(out d, out l);
            if (d < minDiameter)
                return true;

            return ToolingDatabase.HasTooling(ToolingType, d, l) == ToolingDatabase.ToolingLevel.Full;
        }

        public override float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (!onStartFinished) return 0f;

            float baseCost = base.GetModuleCost(defaultCost, sit);
            float d, l;
            GetDimensions(out d, out l);
            return baseCost + (d * l * costMultiplierDL);
        }
    }
}
