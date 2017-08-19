using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleToolingPFSide : ModuleToolingDiamLen
    {
        protected PartModule pm;

        protected BaseField baseRad, maxRad, cylEnd, sideThickness, inlineHeight, noseHeightRatio;

        public override void OnAwake()
        {
            base.OnAwake();
            pm = part.Modules["ProceduralFairingSide"];
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            pm = part.Modules["ProceduralFairingSide"];
        }
        protected override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (pm == null)
            {
                Debug.LogError("[ModuleTooling]: Could not find PF module to bind to");
                return;
            }

            if (baseRad == null)
            {
                baseRad = pm.Fields["baseRad"];
                maxRad = pm.Fields["maxRad"];
                cylEnd = pm.Fields["cylEnd"];
                sideThickness = pm.Fields["sideThickness"];
                inlineHeight = pm.Fields["inlineHeight"];
                noseHeightRatio = pm.Fields["noseHeightRatio"];


                if (baseRad == null)
                {
                    Debug.LogError("[ModuleTooling]: Could not bind to fields in PF module");
                    return;
                }
            }
            float baseRadF, maxRadF, cylEndF, sideThicknessF, inlineHeightF, noseHeightRatioF;
            baseRadF = baseRad.GetValue<float>(pm);
            maxRadF = maxRad.GetValue<float>(pm);
            cylEndF = cylEnd.GetValue<float>(pm);
            sideThicknessF = sideThickness.GetValue<float>(pm);
            inlineHeightF = inlineHeight.GetValue<float>(pm);
            noseHeightRatioF = noseHeightRatio.GetValue<float>(pm);

            diam = (Math.Max(baseRadF, maxRadF) + sideThicknessF) * 2f;
            if (inlineHeightF > 0f)
                len = inlineHeightF;
            else
                len = noseHeightRatioF * diam * 0.5f + cylEndF;
        }
    }
}
