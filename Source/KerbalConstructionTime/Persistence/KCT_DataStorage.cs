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
        [Persistent] public bool StarterLCSelected = false;
        [Persistent] public bool HiredStarterApplicants = false;
        [Persistent] public bool StartedProgram = false;
        [Persistent] public bool AcceptedContract = false;

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
            KCTGameStates.StarterLCBuilding = StarterLCSelected;
            KCTGameStates.HiredStarterApplicants = HiredStarterApplicants;
            KCTGameStates.StartedProgram = StartedProgram;
            KCTGameStates.AcceptedContract = AcceptedContract;

            if (KCTGameStates.LoadedSaveVersion < KCTGameStates.VERSION)
            {
                if (saveVersion < 1)
                {
                    KCTGameStates.LastEngineers *= 2;
                    KCTGameStates.LastResearchers *= 2;
                    KCTGameStates.UnassignedPersonnel *= 2;
                    KCTGameStates.Researchers *= 2;
                }
                if (saveVersion < 3)
                {
                    KCTGameStates.HiredStarterApplicants = true;
                    KCTGameStates.StartedProgram = true;
                }
                if (saveVersion < 5)
                {
                    KCTGameStates.AcceptedContract = true;
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
            StarterLCSelected = KCTGameStates.StarterLCBuilding;
            HiredStarterApplicants = KCTGameStates.HiredStarterApplicants;
            StartedProgram = KCTGameStates.StartedProgram;
            AcceptedContract = KCTGameStates.AcceptedContract;
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
