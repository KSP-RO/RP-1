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

		// A higher number here is more efficient.  This is the number of controllable tons per ton of avionics mass at 50% of the maximum controllable tonnage.  
		// This actual ratio will be higher if closer to the maximum, and lower if farther away, however, as we get closer to our maximum, 
		// the price gets exponentionally higher, since we're trying to cram more controls in the same amount of space (at 50%, we are at 75% of the avionics mass 
		// we would be had we selected 100% of our maximum).
		// I consider 50% utilization to be the "sweet spot" of cost efficiency vs mass efficiency. 
		// (mass efficiency calculation = -x^2+2x, maxControllableTons = volume * avionicsMassPerVolume * tonnageToMassRatio * 2)
		[Persistent]
		public float tonnageToMassRatio = 1;

		// This is the cost per controlled ton, again, at 50% of our maximum controllable tonnage.  Obviously, lower is better here.
		// At 100% of the maximum, this value will actually be 4 times greater.
		// (cost efficiency calculation = x^2, maxCost = costPerControlledTon * maxControlledTons * 4);
		[Persistent]
		public float costPerControlledTon = 1;


		// Again, this is the rate at 50% capacity.  This rate changes linerally, at 0% utilization, 
		// the rate will be 0.5x,  while at 100%, the rate will be 1.5x 
		[Persistent]
		public float enabledkWPerTon = 1;

		// If postitive, this will enable the abilty to put avioniccs on standby.
		// Again, this is the rate at 50% capacity.  This rate changes linerally, at 0% utilization, 
		// the rate will be 0.5x,  while at 100%, the rate will be 1.5x 
		[Persistent]
		public float disabledkWPerTon = -1;

		// This is the overall density of this avionics unit at 50% utilization.  This can be tweaked to help match up historical avionics units with their proper sizes.
		[Persistent]
		public float standardAvionicsDensity = 1f;

		// This is the least amout of mass this avionics unit can be configured to control.  Set this to prevent unrealistic scaled down avionics units.
		[Persistent]
		public float minimumTonnage = 0;

		// This is the largest amout of mass this avionics unit can be configured to control.  Set this to prevent unrealistic supersized avionics units.
		[Persistent]
		public float maximumTonnage = float.MaxValue;

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
