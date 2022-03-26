using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalConstructionTime
{
    public class ReconRollout : IKCTBuildItem
    {
        public string Name => RRInverseDict[RRType];
        public double BP = 0, Progress = 0, Cost = 0, Mass = 0, VesselBP;
        public string AssociatedID = string.Empty;
        public string LaunchPadID = "LaunchPad";
        public const string ReconditioningStr = "Reconditioning";
        public const string RolloutStr = "Rollout";
        public const string RollbackStr = "Rollback";
        public const string RecoveryStr = "Recover";
        public const string UnknownStr = "Unknown Situation";
        public bool IsHumanRated;

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

        public BuildListVessel AssociatedBLV => Utilities.FindBLVesselByID(LC, new Guid(AssociatedID));

        private LCItem _lc = null;
        public LCItem LC
        {
            get
            {
                if (_lc == null)
                {
                    foreach (var ksc in KCTGameStates.KSCs)
                        foreach (var lc in ksc.LaunchComplexes)
                            if (lc.Recon_Rollout.Contains(this))
                            {
                                _lc = lc;
                                break;
                            }
                }
                
                return _lc;
            }
            set
            {
                _lc = value;
            }
        }

        public ReconRollout()
        {
            Progress = 0;
            BP = 0;
            Cost = 0;
            Mass = 0;
            VesselBP = 0;
            RRType = RolloutReconType.None;
            AssociatedID = "";
            IsHumanRated = false;
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
        public ReconRollout(BuildListVessel vessel, RolloutReconType type, string id, string launchSite="")
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

        public string GetItemName() => Name;

        public double GetBuildRate()
        {
            double buildRate = Utilities.GetBuildRate(0, LC, IsHumanRated, false);
            if (RRType != RolloutReconType.Reconditioning)
                buildRate = Math.Min(buildRate, Utilities.GetBuildRateCap(VesselBP, Mass, LC));
            buildRate *= LC.EfficiencyEngineers * KCTGameStates.EfficiecnyEngineers * LC.RushRate;

            if (RRType == RolloutReconType.Rollback)
                buildRate *= -1;
            return buildRate;
        }

        public double GetFractionComplete() => RRType == RolloutReconType.Rollback ? (BP - Progress) / BP : Progress / BP;

        public double GetTimeLeft()
        {
            double n = RRType == RolloutReconType.Rollback ? 0 : BP;
            return (n - Progress) / GetBuildRate();
        }
        public double GetTimeLeftEst(double offset) => GetTimeLeft();

        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.Reconditioning;

        public bool IsComplete() => RRType == RolloutReconType.Rollback ? Progress <= 0 : Progress >= BP;

        public void IncrementProgress(double UTDiff)
        {
            double progBefore = Progress;
            Progress += GetBuildRate() * UTDiff;
            if (Progress > BP) Progress = BP;

            int prevStep = (int)Math.Floor(10 * progBefore / BP);
            int curStep = (int)Math.Floor(10 * Progress / BP);

            if (Utilities.CurrentGameIsCareer() && RRType == RolloutReconType.Rollout && Cost > 0)
            {
                int steps = curStep - prevStep;
                if (steps > 0) //  Pay or halt at 10% intervals
                {
                    if (Funding.Instance.Funds < Cost / 10) //If they can't afford to continue the rollout, progress stops
                    {
                        Progress = progBefore;
                        if (TimeWarp.CurrentRate > 1f && KCTWarpController.Instance is KCTWarpController)
                        {
                            ScreenMessages.PostScreenMessage("Timewarp was stopped because there's insufficient funds to continue the rollout");
                            KCTWarpController.Instance.StopWarp();
                        }
                    }
                    else
                        Utilities.SpendFunds(steps * Cost / 10, TransactionReasons.VesselRollout);
                }
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
