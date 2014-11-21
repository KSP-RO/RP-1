using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;
using KSPAchievements;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToNewGames, new GameScenes[]
{
	GameScenes.FLIGHT,
	GameScenes.TRACKSTATION,
	GameScenes.SPACECENTER
})]
    class ProgressTreeAdder : ScenarioModule
    {
        public override void OnAwake()
        {
            base.OnAwake();
            if ((object)ProgressTracking.Instance != null)
            {
                RecordsAltitudeProbe altitudeRecordProbe = new KSPAchievements.RecordsAltitudeProbe();
                AddNode(altitudeRecordProbe);
            }
            else
                Debug.Log("*RP-0* Progress Tree was null! Scene " + HighLogic.LoadedScene);
        }
        public void AddNode(ProgressNode node)
        {
            if ((object)(ProgressTracking.Instance.achievementTree[node.Id]) == null)
            {
                ProgressTracking.Instance.achievementTree.AddNode(node);
                if (node.OnDeploy != null)
                    node.OnDeploy();
                Debug.Log("*RP-0* Added node " + node.Id);
            }
            else
                Debug.Log("*RP-0* Tree has node " + node.Id);
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }
    }
}
