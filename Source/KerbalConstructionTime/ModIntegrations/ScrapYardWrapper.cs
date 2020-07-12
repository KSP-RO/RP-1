using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

//TODO: Change namespace to your mod's namespace
namespace KerbalConstructionTime
{
    //DO NOT CHANGE ANYTHING BELOW THIS LINE
    public sealed class ScrapYardWrapper
    {
        private static bool? available;
        private static Type SYType;
        private static object _instance;

        /// <summary>
        /// The part tracker type to reference
        /// </summary>
        public enum TrackType
        {
            /// <summary>
            /// Total number of builds/uses combining new and reused
            /// </summary>
            TOTAL,
            /// <summary>
            /// Only new builds/uses of the part
            /// </summary>
            NEW,
            /// <summary>
            /// Only reused builds/uses of the part
            /// </summary>
            INVENTORIED
        }

        /// <summary>
        /// The strictness of comparing two parts for equivalency
        /// </summary>
        public enum ComparisonStrength
        {
            /// <summary>
            /// Equivalent if their names match
            /// </summary>
            NAME,
            /// <summary>
            /// EqualEquivalent if name and dry cost match
            /// </summary>
            COSTS,
            /// <summary>
            /// Equaivalent if name, dry cost, and Modules (except ModuleSYPartTracker) match
            /// </summary>
            MODULES,
            /// <summary>
            /// Equivalent if name, dry cost, Modules, and TimesRecovered match
            /// </summary>
            TRACKER,
            /// <summary>
            /// Equivalent if name, dry cost, Modules, TimesRecovered and IDs match
            /// </summary>
            STRICT
        }

        /// <summary>
        /// True if ScrapYard is available, false if not
        /// </summary>
        public static bool Available
        {
            get
            {
                if (available == null)
                {
                    SYType = AssemblyLoader.loadedAssemblies
                        .Select(a => a.assembly.GetExportedTypes())
                        .SelectMany(t => t)
                        .FirstOrDefault(t => t.FullName == "ScrapYard.APIManager");
                    available = SYType != null;
                }
                return available.GetValueOrDefault();
            }
        }

        #region Public Methods

        #region Inventory Manipulation
        /// <summary>
        /// Takes a List of Parts and returns the Parts that are present in the inventory. 
        /// </summary>
        /// <param name="sourceParts">Source list of parts</param>
        /// <param name="strictness">How strict of a comparison to use. Defaults to MODULES</param>
        /// <returns>List of Parts that are in the inventory</returns>
        public static IList<Part> GetPartsInInventory(IEnumerable<Part> sourceParts, ComparisonStrength strictness = ComparisonStrength.MODULES)
        {
            if (!Available)
            {
                return null;
            }
            return (IList<Part>)invokeMethod("GetPartsInInventory_Parts", sourceParts, strictness.ToString());
            //Why do a ToString on an enum instead of casting to int? Because if the internal enum changes then the intended strictness is kept.
        }

        /// <summary>
        /// Takes a List of part ConfigNodes and returns the ConfigNodes that are present in the inventory. 
        /// </summary>
        /// <param name="sourceParts">Source list of parts</param>
        /// <param name="strictness">How strict of a comparison to use. Defaults to MODULES</param>
        /// <returns>List of part ConfigNodes that are in the inventory</returns>
        public static IList<ConfigNode> GetPartsInInventory(IEnumerable<ConfigNode> sourceParts, ComparisonStrength strictness = ComparisonStrength.MODULES)
        {
            if (!Available)
            {
                return null;
            }
            return (IList<ConfigNode>)invokeMethod("GetPartsInInventory_ConfigNodes", sourceParts, strictness.ToString());
            //Why do a ToString on an enum instead of casting to int? Because if the internal enum changes then the intended strictness is kept.
        }

        /// <summary>
        /// Adds a list of parts to the Inventory
        /// </summary>
        /// <param name="parts">The list of parts to add</param>
        /// <param name="incrementRecovery">If true, increments the number of recoveries in the tracker</param>
        public static void AddPartsToInventory(IEnumerable<Part> parts, bool incrementRecovery)
        {
            if (Available)
            {
                invokeMethod("AddPartsToInventory_Parts", parts, incrementRecovery);
            }
        }

        /// <summary>
        /// Adds a list of parts to the Inventory
        /// </summary>
        /// <param name="parts">The list of parts to add</param>
        /// <param name="incrementRecovery">If true, increments the number of recoveries in the tracker</param>
        public static void AddPartsToInventory(IEnumerable<ConfigNode> parts, bool incrementRecovery)
        {
            if (Available)
            {
                invokeMethod("AddPartsToInventory_Nodes", parts, incrementRecovery);
            }
        }

