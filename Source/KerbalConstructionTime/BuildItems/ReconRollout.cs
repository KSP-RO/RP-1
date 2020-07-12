using System;
using System.Linq;

namespace KerbalConstructionTime
{
    public class ReconRollout : IKCTBuildItem
    {
        public string Name = string.Empty;
        public double BP = 0, Progress = 0, Cost = 0;
        public string AssociatedID = string.Empty;
        public string LaunchPadID = "LaunchPad";

        public enum RolloutReconType { Reconditioning, Rollout, Rollback, Recovery, None };
        private RolloutReconType _rrTypeInternal = RolloutReconType.None;

        public RolloutReconType RRType
        {
            get
            {
                if (_rrTypeInternal != RolloutReconType.None)
                    return _rrTypeInternal;
                else
                {
                    if (Name == "LaunchPad Reconditioning")
                        _rrTypeInternal = RolloutReconType.Reconditioning;
                    else if (Name == "Vessel Rollout")
                        _rrTypeInternal = RolloutReconType.Rollout;
                    else if (Name == "Vessel Rollback")
                        _rrTypeInternal = RolloutReconType.Rollback;
                    else if (Name == "Vessel Recovery")
                        _rrTypeInternal = RolloutReconType.Recovery;
                    return _rrTypeInternal;
                }
            }
            set
            {
                _rrTypeInternal = value;
            }
        }

        public BuildListVessel AssociatedBLV => Utilities.FindBLVesselByID(new Guid(AssociatedID));

        public KSCItem KSC => KCTGameStates.KSCs.FirstOrDefault(k => k.Recon_Rollout.Exists(r => r.AssociatedID == AssociatedID));

        public ReconRollout()
        {
            Name = "LaunchPad Reconditioning";
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
            if (type == RolloutReconType.Reconditioning) 
            {
                try
                {
                    BP = MathParser.ParseReconditioningFormula(new BuildListVessel(vessel), true);
                }
                catch
                {
                    KCTDebug.Log("Error while determining BP for recon_rollout");
                }
                finally
                {
                    Name = "LaunchPad Reconditioning";
                }
            }
            else if (type == RolloutReconType.Rollout)
            {
                try
                {
                    BP = MathParser.ParseReconditioningFormula(new BuildListVessel(vessel), false);
                }
                catch
                {
                    KCTDebug.Log("Error while determining BP for recon_rollout");
                }
                finally
                {
                    Name = "Vessel Rollout";
                }
            }
            else if (type == RolloutReconType.Rollback)
            {
                try
                {
                    BP = MathParser.ParseReconditioningFormula(new BuildListVessel(vessel), false);
                }
                catch
                {
                    KCTDebug.Log("Error while determining BP for recon_rollout");
                }
                finally
                {
                    Name = "Vessel Rollback";
                    Progress = BP;
                }
            }
            else if (type == RolloutReconType.Recovery)
            {
                try
                {
                    BP = MathParser.ParseReconditioningFormula(new BuildListVessel(vessel), false);
                }
                catch
                {
                    KCTDebug.Log("Error while determining BP for recon_rollout");
                }
                finally
                {
                    Name = "Vessel Recovery";
                    double KSCDistance = (float)SpaceCenter.Instance.GreatCircleDistance(SpaceCenter.Instance.cb.GetRelSurfaceNVector(vessel.latitude, vessel.longitude));
                    double maxDist = SpaceCenter.Instance.cb.Radius * Math.PI;
                    BP += BP * (KSCDistance / maxDist);
                }
            }
        }

