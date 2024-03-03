using ContractConfigurator;
using ContractConfigurator.Parameters;
using Contracts;
using ROUtils.DataTypes;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ROUtils;

namespace RP0.Milestones
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class MilestoneHandler : ScenarioModule
    {
        public static MilestoneHandler Instance { get; private set; }
        public static Dictionary<string, Milestone> ProgramToMilestone { get; private set; }
        public static Dictionary<string, Milestone> ContractToMilestone { get; private set; }
        public static Dictionary<string, Milestone> ContractParamToMilestone { get; private set; }
        public static Dictionary<string, Milestone> Milestones { get; private set; }

        private HashSet<string> _contractParamsCompletedThisFrame = new HashSet<string>();
        private int _tickCount = 0;
        private const int _TicksToStart = 3;

        [KSPField(isPersistant = true)]
        private PersistentHashSetValueType<string> seenMilestones = new PersistentHashSetValueType<string>();

        [KSPField(isPersistant = true)]
        private PersistentListValueType<string> queuedMilestones = new PersistentListValueType<string>();

        [KSPField(isPersistant = true)]
        private PersistentDictionaryValueTypeKey<string, PersistentListValueType<string>> milestoneData = new PersistentDictionaryValueTypeKey<string, PersistentListValueType<string>>();

        public override void OnAwake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;

            GameEvents.Contract.onCompleted.Add(OnContractComplete);
            GameEvents.Contract.onParameterChange.Add(OnContractParamChange);
            GameEvents.onCrewKilled.Add(OnCrewKilled);

            if (ContractToMilestone == null)
            {
                ContractToMilestone = new Dictionary<string, Milestone>();
                ContractParamToMilestone = new Dictionary<string, Milestone>();
                ProgramToMilestone = new Dictionary<string, Milestone>();
                Milestones = new Dictionary<string, Milestone>();

                foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("RP0_MILESTONE"))
                {
                    var m = new Milestone(n);
                    Milestones.Add(m.name, m);
                    if (!string.IsNullOrEmpty(m.contractName))
                        ContractToMilestone.Add(m.contractName, m);
                    if (!string.IsNullOrEmpty(m.programName))
                        ProgramToMilestone.Add(m.programName, m);
                    if (!string.IsNullOrEmpty(m.screenshotContractParamName))
                        ContractParamToMilestone.Add(m.screenshotContractParamName, m);
                }
            }
        }

        private void Update()
        {
            // ContractSystem takes a little extra time to wake up
            if (_tickCount < _TicksToStart)
            {
                ++_tickCount;
                return;
            }

            _contractParamsCompletedThisFrame.Clear();

            // If we have queued milestones, and we're not in a subscene, and there isn't one showing, try showing
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER && queuedMilestones.Count > 0 &&
                !KCT_GUI.InSCSubscene && !NewspaperUI.IsOpen)
            {
                TryCreateNewspaper();
            }
        }

        public void OnDestroy()
        {
            GameEvents.Contract.onCompleted.Remove(OnContractComplete);
            GameEvents.Contract.onParameterChange.Remove(OnContractParamChange);
            GameEvents.onCrewKilled.Remove(OnCrewKilled);
        }

        private void TryAddDate(string name)
        {
            if (!milestoneData.ContainsKey(name))
            {
                var list = new PersistentListValueType<string>();
                milestoneData.Add(name, list);
                list.Add(KSPUtil.PrintDate(Planetarium.GetUniversalTime(), false));
            }
        }

        private void AddVesselCrewData(string milestone, Vessel v)
        {
            if (v == null)
            {
                AddData(milestone, string.Empty);
                AddData(milestone, string.Empty);
                return;
            }

            var crew = v.GetVesselCrew();
            if (crew.Count > 0)
                AddData(milestone, crew[0].displayName);
            else
                AddData(milestone, string.Empty);

            AddData(milestone, v.vesselName);
        }

        /// <summary>
        /// Returns the index of the data added, or -1 if it did not add because
        /// there wasn't a date yet (or there was no data assigned at all)
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private int AddData(string name, string data)
        {
            if (!milestoneData.TryGetValue(name, out var list) || list.Count == 0)
                return -1; // can't add data before adding a date.

            list.Add(data);
            return list.Count - 1;
        }

        private void OnCrewKilled(EventReport evt)
        {
            const string crewKilledMilestoneName = "CrewKilledMilestone";
            if (Milestones.ContainsKey(crewKilledMilestoneName) && evt.eventType == FlightEvents.CREW_KILLED)
            {
                if (!queuedMilestones.Contains(crewKilledMilestoneName))
                {
                    queuedMilestones.Add(crewKilledMilestoneName);
                    TryAddDate(crewKilledMilestoneName);
                    // empty vessel and first-crewmember data
                    AddVesselCrewData(crewKilledMilestoneName, null);
                }
                AddData(crewKilledMilestoneName, HighLogic.CurrentGame.CrewRoster[evt.sender].displayName);
            }
        }

        public void OnProgramComplete(string name)
        {
            if (ProgramToMilestone.TryGetValue(name, out var milestone) && !seenMilestones.Contains(milestone.name))
            {
                seenMilestones.Add(milestone.name);
                queuedMilestones.Add(milestone.name);
                TryAddDate(milestone.name);
                // No vessel or crew
                AddVesselCrewData(milestone.name, null);
                // Add extra data here if desired
            }
        }

        private void OnContractComplete(Contract data)
        {
            if (data is ConfiguredContract cc)
                StartCoroutine(ContractCompleteRoutine(cc));
        }

        private void OnContractParamChange(Contract c, ContractParameter cp)
        {
            // Contract param can get the state changed to the same value more than once during a single frame
            if (cp.State == ParameterState.Complete && cp is ContractConfiguratorParameter ccp &&
                !_contractParamsCompletedThisFrame.Contains(ccp.ID))
            {
                _contractParamsCompletedThisFrame.Add(ccp.ID);
                StartCoroutine(ContractParamCompleteRoutine(ccp));
            }
        }

        private IEnumerator ContractCompleteRoutine(ConfiguredContract cc)
        {
            // The contract will only be seen as completed after the ContractSystem has run its next update
            // This will happen within 1 or 2 frames of the contract completion event getting fired.
            yield return null;
            yield return null;

            if (ContractToMilestone.TryGetValue(cc.contractType.name, out var milestone) && !seenMilestones.Contains(milestone.name))
            {
                seenMilestones.Add(milestone.name);
                queuedMilestones.Add(milestone.name);
                TryAddDate(milestone.name);
                AddVesselCrewData(milestone.name, FlightGlobals.ActiveVessel);
                // Add extra data here if desired

                if (!HighLogic.LoadedSceneIsFlight || SpaceCenterManagement.Instance.IsSimulatedFlight)
                    yield break;

                yield return CaptureScreenshot(milestone, overwrite: false);
            }
        }

        private IEnumerator ContractParamCompleteRoutine(ContractConfiguratorParameter ccp)
        {
            if (!HighLogic.LoadedSceneIsFlight || SpaceCenterManagement.Instance.IsSimulatedFlight ||
                !ContractParamToMilestone.TryGetValue(ccp.ID, out Milestone milestone))
            {
                yield break;
            }

            yield return CaptureScreenshot(milestone, overwrite: true);
        }

        private IEnumerator CaptureScreenshot(Milestone milestone, bool overwrite)
        {
            string filePath = $"{KSPUtil.ApplicationRootPath}/saves/{HighLogic.SaveFolder}/{milestone.name}.png";
            if (!overwrite && File.Exists(filePath))
                yield break;

            bool wasShowing = KSP.UI.UIMasterController.Instance.mainCanvas.enabled;
            if (wasShowing)
                GameEvents.onHideUI.Fire();

            float oldDist = FlightCamera.fetch.distance;
            float oldMin = FlightCamera.fetch.minDistance;

            FlightCamera.fetch.minDistance = 1f;
            Vector3 size = ShipConstruction.CalculateCraftSize(FlightGlobals.ActiveVessel.parts, FlightGlobals.ActiveVessel.rootPart);
            float newDist = KSPCameraUtil.GetDistanceToFit(size, FlightCamera.fetch.FieldOfView) * 1.1f + 1f;
            FlightCamera.fetch.SetDistanceImmediate(newDist);

            yield return new WaitForEndOfFrame();

            int width = Screen.width;
            int height = Screen.height;
            int desiredHeight = Mathf.CeilToInt(height / (float)width * 512f);

            // This works around a Unity 2019.3+ bug. See http://answers.unity.com/answers/1914706/view.html
            // Normally we'd use ScreenCapture.CaptureScreenAsTexture
            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            RenderTexture rt = new RenderTexture(512, desiredHeight, 0);
            RenderTexture.active = rt;
            Graphics.Blit(tex, rt);
            Texture2D result = new Texture2D(512, desiredHeight);
            result.ReadPixels(new Rect(0, 0, 512, desiredHeight), 0, 0);
            result.Apply();

            var bytes = ImageConversion.EncodeToPNG(result);
            File.WriteAllBytes(filePath, bytes);

            FlightCamera.fetch.minDistance = oldMin;
            FlightCamera.fetch.SetDistanceImmediate(oldDist);

            if (wasShowing)
                GameEvents.onShowUI.Fire();
        }

        public void TryCreateNewspaper()
        {
            if (queuedMilestones.Count > 0)
            {
                var m = Milestones[queuedMilestones.Pop()];
                NewspaperUI.ShowGUI(m, milestoneData.ValueOrDefault(m.name));
                if (m.canRequeue)
                    seenMilestones.Remove(m.name);
            }
        }
    }
}
