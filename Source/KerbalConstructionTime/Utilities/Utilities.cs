using KSP.UI;
using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace KerbalConstructionTime
{
    public static class Utilities
    {
        private static bool? _isKSCSwitcherInstalled = null;
        private static bool? _isKRASHInstalled = null;
        private static bool? _isPrincipiaInstalled = null;
        private static DateTime _startedFlashing;
        internal const string _icon_KCT_Off_24 = "RP-0/PluginData/Icons/KCT_off-24";
        internal const string _icon_KCT_Off_38 = "RP-0/PluginData/Icons/KCT_off-38";
        internal const string _icon_KCT_On_24 = "RP-0/PluginData/Icons/KCT_on-24";
        internal const string _icon_KCT_On_38 = "RP-0/PluginData/Icons/KCT_on-38";
        internal const string _icon_KCT_Off = "RP-0/PluginData/Icons/KCT_off";
        internal const string _icon_KCT_On = "RP-0/PluginData/Icons/KCT_on";

        public static AvailablePart GetAvailablePartByName(string partName)
        {
            return PartLoader.getPartInfoByName(partName);
        }

        /// <summary>
        /// This is actually the cost in BPs which can in turn be used to calculate the ingame time it takes to build the vessel.
        /// </summary>
        /// <param name="parts"></param>
        /// <returns></returns>
        public static double GetBuildTime(List<Part> parts)
        {
            double totalEffectiveCost = GetEffectiveCost(parts);
            return GetBuildTime(totalEffectiveCost);
        }

        public static double GetBuildTime(List<ConfigNode> parts)
        {
            double totalEffectiveCost = GetEffectiveCost(parts);
            return GetBuildTime(totalEffectiveCost);
        }

        public static double GetBuildTime(double totalEffectiveCost)
        {
            var formulaParams = new Dictionary<string, string>()
            {
                { "E", totalEffectiveCost.ToString() },
                { "O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString() }
            };
            double finalBP = MathParser.GetStandardFormulaValue("BP", formulaParams);
            return finalBP;
        }

        public static double GetEffectiveCost(List<Part> parts)
        {
            //get list of parts that are in the inventory
            IList<Part> inventorySample = ScrapYardWrapper.GetPartsInInventory(parts, ScrapYardWrapper.ComparisonStrength.STRICT) ?? new List<Part>();

            double totalEffectiveCost = 0;

            List<string> globalVariables = new List<string>();

            foreach (Part p in parts)
            {
                string name = p.partInfo.name;
                double effectiveCost = 0;
                double cost = GetPartCosts(p);
                double dryCost = GetPartCosts(p, false);

                double drymass = p.mass;
                double wetmass = p.GetResourceMass() + drymass;

                double PartMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetPartVariable(name);
                double ModuleMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetModuleVariable(p.Modules, out bool doRes);
                double ResourceMultiplier = 1d;
                if (doRes)
                    ResourceMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetResourceVariable(p.Resources);
                PresetManager.Instance.ActivePreset.PartVariables.SetGlobalVariables(globalVariables, p.Modules);

                double InvEff = (inventorySample.Contains(p) ? PresetManager.Instance.ActivePreset.TimeSettings.InventoryEffect : 0);
                int builds = ScrapYardWrapper.GetBuildCount(p);
                int used = ScrapYardWrapper.GetUseCount(p);
                //C=cost, c=dry cost, M=wet mass, m=dry mass, U=part tracker, O=overall multiplier, I=inventory effect (0 if not in inv), B=build effect

                effectiveCost = MathParser.GetStandardFormulaValue("EffectivePart",
                    new Dictionary<string, string>()
                    {
                        {"C", cost.ToString()},
                        {"c", dryCost.ToString()},
                        {"M", wetmass.ToString()},
                        {"m", drymass.ToString()},
                        {"U", builds.ToString()},
                        {"u", used.ToString() },
                        {"O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString()},
                        {"I", InvEff.ToString()},
                        {"B", PresetManager.Instance.ActivePreset.TimeSettings.BuildEffect.ToString()},
                        {"PV", PartMultiplier.ToString()},
                        {"RV", ResourceMultiplier.ToString()},
                        {"MV", ModuleMultiplier.ToString()}
                    });

                if (InvEff != 0)
                {
                    inventorySample.Remove(p);
                }

                if (effectiveCost < 0) effectiveCost = 0;
                totalEffectiveCost += effectiveCost;
            }

            double globalMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetGlobalVariable(globalVariables);

            return totalEffectiveCost * globalMultiplier;
        }

        public static double GetEffectiveCost(List<ConfigNode> parts)
        {
            //get list of parts that are in the inventory
            IList<ConfigNode> inventorySample = ScrapYardWrapper.GetPartsInInventory(parts, ScrapYardWrapper.ComparisonStrength.STRICT) ?? new List<ConfigNode>();

            double totalEffectiveCost = 0;
            var globalVariables = new List<string>();
            foreach (ConfigNode p in parts)
            {
                string name = PartNameFromNode(p);
                string raw_name = name;
                double effectiveCost = 0;
                double cost;
                float wetMass;

                ShipConstruction.GetPartCostsAndMass(p, GetAvailablePartByName(name), out float dryCost, out float fuelCost, out float dryMass, out float fuelMass);
                cost = dryCost + fuelCost;
                wetMass = dryMass + fuelMass;

                double PartMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetPartVariable(raw_name);
                var moduleNames = new List<string>();
                bool hasResourceCostMult = true;
                foreach (ConfigNode modNode in GetModulesFromPartNode(p))
                {
                    string s = modNode.GetValue("name");
                    if (s == "ModuleTagNoResourceCostMult")
                        hasResourceCostMult = false;
                    moduleNames.Add(s);
                }
                double ModuleMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetModuleVariable(moduleNames);

                double ResourceMultiplier = 1d;
                if (hasResourceCostMult)
                {
                    var resourceNames = new List<string>();
                    foreach (ConfigNode rNode in GetResourcesFromPartNode(p))
                        resourceNames.Add(rNode.GetValue("name"));
                    ResourceMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetResourceVariable(resourceNames);
                }

                PresetManager.Instance.ActivePreset.PartVariables.SetGlobalVariables(globalVariables, moduleNames);

                double InvEff = inventorySample.Contains(p) ? PresetManager.Instance.ActivePreset.TimeSettings.InventoryEffect : 0;
                int builds = ScrapYardWrapper.GetBuildCount(p);
                int used = ScrapYardWrapper.GetUseCount(p);
                //C=cost, c=dry cost, M=wet mass, m=dry mass, U=part tracker, O=overall multiplier, I=inventory effect (0 if not in inv), B=build effect

                effectiveCost = MathParser.GetStandardFormulaValue("EffectivePart",
                    new Dictionary<string, string>()
                    {
                        {"C", cost.ToString()},
                        {"c", dryCost.ToString()},
                        {"M", wetMass.ToString()},
                        {"m", dryMass.ToString()},
                        {"U", builds.ToString()},
                        {"u", used.ToString()},
                        {"O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString()},
                        {"I", InvEff.ToString()},
                        {"B", PresetManager.Instance.ActivePreset.TimeSettings.BuildEffect.ToString()},
                        {"PV", PartMultiplier.ToString()},
                        {"RV", ResourceMultiplier.ToString()},
                        {"MV", ModuleMultiplier.ToString()}
                    });

                if (InvEff != 0)
                {
                    inventorySample.Remove(p);
                }

                if (effectiveCost < 0) effectiveCost = 0;
                totalEffectiveCost += effectiveCost;
            }

            double globalMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetGlobalVariable(globalVariables);

            return totalEffectiveCost * globalMultiplier;
        }

        public static string PartNameFromNode(ConfigNode part)
        {
            string name = part.GetValue("part");
            if (name != null)
                name = name.Split('_')[0];
            else
                name = part.GetValue("name");
            return name;
        }

        public static double GetPartCosts(Part part, bool includeFuel = true)
        {
            double cost = part.partInfo.cost + part.GetModuleCosts(part.partInfo.cost);
            foreach (PartResource rsc in part.Resources)
            {
                PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(rsc.resourceName);
                if (!includeFuel)
                {
                    cost -= rsc.maxAmount * def.unitCost;
                }
                else //accounts for if you remove some fuel from a tank
                {
                    cost -= (rsc.maxAmount - rsc.amount) * def.unitCost;
                }
            }
            return cost;
        }

        public static ConfigNode[] GetModulesFromPartNode(ConfigNode partNode)
        {
            var n = partNode.GetNodes("MODULE").ToList();
            for (int i = n.Count - 1; i >= 0; i--)
            {
                ConfigNode cn = n[i];

                string s = null;
                var b = cn.TryGetValue("name", ref s);
                if (!b || string.IsNullOrEmpty(s))
                    n.Remove(cn);
            }
            return n.ToArray();
        }

        public static ConfigNode[] GetResourcesFromPartNode(ConfigNode partNode)
        {
            return partNode.GetNodes("RESOURCE");
        }

        public static double GetBuildRate(int index, BuildListVessel.ListType type, KSCItem KSC, bool UpgradedRate = false)
        {
            return GetBuildRate(index, type, KSC, UpgradedRate ? 1 : 0);
        }

        public static double GetBuildRate(int index, BuildListVessel.ListType type, KSCItem KSC, int upgradeDelta)
        {
            if (KSC == null) KSC = KCTGameStates.ActiveKSC;
            double ret = 0;
            if (type == BuildListVessel.ListType.VAB)
            {
                if (upgradeDelta == 0 && KSC.VABRates.Count > index)
                {
                    return KSC.VABRates[index];
                }
                else if (upgradeDelta == 1 && KSC.UpVABRates.Count > index)
                {
                    return KSC.UpVABRates[index];
                }
                else if (upgradeDelta > 1)
                {
                    return MathParser.ParseBuildRateFormula(BuildListVessel.ListType.VAB, index, KSC, upgradeDelta);
                }
                else
                {
                    return 0;
                }
            }
            else if (type == BuildListVessel.ListType.SPH)
            {
                if (upgradeDelta == 0 && KSC.SPHRates.Count > index)
                {
                    return KSC.SPHRates[index];
                }
                else if (upgradeDelta == 1 && KSC.UpSPHRates.Count > index)
                {
                    return KSC.UpSPHRates[index];
                }
                else if (upgradeDelta > 1)
                {
                    return MathParser.ParseBuildRateFormula(BuildListVessel.ListType.SPH, index, KSC, upgradeDelta);
                }
                else
                {
                    return 0;
                }
            }
            else if (type == BuildListVessel.ListType.TechNode)
            {
                ret = KCTGameStates.TechList[index].BuildRate;
            }
            return ret;
        }

        public static double GetBuildRate(BuildListVessel ship)
        {
            if (ship.Type == BuildListVessel.ListType.None)
                ship.FindTypeFromLists();

            if (ship.Type == BuildListVessel.ListType.VAB)
                return GetBuildRate(ship.KSC.VABList.IndexOf(ship), ship.Type, ship.KSC);
            else if (ship.Type == BuildListVessel.ListType.SPH)
                return GetBuildRate(ship.KSC.SPHList.IndexOf(ship), ship.Type, ship.KSC);
            else
                return 0;
        }

        public static List<double> BuildRatesVAB(KSCItem KSC)
        {
            if (KSC == null) KSC = KCTGameStates.ActiveKSC;
            return KSC.VABRates;
        }

        public static List<double> BuildRatesSPH(KSCItem KSC)
        {
            if (KSC == null) KSC = KCTGameStates.ActiveKSC;
            return KSC.SPHRates;
        }

        public static double GetVABBuildRateSum(KSCItem KSC)
        {
            double rateTotal = 0;
            List<double> rates = BuildRatesVAB(KSC);
            for (int i = 0; i < rates.Count; i++)
            {
                double rate = rates[i];
                rateTotal += rate;
            }
            return rateTotal;
        }

        public static double GetSPHBuildRateSum(KSCItem KSC)
        {
            double rateTotal = 0;
            List<double> rates = BuildRatesSPH(KSC);
            for (int i = 0; i < rates.Count; i++)
            {
                double rate = rates[i];
                rateTotal += rate;
            }
            return rateTotal;
        }

        public static double GetBothBuildRateSum(KSCItem KSC)
        {
            double rateTotal = GetSPHBuildRateSum(KSC);
            rateTotal += GetVABBuildRateSum(KSC);

            return rateTotal;
        }

        public static void ProgressBuildTime()
        {
            double UT = 0;
            if (HighLogic.LoadedSceneIsEditor) //support for EditorTime
                UT = HighLogic.CurrentGame.flightState.universalTime;
            else
                UT = Planetarium.GetUniversalTime();
            if (KCTGameStates.LastUT == 0)
                KCTGameStates.LastUT = UT;
            double UTDiff = UT - KCTGameStates.LastUT;
            if (UTDiff > 0 && (HighLogic.LoadedSceneIsEditor || UTDiff < (TimeWarp.fetch.warpRates[TimeWarp.fetch.warpRates.Length - 1] * 2)))
            {
                foreach (KSCItem ksc in KCTGameStates.KSCs)
                {
                    for (int i = 0; i < ksc.VABList.Count; i++)
                    {
                        ksc.VABList[i].IncrementProgress(UTDiff);
                    }
                    for (int i = 0; i < ksc.SPHList.Count; i++)
                    {
                        ksc.SPHList[i].IncrementProgress(UTDiff);
                    }

                    foreach (ReconRollout rr in ksc.Recon_Rollout)
                    {
                        rr.IncrementProgress(UTDiff);
                    }
                    //Reset the associated launchpad id when rollback completes
                    ksc.Recon_Rollout.ForEach(delegate(ReconRollout rr)
                    {
                        if (rr.RRType == ReconRollout.RolloutReconType.Rollback && rr.IsComplete())
                        {
                            BuildListVessel blv = FindBLVesselByID(new Guid(rr.AssociatedID));
                            if (blv != null)
                                blv.LaunchSiteID = -1;
                        }
                    });
                    ksc.Recon_Rollout.RemoveAll(rr => !PresetManager.Instance.ActivePreset.GeneralSettings.ReconditioningTimes || 
                                                      (rr.RRType != ReconRollout.RolloutReconType.Rollout && rr.IsComplete()));

                    foreach (AirlaunchPrep ap in ksc.AirlaunchPrep)
                    {
                        ap.IncrementProgress(UTDiff);
                    }
                    ksc.AirlaunchPrep.RemoveAll(ap => ap.Direction != AirlaunchPrep.PrepDirection.Mount && ap.IsComplete());

                    foreach (FacilityUpgrade kscTech in ksc.KSCTech)
                    {
                        kscTech.IncrementProgress(UTDiff);
                    }
                    if (HighLogic.LoadedScene == GameScenes.SPACECENTER) ksc.KSCTech.RemoveAll(ub => ub.UpgradeProcessed);

                }
                for (int i = 0; i < KCTGameStates.TechList.Count; i++)
                {
                    TechItem tech = KCTGameStates.TechList[i];
                    tech.IncrementProgress(UTDiff);
                }
            }

            if (KCTGameStates.TargetedItem != null && KCTGameStates.TargetedItem.IsComplete())
            {
                TimeWarp.SetRate(0, true);
                KCTGameStates.TargetedItem = null;
                KCTGameStates.WarpInitiated = false;
            }
            KCTGameStates.LastUT = UT;
        }

        public static float GetTotalVesselCost(ProtoVessel vessel, bool includeFuel = true)
        {
            float total = 0, totalDry = 0;
            foreach (ProtoPartSnapshot part in vessel.protoPartSnapshots)
            {
                total += ShipConstruction.GetPartCosts(part, part.partInfo, out float dry, out float wet);
                totalDry += dry;
            }
            if (includeFuel)
                return total;
            else
                return totalDry;
        }

        public static float GetTotalVesselCost(ConfigNode vessel, bool includeFuel = true)
        {
            float total = 0;
            foreach (ConfigNode part in vessel.GetNodes("PART"))
            {
                total += GetPartCostFromNode(part, includeFuel);
            }
            return total;
        }

        public static float GetPartCostFromNode(ConfigNode part, bool includeFuel = true)
        {
            string name = PartNameFromNode(part);
            AvailablePart aPart = GetAvailablePartByName(name);
            if (aPart == null)
                return 0;
            ShipConstruction.GetPartCostsAndMass(part, aPart, out float dryCost, out float fuelCost, out _, out _);

            if (includeFuel)
                return dryCost + fuelCost;
            else
                return dryCost;
        }

        public static float GetPartMassFromNode(ConfigNode part, bool includeFuel = true, bool includeClamps = true)
        {
            AvailablePart aPart = GetAvailablePartByName(PartNameFromNode(part));

            if (aPart == null || (!includeClamps && aPart.partPrefab != null && aPart.partPrefab.Modules.Contains<LaunchClamp>()))
                return 0;
            ShipConstruction.GetPartCostsAndMass(part, aPart, out _, out _, out float dryMass, out float fuelMass);
            if (includeFuel)
                return dryMass+fuelMass;
            else
                return dryMass;
        }

        public static float GetShipMass(this ShipConstruct sc, bool excludeClamps, out float dryMass, out float fuelMass)
        {
            dryMass = 0f;
            fuelMass = 0f;
            int partCount = sc.parts.Count;
            while (partCount-- > 0)
            {
                Part part = sc.parts[partCount];
                AvailablePart partInfo = part.partInfo;

                if (excludeClamps && part.partInfo.partPrefab.Modules.Contains<LaunchClamp>())
                    continue;

                float partDryMass = partInfo.partPrefab.mass + part.GetModuleMass(partInfo.partPrefab.mass, ModifierStagingSituation.CURRENT);
                float partFuelMass = 0f;
                int resCount = part.Resources.Count;
                while (resCount-- > 0)
                {
                    PartResource resource = part.Resources[resCount];
                    PartResourceDefinition info = resource.info;
                    partFuelMass += info.density * (float)resource.amount;
                }
                dryMass += partDryMass;
                fuelMass += partFuelMass;
            }
            return dryMass + fuelMass;
        }

        public static string GetTweakScaleSize(ProtoPartSnapshot part)
        {
            string partSize = string.Empty;
            if (part.modules != null)
            {
                ProtoPartModuleSnapshot tweakscale = part.modules.Find(mod => mod.moduleName == "TweakScale");
                if (tweakscale != null)
                {
                    ConfigNode tsCN = tweakscale.moduleValues;
                    string defaultScale = tsCN.GetValue("defaultScale");
                    string currentScale = tsCN.GetValue("currentScale");
                    if (!defaultScale.Equals(currentScale))
                        partSize = "," + currentScale;
                }
            }
            return partSize;
        }

        public static string GetTweakScaleSize(ConfigNode part)
        {
            string partSize = string.Empty;
            if (part.HasNode("MODULE"))
            {
                ConfigNode[] Modules = part.GetNodes("MODULE");
                if (Modules.Length > 0 && Modules.FirstOrDefault(mod => mod.GetValue("name") == "TweakScale") != null)
                {
                    ConfigNode tsCN = Modules.First(mod => mod.GetValue("name") == "TweakScale");
                    string defaultScale = tsCN.GetValue("defaultScale");
                    string currentScale = tsCN.GetValue("currentScale");
                    if (!defaultScale.Equals(currentScale))
                        partSize = "," + currentScale;
                }
            }
            return partSize;
        }

        public static string GetTweakScaleSize(Part part)
        {
            string partSize = "";
            if (part.Modules != null && part.Modules.Contains("TweakScale"))
            {
                PartModule tweakscale = part.Modules["TweakScale"];

                object defaultScale = tweakscale.Fields.GetValue("defaultScale");
                object currentScale = tweakscale.Fields.GetValue("currentScale");
                if (!defaultScale.Equals(currentScale))
                    partSize = "," + currentScale.ToString();
            }
            return partSize;
        }

        /// <summary>
        /// Tests to see if two ConfigNodes have the same information. Currently requires same ordering of subnodes
        /// </summary>
        /// <param name="node1"></param>
        /// <param name="node2"></param>
        /// <returns></returns>
        public static bool ConfigNodesAreEquivalent(ConfigNode node1, ConfigNode node2)
        {
            //Check that the number of subnodes are equal
            if (node1.GetNodes().Length != node2.GetNodes().Length)
                return false;
            //Check that all the values are identical
            foreach (string valueName in node1.values.DistinctNames())
            {
                if (!node2.HasValue(valueName))
                    return false;
                if (node1.GetValue(valueName) != node2.GetValue(valueName))
                    return false;
            }

            //Check all subnodes for equality
            for (int index = 0; index < node1.GetNodes().Length; ++index)
            {
                if (!ConfigNodesAreEquivalent(node1.nodes[index], node2.nodes[index]))
                    return false;
            }

            //If all these tests pass, we consider the nodes to be equivalent
            return true;
        }

        public static string GetStockButtonTexturePath()
        {

            if (KCTEvents.Instance.KCTButtonStockImportant && DateTime.Now.CompareTo(_startedFlashing.AddSeconds(0)) > 0 && DateTime.Now.Millisecond < 500)
                return _icon_KCT_Off_38;
            else if (KCTEvents.Instance.KCTButtonStockImportant && DateTime.Now.CompareTo(_startedFlashing.AddSeconds(3)) > 0)
            {
                KCTEvents.Instance.KCTButtonStockImportant = false;
                return _icon_KCT_On_38;
            }
            //The normal icon
            else
                return _icon_KCT_On_38;
        }

        public static string GetButtonTexturePath()
        {
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return _icon_KCT_Off_24;

            string textureReturn;
            //Flash for up to 3 seconds, at half second intervals per icon
            if (KCTEvents.Instance.KCTButtonStockImportant && DateTime.Now.CompareTo(_startedFlashing.AddSeconds(3)) < 0 && DateTime.Now.Millisecond < 500)
                textureReturn = _icon_KCT_Off;
            //If it's been longer than 3 seconds, set Important to false and stop flashing
            else if (KCTEvents.Instance.KCTButtonStockImportant && DateTime.Now.CompareTo(_startedFlashing.AddSeconds(3)) > 0)
            {
                KCTEvents.Instance.KCTButtonStockImportant = false;
                textureReturn = _icon_KCT_On;
            }
            //The normal icon
            else
                textureReturn = _icon_KCT_On;

            return textureReturn + "-24";
        }

        public static bool CurrentGameHasScience()
        {
            return HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX;
        }

        public static bool CurrentGameIsSandbox()
        {
            return HighLogic.CurrentGame.Mode == Game.Modes.SANDBOX;
        }

        public static bool CurrentGameIsCareer()
        {
            return HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
        }

        public static bool CurrentGameIsMission()
        {
            return HighLogic.CurrentGame.Mode == Game.Modes.MISSION || HighLogic.CurrentGame.Mode == Game.Modes.MISSION_BUILDER;
        }

        public static string AddScienceWithMessage(float science, TransactionReasons reason)
        {
            if (science > 0)
            {
                ResearchAndDevelopment.Instance.AddScience(science, reason);
                var message = new ScreenMessage($"[KCT] {science} science added.", 4f, ScreenMessageStyle.UPPER_LEFT);
                ScreenMessages.PostScreenMessage(message);
                return message.ToString();
            }
            return string.Empty;
        }

        public static void MoveVesselToWarehouse(BuildListVessel ship)
        {
            if (ship.Type == BuildListVessel.ListType.None)
                ship.FindTypeFromLists();

            if (ship.Type == BuildListVessel.ListType.VAB)
                MoveVesselToWarehouse(ship.Type, ship.KSC.VABList.IndexOf(ship), ship.KSC);
            else if (ship.Type == BuildListVessel.ListType.SPH)
                MoveVesselToWarehouse(ship.Type, ship.KSC.SPHList.IndexOf(ship), ship.KSC);
        }

        public static void MoveVesselToWarehouse(BuildListVessel.ListType ListIdentifier, int index, KSCItem KSC)
        {
            if (KSC == null) KSC = KCTGameStates.ActiveKSC;

            KCTEvents.Instance.KCTButtonStockImportant = true;
            _startedFlashing = DateTime.Now;    //Set the time to start flashing

            var Message = new StringBuilder();
            Message.AppendLine("The following vessel is complete:");
            BuildListVessel vessel = null;
            if (ListIdentifier == BuildListVessel.ListType.VAB)
            {
                vessel = KSC.VABList[index];
                KSC.VABList.RemoveAt(index);
                KSC.VABWarehouse.Add(vessel);
                
                Message.AppendLine(vessel.ShipName);
                Message.AppendLine("Please check the VAB Storage at "+KSC.KSCName+" to launch it.");
            
            }
            else if (ListIdentifier == BuildListVessel.ListType.SPH)
            {
                vessel = KSC.SPHList[index];
                KSC.SPHList.RemoveAt(index);
                KSC.SPHWarehouse.Add(vessel);

                Message.AppendLine(vessel.ShipName);
                Message.AppendLine("Please check the SPH Storage at " + KSC.KSCName + " to launch it.");
            }

            if ((KCTGameStates.Settings.ForceStopWarp || vessel == KCTGameStates.TargetedItem) && TimeWarp.CurrentRateIndex != 0)
            {
                TimeWarp.SetRate(0, true);
                KCTGameStates.WarpInitiated = false;
            }

            //Assign science based on science rate
            if (CurrentGameHasScience() && !vessel.CannotEarnScience)
            {
                double rate = MathParser.GetStandardFormulaValue("Research", new Dictionary<string, string>() { { "N", KSC.RDUpgrades[0].ToString() }, { "R", BuildingUpgradeLevel(SpaceCenterFacility.ResearchAndDevelopment).ToString() } });
                if (rate > 0)
                {
                    Message.AppendLine(AddScienceWithMessage((float)(rate * vessel.BuildPoints), TransactionReasons.None));
                }
            }

            //Add parts to the tracker
            if (!vessel.CannotEarnScience) //if the vessel was previously completed, then we shouldn't register it as a new build
            {
                ScrapYardWrapper.RecordBuild(vessel.ExtractedPartNodes);
            }

            string stor = ListIdentifier == 0 ? "VAB" : "SPH";
            KCTDebug.Log("Moved vessel " + vessel.ShipName + " to " + KSC.KSCName + "'s " + stor + " storage.");

            KCT_GUI.ResetBLWindow(false);
            if (!KCTGameStates.Settings.DisableAllMessages)
            {
                DisplayMessage("Vessel Complete!", Message, MessageSystemButton.MessageButtonColor.GREEN, MessageSystemButton.ButtonIcons.COMPLETE);
            }
        }

        public static double SpendFunds(double toSpend, TransactionReasons reason)
        {
            if (!CurrentGameIsCareer())
                return 0;
            KCTDebug.Log($"Removing funds: {toSpend}, New total: {Funding.Instance.Funds - toSpend}");
            if (toSpend < Funding.Instance.Funds)
                Funding.Instance.AddFunds(-toSpend, reason);
            return Funding.Instance.Funds;
        }

        public static double AddFunds(double toAdd, TransactionReasons reason)
        {
            if (!CurrentGameIsCareer())
                return 0;
            KCTDebug.Log($"Adding funds: {toAdd}, New total: {Funding.Instance.Funds + toAdd}");
            Funding.Instance.AddFunds(toAdd, reason);
            return Funding.Instance.Funds;
        }

        public static void ProcessSciPointTotalChange(float changeDelta)
        {
            // Earned point totals shouldn't decrease. This would only make sense when done through the cheat menu.
            if (changeDelta <= 0f || KCTGameStates.IsRefunding) return;

            bool addSavePts = KCTGameStates.SciPointsTotal == -1f;
            EnsureCurrentSaveHasSciTotalsInitialized(changeDelta);

            float pointsBef;
            if (addSavePts)
                pointsBef = 0f;
            else
                pointsBef = KCTGameStates.SciPointsTotal;

            KCTGameStates.SciPointsTotal += changeDelta;
            KCTDebug.Log("Total sci points earned is now: " + KCTGameStates.SciPointsTotal);

            double upgradesBef = MathParser.GetStandardFormulaValue("UpgradesForScience", new Dictionary<string, string>() { { "N", pointsBef.ToString() } });
            double upgradesAft = MathParser.GetStandardFormulaValue("UpgradesForScience", new Dictionary<string, string>() { { "N", KCTGameStates.SciPointsTotal.ToString() } });
            KCTDebug.Log($"Upg points bef: {upgradesBef}; aft: {upgradesAft}");

            int upgradesToAdd = (int)upgradesAft - (int)upgradesBef;
            if (upgradesToAdd > 0)
            {
                KCTDebug.Log($"Added {upgradesToAdd} upgrade points");
                ScreenMessages.PostScreenMessage($"{upgradesToAdd} KCT Upgrade Point{(upgradesToAdd > 1 ? "s" : string.Empty)} Added!", 8f, ScreenMessageStyle.UPPER_LEFT);
            }
        }

        public static void EnsureCurrentSaveHasSciTotalsInitialized(float changeDelta)
        {
            if (KCTGameStates.SciPointsTotal == -1f)
            {
                KCTDebug.Log("Trying to determine total science points for current save...");

                float totalSci = 0f;
                foreach (TechItem t in KCTGameStates.TechList)
                {
                    KCTDebug.Log($"Found tech in KCT list: {t.ProtoNode.techID} | {t.ProtoNode.state} | {t.ProtoNode.scienceCost}");
                    if (t.ProtoNode.state == RDTech.State.Available) continue;

                    totalSci += t.ProtoNode.scienceCost;
                }

                var techIDs = KerbalConstructionTimeData.techNameToTitle.Keys;
                foreach (var techId in techIDs)
                {
                    var ptn = ResearchAndDevelopment.Instance.GetTechState(techId);
                    if (ptn == null)
                    {
                        KCTDebug.Log($"Failed to find tech with id {techId}");
                        continue;
                    }

                    KCTDebug.Log($"Found tech {ptn.techID} | {ptn.state} | {ptn.scienceCost}");
                    if (ptn.techID == "unlockParts") continue;    // This node in RP-1 is unlocked automatically but has a high science cost
                    if (ptn.state != RDTech.State.Available) continue;

                    totalSci += ptn.scienceCost;
                }

                totalSci += ResearchAndDevelopment.Instance.Science - changeDelta;

                KCTDebug.Log("Calculated total: " + totalSci);
                KCTGameStates.SciPointsTotal = totalSci;
            }
        }

        public static BuildListVessel AddVesselToBuildList()
        {
            return AddVesselToBuildList(EditorLogic.fetch.launchSiteName);
        }

        public static BuildListVessel AddVesselToBuildList(string launchSite)
        {
            if (string.IsNullOrEmpty(launchSite))
            {
                launchSite = EditorLogic.fetch.launchSiteName;
            }

            double effCost = GetEffectiveCost(EditorLogic.fetch.ship.Parts);
            double bp = GetBuildTime(effCost);
            var blv = new BuildListVessel(EditorLogic.fetch.ship, launchSite, effCost, bp, EditorLogic.FlagURL)
            {
                ShipName = EditorLogic.fetch.shipNameField.text
            };

            return AddVesselToBuildList(blv);
        }

        public static BuildListVessel AddVesselToBuildList(BuildListVessel blv)
        {
            if (CurrentGameIsCareer())
            {
                //Check upgrades
                //First, mass limit
                List<string> facilityChecks = blv.MeetsFacilityRequirements(true);
                if (facilityChecks.Count != 0)
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "editorChecksFailedPopup", "Failed editor checks!",
                        "Warning! This vessel did not pass the editor checks! It will still be built, but you will not be able to launch it without upgrading. Listed below are the failed checks:\n" 
                        + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()), "Acknowledged", false, HighLogic.UISkin);
                }


                double totalCost = blv.GetTotalCost();
                double prevFunds = Funding.Instance.Funds;
                if (totalCost > prevFunds)
                {
                    KCTDebug.Log("Tried to add " + blv.ShipName + " to build list but not enough funds.");
                    KCTDebug.Log("Vessel cost: " + GetTotalVesselCost(blv.ShipNode) + ", Current funds: " + prevFunds);
                    var msg = new ScreenMessage("Not Enough Funds To Build!", 4f, ScreenMessageStyle.UPPER_CENTER);
                    ScreenMessages.PostScreenMessage(msg);
                    return null;
                }
                else
                {
                    SpendFunds(totalCost, TransactionReasons.VesselRollout);
                }
            }

            string type = string.Empty;
            if (blv.Type == BuildListVessel.ListType.VAB)
            {
                blv.LaunchSite = "LaunchPad";
                KCTGameStates.ActiveKSC.VABList.Add(blv);
                type = "VAB";
            }
            else if (blv.Type == BuildListVessel.ListType.SPH)
            {
                blv.LaunchSite = "Runway";
                KCTGameStates.ActiveKSC.SPHList.Add(blv);
                type = "SPH";
            }

            ScrapYardWrapper.ProcessVessel(blv.ExtractedPartNodes);

            KCTDebug.Log($"Added {blv.ShipName} to {type} build list at KSC {KCTGameStates.ActiveKSC.KSCName}. Cost: {blv.Cost}. IntegrationCost: {blv.IntegrationCost}");
            KCTDebug.Log("Launch site is " + blv.LaunchSite);
            var message = new ScreenMessage($"[KCT] Added {blv.ShipName} to {type} build list.", 4f, ScreenMessageStyle.UPPER_CENTER);
            ScreenMessages.PostScreenMessage(message);
            return blv;
        }

        public static IKCTBuildItem GetNextThingToFinish()
        {
            IKCTBuildItem thing = null;
            if (KCTGameStates.ActiveKSC == null)
                return null;
            double shortestTime = double.PositiveInfinity;
            foreach (KSCItem KSC in KCTGameStates.KSCs)
            {
                foreach (IKCTBuildItem blv in KSC.VABList)
                {
                    double time = blv.GetTimeLeft();
                    if (time < shortestTime)
                    {
                        thing = blv;
                        shortestTime = time;
                    }
                }
                foreach (IKCTBuildItem blv in KSC.SPHList)
                {
                    double time = blv.GetTimeLeft();
                    if (time < shortestTime)
                    {
                        thing = blv;
                        shortestTime = time;
                    }
                }
                
                foreach (IKCTBuildItem rr in KSC.Recon_Rollout)
                {
                    if (rr.IsComplete())
                        continue;
                    double time = rr.GetTimeLeft();
                    if (time < shortestTime)
                    {
                        thing = rr;
                        shortestTime = time;
                    }
                }

                foreach (IKCTBuildItem ap in KSC.AirlaunchPrep)
                {
                    if (ap.IsComplete())
                        continue;
                    double time = ap.GetTimeLeft();
                    if (time < shortestTime)
                    {
                        thing = ap;
                        shortestTime = time;
                    }
                }

                foreach (IKCTBuildItem ub in KSC.KSCTech)
                {
                    if (ub.IsComplete())
                        continue;
                    double time = ub.GetTimeLeft();
                    if (time < shortestTime)
                    {
                        thing = ub;
                        shortestTime = time;
                    }
                }
            }
            foreach (TechItem tech in KCTGameStates.TechList)
            {
                // Ignore items that are blocked
                if (tech.GetBlockingTech(KCTGameStates.TechList) == null)
                {
                    double time = ((IKCTBuildItem)tech).GetTimeLeft();
                    if (time < shortestTime)
                    {
                        thing = tech;
                        shortestTime = time;
                    }
                }
            }
            return thing;
        }

        public static void RampUpWarp()
        {
            IKCTBuildItem ship = GetNextThingToFinish();
            RampUpWarp(ship);
        }

        public static void RampUpWarp(IKCTBuildItem item)
        {
            int newRate = TimeWarp.CurrentRateIndex;
            double timeLeft = item.GetTimeLeft();
            if (double.IsPositiveInfinity(timeLeft))
                timeLeft = GetNextThingToFinish().GetTimeLeft();
            while ((newRate + 1 < TimeWarp.fetch.warpRates.Length) &&
                   (timeLeft > TimeWarp.fetch.warpRates[newRate + 1] * Planetarium.fetch.fixedDeltaTime) &&
                   (newRate < KCTGameStates.Settings.MaxTimeWarp))
            {
                newRate++;
            }
            TimeWarp.SetRate(newRate, true);
        }

        public static void DisableModFunctionality()
        {
            InputLockManager.RemoveControlLock("KCTLaunchLock");
            KCT_GUI.HideAll();
        }

        public static object GetMemberInfoValue(MemberInfo member, object sourceObject)
        {
            object newVal;
            if (member is FieldInfo)
                newVal = ((FieldInfo)member).GetValue(sourceObject);
            else
                newVal = ((PropertyInfo)member).GetValue(sourceObject, null);
            return newVal;
        }

        public static int TotalSpentUpgrades(KSCItem ksc = null)
        {
            if (ksc == null) ksc = KCTGameStates.ActiveKSC;
            int spentPoints = 0;
            if (PresetManager.Instance.ActivePreset.GeneralSettings.SharedUpgradePool)
            {
                for (int j = 0; j < KCTGameStates.KSCs.Count; j++)
                {
                    KSCItem KSC = KCTGameStates.KSCs[j];
                    for (int i = 0; i < KSC.VABUpgrades.Count; i++) spentPoints += KSC.VABUpgrades[i];
                    for (int i = 0; i < KSC.SPHUpgrades.Count; i++) spentPoints += KSC.SPHUpgrades[i];
                    spentPoints += KSC.RDUpgrades[0];
                }
                spentPoints += ksc.RDUpgrades[1]; //only count this once, all KSCs share this value
            }
            else
            {
                for (int i = 0; i < ksc.VABUpgrades.Count; i++) spentPoints += ksc.VABUpgrades[i];
                for (int i = 0; i < ksc.SPHUpgrades.Count; i++) spentPoints += ksc.SPHUpgrades[i];
                for (int i = 0; i < ksc.RDUpgrades.Count; i++) spentPoints += ksc.RDUpgrades[i];
            }
            return spentPoints;
        }

        public static int SpentUpgradesFor(SpaceCenterFacility facility, KSCItem ksc = null)
        {
            if (ksc == null) ksc = KCTGameStates.ActiveKSC;
            int spentPoints = 0;
            switch (facility)
            {
                case SpaceCenterFacility.ResearchAndDevelopment:
                    if (PresetManager.Instance.ActivePreset.GeneralSettings.SharedUpgradePool)
                    {
                        for (int j = 0; j < KCTGameStates.KSCs.Count; j++)
                        {
                            KSCItem KSC = KCTGameStates.KSCs[j];
                            spentPoints += KSC.RDUpgrades[0];
                        }
                        spentPoints += ksc.RDUpgrades[1]; //only count this once, all KSCs share this value
                    }
                    else
                    {
                        for (int i = 0; i < ksc.RDUpgrades.Count; i++) spentPoints += ksc.RDUpgrades[i];
                    }
                    break;
                case SpaceCenterFacility.SpaceplaneHangar:
                    if (PresetManager.Instance.ActivePreset.GeneralSettings.SharedUpgradePool)
                    {
                        for (int j = 0; j < KCTGameStates.KSCs.Count; j++)
                        {
                            KSCItem KSC = KCTGameStates.KSCs[j];
                            for (int i = 0; i < KSC.SPHUpgrades.Count; i++) spentPoints += KSC.SPHUpgrades[i];
                        }
                    }
                    else
                    {
                        for (int i = 0; i < ksc.SPHUpgrades.Count; i++) spentPoints += ksc.SPHUpgrades[i];
                    }
                    break;
                case SpaceCenterFacility.VehicleAssemblyBuilding:
                    if (PresetManager.Instance.ActivePreset.GeneralSettings.SharedUpgradePool)
                    {
                        for (int j = 0; j < KCTGameStates.KSCs.Count; j++)
                        {
                            KSCItem KSC = KCTGameStates.KSCs[j];
                            for (int i = 0; i < KSC.VABUpgrades.Count; i++) spentPoints += KSC.VABUpgrades[i];
                        }
                    }
                    else
                    {
                        for (int i = 0; i < ksc.VABUpgrades.Count; i++) spentPoints += ksc.VABUpgrades[i];
                    }
                    break;
                default:
                    throw new ArgumentException("invalid facility");
            }

            return spentPoints;
        }

        public static List<string> GetLaunchSites(bool isVAB)
        {
            EditorDriver.editorFacility = isVAB ? EditorFacility.VAB : EditorFacility.SPH;
            typeof(EditorDriver).GetMethod("setupValidLaunchSites", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null);
            return EditorDriver.ValidLaunchSites;
        }

        public static bool IsKSCSwitcherInstalled
        {
            get
            {
                if (!_isKSCSwitcherInstalled.HasValue)
                {
                    Type Switcher = null;
                    AssemblyLoader.loadedAssemblies.TypeOperation(t =>
                    {
                        if (t.FullName == "regexKSP.KSCSwitcher")
                        {
                            Switcher = t;
                        }
                    });

                    _isKSCSwitcherInstalled = Switcher != null;
                }
                return _isKSCSwitcherInstalled.Value;
            }
        }

        public static bool IsKRASHInstalled
        {
            get
            {
                if (!_isKRASHInstalled.HasValue)
                {
                    _isKRASHInstalled = AssemblyLoader.loadedAssemblies.Any(a => string.Equals(a.name, "KRASH", StringComparison.OrdinalIgnoreCase));
                }
                return _isKRASHInstalled.Value;
            }
        }

        public static bool IsPrincipiaInstalled
        {
            get
            {
                if (!_isPrincipiaInstalled.HasValue)
                {
                    _isPrincipiaInstalled = AssemblyLoader.loadedAssemblies.Any(a => string.Equals(a.name, "ksp_plugin_adapter", StringComparison.OrdinalIgnoreCase));
                }
                return _isPrincipiaInstalled.Value;
            }
        }

        public static bool IsKRASHSimActive
        {
            get
            {
                Assembly a = AssemblyLoader.loadedAssemblies.FirstOrDefault(la => string.Equals(la.name, "KRASH", StringComparison.OrdinalIgnoreCase))?.assembly;
                Type t = a?.GetType("KRASH.KRASHShelter");
                FieldInfo fi = t?.GetField("persistent", BindingFlags.Public | BindingFlags.Static);
                object krashPersistent = fi?.GetValue(null);
                fi = krashPersistent?.GetType().GetField("shelterSimulationActive", BindingFlags.Public | BindingFlags.Instance);
                bool? isActive = (bool?)fi?.GetValue(krashPersistent);

                return isActive ?? false;
            }
        }

        public static string GetActiveRSSKSC()
        {
            if (!IsKSCSwitcherInstalled) return "Stock";

            //get the LastKSC.KSCLoader.instance object
            //check the Sites object (KSCSiteManager) for the lastSite, if "" then get defaultSite
            Type Loader = null;
            AssemblyLoader.loadedAssemblies.TypeOperation(t =>
            {
                if (t.FullName == "regexKSP.KSCLoader")
                {
                    Loader = t;
                }
            });
            object LoaderInstance = GetMemberInfoValue(Loader.GetMember("instance")[0], null);
            if (LoaderInstance == null)
                return "Stock";
            object SitesObj = GetMemberInfoValue(Loader.GetMember("Sites")[0], LoaderInstance);
            string lastSite = (string)GetMemberInfoValue(SitesObj.GetType().GetMember("lastSite")[0], SitesObj);

            if (lastSite == "")
            {
                string defaultSite = (string)GetMemberInfoValue(SitesObj.GetType().GetMember("defaultSite")[0], SitesObj);
                return defaultSite;
            }
            return lastSite;
        }

        public static void SetActiveKSCToRSS()
        {
            string site = GetActiveRSSKSC();
            SetActiveKSC(site);
        }

        public static void SetActiveKSC(string site)
        {
            if (site == "") site = "Stock";
            if (KCTGameStates.ActiveKSC == null || site != KCTGameStates.ActiveKSC.KSCName)
            {
                KCTDebug.Log("Setting active site to " + site);
                KSCItem setActive = KCTGameStates.KSCs.FirstOrDefault(ksc => ksc.KSCName == site);
                if (setActive != null)
                {
                    KCTGameStates.ActiveKSC = setActive;
                }
                else
                {
                    setActive = new KSCItem(site);
                    if (CurrentGameIsCareer())
                        setActive.ActiveLPInstance.level = 0;
                    KCTGameStates.KSCs.Add(setActive);
                    KCTGameStates.ActiveKSC = setActive;
                }
            }
            KCTGameStates.ActiveKSCName = site;
        }

        public static PQSCity FindKSC(CelestialBody home)
        {
            if (home != null)
            {
                if (home.pqsController != null && home.pqsController.transform != null)
                {
                    Transform t = home.pqsController.transform.Find("KSC");
                    if (t != null)
                    {
                        PQSCity KSC = (PQSCity)t.GetComponent(typeof(PQSCity));
                        if (KSC != null) { return KSC; }
                    }
                }
            }

            PQSCity[] cities = Resources.FindObjectsOfTypeAll<PQSCity>();
            foreach (PQSCity c in cities)
            {
                if (c.name == "KSC")
                {
                    return c;
                }
            }

            return null;
        }

        public static void DisplayMessage(string title, StringBuilder text, MessageSystemButton.MessageButtonColor color, MessageSystemButton.ButtonIcons icon)
        {
            var m = new MessageSystem.Message(title, text.ToString(), color, icon);
            MessageSystem.Instance.AddMessage(m);
        }

        public static bool LaunchFacilityIntact(BuildListVessel.ListType type)
        {
            bool intact = true;
            if (type == BuildListVessel.ListType.VAB)
            {
                intact = new PreFlightTests.FacilityOperational("LaunchPad", "LaunchPad").Test();
            }
            else if (type == BuildListVessel.ListType.SPH)
            {
                if (!new PreFlightTests.FacilityOperational("Runway", "Runway").Test())
                    intact = false;
            }
            return intact;
        }

        public static void RecalculateEditorBuildTime(ShipConstruct ship)
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            double effCost = GetEffectiveCost(ship.Parts);
            KCTGameStates.EditorBuildTime = GetBuildTime(effCost);
            var kctVessel = new BuildListVessel(ship, EditorLogic.fetch.launchSiteName, effCost, KCTGameStates.EditorBuildTime, EditorLogic.FlagURL);

            KCTGameStates.EditorIntegrationTime = MathParser.ParseIntegrationTimeFormula(kctVessel);
            KCTGameStates.EditorRolloutCosts = MathParser.ParseRolloutCostFormula(kctVessel);
            KCTGameStates.EditorIntegrationCosts = MathParser.ParseIntegrationCostFormula(kctVessel);
            KCTGameStates.EditorRolloutTime = MathParser.ParseReconditioningFormula(kctVessel, false);
        }

        public static bool ApproximatelyEqual(double d1, double d2, double error = 0.01 )
        {
            return (1-error) <= (d1 / d2) && (d1 / d2) <= (1+error);
        }

        public static float GetParachuteDragFromPart(AvailablePart parachute)
        {
            foreach (AvailablePart.ModuleInfo mi in parachute.moduleInfos)
            {
                if (mi.info.Contains("Fully-Deployed Drag"))
                {
                    string[] split = mi.info.Split(new char[] {':', '\n'});
                    //TODO: Get SR code and put that in here, maybe with TryParse instead of Parse
                    for (int i=0; i<split.Length; i++)
                    {
                        if (split[i].Contains("Fully-Deployed Drag"))
                        {
                            if (!float.TryParse(split[i + 1], out float drag))
                            {
                                string[] split2 = split[i + 1].Split('>');
                                if (!float.TryParse(split2[1], out drag))
                                {
                                    Debug.Log("[KCT] Failure trying to read parachute data. Assuming 500 drag.");
                                    drag = 500;
                                }
                            }
                            return drag;
                        }
                    }
                }
            }
            return 0;
        }

        public static bool IsUnmannedCommand(AvailablePart part)
        {
            foreach (AvailablePart.ModuleInfo mi in part.moduleInfos)
            {
                if (mi.info.Contains("Unmanned")) return true;
            }
            return false;
        }

        public static bool ReconditioningActive(KSCItem KSC, string launchSite = "LaunchPad")
        {
            if (KSC == null) KSC = KCTGameStates.ActiveKSC;

            ReconRollout recon = KSC.GetReconditioning(launchSite);
            return (recon != null);
        }

        public static BuildListVessel FindBLVesselByID(Guid id)
        {
            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                var v = FindBLVesselByID(id, ksc);
                if (v != null) return v;
            }

            return null;
        }

        public static BuildListVessel FindBLVesselByID(Guid id, KSCItem ksc)
        {
            if (ksc != null)
            {
                BuildListVessel ves = ksc.VABWarehouse.Find(blv => blv.Id == id);
                if (ves != null)
                    return ves;

                ves = ksc.VABList.Find(blv => blv.Id == id);
                if (ves != null)
                    return ves;

                ves = ksc.SPHWarehouse.Find(blv => blv.Id == id);
                if (ves != null)
                    return ves;

                ves = ksc.SPHList.Find(blv => blv.Id == id);
                if (ves != null)
                    return ves;
            }

            return null;
        }

        public static void AddToDict(Dictionary<string, int> dict, string key, int value)
        {
            if (value <= 0) return;
            if (!dict.ContainsKey(key))
                dict.Add(key, value);
            else
                dict[key] += value;
        }

        public static bool RemoveFromDict(Dictionary<string, int> dict, string key, int value)
        {
            if (!dict.ContainsKey(key))
                return false;
            else if (dict[key] < value)
                return false;
            else
            {
                dict[key] -= value;
                return true;
            }
        }

        public static bool PartIsUnlocked(ConfigNode partNode)
        {
            string partName = PartNameFromNode(partNode);
            return PartIsUnlocked(partName);
        }

        public static bool PartIsUnlocked(string partName)
        {
            if (partName == null) return false;

            AvailablePart partInfoByName = PartLoader.getPartInfoByName(partName);
            if (partInfoByName == null) return false;

            ProtoTechNode techState = ResearchAndDevelopment.Instance.GetTechState(partInfoByName.TechRequired);
            bool partIsUnlocked = techState != null && techState.state == RDTech.State.Available &&
                                  RUIutils.Any(techState.partsPurchased, (a => a.name == partName));

            bool isExperimental = ResearchAndDevelopment.IsExperimentalPart(partInfoByName);

            return partIsUnlocked || isExperimental;
        }

        public static bool PartIsProcedural(ConfigNode part)
        {
            ConfigNode[] modules = part.GetNodes("MODULE");
            if (modules == null)
                return false;
            foreach (ConfigNode mod in modules)
            {
                if (mod.HasValue("name") && mod.GetValue("name").IndexOf("procedural", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        public static bool PartIsProcedural(ProtoPartSnapshot part)
        {
            if (part.modules != null)
                return part.modules.Find(m => m?.moduleName?.IndexOf("procedural", StringComparison.OrdinalIgnoreCase) >= 0) != null;
            return false;
        }

        public static bool PartIsProcedural(Part part)
        {
            if (part?.Modules != null)
            {
                for (int i = 0; i < part.Modules.Count; i++ )
                {
                    if (part.Modules[i]?.moduleName?.IndexOf("procedural", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        public static string ConstructLockedPartsWarning(Dictionary<AvailablePart, int> lockedPartsOnShip)
        {
            if (lockedPartsOnShip == null || lockedPartsOnShip.Count == 0)
                return null;

            var sb = new StringBuilder();
            sb.Append("This vessel contains parts which are not available at the moment:\n");

            foreach (KeyValuePair<AvailablePart, int> kvp in lockedPartsOnShip)
            {
                sb.Append($" <color=orange><b>{kvp.Value}x {kvp.Key.title}</b></color>\n");
            }

            return sb.ToString();
        }

        public static int BuildingUpgradeLevel(SpaceCenterFacility facility)
        {
            int lvl = BuildingUpgradeMaxLevel(facility);
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                lvl = (int)Math.Round(lvl * ScenarioUpgradeableFacilities.GetFacilityLevel(facility));
            }
            return lvl;
        }

        public static int BuildingUpgradeLevel(string facilityID)
        {
            int lvl = BuildingUpgradeMaxLevel(facilityID);
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                lvl = (int)Math.Round(lvl * ScenarioUpgradeableFacilities.GetFacilityLevel(facilityID));
            }
            return lvl;
        }

        public static int BuildingUpgradeMaxLevel(string facilityID)
        {
            int lvl = ScenarioUpgradeableFacilities.GetFacilityLevelCount(facilityID);
            if (lvl < 0)
            {
                if (!KCTGameStates.BuildingMaxLevelCache.TryGetValue(facilityID.Split('/').Last(), out lvl))
                {
                    //screw it, let's call it 2
                    lvl = 2;
                    KCTDebug.Log($"Couldn't get actual max level or cached one for {facilityID}. Assuming 2.");
                }
            }
            return lvl;
        }

        public static int BuildingUpgradeMaxLevel(SpaceCenterFacility facility)
        {
            int lvl = ScenarioUpgradeableFacilities.GetFacilityLevelCount(facility);
            if (lvl < 0)
            {
                if (!KCTGameStates.BuildingMaxLevelCache.TryGetValue(facility.ToString(), out lvl))
                {
                    //screw it, let's call it 2
                    lvl = 2;
                    KCTDebug.Log($"Couldn't get actual max level or cached one for {facility}. Assuming 2.");
                }
            }
            return lvl;
        }

        public static int TotalUpgradePoints()
        {
            int total = 0;
            //Starting points
            total += PresetManager.Instance.StartingUpgrades(HighLogic.CurrentGame.Mode);
            //R&D
            if (PresetManager.Instance.ActivePreset.GeneralSettings.TechUpgrades)
            {
                //Completed tech nodes
                if (CurrentGameHasScience())
                {
                    total += KCTGameStates.LastKnownTechCount;
                    if (KCTGameStates.LastKnownTechCount == 0)
                        total += ResearchAndDevelopment.Instance != null ? ResearchAndDevelopment.Instance.snapshot.GetData().GetNodes("Tech").Length : 0;
                }

                //In progress tech nodes
                total += KCTGameStates.TechList.Count;
            }
            total += (int)MathParser.GetStandardFormulaValue("UpgradesForScience", new Dictionary<string, string>()
            {
                { "N", KCTGameStates.SciPointsTotal.ToString() }
            });
            //Purchased funds
            total += KCTGameStates.PurchasedUpgrades[0];
            //Purchased science
            total += KCTGameStates.PurchasedUpgrades[1];
            //Temp upgrades (currently for when tech nodes finish)
            total += KCTGameStates.MiscellaneousTempUpgrades;
            
            //Misc. (when API)
            total += KCTGameStates.TemporaryModAddedUpgradesButReallyWaitForTheAPI;
            total += KCTGameStates.PermanentModAddedUpgradesButReallyWaitForTheAPI;

            return total;
        }

        public static bool RecoverActiveVesselToStorage(BuildListVessel.ListType listType)
        {
            var test = new ShipConstruct();
            try
            {
                KCTDebug.Log("Attempting to recover active vessel to storage.  listType: " + listType);
                GamePersistence.SaveGame("KCT_Backup", HighLogic.SaveFolder, SaveMode.OVERWRITE);
  
                KCTGameStates.RecoveredVessel = new BuildListVessel(FlightGlobals.ActiveVessel, listType);
  
                //KCT_GameStates.recoveredVessel.type = listType;
                if (listType == BuildListVessel.ListType.VAB)
                    KCTGameStates.RecoveredVessel.LaunchSite = "LaunchPad";
                else
                    KCTGameStates.RecoveredVessel.LaunchSite = "Runway";

                //check for symmetry parts and remove those references if they can't be found
                RemoveMissingSymmetry(KCTGameStates.RecoveredVessel.ShipNode);

                // debug, save to a file
                KCTGameStates.RecoveredVessel.ShipNode.Save("KCTVesselSave");

                //test if we can actually convert it
                bool success = test.LoadShip(KCTGameStates.RecoveredVessel.ShipNode);

                if (success)
                    ShipConstruction.CreateBackup(test);
                KCTDebug.Log("Load test reported success = " + success);
                if (!success)
                {
                    KCTGameStates.RecoveredVessel = null;
                    return false;
                }

                // Recovering the vessel in a coroutine was generating an exception insideKSP if a mod had added
                // modules to the vessel or it's parts at runtime.
                //
                // This is the way KSP does it
                //
                GameEvents.OnVesselRecoveryRequested.Fire(FlightGlobals.ActiveVessel);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[KCT] Error while recovering craft into inventory.");
                Debug.LogError("[KCT] error: " + ex);
                KCTGameStates.RecoveredVessel = null;
                ShipConstruction.ClearBackups();
                return false;
            }
        }

        public static void RemoveMissingSymmetry(ConfigNode ship)
        {
            //loop through, find all sym = lines and find the part they reference
            int referencesRemoved = 0;
            foreach (ConfigNode partNode in ship.GetNodes("PART"))
            {
                List<string> toRemove = new List<string>();
                foreach (string symPart in partNode.GetValues("sym"))
                {
                    //find the part in the ship
                    if (ship.GetNodes("PART").FirstOrDefault(cn => cn.GetValue("part") == symPart) == null)
                        toRemove.Add(symPart);
                }

                foreach (string remove in toRemove)
                {
                    foreach (ConfigNode.Value val in partNode.values)
                    {
                        if (val.value == remove)
                        {
                            referencesRemoved++;
                            partNode.values.Remove(val);
                            break;
                        }
                    }
                }
            }
            KCTDebug.Log("Removed " + referencesRemoved + " invalid symmetry references.");
        }

        /// <summary>
        /// Overrides or disables the editor's launch button (and individual site buttons) depending on settings
        /// </summary>
        public static void HandleEditorButton()
        {
            if (KCT_GUI.IsPrimarilyDisabled) return;

            //also set the editor ui to 1 height
            KCT_GUI.EditorWindowPosition.height = 1;

            var kctInstance = (EditorAddon)KerbalConstructionTime.Instance;

            if (KCTGameStates.Settings.OverrideLaunchButton)
            {
                if (KCTGameStates.EditorShipEditingMode)
                {
                    // Prevent switching between VAB and SPH in edit mode.
                    // Bad things will happen if the edits are saved in another mode than the initial one.
                    EditorLogic.fetch.switchEditorBtn.onClick.RemoveAllListeners();
                    EditorLogic.fetch.switchEditorBtn.onClick.AddListener(() =>
                    {
                        PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotSwitchEditor",
                            "Cannot switch editor!",
                            "Switching between VAB and SPH is not allowed while editing a vessel.",
                            "Acknowledged", false, HighLogic.UISkin);
                    });
                }

                KCTDebug.Log("Attempting to take control of launch button");

                EditorLogic.fetch.launchBtn.onClick.RemoveAllListeners();
                EditorLogic.fetch.launchBtn.onClick.AddListener(() => { KerbalConstructionTime.ShowLaunchAlert(null); });

                if (!kctInstance.IsLaunchSiteControllerBound)
                {
                    kctInstance.IsLaunchSiteControllerBound = true;
                    KCTDebug.Log("Attempting to take control of launchsite specific buttons");
                    //delete listeners to the launchsite specific buttons
                    UILaunchsiteController controller = UnityEngine.Object.FindObjectOfType<UILaunchsiteController>();
                    if (controller == null)
                        KCTDebug.Log("HandleEditorButton.controller is null");
                    else
                    {
                        // Need to use the try/catch because if multiple launch sites are disabled, then this would generate
                        // the following error:
                        //                          Cannot cast from source type to destination type
                        // which happens because the private member "launchPadItems" is a list, and if it is null, then it is
                        // not castable to a IEnumerable
                        try
                        {
                            if (controller.GetType().GetPrivateMemberValue("launchPadItems", controller, 4) is IEnumerable list)
                            {
                                foreach (object site in list)
                                {
                                    //find and disable the button
                                    //why isn't EditorLaunchPadItem public despite all of its members being public?
                                    Button button = site.GetType().GetPublicValue<Button>("buttonLaunch", site);
                                    if (button != null)
                                    {
                                        button.onClick.RemoveAllListeners();
                                        string siteName = site.GetType().GetPublicValue<string>("siteName", site);
                                        button.onClick.AddListener(() => { KerbalConstructionTime.ShowLaunchAlert(siteName); });
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            KCTDebug.Log("HandleEditorButton: Exception: " + ex);
                        }
                    }
                }
            }
            else
            {
                InputLockManager.SetControlLock(ControlTypes.EDITOR_LAUNCH, "KCTLaunchLock");
                if (!kctInstance.IsLaunchSiteControllerBound)
                {
                    kctInstance.IsLaunchSiteControllerBound = true;
                    KCTDebug.Log("Attempting to disable launchsite specific buttons");
                    UILaunchsiteController controller = UnityEngine.Object.FindObjectOfType<UILaunchsiteController>();
                    if (controller != null)
                    {
                        controller.locked = true;
                    }
                }
            }
        }

        public static bool IsVabRecoveryAvailable()
        {
            string reqTech = PresetManager.Instance.ActivePreset.GeneralSettings.VABRecoveryTech;
            return HighLogic.LoadedSceneIsFlight && FlightGlobals.ActiveVessel != null &&
                   FlightGlobals.ActiveVessel.IsRecoverable &&
                   FlightGlobals.ActiveVessel.IsClearToSave() == ClearToSaveStatus.CLEAR && 
                   (FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH ||
                    string.IsNullOrEmpty(reqTech) ||
                    ResearchAndDevelopment.GetTechnologyState(reqTech) == RDTech.State.Available);
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
