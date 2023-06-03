using System.Collections.Generic;
using System.Linq;
using Contracts;
using ContractConfigurator.Parameters;
using KerbalConstructionTime;

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
        [Persistent] public Dictionary<string, List<string>> ContractTracker;

        public RP1ContractTracker()
        {
             if (ContractTracker == null)
             {
                ContractTracker = new Dictionary<string, List<string>>();
             //ContractTracker.Add("Testor", new List<string> { "Nestor" });
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
            if (!(c is ConfiguredContract cc))
                return;

            foreach (VesselParameterGroup vpg in cc.GetChildren().Where(x => x is VesselParameterGroup))
            {
                Vessel trackedVessel = vpg.TrackedVessel;
                if (trackedVessel != null)
                {
                    string contractName = cc.contractType.tag ?? cc.contractType.name; 
                    AddToContractTracker(trackedVessel, contractName);
                    CheckClosedVPGs(cc, trackedVessel);
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
            List<string> contracts;
            string vesselID = v.GetKCTVesselId();

            if (vesselID == null)
                return;

            if (ContractTracker.TryGetValue(vesselID,out contracts))
            {
                contracts.Add(contract);
            }
            else
            {
                contracts = new List<string> { contract };
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
                List<string> contracts = new List<string>();

                foreach (string value in keyNode.GetValues("Contract"))
                {
                    contracts.Add(value);
                }

                ContractTracker.Add(keyName, contracts);
            }
        }
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
           foreach (KeyValuePair<string,List<string>> item in ContractTracker)
            {
                ConfigNode keyNode = node.AddNode(item.Key);
                foreach (string contract in item.Value)
                {
                    keyNode.AddValue("Contract", contract);
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