using System;
using SaveUpgradePipeline;
using UnityEngine;
using ROUtils;

namespace RP0.UpgradeScripts
{
    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO")]
    public class v3_0_ACRnD : UpgradeScript
    {
        public override string Name { get => "RP-1 Astronaut Complex and RnD Upgrader"; }
        public override string Description { get => "Updates AC and RnD building levels"; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(3, 0, 0);
        public override Version TargetVersion => _targetVersion;

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            nodeName = node.GetValue("name");
            return nodeName == "ScenarioUpgradeableFacilities" ? TestResult.Upgradeable : TestResult.Pass;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            string name = node.GetValue("name");
            if (name != "ScenarioUpgradeableFacilities")
                return;

            // We could probably do this in one pass given the layout of the sfs
            // but we're being cautious
            float rndLevel = 0f;
            foreach (ConfigNode n in node.nodes)
            {
                if (n.name != "SpaceCenter/ResearchAndDevelopment")
                    continue;

                n.TryGetValue("lvl", ref rndLevel);
                n.SetValue("lvl", "0"); // will be fixed on game load
                break;
            }

            // Idea: if you've upgraded RnD, we'll put that money in AC instead.
            // If not, we give you a free first-upgrade AC so it doesn't kill X-Planes
            foreach (ConfigNode n in node.nodes)
            {
                if (n.name != "SpaceCenter/AstronautComplex")
                    continue;

                string lvl = n.GetValue("lvl");
                switch (lvl)
                {
                    case "0.5": lvl = "0.75"; break;
                    case "1":   lvl = "1"; break;
                    default: lvl = rndLevel > 0f ? "0.5" : "0.25"; break;
                }

                n.SetValue("lvl", lvl);
                RP0Debug.Log($"UpgradePipeline context {loadContext} updated AC level to {lvl}");
                return;
            }
        }
    }

    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO/KSCs/KSCItem/FacilityUpgrades")]
    public class v3_0_ACRnDKCT : UpgradeScript
    {
        public override string Name { get => "RP-1 Astronaut Complex and RnD Construction Upgrader"; }
        public override string Description { get => "Updates AC and RnD building levels in constructions"; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(3, 0, 0);
        public override Version TargetVersion => _targetVersion;

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            foreach (ConfigNode n in node.nodes)
            {
                nodeName = n.GetValue("id");
                if (nodeName == "SpaceCenter/AstronautComplex" || nodeName == "SpaceCenter/ResearchAndDevelopment")
                    return TestResult.Upgradeable;
            }
            return TestResult.Pass;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            for (int i = node.nodes.Count; i-- > 0;)
            {
                var n = node.nodes[i];
                string name = n.GetValue("id");

                // Nuke RnD upgrades
                if (name == "SpaceCenter/ResearchAndDevelopment")
                {
                    node.nodes.nodes.RemoveAt(i);
                    continue;
                }

                // Handle AC upgrades
                if (name != "SpaceCenter/AstronautComplex")
                    continue;

                int oldCur = 0;
                int oldUp = 0;
                double oldSpentCost = 0d;
                n.TryGetValue("currentLevel", ref oldCur);
                n.TryGetValue("upgradeLevel", ref oldUp);
                n.TryGetValue("spentCost", ref oldSpentCost);

                int newCur = 0;
                int newUp = 0;
                switch (oldUp)
                {
                    case 1:
                        newCur = 2;
                        newUp = 3;
                        break;

                    case 2:
                        newCur = 3;
                        newUp = 4;
                        break;
                }
                // We rejigger BP and total cost. Ignore fundsloss parameter here
                double cost = Database.FacilityLevelCosts[SpaceCenterFacility.AstronautComplex][newUp];
                double oldCost = Database.FacilityLevelCosts[SpaceCenterFacility.AstronautComplex].SumThrough(newUp - 1);
                double bp = Formula.GetConstructionBP(cost, oldCost, SpaceCenterFacility.AstronautComplex);
                double oldBP = 0d;
                n.TryGetValue("BP", ref oldBP);
                n.SetValue("BP", Math.Min(bp, oldBP));
                n.SetValue("cost", cost.ToString("N0"));
                n.SetValue("currentLevel", newCur);
                n.SetValue("upgradeLevel", newUp);

                RP0Debug.Log($"UpgradePipeline context {loadContext} updated KCT Admin Building upgrade to target level {newUp} (was {oldUp})");
            }
        }
    }

    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO")]
    public class v3_0_KCTSCM : UpgradeScript
    {
        public override string Name { get => "RP-1 KCT Upgrader"; }
        public override string Description { get => "Updates main scenario module for RP-1"; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(3, 0, 0);
        public override Version TargetVersion => _targetVersion;

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            return node.GetValue("name") == "KerbalConstructionTimeData" ? TestResult.Upgradeable : TestResult.Pass;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            if(node.GetValue("name") == "KerbalConstructionTimeData")
            {
                node.SetValue("name", "SpaceCenterManagement");
                RP0Debug.Log($"UpgradePipeline context {loadContext} updated KCTData to be SpaceCenterManagement");
            }
        }
    }
}
