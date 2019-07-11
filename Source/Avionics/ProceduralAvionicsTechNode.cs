using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0.ProceduralAvionics
{
	[Serializable]
	public class ProceduralAvionicsTechNode : IConfigNode
	{
		[Persistent]
		public string name;

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

		// This is the service level of the SAS part module
		[Persistent]
		public int SASServiceLevel = 5;

		// Controls whether or not this part has a science return container 
		[Persistent]
		public bool hasScienceContainer = false;

        // is this capable of >LEO use?
        [Persistent]
        public bool interplanetary = true;

		public bool IsAvailable {
			get {
				return ResearchAndDevelopment.GetTechnologyState(name) == RDTech.State.Available;
			}
		}

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
