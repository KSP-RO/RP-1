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

		[Persistent]
		public float tonnageToMassRatio = 10; // default is 10 tons of control to one ton of part mass.  A higher number is more efficient.

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
