using System;
using System.Collections.Generic;
using UniLinq;
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

            var unlockedTechNode = node.GetNode(UNLOCKED_TECH_NODE_NAME);

            string serialized = "";

            if (unlockedTechNode != null) {
                string param = unlockedTechNode.GetValue(UNLOCKED_TECH_STATE);
                if (param != null) {
                    serialized = param;
                }
            }
            ProceduralAvionicsUtils.Log("setting unlocked state: ", serialized);
            ProceduralAvionicsTechManager.SetUnlockedTechState(serialized);
        }
    }
}
