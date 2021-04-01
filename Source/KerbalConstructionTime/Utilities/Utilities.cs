using KSP.UI;
using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace KerbalConstructionTime
{
    public static class Utilities
    {
        private static bool? _isKSCSwitcherInstalled = null;
        private static bool? _isKRASHInstalled = null;
        private static bool? _isPrincipiaInstalled = null;
        private static bool? _isTestFlightInstalled = null;
        private static bool? _isTestLiteInstalled = null;

        private static PropertyInfo _piTFInstance;
        private static PropertyInfo _piTFSettingsEnabled;
        private static Type _tlSettingsType;
        private static FieldInfo _fiTLSettingsDisabled;

        private static DateTime _startedFlashing;
        internal const string _legacyDefaultKscId = "Stock";
        internal const string _defaultKscId = "us_cape_canaveral";
        internal const string _iconPath = "RP-0/PluginData/Icons/";
        internal const string _icon_KCT_Off_24 = _iconPath + "KCT_off-24";
        internal const string _icon_KCT_Off_38 = _iconPath + "KCT_off-38";
        internal const string _icon_KCT_On_24 = _iconPath + "KCT_on-24";
        internal const string _icon_KCT_On_38 = _iconPath + "KCT_on-38";
        internal const string _icon_KCT_Off = _iconPath + "KCT_off";
        internal const string _icon_KCT_On = _iconPath + "KCT_on";
        internal const string _icon_plane = _iconPath + "KCT_flight";
        internal const string _icon_rocket = _iconPath + "KCT_rocket";
        internal const string _icon_settings = _iconPath + "KCT_setting";

        public static AvailablePart GetAvailablePartByName(string partName) => PartLoader.getPartInfoByName(partName);

        /// <summary>
        /// Returns cost in BPs, used to calculate the ingame time to build the vessel.
        /// </summary>
        /// <param name="parts"></param>
        /// <returns></returns>
        public static double GetBuildTime(List<Part> parts) => GetBuildTime(GetEffectiveCost(parts));
        public static double GetBuildTime(List<ConfigNode> parts) => GetBuildTime(GetEffectiveCost(parts));
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

        // A little silly, but made to mirror ShipConstruction.GetPartCostsAndMass
        private static void GetPartCostsAndMass(Part p, out float dryCost, out float fuelCost, out float dryMass, out float fuelMass)
        {
            dryCost = (float)GetPartCosts(p, false);
            fuelCost = (float)GetPartCosts(p) - dryCost;
            dryMass = p.mass;
            fuelMass = p.GetResourceMass();
        }

        private static double GetEffectiveCostInternal(object o, HashSet<string> globalMods, IList<Part> inventorySample)
        {
            if (!(o is Part) && !(o is ConfigNode))
                return 0;
            if (globalMods == null || inventorySample == null)
                return 0;

            string name = (o as Part)?.partInfo.name ?? GetPartNameFromNode(o as ConfigNode);
            Part partRef = o as Part ?? GetAvailablePartByName(name).partPrefab;

            float dryCost;
            float fuelCost;
            float dryMass;
            float fuelMass;

            if (o is ConfigNode)
                ShipConstruction.GetPartCostsAndMass(o as ConfigNode, GetAvailablePartByName(name), out dryCost, out fuelCost, out dryMass, out fuelMass);
            else
                Utilities.GetPartCostsAndMass(partRef, out dryCost, out fuelCost, out dryMass, out fuelMass);

            float wetMass = dryMass + fuelMass;
            float cost = dryCost + fuelCost;

            double PartMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetPartVariable(name);
            double ModuleMultiplier = ApplyModuleCostModifiers(partRef, out bool applyResourceMods);

            // Resource contents may not match the prefab (ie, ModularFuelTanks implementation)
            double ResourceMultiplier = 1d;
            if (applyResourceMods)
            {
                if (o is ConfigNode)
                {
                    var resourceNames = new List<string>();
                    foreach (ConfigNode rNode in (o as ConfigNode).GetNodes("RESOURCE"))
                        resourceNames.Add(rNode.GetValue("name"));
                    ResourceMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetResourceVariable(resourceNames);
                }
                else
                    ResourceMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetResourceVariable(partRef.Resources);
            }

            GatherGlobalModifiers(globalMods, partRef);

            double InvEff = inventorySample.Contains(partRef) ? PresetManager.Instance.ActivePreset.TimeSettings.InventoryEffect : 0;
            int builds = ScrapYardWrapper.GetBuildCount(partRef);
            int used = ScrapYardWrapper.GetUseCount(partRef);

            //C=cost, c=dry cost, M=wet mass, m=dry mass, U=part tracker, O=overall multiplier, I=inventory effect (0 if not in inv), B=build effect
            double effectiveCost = MathParser.GetStandardFormulaValue("EffectivePart",
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
                inventorySample.Remove(partRef);

            if (effectiveCost < 0)
                effectiveCost = 0;
            return effectiveCost;
        }

        public static double GetEffectiveCost(List<Part> parts)
        {
            //get list of parts that are in the inventory
            IList<Part> inventorySample = ScrapYardWrapper.GetPartsInInventory(parts, ScrapYardWrapper.ComparisonStrength.STRICT) ?? new List<Part>();
            var globalVariables = new HashSet<string>();
            double totalEffectiveCost = 0;
            foreach (Part p in parts)
            {
                totalEffectiveCost += GetEffectiveCostInternal(p, globalVariables, inventorySample);
            }

            double globalMultiplier = ApplyGlobalCostModifiers(globalVariables);
            return totalEffectiveCost * globalMultiplier;
        }


        public static double GetEffectiveCost(List<ConfigNode> parts)
        {
            //get list of parts that are in the inventory
            var apList = new List<Part>();
            foreach (ConfigNode n in parts)
            {
                if (GetPartNameFromNode(n) is string pName &&
                    GetAvailablePartByName(pName) is AvailablePart ap)
                    apList.Add(ap.partPrefab);
            }

            //IList<ConfigNode> inventorySample = ScrapYardWrapper.GetPartsInInventory(parts, ScrapYardWrapper.ComparisonStrength.STRICT) ?? new List<ConfigNode>();
            IList<Part> inventorySample = ScrapYardWrapper.GetPartsInInventory(apList, ScrapYardWrapper.ComparisonStrength.STRICT) ?? new List<Part>();
            var globalVariables = new HashSet<string>();
            double totalEffectiveCost = 0;
            foreach (ConfigNode p in parts)
            {
                totalEffectiveCost += GetEffectiveCostInternal(p, globalVariables, inventorySample);
            }

            double globalMultiplier = ApplyGlobalCostModifiers(globalVariables);
            return totalEffectiveCost * globalMultiplier;
        }

        public static void GatherGlobalModifiers(HashSet<string> modifiers, Part p)
        {
            PresetManager.Instance.ActivePreset.PartVariables.SetGlobalVariables(modifiers, p.Modules);
            if (p.Modules.GetModule<ModuleTagList>() is ModuleTagList pm)
                foreach (var x in pm.tags)
                    if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod) && mod.globalMult != 1)
                        modifiers.Add(mod.name);
        }

        public static double ApplyGlobalCostModifiers(HashSet<string> modifiers)
        {
            double res = PresetManager.Instance.ActivePreset.PartVariables.GetGlobalVariable(modifiers.ToList());
            foreach (var x in modifiers)
                if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod))
                    res *= mod.globalMult;
            return res;
        }

        public static double ApplyModuleCostModifiers(Part p, out bool useResourceMult)
        {
            double res = PresetManager.Instance.ActivePreset.PartVariables.GetModuleVariable(p.Modules, out useResourceMult);
            if (p.Modules.GetModule<ModuleTagList>() is ModuleTagList pm)
                foreach (var x in pm.tags)
                {
                    if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod))
                        res *= mod.partMult;

                    useResourceMult &= !x.Equals("NoResourceCostMult", StringComparison.OrdinalIgnoreCase);
                }
            return res;
        }

        public static string GetPartNameFromNode(ConfigNode part)
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
                double fuel = includeFuel ? (rsc.maxAmount - rsc.amount) : rsc.maxAmount;
                cost -= fuel * def.unitCost;
            }
            return cost;
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

            if (PresetManager.Instance.ActivePreset.GeneralSettings.CommonBuildLine)
            {
                return GetBuildRate(ship.KSC.BuildList.IndexOf(ship), BuildListVessel.ListType.VAB, ship.KSC);
            }
            else
            {
                if (ship.Type == BuildListVessel.ListType.VAB)
                    return GetBuildRate(ship.KSC.VABList.IndexOf(ship), ship.Type, ship.KSC);
                else if (ship.Type == BuildListVessel.ListType.SPH)
                    return GetBuildRate(ship.KSC.SPHList.IndexOf(ship), ship.Type, ship.KSC);
            }

            return 0;
        }

        public static List<double> GetVABBuildRates(KSCItem KSC)
        {
            if (KSC == null) KSC = KCTGameStates.ActiveKSC;
            return KSC.VABRates;
        }

        public static List<double> GetSPHBuildRates(KSCItem KSC)
        {
            if (KSC == null) KSC = KCTGameStates.ActiveKSC;
            return KSC.SPHRates;
        }

        public static double GetVABBuildRateSum(KSCItem KSC)
        {
            double rateTotal = 0;
            foreach (var rate in GetVABBuildRates(KSC))
                rateTotal += rate;
            return rateTotal;
        }

        public static double GetSPHBuildRateSum(KSCItem KSC)
        {
            double rateTotal = 0;
            foreach (var rate in GetSPHBuildRates(KSC))
                rateTotal += rate;
            return rateTotal;
        }

        public static double GetBothBuildRateSum(KSCItem KSC)
        {
            double rateTotal = GetVABBuildRateSum(KSC);
            if (!PresetManager.Instance.ActivePreset.GeneralSettings.CommonBuildLine)
                rateTotal += GetSPHBuildRateSum(KSC);

            return rateTotal;
        }

        public static float GetTotalVesselCost(ProtoVessel vessel, bool includeFuel = true)
        {
            float total = 0, totalDry = 0;
            foreach (ProtoPartSnapshot part in vessel.protoPartSnapshots)
            {
                total += ShipConstruction.GetPartCosts(part, part.partInfo, out float dry, out float wet);
                totalDry += dry;
            }
            return includeFuel ? total : totalDry;
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
            string name = GetPartNameFromNode(part);
            if (!(GetAvailablePartByName(name) is AvailablePart aPart))
                return 0;
            ShipConstruction.GetPartCostsAndMass(part, aPart, out float dryCost, out float fuelCost, out _, out _);
            return includeFuel ? dryCost + fuelCost : dryCost;
        }

        public static float GetPartMassFromNode(ConfigNode part, bool includeFuel = true, bool includeClamps = true)
        {
            AvailablePart aPart = GetAvailablePartByName(GetPartNameFromNode(part));

            if (aPart == null || (!includeClamps && (aPart?.partPrefab?.Modules.Contains<LaunchClamp>() == true)))
                return 0;
            ShipConstruction.GetPartCostsAndMass(part, aPart, out _, out _, out float dryMass, out float fuelMass);
            return includeFuel ? dryMass + fuelMass : dryMass;
        }

        public static float GetShipMass(this ShipConstruct sc, bool excludeClamps, out float dryMass, out float fuelMass)
        {
            dryMass = 0f;
            fuelMass = 0f;
            foreach (var part in sc.parts)
            {
                AvailablePart partInfo = part.partInfo;

                if (excludeClamps && part.partInfo.partPrefab.Modules.Contains<LaunchClamp>())
                    continue;

                float partDryMass = partInfo.partPrefab.mass + part.GetModuleMass(partInfo.partPrefab.mass, ModifierStagingSituation.CURRENT);
                float partFuelMass = 0f;
                foreach (var resource in part.Resources)
                {
                    partFuelMass += resource.info.density * (float)resource.amount;
                }
                dryMass += partDryMass;
                fuelMass += partFuelMass;
            }
            return dryMass + fuelMass;
        }

        public static string GetTweakScaleSize(ProtoPartSnapshot part)
        {
            string partSize = string.Empty;
            if (part?.modules?.Find(mod => mod.moduleName == "TweakScale") is ProtoPartModuleSnapshot tweakscale)
            {
                ConfigNode tsCN = tweakscale.moduleValues;
                string defaultScale = tsCN.GetValue("defaultScale");
                string currentScale = tsCN.GetValue("currentScale");
                if (!defaultScale.Equals(currentScale))
                    partSize = "," + currentScale;
            }
            return partSize;
        }

        public static string GetTweakScaleSize(ConfigNode part)
        {
            string partSize = string.Empty;
            if (part.HasNode("MODULE") &&
                part.GetNodes("MODULE").FirstOrDefault(m => m.GetValue("name") == "TweakScale") is ConfigNode tsCN)
            {
                string defaultScale = tsCN.GetValue("defaultScale");
                string currentScale = tsCN.GetValue("currentScale");
                if (!defaultScale.Equals(currentScale))
                    partSize = "," + currentScale;
            }
            return partSize;
        }

        public static string GetTweakScaleSize(Part part)
        {
            string partSize = "";
            if (part?.Modules?.GetModule("TweakScale") is PartModule tweakscale)
            {
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

        /// <summary>
        /// Use this method instead of Planetarium.GetUniversalTime().
        /// Fixes the KSP stupidity where wrong UT can be returned when reverting back to the Editor.
        /// </summary>
        /// <returns></returns>
        public static double GetUT()
        {
            return HighLogic.LoadedSceneIsEditor ? HighLogic.CurrentGame.UniversalTime : Planetarium.GetUniversalTime();
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
            if (ship.Type != BuildListVessel.ListType.VAB && ship.Type != BuildListVessel.ListType.SPH)
                return;
            var list = ship.Type == BuildListVessel.ListType.VAB ? ship.KSC.VABList : ship.KSC.SPHList;
            MoveVesselToWarehouse(ship.Type, list.IndexOf(ship), ship.KSC);
        }

        public static void MoveVesselToWarehouse(BuildListVessel.ListType ListIdentifier, int index, KSCItem KSC)
        {
            if (KSC == null) KSC = KCTGameStates.ActiveKSC;
            if (ListIdentifier != BuildListVessel.ListType.VAB && ListIdentifier != BuildListVessel.ListType.SPH)
                return;

            KCTEvents.Instance.KCTButtonStockImportant = true;
            _startedFlashing = DateTime.Now;    //Set the time to start flashing

            var Message = new StringBuilder();
            Message.AppendLine("The following vessel is complete:");
            BuildListVessel vessel;
            string stor = ListIdentifier == BuildListVessel.ListType.VAB ? "VAB" : "SPH";
            if (ListIdentifier == BuildListVessel.ListType.VAB)
            {
                vessel = KSC.VABList[index];
                KSC.VABList.RemoveAt(index);
                KSC.VABWarehouse.Add(vessel);
            }
            else
            {
                vessel = KSC.SPHList[index];
                KSC.SPHList.RemoveAt(index);
                KSC.SPHWarehouse.Add(vessel);
            }
            Message.AppendLine(vessel.ShipName);
            Message.AppendLine($"Please check the {stor} Storage at {KSC.KSCName} to launch it.");

            //Assign science based on science rate
            if (CurrentGameHasScience() && !vessel.CannotEarnScience)
            {
                double rate = MathParser.GetStandardFormulaValue("Research", new Dictionary<string, string>() { { "N", KSC.RDUpgrades[0].ToString() }, { "R", GetBuildingUpgradeLevel(SpaceCenterFacility.ResearchAndDevelopment).ToString() } });
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

            KCTDebug.Log($"Moved vessel {vessel.ShipName} to {KSC.KSCName}'s {stor} storage.");

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

            EnsureCurrentSaveHasSciTotalsInitialized(changeDelta);
            float pointsBef = Math.Max(0, KCTGameStates.SciPointsTotal);

            KCTGameStates.SciPointsTotal += changeDelta;
            KCTDebug.Log("Total sci points earned is now: " + KCTGameStates.SciPointsTotal);

            double upgradesBef = MathParser.GetStandardFormulaValue("UpgradesForScience", new Dictionary<string, string>() { { "N", pointsBef.ToString() } });
            double upgradesAft = MathParser.GetStandardFormulaValue("UpgradesForScience", new Dictionary<string, string>() { { "N", KCTGameStates.SciPointsTotal.ToString() } });
            KCTDebug.Log($"Upg points bef: {upgradesBef}; aft: {upgradesAft}");

            int upgradesToAdd = (int)upgradesAft - (int)upgradesBef;
            if (upgradesToAdd > 0)
            {
                KCT_GUI.ResetUpgradePointCounts();
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

        public static BuildListVessel AddVesselToBuildList() => AddVesselToBuildList(EditorLogic.fetch.launchSiteName);
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
                //Check if vessel fails facility checks but can still be built
                List<string> facilityChecks = blv.MeetsFacilityRequirements(true);
                if (facilityChecks.Count != 0)
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "editorChecksFailedPopup",
                        "Failed editor checks!",
                        "Warning! This vessel did not pass the editor checks! It will still be built, but you will not be able to launch it without upgrading. Listed below are the failed checks:\n"
                        + string.Join("\n", facilityChecks.Select(s => $"• {s}").ToArray()),
                        "Acknowledged",
                        false,
                        HighLogic.UISkin);
                }

                //Check if vessel contains locked or experimental parts, and therefore cannot be built
                Dictionary<AvailablePart, int> lockedParts = blv.GetLockedParts();
                if (lockedParts?.Count > 0)
                {
                    KCTDebug.Log($"Tried to add {blv.ShipName} to build list but it contains locked parts.");

                    //Simple ScreenMessage since there's not much you can do other than removing the locked parts manually.
                    var lockedMsg = ConstructLockedPartsWarning(lockedParts);
                    var msg = new ScreenMessage(lockedMsg, 4f, ScreenMessageStyle.UPPER_CENTER);
                    ScreenMessages.PostScreenMessage(msg);
                    return null;
                }
                Dictionary<AvailablePart, int> devParts = blv.GetExperimentalParts();
                if (devParts?.Count > 0)
                {
                    var devMsg = ConstructExperimentalPartsWarning(devParts);

                    //PopupDialog asking you if you want to pay the entry cost for all the parts that can be unlocked (tech node researched)
                    DialogGUIButton[] buttons;

                    var unlockableParts = devParts.Keys.Where(p => ResearchAndDevelopment.GetTechnologyState(p.TechRequired) == RDTech.State.Available).ToList();
                    int n = unlockableParts.Count();
                    int unlockCost = FindUnlockCost(unlockableParts);
                    string mode = KCTGameStates.EditorShipEditingMode ? "save edits" : "build vessel";
                    if (unlockableParts.Any())
                    {
                        buttons = new DialogGUIButton[] {
                            new DialogGUIButton("Acknowledged", () => { }),
                            new DialogGUIButton($"Unlock {n} part{(n > 1? "s":"")} for {unlockCost} Fund{(unlockCost > 1? "s":"")} and {mode}", () =>
                            {
                                if (Funding.Instance.Funds > unlockCost)
                                {
                                    UnlockExperimentalParts(unlockableParts);
                                    if (!KCTGameStates.EditorShipEditingMode)
                                        AddVesselToBuildList(blv);
                                    else SaveShipEdits(KCTGameStates.EditedVessel);
                                }
                                else
                                {
                                    var msg = new ScreenMessage("Insufficient funds to unlock parts", 5f, ScreenMessageStyle.UPPER_CENTER);
                                    ScreenMessages.PostScreenMessage(msg);
                                }
                            })
                        };
                    }
                    else
                    {
                        buttons = new DialogGUIButton[] {
                            new DialogGUIButton("Acknowledged", () => { })
                        };
                    }

                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog("devPartsCheckFailedPopup",
                            devMsg,
                            "Vessel cannot be built!",
                            HighLogic.UISkin,
                            buttons),
                        false,
                        HighLogic.UISkin);
                    return null;
                }

                double totalCost = blv.GetTotalCost();
                double prevFunds = Funding.Instance.Funds;
                if (totalCost > prevFunds)
                {
                    KCTDebug.Log($"Tried to add {blv.ShipName} to build list but not enough funds.");
                    KCTDebug.Log($"Vessel cost: {GetTotalVesselCost(blv.ShipNode)}, Current funds: {prevFunds}");
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
            bool isCommonLine = PresetManager.Instance?.ActivePreset?.GeneralSettings.CommonBuildLine ?? false;
            string text = isCommonLine ? $"Added {blv.ShipName} to build list." : $"Added {blv.ShipName} to {type} build list.";
            var message = new ScreenMessage(text, 4f, ScreenMessageStyle.UPPER_CENTER);
            ScreenMessages.PostScreenMessage(message);
            return blv;
        }

        public static void SaveShipEdits(BuildListVessel ship)
        {
            AddFunds(ship.GetTotalCost(), TransactionReasons.VesselRollout);
            BuildListVessel newShip = AddVesselToBuildList();
            if (newShip == null)
            {
                SpendFunds(ship.GetTotalCost(), TransactionReasons.VesselRollout);
                return;
            }

            ship.RemoveFromBuildList();

            GetShipEditProgress(ship, out double progressBP, out _, out _);
            newShip.Progress = progressBP;
            newShip.RushBuildClicks = ship.RushBuildClicks;
            KCTDebug.Log($"Finished? {ship.IsFinished}");
            if (ship.IsFinished)
                newShip.CannotEarnScience = true;

            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);

            KCTGameStates.ClearVesselEditMode();

            KCTDebug.Log("Edits saved.");

            HighLogic.LoadScene(GameScenes.SPACECENTER);
        }

        public static Dictionary<string, ProtoTechNode> GetUnlockedProtoTechNodes()
        {
            var protoTechNodes = new Dictionary<string, ProtoTechNode>();
            // get the nodes that have been researched from ResearchAndDevelopment
            foreach (ConfigNode cn in ResearchAndDevelopment.Instance?.snapshot.GetData().GetNodes("Tech") ?? Enumerable.Empty<ConfigNode>())
            {
                // save proto nodes that have been researched
                ProtoTechNode protoTechNode = new ProtoTechNode(cn);
                protoTechNodes.Add(protoTechNode.techID, protoTechNode);
            }

            return protoTechNodes;
        }

        public static void GetShipEditProgress(BuildListVessel ship, out double newProgressBP, out double originalCompletionPercent, out double newCompletionPercent)
        {
            double origTotalBP = ship.BuildPoints + ship.IntegrationPoints;
            double newTotalBP = KCTGameStates.EditorBuildTime + KCTGameStates.EditorIntegrationTime;
            double totalBPDiff = Math.Abs(newTotalBP - origTotalBP);
            double oldProgressBP = ship.IsFinished ? origTotalBP : ship.Progress;
            newProgressBP = Math.Max(0, oldProgressBP - (1.1 * totalBPDiff));
            originalCompletionPercent = oldProgressBP / origTotalBP;
            newCompletionPercent = newProgressBP / newTotalBP;
        }

        public static int FindUnlockCost(List<AvailablePart> availableParts)
        {
            Assembly a = AssemblyLoader.loadedAssemblies.FirstOrDefault(la => string.Equals(la.name, "RealFuels", StringComparison.OrdinalIgnoreCase))?.assembly;
            Type t = a?.GetType("RealFuels.EntryCostManager");
            var mi = t?.GetMethod("ConfigEntryCost", new Type[] { typeof(IEnumerable<string>) });
            if (mi != null)    // Older RF versions lack this method
            {
                var pi = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object instance = pi.GetValue(null);
                IEnumerable<string> partNames = availableParts.Select(p => p.name);
                double sum = (double)mi.Invoke(instance, new[] { partNames });
                return (int)sum;
            }
            else
            {
                int cost = 0;
                foreach (var p in availableParts)
                {
                    cost += p.entryCost;
                }
                return cost;
            }
        }

        public static void UnlockExperimentalParts(List<AvailablePart> availableParts)
        {
            foreach (var ap in availableParts)
            {
                ProtoTechNode protoNode = ResearchAndDevelopment.Instance.GetTechState(ap.TechRequired);

                if (!protoNode.partsPurchased.Contains(ap))
                {
                    protoNode.partsPurchased.Add(ap);
                    GameEvents.OnPartPurchased.Fire(ap);
                    HandlePurchase(ap);
                }

                KCTDebug.Log($"{ap.title} is no longer an experimental part. Part was unlocked.");
                RemoveExperimentalPart(ap);
            }

            EditorPartList.Instance?.Refresh();
            EditorPartList.Instance?.Refresh(EditorPartList.State.PartsList);
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
        }

        public static void AddResearchedPartsToExperimental()
        {
            Dictionary<string, ProtoTechNode> protoTechNodes = GetUnlockedProtoTechNodes();

            foreach (var ap in PartLoader.LoadedPartsList)
            {
                if (PartIsUnlockedButNotPurchased(protoTechNodes, ap))
                {
                    AddExperimentalPart(ap);
                }
            }
        }

        public static void RemoveResearchedPartsFromExperimental()
        {
            Dictionary<string, ProtoTechNode> protoTechNodes = GetUnlockedProtoTechNodes();

            foreach (var ap in PartLoader.LoadedPartsList)
            {
                if (PartIsUnlockedButNotPurchased(protoTechNodes, ap))
                {
                    RemoveExperimentalPart(ap);
                }
            }
        }

        public static bool PartIsUnlockedButNotPurchased(Dictionary<string, ProtoTechNode> unlockedProtoTechNodes, AvailablePart ap)
        {
            bool nodeIsInList = unlockedProtoTechNodes.TryGetValue(ap.TechRequired, out ProtoTechNode ptn);
            if (!nodeIsInList) return false;

            bool nodeIsUnlocked = ptn.state == RDTech.State.Available;
            bool partNotPurchased = !ptn.partsPurchased.Contains(ap);

            return nodeIsUnlocked && partNotPurchased;
        }

        public static bool AddExperimentalPart(AvailablePart ap)
        {
            if (ap is null || !CurrentGameIsCareer() || ResearchAndDevelopment.IsExperimentalPart(ap))
                return false;

            ResearchAndDevelopment.AddExperimentalPart(ap);
            return true;
        }

        public static bool RemoveExperimentalPart(AvailablePart ap)
        {
            if (ap is null || !CurrentGameIsCareer())
                return false;

            ResearchAndDevelopment.RemoveExperimentalPart(ap);
            return true;
        }

        public static void HandlePurchase(AvailablePart partInfo)
        {
            ProtoTechNode techState = ResearchAndDevelopment.Instance.GetTechState(partInfo.TechRequired);

            foreach (var name in partInfo.identicalParts.Split(','))
            {
                if (PartLoader.getPartInfoByName(name.Replace('_', '.').Trim()) is AvailablePart info
                    && info.TechRequired == partInfo.TechRequired)
                {
                    info.costsFunds = false;
                    techState.partsPurchased.Add(info);
                    GameEvents.OnPartPurchased.Fire(info);
                    info.costsFunds = true;
                }
            }
        }

        private static void _checkTime(in IKCTBuildItem item, ref double shortestTime, ref IKCTBuildItem closest)
        {
            if (item.IsComplete()) return;
            double time = item.GetTimeLeft();
            if (time < shortestTime)
            {
                closest = item;
                shortestTime = time;
            }
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
                    _checkTime(blv, ref shortestTime, ref thing);
                foreach (IKCTBuildItem blv in KSC.SPHList)
                    _checkTime(blv, ref shortestTime, ref thing);
                foreach (IKCTBuildItem rr in KSC.Recon_Rollout)
                    _checkTime(rr, ref shortestTime, ref thing);
                foreach (IKCTBuildItem ap in KSC.AirlaunchPrep)
                    _checkTime(ap, ref shortestTime, ref thing);
                foreach (IKCTBuildItem ub in KSC.KSCTech)
                    _checkTime(ub, ref shortestTime, ref thing);
            }
            foreach (TechItem tech in KCTGameStates.TechList)
            {
                if (tech.GetBlockingTech(KCTGameStates.TechList) == null)   // Ignore items that are blocked
                    _checkTime(tech, ref shortestTime, ref thing);
            }
            return thing;
        }

        public static void DisableModFunctionality()
        {
            DisableSimulationLocks();
            InputLockManager.RemoveControlLock(KerbalConstructionTime.KCTLaunchLock);
            KCT_GUI.HideAll();
        }

        public static object GetMemberInfoValue(MemberInfo member, object sourceObject)
        {
            object newVal;
            if (member is FieldInfo info)
                newVal = info.GetValue(sourceObject);
            else
                newVal = ((PropertyInfo)member).GetValue(sourceObject, null);
            return newVal;
        }

        public static int GetTotalSpentUpgrades(KSCItem ksc = null)
        {
            if (ksc == null) ksc = KCTGameStates.ActiveKSC;
            int spentPoints = 0;
            if (PresetManager.Instance.ActivePreset.GeneralSettings.SharedUpgradePool)
            {
                foreach (var KSC in KCTGameStates.KSCs)
                {
                    foreach (var vabPoints in KSC.VABUpgrades) spentPoints += vabPoints;
                    foreach (var sphPoints in KSC.SPHUpgrades) spentPoints += sphPoints;
                    spentPoints += KSC.RDUpgrades[0];
                }
                spentPoints += ksc.RDUpgrades[1]; //only count this once, all KSCs share this value
            }
            else
            {
                foreach (var vabPoints in ksc.VABUpgrades) spentPoints += vabPoints;
                foreach (var sphPoints in ksc.SPHUpgrades) spentPoints += sphPoints;
                foreach (var rndPoints in ksc.RDUpgrades) spentPoints += rndPoints;
            }
            return spentPoints;
        }

        public static int GetSpentUpgradesFor(SpaceCenterFacility facility, KSCItem ksc = null)
        {
            if (ksc == null) ksc = KCTGameStates.ActiveKSC;
            int spentPoints = 0;
            switch (facility)
            {
                case SpaceCenterFacility.ResearchAndDevelopment:
                    if (PresetManager.Instance.ActivePreset.GeneralSettings.SharedUpgradePool)
                    {
                        foreach (var KSC in KCTGameStates.KSCs)
                            spentPoints += KSC.RDUpgrades[0];
                        spentPoints += ksc.RDUpgrades[1]; //only count this once, all KSCs share this value
                    }
                    else
                    {
                        foreach (var rndPoints in ksc.RDUpgrades) spentPoints += rndPoints;
                    }
                    break;
                case SpaceCenterFacility.SpaceplaneHangar:
                    if (PresetManager.Instance.ActivePreset.GeneralSettings.SharedUpgradePool)
                    {
                        foreach (var KSC in KCTGameStates.KSCs)
                        {
                            foreach (var points in KSC.SPHUpgrades) spentPoints += points;
                        }
                    }
                    else
                    {
                        foreach (var points in ksc.SPHUpgrades) spentPoints += points;
                    }
                    break;
                case SpaceCenterFacility.VehicleAssemblyBuilding:
                    if (PresetManager.Instance.ActivePreset.GeneralSettings.SharedUpgradePool)
                    {
                        foreach (var KSC in KCTGameStates.KSCs)
                        {
                            foreach (var points in KSC.VABUpgrades) spentPoints += points;
                        }
                    }
                    else
                    {
                        foreach (var points in ksc.VABUpgrades) spentPoints += points;
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

        public static bool IsSimulationActive
        {
            get
            {
                if (KCTGameStates.IsSimulatedFlight) return true;

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
            if (!IsKSCSwitcherInstalled) return null;

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
                return null;
            object sitesObj = GetMemberInfoValue(Loader.GetMember("Sites")[0], LoaderInstance);
            string lastSite = (string)GetMemberInfoValue(sitesObj.GetType().GetMember("lastSite")[0], sitesObj);

            if (lastSite == string.Empty)
                lastSite = (string)GetMemberInfoValue(sitesObj.GetType().GetMember("defaultSite")[0], sitesObj);
            return lastSite;
        }

        public static void SetActiveKSCToRSS()
        {
            Profiler.BeginSample("KCT SetActiveKSCToRSS");
            string site = GetActiveRSSKSC();
            SetActiveKSC(site);
            Profiler.EndSample();
        }

        public static void SetActiveKSC(string site)
        {
            if (string.IsNullOrEmpty(site))
                site = _defaultKscId;
            if (KCTGameStates.ActiveKSC == null || site != KCTGameStates.ActiveKSC.KSCName)
            {
                KCTDebug.Log($"Setting active site to {site}");
                KSCItem setActive = KCTGameStates.KSCs.FirstOrDefault(ksc => ksc.KSCName == site);
                if (setActive != null)
                {
                    SetActiveKSC(setActive);
                }
                else
                {
                    setActive = new KSCItem(site);
                    if (CurrentGameIsCareer())
                        setActive.ActiveLPInstance.level = 0;
                    KCTGameStates.KSCs.Add(setActive);
                    SetActiveKSC(setActive);
                }
            }
        }

        public static void SetActiveKSC(KSCItem ksc)
        {
            if (ksc == null) return;

            KCTGameStates.ActiveKSC = ksc;
            KCTGameStates.ActiveKSCName = ksc.KSCName;
        }

        public static PQSCity FindKSC(CelestialBody home)
        {
            if (home?.pqsController?.transform?.Find("KSC") is Transform t &&
                t.GetComponent(typeof(PQSCity)) is PQSCity KSC)
            {
                return KSC;
            }

            return Resources.FindObjectsOfTypeAll<PQSCity>().FirstOrDefault(x => x.name == "KSC");
        }

        public static void DisplayMessage(string title, StringBuilder text, MessageSystemButton.MessageButtonColor color, MessageSystemButton.ButtonIcons icon)
        {
            var m = new MessageSystem.Message(title, text.ToString(), color, icon);
            MessageSystem.Instance.AddMessage(m);
        }

        public static bool IsLaunchFacilityIntact(BuildListVessel.ListType type)
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
            KCTGameStates.EditorIntegrationCosts = MathParser.ParseIntegrationCostFormula(kctVessel);

            if (EditorDriver.editorFacility == EditorFacility.VAB)
            {
                KCTGameStates.EditorRolloutCosts = MathParser.ParseRolloutCostFormula(kctVessel);
                KCTGameStates.EditorRolloutTime = MathParser.ParseReconditioningFormula(kctVessel, false);
            }
            else
            {
                // SPH lacks rollout times and costs
                KCTGameStates.EditorRolloutCosts = 0;
                KCTGameStates.EditorRolloutTime = 0;
            }
        }

        public static bool IsApproximatelyEqual(double d1, double d2, double error = 0.01)
        {
            return (1 - error) <= (d1 / d2) && (d1 / d2) <= (1 + error);
        }

        public static float GetParachuteDragFromPart(AvailablePart parachute)
        {
            foreach (AvailablePart.ModuleInfo mi in parachute.moduleInfos)
            {
                if (mi.info.Contains("Fully-Deployed Drag"))
                {
                    string[] split = mi.info.Split(new char[] { ':', '\n' });
                    //TODO: Get SR code and put that in here, maybe with TryParse instead of Parse
                    for (int i = 0; i < split.Length; i++)
                    {
                        if (split[i].Contains("Fully-Deployed Drag"))
                        {
                            if (!float.TryParse(split[i + 1], out float drag))
                            {
                                string[] split2 = split[i + 1].Split('>');
                                if (!float.TryParse(split2[1], out drag))
                                {
                                    KCTDebug.Log("Failure trying to read parachute data. Assuming 500 drag.");
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
            return KSC.GetReconditioning(launchSite) is ReconRollout;
        }

        public static BuildListVessel FindBLVesselByID(Guid id)
        {
            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                if (FindBLVesselByID(id, ksc) is BuildListVessel blv)
                    return blv;
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
            string partName = GetPartNameFromNode(partNode);
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

            bool partIsExperimental = ResearchAndDevelopment.IsExperimentalPart(partInfoByName);

            return partIsUnlocked || partIsExperimental;
        }

        public static bool PartIsExperimental(string partName)
        {
            if (partName == null) return false;

            AvailablePart partInfoByName = PartLoader.getPartInfoByName(partName);
            if (partInfoByName == null) return false;

            return ResearchAndDevelopment.IsExperimentalPart(partInfoByName);
        }

        public static bool PartIsExperimental(ConfigNode partNode)
        {
            string partName = GetPartNameFromNode(partNode);
            return PartIsExperimental(partName);
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
                for (int i = 0; i < part.Modules.Count; i++)
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

            var sb = StringBuilderCache.Acquire();
            sb.Append("Warning! This vessel cannot be built. It contains parts which are not available at the moment:\n");

            foreach (KeyValuePair<AvailablePart, int> kvp in lockedPartsOnShip)
            {
                sb.Append($" <color=orange><b>{kvp.Value}x {kvp.Key.title}</b></color>\n");
            }

            return sb.ToStringAndRelease();
        }

        public static string ConstructExperimentalPartsWarning(Dictionary<AvailablePart, int> devPartsOnShip)
        {
            if (devPartsOnShip == null || devPartsOnShip.Count == 0)
                return null;

            var sb = StringBuilderCache.Acquire();
            sb.Append("This vessel contains parts that are still in development. ");
            if (devPartsOnShip.Any(kvp => ResearchAndDevelopment.GetTechnologyState(kvp.Key.TechRequired) == RDTech.State.Available))
                sb.Append("Green parts have been researched and can be unlocked.\n");
            else
                sb.Append("\n");

            foreach (KeyValuePair<AvailablePart, int> kvp in devPartsOnShip)
            {
                if (ResearchAndDevelopment.GetTechnologyState(kvp.Key.TechRequired) == RDTech.State.Available)
                    sb.Append($" <color=green><b>{kvp.Value}x {kvp.Key.title}</b></color>\n");
                else
                    sb.Append($" <color=orange><b>{kvp.Value}x {kvp.Key.title}</b></color>\n");
            }

            return sb.ToStringAndRelease();
        }

        public static int GetBuildingUpgradeLevel(SpaceCenterFacility facility)
        {
            int lvl = GetBuildingUpgradeMaxLevel(facility);
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                lvl = (int)Math.Round(lvl * ScenarioUpgradeableFacilities.GetFacilityLevel(facility));
            }
            return lvl;
        }

        public static int GetBuildingUpgradeLevel(string facilityID)
        {
            int lvl = GetBuildingUpgradeMaxLevel(facilityID);
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
            {
                lvl = (int)Math.Round(lvl * ScenarioUpgradeableFacilities.GetFacilityLevel(facilityID));
            }
            return lvl;
        }

        public static int GetBuildingUpgradeMaxLevel(string facilityID)
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

        public static int GetBuildingUpgradeMaxLevel(SpaceCenterFacility facility)
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

        public static int GetTotalUpgradePoints()
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
            KCTDebug.Log($"Removed {referencesRemoved} invalid symmetry references.");
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
                InputLockManager.SetControlLock(ControlTypes.EDITOR_LAUNCH, KerbalConstructionTime.KCTLaunchLock);
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

        public static bool IsSphRecoveryAvailable()
        {
            return HighLogic.LoadedSceneIsFlight && FlightGlobals.ActiveVessel != null &&
                   FlightGlobals.ActiveVessel.IsRecoverable &&
                   FlightGlobals.ActiveVessel.IsClearToSave() == ClearToSaveStatus.CLEAR;
        }

        public static void EnableSimulationLocks()
        {
            InputLockManager.SetControlLock(ControlTypes.QUICKSAVE, "KCTLockSimQS");
            InputLockManager.SetControlLock(ControlTypes.QUICKLOAD, "KCTLockSimQL");
        }

        public static void DisableSimulationLocks()
        {
            InputLockManager.RemoveControlLock("KCTLockSimQS");
            InputLockManager.RemoveControlLock("KCTLockSimQL");
        }

        public static void MakeSimulationSave()
        {
            KCTDebug.Log("Making simulation backup file.");
            GamePersistence.SaveGame("KCT_simulation_backup", HighLogic.SaveFolder, SaveMode.OVERWRITE);
        }

        public static bool SimulationSaveExists()
        {
            return File.Exists($"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/KCT_simulation_backup.sfs");
        }

        /// <summary>
        /// Copies the simulation save to /Backup/ folder and deletes it from the main savegame folder.
        /// </summary>
        public static void DeleteSimulationSave()
        {
            string preSimFile = $"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/KCT_simulation_backup.sfs";
            string backupFolderPath = $"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/Backup";
            string backupFile = $"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/Backup/KCT_simulation_backup.sfs";

            Directory.CreateDirectory(backupFolderPath);
            File.Delete(backupFile);
            File.Move(preSimFile, backupFile);
        }

        public static void LoadSimulationSave(bool useNewMethod)
        {
            string backupFile = $"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/KCT_simulation_backup.sfs";
            string saveFile = $"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/persistent.sfs";
            DisableSimulationLocks();

            if (FlightGlobals.fetch != null)
            {
                FlightGlobals.PersistentVesselIds.Clear();
                FlightGlobals.PersistentLoadedPartIds.Clear();
                FlightGlobals.PersistentUnloadedPartIds.Clear();
            }

            KCTDebug.Log("Swapping persistent.sfs with simulation backup file.");
            if (useNewMethod)
            {
                ConfigNode lastShip = ShipConstruction.ShipConfig;
                EditorFacility lastEditor = HighLogic.CurrentGame.editorFacility;

                Game newGame = GamePersistence.LoadGame("KCT_simulation_backup", HighLogic.SaveFolder, true, false);
                GamePersistence.SaveGame(newGame, "persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                GameScenes targetScene = HighLogic.LoadedScene;
                newGame.startScene = targetScene;

                // This has to be before... newGame.Start()
                if (targetScene == GameScenes.EDITOR)
                {
                    newGame.editorFacility = lastEditor;
                }
                newGame.Start();

                // ... And this has to be after. <3 KSP
                if (targetScene == GameScenes.EDITOR)
                {
                    EditorDriver.StartupBehaviour = EditorDriver.StartupBehaviours.LOAD_FROM_CACHE;
                    ShipConstruction.ShipConfig = lastShip;
                }
            }
            else
            {
                File.Copy(backupFile, saveFile, true);
                Game newGame = GamePersistence.LoadGame("KCT_simulation_backup", HighLogic.SaveFolder, true, false);
                GameEvents.onGameStatePostLoad.Fire(newGame.config);
            }

            DeleteSimulationSave();
        }

        public static bool IsTestFlightInstalled
        {
            get
            {
                if (!_isTestFlightInstalled.HasValue)
                {
                    Assembly a = AssemblyLoader.loadedAssemblies.FirstOrDefault(la => string.Equals(la.name, "TestFlightCore", StringComparison.OrdinalIgnoreCase))?.assembly;
                    _isTestFlightInstalled = a != null;
                    if (_isTestFlightInstalled.Value)
                    {
                        Type t = a.GetType("TestFlightCore.TestFlightManagerScenario");
                        _piTFInstance = t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                        _piTFSettingsEnabled = t?.GetProperty("SettingsEnabled", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    }
                }
                return _isTestFlightInstalled.Value;
            }
        }

        public static bool IsTestLiteInstalled
        {
            get
            {
                if (!_isTestLiteInstalled.HasValue)
                {
                    Assembly a = AssemblyLoader.loadedAssemblies.FirstOrDefault(la => string.Equals(la.name, "TestLite", StringComparison.OrdinalIgnoreCase))?.assembly;
                    _isTestLiteInstalled = a != null;
                    if (_isTestLiteInstalled.Value)
                    {
                        _tlSettingsType = a.GetType("TestLite.TestLiteGameSettings");
                        _fiTLSettingsDisabled = _tlSettingsType?.GetField("disabled");
                    }
                }
                return _isTestLiteInstalled.Value;
            }
        }

        public static void ToggleFailures(bool isEnabled)
        {
            if (IsTestFlightInstalled) ToggleTFFailures(isEnabled);
            else if (IsTestLiteInstalled) ToggleTLFailures(isEnabled);
        }

        public static void ToggleTFFailures(bool isEnabled)
        {
            object tfInstance = _piTFInstance.GetValue(null);
            _piTFSettingsEnabled.SetValue(tfInstance, isEnabled);
        }

        private static void ToggleTLFailures(bool isEnabled)
        {
            _fiTLSettingsDisabled.SetValue(HighLogic.CurrentGame.Parameters.CustomParams(_tlSettingsType), !isEnabled);
            GameEvents.OnGameSettingsApplied.Fire();
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
