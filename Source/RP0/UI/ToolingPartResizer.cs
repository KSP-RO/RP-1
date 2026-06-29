using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using RealFuels.Tanks;

namespace RP0
{
    // "Resize" button on each tooled (diameter, length) row in ToolingGUI.DisplayRow: writes the
    // dimensions to whatever part has its PAW open right now, *if* that part's RF tank type is one
    // of the materials this row is tooled for. We don't try to spawn a new part — pre-RP-1 changes,
    // ModuleToolingPTank emits the same ToolingType regardless of part class, so the DB has no way
    // to tell us which part class an entry belongs to. The user picks the part in the editor; this
    // tool just snaps its dims to a tooled value.
    internal static class ToolingPartResizer
    {
        /// <summary>
        /// Resolves the part whose PAW is currently visible. Returns null when nothing's open, or
        /// when multiple PAWs are up (ambiguous which one the player means).
        /// </summary>
        public static Part PawTarget()
        {
            List<UIPartActionWindow> windows = UIPartActionController.Instance?.windows;
            if (windows == null) return null;
            Part candidate = null;
            for (int i = 0; i < windows.Count; i++)
            {
                UIPartActionWindow w = windows[i];
                if (w == null || w.part == null) continue;
                if (candidate != null) return null;        // 2+ PAWs -> ambiguous
                candidate = w.part;
            }
            return candidate;
        }

        /// <summary>Returns the part's current RF tank type if it has a ModuleFuelTanks, else null.</summary>
        public static string CurrentTankType(Part p)
        {
            if (p == null) return null;
            return p.Modules.GetModule<ModuleFuelTanks>()?.type;
        }

        /// <summary>
        /// Picks an RF tank type to apply when refitting, or null when none of the row's materials
        /// can be applied to this part. Prefers the part's current type if it's already one of the
        /// row's materials (no switch needed); otherwise the first material the part can switch to
        /// right now -- one it lists in typesAvailable whose RF type-upgrade is either absent or
        /// already purchased. Never returns a tech-locked material (one whose upgrade isn't bought):
        /// that mirrors ModuleFuelTanks.Validate and avoids leaving the tank in a locked state. The
        /// caller disables Refit and explains why when this is null.
        /// </summary>
        public static string PickRfType(Part p, IEnumerable<string> sources)
        {
            if (p == null || sources == null) return null;
            ModuleFuelTanks rf = p.Modules.GetModule<ModuleFuelTanks>();
            if (rf == null) return null;
            IList<string> srcList = sources as IList<string> ?? sources.ToList();
            if (srcList.Count == 0) return null;
            if (rf.type != null)
            {
                foreach (string s in srcList) if (string.Equals(s, rf.type, StringComparison.Ordinal)) return rf.type;
            }
            // typesAvailable is a List<TankDefinition>; it can include locked types (preview mode),
            // so test each candidate's upgrade state rather than trusting membership alone.
            List<TankDefinition> available = rf.typesAvailable;
            if (available == null) return null;
            foreach (string s in srcList)
            {
                TankDefinition def = available.FirstOrDefault(t => string.Equals(t.name, s, StringComparison.Ordinal));
                if (def != null && IsTypeUnlocked(rf, def.name)) return s;
            }
            return null;
        }

        // True when the part can switch to this tank type without buying anything: either the type
        // carries no RF type-upgrade, or that upgrade is already purchased.
        private static bool IsTypeUnlocked(ModuleFuelTanks rf, string typeName)
        {
            PartUpgradeHandler.Upgrade up = ModuleFuelTanks.GetUpgradeForType(rf, typeName);
            return up == null || PartUpgradeManager.Handler.IsUnlocked(up.name);
        }

