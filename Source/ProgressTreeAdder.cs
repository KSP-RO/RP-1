using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    class ProgressTreeAdder : MonoBehaviour
    {
        public void OnAwake()
        {
            if ((object)ProgressTracking.Instance != null)
            {
                KSPAchievements.RecordsAltitudeProbe altitudeRecordProbe = new KSPAchievements.RecordsAltitudeProbe();
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
                Debug.Log("*RP-0* Added node " + node.Id);
            }
            else
                Debug.Log("*RP-0* Tree has node " + node.Id);
        }
    }
}
