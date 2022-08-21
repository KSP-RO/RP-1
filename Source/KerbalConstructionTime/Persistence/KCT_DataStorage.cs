using System.Collections.Generic;

namespace KerbalConstructionTime
{
    /// <summary>
    /// This type is used for serializing data to 'KerbalConstructionTimeData' scenario.
    /// Do not rename!
    /// </summary>
    public class KCT_DataStorage
    {
        [Persistent]
        public bool enabledForSave = HighLogic.CurrentGame.Mode == Game.Modes.CAREER ||
                                     HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX ||
                                     HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX;

        [Persistent] public string activeKSC = string.Empty;
        [Persistent] public float SciPoints = -1f;
        [Persistent] public bool IsSimulation;
        [Persistent] public bool DisableFailuresInSim = true;
        [Persistent] public int Researchers;
        [Persistent] public int UnassignedPersonnel;
        [Persistent] public bool StarterLCSelected = false;
        [Persistent] public bool HiredStarterApplicants = false;
        [Persistent] public bool StartedProgram = false;
        [Persistent] public bool AcceptedContract = false;

        [Persistent] public int saveVersion;

        public void ReadFields()
        {
            KerbalConstructionTimeData.Instance.ActiveKSCName = activeKSC;
            KerbalConstructionTimeData.Instance.SciPointsTotal = SciPoints;
            KerbalConstructionTimeData.Instance.IsSimulatedFlight = IsSimulation;
            KerbalConstructionTimeData.Instance.SimulationParams.DisableFailures = DisableFailuresInSim;
            KerbalConstructionTimeData.Instance.Researchers = Researchers;
            KerbalConstructionTimeData.Instance.Applicants = UnassignedPersonnel;
            KerbalConstructionTimeData.Instance.LoadedSaveVersion = saveVersion;
            KerbalConstructionTimeData.Instance.StarterLCBuilding = StarterLCSelected;
            KerbalConstructionTimeData.Instance.HiredStarterApplicants = HiredStarterApplicants;
            KerbalConstructionTimeData.Instance.StartedProgram = StartedProgram;
            KerbalConstructionTimeData.Instance.AcceptedContract = AcceptedContract;

            if (KerbalConstructionTimeData.Instance.LoadedSaveVersion < KCTGameStates.VERSION)
            {
                if (saveVersion < 1)
                {
                    KerbalConstructionTimeData.Instance.Applicants *= 2;
                    KerbalConstructionTimeData.Instance.Researchers *= 2;
                }
                if (saveVersion < 3)
                {
                    KerbalConstructionTimeData.Instance.HiredStarterApplicants = true;
                    KerbalConstructionTimeData.Instance.StartedProgram = true;
                }
                if (saveVersion < 5)
                {
                    KerbalConstructionTimeData.Instance.AcceptedContract = true;
                }
            }
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
