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
