using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0.ProceduralAvionics
{
	[Serializable]
	class ProceduralAvionicsTechNode:IConfigNode
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
