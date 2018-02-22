﻿using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleToolingGeneric : ModuleToolingDiamLen
    {
        [KSPField]
        public string partModuleName = string.Empty;

        [KSPField]
        public string diamField = "diameter";

        [KSPField]
        public string lenField = "length";

        [KSPField]
        public bool useLength = true;

        protected PartModule pm;

        protected BaseField diameter, length;

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
        protected override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (pm == null)
            {
                Debug.LogError("[ModuleTooling]: Could not find module " + partModuleName + " to bind to");
                return;
            }

            if (diameter == null)
            {
                diameter = pm.Fields[diamField];

                if (diameter == null)
                {
                    Debug.LogError("[ModuleTooling]: Could not bind to field: " + diamField + " on " + partModuleName);
                    return;
                }
            }

            if(useLength && length == null)
            {
                length = pm.Fields[lenField];

                if (length == null)
                {
                    Debug.LogError("[ModuleTooling]: Could not bind to field: " + lenField + " on " + partModuleName);
                    return;
                }
            }

            diam = diameter.GetValue<float>(pm);

            if (useLength)
                len = length.GetValue<float>(pm);
        }
    }
}
