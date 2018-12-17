using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleToolingSSTUTank : ModuleToolingDiamLen
    {
        protected PartModule SSTUTank;

        protected BaseField diam1;
        protected BaseField scale;

        public override void OnAwake()
        {
            base.OnAwake();
            // ******* 1.4+ SSTUTank = part.Modules["SSTUModularPart"];
            SSTUTank = part.Modules["SSTUModularFuelTank"];
        }
        /* Removing as I believe it is causing a lot of slowdowns and log spam
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            // ******* 1.4+ SSTUTank = part.Modules["SSTUModularPart"];
            SSTUTank = part.Modules["SSTUModularFuelTank"];
        }
        */

        public override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (SSTUTank == null)
            {
                Debug.LogError("[ModuleTooling]: Could not find SSTU part to bind to");
                return;
            }

            // Get diameter from part field and convert to Float
            // ******* 1.4+ diam1 = SSTUTank.Fields["currentDiameter"];
            diam1 = SSTUTank.Fields["currentTankDiameter"];
            diam = diam1.GetValue<float>(SSTUTank);

            // Get core size from part field and convert to String
            // ******* 1.4+ string coreStr = SSTUTank.Fields["currentCore"].GetValue<string>(SSTUTank);
            string coreStr = SSTUTank.Fields["currentTankType"].GetValue<string>(SSTUTank);
            Debug.Log($"[RP1-ModuleTooling]: SSTU Tank Core: {coreStr}");

            /* SSTU Modular Tanks are actually separate models and these are the 4 common models that
             * are simple cylinders. We first look for the beginning 7 characters of the string
             * and once we find those, we look for the remaining 3 characters. These characters
             * represent the different models and are multipliers on the Diameter for the Length of the tank. */
            if (coreStr.Contains("MFT-A-") || coreStr.Contains("MFT-B-") || coreStr.Contains("MFT-C-") || coreStr.Contains("MFT-CF-"))
            {
                if (coreStr.Contains("0-3")) { len = diam * 0.25f; }
                else if (coreStr.Contains("0-7")) { len = diam * 0.75f; }
                else
                {
                    string coreSwap = coreStr.Replace('-', '.');
                    string coreMult = coreSwap.Substring(coreSwap.Length - 3);                    
                    float lenMult = float.Parse(coreMult);
                    float curLen = lenMult * diam;
                    scale = SSTUTank.Fields["currentTankVerticalScale"];
                    float vScale = scale.GetValue<float>(SSTUTank);
                    len = curLen * vScale;
                }
                // Debug.Log($"[RP1-ModuleTooling]: SSTU Tank Size: Diameter = {diam}, Length = {len}");
            }
            else
            {
                Debug.LogError("[ModuleTooling]: Could not find SSTU MFT part to bind to");
                return;
            }
        }
    }
}
