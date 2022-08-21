using RP0;
using System;
using UniLinq;

namespace KerbalConstructionTime
{
    public class AirlaunchPrep : LCProject
    {
        public enum PrepDirection { Mount, Unmount };

        public override string Name => direction == PrepDirection.Mount ? Name_Mount : Name_Unmount;

        public const string Name_Mount = "Mounting to carrier";
        public const string Name_Unmount = "Unmounting";
        
        [Persistent]
        public PrepDirection direction = PrepDirection.Mount;

        protected override TransactionReasonsRP0 transactionReason => TransactionReasonsRP0.AirLaunchRollout;
        protected override TransactionReasonsRP0 transactionReasonTime => TransactionReasonsRP0.RateAirlaunch;

        public AirlaunchPrep() : base()
        {
        }

        public AirlaunchPrep(BuildListVessel vessel, string id)
        {
            direction = PrepDirection.Mount;
            associatedID = id;
            progress = 0;

            BP = Formula.GetAirlaunchBP(vessel);
            cost = Formula.GetAirlaunchCost(vessel);
            mass = vessel.GetTotalMass();
            isHumanRated = vessel.humanRated;
            vesselBP = vessel.buildPoints + vessel.integrationPoints;
            _lc = vessel.LC;
        }

        public override bool IsReversed => direction == PrepDirection.Unmount;
        public override bool HasCost => direction == PrepDirection.Mount;

        public override BuildListVessel.ListType GetListType() => BuildListVessel.ListType.AirLaunch;

        public void SwitchDirection()
        {
            if (direction == PrepDirection.Mount)
                direction = PrepDirection.Unmount;
            else
                direction = PrepDirection.Mount;
        }
        public override void Load(ConfigNode node)
        {
            base.Load(node);
            if (KCTGameStates.LoadedSaveVersion < 15)
            {
                string n = node.GetValue("name");
                direction = n == Name_Mount ? PrepDirection.Mount : PrepDirection.Unmount;
            }
        }
    }
}
