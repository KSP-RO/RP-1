using System;
using SaveUpgradePipeline;
using UnityEngine;

namespace RP0.UpgradeScripts
{
    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO")]
    public class v2_4_AdminLevels : UpgradeScript
    {
        public override string Name { get => "RP-1 Admin Building Levels Upgrader"; }
        public override string Description { get => "Updates admin building level"; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(2, 4, 0);
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

            foreach (ConfigNode n in node.nodes)
            {
                if (n.name != "SpaceCenter/Administration")
                    continue;

                string lvl = n.GetValue("lvl");
                switch (lvl)
                {
                    case "0.5": lvl = "0.375"; break;
                    case "1":   lvl = "0.625"; break;
                    default: return;
                }

                n.SetValue("lvl", lvl);
                RP0Debug.Log($"UpgradePipeline context {loadContext} updated Admin level to {lvl}");
                return;
            }
        }
    }

    [UpgradeModule(LoadContext.SFS, sfsNodeUrl = "GAME/SCENARIO/KSCs/KSCItem/FacilityUpgrades/FacilityUpgrade")]
    public class v2_4_AdminLevelsKCT : UpgradeScript
    {
        public override string Name { get => "RP-1 Admin Building Levels Construction Upgrader"; }
        public override string Description { get => "Updates admin building level in constructions"; }
        public override Version EarliestCompatibleVersion { get => new Version(2, 0, 0); }
        protected static Version _targetVersion = new Version(2, 4, 0);
        public override Version TargetVersion => _targetVersion;

        public override TestResult OnTest(ConfigNode node, LoadContext loadContext, ref string nodeName)
        {
            nodeName = node.GetValue("id");
            return nodeName == "SpaceCenter/Administration" ? TestResult.Upgradeable : TestResult.Pass;
        }

        public override void OnUpgrade(ConfigNode node, LoadContext loadContext, ConfigNode parentNode)
        {
            string name = node.GetValue("id");
            if (name != "SpaceCenter/Administration")
                return;

            int oldCur = 0;
            int oldUp = 0;
            double oldCost = 0d;
            node.TryGetValue("currentLevel", ref oldCur);
            node.TryGetValue("upgradeLevel", ref oldUp);
            node.TryGetValue("spentCost", ref oldCost);
            
            int newCur = 0;
            int newUp = 0;
            switch (oldUp)
            {
                case 1:
                    if (oldCost < 40000d)
                    {
                        newCur = 0;
                        newUp = 1;
                    }
                    else if (oldCost < 140000d)
                    {
                        newCur = 1;
                        newUp = 2;
                    }
                    else
                    {
                        newCur = 2;
                        newUp = 3;
                    }
                    break;

                case 2:
                    if (oldCost < 400000d)
                    {
                        newCur = 3;
                        newUp = 4;
                    }
                    else
                    {
                        newCur = 4;
                        newUp = 5;
                    }
                    break;
            }
            if (newUp != 5)
            {
                // Except in the case of going to lvl5, we rejigger BP and total cost
                double cost = 0;
                switch (newUp)
                {
                    case 1: cost = 40000d; break;
                    case 2: cost = 140000d; break;
                    case 3: cost = 250000d; break;
                    case 4: cost = 400000d; break;
                }
                node.SetValue("BP", Formula.GetConstructionBP(cost, 0d, SpaceCenterFacility.Administration));
                node.SetValue("cost", cost.ToString("N0"));
            }
            node.SetValue("currentLevel", newCur);
            node.SetValue("upgradeLevel", newUp);

            RP0Debug.Log($"UpgradePipeline context {loadContext} updated KCT Admin Building upgrade to target level {newUp} (was {oldUp})");
        }
    }
}
