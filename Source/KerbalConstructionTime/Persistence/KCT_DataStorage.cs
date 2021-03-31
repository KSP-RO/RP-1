using System.Collections.Generic;

namespace KerbalConstructionTime
{
    /// <summary>
    /// This type is used for serializing data to 'KerbalConstructionTimeData' scenario.
    /// Do not rename!
    /// </summary>
    public class KCT_DataStorage : ConfigNodeStorage
    {
        [Persistent]
        public bool enabledForSave = HighLogic.CurrentGame.Mode == Game.Modes.CAREER ||
                                     HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX ||
                                     HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX;

        [Persistent] public List<int> VABUpgrades = new List<int>() {0};
        [Persistent] public List<int> SPHUpgrades = new List<int>() {0};
        [Persistent] public List<int> RDUpgrades = new List<int>() {0,0};
        [Persistent] public List<int> PurchasedUpgrades = new List<int>() {0,0};
        [Persistent] public List<string> PartTracker = new List<string>();
        [Persistent] public List<string> PartInventory = new List<string>();
        [Persistent] public string activeKSC = string.Empty;
        [Persistent] public float SciPoints = -1f;
        [Persistent] public int UpgradesResetCounter = 0, TechUpgrades = 0;
        [Persistent] public bool IsSimulation;
        [Persistent] public bool DisableFailuresInSim = true;

        public override void OnDecodeFromConfigNode()
        {
            KCTGameStates.PurchasedUpgrades = PurchasedUpgrades;
            KCTGameStates.ActiveKSCName = activeKSC;
            KCTGameStates.UpgradesResetCounter = UpgradesResetCounter;
            KCTGameStates.TechUpgradesTotal = TechUpgrades;
            KCTGameStates.SciPointsTotal = SciPoints;
            KCTGameStates.IsSimulatedFlight = IsSimulation;
            KCTGameStates.SimulationParams.DisableFailures = DisableFailuresInSim;
        }

        public override void OnEncodeToConfigNode()
        {
            TechUpgrades = KCTGameStates.TechUpgradesTotal;
            PurchasedUpgrades = KCTGameStates.PurchasedUpgrades;
            SciPoints = KCTGameStates.SciPointsTotal;
            activeKSC = KCTGameStates.ActiveKSC.KSCName;
            UpgradesResetCounter = KCTGameStates.UpgradesResetCounter;
            IsSimulation = KCTGameStates.IsSimulatedFlight;
            DisableFailuresInSim = KCTGameStates.SimulationParams.DisableFailures;
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
