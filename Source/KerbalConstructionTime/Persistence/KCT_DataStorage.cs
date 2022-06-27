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
        [Persistent] public int Researchers;
        [Persistent] public int UnassignedPersonnel;
        [Persistent] public double EfficiencyResearchers = 0.25d;
        [Persistent] public double EfficiencyEngineers = 0.25d;
        [Persistent] public double LastEngineers = 0d;
        [Persistent] public double LastResearchers = 0d;

        [Persistent] public int saveVersion;

        public override void OnDecodeFromConfigNode()
        {
            KCTGameStates.ActiveKSCName = activeKSC;
            KCTGameStates.SciPointsTotal = SciPoints;
            KCTGameStates.IsSimulatedFlight = IsSimulation;
            KCTGameStates.SimulationParams.DisableFailures = DisableFailuresInSim;
            KCTGameStates.Researchers = Researchers;
            KCTGameStates.EfficiencyResearchers = EfficiencyResearchers;
            KCTGameStates.EfficiencyEngineers = EfficiencyEngineers;
            KCTGameStates.UnassignedPersonnel = UnassignedPersonnel;
            KCTGameStates.LastEngineers = LastEngineers;
            KCTGameStates.LastResearchers = LastResearchers;
            KCTGameStates.LoadedSaveVersion = saveVersion;

            if (saveVersion < KCTGameStates.VERSION)
            {
                if (saveVersion < 1)
                {
                    KCTGameStates.LastEngineers *= 2;
                    KCTGameStates.LastResearchers *= 2;
                    KCTGameStates.UnassignedPersonnel *= 2;
                    KCTGameStates.Researchers *= 2;
                }
            }
        }

        public override void OnEncodeToConfigNode()
        {
            SciPoints = KCTGameStates.SciPointsTotal;
            activeKSC = KCTGameStates.ActiveKSC.KSCName;
            IsSimulation = KCTGameStates.IsSimulatedFlight;
            DisableFailuresInSim = KCTGameStates.SimulationParams.DisableFailures;
            Researchers = KCTGameStates.Researchers;
            UnassignedPersonnel = KCTGameStates.UnassignedPersonnel;
            EfficiencyResearchers = KCTGameStates.EfficiencyResearchers;
            EfficiencyEngineers = KCTGameStates.EfficiencyEngineers;
            LastResearchers = KCTGameStates.LastResearchers;
            LastEngineers = KCTGameStates.LastEngineers;
            saveVersion = KCTGameStates.VERSION;
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
