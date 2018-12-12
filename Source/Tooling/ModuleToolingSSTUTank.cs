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

        public override void OnAwake()
        {
            base.OnAwake();
            // ******* 1.4+ SSTUTank = part.Modules["SSTUModularPart"];
            SSTUTank = part.Modules["SSTUModularFuelTank"];
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            // ******* 1.4+ SSTUTank = part.Modules["SSTUModularPart"];
            SSTUTank = part.Modules["SSTUModularFuelTank"];
        }
        protected override void GetDimensions(out float diam, out float len)
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
                if (coreStr.Contains("0-5")) { len = diam * 0.5f; }
                else if (coreStr.Contains("1-0")) { len = diam; }
                else if (coreStr.Contains("1-5")) { len = diam * 1.5f; }
                else if (coreStr.Contains("2-0")) { len = diam * 2.0f; }
                else if (coreStr.Contains("2-5")) { len = diam * 2.5f; }
                else if (coreStr.Contains("3-0")) { len = diam * 3.0f; }
                else if (coreStr.Contains("3-5")) { len = diam * 3.5f; }
                else if (coreStr.Contains("4-0")) { len = diam * 4.0f; }
                else if (coreStr.Contains("4-5")) { len = diam * 4.5f; }
                else if (coreStr.Contains("5-0")) { len = diam * 5.0f; }
                else if (coreStr.Contains("5-5")) { len = diam * 5.5f; }
                else if (coreStr.Contains("6-0")) { len = diam * 6.0f; }
                else if (coreStr.Contains("6-5")) { len = diam * 6.5f; }
                else if (coreStr.Contains("7-0")) { len = diam * 7.0f; }
                else if (coreStr.Contains("7-5")) { len = diam * 7.5f; }
                else if (coreStr.Contains("8-0")) { len = diam * 8.0f; }
                else ( len = diam * 0.25f; }
                Debug.Log($"[RP1-ModuleTooling]: SSTU Tank Size: Diameter = {diam}, Length = {len}");
            }
            else
            {
                Debug.LogError("[ModuleTooling]: Could not find SSTU MFT part to bind to");
                return;
            }
        }
    }
}
