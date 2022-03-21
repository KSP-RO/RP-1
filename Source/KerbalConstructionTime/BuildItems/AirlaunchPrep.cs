using System;
using System.Linq;

namespace KerbalConstructionTime
{
    public class AirlaunchPrep : IKCTBuildItem
    {
        public string Name => Direction == PrepDirection.Mount ? Name_Mount : Name_Unmount;
        public double BP = 0, Progress = 0, Cost = 0, Mass = 0, VesselBP;
        public string AssociatedID = string.Empty;
        public bool IsHumanRated = false;

        public const string Name_Mount = "Mounting to carrier";
        public const string Name_Unmount = "Unmounting";

        public enum PrepDirection { Mount, Unmount };
        public PrepDirection Direction = PrepDirection.Mount;

        public BuildListVessel AssociatedBLV => Utilities.FindBLVesselByID(new Guid(AssociatedID));

        public LCItem LC
        {
            get
            {
                foreach (var ksc in KCTGameStates.KSCs)
                    foreach (var lc in ksc.LaunchComplexes)
                        if (lc.Recon_Rollout.Exists(r => r.AssociatedID == AssociatedID))
                            return lc;

                return null;
            }
        }

        public AirlaunchPrep()
        {
            Progress = 0;
            BP = 0;
            Cost = 0;
            Mass = 0;
            VesselBP = 0;
            IsHumanRated = false;
            Direction = PrepDirection.Mount;
            AssociatedID = string.Empty;
        }

        public AirlaunchPrep(BuildListVessel vessel, string id)
        {
            Direction = PrepDirection.Mount;
            AssociatedID = id;
            Progress = 0;

            BP = MathParser.ParseAirlaunchTimeFormula(vessel);
            Cost = MathParser.ParseAirlaunchCostFormula(vessel);
            Mass = vessel.GetTotalMass();
            IsHumanRated = vessel.IsHumanRated;
            VesselBP = vessel.BuildPoints;
        }

        public double GetBuildRate()
        {
            double buildRate = Math.Min(Utilities.GetBuildRate(0, LC, IsHumanRated, false), Utilities.GetBuildRateCap(VesselBP, Mass, LC))
                * LC.EfficiencyPersonnel * KCTGameStates.EfficiecnyEngineers;

            if (Direction == PrepDirection.Unmount)
                buildRate *= -1;

            return buildRate;
        }

        public string GetItemName() => Name;

        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.SPH;

        public double GetFractionComplete() => Direction == PrepDirection.Mount ? Progress / BP : (BP - Progress) / BP;

        public double GetTimeLeft()
        {
            double goal = Direction == PrepDirection.Mount ? BP : 0;
            return (goal - Progress) / GetBuildRate();
        }

        public bool IsComplete() => Direction == PrepDirection.Mount ? Progress >= BP : Progress <= 0;

        public void IncrementProgress(double UTDiff)
        {
            double progBefore = Progress;
            Progress += GetBuildRate() * UTDiff;
            if (Progress > BP) Progress = BP;

            if (Utilities.CurrentGameIsCareer() && Direction == PrepDirection.Mount && Cost > 0)
            {
                int steps;
                if ((steps = (int)(Math.Floor(Progress / BP * 10) - Math.Floor(progBefore / BP * 10))) > 0)    //passed 10% of the progress
                {
                    if (Funding.Instance.Funds < Cost / 10)    //If they can't afford to continue the rollout, progress stops
                    {
                        Progress = progBefore;
                        if (TimeWarp.CurrentRate > 1f && KCTWarpController.Instance is KCTWarpController)
                        {
                            ScreenMessages.PostScreenMessage("Timewarp was stopped because there's insufficient funds to continue the airlaunch preparations");
                            KCTWarpController.Instance.StopWarp();
                        }
                    }
                    else
                        Utilities.SpendFunds(Cost / 10 * steps, TransactionReasons.VesselRollout);
                }
            }
        }

        public void SwitchDirection()
        {
            if (Direction == PrepDirection.Mount)
                Direction = PrepDirection.Unmount;
            else
                Direction = PrepDirection.Mount;
        }
    }
}
