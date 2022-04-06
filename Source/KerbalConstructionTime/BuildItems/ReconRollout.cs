using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalConstructionTime
{
    public class ReconRollout : LCProject
    {
        public override string Name => RRInverseDict[RRType];
        public string LaunchPadID = "LaunchPad";
        public const string ReconditioningStr = "Reconditioning";
        public const string RolloutStr = "Rollout";
        public const string RollbackStr = "Rollback";
        public const string RecoveryStr = "Recover";
        public const string UnknownStr = "Unknown Situation";

        public enum RolloutReconType { Reconditioning, Rollout, Rollback, Recovery, None };
        public RolloutReconType RRType = RolloutReconType.None;

        public static Dictionary<string, RolloutReconType> RRDict = new Dictionary<string, RolloutReconType>()
        {
            {  ReconditioningStr, RolloutReconType.Reconditioning },
            {  RolloutStr, RolloutReconType.Rollout },
            {  RollbackStr, RolloutReconType.Rollback },
            {  RecoveryStr, RolloutReconType.Recovery },
            {  UnknownStr, RolloutReconType.None }
        };
        public static Dictionary<RolloutReconType, string> RRInverseDict = new Dictionary<RolloutReconType, string>()
        {
            {  RolloutReconType.Reconditioning, ReconditioningStr },
            {  RolloutReconType.Rollout, RolloutStr },
            {  RolloutReconType.Rollback, RollbackStr },
            {  RolloutReconType.Recovery, RecoveryStr },
            {  RolloutReconType.None, UnknownStr }
        };

        public ReconRollout() : base()
        {
            RRType = RolloutReconType.None;
            LaunchPadID = "LaunchPad";
        }

        public ReconRollout(Vessel vessel, RolloutReconType type, string id, string launchSite, LCItem lc)
        {
            RRType = type;
            AssociatedID = id;
            LaunchPadID = launchSite;
            KCTDebug.Log("New recon_rollout at launchsite: " + LaunchPadID);
            Progress = 0;
            _lc = lc;

            Mass = vessel.GetTotalMass();
            try
            {
                var blv = new BuildListVessel(vessel);
                IsHumanRated = blv.IsHumanRated;
                BP = MathParser.ParseReconditioningFormula(blv, true);
                VesselBP = blv.BuildPoints + blv.IntegrationPoints;
            }
            catch
            {
                KCTDebug.Log("Error while determining BP for recon_rollout");
            }
            if (type == RolloutReconType.Rollback)
                Progress = BP;
            else if (type == RolloutReconType.Recovery)
            {
                double KSCDistance = (float)SpaceCenter.Instance.GreatCircleDistance(SpaceCenter.Instance.cb.GetRelSurfaceNVector(vessel.latitude, vessel.longitude));
                double maxDist = SpaceCenter.Instance.cb.Radius * Math.PI;
                BP += BP * (KSCDistance / maxDist);
            }
        }
        public ReconRollout(BuildListVessel vessel, RolloutReconType type, string id, string launchSite = "")
        {
            RRType = type;
            AssociatedID = id;
            LaunchPadID = string.IsNullOrEmpty(launchSite) ? vessel.LaunchSite : launchSite;    //For when we add custom launchpads
            Progress = 0;
            Mass = vessel.GetTotalMass();
            _lc = vessel.LC;
            VesselBP = vessel.BuildPoints + vessel.IntegrationPoints;
            IsHumanRated = vessel.IsHumanRated;
            BP = MathParser.ParseReconditioningFormula(vessel, type == RolloutReconType.Reconditioning);

            if (type == RolloutReconType.Rollout)
                Cost = MathParser.ParseRolloutCostFormula(vessel);
            else if (type == RolloutReconType.Rollback)
                Progress = BP;
            else if (type == RolloutReconType.Recovery)
            {
                double maxDist = SpaceCenter.Instance.cb.Radius * Math.PI;
                BP += BP * (vessel.DistanceFromKSC / maxDist);
            }
        }

        public void SwapRolloutType()
        {
            if (RRType == RolloutReconType.Rollout)
                RRType = RolloutReconType.Rollback;
            else if (RRType == RolloutReconType.Rollback)
                RRType = RolloutReconType.Rollout;
        }

        public override bool IsCapped => RRType != RolloutReconType.Reconditioning;

        public override bool IsReversed => RRType == RolloutReconType.Rollback;

        public override bool HasCost => RRType == RolloutReconType.Rollout;

        public override BuildListVessel.ListType GetListType() => BuildListVessel.ListType.Reconditioning;
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