        /// <summary>
        /// Adds a part to the Inventory
        /// </summary>
        /// <param name="part">The part to add</param>
        /// <param name="incrementRecovery">If true, increments the counter for how many times the part was recovered</param>
        /// <returns>True if added, false otherwise</returns>
        public static bool AddPartToInventory(Part part, bool incrementRecovery)
        {
            if (!Available)
            {
                return false;
            }
            return (bool)invokeMethod("AddPartToInventory_Part", part, incrementRecovery);
        }

        /// <summary>
        /// Adds a part to the Inventory
        /// </summary>
        /// <param name="part">The part to add</param>
        /// <param name="incrementRecovery">If true, increments the counter for how many times the part was recovered</param>
        /// <returns>True if added, false otherwise</returns>
        public static bool AddPartToInventory(ConfigNode part, bool incrementRecovery)
        {
            if (!Available)
            {
                return false;
            }
            return (bool)invokeMethod("AddPartToInventory_Node", part, incrementRecovery);
        }


        /// <summary>
        /// Removes a part from the Inventory using the given strictness for finding the part
        /// </summary>
        /// <param name="part">The part to remove</param>
        /// <param name="strictness">The strictenss to use when searching for the part. Defaults to MODULES</param>
        /// <returns>True if removed, false otherwise.</returns>
        public static bool RemovePartFromInventory(Part part, ComparisonStrength strictness = ComparisonStrength.MODULES)
        {
            if (!Available)
            {
                return false;
            }
            return (bool)invokeMethod("RemovePartFromInventory_Part", part, strictness.ToString());
        }

        /// <summary>
        /// Removes a part from the Inventory using the given strictness for finding the part
        /// </summary>
        /// <param name="part">The part to remove</param>
        /// <param name="strictness">The strictenss to use when searching for the part. Defaults to MODULES</param>
        /// <returns>True if removed, false otherwise.</returns>
        public static bool RemovePartFromInventory(ConfigNode part, ComparisonStrength strictness = ComparisonStrength.MODULES)
        {
            if (!Available)
            {
                return false;
            }
            return (bool)invokeMethod("RemovePartFromInventory_Node", part, strictness.ToString());
        }

        /// <summary>
        /// Finds a part in the inventory for the given part
        /// </summary>
        /// <param name="part">The part to search for</param>
        /// <param name="strictness">The strictness to use when searching for the part. Defaults to MODULES.</param>
        /// <returns>A ConfigNode representing the InventoryPart, or null if none found.</returns>
        public static ConfigNode FindInventoryPart(Part part, ComparisonStrength strictness = ComparisonStrength.MODULES)
        {
            if (!Available)
            {
                return null;
            }
            return invokeMethod("FindInventoryPart_Part", part, strictness.ToString()) as ConfigNode;
        }

        /// <summary>
        /// Finds a part in the inventory for the given part
        /// </summary>
        /// <param name="part">The part to search for</param>
        /// <param name="strictness">The strictness to use when searching for the part. Defaults to MODULES.</param>
        /// <returns>A ConfigNode representing the InventoryPart, or null if none found.</returns>
        public static ConfigNode FindInventoryPart(ConfigNode part, ComparisonStrength strictness = ComparisonStrength.MODULES)
        {
            if (!Available)
            {
                return null;
            }
            return invokeMethod("FindInventoryPart_Node", part, strictness.ToString()) as ConfigNode;
        }


        /// <summary>
        /// Finds a part in the inventory for the given id
        /// </summary>
        /// <param name="id">The id of the part to search for.</param>
        /// <returns>A ConfigNode representing the InventoryPart, or null if none found.</returns>
        public static ConfigNode FindInventoryPart(string id)
        {
            if (!Available)
            {
                return null;
            }
            return invokeMethod("FindInventoryPart_ID", id) as ConfigNode;
        }

        /// <summary>
        /// Gets all parts in the inventory as a list of ConfigNodes
        /// </summary>
        /// <returns>The list of all inventory parts</returns>
        public static IList<ConfigNode> GetAllInventoryParts()
        {
            if (!Available)
            {
                return null;
            }
            return invokeMethod("GetAllInventoryParts") as IList<ConfigNode>;
        }

