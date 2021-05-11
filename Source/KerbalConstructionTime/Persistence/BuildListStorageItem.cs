using System;
using System.Collections.Generic;

namespace KerbalConstructionTime
{
    public class BuildListStorageItem
    {
        [Persistent]
        string shipName, shipID;
        [Persistent]
        double progress, effectiveCost, buildTime, integrationTime;
        [Persistent]
        string launchSite, flag;
        [Persistent]
        bool cannotEarnScience;
        [Persistent]
        float cost = 0, integrationCost;
        [Persistent]
        float mass = 0, kscDistance = 0;
        [Persistent]
        int numStageParts = 0, numStages = 0;
        [Persistent]
        double stagePartCost = 0;
        [Persistent]
        int rushBuildClicks = 0;
        [Persistent]
        int EditorFacility = 0, LaunchPadID = -1;
        [Persistent]
        int BuildListIndex = -1;
        [Persistent]
        List<string> desiredManifest = new List<string>();
        [Persistent]
        string KCTPersistentID;
        [Persistent]
        EditorFacility FacilityBuiltIn;

        public BuildListVessel ToBuildListVessel()
        {
            var ret = new BuildListVessel(shipName, launchSite, effectiveCost, buildTime, integrationTime, flag, cost, integrationCost, EditorFacility)
            {
                Progress = progress,
                Id = new Guid(shipID),
                CannotEarnScience = cannotEarnScience,
                TotalMass = mass,
                NumStageParts = numStageParts,
                NumStages = numStages,
                StagePartCost = stagePartCost,
                DistanceFromKSC = kscDistance,
                RushBuildClicks = rushBuildClicks,
                LaunchSiteID = LaunchPadID,
                BuildListIndex = BuildListIndex,
                DesiredManifest = desiredManifest,
                KCTPersistentID = KCTPersistentID,
                FacilityBuiltIn = FacilityBuiltIn
            };
            return ret;
        }

        public BuildListStorageItem FromBuildListVessel(BuildListVessel blv)
        {
            progress = blv.Progress;
            effectiveCost = blv.EffectiveCost;
            buildTime = blv.BuildPoints;
            integrationTime = blv.IntegrationPoints;
            launchSite = blv.LaunchSite;
            flag = blv.Flag;
            shipName = blv.ShipName;
            shipID = blv.Id.ToString();
            cannotEarnScience = blv.CannotEarnScience;
            cost = blv.Cost;
            integrationCost = blv.IntegrationCost;
            rushBuildClicks = blv.RushBuildClicks;
            mass = blv.TotalMass;
            numStageParts = blv.NumStageParts;
            numStages = blv.NumStages;
            stagePartCost = blv.StagePartCost;
            kscDistance = blv.DistanceFromKSC;
            EditorFacility = (int)blv.GetEditorFacility();
            BuildListIndex = blv.BuildListIndex;
            LaunchPadID = blv.LaunchSiteID;
            desiredManifest = blv.DesiredManifest;
            KCTPersistentID = blv.KCTPersistentID;
            FacilityBuiltIn = blv.FacilityBuiltIn;
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
