using System;
using System.Linq;

namespace KerbalConstructionTime
{
    public class AirlaunchPrep : IKCTBuildItem
    {
        public string Name = string.Empty;
        public double BP = 0, Progress = 0, Cost = 0;
        public string AssociatedID = string.Empty;

        public const string Name_Mount = "Mounting to carrier";
        public const string Name_Unmount = "Unmounting";

        public enum PrepDirection { Mount, Unmount };
        public PrepDirection Direction = PrepDirection.Mount;

        public BuildListVessel AssociatedBLV => Utilities.FindBLVesselByID(new Guid(AssociatedID));

        public KSCItem KSC => KCTGameStates.KSCs.FirstOrDefault(k => k.AirlaunchPrep.Exists(r => r.AssociatedID == AssociatedID));

        public AirlaunchPrep()
        {
            Name = Name_Mount;
            Progress = 0;
            BP = 0;
            Cost = 0;
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
            Name = Name_Mount;
        }

        public double GetBuildRate()
        {
            double buildRate = Utilities.GetSPHBuildRateSum(KSC);

            if (Direction == PrepDirection.Unmount)
                buildRate *= -1;

            return buildRate;
        }

        public string GetItemName()
        {
            return Name;
        }

        public BuildListVessel.ListType GetListType()
        {
            return BuildListVessel.ListType.SPH;
        }

        public double GetTimeLeft()
        {
            double timeLeft = (BP - Progress) / GetBuildRate();
            if (Direction == PrepDirection.Unmount)
                timeLeft = (-Progress) / GetBuildRate();
            return timeLeft;
        }

        public bool IsComplete()
        {
            bool complete = Progress >= BP;
            if (Direction == PrepDirection.Unmount)
                complete = Progress <= 0;
            return complete;
        }

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
                        if (TimeWarp.CurrentRate > 1f && KCTGameStates.WarpInitiated && this == KCTGameStates.TargetedItem)
                        {
                            ScreenMessages.PostScreenMessage("Timewarp was stopped because there's insufficient funds to continue the airlaunch preparations");
                            TimeWarp.SetRate(0, true);
                            KCTGameStates.WarpInitiated = false;
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
            {
                Direction = PrepDirection.Unmount;
                Name = Name_Unmount;
            }
            else
            {
                Direction = PrepDirection.Mount;
                Name = Name_Mount;
            }
        }
    }
}