        /// <summary>
        /// Refreshes a part node to be fresh and not from the inventory
        /// </summary>
        /// <param name="partNode">The part to refresh</param>
        /// <returns>Success</returns>
        public static bool RefreshPart(ConfigNode partNode)
        {
            if (!Available)
            {
                return false;
            }
            return (bool)invokeMethod("RefreshPart_Node", partNode);
        }
        #endregion Inventory Manipulation


        #region Vessel Processing
        /// <summary>
        /// Removes inventory parts, refunds funds, marks it as tracked
        /// </summary>
        /// <param name="parts">The vessel as a List of Parts</param>
        /// <returns>True if processed, false otherwise</returns>
        public static bool ProcessVessel(IEnumerable<Part> parts)
        {
            if (!Available)
            {
                return false;
            }
            return (bool)invokeMethod("ProcessVessel_Parts", parts);
        }

        /// <summary>
        /// Removes inventory parts, refunds funds, marks it as tracked
        /// </summary>
        /// <param name="parts">The vessel as a List of part ConfigNodes</param>
        /// <returns>True if processed, false otherwise</returns>
        public static bool ProcessVessel(IEnumerable<ConfigNode> parts)
        {
            if (!Available) return false;
            return (bool)invokeMethod("ProcessVessel_Nodes", parts);
        }



        /// <summary>
        /// Records a build in the part tracker
        /// </summary>
        /// <param name="parts">The vessel as a list of Parts.</param>
        public static void RecordBuild(IEnumerable<Part> parts)
        {
            if (Available)
            {
                invokeMethod("RecordBuild_Parts", parts);
            }
        }

        /// <summary>
        /// Records a build in the part tracker
        /// </summary>
        /// <param name="parts">The vessel as a list of ConfigNodes.</param>
        public static void RecordBuild(IEnumerable<ConfigNode> parts)
        {
            if (Available)
            {
                invokeMethod("RecordBuild_Nodes", parts);
            }
        }

        /// <summary>
        /// Sets whether a vessel is tracked or not
        /// </summary>
        /// <param name="id">The ID of the vessel</param>
        /// <param name="newStatus">The status to set</param>
        /// <returns>The previous status</returns>
        public static bool SetProcessedStatus(string id, bool newStatus)
        {
            if (Available)
            {
                return (bool)invokeMethod("SetProcessedStatus_ID", id, newStatus);
            }
            return false;
        }

        #endregion Vessel Processing


        #region Part Tracker
        /// <summary>
        /// Gets the number of builds for a part
        /// </summary>
        /// <param name="part">The part to check</param>
        /// <returns>Number of builds for the part</returns>
        public static int GetBuildCount(Part part, TrackType trackType = TrackType.TOTAL)
        {
            if (!Available)
            {
                return 0;
            }
            return (int)invokeMethod("GetBuildCount_Part", part, trackType.ToString());
        }

        /// <summary>
        /// Gets the number of builds for a part
        /// </summary>
        /// <param name="partNode">The ConfigNode of the part to check</param>
        /// <returns>Number of builds for the part</returns>
        public static int GetBuildCount(ConfigNode part, TrackType trackType = TrackType.TOTAL)
        {
            if (!Available)
            {
                return 0;
            }
            return (int)invokeMethod("GetBuildCount_Node", part, trackType.ToString());
        }

        /// <summary>
        /// Gets the number of total uses of a part
        /// </summary>
        /// <param name="part">The part to check</param>
        /// <returns>Number of uses of the part</returns>
        public static int GetUseCount(Part part, TrackType trackType = TrackType.TOTAL)
        {
            if (!Available)
            {
                return 0;
            }
            return (int)invokeMethod("GetUseCount_Part", part, trackType.ToString());
        }

        /// <summary>
        /// Gets the number of total uses of a part
        /// </summary>
        /// <param name="partNode">The ConfigNode of the part to check</param>
        /// <returns>Number of uses of the part</returns>
        public static int GetUseCount(ConfigNode part, TrackType trackType = TrackType.TOTAL)
        {
            if (!Available)
            {
                return 0;
            }
            return (int)invokeMethod("GetUseCount_Node", part, trackType.ToString());
        }

        /// <summary>
        /// Gets the unique ID for the current part.
        /// It is recommended to cache this.
        /// </summary>
        /// <param name="part">The part to get the ID of</param>
        /// <returns>The part's ID (a Guid) as a string or null if it can't be gotten</returns>
        public static string GetPartID(Part part)
        {
            if (!Available)
            {
                return null;
            }
            return invokeMethod("GetPartID_Part", part) as string;
        }

