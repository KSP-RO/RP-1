using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP;

namespace ClearInputLocks.RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class ClearInputLocks : ScenarioModule
    {
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7))
            {
                InputLockManager.ClearControlLocks();
                // Debug.Log("Input Locks Cleared");
            }
        }
    }        
}
