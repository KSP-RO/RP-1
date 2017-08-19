using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    public abstract class ModuleToolingDiamLen : ModuleTooling
    {
        protected abstract void GetDimensions(out float diam, out float len);

        public virtual string GetDimensions()
        {
            float d, l;
            GetDimensions(out d, out l);
            if (l != 0f)
                return d.ToString("F2") + "m x " + l.ToString("F2") + "m";
            else
                return d.ToString("F2") + "m";
        }

        public override float GetToolingCost()
        {
            float d, l;
            GetDimensions(out d, out l);
            float cost = lengthToolingCost.x * d * d + lengthToolingCost.y * d + lengthToolingCost.z * l + lengthToolingCost.w;
            if (ToolingDatabase.HasTooling(toolingType, d, l) == ToolingDatabase.ToolingLevel.None)
            {
                float mult = 1f;
                foreach (string s in reducers)
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
            ToolingDatabase.UnlockTooling(toolingType, d, l);
        }

        public override bool IsUnlocked()
        {
            float d, l;
            GetDimensions(out d, out l);
            if (d < minDiameter)
                return true;

            return ToolingDatabase.HasTooling(toolingType, d, l) == ToolingDatabase.ToolingLevel.Full;
        }
    }
}
