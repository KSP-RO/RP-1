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
        /// row's materials (no switch needed); otherwise the first material the part actually
        /// advertises as available (per ModuleFuelTanks.typesAvailable). Never falls back to a
        /// material the part can't accept -- forcing a tech-locked or incompatible type would leave
        /// the tank in an invalid state. The caller disables Refit and explains why when this is null.
        /// </summary>
        public static string PickRfType(Part p, IEnumerable<string> sources)
        {
            if (p == null || sources == null) return null;
            ModuleFuelTanks rf = p.Modules.GetModule<ModuleFuelTanks>();
            IList<string> srcList = sources as IList<string> ?? sources.ToList();
            if (srcList.Count == 0) return null;
            if (rf?.type != null)
            {
                foreach (string s in srcList) if (string.Equals(s, rf.type, StringComparison.Ordinal)) return rf.type;
            }
            // Only switch to a material the part advertises as available. If we can't read the
            // available list, or none of the row's materials are in it, refuse rather than guess.
            string[] available = (rf?.typesAvailable as IEnumerable<string>)?.ToArray();
            if (available != null)
            {
                foreach (string s in srcList)
                    if (Array.IndexOf(available, s) >= 0) return s;
            }
            return null;
        }

        /// <summary>
        /// Writes (diameter, length) to the part, optionally switching its RF tank type first.
        /// Posts a screen message describing the outcome; failures are logged and surfaced, not thrown.
        /// </summary>
        public static void Resize(Part p, float diameter, float length, string targetRfType = null)
        {
            if (p == null) { Msg("No part PAW open."); return; }
            try
            {
                bool typeChanged = false;
                if (!string.IsNullOrEmpty(targetRfType))
                {
                    ModuleFuelTanks rf = p.Modules.GetModule<ModuleFuelTanks>();
                    if (rf != null && !string.Equals(rf.type, targetRfType, StringComparison.Ordinal))
                    {
                        ApplyRfTankType(rf, targetRfType);
                        typeChanged = true;
                    }
                }
                Apply(p, diameter, length);
                Msg(typeChanged
                    ? $"Refit {p.partInfo?.title} to {targetRfType} at d={diameter:F3}m, L={length:F3}m"
                    : $"Resized {p.partInfo?.title} to d={diameter:F3}m, L={length:F3}m");
            }
            catch (Exception ex) { Debug.LogError("[RP0 Tooling] resize failed: " + ex); Msg("Resize failed: " + ex.Message); }
        }

        /// <summary>Dispatches the dimension write to whichever procedural module the part carries.</summary>
        private static void Apply(Part p, float diameter, float length)
        {
            PartModule roTank = p.Modules.Cast<PartModule>().FirstOrDefault(m => m.moduleName == "ModuleROTank");
            if (roTank != null)
            {
                ApplyRoTank(roTank, diameter, length);
            }
            else
            {
                PartModule procPart = p.Modules.Cast<PartModule>().FirstOrDefault(m => m.moduleName == "ProceduralPart");
                if (procPart != null) ApplyProcShape(p, procPart, diameter, length);
            }
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch?.ship);
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
        /// ROTanks geometry is driven by two editable knobs: currentDiameter (cross-section) and
        /// currentVScale (vertical stretch). totalTankLength / largestDiameter are computed outputs.
        /// ROT meshes scale uniformly with diameter, so a fixed vScale gives a length proportional
        /// to diameter: totalTankLength = aspect * currentDiameter * currentVScale. We measure aspect
        /// from the current state BEFORE mutating anything and invert it to find the vScale that
        /// yields the target length at the target diameter.
        /// </summary>
        private static void ApplyRoTank(PartModule roTank, float diameter, float length)
        {
            float curD        = GetFloat(roTank, "currentDiameter");
            float curVScale   = GetFloat(roTank, "currentVScale");
            float curTotalLen = GetFloat(roTank, "totalTankLength");
            float aspect = (curD > 0f && curVScale > 0f && curTotalLen > 0f)
                ? curTotalLen / (curD * curVScale)
                : 1f;
            float newVScale = (aspect > 0f && diameter > 0f) ? length / (aspect * diameter) : 1f;

            SetField(roTank, "currentDiameter", diameter);
            SetField(roTank, "largestDiameter", diameter);
            SetField(roTank, "currentVScale", newVScale);
            SetField(roTank, "totalTankLength", length);
        }

        /// <summary>Writes diameter/length onto the active ProceduralPart shape and forces a rebuild.</summary>
        private static void ApplyProcShape(Part p, PartModule procPart, float diameter, float length)
        {
            string shapeName = (string)procPart.Fields["shapeName"]?.GetValue(procPart);
            PartModule shape = ShapeForName(p, shapeName);
            if (shape == null) return;
            SetField(shape, "length", length);
            SetField(shape, "diameter", diameter);
            SetField(shape, "topDiameter", diameter);
            SetField(shape, "bottomDiameter", diameter);
            // ProceduralShape rebuilds via Update() polling old vs current; calling UpdateShape()
            // nudges it now instead of waiting for next frame.
            TryInvoke(shape, "UpdateShape", new object[] { true });
            TryInvoke(procPart, "UpdateShape", new object[] { true });
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
            return p.Modules.Cast<PartModule>().FirstOrDefault(m => m.moduleName == moduleName);
        }

        /// <summary>
        /// BaseField.SetValue writes the underlying field, but some KSP versions don't fire the
        /// UI_Control.onFieldChanged callback that shape modules listen on; fire it explicitly with
        /// the prior value so the shape rebuilds even when there's no PAW interaction.
        /// </summary>
        private static void SetField(PartModule m, string name, float value)
        {
            BaseField f = m.Fields[name];
            if (f == null) return;
            object before = null;
            try { before = f.GetValue(m); }
            catch (Exception ex) { Debug.LogWarning($"[RP0 Tooling] SetField could not read '{name}' on {m.moduleName}: {ex.Message}"); }
            try
            {
                f.SetValue(value, m);
                try { f.uiControlEditor?.onFieldChanged?.Invoke(f, before); }
                catch (Exception ex) { Debug.LogWarning($"[RP0 Tooling] SetField onFieldChanged callback for '{name}' on {m.moduleName} threw: {ex.Message}"); }
            }
            catch (Exception ex) { Debug.LogWarning($"[RP0 Tooling] SetField could not write '{name}' on {m.moduleName}: {ex.Message}"); }
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
