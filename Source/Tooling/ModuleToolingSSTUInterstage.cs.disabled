using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleToolingSSTUInterstage : ModuleToolingDiamLen
    {
        [KSPField]
        public string partModuleName = "SSTUInterstageDecoupler";

        public string bottomDiamField = "currentBottomDiameter";
        public string topDiamField = "currentTopDiameter";
        public string lenField = "currentHeight";

        protected PartModule pm;

        protected BaseField bottomDiam, topDiam, length;

        public override void OnAwake()
        {
            base.OnAwake();

            // Grab current link to module, *if* we've done a load already to get the field.
            if (!string.IsNullOrEmpty(partModuleName))
                pm = part.Modules[partModuleName];
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!string.IsNullOrEmpty(partModuleName))
                pm = part.Modules[partModuleName];
        }

        public override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (pm == null)
            {
                Debug.LogError("[ModuleTooling]: Could not find module " + partModuleName + " to bind to");
                return;
            }

            if (bottomDiam == null)
            {
                bottomDiam = pm.Fields[bottomDiamField];
                topDiam = pm.Fields[topDiamField];

                if (bottomDiam == null)
                {
                    Debug.LogError("[ModuleTooling]: Could not bind to field: " + bottomDiamField + " on " + partModuleName);
                    return;
                }
                if (topDiam == null)
                {
                    Debug.LogError("[ModuleTooling]: Could not bind to field: " + topDiamField + " on " + partModuleName);
                    return;
                }
            }

            if(length == null)
            {
                length = pm.Fields[lenField];

                if (length == null)
                {
                    Debug.LogError("[ModuleTooling]: Could not bind to field: " + lenField + " on " + partModuleName);
                    return;
                }
            }

            diam = Math.Max(bottomDiam.GetValue<float>(pm), topDiam.GetValue<float>(pm));
            len = length.GetValue<float>(pm);
        }
    }
}