        /// <summary>
        /// Writes (diameter, length) to the part and all its symmetry counterparts, optionally
        /// switching their RF tank type first. Dimension changes go through the procedural module's
        /// own field-changed handler -- the same path the PAW sliders use -- which rebuilds geometry,
        /// repositions attach nodes and attached parts, and already walks the symmetry group itself,
        /// so it's fired once on the master. Posts a single screen message; failures are logged and
        /// surfaced, not thrown. If the shape can't be refit (e.g. a truss), the reason is posted and
        /// no success message is shown.
        /// </summary>
        public static void Resize(Part p, float diameter, float length, string targetRfType = null)
        {
            if (p == null) { Msg("No part PAW open."); return; }
            try
            {
                // RF tank type isn't a slider-driven dimension and our reflection write doesn't mirror
                // across symmetry, so switch it on the master and every counterpart explicitly.
                bool typeChanged = ApplyRfTypeToGroup(p, targetRfType);

                if (!ApplyGeometry(p, diameter, length))
                    return;   // shape couldn't be refit -- ApplyGeometry already said why

                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch?.ship);

                // The open PAW caches its slider text; our programmatic field writes don't invalidate
                // it, so the displayed value goes stale even though the underlying value is correct
                // (increment/decrement operate on the new value). Force the part's PAW to rebuild its
                // field displays, the same way ProceduralParts does after a shape change.
                MonoUtilities.RefreshPartContextWindow(p);

                int count = 1 + CountCounterparts(p);
                string scope = count > 1 ? $" (x{count})" : "";
                Msg(typeChanged
                    ? $"Refit {p.partInfo?.title} to {targetRfType} at d={diameter:F3}m, L={length:F3}m{scope}"
                    : $"Resized {p.partInfo?.title} to d={diameter:F3}m, L={length:F3}m{scope}");
            }
            catch (Exception ex) { Debug.LogError("[RP0 Tooling] resize failed: " + ex); Msg("Resize failed: " + ex.Message); }
        }

        private static int CountCounterparts(Part p)
        {
            List<Part> cp = p.symmetryCounterparts;
            if (cp == null) return 0;
            int n = 0;
            for (int i = 0; i < cp.Count; i++) if (cp[i] != null && cp[i] != p) n++;
            return n;
        }

        /// <summary>
        /// Switches the RF tank type on the part and each symmetry counterpart. Returns whether the
        /// master part's type actually changed (drives the success-message wording).
        /// </summary>
        private static bool ApplyRfTypeToGroup(Part p, string targetRfType)
        {
            if (string.IsNullOrEmpty(targetRfType)) return false;
            bool masterChanged = ApplyRfTypeToPart(p, targetRfType);
            List<Part> counterparts = p.symmetryCounterparts;
            if (counterparts != null)
                for (int i = 0; i < counterparts.Count; i++)
                {
                    Part c = counterparts[i];
                    if (c != null && c != p) ApplyRfTypeToPart(c, targetRfType);
                }
            return masterChanged;
        }

        private static bool ApplyRfTypeToPart(Part p, string targetRfType)
        {
            ModuleFuelTanks rf = p.Modules.GetModule<ModuleFuelTanks>();
            if (rf == null || string.Equals(rf.type, targetRfType, StringComparison.Ordinal)) return false;
            ApplyRfTankType(rf, targetRfType);
            return true;
        }

        /// <summary>
        /// Dispatches the dimension write to whichever procedural module the part carries. Returns
        /// false when the part has no ROTank/ProceduralPart to drive or the shape isn't refittable.
        /// </summary>
        private static bool ApplyGeometry(Part p, float diameter, float length)
        {
            PartModule roTank = p.Modules.GetModule("ModuleROTank");
            if (roTank != null) { ApplyRoTank(p, roTank, diameter, length); return true; }
            PartModule procPart = p.Modules.GetModule("ProceduralPart");
            if (procPart != null) return ApplyProcShape(p, procPart, diameter, length);
            return false;
        }

