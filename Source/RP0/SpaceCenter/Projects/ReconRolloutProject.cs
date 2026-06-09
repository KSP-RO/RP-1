using System;
using System.Collections.Generic;

namespace RP0
{
    public class ReconRolloutProject : LCOpsProject
    {
        public enum RolloutReconType
        {
            Reconditioning,
            Rollout,
            Rollback,
            Recovery,
            Refurbishment,
            None,
            AirlaunchMount,
            AirlaunchUnmount
        };

        public override string Name => KSP.Localization.Localizer.Format("#rp0_LCOps_Type_" + RRType.ToString());

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
                    case RolloutReconType.Refurbishment:
                        return TransactionReasonsRP0.VesselRefurbishment;
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
                    case RolloutReconType.Refurbishment:
                        return TransactionReasonsRP0.RateRefurbishment;
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
        /// Only used for reconditioning
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
                cost = Formula.GetReconditioningCost(vp);
                vesselBP = vp.buildPoints;
            }
            catch
            {
                RP0Debug.Log("Error while determining BP for recon_rollout");
            }
        }

        /// <summary>
        /// Called for everything but reconditioning
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
                    RP0Debug.LogWarning("Non-Reconditioning ReconRolloutProject called with Reconditioning");
                    BP = Formula.GetReconditioningBP(vessel);
                    cost = Formula.GetReconditioningCost(vessel);
                    break;

                case RolloutReconType.Rollout:
                    BP = Formula.GetRolloutBP(vessel);
                    cost = Formula.GetRolloutCost(vessel);
                    break;

                case RolloutReconType.Rollback:
                    BP = Formula.GetRolloutBP(vessel);
                    progress = BP; // starts complete, runs in reverse
                    break;

                case RolloutReconType.Recovery:
                    InitRecovery(vessel);
                    break;

                case RolloutReconType.Refurbishment:
                    BP = Formula.GetRefurbishmentBP(vessel);
                    cost = Formula.GetRefurbishmentCost(vessel);
                    break;

                case RolloutReconType.AirlaunchMount:
                    BP = Formula.GetAirlaunchBP(vessel);
                    cost = Formula.GetAirlaunchCost(vessel);
                    break;

                case RolloutReconType.AirlaunchUnmount:
                    BP = Formula.GetAirlaunchBP(vessel);
                    progress = BP; // starts complete, runs in reverse
                    break;
            }
        }

        /// <summary>
        /// Calculates recovery BP and cost.
        /// Base BP comes from vessel type (SPH/VAB).
        /// Transit time scales with distance from KSC.
        /// Transport mode (Air > Truck > Barge) is selected by vessel mass via
        /// RecoveryTechLevel.GetTransportLevel; its TransitRate divides the BP.
        /// Runway landings reduce transit time by 25%.
        /// Cost scales purely with distance — unaffected by transport mode or tech.
        /// </summary>
        private void InitRecovery(VesselProject vessel)
        {
            double baseBP = vessel.FacilityBuiltIn == EditorFacility.SPH
                ? Formula.GetRecoveryBPSPH(vessel)
                : Formula.GetRecoveryBPVAB(vessel);

            double maxDist = SpaceCenter.Instance.cb.Radius * Math.PI;
            double distanceFraction = vessel.kscDistance / maxDist; // 0..1

            // Transit time grows linearly with distance; doubles at maximum range
            BP = baseBP * (1d + distanceFraction);

            // Runway landings skip most of the transit overhead
            if (vessel.LandedAt?.Contains("Runway") ?? false)
                BP *= 0.75;

            // Select transport mode by mass and apply its transit rate.
            // vessel.GetTotalMass() returns tonnes; GetTransportLevel expects kg.
            var transport = RecoveryTechLevel.GetTransportLevel(vessel.GetTotalMass() * 1000d);
            BP /= transport.TransitRate;

            cost = Formula.GetRecoveryCost(vessel, distanceFraction);
        }

        /// <summary>
        /// Creates the follow-on Refurbishment project when Recovery completes.
        /// </summary>
        public ReconRolloutProject CreateFollowOnRefurbishment(VesselProject vessel)
        {
            if (RRType != RolloutReconType.Recovery)
                throw new InvalidOperationException("CreateFollowOnRefurbishment called on non-Recovery project");

            return new ReconRolloutProject(vessel, RolloutReconType.Refurbishment, associatedID, launchPadID);
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

        // Refurbishment, like reconditioning, does not hold up operations at the LC

        public override bool IsCapped =>
            RRType != RolloutReconType.Reconditioning &&
            RRType != RolloutReconType.Refurbishment;

        public override bool IsBlocking =>
            RRType != RolloutReconType.Reconditioning &&
            RRType != RolloutReconType.Refurbishment;

        public override bool IsReversed =>
            RRType == RolloutReconType.Rollback ||
            RRType == RolloutReconType.AirlaunchUnmount;

        public override bool HasCost =>
            RRType == RolloutReconType.Rollout ||
            RRType == RolloutReconType.AirlaunchMount ||
            RRType == RolloutReconType.Reconditioning ||
            RRType == RolloutReconType.Recovery ||
            RRType == RolloutReconType.Refurbishment;

        public override bool KeepsLCActive =>
            RRType != RolloutReconType.Reconditioning &&
            RRType != RolloutReconType.Refurbishment;

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

        protected override double CalculateBuildRate(int delta)
        {
            if (RRType == RolloutReconType.Reconditioning || RRType == RolloutReconType.Refurbishment)
            {
                bool isHRCapped = IsCapped && !isHumanRated && LC.IsHumanRated;
                return Formula.GetReconditioningBuildRate(LC, isHRCapped);
            }
            else
            {
                return base.CalculateBuildRate(delta);
            }
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