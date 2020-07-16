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

        // Controls whether or not this part has a science return container 
        [Persistent]
        public bool hasScienceContainer = false;

        // is this capable of >LEO use?
        [Persistent]
        public bool interplanetary = true;

        public bool IsAvailable => ResearchAndDevelopment.GetTechnologyState(name) == RDTech.State.Available;

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
            if (name == null) {
                name = node.GetValue("name");
            }
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
