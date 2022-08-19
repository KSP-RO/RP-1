using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using System.Reflection;
using ContractConfigurator;
using Contracts;

namespace RP0.Milestones
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class MilestoneHandler : ScenarioModule
    {
        public static MilestoneHandler Instance { get; private set; }
        public static List<string> CompletedContracts { get; private set; }
        public static Dictionary<string, Milestone> MilestoneDict { get; private set; }

        public void OnDestroy()
        {
            GameEvents.Contract.onCompleted.Remove(OnContractComplete);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (CompletedContracts == null)
            {
                CompletedContracts = new List<string>();
            }

            if (MilestoneDict == null)
            {
                MilestoneDict = new Dictionary<string, Milestone>();

                foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("RP0_MILESTONE"))
                {
                    Milestone m = new Milestone(n);
                    MilestoneDict.Add(m.name, m);
                }
            }
        }

        private void Start()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;

            GameEvents.Contract.onCompleted.Add(OnContractComplete);

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                CreateNewspaper();
            }
        }

        private void OnContractComplete(Contract data)
        {
            ContractCompleteProcess(data);
        }

        private void ContractCompleteProcess(Contract data)
        {
            // Add to completed list
            if (data is ConfiguredContract cc)
            {
                CompletedContracts.Add(cc.contractType.name);
                MilestoneDict[cc.contractType.name].date = KSPUtils.GetUT();
            }
        }

        private void CreateNewspaper()
        {
            foreach (string str in CompletedContracts)
            {
                foreach (string mile in MilestoneDict.Keys)
                {
                    if (str == mile)
                    {
                        NewspaperUI.ShowGUI(MilestoneDict[mile]);
                    }
                }
            }
            CompletedContracts.Clear();
        }
    }
}
