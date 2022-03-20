namespace KerbalConstructionTime
{
    public class AirlaunchPrepStorageItem
    {
        [Persistent] private string name = "";
        [Persistent] public double BP = 0, progress = 0, cost = 0, mass = 0, vesselBP = 0;
        [Persistent] public string associatedID = "";
        [Persistent] public bool isHumanRated;

        public AirlaunchPrep ToAirlaunchPrep()
        {
            var ret = new AirlaunchPrep
            {
                BP = BP,
                Progress = progress,
                Cost = cost,
                Direction = name == AirlaunchPrep.Name_Mount ? AirlaunchPrep.PrepDirection.Mount : AirlaunchPrep.PrepDirection.Unmount,
                AssociatedID = associatedID,
                Mass = mass,
                VesselBP = vesselBP,
                IsHumanRated = isHumanRated
            };
            return ret;
        }

        public AirlaunchPrepStorageItem FromAirlaunchPrep(AirlaunchPrep ap)
        {
            name = ap.Name;
            BP = ap.BP;
            progress = ap.Progress;
            cost = ap.Cost;
            associatedID = ap.AssociatedID;
            mass = ap.Mass;
            vesselBP = ap.VesselBP;
            isHumanRated = ap.IsHumanRated;
            return this;
        }
    }
}