        public ReconRollout(BuildListVessel vessel, RolloutReconType type, string id, string launchSite="")
        {
            RRType = type;
            AssociatedID = id;
            if (launchSite != "") //For when we add custom launchpads
                LaunchPadID = launchSite;
            else
                LaunchPadID = vessel.LaunchSite;

            Progress = 0;
            if (type == RolloutReconType.Reconditioning)
            {
                BP = MathParser.ParseReconditioningFormula(vessel, true);
                //BP *= (1 - KCT_PresetManager.Instance.ActivePreset.timeSettings.RolloutReconSplit);
                Name = "LaunchPad Reconditioning";
            }
            else if (type == RolloutReconType.Rollout)
            {
                BP = MathParser.ParseReconditioningFormula(vessel, false);
                //BP *= KCT_PresetManager.Instance.ActivePreset.timeSettings.RolloutReconSplit;
                Name = "Vessel Rollout";
                Cost = MathParser.ParseRolloutCostFormula(vessel);
            }
            else if (type == RolloutReconType.Rollback)
            {
                BP = MathParser.ParseReconditioningFormula(vessel, false);
                //BP *= KCT_PresetManager.Instance.ActivePreset.timeSettings.RolloutReconSplit;
                Progress = BP;
                Name = "Vessel Rollback";
            }
            else if (type == RolloutReconType.Recovery)
            {
                BP = MathParser.ParseReconditioningFormula(vessel, false);
                //BP *= KCT_PresetManager.Instance.ActivePreset.timeSettings.RolloutReconSplit;
                Name = "Vessel Recovery";
                double maxDist = SpaceCenter.Instance.cb.Radius * Math.PI;
                BP += BP * (vessel.DistanceFromKSC / maxDist);
            }
        }

        public void SwapRolloutType()
        {
            if (RRType == RolloutReconType.Rollout)
            {
                RRType = RolloutReconType.Rollback;
                Name = "Vessel Rollback";
            }
            else if (RRType == RolloutReconType.Rollback)
            {
                RRType = RolloutReconType.Rollout;
                Name = "Vessel Rollout";
            }
        }

        public double ProgressPercent()
        {
            return Math.Round(100 * (Progress / BP), 2);
        }

        public string GetItemName()
        {
            return Name;
        }

        public double GetBuildRate()
        {
            double buildRate;
            if (AssociatedBLV != null && AssociatedBLV.Type == BuildListVessel.ListType.SPH)
                buildRate = Utilities.GetSPHBuildRateSum(KSC);
            else
                buildRate = Utilities.GetVABBuildRateSum(KSC);

            if (RRType == RolloutReconType.Rollback)
                buildRate *= -1;

            return buildRate;
        }

        public double GetTimeLeft()
        {
            double timeLeft = (BP - Progress) / ((IKCTBuildItem)this).GetBuildRate();
            if (RRType == RolloutReconType.Rollback)
                timeLeft = (-Progress) / ((IKCTBuildItem)this).GetBuildRate();
            return timeLeft;
        }

        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.Reconditioning;

        public bool IsComplete()
        {
            bool complete = Progress >= BP;
            if (RRType == RolloutReconType.Rollback)
                complete = Progress <= 0;
            return complete;
        }

        public void IncrementProgress(double UTDiff)
        {
            double progBefore = Progress;
            Progress += GetBuildRate() * UTDiff;
            if (Progress > BP) Progress = BP;

            if (Utilities.CurrentGameIsCareer() && RRType == RolloutReconType.Rollout && Cost > 0)
            {
                int steps;
                if ((steps = (int)(Math.Floor(Progress / BP * 10) - Math.Floor(progBefore / BP * 10))) > 0) //passed 10% of the progress
                {
                    if (Funding.Instance.Funds < Cost / 10) //If they can't afford to continue the rollout, progress stops
                    {
                        Progress = progBefore;
                        if (TimeWarp.CurrentRate > 1f && KCTGameStates.WarpInitiated && this == KCTGameStates.TargetedItem)
                        {
                            ScreenMessages.PostScreenMessage("Timewarp was stopped because there's insufficient funds to continue the rollout");
                            TimeWarp.SetRate(0, true);
                            KCTGameStates.WarpInitiated = false;
                        }
                    }
                    else
                        Utilities.SpendFunds(Cost / 10 * steps, TransactionReasons.VesselRollout);
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
