using System;
using RealFuels.Tanks;
using UnityEngine;

namespace RP0
{
    public class ModuleToolingPTank : ModuleToolingDiamLen
    {
        protected PartModule procTank, procShape;
        protected ModuleFuelTanks rfTank;

        protected BaseField diam1, diam2, length;
        protected enum TankType { ProceduralPart, ROTank, Unknown }
        protected TankType TTank => part.Modules.Contains("ProceduralPart") ? TankType.ProceduralPart :
                                    part.Modules.Contains("ModuleROTank") ? TankType.ROTank : TankType.Unknown;

        public override string ToolingType
        {
            get
            {
                // This PartModule is also used on structural bits which may not have a RF tank
                if (rfTank != null && rfTank.type is string rfType)
                {
                    if (rfType.EndsWith("-HP"))
                    {
                        // Highly Pressurised tanks currently share the tooling with regular tanks
                        rfType = rfType.Substring(0, rfType.Length - 3);
                    }

                    return rfType;
                }

                return base.ToolingType;
            }
        }

        public override float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (onStartFinished) UpdateToolingDefinition();
            return base.GetModuleCost(defaultCost, sit);
        }

        public override float GetToolingCost()
        {
            UpdateToolingDefinition();
            return base.GetToolingCost();
        }

        protected override void LoadPartModules()
        {
            procTank = TTank switch
            {
                TankType.ProceduralPart => part.Modules["ProceduralPart"],
                TankType.ROTank => part.Modules["ModuleROTank"],
                TankType.Unknown => null,
                _ => null
            };
            rfTank = part.Modules.GetModule<ModuleFuelTanks>();
        }

        private void UpdateToolingDefinition()
        {
            if (ToolingManager.Instance.GetToolingDefinition(ToolingType) is ToolingDefinition toolingDef)
            {
                if (toolingDef.untooledMultiplier != default)
                    untooledMultiplier = toolingDef.untooledMultiplier;

                if (toolingDef.finalToolingCostMultiplier != default)
                    finalToolingCostMultiplier = toolingDef.finalToolingCostMultiplier;

                if (toolingDef.costMultiplierDL != default)
                    costMultiplierDL = toolingDef.costMultiplierDL;

                if (!string.IsNullOrEmpty(toolingDef.toolingName))
                    toolingName = toolingDef.toolingName;

                if (!string.Equals(toolingDef.costReducers, costReducers))
                {
                    costReducers = toolingDef.costReducers;
                    reducerList = null;    // force base class to recalculate the list from string
                }
            }
        }

        public override void GetDimensions(out float diam, out float len)
        {
            diam = 0f;
            len = 0f;
            object host = null;
            bool cone = false;

            if (procTank == null)
            {
                Debug.LogError($"[ModuleTooling]: Could not find ProceduralPart or ModuleROTank to bind to for {part}");
                return;
            }
            if (TTank == TankType.ProceduralPart)
            {
                string shapeName = procTank.Fields["shapeName"].GetValue<string>(procTank);
                procShape = shapeName switch
                {
                    "Smooth Cone" => part.Modules["ProceduralShapeBezierCone"],
                    "Cone" => part.Modules["ProceduralShapeCone"],
                    "Fillet Cylinder" => part.Modules["ProceduralShapePill"],
                    "Polygon" => part.Modules["ProceduralShapePolygon"],
                    _ => part.Modules["ProceduralShapeCylinder"]
                };
                cone = shapeName.Contains("Cone");

                if (procShape == null)
                {
                    Debug.LogError("[ModuleTooling] Could not find proc SHAPE to bind to");
                    return;
                }
                length = procShape.Fields["length"];
                diam1 = cone ? procShape.Fields["topDiameter"] : procShape.Fields["diameter"];
                diam2 = cone ? procShape.Fields["bottomDiameter"] : null;
                host = procShape;
            } else if (TTank == TankType.ROTank)
            {
                length = procTank.Fields["totalTankLength"];
                diam1 = procTank.Fields["largestDiameter"];
                diam2 = null;
                host = procTank;
            }

            if (diam1 == null || length == null)
            {
                Debug.LogError($"[ModuleTooling] Could not bind to length or diamater fields for {host} on {part}");
                return;
            }
            diam = Mathf.Max(diam1.GetValue<float>(host), (cone && procShape) ? diam2.GetValue<float>(host) : 0);
            len = length.GetValue<float>(host);
        }
    }
}
