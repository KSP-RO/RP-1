namespace KerbalConstructionTime
{
    public class AirlaunchPrepStorageItem
    {
        [Persistent] private string name = "";
        [Persistent] public double BP = 0, progress = 0, cost = 0;
        [Persistent] public string associatedID = "";

        public AirlaunchPrep ToAirlaunchPrep()
        {
            var ret = new AirlaunchPrep
            {
                BP = BP,
                Progress = progress,
                Cost = cost,
                Direction = name == AirlaunchPrep.Name_Mount ? AirlaunchPrep.PrepDirection.Mount : AirlaunchPrep.PrepDirection.Unmount,
                AssociatedID = associatedID
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
            return this;
        }
    }
}
