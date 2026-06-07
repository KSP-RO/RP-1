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
        // Resolves the part whose PAW is currently visible. Returns null when nothing's open, or
        // when multiple PAWs are up (ambiguous which one the player means).
        public static Part PawTarget()
        {
            var windows = UIPartActionController.Instance?.windows;
            if (windows == null) return null;
            Part candidate = null;
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                if (w == null || w.part == null) continue;
                if (candidate != null) return null;        // 2+ PAWs -> ambiguous
                candidate = w.part;
            }
            return candidate;
        }

        // Returns the part's current RF tank type if it has a ModuleFuelTanks, else null.
        public static string CurrentTankType(Part p)
        {
            if (p == null) return null;
            return p.Modules.GetModule<ModuleFuelTanks>()?.type;
        }

        // Picks an RF tank type to apply when refitting: prefer the part's current type if it's
        // already in the row's source list (no change needed), otherwise the first source in the
        // list that the part can actually accept (per ModuleFuelTanks.typesAvailable), and finally
        // just the first source if we can't see the available list.
        public static string PickRfType(Part p, IEnumerable<string> sources)
        {
            if (p == null || sources == null) return null;
            var rf = p.Modules.GetModule<ModuleFuelTanks>();
            var srcList = sources as IList<string> ?? sources.ToList();
            if (srcList.Count == 0) return null;
            if (rf?.type != null)
            {
                foreach (var s in srcList) if (string.Equals(s, rf.type, StringComparison.Ordinal)) return rf.type;
            }
            // Try to honour the part's typesAvailable so we don't pick a material that won't apply.
            string[] available = null;
            try { available = (rf?.typesAvailable as IEnumerable<string>)?.ToArray(); } catch { }
            if (available != null && available.Length > 0)
            {
                foreach (var s in srcList)
                    if (Array.IndexOf(available, s) >= 0) return s;
            }
            return srcList[0];
        }

        public static void Resize(Part p, float diameter, float length, string targetRfType = null)
        {
            if (p == null) { Msg("No part PAW open."); return; }
            try
            {
                bool typeChanged = false;
                if (!string.IsNullOrEmpty(targetRfType))
                {
                    var rf = p.Modules.GetModule<ModuleFuelTanks>();
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

        static void Apply(Part p, float diameter, float length)
        {
            var roTank = p.Modules.Cast<PartModule>().FirstOrDefault(m => m.moduleName == "ModuleROTank");
            if (roTank != null)
            {
                ApplyRoTank(roTank, diameter, length);
            }
            else
            {
                var procPart = p.Modules.Cast<PartModule>().FirstOrDefault(m => m.moduleName == "ProceduralPart");
                if (procPart != null) ApplyProcShape(p, procPart, diameter, length);
            }
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch?.ship);
        }

        static void ApplyRfTankType(ModuleFuelTanks rfTank, string rfType)
        {
            var typeField = typeof(ModuleFuelTanks).GetField("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (typeField != null) typeField.SetValue(rfTank, rfType);
            else
            {
                var typeProp = typeof(ModuleFuelTanks).GetProperty("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                typeProp?.SetValue(rfTank, rfType, null);
            }
            if (!TryInvoke(rfTank, "UpdateTankType", new object[] { false }) &&
                !TryInvoke(rfTank, "UpdateTankType", null))
            {
                TryInvoke(rfTank, "ChangeTankType", new object[] { rfType });
            }
        }

        // ROTanks geometry is driven by two editable knobs: currentDiameter (cross-section) and
        // currentVScale (vertical stretch). totalTankLength / largestDiameter are computed outputs.
        // ROT meshes scale uniformly with diameter, so a fixed vScale gives a length proportional
        // to diameter:
        //   totalTankLength = aspect × currentDiameter × currentVScale
        // We measure aspect from the current state BEFORE mutating anything and invert it to find
        // the vScale that yields the target length at the target diameter.
        static void ApplyRoTank(PartModule roTank, float diameter, float length)
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

        static void ApplyProcShape(Part p, PartModule procPart, float diameter, float length)
        {
            var shapeName = (string)procPart.Fields["shapeName"]?.GetValue(procPart);
            var shape = ShapeForName(p, shapeName);
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

        static PartModule ShapeForName(Part p, string shapeName)
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

        // BaseField.SetValue writes the underlying field, but some KSP versions don't fire the
        // UI_Control.onFieldChanged callback that shape modules listen on; fire it explicitly with
        // the prior value so the shape rebuilds even when there's no PAW interaction.
        static void SetField(PartModule m, string name, float value)
        {
            var f = m.Fields[name];
            if (f == null) return;
            object before = null; try { before = f.GetValue(m); } catch { }
            try
            {
                f.SetValue(value, m);
                try { f.uiControlEditor?.onFieldChanged?.Invoke(f, before); } catch { }
            }
            catch { }
        }

        static float GetFloat(PartModule m, string name)
        {
            try
            {
                var f = m.Fields[name];
                if (f == null) return 0f;
                var v = f.GetValue(m);
                return v is float fv ? fv : Convert.ToSingle(v);
            }
            catch { return 0f; }
        }

        static bool TryInvoke(object o, string method, object[] args)
        {
            int argCount = args?.Length ?? 0;
            var mi = o.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == method && m.GetParameters().Length == argCount);
            if (mi == null) return false;
            try { mi.Invoke(o, args); return true; }
            catch { return false; }
        }

        static void Msg(string s) => ScreenMessages.PostScreenMessage(new ScreenMessage(s, 4f, ScreenMessageStyle.UPPER_CENTER));
    }
}
