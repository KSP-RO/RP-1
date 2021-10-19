using System;
using System.Collections.Generic;
using System.Linq;

namespace KerbalConstructionTime
{
    public class ReconRollout : IKCTBuildItem
    {
        public string Name => RRInverseDict[RRType];
        public double BP = 0, Progress = 0, Cost = 0;
        public string AssociatedID = string.Empty;
        public string LaunchPadID = "LaunchPad";
        public const string ReconditioningStr = "LaunchPad Reconditioning";
        public const string RolloutStr = "Vessel Rollout";
        public const string RollbackStr = "Vessel Rollback";
        public const string RecoveryStr = "Vessel Recovery";
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

        public BuildListVessel AssociatedBLV => Utilities.FindBLVesselByID(new Guid(AssociatedID));

        public KSCItem KSC => KCTGameStates.KSCs.FirstOrDefault(k => k.Recon_Rollout.Exists(r => r.AssociatedID == AssociatedID));

        public ReconRollout()
        {
            Progress = 0;
            BP = 0;
            Cost = 0;
            RRType = RolloutReconType.None;
            AssociatedID = "";
            LaunchPadID = "LaunchPad";
        }

        public ReconRollout(Vessel vessel, RolloutReconType type, string id, string launchSite)
        {
            RRType = type;
            AssociatedID = id;
            LaunchPadID = launchSite;
            KCTDebug.Log("New recon_rollout at launchsite: " + LaunchPadID);
            Progress = 0;
            try
            {
                BP = MathParser.ParseReconditioningFormula(new BuildListVessel(vessel), true);
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
            BP = MathParser.ParseReconditioningFormula(vessel, true);
            //if (type != RolloutReconType.Reconditioning)
                //BP *= KCT_PresetManager.Instance.ActivePreset.timeSettings.RolloutReconSplit;

            if (type == RolloutReconType.Reconditioning)
            {
                //BP *= (1 - KCT_PresetManager.Instance.ActivePreset.timeSettings.RolloutReconSplit);
            }
            else if (type == RolloutReconType.Rollout)
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

        public double ProgressPercent() => Math.Round(100 * (Progress / BP), 2);

        public string GetItemName() => Name;

        public double GetBuildRate()
        {
            double buildRate = AssociatedBLV?.Type == BuildListVessel.ListType.SPH
                                ? Utilities.GetSPHBuildRateSum(KSC) : Utilities.GetVABBuildRateSum(KSC);

            if (RRType == RolloutReconType.Rollback)
                buildRate *= -1;
            return buildRate;
        }

        public double GetTimeLeft()
        {
            double n = RRType == RolloutReconType.Rollback ? 0 : BP;
            return (n - Progress) / GetBuildRate();
        }

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
