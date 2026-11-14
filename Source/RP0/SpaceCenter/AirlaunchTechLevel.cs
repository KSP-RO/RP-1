using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using ROUtils.DataTypes;

namespace RP0
{
    /// <summary>
    /// An airlaunch carrier aircraft
    /// </summary>
    public class AirlaunchTechLevel : ConfigNodePersistenceBase, IConfigNode
    {
        /// <summary>
        /// Stable id, persisted in saves.
        /// </summary>
        [Persistent]
        public string name; // ID

        /// <summary>
        /// Display name.
        /// </summary>
        [Persistent]
        public string title; // Display name

        [Persistent]
        public string TechRequired;

        [Persistent]
        public string Supersedes; // Plane this carrier replaces/improves

        [Persistent]
        public double MinAltitude; // Minimum altitude (m)

        [Persistent]
        public double MaxAltitude; // Maximum altitude (m)

        [Persistent]
        public double MaxKscDistance; // Maximum Distance from KSC (m)

        [Persistent]
        public double MinVelocity; // Minimum velocity (m/s)

        [Persistent]
        public double MaxVelocity; // Maximum velocity (m/s)

        [Persistent]
        public double MaxMass; // Maximum mass (kg)

        [Persistent]
        public Vector3 MaxSize; // Size in m*m*m

        [Persistent]
        public double CostMultiplier = 1d; // Multiplier to mounting cost

        [Persistent]
        public double TimeMultiplier = 1d; // Multiplier to mounting time

        private static List<AirlaunchTechLevel> _carriers = null;

        public AirlaunchTechLevel() { }

        public AirlaunchTechLevel(ConfigNode n)
        {
            Load(n);
            if (string.IsNullOrEmpty(name))
                name = TechRequired;
            if (string.IsNullOrEmpty(title))
                title = name;
        }

        public override string ToString()
        {
            return $"{name};Alt:{MaxAltitude};Vel:{MaxVelocity}";
        }

        public double MaxMassTonnes => MaxMass / 1000d;

        public string TechTitle => ResearchAndDevelopment.GetTechnologyTitle(TechRequired);


        public bool CanLaunchVessel(VesselProject vessel, out string reason)
        {
            if (vessel == null)
            {
                reason = "No vessel";
                return false;
            }
            return CanLaunchVessel(vessel.GetTotalMass(), vessel.GetShipSize(), out reason);
        }

        /// <param name="mass">Vessel mass in tonnes</param>
        /// <param name="dimensions">Vessel size in m (width, height, length)</param>
        public bool CanLaunchVessel(double mass, Vector3 dimensions, out string reason)
        {
            // Vessel mass is in tonnes, MaxMass is in kg
            if (mass > MaxMassTonnes)
            {
                reason = $"mass {mass:0.#} t > {MaxMassTonnes:0.#} t max";
                return false;
            }
            if (dimensions.x > MaxSize.x | dimensions.y > MaxSize.y | dimensions.z > MaxSize.z)
            {
                reason = $"size {dimensions.x:0.#} x {dimensions.y:0.#} x {dimensions.z:0.#} m > {MaxSize.x:0.#} x {MaxSize.y:0.#} x {MaxSize.z:0.#} m max";
                return false;
            }

            reason = null;
            return true;
        }

        public enum FitResult { Fits, TooHeavy, TooLarge }

        /// <param name="mass">Vessel mass in tonnes</param>
        public FitResult CheckFit(double mass, Vector3 dimensions)
        {
            if (mass > MaxMassTonnes)
                return FitResult.TooHeavy;
            if (dimensions.x > MaxSize.x | dimensions.y > MaxSize.y | dimensions.z > MaxSize.z)
                return FitResult.TooLarge;
            return FitResult.Fits;
        }

        public FitResult CheckFit(VesselProject vessel) => CheckFit(vessel.GetTotalMass(), vessel.GetShipSize());

        public bool EnvelopeContains(double velocity, double altitude) =>
            velocity >= MinVelocity && velocity <= MaxVelocity && altitude >= MinAltitude && altitude <= MaxAltitude;

        /// <summary>
        /// Name of the PARTUPGRADE that has to be purchased in R&D to use this carrier.
        /// </summary>
        public string UpgradeName => "Airlaunch-" + name;

        public bool IsTechResearched => ResearchAndDevelopment.GetTechnologyState(TechRequired) == RDTech.State.Available;

        /// <summary>
        /// True if the carrier's upgrade has been purchased, or if there is no upgrade defined for it.
        /// </summary>
        public bool IsPurchased => PartUpgradeManager.Handler.GetUpgrade(UpgradeName) == null || PartUpgradeManager.Handler.IsUnlocked(UpgradeName);

        /// <summary>
        /// Usable for real launches: tech researched and upgrade purchased.
        /// </summary>
        public bool IsUnlocked => IsTechResearched && IsPurchased;

        public bool IsUnderResearch => SpaceCenterManagement.Instance.TechListHas(TechRequired);

        /// <summary>
        /// For sims: if researched/being researched
        /// Real: if unlocked
        /// </summary>
        public bool IsAvailable(bool forSim) => forSim ? IsTechResearched || IsUnderResearch : IsUnlocked;

        /// <summary>
        /// What the player has to do to get this carrier, e.g. "NB-52B (purchase in R&D)".
        /// </summary>
        public string UnlockHint => IsTechResearched ? $"{title} (purchase in R&D)" : $"{title} ({TechTitle})";

