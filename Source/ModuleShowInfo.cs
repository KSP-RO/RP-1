using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class ModuleShowInfoUpdater : MonoBehaviour
    {
        protected bool run = true;
        private void Update()
        {
            if (run)
            {
                EntryCostDatabaseAccessor.Init();
                EntryCostDatabaseAccessor.GetFields();
//                EntryCostDatabaseAccessor.ScanDatabase();
                foreach (AvailablePart ap in EntryCostDatabaseAccessor.nameToPart.Values)
                {
                    if (ap.partPrefab.FindModuleImplementing<ModuleShowInfo>() is ModuleShowInfo msi)
                    {
                        foreach (AvailablePart.ModuleInfo x in ap.moduleInfos)
                        {
                            if (x.moduleName.Equals(ModuleShowInfo.sModuleName))
                            {
                                x.info = msi.GetInfo();
                            }
                        }
                    }
                }

                run = false;
                GameObject.Destroy(this);
            }
        }
    }

    public class EntryCostDatabaseAccessor
    {
        public static Dictionary<string, RealFuels.PartEntryCostHolder> holders = null;
        public static Dictionary<string, AvailablePart> nameToPart = null;
        private static FieldInfo holdersField = null, nameToPartField = null;
        private static bool initialized = false;

        public static void Init()
        {
            if (initialized) return;
            initialized = true;

            if (AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == "RealFuels") is var RealFuelsAssembly &&
                RealFuelsAssembly.assembly.GetType("RealFuels.EntryCostDatabase") is Type rfEntryCostDatabaseType)
            {
                holdersField = rfEntryCostDatabaseType.GetField("holders", BindingFlags.NonPublic | BindingFlags.Static);
                nameToPartField = rfEntryCostDatabaseType.GetField("nameToPart", BindingFlags.NonPublic | BindingFlags.Static);
            } 
        }

        public static void GetFields()
        {
            holders = (Dictionary<string, RealFuels.PartEntryCostHolder>)holdersField.GetValue(null);
            nameToPart = (Dictionary<string, AvailablePart>)nameToPartField.GetValue(null);
        }

        public static RealFuels.PartEntryCostHolder GetHolder(string s)
        {
            if (holders is null) return null;
            if (!holders.TryGetValue(s, out RealFuels.PartEntryCostHolder val))
            {
                // Debug.LogWarning($"EntryCostModifierDatabase missing ECM PartHolder for {s}");
            }
            return val;
        }

        public static int GetCost(RealFuels.PartEntryCostHolder h, bool clearTracker=true)
        {
            if (clearTracker) RealFuels.EntryCostDatabase.ClearTracker();
            return h.GetCost();
        }

        public static bool IsUnlocked(string name) => RealFuels.EntryCostDatabase.IsUnlocked(name);
        private static string Color(bool unlocked) => unlocked ? "<color=green>" : null;
        private static string Uncolor(bool unlocked) => unlocked ? "</color>" : null;

        private static string CostString(RealFuels.PartEntryCostHolder h, int cost=-1)
        {
            if (cost == -1) cost = GetCost(h);
            bool unlocked = IsUnlocked(h.name);
            string sCost = !unlocked ? $": {cost}" : null;
            return $"{Color(unlocked)}{h.name}{sCost}{Uncolor(unlocked)}";
        }

        public static string DisplayHolder(RealFuels.PartEntryCostHolder h, bool recurse=false)
        {
            if (h is null) return "null";
            if (h.cost == 0 && h.children.Count == 1) return DisplayHolder(GetHolder(h.children.First()), recurse);
            string s = string.Empty;
            foreach (string child in h.children)
            {
                if (GetHolder(child) is RealFuels.PartEntryCostHolder childHolder)
                    s += $" | {CostString(childHolder)}";
            }
            // Append the recursive scan after building the top level, instead of during.
            if (recurse)
            {
                foreach (string child in h.children)
                {
                    if (GetHolder(child) is RealFuels.PartEntryCostHolder childHolder)
                        s += $"[{DisplayHolder(childHolder, recurse)}]";
                }
            }

            return $"{CostString(h, h.cost)}{s}";
        }

        public static void ScanDatabase()
        {
            if (holders != null)
            {
                foreach (var x in holders)   // Recurse through the database to touch all holders.
                {
                    DisplayHolder(x.Value, true);
                }
            }
        }
    }

    public class ModuleShowInfo : PartModule
    {
        public static string sModuleName = "Show Info";
        public override string GetModuleDisplayName() => "Unlock Requirements";

        public override string GetInfo()
        {
            EntryCostDatabaseAccessor.Init();
            EntryCostDatabaseAccessor.GetFields();

            string data = null, APInfo = null;
            if (EntryCostDatabaseAccessor.GetHolder(part.name) is RealFuels.PartEntryCostHolder h)
                data = $"Total cost: {EntryCostDatabaseAccessor.GetCost(h)}\n{EntryCostDatabaseAccessor.DisplayHolder(h, false)}";
            if (part.partInfo is AvailablePart ap)
                APInfo = $"Tech Required: {ap.TechRequired}";
            string res = $"Part name: {part.name}\n{APInfo}\n{data}";
            return res;
        }
    }
}
