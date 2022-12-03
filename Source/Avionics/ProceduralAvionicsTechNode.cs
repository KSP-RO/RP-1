using System;

namespace RP0.ProceduralAvionics
{
    [Serializable]
    public class ProceduralAvionicsTechNode : IConfigNode
    {
        [Persistent]
        public string name;

        [Persistent]
        public string dispName;

        [Persistent]
        public int techLevel;

        [Persistent]
        public float massExponent;

        [Persistent]
        public float massConstant;

        [Persistent]
        public float massFactor;

        [Persistent]
        public float shieldingMassFactor;

        [Persistent]
        public float costExponent;

        [Persistent]
        public float costConstant;

        [Persistent]
        public float costFactor;

        [Persistent]
        public float powerExponent;

        [Persistent]
        public float powerConstant;

        [Persistent]
        public float powerFactor;

        [Persistent]
        public float disabledPowerFactor = -1;

        [Persistent]
        public float avionicsDensity = 1;

        [Persistent]
        public float reservedRFTankVolume = 0.0015f;

        /// <summary>
        /// Controls whether or not this part has a science return container
        /// </summary>
        [Persistent]
        public bool hasScienceContainer = false;

        // is this capable of >LEO use?
        [Persistent]
        public bool interplanetary = true;

        /// <summary>
        /// Allow axial translation (for science cores)
        /// </summary>
        [Persistent]
        public bool allowAxial = false;

        [Persistent]
        public int kosDiskSpace = 500;

        [Persistent]
        public float kosSpaceCostFactor = 0.1f;

        [Persistent]
        public float kosSpaceMassFactor = 0.0001f;

        [Persistent]
        public float kosECPerInstruction = 0.000001f;

        public string TechNodeName => name;

        public string TechNodeTitle => ResearchAndDevelopment.GetTechnologyTitle(TechNodeName);

        public bool IsAvailable => ResearchAndDevelopment.GetTechnologyState(TechNodeName) == RDTech.State.Available;

        public bool IsScienceCore => massExponent == 0 && powerExponent == 0 && costExponent == 0;

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
            if (name == null)
            {
                name = node.GetValue("name");
            }
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