        /// <summary>Sets the RF tank type field (via reflection) and nudges RF to re-resolve it.</summary>
        private static void ApplyRfTankType(ModuleFuelTanks rfTank, string rfType)
        {
            FieldInfo typeField = typeof(ModuleFuelTanks).GetField("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (typeField != null) typeField.SetValue(rfTank, rfType);
            else
            {
                PropertyInfo typeProp = typeof(ModuleFuelTanks).GetProperty("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                typeProp?.SetValue(rfTank, rfType, null);
            }
            if (!TryInvoke(rfTank, "UpdateTankType", new object[] { false }) &&
                !TryInvoke(rfTank, "UpdateTankType", null))
            {
                TryInvoke(rfTank, "ChangeTankType", new object[] { rfType });
            }
        }

        /// <summary>
        /// ROTanks expose currentDiameter for the cross-section, and drive length two different ways:
        /// in lengthWidth mode the editable knob is currentLength (currentVScale is hidden), otherwise
        /// it's currentVScale. totalTankLength is a computed output. We set the diameter first (so the
        /// tank rebuilds and totalTankLength reflects the new diameter), then drive whichever length
        /// control is active to hit the target totalTankLength. All writes go through the field-changed
        /// handlers, which reposition attachments and walk the symmetry group.
        /// </summary>
        private static void ApplyRoTank(Part p, PartModule roTank, float diameter, float length)
        {
            ChangeDimension(p, roTank, "currentDiameter", diameter);

            if (GetBool(roTank, "lengthWidth"))
            {
                // totalTankLength = currentLength + a fixed offset (end caps minus dome) that doesn't
                // depend on currentLength, so measure it at the new diameter and back-solve.
                float offset = GetFloat(roTank, "totalTankLength") - GetFloat(roTank, "currentLength");
                ChangeDimension(p, roTank, "currentLength", length - offset);
            }
            else
            {
                // Length is driven by currentVScale; totalTankLength scales ~linearly with it.
                float totalLen = GetFloat(roTank, "totalTankLength");
                float curVScale = GetFloat(roTank, "currentVScale");
                if (totalLen > 0f && curVScale > 0f)
                    ChangeDimension(p, roTank, "currentVScale", curVScale * length / totalLen);
            }
        }

        /// <summary>
        /// Writes diameter/length onto the active ProceduralPart shape. The target fields mirror
        /// ModuleToolingPTank.GetDimensions so we set exactly what tooling reads back: "length", and
        /// the diameter field for this shape class -- "diameter" for plain shapes, "outerDiameter"
        /// for hollow ones, and top/bottom (set equal) for cones so the tooled diameter (max of the
        /// two) lands on the requested value. Truss shapes use an unrelated realLength/rodDiameter
        /// model and are skipped.
        /// </summary>
        private static bool ApplyProcShape(Part p, PartModule procPart, float diameter, float length)
        {
            string shapeName = (string)procPart.Fields["shapeName"]?.GetValue(procPart);
            if (string.IsNullOrEmpty(shapeName)) return false;
            if (shapeName.Contains("Truss")) { Msg($"Refit doesn't support {shapeName} shapes."); return false; }

            PartModule shape = ShapeForName(p, shapeName);
            if (shape == null) return false;

            bool isCone = shapeName.Contains("Cone");
            bool isHollow = shapeName.Contains("Hollow");

            ChangeDimension(p, shape, "length", length);
            if (isCone)
            {
                ChangeDimension(p, shape, isHollow ? "topOuterDiameter" : "topDiameter", diameter);
                ChangeDimension(p, shape, isHollow ? "bottomOuterDiameter" : "bottomDiameter", diameter);
            }
            else
            {
                ChangeDimension(p, shape, isHollow ? "outerDiameter" : "diameter", diameter);
            }
            return true;
        }

        /// <summary>Resolves the ProceduralShape* PartModule that matches the part's current shape name.</summary>
        private static PartModule ShapeForName(Part p, string shapeName)
        {
            string moduleName = shapeName switch
            {
                "Smooth Cone"            => "ProceduralShapeBezierCone",
                "Cone"                   => "ProceduralShapeCone",
                "Fillet Cylinder"        => "ProceduralShapePill",
                "Polygon"                => "ProceduralShapePolygon",
                "Hollow Cylinder"        => "ProceduralShapeHollowCylinder",
                "Hollow Cone"            => "ProceduralShapeHollowCone",
                "Hollow Fillet Cylinder" => "ProceduralShapeHollowPill",
                "Truss"                  => "ProceduralShapeHollowTruss",
                _                        => "ProceduralShapeCylinder",
            };
            return p.Modules.GetModule(moduleName);
        }

        /// <summary>
        /// Drives one editable dimension field the way a PAW slider does: writes the value on the
        /// master module, mirrors it onto the same module on every symmetry counterpart (so their
        /// geometry rebuilds to the new size), then fires the master field's onFieldChanged once with
        /// the prior value. That handler (ProceduralParts' OnShapeDimensionChanged, ROTank's
        /// OnDiameterChanged, ...) rebuilds the mesh, repositions attach nodes, pushes attached parts,
        /// and already walks the symmetry group -- so it must NOT be invoked per counterpart, or
        /// attachments get translated multiple times and parts drift.
        /// </summary>
        private static void ChangeDimension(Part masterPart, PartModule masterModule, string fieldName, float value)
        {
            BaseField mf = masterModule.Fields[fieldName];
            if (mf == null) return;
            object oldValue;
            try { oldValue = mf.GetValue(masterModule); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RP0 Tooling] ChangeDimension could not read '{fieldName}' on {masterModule.moduleName}: {ex.Message}");
                return;
            }

            SetFieldValue(masterModule, mf, value);

            List<Part> counterparts = masterPart.symmetryCounterparts;
            if (counterparts != null)
                for (int i = 0; i < counterparts.Count; i++)
                {
                    Part c = counterparts[i];
                    if (c == null || c == masterPart) continue;
                    PartModule cm = c.Modules.GetModule(masterModule.moduleName);
                    BaseField cf = cm?.Fields[fieldName];
                    if (cf != null) SetFieldValue(cm, cf, value);
                }

            try { mf.uiControlEditor?.onFieldChanged?.Invoke(mf, oldValue); }
            catch (Exception ex) { Debug.LogWarning($"[RP0 Tooling] onFieldChanged for '{fieldName}' on {masterModule.moduleName} threw: {ex.Message}"); }
        }

        private static void SetFieldValue(PartModule m, BaseField f, float value)
        {
            try { f.SetValue(value, m); }
            catch (Exception ex) { Debug.LogWarning($"[RP0 Tooling] could not write '{f.name}' on {m.moduleName}: {ex.Message}"); }
        }

        /// <summary>Reads a float-valued BaseField via reflection, returning 0 if it's absent or unreadable.</summary>
        private static float GetFloat(PartModule m, string name)
        {
            try
            {
                BaseField f = m.Fields[name];
                if (f == null) return 0f;
                object v = f.GetValue(m);
                return v is float fv ? fv : Convert.ToSingle(v);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RP0 Tooling] GetFloat could not read '{name}' on {m.moduleName}: {ex.Message}");
                return 0f;
            }
        }

        /// <summary>Reads a bool-valued BaseField via reflection, returning false if absent or unreadable.</summary>
        private static bool GetBool(PartModule m, string name)
        {
            try
            {
                BaseField f = m.Fields[name];
                return f != null && f.GetValue(m) is bool b && b;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RP0 Tooling] GetBool could not read '{name}' on {m.moduleName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Invokes a method by name + argument count via reflection. Returns false (and logs) when
        /// the method is missing or the call throws, so callers can fall back to an alternate API.
        /// </summary>
        private static bool TryInvoke(object o, string method, object[] args)
        {
            int argCount = args?.Length ?? 0;
            MethodInfo mi = o.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == method && m.GetParameters().Length == argCount);
            if (mi == null) return false;
            try { mi.Invoke(o, args); return true; }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RP0 Tooling] {method}({argCount} args) on {o.GetType().Name} threw: {ex.Message}");
                return false;
            }
        }

        private static void Msg(string s) => ScreenMessages.PostScreenMessage(new ScreenMessage(s, 4f, ScreenMessageStyle.UPPER_CENTER));
    }
}
