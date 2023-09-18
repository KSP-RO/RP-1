using System.Collections.Generic;
using System.Linq;
using Contracts;
using ContractConfigurator.Parameters;
using RP0;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// Class for tracking contracts completed by a KCT vessel.
    /// </summary>
    [KSPScenario(ScenarioCreationOptions.AddToExistingCareerGames | ScenarioCreationOptions.AddToNewCareerGames,
        GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.SPACECENTER)]
    public class RP1ContractTracker : ScenarioModule
    {
        static public RP1ContractTracker Instance { get; private set; }
        [Persistent] public Dictionary<string, List<ContractInfo>> ContractTracker;

        public class ContractInfo
        {
            public string Name;
            public double CompletionTime;
        }


        public RP1ContractTracker()
        {
             if (ContractTracker == null)
             {
                ContractTracker = new Dictionary<string, List<ContractInfo>>();
             }
            Instance = this;
        }

        protected void Start()
        {
            GameEvents.Contract.onCompleted.Add(OnContractCompleted);
        }

        protected void OnDestroy()
        {
            GameEvents.Contract.onCompleted.Remove(OnContractCompleted);
        }

        protected void OnContractCompleted(Contract c)
        {
            if ((c is ConfiguredContract cc))  //note: Only vessels in VPGs are tracked currently
            {
                foreach (VesselParameterGroup vpg in cc.GetChildren().Where(x => x is VesselParameterGroup))
                {
                    Vessel trackedVessel = vpg.TrackedVessel;
                    if (trackedVessel != null)
                    {
                        if (!string.IsNullOrEmpty(cc.contractType.tag))
                            AddToContractTracker(trackedVessel, cc.contractType.tag);

                        AddToContractTracker(trackedVessel, cc.contractType.name);
                        CheckClosedVPGs(cc, trackedVessel);
                    }
                }
            }
        }

        private void CheckClosedVPGs(ConfiguredContract cc, Vessel trackedVessel)
        {
            foreach (ConfiguredContract activeContract in ConfiguredContract.ActiveContracts)
            {
                if (cc != activeContract)
                {
                    foreach (VesselParameterGroup vpg in activeContract.GetChildren().Where(x => x is VesselParameterGroup))
                    {
                        if (vpg.TrackedVessel.GetKCTVesselId() == trackedVessel.GetKCTVesselId())
                            CheckVPG(vpg);
                    }
                }
            }

        }

        private void AddToContractTracker(Vessel v, string contract)
        {
            List<ContractInfo> contracts;
            string vesselID = v.GetKCTVesselId();

            if (vesselID == null)
                return;

            ContractInfo item = new ContractInfo
            {
                Name = contract,
                CompletionTime = Planetarium.GetUniversalTime()
            };

            if (ContractTracker.TryGetValue(vesselID,out contracts))
            {
                contracts.Add(item);
            }
            else
            {
                contracts = new List<ContractInfo> { item };
                ContractTracker.Add(v.GetKCTVesselId(), contracts);
            }
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            ContractTracker.Clear();

            foreach (ConfigNode keyNode in node.GetNodes())
            {
                string keyName = keyNode.name;
                List<ContractInfo> contracts = new List<ContractInfo>();

                foreach (ConfigNode contractNode in keyNode.GetNodes("Contract"))
                {
                    ContractInfo contract = new ContractInfo();
                    contractNode.TryGetValue("Name", ref contract.Name);
                    contractNode.TryGetValue("CompletionTime", ref contract.CompletionTime);
                    contracts.Add(contract);
                }

                ContractTracker.Add(keyName, contracts);
            }
        }
        public override void OnSave(ConfigNode node)
        {
           base.OnSave(node);
           foreach (KeyValuePair<string,List<ContractInfo>> vesselID in ContractTracker)
            {
                ConfigNode keyNode = node.AddNode(vesselID.Key);
                foreach (ContractInfo contract in vesselID.Value)
                {
                    ConfigNode contractNode = keyNode.AddNode("Contract");
                    contractNode.AddValue("Name", contract.Name);
                    contractNode.AddValue("CompletionTime", contract.CompletionTime);
                }
            }
        }

        private void CheckVPG(VesselParameterGroup vpg)
        {
            if (vpg.TrackedVessel == null)
                return;

            foreach (HasCompleted param in vpg.GetAllDescendents().Where(x => x is HasCompleted))
            {
                param.Enable();
                param.CheckVPGVessel(vpg.TrackedVessel);

                if (!vpg.Enabled) {
                    vpg.Enable();
                    vpg.SetState(ParameterState.Incomplete);
                    vpg.Enable();   // Ugh, don't ask why
                    vpg.Reset();
                }
            }
        }
    }
}