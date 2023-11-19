using System;
using System.Collections.Generic;

namespace RP0
{
    public class ReconRolloutProject : LCOpsProject
    {
        public enum RolloutReconType { Reconditioning, Rollout, Rollback, Recovery, None, AirlaunchMount, AirlaunchUnmount };

        public override string Name => KSP.Localization.Localizer.Format("#rp0_LCOps_Type_" + RRType.ToString())
            ;
        [Persistent]
        public string launchPadID = "LaunchPad";
        [Persistent]
        public RolloutReconType RRType = RolloutReconType.None;

        public override TransactionReasonsRP0 TransactionReason
        {
            get
            {
                switch (RRType)
                {
                    case RolloutReconType.Rollout:
                    case RolloutReconType.Rollback:
                        return TransactionReasonsRP0.RocketRollout;
                    case RolloutReconType.Recovery:
                        return TransactionReasonsRP0.VesselRecovery;
                    case RolloutReconType.Reconditioning:
                        return TransactionReasonsRP0.StructureRepair;
                    case RolloutReconType.AirlaunchMount:
                    case RolloutReconType.AirlaunchUnmount:
                        return TransactionReasonsRP0.AirLaunchRollout;

                    default:
                        return TransactionReasonsRP0.None;
                }
            }
        }

        protected override TransactionReasonsRP0 transactionReasonTime
        {
            get
            {
                switch (RRType)
                {
                    case RolloutReconType.Rollout:
                    case RolloutReconType.Rollback:
                        return TransactionReasonsRP0.RateRollout;
                    case RolloutReconType.Recovery:
                        return TransactionReasonsRP0.RateRecovery;
                    case RolloutReconType.Reconditioning:
                        return TransactionReasonsRP0.RateReconditioning;
                    case RolloutReconType.AirlaunchMount:
                    case RolloutReconType.AirlaunchUnmount:
                        return TransactionReasonsRP0.RateAirlaunch;

                    default:
                        return TransactionReasonsRP0.None;
                }
            }
        }

        public ReconRolloutProject() : base()
        {
        }

        /// <summary>
        /// Only used for non-airlaunch cases
        /// (either recovery or reconditioning)
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="launchSite"></param>
        /// <param name="lc"></param>
        public ReconRolloutProject(Vessel vessel, RolloutReconType type, string id, string launchSite, LaunchComplex lc)
        {
            RRType = type;
            associatedID = id;
            launchPadID = launchSite;
            RP0Debug.Log("New recon_rollout at launchsite: " + launchPadID);
            progress = 0;
            _lc = lc;

            mass = vessel.GetTotalMass();
            try
            {
                var vp = new VesselProject(vessel, ProjectType.VAB);
                isHumanRated = vp.humanRated;
                BP = Formula.GetReconditioningBP(vp);
                vesselBP = vp.buildPoints;
            }
            catch
            {
                RP0Debug.Log("Error while determining BP for recon_rollout");
            }
            if (type == RolloutReconType.Rollback)
                progress = BP;
            else if (type == RolloutReconType.Recovery)
            {
                double KSCDistance = (float)SpaceCenter.Instance.GreatCircleDistance(SpaceCenter.Instance.cb.GetRelSurfaceNVector(vessel.latitude, vessel.longitude));
                double maxDist = SpaceCenter.Instance.cb.Radius * Math.PI;
                BP += BP * (KSCDistance / maxDist);
            }
        }

        /// <summary>
        /// Called for everything but reconditioning and recovery
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="launchSite"></param>
        public ReconRolloutProject(VesselProject vessel, RolloutReconType type, string id, string launchSite = "")
        {
            RRType = type;
            associatedID = id;
            launchPadID = string.IsNullOrEmpty(launchSite) ? vessel.launchSite : launchSite;    //For when we add custom launchpads
            progress = 0;
            mass = vessel.GetTotalMass();
            _lc = vessel.LC;
            vesselBP = vessel.buildPoints;
            isHumanRated = vessel.humanRated;
            
            switch (type)
            {
                case RolloutReconType.Reconditioning:
                    BP = Formula.GetReconditioningBP(vessel);
                    break;

                case RolloutReconType.Rollout:
                case RolloutReconType.Rollback:
                    BP = Formula.GetRolloutBP(vessel);
                    break;

                case RolloutReconType.Recovery:
                    BP = vessel.FacilityBuiltIn == EditorFacility.SPH ? Formula.GetRecoveryBPSPH(vessel) : Formula.GetRecoveryBPVAB(vessel);
                    break;

                case RolloutReconType.AirlaunchMount:
                case RolloutReconType.AirlaunchUnmount:
                    BP = Formula.GetAirlaunchBP(vessel);
                    break;
            }

            if (type == RolloutReconType.Rollout)
                cost = Formula.GetRolloutCost(vessel);
            else if (type == RolloutReconType.AirlaunchMount)
                cost = Formula.GetAirlaunchCost(vessel);
            else if (type == RolloutReconType.Rollback || type == RolloutReconType.AirlaunchUnmount)
                progress = BP;
            else if (type == RolloutReconType.Recovery)
            {
                double maxDist = SpaceCenter.Instance.cb.Radius * Math.PI;
                BP += BP * (vessel.kscDistance / maxDist);
                BP *= (vessel.LandedAt?.Contains("Runway") ?? false) ? .75 : 1;
            } 
        }

        public void SwitchDirection()
        {
            switch (RRType)
            {
                case RolloutReconType.Rollout: RRType = RolloutReconType.Rollback; break;
                case RolloutReconType.Rollback: RRType = RolloutReconType.Rollout; break;
                case RolloutReconType.AirlaunchMount: RRType = RolloutReconType.AirlaunchUnmount; break;
                case RolloutReconType.AirlaunchUnmount: RRType = RolloutReconType.AirlaunchMount; break;
            }
            MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
        }

        public override bool IsCapped => RRType != RolloutReconType.Reconditioning;
        public override bool IsBlocking => RRType != RolloutReconType.Reconditioning;

        public override bool IsReversed => RRType == RolloutReconType.Rollback || RRType == RolloutReconType.AirlaunchUnmount;

        public override bool HasCost => RRType == RolloutReconType.Rollout || RRType == RolloutReconType.AirlaunchMount;

        public override ProjectType GetProjectType()
        {
            switch (RRType)
            {
                case RolloutReconType.AirlaunchMount:
                case RolloutReconType.AirlaunchUnmount:
                    return ProjectType.AirLaunch;

                default:
                    return ProjectType.Reconditioning;
            }
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
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
