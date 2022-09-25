namespace KerbalConstructionTime
{
    public class ReconRolloutStorageItem
    {
        [Persistent] private string name = "";
        [Persistent] public double BP = 0, progress = 0, cost = 0;
        [Persistent] public string associatedID = "";
        [Persistent] public string launchPadID = "LaunchPad";

        public ReconRollout ToReconRollout()
        {
            return new ReconRollout
            {
                BP = BP,
                Progress = progress,
                Cost = cost,
                RRType = ReconRollout.RRDict.ContainsKey(name) ? ReconRollout.RRDict[name] : ReconRollout.RolloutReconType.None,
                AssociatedID = associatedID,
                LaunchPadID = launchPadID
            };
        }

        public ReconRolloutStorageItem FromReconRollout(ReconRollout rr)
        {
            name = rr.Name;
            BP = rr.BP;
            progress = rr.Progress;
            cost = rr.Cost;
            associatedID = rr.AssociatedID;
            launchPadID = rr.LaunchPadID;
            return this;
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
