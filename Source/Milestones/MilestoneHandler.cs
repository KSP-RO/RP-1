using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using System.Reflection;
using ContractConfigurator;
using Contracts;
using RP0.DataTypes;
using System.Collections;

namespace RP0.Milestones
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class MilestoneHandler : ScenarioModule
    {
        public static MilestoneHandler Instance { get; private set; }
        public static Dictionary<string, Milestone> ProgramToMilestone { get; private set; }
        public static Dictionary<string, Milestone> ContractToMilestone { get; private set; }
        public static Dictionary<string, Milestone> Milestones { get; private set; }

        private int _tickCount = 0;
        private const int _TicksToStart = 3;

        [KSPField(isPersistant = true)]
        public PersistentHashSetValueType<string> seenMilestones = new PersistentHashSetValueType<string>();

        [KSPField(isPersistant = true)]
        public PersistentListValueType<string> queuedMilestones = new PersistentListValueType<string>();

        public override void OnAwake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;

            GameEvents.Contract.onCompleted.Add(OnContractComplete);

            if (ContractToMilestone == null)
            {
                ContractToMilestone = new Dictionary<string, Milestone>();
                ProgramToMilestone = new Dictionary<string, Milestone>();
                Milestones = new Dictionary<string, Milestone>();

                foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("RP0_MILESTONE"))
                {
                    Milestone m = new Milestone(n);
                    Milestones.Add(m.name, m);
                    if (!string.IsNullOrEmpty(m.contractName))
                        ContractToMilestone.Add(m.contractName, m);
                    if (!string.IsNullOrEmpty(m.programName))
                        ProgramToMilestone.Add(m.programName, m);
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

            // If we have queued milestones, and we're not in a subscene, and there isn't one showing, try showing
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER && queuedMilestones.Count > 0 && !KerbalConstructionTime.KCT_GUI.InSCSubscene && !NewspaperUI.IsOpen)
            {
                TryCreateNewspaper();
            }
        }

        public void OnDestroy()
        {
            GameEvents.Contract.onCompleted.Remove(OnContractComplete);
        }

        public void OnProgramComplete(string name)
        {
            if (ProgramToMilestone.TryGetValue(name, out var milestone) && !seenMilestones.Contains(milestone.name))
            {
                seenMilestones.Add(milestone.name);
                queuedMilestones.Add(milestone.name);
            }
        }

        private void OnContractComplete(Contract data)
        {
            if(data is ConfiguredContract cc)
                StartCoroutine(ContractCompleteRoutine(cc));
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
                if (!HighLogic.LoadedSceneIsFlight)
                    yield break;

                bool wasShowing = KSP.UI.UIMasterController.Instance.mainCanvas.enabled;
                if (wasShowing)
                    GameEvents.onHideUI.Fire();

                string filePath = $"{KSPUtil.ApplicationRootPath}/saves/{HighLogic.SaveFolder}/{milestone.name}.png";

                float oldDist = FlightCamera.fetch.distance;

                Vector3 vector = ShipConstruction.CalculateCraftSize(FlightGlobals.ActiveVessel.parts, FlightGlobals.ActiveVessel.rootPart);
                float newDist = KSPCameraUtil.GetDistanceToFit(vector, FlightCamera.fetch.FieldOfView) * FlightCamera.fetch.vesselSwitchBackoffFOVFactor + FlightCamera.fetch.vesselSwitchBackoffPadding;
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
                System.IO.File.WriteAllBytes(filePath, bytes);

                FlightCamera.fetch.SetDistanceImmediate(oldDist);

                if (wasShowing)
                    GameEvents.onShowUI.Fire();
            }

        }

        public void TryCreateNewspaper()
        {
            if (queuedMilestones.Count > 0)
            {
                string mName = queuedMilestones[0];
                queuedMilestones.RemoveAt(0);
                NewspaperUI.ShowGUI(Milestones[mName]);
            }
        }
    }
}
