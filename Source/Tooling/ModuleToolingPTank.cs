using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleToolingPTank : ModuleToolingDiamLen
    {
        protected PartModule procTank, procShape;
        protected string shapeName = string.Empty;
        protected bool cone = false;

        protected BaseField diam1, diam2, length;

        public override void OnAwake()
        {
            base.OnAwake();

            procTank = part.Modules["ProceduralPart"];
        }
        protected override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (procTank == null)
            {
                Debug.LogError("[ModuleTooling]: Could not find proc part to bind to");
                return;
            }

            string newName = procTank.Fields["shapeName"].GetValue<string>(procTank);
            if (newName != shapeName || procShape == null)
            {
                shapeName = newName;
                switch (shapeName)
                {
                    case "Smooth Cone":
                        procShape = part.Modules["ProceduralShapeBezierCone"];
                        cone = true;
                        break;
                    case "Cone":
                        procShape = part.Modules["ProceduralShapeCone"];
                        cone = true;
                        break;
                    case "Fillet Cylinder":
                        procShape = part.Modules["ProceduralShapePill"];
                        cone = false;
                        break;

                    default: // "Cylinder"
                        procShape = part.Modules["ProceduralShapeCylinder"];
                        cone = false;
                        break;
                }

                if (procShape == null)
                {
                    Debug.LogError("[ModuleTooling]: Could not find proc SHAPE to bind to");
                    return;
                }

                if (cone)
                {
                    diam1 = procShape.Fields["topDiameter"];
                    diam2 = procShape.Fields["bottomDiameter"];
                }
                else
                {
                    diam1 = procShape.Fields["diameter"];
                    diam2 = null;
                }

                length = procShape.Fields["length"];
            }
            else if (procShape == null)
            {
                Debug.LogError("[ModuleTooling]: Lost proc SHAPE to bind to");
                return;
            }

            if (diam1 == null || length == null)
            {
                Debug.LogError("[ModuleTooling]: Could not bind to procpart fields");
                return;
            }

            if (cone)
                diam = Math.Max(diam1.GetValue<float>(procShape), diam2.GetValue<float>(procShape));
            else
                diam = diam1.GetValue<float>(procShape);

            len = length.GetValue<float>(procShape);
        }
    }
}
