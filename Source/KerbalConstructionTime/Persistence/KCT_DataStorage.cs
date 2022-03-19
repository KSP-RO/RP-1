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

        [Persistent] public List<string> PartTracker = new List<string>();
        [Persistent] public List<string> PartInventory = new List<string>();
        [Persistent] public string activeKSC = string.Empty;
        [Persistent] public float SciPoints = -1f;
        [Persistent] public bool IsSimulation;
        [Persistent] public bool DisableFailuresInSim = true;
        [Persistent] public int RDPersonnel;
        [Persistent] public double EfficiencyRDPersonnel = 1d;

        public override void OnDecodeFromConfigNode()
        {
            KCTGameStates.ActiveKSCName = activeKSC;
            KCTGameStates.SciPointsTotal = SciPoints;
            KCTGameStates.IsSimulatedFlight = IsSimulation;
            KCTGameStates.SimulationParams.DisableFailures = DisableFailuresInSim;
            KCTGameStates.RDPersonnel = RDPersonnel;
            KCTGameStates.EfficiencyRDPersonnel = EfficiencyRDPersonnel;
        }

        public override void OnEncodeToConfigNode()
        {
            SciPoints = KCTGameStates.SciPointsTotal;
            activeKSC = KCTGameStates.ActiveKSC.KSCName;
            IsSimulation = KCTGameStates.IsSimulatedFlight;
            DisableFailuresInSim = KCTGameStates.SimulationParams.DisableFailures;
            RDPersonnel = KCTGameStates.RDPersonnel;
            EfficiencyRDPersonnel = KCTGameStates.EfficiencyRDPersonnel;
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
