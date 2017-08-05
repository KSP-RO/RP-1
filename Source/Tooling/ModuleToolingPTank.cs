using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleToolingPTank : ModuleTooling
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
        protected void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;

            if (procTank == null)
                return;

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
                        break;

                    default: // "Cylinder"
                        procShape = part.Modules["ProceduralShapeCylinder"];
                        break;
                }

                if (procShape == null)
                    return;

                if (cone)
                {
                    diam1 = procShape.Fields["topDiameter"];
                    diam2 = procShape.Fields["bottomDiameter"];
                }
                else
                {
                    diam1 = procShape.Fields["Diameter"];
                    diam2 = null;
                }

                length = procShape.Fields["length"];
            }
            else if (procShape == null)
                return;

            if (cone)
                diam = Math.Max(diam1.GetValue<float>(procShape), diam2.GetValue<float>(procShape));
            else
                diam = diam1.GetValue<float>(procShape);

            len = length.GetValue<float>(procShape);
        }

        public override float GetToolingCost()
        {
            float d, l;
            GetDimensions(out d, out l);
            float cost = lengthToolingCost.x * d * d + lengthToolingCost.y * d + lengthToolingCost.z * l + lengthToolingCost.w;
            if (ToolingDatabase.HasTooling(toolingType, d, l) == ToolingDatabase.ToolingLevel.None)
                cost += diameterToolingCost.x * d * d + diameterToolingCost.y * d + diameterToolingCost.z;

            return cost;
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
            return ToolingDatabase.HasTooling(toolingType, d, l) == ToolingDatabase.ToolingLevel.Full;
        }
    }
}
