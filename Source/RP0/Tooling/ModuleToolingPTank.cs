using RealFuels.Tanks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                    return rfType;
                }

                return base.ToolingType;
            }
        }

        public override float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
        {
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return 0f;
            if (onStartFinished) TryApplyToolingDefinition();
            return base.GetModuleCost(defaultCost, sit);
        }

        public override float GetToolingCost()
        {
            TryApplyToolingDefinition();
            return GetSpineToolingCost(out float spineCost) ? spineCost : base.GetToolingCost();
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

        public override void GetDimensions(out float diam, out float len)
        {
            // A Spine stands as its widest bounding cylinder wherever RP-1 wants one size (the untooled penalty, IsSame)
            if (GetSpineBounds(out _, out diam, out len))
                return;

            diam = 0f;
            len = 0f;
            object host = null;
            bool isCone = false;
            bool isHollow = false;
            bool isTruss = false;

            if (procTank == null)
            {
                RP0Debug.LogError($"[ModuleTooling]: Could not find ProceduralPart or ModuleROTank to bind to for {part}");
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
                    "Hollow Cylinder" => part.Modules["ProceduralShapeHollowCylinder"],
                    "Hollow Cone" => part.Modules["ProceduralShapeHollowCone"],
                    "Hollow Fillet Cylinder" => part.Modules["ProceduralShapeHollowPill"],
                    "Truss" => part.Modules["ProceduralShapeHollowTruss"],
                    _ => part.Modules["ProceduralShapeCylinder"]
                };
                isCone = shapeName.Contains("Cone");
                isHollow = shapeName.Contains("Hollow");
                isTruss = shapeName.Contains("Truss");

                if (procShape == null)
                {
                    RP0Debug.LogError("[ModuleTooling] Could not find proc SHAPE to bind to");
                    return;
                }
                length = GetRealLengthFieldForShape(procShape, isTruss);
                diam1 = GetPrimaryDiameterFieldForShape(procShape, isCone, isHollow, isTruss);
                diam2 = GetSecondaryDiameterFieldForShape(procShape, isCone, isHollow);
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
                RP0Debug.LogError($"[ModuleTooling] Could not bind to length or diamater fields for {host} on {part}");
                return;
            }
            diam = Mathf.Max(diam1.GetValue<float>(host), (isCone && procShape) ? diam2.GetValue<float>(host) : 0);
            len = length.GetValue<float>(host);
        }

        private BaseField GetRealLengthFieldForShape(PartModule shape, bool isTruss)
        => isTruss switch
        {
            (true) => shape.Fields["realLength"],
            (_) => shape.Fields["length"]
        };

        private static BaseField GetPrimaryDiameterFieldForShape(PartModule shape, bool isCone, bool isHollow, bool isTruss)
        => (isCone, isHollow, isTruss) switch
        {
            (_, _, true)      => shape.Fields["rodDiameter"],
            (false, false, _) => shape.Fields["diameter"],
            (false, true, _)  => shape.Fields["outerDiameter"],
            (true, false, _)  => shape.Fields["topDiameter"],
            (true, true, _)   => shape.Fields["topOuterDiameter"]
        };

        private static BaseField GetSecondaryDiameterFieldForShape(PartModule shape, bool isCone, bool isHollow)
        => (isCone, isHollow) switch
        {
            (false, _)    => null,
            (true, false) => shape.Fields["bottomDiameter"],
            (true, true)  => shape.Fields["bottomOuterDiameter"]
        };

        #region Spine (CustomProceduralParts)

        // CustomProceduralParts adds a Spine shape to every ProceduralParts part: lofted through sections that change
        // along its length. A Spine is tooled as two cylinders, its widest and its narrowest bounding cylinder, both at
        // the part's length, and is tooled when both are (one tooling when they're the same size). What it still needs
        // is its untooled cylinders: one already tooled (or under minDiameter) is neither charged nor bought again, nor
        // listed. Every other shape is tooled as above.

        // The two cylinders, each with whether it's tooled; shown only for a Spine, one line when they're the same size.
        [KSPField(guiActiveEditor = false, guiName = "Tooling max")]
        public string toolingMax = string.Empty;

        [KSPField(guiActiveEditor = false, guiName = "Tooling min")]
        public string toolingMin = string.Empty;

        private static readonly Dictionary<Type, PropertyInfo> currentShapeProperty = new Dictionary<Type, PropertyInfo>();
        private static readonly Dictionary<Type, PropertyInfo[]> boundsProperties = new Dictionary<Type, PropertyInfo[]>();

        public override void PurchaseTooling()
        {
            if (!GetSpineToolings(out float minD, out float maxD, out float len, out bool two))
            {
                base.PurchaseTooling();
                return;
            }

            Untooled(minD, maxD, len, two, out bool max, out bool min);
            if (max)
                ToolingDatabase.UnlockTooling(ToolingType, maxD, len);
            if (min)
                ToolingDatabase.UnlockTooling(ToolingType, minD, len);
        }

        public override bool IsUnlocked()
        {
            if (!GetSpineToolings(out float minD, out float maxD, out float len, out bool two))
                return base.IsUnlocked();

            Untooled(minD, maxD, len, two, out bool max, out bool min);
            return !max && !min;
        }

        public override string GetToolingParameterInfo()
        {
            if (!GetSpineToolings(out float minD, out float maxD, out float len, out bool two))
                return base.GetToolingParameterInfo();

            Untooled(minD, maxD, len, two, out bool max, out bool min);
            if (!max && !min)
                max = min = true;
            min &= two;
            return string.Join(" + ", new[] { min ? $"{minD:F3}m x {len:F3}m" : null, max ? $"{maxD:F3}m x {len:F3}m" : null }
                .Where(s => s != null));
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor && part.PartActionWindow != null)
                UpdatePawLines();
        }

        // The PAW lines, kept up to date while the part's PAW is open
        public void UpdatePawLines()
        {
            bool spine = GetSpineToolings(out float minD, out float maxD, out float len, out bool two);
            BaseField max = Fields[nameof(toolingMax)], min = Fields[nameof(toolingMin)];
            if (max.guiActiveEditor != spine || min.guiActiveEditor != (spine && two))
            {
                max.guiActiveEditor = spine;
                min.guiActiveEditor = spine && two;
                MonoUtilities.RefreshPartContextWindow(part);
            }
            if (!spine)
                return;

            max.guiName = two ? "Tooling max" : "Tooling";
            toolingMax = ToolingLine(maxD, len);
            toolingMin = two ? ToolingLine(minD, len) : string.Empty;
        }

        // The cost of a Spine's untooled cylinders, after TryApplyToolingDefinition; false for any other shape
        private bool GetSpineToolingCost(out float cost)
        {
            cost = 0f;
            if (!GetSpineToolings(out float minD, out float maxD, out float len, out bool two))
                return false;

            Untooled(minD, maxD, len, two, out bool max, out bool min);
            cost = ((max ? GetToolingCost(maxD, len) : 0f) + (min ? GetToolingCost(minD, len) : 0f)) * finalToolingCostMultiplier;
            return true;
        }

        private string ToolingLine(float d, float len) => $"{d:F3} x {len:F3} m, {(IsUnlocked(d, len) ? "tooled" : "not tooled")}";

        private void Untooled(float minD, float maxD, float len, bool two, out bool max, out bool min)
        {
            max = !IsUnlocked(maxD, len);
            min = two && !IsUnlocked(minD, len);
        }

        // The Spine's two toolings when it's the selected shape; two is false when they're the same size.
        private bool GetSpineToolings(out float minD, out float maxD, out float len, out bool two)
        {
            two = false;
            if (!GetSpineBounds(out minD, out maxD, out len))
                return false;

            two = !ToolingDatabase.IsSameSize(minD, len, maxD, len);
            return true;
        }

        /// <summary>
        /// The bounds of the part's shape if it's a Spine, or any ProceduralParts shape that reports them the same way:
        /// ProceduralPart.CurrentShape with a MinBoundingDiameter (the bounding diameter of its narrowest section) beside
        /// ProceduralAbstractShape's MaxDiameter and Length. Read by reflection: RP-1 doesn't reference ProceduralParts.
        /// </summary>
        private bool GetSpineBounds(out float minD, out float maxD, out float len)
        {
            minD = maxD = len = 0f;
            if (procTank == null || TTank != TankType.ProceduralPart)
                return false;

            Type ppType = procTank.GetType();
            if (!currentShapeProperty.TryGetValue(ppType, out PropertyInfo current))
            {
                current = ppType.GetProperty("CurrentShape", BindingFlags.Public | BindingFlags.Instance);
                currentShapeProperty[ppType] = current;
            }
            if (!(current?.GetValue(procTank) is object shape))
                return false;

            Type shapeType = shape.GetType();
            if (!boundsProperties.TryGetValue(shapeType, out PropertyInfo[] props))
            {
                props = new[] { "MinBoundingDiameter", "MaxDiameter", "Length" }
                    .Select(n => shapeType.GetProperty(n, BindingFlags.Public | BindingFlags.Instance))
                    .ToArray();
                if (props.Any(p => p == null || p.PropertyType != typeof(float)))
                    props = null;
                boundsProperties[shapeType] = props;
            }
            if (props == null)
                return false;

            minD = (float)props[0].GetValue(shape);
            maxD = (float)props[1].GetValue(shape);
            len = (float)props[2].GetValue(shape);
            return true;
        }

        #endregion
    }
}
