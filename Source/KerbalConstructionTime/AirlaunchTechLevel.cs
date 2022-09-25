using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class AirlaunchTechLevel : IConfigNode
    {
        [Persistent]
        public string TechRequired;

        [Persistent]
        public double MinAltitude;

        [Persistent]
        public double MaxAltitude;

        [Persistent]
        public double MaxKscDistance;

        [Persistent]
        public double MinVelocity;

        [Persistent]
        public double MaxVelocity;

        [Persistent]
        public double MaxMass;

        [Persistent]
        public Vector3 MaxSize;

        private static List<AirlaunchTechLevel> _techLevels = null;

        public AirlaunchTechLevel() { }

        public AirlaunchTechLevel(ConfigNode n)
        {
            Load(n);
        }

        public override string ToString()
        {
            return $"{TechRequired};Alt:{MaxAltitude};Vel:{MaxVelocity}";
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }

        public bool CanLaunchVessel(BuildListVessel vessel, out string reason)
        {
            if (vessel == null)
            {
                reason = "No vessel";
                return false;
            }
            double mass = vessel.GetTotalMass();
            if (mass > MaxMass)
            {
                reason = $"mass ({mass:0.#}t) is higher than the allowed {MaxMass:0.#}";
                return false;
            }

            var template = new ShipTemplate();
            template.LoadShip(vessel.ShipNode);
            Vector3 dimensions = ShipConstruction.CalculateCraftSize(template); // Note: For a ShipTemplate, this just returns template.shipSize so is safe.
            if (dimensions.x > MaxSize.x | dimensions.y > MaxSize.y | dimensions.z > MaxSize.z)
            {
                reason = $"size ({dimensions.x:0.#} x {dimensions.y:0.#} x {dimensions.z:0.#} m) is more than the allowed {MaxSize.x:0.#} x {MaxSize.y:0.#} x {MaxSize.z:0.#} m";
                return false;
            }

            reason = null;
            return true;
        }

        public bool IsUnlocked => ResearchAndDevelopment.GetTechnologyState(TechRequired) == RDTech.State.Available;

        public bool IsUnderResearch => KCTGameStates.TechList.Any(tech => tech.TechID == TechRequired);

        public static bool AnyUnlocked()
        {
            return GetCurrentLevel() != null;
        }

        public static bool AnyUnderResearch()
        {
            EnsureLevelsLoaded();
            return _techLevels.Any(tl => KCTGameStates.TechList.Any(tech => tech.TechID == tl.TechRequired));
        }

        public static AirlaunchTechLevel GetCurrentLevel()
        {
            EnsureLevelsLoaded();
            for (int i = _techLevels.Count - 1; i >= 0; i--)
            {
                // Assume that levels are configured in the order of progression
                var level = _techLevels[i];
                if (level.IsUnlocked)
                {
                    return level;
                }
            }

            return null;
        }

        public static AirlaunchTechLevel GetHighestLevelIncludingUnderResearch()
        {
            EnsureLevelsLoaded();
            for (int i = _techLevels.Count - 1; i >= 0; i--)
            {
                // Assume that levels are configured in the order of progression
                var level = _techLevels[i];
                if (level.IsUnderResearch || level.IsUnlocked)
                {
                    return level;
                }
            }

            return null;
        }

        private static void EnsureLevelsLoaded()
        {
            if (_techLevels == null)
            {
                _techLevels = new List<AirlaunchTechLevel>();

                foreach (ConfigNode parentNode in GameDatabase.Instance.GetConfigNodes("KCTAIRLAUNCHTECHS"))
                {
                    foreach (ConfigNode n in parentNode.GetNodes("AIRLAUNCHTECHLEVEL"))
                    {
                        var lvl = new AirlaunchTechLevel(n);
                        _techLevels.Add(lvl);
                    }
                }
            }
        }
    }
}
