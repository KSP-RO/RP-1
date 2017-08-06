using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0.ProceduralAvionics
{
	[KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.SPACECENTER })]
	public class ProceduralAvionicsUnlockedTechSaver : ScenarioModule
	{
		private const string UNLOCKED_TECH_NODE_NAME = "ProceduralAvioncisUnlockedTech";
		private const string UNLOCKED_TECH_STATE = "UnlockedTechState";

		public override void OnSave(ConfigNode node)
		{
			ProceduralAvionicsUtils.Log("ScenarioModule onsave");
			ConfigNode n = new ConfigNode(UNLOCKED_TECH_NODE_NAME);
			n.AddValue(UNLOCKED_TECH_STATE, ProceduralAvionicsTechManager.GetUnlockedTechState());
			node.AddNode(n);
			ProceduralAvionicsUtils.Log("ScenarioModule calling base save");
			base.OnSave(node);
			ProceduralAvionicsUtils.Log("ScenarioModule save done");
		}

		public override void OnLoad(ConfigNode node)
		{
			ProceduralAvionicsUtils.Log("ScenarioModule onload");
			base.OnLoad(node);

			ConfigNode n = node.GetNode(UNLOCKED_TECH_NODE_NAME);

			string serialized = "";

			if (n != null) {
				string param = n.GetValue(UNLOCKED_TECH_STATE);
				if (param != null) {
					serialized = param;
				}
			}
			ProceduralAvionicsUtils.Log("setting unlocked state: ", serialized);
			ProceduralAvionicsTechManager.SetUnlockedTechState(serialized);
		}
	}
}
