
using System.Collections.Generic;
using System.Linq;
using System;
using Contracts;
using ContractConfigurator.Parameters;
using RP0;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// Parameter for checking that a vessel has not completed other contracts.
    /// </summary>
    public class HasCompleted : VesselParameter
    {
        protected List<string> ContractTags { get; set; }
        protected bool InvertParameter { get; set; }

        public HasCompleted()
            : base(null)
        {
        }

        public HasCompleted(List<string> contractTags, bool invertParameter, string title)
            : base(title)
        {
            this.ContractTags = contractTags;
            this.InvertParameter = invertParameter;
        }

        protected override string GetParameterTitle()
        {
            return string.IsNullOrEmpty(title) ? $"Has {(InvertParameter ? "Not " : "")}Completed Other Contracts" : title;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            foreach (string ct in ContractTags)
            {
                node.AddValue("contractTag", ct);
            }
            node.AddValue("invertParameter", InvertParameter);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            ContractTags = ConfigNodeUtil.ParseValue<List<string>>(node, "contractTag", new List<string>());
            InvertParameter = ConfigNodeUtil.ParseValue<bool>(node, "invertParameter");
        }

        protected override void OnRegister()
        {
            GameEvents.Contract.onCompleted.Add(OnContractCompleted);
            CheckTargetVessel();
            base.OnRegister();
        }

        protected override void OnUnregister()
        {
            GameEvents.Contract.onCompleted.Remove(OnContractCompleted);
            base.OnUnregister();
        }

        protected void OnContractCompleted(Contract c)
        {
            CheckTargetVessel();
        }

        private void CheckTargetVessel()
        {
            VesselParameterGroup vpg = InVPG(this);
            CheckVessel(vpg?.TrackedVessel ?? FlightGlobals.ActiveVessel);
        }

        public void CheckVPGVessel(Vessel v)
        {
            CheckVessel(v);
        }

        // Return enclosing VPG for this parmameter if it exists
        protected VesselParameterGroup InVPG(IContractParameterHost node)
        {
            if (node == Root)
                return null;
            if (node.Parent is VesselParameterGroup vpg)
                return vpg;
            else
                return InVPG(node.Parent);
        }

        /// <summary>
        /// Whether this vessel meets the parameter condition.
        /// </summary>
        /// <param name="vessel">The vessel to check</param>
        /// <returns>Whether the vessel meets the condition</returns>
        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            LoggingUtil.LogVerbose(this, "Checking VesselMeetsCondition: {0}", vessel.id);

            bool result = RP1ContractTracker.Instance.ContractTracker.TryGetValue(vessel.GetKCTVesselId(), out List<RP1ContractTracker.ContractInfo> contracts) && 
                          contracts.Select(x => x.Name).ToList().Intersect(ContractTags).Any();

            return InvertParameter ^ result;
        }
    }
}