        /// <summary>
        /// Gets the unique ID for the current part.
        /// It is recommended to cache this.
        /// </summary>
        /// <param name="part">The part to get the ID of</param>
        /// <returns>The part's ID (a Guid) as a string or null if it can't be gotten</returns>
        public static string GetPartID(ConfigNode part)
        {
            if (!Available)
            {
                return null;
            }
            return invokeMethod("GetPartID_Node", part) as string;
        }

        /// <summary>
        /// Gets the number of times a part has been recovered. 
        /// It is recommended to cache this.
        /// </summary>
        /// <param name="part">The part to get the TimesRecovered count of.</param>
        /// <returns>The number of times the part has been recovered.</returns>
        public static int GetTimesUsed(Part part)
        {
            if (!Available)
            {
                return 0;
            }
            return (int)invokeMethod("GetTimesUsed_Part", part);
        }

        /// <summary>
        /// Gets the number of times a part has been recovered. 
        /// It is recommended to cache this.
        /// </summary>
        /// <param name="part">The part to get the TimesRecovered count of.</param>
        /// <returns>The number of times the part has been recovered.</returns>
        public static int GetTimesUsed(ConfigNode part)
        {
            if (!Available)
            {
                return 0;
            }
            return (int)invokeMethod("GetTimesUsed_Node", part);
        }

        /// <summary>
        /// Checks if the part is pulled from the inventory or is new
        /// </summary>
        /// <param name="part">The part to check</param>
        /// <returns>True if from inventory, false if new</returns>
        public static bool PartIsFromInventory(Part part)
        {
            if (!Available)
            {
                return false;
            }
            return (bool)invokeMethod("PartIsFromInventory_Part", part);
        }

        /// <summary>
        /// Checks if the part is pulled from the inventory or is new
        /// </summary>
        /// <param name="part">The part to check</param>
        /// <returns>True if from inventory, false if new</returns>
        public static bool PartIsFromInventory(ConfigNode part)
        {
            if (!Available)
            {
                return false;
            }
            return (bool)invokeMethod("PartIsFromInventory_Node", part);
        }
        #endregion Part Tracker

        #region Settings
        /// <summary>
        /// The list of part names that are blacklisted
        /// </summary>
        public static IEnumerable<string> PartBlacklist
        {
            get
            {
                return Available ? invokeMethod("GetSetting_PartBlacklist") as IEnumerable<string> : null;
            }
        }

        /// <summary>
        /// Whether or not to automatically apply the inventory while building ships in the editor
        /// </summary>
        public static bool AutoApplyInventory
        {
            get
            {
                return Available ? (bool)invokeMethod("GetSetting_AutoApplyInventory") : false;
            }
            set
            {
                if (Available)
                {
                    invokeMethod("SetSetting_AutoApplyInventory", value);
                }
            }
        }

        /// <summary>
        /// Whether the mod is enabled for this save
        /// </summary>
        public static bool ModEnabled
        {
            get
            {
                return Available ? (bool)invokeMethod("GetSetting_ModEnabled") : false;
            }
        }

        /// <summary>
        /// Whether the inventory is in use for this save
        /// </summary>
        public static bool UseInventory
        {
            get
            {
                return Available ? (bool)invokeMethod("GetSetting_UseInventory") : false;
            }
        }

        /// <summary>
        /// Whether the part use tracker is enabled for this save
        /// </summary>
        public static bool UseTracker
        {
            get
            {
                return Available ? (bool)invokeMethod("GetSetting_UseTracker") : false;
            }
        }

        /// <summary>
        /// Whether the Override Funds option is in use for this save
        /// </summary>
        public static bool OverrideFunds
        {
            get
            {
                return Available ? (bool)invokeMethod("GetSetting_OverrideFunds") : false;
            }
        }
        #endregion Settings

        #endregion Public Methods

        #region Private Methods
        /// <summary>
        /// The static instance of the APIManager within ScrapYard
        /// </summary>
        private static object Instance
        {
            get
            {
                if (Available && _instance == null)
                {
                    _instance = SYType.GetProperty("Instance").GetValue(null, null);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Invokes a method on the ScrapYard API
        /// </summary>
        /// <param name="methodName">The name of the method</param>
        /// <param name="parameters">Parameters to pass to the method</param>
        /// <returns>The response</returns>
        private static object invokeMethod(string methodName, params object[] parameters)
        {
            MethodInfo method = SYType.GetMethod(methodName);
            return method?.Invoke(Instance, parameters);
        }
        #endregion Private Methods
    }
}