        /// <summary>
        /// Marker for carriers that can be simulated but not yet used for real launches.
        /// </summary>
        public string SimAvailabilityTag => IsUnlocked ? string.Empty : IsTechResearched ? " (not purchased)" : " (researching)";

        public static IReadOnlyList<AirlaunchTechLevel> All
        {
            get
            {
                EnsureLoaded();
                return _carriers;
            }
        }

        public static bool AnyUnlocked()
        {
            EnsureLoaded();
            return _carriers.Any(c => c.IsUnlocked);
        }

        public static bool AnyUnderResearch()
        {
            EnsureLoaded();
            return _carriers.Any(c => c.IsUnderResearch);
        }

        public static bool AnyTechResearched()
        {
            EnsureLoaded();
            return _carriers.Any(c => c.IsTechResearched);
        }

        /// <summary>
        /// Entry cost of the carrier's upgrade, including any unpurchased prerequisites from ENTRYCOSTMODS. Before strategies and unlock credit.
        /// </summary>
        public double PurchaseCost => RealFuels.EntryCostManager.Instance != null
            ? RealFuels.EntryCostManager.Instance.ConfigEntryCost(UpgradeName)
            : PartUpgradeManager.Handler.GetUpgrade(UpgradeName)?.entryCost ?? 0d;

        /// <summary>
        /// Buys the carrier's upgrade (and its ECM prerequisites), spending unlock credit first. Same flow as other upgrade purchases outside R&D.
        /// </summary>
        public bool Purchase()
        {
            PartUpgradeHandler.Upgrade up = PartUpgradeManager.Handler.GetUpgrade(UpgradeName);
            if (up == null || IsPurchased || !IsTechResearched || RealFuels.EntryCostManager.Instance == null)
                return false;

            Harmony.RFECMPatcher.techNode = up.techRequired;
            bool success = RealFuels.EntryCostManager.Instance.PurchaseConfig(up.name);
            Harmony.RFECMPatcher.techNode = null;
            if (success)
            {
                PartUpgradeManager.Handler.SetUnlocked(up.name, true);
                GameEvents.OnPartUpgradePurchased.Fire(up);
            }
            return success;
        }

        /// <summary>
        /// Names of all carriers superseded by the given ones, following Supersedes chains (B-50 -> B-29 etc.).
        /// </summary>
        private static HashSet<string> GetSuperseded(IEnumerable<AirlaunchTechLevel> carriers)
        {
            var superseded = new HashSet<string>();
            foreach (AirlaunchTechLevel c in carriers)
            {
                string s = c.Supersedes;
                while (!string.IsNullOrEmpty(s) && superseded.Add(s))
                    s = GetByName(s)?.Supersedes;
            }
            return superseded;
        }

        /// <summary>
        /// Available carriers in config order, without those superseded by another available carrier.
        /// </summary>
        public static List<AirlaunchTechLevel> GetAvailable(bool forSim)
        {
            EnsureLoaded();
            var available = _carriers.Where(c => c.IsAvailable(forSim)).ToList();
            var superseded = GetSuperseded(available);
            available.RemoveAll(c => superseded.Contains(c.name));
            return available;
        }

        /// <summary>
        /// Carriers whose tech is researched but whose upgrade isn't purchased yet, without those made redundant by an unlocked carrier.
        /// </summary>
        public static List<AirlaunchTechLevel> GetPurchasable()
        {
            EnsureLoaded();
            var superseded = GetSuperseded(_carriers.Where(c => c.IsUnlocked));
            return _carriers.Where(c => c.IsTechResearched && !c.IsPurchased && !superseded.Contains(c.name)).ToList();
        }

        public static AirlaunchTechLevel GetByName(string carrierName)
        {
            if (string.IsNullOrEmpty(carrierName))
                return null;
            EnsureLoaded();
            return _carriers.FirstOrDefault(c => c.name == carrierName);
        }

        /// <summary>
        /// First carrier in config order (regardless of unlock state) that can lift the vessel.
        /// </summary>
        public static AirlaunchTechLevel FirstFittingInConfig(VesselProject vessel)
        {
            EnsureLoaded();
            return _carriers.FirstOrDefault(c => c.CanLaunchVessel(vessel, out _));
        }

        /// <summary>
        /// Resolves the carrier for a stored name. Saves from before carriers existed have no name. If none existed, use the most capable unlocked carrier that fits or last unlocked one 
        /// </summary>
        public static AirlaunchTechLevel Resolve(string carrierName, VesselProject vessel)
        {
            var carrier = GetByName(carrierName);
            if (carrier != null)
                return carrier;

            var available = GetAvailable(false);
            return available.LastOrDefault(c => c.CanLaunchVessel(vessel, out _)) ?? available.LastOrDefault();
        }

        private static void EnsureLoaded()
        {
            if (_carriers == null)
            {
                _carriers = new List<AirlaunchTechLevel>();

                foreach (ConfigNode parentNode in GameDatabase.Instance.GetConfigNodes("KCTAIRLAUNCHTECHS"))
                {
                    foreach (ConfigNode n in parentNode.GetNodes("AIRLAUNCHTECHLEVEL"))
                    {
                        _carriers.Add(new AirlaunchTechLevel(n));
                    }
                }
            }
        }
    }
}
