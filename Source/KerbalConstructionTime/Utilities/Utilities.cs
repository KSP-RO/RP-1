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
        private static FieldInfo _fiKSCSwInstance;
        private static FieldInfo _fiKSCSwSites;
        private static FieldInfo _fiKSCSwLastSite;
        private static FieldInfo _fiKSCSwDefaultSite;

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

        public static double GetVesselBuildPoints(List<ConfigNode> parts) => GetVesselBuildPoints(GetEffectiveCost(parts, out _));

        public static double GetVesselBuildPoints(double totalEffectiveCost)
        {
            var formulaParams = new Dictionary<string, string>()
            {
                { "E", totalEffectiveCost.ToString() },
                { "O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString() }
            };
            //double finalBP = MathParser.GetStandardFormulaValue("BP", formulaParams);
            // 1000 + (([E]^0.95)*216*min(1,max(0.5,([E]-500)/1500))) + ((max(0,[E]-50000)^1.4)*0.864)
            double bpScalar = UtilMath.Clamp((totalEffectiveCost - 500d) / 1500d, 0.5d, 1d);
            double finalBP = 1000d + Math.Pow(totalEffectiveCost, 0.95) * 216 * bpScalar;
            double powScalar = totalEffectiveCost - 50000d;
            if (powScalar > 0)
                finalBP += Math.Pow(totalEffectiveCost, 1.4d) * 0.864d;

            KCTDebug.Log($"BP: {finalBP}");
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
                GetPartCostsAndMass(partRef, out dryCost, out fuelCost, out dryMass, out fuelMass);

            float wetMass = dryMass + fuelMass;
            float cost = dryCost + fuelCost;

            double partMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetPartVariable(name);
            double moduleMultiplier = ApplyModuleCostModifiers(partRef, out bool applyResourceMods);

            // Resource contents may not match the prefab (ie, ModularFuelTanks implementation)
            double resourceMultiplier = 1d;
            if (applyResourceMods)
            {
                if (o is ConfigNode)
                {
                    var resourceNames = new List<string>();
                    foreach (ConfigNode rNode in (o as ConfigNode).GetNodes("RESOURCE"))
                        resourceNames.Add(rNode.GetValue("name"));
                    resourceMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetResourceVariable(resourceNames);
                }
                else
                    resourceMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetResourceVariable(partRef.Resources);
            }

            GatherGlobalModifiers(globalMods, partRef);

            double InvEff = inventorySample.Contains(partRef) ? PresetManager.Instance.ActivePreset.TimeSettings.InventoryEffect : 0;
            int builds = ScrapYardWrapper.GetBuildCount(partRef);
            int used = ScrapYardWrapper.GetUseCount(partRef);

            //C=cost, c=dry cost, M=wet mass, m=dry mass, U=part tracker, O=overall multiplier, I=inventory effect (0 if not in inv), B=build effect
            //double effectiveCost = MathParser.GetStandardFormulaValue("EffectivePart",
            //    new Dictionary<string, string>()
            //    {
            //            {"C", cost.ToString()},
            //            {"c", dryCost.ToString()},
            //            {"M", wetMass.ToString()},
            //            {"m", dryMass.ToString()},
            //            {"U", builds.ToString()},
            //            {"u", used.ToString()},
            //            {"O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString()},
            //            {"I", InvEff.ToString()},
            //            {"B", PresetManager.Instance.ActivePreset.TimeSettings.BuildEffect.ToString()},
            //            {"PV", partMultiplier.ToString()},
            //            {"RV", resourceMultiplier.ToString()},
            //            {"MV", moduleMultiplier.ToString()}
            //    });
            // [PV]*[RV]*[MV]*[C]
            double effectiveCost = partMultiplier * resourceMultiplier * moduleMultiplier * cost;

            if (InvEff != 0)
                inventorySample.Remove(partRef);

            if (HighLogic.LoadedSceneIsEditor)
            {
                double runTime = 0;
                if (o is Part)
                {
                    foreach (PartModule modNode in (o as Part).Modules)
                    {
                        string s = modNode.moduleName;
                        if (s == "TestFlightReliability_EngineCycle")
                            runTime = Convert.ToDouble(modNode.Fields.GetValue("engineOperatingTime"));
                        else if (s == "ModuleTestLite")
                            runTime = Convert.ToDouble(modNode.Fields.GetValue("runTime"));
                        if (runTime > 0)  //There can be more than one TestLite module per part
                            break;
                    }
                }
                else
                {
                    foreach (ConfigNode modNode in (o as ConfigNode).GetNodes("MODULE"))
                    {
                        string s = modNode.GetValue("name");
                        if (s == "TestFlightReliability_EngineCycle")
                            double.TryParse(modNode.GetValue("engineOperatingTime"), out runTime);
                        else if (s == "ModuleTestLite")
                            double.TryParse(modNode.GetValue("runTime"), out runTime);
                        if (runTime > 0) //There can be more than one TestLite module per part
                            break;
                    }
                }
                if (runTime > 0)
                    effectiveCost = Formula.GetEngineRefurbBPMultiplier(runTime) * effectiveCost;
            }

            if (effectiveCost < 0)
                effectiveCost = 0;

            KCTDebug.Log($"Eff cost for {name}: {effectiveCost} (cost: {cost}; dryCost: {dryCost}; wetMass: {wetMass}; dryMass: {dryMass}; partMultiplier: {partMultiplier}; resourceMultiplier: {resourceMultiplier}; moduleMultiplier: {moduleMultiplier})");

            return effectiveCost;
        }

        public static double GetEffectiveCost(List<Part> parts, out bool isHumanRated)
        {
            //get list of parts that are in the inventory
            IList<Part> inventorySample = ScrapYardWrapper.GetPartsInInventory(parts, ScrapYardWrapper.ComparisonStrength.STRICT) ?? new List<Part>();
            var globalVariables = new HashSet<string>();
            double totalEffectiveCost = 0;
            foreach (Part p in parts)
            {
                totalEffectiveCost += GetEffectiveCostInternal(p, globalVariables, inventorySample);
            }

            double globalMultiplier = ApplyGlobalCostModifiers(globalVariables, out isHumanRated);
            double multipliedCost = totalEffectiveCost * globalMultiplier;
            KCTDebug.Log($"Total eff cost: {totalEffectiveCost}; global mult: {globalMultiplier}; multiplied cost: {multipliedCost}");

            return multipliedCost;
        }


        public static double GetEffectiveCost(List<ConfigNode> parts, out bool isHumanRated)
        {
            //get list of parts that are in the inventory
            var apList = new List<Part>();
            foreach (ConfigNode n in parts)
            {
                if (GetPartNameFromNode(n) is string pName &&
                    GetAvailablePartByName(pName) is AvailablePart ap)
                    apList.Add(ap.partPrefab);
            }

            IList<Part> inventorySample = ScrapYardWrapper.GetPartsInInventory(apList, ScrapYardWrapper.ComparisonStrength.STRICT) ?? new List<Part>();
            var globalVariables = new HashSet<string>();
            double totalEffectiveCost = 0;
            foreach (ConfigNode p in parts)
            {
                totalEffectiveCost += GetEffectiveCostInternal(p, globalVariables, inventorySample);
            }

            double globalMultiplier = ApplyGlobalCostModifiers(globalVariables, out isHumanRated);
            double multipliedCost = totalEffectiveCost * globalMultiplier;
            KCTDebug.Log($"Total eff cost: {totalEffectiveCost}; global mult: {globalMultiplier}; multiplied cost: {multipliedCost}");

            return multipliedCost;
        }

        public static void GatherGlobalModifiers(HashSet<string> modifiers, Part p)
        {
            PresetManager.Instance.ActivePreset.PartVariables.SetGlobalVariables(modifiers, p.Modules);
            if (p.Modules.GetModule<ModuleTagList>() is ModuleTagList pm)
                foreach (var x in pm.tags)
                    if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod) && mod.globalMult != 1)
                        modifiers.Add(mod.name);
        }

        public static double ApplyGlobalCostModifiers(HashSet<string> modifiers, out bool isHumanRated)
        {
            isHumanRated = false;
            double res = PresetManager.Instance.ActivePreset.PartVariables.GetGlobalVariable(modifiers.ToList());
            foreach (var x in modifiers)
                if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod))
                {
                    res *= mod.globalMult;
                    isHumanRated |= mod.isHumanRating;
                }
            return res;
        }

        public static double ApplyModuleCostModifiers(Part p, out bool useResourceMult)
        {
            double res = 1;
            useResourceMult = true;
            if (p.Modules.GetModule<ModuleTagList>() is ModuleTagList pm)
            {
                foreach (var x in pm.tags)
                {
                    if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod))
                        res *= mod.partMult;

                    useResourceMult &= !x.Equals("NoResourceCostMult", StringComparison.OrdinalIgnoreCase);
                }
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

        public static double GetBuildRate(int index, LCItem LC, bool isHumanRated, bool forceRecalc)
        {
            bool useCap = LC.IsHumanRated && !isHumanRated;
            // optimization: if we are checking index 0 use the cached rate, otherwise recalc
            if (forceRecalc || index != 0)
            {
                return Formula.GetVesselBuildRate(index, LC, useCap, 0);
            }

            return useCap ? LC.Rate : LC.RateHRCapped;
        }

        public static double GetBuildRate(LCItem LC, double mass, double BP, bool isHumanRated, int delta = 0)
        {
            bool useCap = LC.IsHumanRated && !isHumanRated;
            int engCap = LC.MaxEngineersFor(mass, BP, isHumanRated);
            if (engCap < LC.Engineers + delta)
                delta = engCap - LC.Engineers;

            if (delta != 0)
            {
                return Formula.GetVesselBuildRate(0, LC, useCap, delta);
            }

            return useCap ? LC.RateHRCapped : LC.Rate;
        }

        public static double GetBuildRate(int index, BuildListVessel.ListType type, LCItem LC, bool isHumanRated, int upgradeDelta = 0)
        {
            if (type == BuildListVessel.ListType.VAB ? LC.LCType == LaunchComplexType.Hangar : LC.LCType == LaunchComplexType.Pad)
                return 0.0001d;

            return Formula.GetVesselBuildRate(index, LC, LC.IsHumanRated && !isHumanRated, upgradeDelta);
        }

        public static double GetBuildRate(BuildListVessel ship)
        {
            if (ship.Type == BuildListVessel.ListType.None)
                ship.FindTypeFromLists();

            int engCap = ship.LC.MaxEngineersFor(ship);
            int delta = 0;
            if (engCap < ship.LC.Engineers)
                delta = engCap - ship.LC.Engineers;

            return GetBuildRate(ship.LC.BuildList.IndexOf(ship), ship.Type, ship.LC, ship.IsHumanRated, delta);
        }

        public static double GetConstructionRate(KSCItem KSC)
        {
            return GetConstructionRate(0, KSC, 0);
        }

        public static double GetConstructionRate(int index, KSCItem KSC, int delta)
        {
            return Formula.GetConstructionBuildRate(index, KSC, delta);
        }

        public static double GetEngineerEfficiencyMultipliers(LCItem LC = null)
        {
            double mult = 1d;
            if (LC != null)
                mult *= LC.EfficiencyEngineers;
            mult *= KCTGameStates.EfficiencyEngineers;
            mult *= PresetManager.Instance.ActivePreset.GeneralSettings.EngineerEfficiencyMultiplier;
            
            return mult;
        }

        public static double GetResearcherEfficiencyMultipliers()
        {
            return KCTGameStates.EfficiencyResearchers * PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherEfficiencyMultiplier;
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

            if (aPart == null)
            {
                return 0;
            }
            else if (!includeClamps)
            {
                if (aPart.partPrefab.Modules.Contains<LaunchClamp>() || aPart.partPrefab.HasTag("PadInfrastructure"))
                    return 0;
            }
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

                if (excludeClamps)
                {
                    if (part.Modules.Contains<LaunchClamp>() || part.HasTag("PadInfrastructure"))
                        continue;
                    if (part.parent != null && part.parent.HasTag("PadInfrastructure"))
                        continue;
                }

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

        // Reimplemented from stock so we ignore tags.
        public static Vector3 GetShipSize(ShipConstruct ship, bool excludeClamps)
        {
            if (ship.parts.Count == 0)
                return Vector3.zero;

            Bounds craftBounds = new Bounds();
            Vector3 rootPos = ship.parts[0].orgPos;
            craftBounds.center = rootPos;


            List<Bounds> pBounds = new List<Bounds>();
            Vector3 sz;

            Part p;
            int iC = ship.parts.Count;
            for (int i = 0; i < iC; ++i)
            {
                p = ship.parts[i];
                if (excludeClamps)
                {
                    if (p.Modules.Contains<LaunchClamp>() || p.HasTag("PadInfrastructure"))
                        continue;
                    if (p.parent != null && p.parent.HasTag("PadInfrastructure"))
                        continue;
                }

                Bounds[] bounds = GetPartRendererBounds(p);
                Bounds b;
                Bounds cb;
                int jC = bounds.Length;
                for (int j = 0; j < jC; ++j)
                {
                    b = bounds[j];
                    cb = b;
                    cb.size *= p.boundsMultiplier;
                    sz = cb.size;
                    cb.Expand(p.GetModuleSize(sz));
                    pBounds.Add(b);
                }
            }
            craftBounds = PartGeometryUtil.MergeBounds(pBounds.ToArray(), ship.parts[0].transform.root);

            return craftBounds.size;
        }

        // Reimplemented from stock so we ignore disabled renderers.
        public static Bounds[] GetPartRendererBounds(Part p)
        {
            List<MeshRenderer> mRenderers = p.FindModelComponents<MeshRenderer>();
            List<SkinnedMeshRenderer> smRenderers = p.FindModelComponents<SkinnedMeshRenderer>();

            for (int i = mRenderers.Count - 1; i >= 0; --i)
            {
                if (!mRenderers[i].enabled)
                    mRenderers.RemoveAt(i);
            }

            for (int i = smRenderers.Count - 1; i >= 0; --i)
            {
                if (!smRenderers[i].enabled)
                    smRenderers.RemoveAt(i);
            }

            Bounds[] bs = new Bounds[mRenderers.Count + smRenderers.Count];

            int j = 0;
            for (int i = 0; i < mRenderers.Count; ++i)
            {
                bs[j++] = mRenderers[i].bounds;
            }
            for (int i = 0; i < smRenderers.Count; ++i)
            {
                bs[j++] = smRenderers[i].bounds;
            }
            return bs;
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
                var message = new ScreenMessage($"{science} science added.", 4f, ScreenMessageStyle.UPPER_LEFT);
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
            // will also return if ship.LC==null

            KCTEvents.Instance.KCTButtonStockImportant = true;
            _startedFlashing = DateTime.Now;    //Set the time to start flashing

            ship.LC.BuildList.Remove(ship);
            ship.LC.Warehouse.Add(ship);
            ship.LC.RecalculateBuildRates();

            var Message = new StringBuilder();
            Message.AppendLine("The following vessel is complete:");
            Message.AppendLine(ship.ShipName);
            Message.AppendLine($"Please check the Storage at {ship.LC.Name} at {ship.KSC.KSCName} to launch it.");

            //Add parts to the tracker
            if (!ship.CannotEarnScience) //if the vessel was previously completed, then we shouldn't register it as a new build
            {
                ScrapYardWrapper.RecordBuild(ship.ExtractedPartNodes);
            }

            KCTDebug.Log($"Moved vessel {ship.ShipName} to {ship.KSC.KSCName}'s {ship.LC.Name} storage.");

            KCT_GUI.ResetBLWindow(false);
            if (!KCTGameStates.Settings.DisableAllMessages)
            {
                DisplayMessage("Vessel Complete!", Message, MessageSystemButton.MessageButtonColor.GREEN, MessageSystemButton.ButtonIcons.COMPLETE);
            }

            KCTEvents.OnRP0MaintenanceChanged.Fire();
        }

        public static double SpendFunds(double toSpend, TransactionReasons reason)
        {
            if (!CurrentGameIsCareer())
                return 0;
            KCTDebug.Log($"Removing funds: {toSpend}, New total: {Funding.Instance.Funds - toSpend}");
            Funding.Instance.AddFunds(-toSpend, reason);
            return Funding.Instance.Funds;
        }

        public static double SpendFunds(double toSpend, RP0.TransactionReasonsRP0 reason)
        {
            if (!CurrentGameIsCareer())
                return 0;
            KCTDebug.Log($"Removing funds: {toSpend}, New total: {Funding.Instance.Funds - toSpend}");
            Funding.Instance.AddFunds(-toSpend, (TransactionReasons)reason);
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

            //double upgradesBef = ApplicantPacketsForScience(pointsBef);
            //double upgradesAft = ApplicantPacketsForScience(KCTGameStates.SciPointsTotal);
            //KCTDebug.Log($"Upg points bef: {upgradesBef}; aft: {upgradesAft}");

            //int upgradesToAdd = (int)upgradesAft - (int)upgradesBef;
            //if (upgradesToAdd > 0)
            //{
            //    int numWorkers = upgradesToAdd * LCItem.EngineersPerPacket;
            //    KCTGameStates.UnassignedPersonnel += numWorkers;
            //    KCTDebug.Log($"Added {numWorkers} workers from science points");
            //    ScreenMessages.PostScreenMessage($"Inspired by our latest scientific discoveries, {numWorkers} workers join the program!", 8f, ScreenMessageStyle.UPPER_LEFT);
            //}
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

        public static void TryAddVesselToBuildList() => TryAddVesselToBuildList(EditorLogic.fetch.launchSiteName);

        public static void TryAddVesselToBuildList(string launchSite)
        {
            if (string.IsNullOrEmpty(launchSite))
            {
                launchSite = EditorLogic.fetch.launchSiteName;
            }

            BuildListVessel.ListType type = EditorLogic.fetch.ship.shipFacility == EditorFacility.VAB ? BuildListVessel.ListType.VAB : BuildListVessel.ListType.SPH;

            if ((type == BuildListVessel.ListType.VAB) != (KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LCType == LaunchComplexType.Pad))
            {
                string dialogStr;
                if (type == BuildListVessel.ListType.VAB)
                {
                    if (KCTGameStates.ActiveKSC.GetHighestLevelLaunchComplex() == null)
                        dialogStr = $"a launch complex. You must wait for a launch complex to finish building or renovating before you can build this vessel.";
                    else
                        dialogStr = $"a launch complex. Please switch to a launch complex in the Space Center Management window's Operations tab and try again.";
                }
                else
                {
                    if (KCTGameStates.ActiveKSC.Hangar.IsOperational)
                        dialogStr = $"the Hangar. Please switch to the Hangar in the Space Center Management window's Operations tab and try again.";
                    else
                        dialogStr = $"the Hangar. You must wait for the Hangar to finish renovating before you can build this vessel.";
                }

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "editorChecksFailedPopup",
                        "Wrong Launch Complex!",
                            $"Warning! This vessel needs to be built in {dialogStr}",
                        "Acknowledged",
                        false,
                        HighLogic.UISkin);
                return;
            }

            bool humanRated;
            double effCost = GetEffectiveCost(EditorLogic.fetch.ship.Parts, out humanRated);
            double bp = GetVesselBuildPoints(effCost);
            var blv = new BuildListVessel(EditorLogic.fetch.ship, launchSite, effCost, bp, EditorLogic.FlagURL, humanRated)
            {
                ShipName = EditorLogic.fetch.shipNameField.text
            };

            TryAddVesselToBuildList(blv);
        }

        public static void TryAddVesselToBuildList(BuildListVessel blv, bool skipPartChecks = false)
        {
            var v = new VesselBuildValidator
            {
                CheckPartAvailability = !skipPartChecks,
                CheckPartConfigs = !skipPartChecks,
                SuccessAction = AddVesselToBuildList
            };
            v.ProcessVessel(blv);
        }

        public static void AddVesselToBuildList(BuildListVessel blv)
        {
            SpendFunds(blv.GetTotalCost(), TransactionReasons.VesselRollout);

            if (blv.Type == BuildListVessel.ListType.VAB)
                blv.LaunchSite = "LaunchPad";
            else if (blv.Type == BuildListVessel.ListType.SPH)
                blv.LaunchSite = "Runway";

            LCItem lc = blv.LC;
            if (lc != null)
            {
                lc.BuildList.Add(blv);
            }
            else
            {
                KCTDebug.LogError($"Error! Tried to add {blv.ShipName} to build list but couldn't find LC! KSC {KCTGameStates.ActiveKSC.KSCName} and active LC {KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance}");
                return;
            }

            ScrapYardWrapper.ProcessVessel(blv.ExtractedPartNodes);

            try
            {
                KCTEvents.OnVesselAddedToBuildQueue.Fire(blv);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            KCTDebug.Log($"Added {blv.ShipName} to build list at {lc.Name} at {KCTGameStates.ActiveKSC.KSCName}. Cost: {blv.Cost}. IntegrationCost: {blv.IntegrationCost}");
            KCTDebug.Log("Launch site is " + blv.LaunchSite);
            string text = $"Added {blv.ShipName} to build list at {lc.Name}.";
            var message = new ScreenMessage(text, 4f, ScreenMessageStyle.UPPER_CENTER);
            ScreenMessages.PostScreenMessage(message);
        }

        /// <summary>
        /// Validates and saves the vessel edits as a new buildlist item.
        /// </summary>
        /// <param name="editableShip">Must be the pre-edits (i.e what was initially loaded into the edit session) state of the vessel</param>
        public static void TrySaveShipEdits(BuildListVessel editableShip)
        {
            // Load the current editor state as a fresh BuildListVessel
            string launchSite = EditorLogic.fetch.launchSiteName;
            bool humanRated;
            double effCost = GetEffectiveCost(EditorLogic.fetch.ship.Parts, out humanRated);
            double bp = GetVesselBuildPoints(effCost);
            var postEditShip = new BuildListVessel(EditorLogic.fetch.ship, launchSite, effCost, bp, EditorLogic.FlagURL, humanRated)
            {
                ShipName = EditorLogic.fetch.shipNameField.text
            };

            double usedShipsCost = editableShip.GetTotalCost();
            foreach (BuildListVessel v in KCTGameStates.MergedVessels)
            {
                usedShipsCost += v.GetTotalCost();
                v.RemoveFromBuildList(out _);
            }
            AddFunds(usedShipsCost, TransactionReasons.VesselRollout);

            var validator = new VesselBuildValidator();
            validator.SuccessAction = (postEditShip2) => SaveShipEdits(editableShip, postEditShip2);
            validator.FailureAction = () => SpendFunds(usedShipsCost, TransactionReasons.VesselRollout);

            validator.ProcessVessel(postEditShip);
        }

        private static void SaveShipEdits(BuildListVessel editableShip, BuildListVessel newShip)
        {
            AddVesselToBuildList(newShip);

            newShip.FacilityBuiltIn = editableShip.FacilityBuiltIn;
            newShip.KCTPersistentID = editableShip.KCTPersistentID;
            newShip.LCID = editableShip.LCID;

            int oldIdx;
            editableShip.RemoveFromBuildList(out oldIdx);
            if (KCTGameStates.Settings.InPlaceEdit && oldIdx >= 0)
            {
                // Remove and reinsert at right place.
                // We *could* insert at the right place to start with, but
                // that requires changing AddVesselToBuildList, which is used as
                // a void delegate elsewhere, so...
                List<BuildListVessel> lst = newShip.LC.BuildList;
                lst.RemoveAt(lst.Count - 1);
                lst.Insert(oldIdx, newShip);
            }
            newShip.LC.RecalculateBuildRates();

            GetShipEditProgress(editableShip, out double progressBP, out _, out _);
            newShip.Progress = progressBP;
            newShip.RushBuildClicks = editableShip.RushBuildClicks;
            KCTDebug.Log($"Finished? {editableShip.IsFinished}");
            if (editableShip.IsFinished)
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
            double origTotalBP;
            double oldProgressBP;

            if (KCTGameStates.MergedVessels.Count == 0)
            {
                origTotalBP = ship.BuildPoints + ship.IntegrationPoints;
                oldProgressBP = ship.IsFinished ? origTotalBP : ship.Progress;
            }
            else
            {
                double totalEffectiveCost = ship.EffectiveCost;
                foreach (BuildListVessel v in KCTGameStates.MergedVessels)
                {
                    totalEffectiveCost += v.EffectiveCost;
                }

                origTotalBP = oldProgressBP = Formula.GetIntegrationBP(ship, KCTGameStates.MergedVessels) + GetVesselBuildPoints(totalEffectiveCost);
                oldProgressBP *= (1 - PresetManager.Instance.ActivePreset.TimeSettings.MergingTimePenalty);
            }

            double newTotalBP = KCTGameStates.EditorBuildPoints + KCTGameStates.EditorIntegrationPoints;
            double totalBPDiff = Math.Abs(newTotalBP - origTotalBP);
            newProgressBP = Math.Max(0, oldProgressBP - (1.1 * totalBPDiff));
            originalCompletionPercent = oldProgressBP / origTotalBP;
            newCompletionPercent = newProgressBP / newTotalBP;
        }

        public static int FindUnlockCost(List<AvailablePart> availableParts)
        {
            return (int)RealFuels.EntryCostManager.Instance.EntryCostForParts(availableParts);
        }

        public static void UnlockExperimentalParts(List<AvailablePart> availableParts)
        {
            // this will spend the funds, which is why we set costsFunds=false below.
            RP0.UnlockSubsidyHandler.Instance.SpendSubsidyAndCost(availableParts);

            foreach (var ap in availableParts)
            {
                ProtoTechNode protoNode = ResearchAndDevelopment.Instance.GetTechState(ap.TechRequired);

                if (!protoNode.partsPurchased.Contains(ap))
                {
                    protoNode.partsPurchased.Add(ap);
                    ap.costsFunds = false;
                    GameEvents.OnPartPurchased.Fire(ap);
                    ap.costsFunds = true;
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
            if (item.GetBuildRate() == 0) return;

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
                foreach (LCItem LC in KSC.LaunchComplexes)
                {
                    if (!LC.IsOperational)
                        continue;
                    foreach (IKCTBuildItem blv in LC.BuildList)
                        _checkTime(blv, ref shortestTime, ref thing);
                    foreach (IKCTBuildItem rr in LC.Recon_Rollout)
                        _checkTime(rr, ref shortestTime, ref thing);
                    foreach (IKCTBuildItem ap in LC.AirlaunchPrep)
                        _checkTime(ap, ref shortestTime, ref thing);
                }
                foreach (IKCTBuildItem ub in KSC.Constructions)
                    _checkTime(ub, ref shortestTime, ref thing);
            }
            foreach (TechItem tech in KCTGameStates.TechList)
            {
                if (tech.GetBlockingTech(KCTGameStates.TechList) == null)   // Ignore items that are blocked
                    _checkTime(tech, ref shortestTime, ref thing);
            }
            foreach (IKCTBuildItem course in RP0.Crew.CrewHandler.Instance.ActiveCourses)
                _checkTime(course, ref shortestTime, ref thing);
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
                    Assembly a = AssemblyLoader.loadedAssemblies.FirstOrDefault(la => string.Equals(la.name, "KSCSwitcher", StringComparison.OrdinalIgnoreCase))?.assembly;
                    _isKSCSwitcherInstalled = a != null;
                    if (_isKSCSwitcherInstalled.Value)
                    {
                        Type t = a.GetType("regexKSP.KSCLoader");
                        _fiKSCSwInstance = t?.GetField("instance", BindingFlags.Public | BindingFlags.Static);
                        _fiKSCSwSites = t?.GetField("Sites", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                        t = a.GetType("regexKSP.KSCSiteManager");
                        _fiKSCSwLastSite = t?.GetField("lastSite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                        _fiKSCSwDefaultSite = t?.GetField("defaultSite", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                        if (_fiKSCSwInstance == null || _fiKSCSwSites == null || _fiKSCSwLastSite == null || _fiKSCSwDefaultSite == null)
                        {
                            KCTDebug.LogError("Failed to bind to KSCSwitcher");
                            _isKSCSwitcherInstalled = false;
                        }
                    }
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

            // get the LastKSC.KSCLoader.instance object
            // check the Sites object (KSCSiteManager) for the lastSite, if "" then get defaultSite

            object loaderInstance = _fiKSCSwInstance.GetValue(null);
            if (loaderInstance == null)
                return null;
            object sites = _fiKSCSwSites.GetValue(loaderInstance);
            string lastSite = _fiKSCSwLastSite.GetValue(sites) as string;

            if (lastSite == string.Empty)
                lastSite = _fiKSCSwDefaultSite.GetValue(sites) as string;
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
                KSCItem newKsc = KCTGameStates.KSCs.FirstOrDefault(ksc => ksc.KSCName == site);
                if (newKsc != null)
                {
                    SetActiveKSC(newKsc);
                }
                else
                {
                    newKsc = new KSCItem(site);
                    newKsc.EnsureStartingLaunchComplexes();
                    KCTGameStates.KSCs.Add(newKsc);
                    SetActiveKSC(newKsc);
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

            double effCost = GetEffectiveCost(ship.Parts, out KCTGameStates.EditorIsHumanRated);
            KCTGameStates.EditorBuildPoints = GetVesselBuildPoints(effCost);
            var kctVessel = new BuildListVessel(ship, EditorLogic.fetch.launchSiteName, effCost, KCTGameStates.EditorBuildPoints, EditorLogic.FlagURL, KCTGameStates.EditorIsHumanRated);
            KCTGameStates.EditorShipMass = GetShipMass(ship, true, out _, out _);
            KCTGameStates.EditorShipSize = GetShipSize(ship, true);
            KCTGameStates.EditorIntegrationPoints = kctVessel.IntegrationPoints;
            KCTGameStates.EditorIntegrationCosts = kctVessel.IntegrationCost;

            if (EditorDriver.editorFacility == EditorFacility.VAB)
            {
                KCTGameStates.EditorRolloutCosts = Formula.GetRolloutCost(kctVessel);
                KCTGameStates.EditorRolloutTime = Formula.GetRolloutBP(kctVessel);
            }
            else
            {
                // SPH lacks rollout times and costs
                KCTGameStates.EditorRolloutCosts = 0;
                KCTGameStates.EditorRolloutTime = 0;
            }

            Tuple<float, List<string>> unlockInfo = GetVesselUnlockInfo(ship);
            KCTGameStates.EditorUnlockCosts = unlockInfo.Item1;
            KCTGameStates.EditorRequiredTechs = unlockInfo.Item2;
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

        public static bool ReconditioningActive(LCItem LC, string launchSite = "LaunchPad")
        {
            if (LC == null) LC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
            return LC.GetReconditioning(launchSite) is ReconRollout;
        }

        public static BuildListVessel FindBLVesselByID(LCItem hintLC, Guid id)
        {
            BuildListVessel b;
            if (hintLC != null)
            {
                b = FindBLVesselByIDInLC(id, hintLC);
                if (b != null)
                    return b;
            }

            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                if (FindBLVesselByID(id, ksc) is BuildListVessel blv)
                    return blv;
            }

            return null;
        }

        public static BuildListVessel FindBLVesselByIDInLC(Guid id, LCItem lc)
        {

            BuildListVessel ves = lc.Warehouse.Find(blv => blv.Id == id);
            if (ves != null)
                return ves;

            ves = lc.BuildList.Find(blv => blv.Id == id);
            if (ves != null)
                return ves;

            return null;
        }

        public static BuildListVessel FindBLVesselByID(Guid id, KSCItem ksc)
        {
            if (ksc != null)
            {
                foreach (LCItem lc in ksc.LaunchComplexes)
                {
                    BuildListVessel ves = FindBLVesselByIDInLC(id, lc);
                    if (ves != null)
                        return ves;
                }
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
            return PartIsUnlocked(partInfoByName);
        }

        public static bool PartIsUnlocked(AvailablePart ap)
        {
            if (ap == null) return false;

            string partName = ap.name;
            ProtoTechNode techState = ResearchAndDevelopment.Instance.GetTechState(ap.TechRequired);
            bool partIsUnlocked = techState != null && techState.state == RDTech.State.Available &&
                                  RUIutils.Any(techState.partsPurchased, (a => a.name == partName));

            return partIsUnlocked;
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

        public static bool RecoverActiveVesselToStorage(BuildListVessel.ListType listType)
        {
            var test = new ShipConstruct();
            try
            {
                KCTDebug.Log("Attempting to recover active vessel to storage.  listType: " + listType);
                GamePersistence.SaveGame("KCT_Backup", HighLogic.SaveFolder, SaveMode.OVERWRITE);

                KCTGameStates.RecoveredVessel = new BuildListVessel(FlightGlobals.ActiveVessel, listType);

                KCTVesselData vData = FlightGlobals.ActiveVessel.GetKCTVesselData();
                KCTGameStates.RecoveredVessel.KCTPersistentID = vData?.VesselID;
                KCTGameStates.RecoveredVessel.FacilityBuiltIn = vData?.FacilityBuiltIn ?? EditorFacility.None;
                KCTGameStates.RecoveredVessel.LCID = vData == null ? Guid.Empty : new Guid(vData.LCID);
                KCTGameStates.RecoveredVessel.LandedAt = FlightGlobals.ActiveVessel.landedAt;

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

            var kctInstance = KerbalConstructionTime.Instance as EditorAddon;

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

                EditorLogic.fetch.launchBtn.onClick.RemoveAllListeners();
                EditorLogic.fetch.launchBtn.onClick.AddListener(() => { KerbalConstructionTime.ShowLaunchAlert(null); });

                if (kctInstance == null)
                    return;

                if (!kctInstance.IsLaunchSiteControllerDisabled)
                {
                    kctInstance.IsLaunchSiteControllerDisabled = true;
                    UILaunchsiteController controller = UnityEngine.Object.FindObjectOfType<UILaunchsiteController>();
                    if (controller == null)
                    {
                        KCTDebug.Log("UILaunchsiteController is null");
                    }
                    else
                    {
                        KCTDebug.Log("Killing UILaunchsiteController");
                        UnityEngine.Object.Destroy(controller);
                    }
                }
            }
            else if(kctInstance != null)
            {
                InputLockManager.SetControlLock(ControlTypes.EDITOR_LAUNCH, KerbalConstructionTime.KCTLaunchLock);
                if (!kctInstance.IsLaunchSiteControllerDisabled)
                {
                    kctInstance.IsLaunchSiteControllerDisabled = true;
                    KCTDebug.Log("Attempting to disable launchsite specific buttons");
                    UILaunchsiteController controller = UnityEngine.Object.FindObjectOfType<UILaunchsiteController>();
                    if (controller != null)
                    {
                        controller.locked = true;
                    }
                }
            }
        }

        /// <summary>
        /// Check whether the part has a tag with specified name defined using the ModuleTagList PartModule.
        /// </summary>
        /// <param name="p">Part to check</param>
        /// <param name="tag">Name of the tag to check</param>
        /// <returns>True if Part has ModuleTagList PM and a tag with given name is defined in that PM</returns>
        public static bool HasTag(this Part p, string tag)
        {
            ModuleTagList mTags = p.FindModuleImplementing<ModuleTagList>();
            return mTags?.tags.Contains(tag) ?? false;
        }

        public static KCTVesselData GetKCTVesselData(this Vessel v)
        {
            var kctvm = (KCTVesselTracker)v.vesselModules.FirstOrDefault(vm => vm is KCTVesselTracker);
            return kctvm?.Data;
        }

        public static string GetKCTVesselId(this Vessel v)
        {
            return v.GetKCTVesselData()?.VesselID;
        }

        public static string GetVesselLaunchId(this Vessel v)
        {
            return v.GetKCTVesselData()?.LaunchID;
        }

        public static string GetVesselLCID(this Vessel v)
        {
            return v.GetKCTVesselData()?.LCID;
        }

        public static string GetVesselLCModID(this Vessel v)
        {
            return v.GetKCTVesselData()?.LCModID;
        }

        public static EditorFacility? GetVesselBuiltAt(this Vessel v)
        {
            return v.GetKCTVesselData()?.FacilityBuiltIn;
        }

        public static bool IsVabRecoveryAvailable(Vessel v)
        {
            return v != null && v.IsRecoverable && v.IsClearToSave() == ClearToSaveStatus.CLEAR &&
                   v.GetVesselBuiltAt() != EditorFacility.SPH &&
                   (v.situation == Vessel.Situations.PRELAUNCH || IsVabRecoveryTechResearched());
        }

        public static bool IsSphRecoveryAvailable(Vessel v)
        {
            return v != null && v.IsRecoverable && v.IsClearToSave() == ClearToSaveStatus.CLEAR &&
                   v.GetVesselBuiltAt() != EditorFacility.VAB;
        }

        public static bool IsVabRecoveryTechResearched()
        {
            string reqTech = PresetManager.Instance.ActivePreset.GeneralSettings.VABRecoveryTech;
            return string.IsNullOrEmpty(reqTech) ||
                   ResearchAndDevelopment.GetTechnologyState(reqTech) == RDTech.State.Available;
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

        public static void CleanupDebris(string launchSiteName)
        {
            if (KCTGameStates.Settings.CleanUpKSCDebris)
            {
                PSystemSetup.SpaceCenterFacility launchFacility = PSystemSetup.Instance.GetSpaceCenterFacility(launchSiteName);
                double lat = 0, lon = 0;
                bool foundSite = false;
                if (launchFacility != null)
                {
                    PSystemSetup.SpaceCenterFacility.SpawnPoint sp = launchFacility.GetSpawnPoint(launchSiteName);
                    lat = sp.latitude;
                    lon = sp.longitude;
                    foundSite = true;
                }
                if (!foundSite)
                {
                    LaunchSite launchSite = PSystemSetup.Instance.GetLaunchSite(launchSiteName);
                    if (launchSite != null)
                    {
                        LaunchSite.SpawnPoint sp = launchSite.GetSpawnPoint(launchSiteName);
                        lat = sp.latitude;
                        lon = sp.longitude;
                        foundSite = true;
                    }
                }
                if (foundSite)
                {
                    const string msg = "it was debris cluttering up KSC";
                    foreach (Vessel v in FlightGlobals.Vessels)
                    {
                        // TODO: check isPersistent?
                        if (v.loaded)
                        {
                            if (v.vesselType == VesselType.Debris && v.LandedOrSplashed && v.mainBody == Planetarium.fetch.Home)
                            {
                                if (Math.Abs(v.latitude - lat) < 1d && Math.Abs(v.longitude - lon) < 1d)
                                    v.SetAutoClean(msg);
                            }
                        }
                        else if (v.protoVessel != null)
                        {
                            if (v.protoVessel.vesselType == VesselType.Debris && (v.protoVessel.landed || v.protoVessel.splashed)
                                && FlightGlobals.Bodies[v.protoVessel.orbitSnapShot.ReferenceBodyIndex] == Planetarium.fetch.Home)
                            {
                                if (Math.Abs(v.protoVessel.latitude - lat) < 1d && Math.Abs(v.protoVessel.longitude - lon) < 1d)
                                {
                                    v.SetAutoClean(msg);
                                    v.protoVessel.autoClean = true;
                                    v.protoVessel.autoCleanReason = msg;
                                }
                            }
                        }
                    }
                }
            }
        }

        public static Tuple<float, List<string>> GetVesselUnlockInfo(ShipConstruct ship)
        {
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return new Tuple<float, List<string>>(0, new List<string>());

            // filter the ship parts list to those parts that are not already purchased
            IEnumerable<KeyValuePair<AvailablePart, PartPurchasability>> purchasableParts = GetPartsWithPurchasability(ship.Parts).Where(kvp => kvp.Value.Status == PurchasabilityStatus.Purchasable || kvp.Value.Status == PurchasabilityStatus.Unavailable);
            HashSet<string> ecmPartsList = new HashSet<string>();
            float runningCost = 0;

            // compare the part specified entry cost to the ECM database
            foreach (AvailablePart part in purchasableParts.Select(kvp => kvp.Key))
            {
                int rawCost = part.entryCost;
                double ecmEstCost = RealFuels.EntryCostManager.Instance.ConfigEntryCost(part.name);
                if (rawCost == ecmEstCost)
                {
                    // this part is managed by the ECM, save its name later for a batch request
                    ecmPartsList.Add(part.name);
                }
                else
                {
                    // this part is not in the ECM, take the raw cost
                    runningCost += rawCost;
                }
            }

            // filter down further to those parts that can't be unlocked with our current tech and get the tech names needed
            List<AvailablePart> lockedParts = purchasableParts.Where(kvp => kvp.Value.Status == PurchasabilityStatus.Unavailable).Select(kvp => kvp.Key).ToList();
            HashSet<string> pendingTech = new HashSet<string>(lockedParts.Select(ap => ap.TechRequired));

            // now back through the list looking for upgrades to add to our batch list
            foreach (Part p in ship.Parts)
            {
                foreach (PartModule pm in p.Modules)
                {
                    var types = new[] { typeof(string).MakeByRefType(), typeof(bool).MakeByRefType(), typeof(float).MakeByRefType(), typeof(string).MakeByRefType() };
                    var mi = pm.GetType().GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public, null, types, null);
                    if (mi != null)
                    {
                        var parameters = new object[] { null, null, null, null };
                        bool allSucceeded;
                        try
                        {
                            allSucceeded = (bool)mi.Invoke(pm, parameters);
                        }
                        catch (Exception ex)
                        {
                            KCTDebug.LogError($"Config validation failed for {p.name}");
                            Debug.LogException(ex);
                            allSucceeded = false;
                            parameters[0] = "error occurred, check the logs";
                            parameters[1] = false;
                            parameters[2] = 0f;
                            parameters[3] = string.Empty;
                        }

                        if (allSucceeded)
                            continue;   // if validate passed, this partmodule is already unlocked and purchased, nothing to do

                        bool CanBeResolved = (bool)parameters[1];
                        float CostToResolve = (float)parameters[2];
                        string techName = (string)parameters[3];
                        if (!CanBeResolved && !string.IsNullOrEmpty(techName))
                            pendingTech.Add(techName);

                        // use a helper to get the ECM name, each PartModule type stores it differently
                        string ecmName = ECMHelper.GetEcmNameFromPartModule(pm);
                        if (!string.IsNullOrEmpty(ecmName))
                            ecmPartsList.Add(ecmName);
                    }
                }
            }

            double ecmCost = RealFuels.EntryCostManager.Instance.ConfigEntryCost(ecmPartsList);

            List<string> techList = SortAndFilterTechListForFinalNodes(pendingTech);
            float totalCost = runningCost + Convert.ToSingle(ecmCost);
            KCTDebug.Log($"Vessel parts unlock cost check. Total: {totalCost}, Raw cost: {runningCost}, ECM cost: {ecmCost}");
            return new Tuple<float, List<string>>(totalCost, techList);
        }

        public static List<string> SortAndFilterTechListForFinalNodes(HashSet<string> input)
        {
            HashSet<string> blacklist = new HashSet<string>();
            SortedList<string, string> slist = new SortedList<string, string>();
            foreach(string s in input)
            {
                foreach (string parent in KerbalConstructionTimeData.techNameToParents[s])
                {
                    blacklist.Add(parent);
                }
            }
            foreach (string s in input)
            {
                if (!blacklist.Contains(s))
                {
                    // sort our result, depth into the tree then alpha
                    int depth = KerbalConstructionTimeData.techNameToParents[s].Count();
                    string skey = $"{depth:d2}{s}";
                    if (!slist.ContainsKey(skey))
                        slist.Add(skey, s);
                }
            }

            return slist.Values.ToList();
        }

 


        public static Dictionary<AvailablePart, PartPurchasability> GetPartsWithPurchasability(List<Part> parts)
        {
            var res = new Dictionary<AvailablePart, PartPurchasability>();

            if (ResearchAndDevelopment.Instance == null)
                return res;

            List<AvailablePart> apList = parts.Select(p => p.partInfo).ToList();
            res = GetPartsWithPurchasability(apList);
            return res;
        }

        public static Dictionary<AvailablePart, PartPurchasability> GetPartsWithPurchasability(List<AvailablePart> parts)
        {
            var res = new Dictionary<AvailablePart, PartPurchasability>();
            foreach (AvailablePart part in parts)
            {
                if (res.TryGetValue(part, out PartPurchasability pp))
                {
                    pp.PartCount++;
                }
                else
                {
                    PurchasabilityStatus status = PurchasabilityStatus.Unavailable;
                    if (Utilities.PartIsUnlocked(part))
                        status = PurchasabilityStatus.Purchased;
                    else if (ResearchAndDevelopment.GetTechnologyState(part.TechRequired) == RDTech.State.Available)
                        status = PurchasabilityStatus.Purchasable;
                    res.Add(part, new PartPurchasability(status, 1));
                }
            }
            return res;
        }
        
        public static void ScrapVessel(BuildListVessel b)
        {
            KCTDebug.Log($"Scrapping {b.ShipName}");
            if (!b.IsFinished)
            {
                List<ConfigNode> parts = b.ExtractedPartNodes;
                b.RemoveFromBuildList(out _);

                //only add parts that were already a part of the inventory
                if (ScrapYardWrapper.Available)
                {
                    List<ConfigNode> partsToReturn = new List<ConfigNode>();
                    foreach (ConfigNode partNode in parts)
                    {
                        if (ScrapYardWrapper.PartIsFromInventory(partNode))
                        {
                            partsToReturn.Add(partNode);
                        }
                    }
                    if (partsToReturn.Any())
                    {
                        ScrapYardWrapper.AddPartsToInventory(partsToReturn, false);
                    }
                }
            }
            else
            {
                b.RemoveFromBuildList(out _);
                ScrapYardWrapper.AddPartsToInventory(b.ExtractedPartNodes, false);    //don't count as a recovery
            }
            ScrapYardWrapper.SetProcessedStatus(ScrapYardWrapper.GetPartID(b.ExtractedPartNodes[0]), false);
            Utilities.AddFunds(b.GetTotalCost(), TransactionReasons.VesselRollout);
        }

        public static void ChangeEngineers(LCItem currentLC, int delta)
        {
            currentLC.EfficiencyEngineers = PredictEfficiencyEngineers(currentLC, delta);

            currentLC.Engineers += delta;
            KCTEvents.OnPersonnelChange.Fire();
            KCTEvents.OnRP0MaintenanceChanged.Fire();
        }

        public static void ChangeEngineers(KSCItem ksc, int delta)
        {
            KCTGameStates.EfficiencyEngineers = PredictEfficiencyEngineers(delta);

            ksc.Engineers += delta;
            KCTEvents.OnPersonnelChange.Fire();
            KCTEvents.OnRP0MaintenanceChanged.Fire();
        }

        public static void ChangeResearchers(int delta)
        {
            KCTGameStates.EfficiencyResearchers = PredictEfficiencyResearchers(delta);

            KCTGameStates.Researchers += delta;
            KCTEvents.OnPersonnelChange.Fire();
            KCTEvents.OnRP0MaintenanceChanged.Fire();
        }

        public static double PredictEfficiencyEngineers(LCItem currentLC, int delta)
        {
            double correctedDelta = currentLC.Engineers + delta - currentLC.LastEngineers;
            if (correctedDelta > 0)
                return Math.Min(currentLC.EfficiencyEngineers,
                    ((currentLC.LastEngineers * currentLC.EfficiencyEngineers) + (correctedDelta * PresetManager.Instance.ActivePreset.GeneralSettings.EngineerStartEfficiency))
                        / (currentLC.LastEngineers + correctedDelta));

            return currentLC.EfficiencyEngineers;
        }

        public static double PredictEfficiencyEngineers(int delta)
        {
            double correctedDelta = KCTGameStates.TotalEngineers + delta - KCTGameStates.LastEngineers;
            if (correctedDelta > 0)
                return Math.Min(KCTGameStates.EfficiencyEngineers,
                    ((KCTGameStates.LastEngineers * KCTGameStates.EfficiencyEngineers) + (correctedDelta * PresetManager.Instance.ActivePreset.GeneralSettings.GlobalEngineerStartEfficiency))
                    / (KCTGameStates.LastEngineers + correctedDelta));

            return KCTGameStates.EfficiencyEngineers;
        }

        public static double PredictEfficiencyResearchers(int delta)
        {
            double correctedDelta = KCTGameStates.Researchers + delta - KCTGameStates.LastResearchers;
            if (correctedDelta > 0)
                return Math.Min(KCTGameStates.EfficiencyResearchers,
                    ((KCTGameStates.LastResearchers * KCTGameStates.EfficiencyResearchers) + (correctedDelta * PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherStartEfficiency))
                    / (KCTGameStates.LastResearchers + correctedDelta));

            return KCTGameStates.EfficiencyResearchers;
        }

        private const double MaxSecondsForDayDisplay = 7d * 86400d;
        private const double MaxTimeToDisplay = 100d * 365.25d * 86400d;

        public static string GetColonFormattedTime(double t, double extraTime = 0d, bool flip = false)
        {
            if (double.IsNaN(t) || double.IsInfinity(t))
                return "(infinity)";

            bool shouldUseDate = KCTGameStates.Settings.UseDates && t > MaxSecondsForDayDisplay;
            double timeCheck = (shouldUseDate ^ flip) ? extraTime + t : t;
            if (timeCheck > MaxTimeToDisplay)
                return "(infinity)";

            if (shouldUseDate ^ flip)
                return KSPUtil.dateTimeFormatter.PrintDateCompact(GetUT() + extraTime + t, false, false);

            return MagiCore.Utilities.GetColonFormattedTime(t);
        }

        public static string GetFormattedTime(double t, double extraTime = 0d, bool allowDate = true)
        {
            if (double.IsNaN(t) || double.IsInfinity(t))
                return "(infinity)";

            bool shouldUseDate = KCTGameStates.Settings.UseDates && t > MaxSecondsForDayDisplay && allowDate;
            double timeCheck = shouldUseDate ? extraTime + t : t;
            if (timeCheck > MaxTimeToDisplay)
                return "(infinity)";

            if (shouldUseDate)
                return KSPUtil.dateTimeFormatter.PrintDate(GetUT() + extraTime + t, false, false);

            return MagiCore.Utilities.GetFormattedTime(t);
        }

        public static GUIContent GetColonFormattedTimeWithTooltip(double t, string identifier, double extraTime = 0, bool showEst = false)
        {
            return new GUIContent(showEst ? $"Est: {GetColonFormattedTime(t, extraTime, false)}" : GetColonFormattedTime(t, extraTime, false), $"{identifier}¶{GetColonFormattedTime(t, extraTime, true)}");
        }

        public static string GetTechUnlockTime(TechItem tech)
        {
            double totalTime = 0d;
            double nodeTime = 0d;
            for (int i = 0; i < KCTGameStates.TechList.Count; ++i)
            {
                TechItem techItem = KCTGameStates.TechList[i];
                nodeTime = techItem.GetTimeLeftEst(totalTime);
                totalTime += nodeTime;
                if (techItem == tech)
                    break;
            }

            if (KCTGameStates.Settings.UseDates)
                return $"Node will unlock: {GetFormattedTime(totalTime)} (duration: {GetColonFormattedTime(nodeTime, 0, true)})";
            else
                return $"Node will unlock: {GetFormattedTime(totalTime)} (duration: {GetColonFormattedTime(nodeTime)})";
        }

        private const double ApplicantsPow = 0.92d;
        public static int ApplicantPacketsForScience(double sci) => (int)(Math.Pow(sci, ApplicantsPow) / 5d);

        public static double ScienceForNextApplicants()
        {
            int applicantsCur = ApplicantPacketsForScience(Math.Max(0d, KCTGameStates.SciPointsTotal));
            return Math.Pow(5d * (applicantsCur + 1d), 1d / ApplicantsPow);
        }

        public static void SetPartUpgradeText()
        {
            foreach (PartUpgradeHandler.Upgrade up in PartUpgradeManager.Handler)
            {
                if (up.name.StartsWith("rp0EngineerUpgrade"))
                    up.description = KSP.Localization.Localizer.Format("#rp0EngineerUpgradeText", (PresetManager.Instance.ActivePreset.GeneralSettings.EngineerEfficiencyUpgrades.GetValue(up.techRequired) * 100d).ToString("N0"));
                else if (up.name.StartsWith("rp0ResearcherUpgrade"))
                    up.description = KSP.Localization.Localizer.Format("#rp0ResearcherUpgradeText", (PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherEfficiencyUpgrades.GetValue(up.techRequired) * 100d).ToString("N0"));
            }
        }

        public static void GetConstructionTooltip(ConstructionBuildItem constr, int i, out string costTooltip, out string identifier)
        {
            identifier = constr.GetItemName() + i;
            costTooltip = $"Remaining Cost: √{((constr.Cost - constr.SpentCost) * constr.RushMultiplier):N0}";
            if (constr is LCConstruction lcc)
            {
                if (lcc.LCData.lcType == LaunchComplexType.Pad)
                    costTooltip = $"Tonnage: {LCItem.SupportedMassAsPrettyTextCalc(lcc.LCData.massMax)}\n{costTooltip}";

                costTooltip = $"Dimensions: {LCItem.SupportedSizeAsPrettyTextCalc(lcc.LCData.sizeMax)}\n{costTooltip}";
            }
            costTooltip = $"{identifier}¶{costTooltip}";
        }

        private static float[] _padTons = null;
        private static Vector3[] _padSizes;

        private static void LoadPadData()
        {
            var upgdFacility = KCT_LaunchPad.GetUpgradeableFacilityReference();
            if (upgdFacility == null)
                return;

            var padUpgdLvls = upgdFacility.UpgradeLevels;

            _padSizes = new Vector3[padUpgdLvls.Length];
            _padTons = new float[padUpgdLvls.Length];

            for (int i = 0; i < padUpgdLvls.Length; i++)
            {
                float normalizedLevel = (float)i / (float)upgdFacility.MaxLevel;
                float limit = GameVariables.Instance.GetCraftMassLimit(normalizedLevel, true);
                _padTons[i] = limit;

                Vector3 sizeLimit = GameVariables.Instance.GetCraftSizeLimit(normalizedLevel, true);
                _padSizes[i] = sizeLimit;
            }
        }

        public static double GetPadStats(float tonnageLimit, Vector3 padSize, bool humanRated, out double curPadCost, out double curVABCost, out float fractionalPadLvl)
        {
            fractionalPadLvl = 0f;
            if (tonnageLimit != float.MaxValue)
            {
                double mass = tonnageLimit;
                curPadCost = Math.Max(0d, Math.Pow(mass, 0.65d) * 2000d + Math.Pow(Math.Max(mass - 350, 0), 1.5d) * 2d - 2000d) + 1000d;

                if (_padTons == null)
                {
                    LoadPadData();
                }

                if (_padTons == null)
                {
                    fractionalPadLvl = 0f;
                }
                else
                {
                    float unlimitedTonnageThreshold = 2500f;

                    if (tonnageLimit >= unlimitedTonnageThreshold)
                    {
                        int padLvl = _padTons.Length - 1;
                        fractionalPadLvl = padLvl;
                    }
                    else
                    {
                        for (int i = 1; i < _padTons.Length; i++)
                        {
                            if (tonnageLimit < _padTons[i])
                            {
                                float lowerBound = _padTons[i - 1];
                                float upperBound = Math.Min(_padTons[i], unlimitedTonnageThreshold);
                                float fractionOverFullLvl = (tonnageLimit - lowerBound) / (upperBound - lowerBound);
                                fractionalPadLvl = (i - 1) + fractionOverFullLvl;

                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                // SPH case
                curPadCost = 0f;
                padSize.y *= 5f;
            }
            curVABCost = padSize.sqrMagnitude * 25d + 100d;
            if (humanRated)
            {
                curPadCost *= 1.5d;
                curVABCost *= 2d;
            }

            return curVABCost + curPadCost;
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
