using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleToolingStockFairing : ModuleToolingDiamLen
    {
        protected ModuleProceduralFairing pm;

        public override void OnAwake()
        {
            base.OnAwake();
            pm = part.Modules.GetModule<ModuleProceduralFairing>();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            pm = part.Modules.GetModule<ModuleProceduralFairing>();
        }

        public override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (pm == null)
            {
                Debug.LogError("[ModuleTooling]: Could not find Stock Fairing module to bind to");
                return;
            }

            foreach (var x in pm.xSections)
            {
                if (diam < x.r)
                {
                    diam = x.r;
                }

                len += x.h;
            }

            diam *= 2f;
        }
    }
}
