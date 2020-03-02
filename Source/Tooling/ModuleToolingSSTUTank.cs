using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    public class ModuleToolingSSTUTank : ModuleToolingDiamLen
    {
        protected PartModule SSTUTank;

        protected override void LoadPartModules()
        {
            base.LoadPartModules();
            SSTUTank = part.Modules["SSTUModularFuelTank"];
        }

        public override void GetDimensions(out float diameter, out float length)
        {
            diameter = 0f;
            length = 0f;

            if (SSTUTank == null)
            {
                Debug.LogError("[ModuleTooling] Could not find SSTU part to bind to");
                return;
            }

            // Get diameter from part field and convert to Float
            // ******* 1.4+ diam1 = SSTUTank.Fields["currentDiameter"];
            diameter = SSTUTank.Fields["currentTankDiameter"].GetValue<float>(SSTUTank);
            length = diameter * GetLengthMultiplier();
            // Debug.Log($"[ModuleTooling] SSTU Tank Size: Diameter = {diam}, Length = {len}");
        }

        private float GetLengthMultiplier()
        {
            // Get core size from part field and convert to String
            // ******* 1.4+ string coreStr = SSTUTank.Fields["currentCore"].GetValue<string>(SSTUTank);
            string coreStr = SSTUTank.Fields["currentTankType"].GetValue<string>(SSTUTank);
            // Debug.Log($"[ModuleTooling] SSTU Tank Core: {coreStr}");

            string noseType = SSTUTank.Fields["currentNoseType"].GetValue<string>(SSTUTank);
            string mountType = SSTUTank.Fields["currentMountType"].GetValue<string>(SSTUTank);

            var baseMultiplier = GetBaseLengthMultiplier(coreStr);
            var noseMultiplier = GetNoseMountLengthMultiplier(noseType);
            var mountMultiplier = GetNoseMountLengthMultiplier(mountType);

            return baseMultiplier + noseMultiplier + mountMultiplier;
        }

        private float GetBaseLengthMultiplier(string coreStr)
        {
            // SSTU Modular Tanks are actually separate models and these are the 4 common models that are simple cylinders.
            if (coreStr.Contains("MFT-A-") || coreStr.Contains("MFT-B-") || coreStr.Contains("MFT-C-") || coreStr.Contains("MFT-CF-"))
            {
                return GetStandardTankBaseLengthMultiplier(coreStr);
            }
            if (coreStr.Contains("MFT-D-"))
            {
                return GetBoosterBaseLengthMultiplier(coreStr);
            }

            Debug.LogError("[ModuleTooling] Unknown Tank: " + coreStr);
            return 0;
        }

        private float GetBoosterBaseLengthMultiplier(string coreStr)
        {
            return _boosterLengthDict[coreStr];
        }

        private float GetStandardTankBaseLengthMultiplier(string coreStr)
        {
            /* We first look for the beginning 7 characters of the string
             * and once we find those, we look for the remaining 3 characters. These characters
             * represent the different models and are multipliers on the Diameter for the Length of the tank. */
            if (coreStr.Contains("0-3")) { return 0.25f; }
            else if (coreStr.Contains("0-7")) { return 0.75f; }

            var scale = SSTUTank.Fields["currentTankVerticalScale"].GetValue<float>(SSTUTank);
            return ParseLengthMultiplier(coreStr) * scale;
        }

        private static float ParseLengthMultiplier(string coreStr)
        {
            string coreSwap = coreStr.Replace('-', '.');
            string coreMult = coreSwap.Substring(coreSwap.Length - 3);
            return float.Parse(coreMult);
        }

        private static float GetNoseMountLengthMultiplier(string noseType)
        {
            if (!noseMountDict.ContainsKey(noseType))
            {
                return 0;
            }
            return noseMountDict[noseType];
        }

        private static Dictionary<string, float> _boosterLengthDict = new Dictionary<string, float>()
        {
            {"MFT-D-1-0", 4.7f},
            {"MFT-D-2-0", 5.5f},
            {"MFT-D-3-0", 6.25f},
            {"MFT-D-4-0", 7f}
        };

        // Nose Type = currentNoseType      Mount = currentMountType
        private static Dictionary<string, float> noseMountDict = new Dictionary<string, float>()
        {
            {"Adapter-2-1-Flat", 0.09f},
            {"Adapter-2-1-Short", 0.5f},
            {"Adapter-2-1-Long", 1.0f},
            {"Adapter-4-3-Flat", 0.09f},
            {"Adapter-4-3-Long", 1.0f},
            {"Adapter-4-3-Short", 0.5f},
            {"Adapter-3-1-Flat", 0.12f},
            {"Adapter-3-1-Short", 0.5f},
            {"Adapter-3-1-Long", 1.0f},
            {"Adapter-3-2-Flat", 0.12f},
            {"Adapter-3-2-Short", 0.5f},
            {"Adapter-3-2-Long", 1.0f},
            {"Adapter-1-2-Flat", 0.18f},
            {"Adapter-1-2-Short", 1.0f},
            {"Adapter-1-2-Long", 2.0f},
            {"Adapter-3-4-Flat", 0.12f},
            {"Adapter-3-4-Long", 1.333f},
            {"Adapter-3-4-Short", 0.667f},
            {"Adapter-1-3-Flat", 0.36f},
            {"Adapter-1-3-Short", 1.5f},
            {"Adapter-1-3-Long", 3.0f},
            {"Adapter-2-3-Flat", 0.18f},
            {"Adapter-2-3-Short", 0.75f},
            {"Adapter-2-3-Long", 1.5f},
            {"Adapter-1-1-VA", 0.081f},
            {"Adapter-4-1-Flat", 0.079f},
            {"Adapter-4-1-Short", 0.386f},
            {"Adapter-2-1-Dome", 0.12f},
            {"Adapter-Soyuz", 5.695f},
            {"Adapter-Soyuz2", 5.0f},
            {"Adapter-Soyuz3", 4.304f},
            {"Adapter-Soyuz4", 3.608f},
            {"MFT-SF-ADPT-N", 0.48f},
            {"MFT-SF-ADPT-S", 0.5f},
            {"MFT-SF-ADPT-M", 0.5f},
            {"MFT-S-ADPT-N", 0.5f},
            {"MFT-S-ADPT-S", 0.5f},
            {"MFT-S-ADPT-M", 0.5f},
            {"Nosecone-1", 1.261f},
            {"Nosecone-2", 1.356f},
            {"Nosecone-3", 0.64f},
            {"Nosecone-4", 0.391f},
            {"Nosecone-5", 1.215f},
            {"SRB-Nosecone-1", 1.0f},
            {"SRB-Nosecone-2", 1.167f},
            {"SRB-Nosecone-3", 1.346f},
            {"SRB-Nosecone-4", 1.465f},
            {"SRB-Nosecone-5", 1.526f},
            {"SRB-Nosecone-6", 1.534f},
            {"Mount-SLS", 0.2f},
            {"Mount-SLS-6", 0.2f},
            {"Mount-Saturn-V", 0.448f},
            {"Mount-Pyrios", 0.267f},
            {"Mount-Nova", 0.21f},
            {"Mount-Direct", 0.5f},
            {"Mount-S-II", 0.35f},
            {"Mount-S-IVB", 0.436f},
            {"Mount-Generic", 0.18f},
            {"Mount-Skeletal-L", 0.348f},
            {"Mount-Skeletal-M", 0.348f},
            {"Mount-Skeletal-S", 0.328f},
            {"Mount-Delta-IV", 0.267f},
            {"Mount-Shroud", 0.2f},
            {"Mount-Shroud2", 0.267f},
            {"Mount-RD-107", 0.027f},
            {"Mount-RD-108", 0.035f},
            {"Mount-Shroud3", 0.4f},
            {"Mount-Shroud4", 0.6f},
            {"Mount-Shroud5", 0.8f},
            {"Mount-Shroud6", 1f},
        };
    }
}
