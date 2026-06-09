using System;
using System.Collections.Generic;
using UnityEngine;
using ROUtils;
using ROUtils.DataTypes;

namespace RP0
{

    namespace RP0
    {
        /// <summary>
        /// The result of a transport mode lookup: the best mode available for a
        /// given vessel mass, with its current transit rate.
        /// </summary>
        public class RecoveryTransportLevel
        {
            /// <summary>Mass ceiling for this mode in kg. double.MaxValue for Barge (no limit).</summary>
            public double MaxMass;

            /// <summary>
            /// BP divisor applied in ReconRolloutProject.InitRecovery. Higher = faster recovery.
            /// </summary>
            public double TransitRate;

            /// <summary>Which transport mode was selected.</summary>
            public RecoveryTransportMode Mode;
        }

        public enum RecoveryTransportMode { Barge, Truck, Air }

        /// <summary>
        /// Reads SCMRECOVERYTECHS cfg nodes and selects the best available transport
        /// mode for a given vessel mass, mirroring the AirlaunchTechLevel pattern.
        ///
        /// Three modes in priority order: Air > Truck > Barge.
        /// A vessel uses the highest-priority mode whose MaxMass it fits within.
        /// Barge has no mass limit and is always the guaranteed fallback.
        ///
        /// What changes per tech node: MaxMassAir, MaxMassTruck, and the three
        /// TransitRate* values. Nodes are walked in cfg order; later unlocked nodes
        /// override earlier ones (same progression assumption as AirlaunchTechLevel).
        /// </summary>
        public class RecoveryTechLevel : ConfigNodePersistenceBase, IConfigNode
        {
            [Persistent] public string TechRequired = string.Empty;

            /// <summary>Max vessel mass in kg for air transport. 0 = air not yet available.</summary>
            [Persistent] public double MaxMassAir = 0d;

            /// <summary>Max vessel mass in kg for truck transport. 0 = truck not yet available.</summary>
            [Persistent] public double MaxMassTruck = 0d;

            [Persistent] public double TransitRateAir = 1.0d;
            [Persistent] public double TransitRateTruck = 1.0d;
            [Persistent] public double TransitRateBarge = 1.0d;

            private static List<RecoveryTechLevel> _techLevels = null;

            public RecoveryTechLevel() { }

            public RecoveryTechLevel(ConfigNode n)
            {
                Load(n);
            }

            public bool IsUnlocked =>
                string.IsNullOrEmpty(TechRequired) ||
                ResearchAndDevelopment.GetTechnologyState(TechRequired) == RDTech.State.Available;

            /// <summary>
            /// Returns the best transport mode available for the given vessel mass (in kg),
            /// along with the current transit rate for that mode.
            ///
            /// Walks all unlocked levels in cfg order, accumulating the highest available
            /// mass limits and the most recent rate values, then selects Air > Truck > Barge.
            /// </summary>
            public static RecoveryTransportLevel GetTransportLevel(double vesselMassKg)
            {
                EnsureLevelsLoaded();

                double maxMassAir = 0d;
                double maxMassTruck = 0d;
                double rateAir = 1.0d;
                double rateTruck = 1.0d;
                double rateBarge = 1.0d;

                foreach (var level in _techLevels)
                {
                    if (!level.IsUnlocked)
                        continue;

                    // Mass limits: take the highest value seen across all unlocked nodes
                    if (level.MaxMassAir > maxMassAir) maxMassAir = level.MaxMassAir;
                    if (level.MaxMassTruck > maxMassTruck) maxMassTruck = level.MaxMassTruck;

                    // Rates: later nodes override earlier ones
                    rateAir = level.TransitRateAir;
                    rateTruck = level.TransitRateTruck;
                    rateBarge = level.TransitRateBarge;
                }

                // Air takes priority if available and vessel fits
                if (maxMassAir > 0d && vesselMassKg <= maxMassAir)
                {
                    return new RecoveryTransportLevel
                    {
                        MaxMass = maxMassAir,
                        TransitRate = rateAir,
                        Mode = RecoveryTransportMode.Air
                    };
                }

                // Truck next
                if (maxMassTruck > 0d && vesselMassKg <= maxMassTruck)
                {
                    return new RecoveryTransportLevel
                    {
                        MaxMass = maxMassTruck,
                        TransitRate = rateTruck,
                        Mode = RecoveryTransportMode.Truck
                    };
                }

                // Barge: always available, no mass limit
                return new RecoveryTransportLevel
                {
                    MaxMass = double.MaxValue,
                    TransitRate = rateBarge,
                    Mode = RecoveryTransportMode.Barge
                };
            }

            /// <summary>
            /// Clears the cached tech level list, forcing a reload on next access.
            /// Call this if cfg data may have changed (e.g. in editor/testing contexts).
            /// </summary>
            public static void InvalidateCache()
            {
                _techLevels = null;
            }

            private static void EnsureLevelsLoaded()
            {
                if (_techLevels != null)
                    return;

                _techLevels = new List<RecoveryTechLevel>();
                foreach (ConfigNode parentNode in GameDatabase.Instance.GetConfigNodes("SCMRECOVERYTECHS"))
                {
                    foreach (ConfigNode n in parentNode.GetNodes("RECOVERYTECHLEVEL"))
                    {
                        _techLevels.Add(new RecoveryTechLevel(n));
                    }
                }
            }
        }
    }
}