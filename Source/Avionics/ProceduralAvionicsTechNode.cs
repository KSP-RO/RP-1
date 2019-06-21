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
		public float massExponent = 1;

        [Persistent]
        public float massConstant = 1;

        [Persistent]
        public float massFactor = 1;

        [Persistent]
        public float shieldingMassFactor = 0;

        [Persistent]
        public float costExponent = 1;

        [Persistent]
        public float costConstant = 1;

        [Persistent]
        public float costFactor = 1;

        [Persistent]
        public float powerExponent = 1;

        [Persistent]
        public float powerConstant = 1;

        [Persistent]
        public float powerFactor = 1;

		[Persistent]
		public float disabledPowerFactor = -1;

		[Persistent]
		public float avionicsDensity = 1f;

		// This is the service level of the SAS part module
		[Persistent]
		public int SASServiceLevel = 0;

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
