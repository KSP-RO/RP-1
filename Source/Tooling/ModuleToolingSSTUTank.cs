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
            // Debug.Log($"[RP1-ModuleTooling]: SSTU Tank Core: {coreStr}");

            string noseType = SSTUTank.Fields["currentNoseType"].GetValue<string>(SSTUTank);
            string mountType = SSTUTank.Fields["currentMountType"].GetValue<string>(SSTUTank);

            float baseLen=0f, noseLen=0f, mountLen=0f;

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
                    baseLen = curLen * vScale;
                }
                // Debug.Log($"[RP1-ModuleTooling]: SSTU Tank Size: Diameter = {diam}, Length = {len}");

                // Add the size of the mount and the size of the nosecone to the overall length
                if (noseMountDict.ContainsKey(noseType) || noseMountDict.ContainsKey(mountType))
                {
                    if (noseMountDict.ContainsKey(noseType))
                    {
                        float mult = noseMountDict[noseType];
                        noseLen = diam * mult;
                    }

                    if (noseMountDict.ContainsKey(mountType))
                    {
                        float mult = noseMountDict[mountType];
                        mountLen = diam * mult;
                    }
                }
                else
                {
                    noseLen = 0;
                    mountLen = 0;
                }

                len = baseLen + noseLen + mountLen;

            }
            else
            {
                Debug.LogError("[ModuleTooling]: Could not find SSTU MFT part to bind to");
                return;
            }
        }

        // Nose Type = currentNoseType      Mount = currentMountType
        public Dictionary<string, float> noseMountDict = new Dictionary<string, float>()
        {
            {"Adapter-1-2-Flat", 0.09f},
            {"Adapter-1-2-Short", 0.5f },
            {"Adapter-1-2-Long", 1.0f },
            {"Adapter-3-4-Flat", 0.09f },
            {"Adapter-3-4-Long", 1.0f },
            {"Adapter-3-4-Short", 0.5f },
            {"Adapter-1-3-Flat", 0.09f},
            {"Adapter-1-3-Short", 0.375f },
            {"Adapter-1-3-Long", 0.75f },
            {"Adapter-2-3-Flat", 0.09f},
            {"Adapter-2-3-Short", 0.375f },
            {"Adapter-2-3-Long", 0.75f },
            {"Adapter-2-1-Flat", 0.09f },
            {"Adapter-2-1-Short", 0.5f },
            {"Adapter-2-1-Long", 1.0f },
            {"Adapter-4-3-Flat", 0.09f },
            {"Adapter-4-3-Long", 1.0f },
            {"Adapter-4-3-Short", 0.5f },
            {"Adapter-3-1-Flat", 0.09f},
            {"Adapter-3-1-Short", 0.375f },
            {"Adapter-3-1-Long", 0.75f },
            // {"Adapter-3-1-Extended", 0.590f },  1.4.x
            {"Adapter-3-2-Flat", 0.09f},
            {"Adapter-3-2-Short", 0.375f },
            {"Adapter-3-2-Long", 0.75f },
            // {"Adapter-3-2-Extended", 0.590f },  1.4.x
            // {"Adapter-4-1-Flat", 0.0393f },  1.4.x
            // {"Adapter-4-1-Short", 0.193f },  1.4.x
            {"Nosecone-1", 1.261f },
            {"Nosecone-2", 1.356f },
            {"Nosecone-3", 0.640f },
            {"Nosecone-4", 0.391f },
            {"Nosecone-5", 1.215f },
            {"SRB-Nosecone-1", 1.0f },
            {"SRB-Nosecone-2", 1.167f },
            {"SRB-Nosecone-3", 1.346f },
            {"SRB-Nosecone-4", 1.465f },
            {"SRB-Nosecone-5", 1.526f },
            {"SRB-Nosecone-6", 1.534f },
            {"Mount-SLS", 0.2f },
            {"Mount-SLS-6", 0.2f },
            {"Mount-Saturn-V", 0.448f },
            {"Mount-Pyrios", 0.267f },
            {"Mount-Nova", 0.21f },
            {"Mount-Direct", 0.5f },
            {"Mount-S-II", 0.35f },
            {"Mount-S-IVB", 0.436f },
            {"Mount-Generic", 0.18f },
            {"Mount-Skeletal-L", 0.348f },
            {"Mount-Skeletal-M", 0.348f },
            {"Mount-Skeletal-S", 0.328f },
            {"Mount-Delta-IV", 0.267f },
            {"Mount-Shroud", 0.2f },
            {"Mount-Shroud2", 0.267f },
            {"Mount-RD-107", 0.027f },
            {"Mount-RD-108", 0.035f },
            {"Mount-Shroud3", 0.1f },
            {"Mount-Shroud4", 0.1f },
            {"Mount-Shroud5", 0.1f },
            {"Mount-Shroud6", 0.1f },
            // {"Adapter-2-1-Dome", 0.12f },   1.4.x
            // {"Adapter-Dome-A", 1f },        1.4.x
            // {"Adapter-Dome-B", 1f },        1.4.x
            {"Adapter-Soyuz", 5.695f },
            {"Adapter-Soyuz2", 5f },
            {"Adapter-Soyuz3", 4.304f },
            {"Adapter-Soyuz4", 3.608f }
        };


    }
}
