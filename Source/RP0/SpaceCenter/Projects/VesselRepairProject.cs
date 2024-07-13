using RealFuels;

namespace RP0
{
    public class VesselRepairProject : LCOpsProject
    {
        public override string Name => "Repair vessel";

        [Persistent]
        public string launchPadID = "LaunchPad";
        [Persistent]
        public string shipName;

        public override TransactionReasonsRP0 TransactionReason => TransactionReasonsRP0.RocketRollout;

        protected override TransactionReasonsRP0 transactionReasonTime => TransactionReasonsRP0.RateRollout;

        public override bool KeepsLCActive => false;

        public override ProjectType GetProjectType() => ProjectType.VesselRepair;

        public VesselRepairProject() : base()
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="launchSite"></param>
        /// <param name="lc"></param>
        public VesselRepairProject(Vessel vessel, string launchSite, LaunchComplex lc)
        {
            associatedID = vessel.id.ToString();
            launchPadID = launchSite;
            RP0Debug.Log("New VesselRepair at launchsite: " + launchPadID);
            progress = 0;
            _lc = lc;

            mass = vessel.GetTotalMass();
            shipName = vessel.vesselName;
            try
            {
                var vp = new VesselProject(vessel, ProjectType.VAB);
                isHumanRated = vp.humanRated;
                BP = Formula.GetVesselRepairBP(vp);
                vesselBP = vp.buildPoints;
            }
            catch
            {
                RP0Debug.Log("Error while determining BP for VesselRepair");
            }
        }

        public bool ApplyRepairs()
        {
            Vessel v = FlightGlobals.FindVessel(AssociatedIdAsGuid);
            if (v != null)
            {
                if (!v.loaded) return false;

                v.ctrlState.mainThrottle = 0;
                TFInterop.ResetAllFailures(v);
                SpaceCenterManagement.Instance.DoingVesselRepair = true;

                try
                {
                    ResetIgnitionsOnVessel(v);
                }
                catch
                {
                    // Might be using older RF version that is missing the ResetIgnitions method
                }

                return true;
            }
            else
            {
                RP0Debug.LogError($"Failed to find vessel {associatedID} to reset failures on");
                return false;
            }
        }

        private static void ResetIgnitionsOnVessel(Vessel v)
        {
            foreach (var merf in v.FindPartModulesImplementing<ModuleEnginesRF>())
            {
                merf.ResetIgnitions();
            }
        }
    }
}
