using ContractConfigurator.Parameters;
using Contracts;
using RP0;
using System.Collections.Generic;
using System.Linq;

namespace ContractConfigurator.RP0
{
    /// <summary>
    /// Parameter for enforcing no docking between vessels.
    /// </summary>
    public class RP1NoDocking : VesselParameter
    {
        protected HashSet<string> dockedVesselIDs { get; set; } = new HashSet<string>();
        protected List<string> vessels { get; set; }

        public RP1NoDocking()
            : base(null)
        {
        }

        public RP1NoDocking(bool failContract, IEnumerable<string> vessels, string title)
            : base(title)
        {
            this.vessels = vessels.ToList();
            this.title = title;

            failWhenUnmet = true;
            fakeFailures = !failContract;
            disableOnStateChange = false;

            state = ParameterState.Complete;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            foreach (string vessel in vessels)
            {
                node.AddValue("vessel", vessel);
            }

            ConfigNode dockedNode = node.AddNode("DOCKED_VESSELS");
            foreach (string vId in dockedVesselIDs)
            {
                dockedNode.AddValue("vessel", vId);
            }
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            vessels = ConfigNodeUtil.ParseValue(node, "vessel", new List<string>());
            dockedVesselIDs = new HashSet<string>(ConfigNodeUtil.ParseValue(node.GetNode("DOCKED_VESSELS"), "vessel", new List<string>()));
        }

        protected override void OnPartAttach(GameEvents.HostTargetAction<Part, Part> e)
        {
            base.OnPartAttach(e);

            if (HighLogic.LoadedScene == GameScenes.EDITOR || e.host.vessel == null || e.target.vessel == null)
            {
                return;
            }

            LoggingUtil.LogVerbose(this, "OnPartAttach");
            Vessel v1, v2;
            if (Parent is VesselParameterGroup vpg)
            {
                v1 = vpg.TrackedVessel ?? FlightGlobals.ActiveVessel;
                v2 = vessels.Count > 0 ? ContractVesselTracker.Instance.GetAssociatedVessel(vessels[0]) : null;

                // No vessel association
                if (vessels.Count > 0 && v2 == null)
                {
                    return;
                }
            }
            else
            {
                v1 = ContractVesselTracker.Instance.GetAssociatedVessel(vessels[0]);
                v2 = vessels.Count > 1 ? ContractVesselTracker.Instance.GetAssociatedVessel(vessels[1]) : null;

                // No vessel association
                if (v1 == null || vessels.Count > 1 && v2 == null)
                {
                    return;
                }
            }

            LoggingUtil.LogVerbose(this, "v1 = {0}", (v1 == null ? "null" : v1.id.ToString()));
            LoggingUtil.LogVerbose(this, "v2 = {1}", (v2 == null ? "null" : v2.id.ToString()));
            LoggingUtil.LogVerbose(this, "e.host.vessel = {0}", e.host.vessel.id.ToString());
            LoggingUtil.LogVerbose(this, "e.target.vessel = {0}", e.target.vessel.id.ToString());

            // Check for match
            bool forceStateChange = false;
            if (e.host.vessel == (v1 ?? e.host.vessel) && e.target.vessel == (v2 ?? e.target.vessel) ||
                e.host.vessel == (v2 ?? e.host.vessel) && e.target.vessel == (v1 ?? e.target.vessel))
            {
                dockedVesselIDs.Add(e.host.vessel.GetKCTVesselId());
                dockedVesselIDs.Add(e.target.vessel.GetKCTVesselId());
            }

            CheckVessel(e.host.vessel, forceStateChange);
            CheckVessel(e.target.vessel, forceStateChange);
        }

        /// <summary>
        /// Whether this vessel meets the parameter condition.
        /// </summary>
        /// <param name="vessel">The vessel to check</param>
        /// <returns>Whether the vessel meets the condition</returns>
        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            LoggingUtil.LogVerbose(this, "Checking VesselMeetsCondition: {0}", vessel.id);
            string vId = vessel.GetKCTVesselId();
            return !dockedVesselIDs.Contains(vId);
        }
    }
}
