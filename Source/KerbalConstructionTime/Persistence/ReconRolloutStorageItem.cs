namespace KerbalConstructionTime
{
    public class ReconRolloutStorageItem
    {
        [Persistent] private string name = "";
        [Persistent] public double BP = 0, progress = 0, cost = 0, mass = 0, vesselBP = 0;
        [Persistent] public string associatedID = "";
        [Persistent] public string launchPadID = "LaunchPad";
        [Persistent] public string lcID = "";
        [Persistent] public bool isHumanRated;

        public ReconRollout ToReconRollout()
        {
            var r = new ReconRollout
            {
                BP = BP,
                Progress = progress,
                Cost = cost,
                RRType = ReconRollout.RRDict.ContainsKey(name) ? ReconRollout.RRDict[name] : ReconRollout.RolloutReconType.None,
                Mass = mass,
                AssociatedID = associatedID,
                LaunchPadID = launchPadID,
                VesselBP = vesselBP,
                IsHumanRated = isHumanRated,
                LC = KCTGameStates.FindLCFromID(new System.Guid(lcID))
            };

            // back-compat
            if (r.RRType == ReconRollout.RolloutReconType.None)
            {
                switch (name)
                {
                    case "LaunchPad Reconditioning":
                        r.RRType = ReconRollout.RolloutReconType.Reconditioning;
                        break;
                    case "Vessel Rollout":
                        r.RRType = ReconRollout.RolloutReconType.Rollout;
                        break;
                    case "Vesssel Rollback":
                        r.RRType = ReconRollout.RolloutReconType.Rollback;
                        break;
                    case "Vessel Recovery":
                    case "Recovery":
                        r.RRType = ReconRollout.RolloutReconType.Recovery;
                        break;
                }
            }

            return r;
        }

        public ReconRolloutStorageItem FromReconRollout(ReconRollout rr)
        {
            name = rr.Name;
            BP = rr.BP;
            progress = rr.Progress;
            cost = rr.Cost;
            associatedID = rr.AssociatedID;
            launchPadID = rr.LaunchPadID;
            mass = rr.Mass;
            vesselBP = rr.VesselBP;
            isHumanRated = rr.IsHumanRated;
            lcID = rr.LC.ID.ToString("N");
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
