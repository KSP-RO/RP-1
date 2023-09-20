using KSP.UI;
using KSP.UI.Screens;
using RP0;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UniLinq;
using UnityEngine;
using UnityEngine.Profiling;

namespace KerbalConstructionTime
{
    public static class Utilities
    {
        private static bool? _isPrincipiaInstalled = null;
        private static bool? _isTestFlightInstalled = null;
        private static bool? _isTestLiteInstalled = null;

        private static PropertyInfo _piTFInstance;
        private static PropertyInfo _piTFSettingsEnabled;
        private static Type _tlSettingsType;
        private static FieldInfo _fiTLSettingsDisabled;

        private static DateTime _startedFlashing;
        internal const string _iconPath = "RP-1/PluginData/Icons/";
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

        public static string GetPartNameFromNode(ConfigNode part)
        {
            string name = part.GetValue("part");
            if (name != null)
                name = name.Split('_')[0];
            else
                name = part.GetValue("name");
            return name;
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
            if (!LC.IsOperational)
                return 0d;

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
            int engCap = ship.LC.MaxEngineersFor(ship);
            int delta = 0;
            if (engCap < ship.LC.Engineers)
                delta = engCap - ship.LC.Engineers;

            return GetBuildRate(ship.LC.BuildList.IndexOf(ship), ship.Type, ship.LC, ship.humanRated, delta);
        }

        public static double GetConstructionRate(int index, KSCItem KSC, SpaceCenterFacility facilityType)
        {
            return Formula.GetConstructionBuildRate(index, KSC, facilityType);
        }

        public static double GetResearcherEfficiencyMultipliers()
        {
            return PresetManager.Instance.ActivePreset.GeneralSettings.ResearcherEfficiency;
        }

        public static bool IsClamp(Part part)
        {
            return part.FindModuleImplementing<LaunchClamp>() != null || part.HasTag(ModuleTagList.PadInfrastructure);
        }

        public static bool IsClampOrChild(this ProtoPartSnapshot p)
        {
            return IsClamp(p.partPrefab) || (p.parent != null && IsClampOrChild(p.parent));
        }

        public static bool IsClampOrChild(this Part p)
        {
            return IsClamp(p) || (p.parent != null && IsClampOrChild(p.parent));
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

        public static float GetTotalVesselCost(List<Part> parts, bool includeFuel = true)
        {
            Profiler.BeginSample("RP0SaveShip");
            float total = 0f;
            float resCost = 0f;
            int count = parts.Count;
            while (count-- > 0)
            {
                Part part = parts[count];
                AvailablePart partInfo = part.partInfo;
                float dryCost = partInfo.cost + part.GetModuleCosts(partInfo.cost);
                int resCount = part.Resources.Count;
                while (resCount-- > 0)
                {
                    PartResource partResource = part.Resources[resCount];
                    PartResourceDefinition info = partResource.info;
                    dryCost -= info.unitCost * (float)partResource.maxAmount;
                    resCost += info.unitCost * (float)partResource.amount;
                }
                total += dryCost;
            }
            if (includeFuel)
                total += resCost;

            Profiler.EndSample();
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
                if (IsClamp(aPart.partPrefab))
                    return 0;
            }
            ShipConstruction.GetPartCostsAndMass(part, aPart, out _, out _, out float dryMass, out float fuelMass);
            return includeFuel ? dryMass + fuelMass : dryMass;
        }

        public static float GetShipMass(this ShipConstruct sc, bool excludeClamps, out float dryMass, out float fuelMass)
        {
            Profiler.BeginSample("RP0GetShipMass");
            dryMass = 0f;
            fuelMass = 0f;
            foreach (var part in sc.parts)
            {
                AvailablePart partInfo = part.partInfo;

                if (excludeClamps)
                {
                    if (part.IsClampOrChild())
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
            Profiler.EndSample();
            return dryMass + fuelMass;
        }

        // Reimplemented from stock so we ignore tags.
        public static Vector3 GetShipSize(ShipConstruct ship, bool excludeClamps, bool excludeChutes)
        {
            if (ship.parts.Count == 0)
                return Vector3.zero;

            Profiler.BeginSample("RP0GetShipSize");

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
                    if (p.IsClampOrChild())
                        continue;
                }
                if (excludeChutes)
                {
                    if (p.Modules["RealChuteModule"] != null)
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

            Profiler.EndSample();
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

        public static void MoveVesselToWarehouse(BuildListVessel ship)
        {
            KCTEvents.Instance.KCTButtonStockImportant = true;
            _startedFlashing = DateTime.Now;    //Set the time to start flashing

            ship.LC.BuildList.Remove(ship);
            ship.LC.Warehouse.Add(ship);
            ship.LC.RecalculateBuildRates();

            KCTDebug.Log($"Moved vessel {ship.shipName} to {ship.KSC.KSCName}'s {ship.LC.Name} storage.");

            KCT_GUI.ResetBLWindow(false);
            if (!KCTGameStates.Settings.DisableAllMessages)
            {
                var Message = new StringBuilder();
                Message.AppendLine("The following vessel is complete:");
                Message.AppendLine(ship.shipName);
                Message.AppendLine($"Please check the Storage at {ship.LC.Name} at {ship.KSC.KSCName} to launch it.");
                DisplayMessage("Vessel Complete!", Message, MessageSystemButton.MessageButtonColor.GREEN, MessageSystemButton.ButtonIcons.COMPLETE);
            }

            MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
        }

        public static double SpendFunds(double toSpend, TransactionReasons reason)
        {
            if (!CurrentGameIsCareer())
                return 0;
            KCTDebug.Log($"Removing funds: {toSpend}, New total: {Funding.Instance.Funds - toSpend}");
            Funding.Instance.AddFunds(-toSpend, reason);
            return Funding.Instance.Funds;
        }

        public static double SpendFunds(double toSpend, TransactionReasonsRP0 reason)
        {
            return SpendFunds(toSpend, reason.Stock());
        }

        public static double AddFunds(double toAdd, TransactionReasons reason)
        {
            if (!CurrentGameIsCareer())
                return 0;
            KCTDebug.Log($"Adding funds: {toAdd}, New total: {Funding.Instance.Funds + toAdd}");
            Funding.Instance.AddFunds(toAdd, reason);
            return Funding.Instance.Funds;
        }

        public static double AddFunds(double toAdd, TransactionReasonsRP0 reason)
        {
            return AddFunds(toAdd, reason.Stock());
        }

        public static void ProcessSciPointTotalChange(float changeDelta)
        {
            // Earned point totals shouldn't decrease. This would only make sense when done through the cheat menu.
            if (changeDelta <= 0f || KCTGameStates.IsRefunding) return;

            EnsureCurrentSaveHasSciTotalsInitialized(changeDelta);
            float pointsBef = Math.Max(0, KerbalConstructionTimeData.Instance.SciPointsTotal);

            KerbalConstructionTimeData.Instance.SciPointsTotal += changeDelta;
            KCTDebug.Log("Total sci points earned is now: " + KerbalConstructionTimeData.Instance.SciPointsTotal);

            //double upgradesBef = ApplicantPacketsForScience(pointsBef);
            //double upgradesAft = ApplicantPacketsForScience(KerbalConstructionTimeData.Instance.SciPointsTotal);
            //KCTDebug.Log($"Upg points bef: {upgradesBef}; aft: {upgradesAft}");

            //int upgradesToAdd = (int)upgradesAft - (int)upgradesBef;
            //if (upgradesToAdd > 0)
            //{
            //    int numWorkers = upgradesToAdd * LCItem.EngineersPerPacket;
            //    KerbalConstructionTimeData.Instance.UnassignedPersonnel += numWorkers;
            //    KCTDebug.Log($"Added {numWorkers} workers from science points");
            //    ScreenMessages.PostScreenMessage($"Inspired by our latest scientific discoveries, {numWorkers} workers join the program!", 8f, ScreenMessageStyle.UPPER_LEFT);
            //}
        }

        public static void EnsureCurrentSaveHasSciTotalsInitialized(float changeDelta)
        {
            if (KerbalConstructionTimeData.Instance.SciPointsTotal == -1f)
            {
                KCTDebug.Log("Trying to determine total science points for current save...");

                float totalSci = 0f;
                foreach (TechItem t in KerbalConstructionTimeData.Instance.TechList)
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
                KerbalConstructionTimeData.Instance.SciPointsTotal = totalSci;
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
                    if (KCTGameStates.ActiveKSC.IsAnyLCOperational)
                        dialogStr = $"a launch complex. Please switch to a launch complex and try again.";
                    else
                        dialogStr = $"a launch complex. You must build a launch complex (or wait for a launch complex to finish building or renovating) before you can integrate this vessel.";

                }
                else
                {
                    if (KCTGameStates.ActiveKSC.Hangar.IsOperational)
                        dialogStr = $"the Hangar. Please switch to the Hangar as active launch complex and try again.";
                    else
                        dialogStr = $"the Hangar. You must wait for the Hangar to finish renovating before you can integrate this vessel.";
                }

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "editorChecksFailedPopup",
                        "Wrong Launch Complex!",
                            $"Warning! This vessel needs to be built in {dialogStr}",
                        "Acknowledged",
                        false,
                        HighLogic.UISkin);
                return;
            }

            var blv = new BuildListVessel(EditorLogic.fetch.ship, launchSite, EditorLogic.FlagURL, true)
            {
                shipName = EditorLogic.fetch.shipNameField.text
            };

            TryAddVesselToBuildList(blv);
        }

        public static void TryAddVesselToBuildList(BuildListVessel blv, bool skipPartChecks = false, LCItem overrideLC = null)
        {
            if (overrideLC != null)
                blv.LCID = overrideLC.ID;

            var v = new VesselBuildValidator
            {
                CheckPartAvailability = !skipPartChecks,
                CheckPartConfigs = !skipPartChecks,
                SuccessAction = AddVesselToBuildList
            };
            v.ProcessVessel(blv);
        }

        public static void AddVesselToBuildList(BuildListVessel blv) => AddVesselToBuildList(blv, true);

        public static void AddVesselToBuildList(BuildListVessel blv, bool spendFunds)
        {
            if (spendFunds)
                SpendFunds(blv.GetTotalCost(), TransactionReasonsRP0.VesselPurchase);

            if (blv.Type == BuildListVessel.ListType.SPH)
                blv.launchSite = "Runway";
            else
                blv.launchSite = "LaunchPad";

            LCItem lc = blv.LC;
            if (lc != null)
            {
                lc.BuildList.Add(blv);
            }
            else
            {
                KCTDebug.LogError($"Error! Tried to add {blv.shipName} to build list but couldn't find LC! KSC {KCTGameStates.ActiveKSC.KSCName} and active LC {KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance}");
                return;
            }

            try
            {
                KCTEvents.OnVesselAddedToBuildQueue.Fire(blv);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            KCTDebug.Log($"Added {blv.shipName} to build list at {lc.Name} at {KCTGameStates.ActiveKSC.KSCName}. Cost: {blv.cost}. IntegrationCost: {blv.integrationCost}");
            KCTDebug.Log("Launch site is " + blv.launchSite);
            string text = $"Added {blv.shipName} to integration list at {lc.Name}.";
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
            var postEditShip = new BuildListVessel(EditorLogic.fetch.ship, launchSite, EditorLogic.FlagURL, true)
            {
                shipName = EditorLogic.fetch.shipNameField.text,
                FacilityBuiltIn = editableShip.FacilityBuiltIn,
                KCTPersistentID = editableShip.KCTPersistentID,
                LCID = editableShip.LCID
            };

            double usedShipsCost = editableShip.GetTotalCost();
            foreach (BuildListVessel v in KerbalConstructionTimeData.Instance.MergedVessels)
            {
                usedShipsCost += v.GetTotalCost();
                v.RemoveFromBuildList(out _);
            }

            var validator = new VesselBuildValidator();
            validator.CostOffset = usedShipsCost;
            validator.SuccessAction = (postEditShip2) => SaveShipEdits(usedShipsCost, editableShip, postEditShip2);
            validator.FailureAction = () => {; };

            validator.ProcessVessel(postEditShip);
        }

        private static void SaveShipEdits(double oldCost, BuildListVessel editableShip, BuildListVessel newShip)
        {
            double costDelta;
            if (CurrentGameIsCareer() && (costDelta = oldCost - newShip.cost) != 0d)
            {
                Funding.Instance.AddFunds((float)costDelta, TransactionReasonsRP0.VesselPurchase.Stock());
            }

            AddVesselToBuildList(newShip, false);

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
            newShip.progress = progressBP;
            KCTDebug.Log($"Finished? {editableShip.IsFinished}");
            if (editableShip.IsFinished)
                newShip.cannotEarnScience = true;

            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);

            KCTGameStates.ClearVesselEditMode();

            KCTDebug.Log("Edits saved.");

            HighLogic.LoadScene(GameScenes.SPACECENTER);
        }

        public static void GetShipEditProgress(BuildListVessel ship, out double newProgressBP, out double originalCompletionPercent, out double newCompletionPercent)
        {
            double origTotalBP;
            double oldProgressBP;

            if (KerbalConstructionTimeData.Instance.MergedVessels.Count == 0)
            {
                origTotalBP = ship.buildPoints + ship.integrationPoints;
                oldProgressBP = ship.IsFinished ? origTotalBP : ship.progress;
            }
            else
            {
                double totalEffectiveCost = ship.effectiveCost;
                foreach (BuildListVessel v in KerbalConstructionTimeData.Instance.MergedVessels)
                {
                    totalEffectiveCost += v.effectiveCost;
                }

                origTotalBP = oldProgressBP = Formula.GetIntegrationBP(ship, KerbalConstructionTimeData.Instance.MergedVessels) + Formula.GetVesselBuildPoints(totalEffectiveCost);
                oldProgressBP *= (1 - PresetManager.Instance.ActivePreset.GeneralSettings.MergingTimePenalty);
            }

            double newTotalBP = KerbalConstructionTime.Instance.EditorVessel.buildPoints + KerbalConstructionTime.Instance.EditorVessel.integrationPoints;
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
            RP0.UnlockCreditHandler.Instance.SpendCreditAndCost(availableParts);

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
            if (HighLogic.LoadedSceneIsEditor)
                KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
        }

        public static void AddResearchedPartsToExperimental()
        {
            if (ResearchAndDevelopment.Instance == null) return;

            foreach (var ap in PartLoader.LoadedPartsList)
            {
                if (PartIsUnlockedButNotPurchased(ap))
                {
                    AddExperimentalPart(ap);
                }
            }
        }

        public static void AddNodePartsToExperimental(string techID)
        {
            foreach (var ap in PartLoader.LoadedPartsList)
            {
                if (ap.TechRequired == techID && PartIsUnlockedButNotPurchased(ap))
                {
                    AddExperimentalPart(ap);
                }
            }
        }

        public static void RemoveResearchedPartsFromExperimental()
        {
            foreach (var ap in PartLoader.LoadedPartsList)
            {
                if (PartIsUnlockedButNotPurchased(ap))
                {
                    RemoveExperimentalPart(ap);
                }
            }
        }

        public static bool PartIsUnlockedButNotPurchased(AvailablePart ap)
        {
            bool nodeIsInList = ResearchAndDevelopment.Instance.protoTechNodes.TryGetValue(ap.TechRequired, out ProtoTechNode ptn);
            if (!nodeIsInList) return KerbalConstructionTimeData.Instance.TechListHas(ap.TechRequired);

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
                    foreach (IKCTBuildItem ap in LC.Airlaunch_Prep)
                        _checkTime(ap, ref shortestTime, ref thing);
                }
                foreach (IKCTBuildItem ub in KSC.Constructions)
                    _checkTime(ub, ref shortestTime, ref thing);
            }
            foreach (TechItem tech in KerbalConstructionTimeData.Instance.TechList)
            {
                if (tech.GetBlockingTech(KerbalConstructionTimeData.Instance.TechList) == null)   // Ignore items that are blocked
                    _checkTime(tech, ref shortestTime, ref thing);
            }
            foreach (IKCTBuildItem course in RP0.Crew.CrewHandler.Instance.TrainingCourses)
                _checkTime(course, ref shortestTime, ref thing);
            if (KerbalConstructionTimeData.Instance.fundTarget.IsValid)
                _checkTime(KerbalConstructionTimeData.Instance.fundTarget, ref shortestTime, ref thing);

            return thing;
        }

        public static void DisableModFunctionality()
        {
            DisableSimulationLocks();
            InputLockManager.RemoveControlLock(KerbalConstructionTime.KCTLaunchLock);
            KCT_GUI.HideAll();
        }

        public static List<string> GetLaunchSites(bool isVAB)
        {
            EditorDriver.editorFacility = isVAB ? EditorFacility.VAB : EditorFacility.SPH;
            EditorDriver.setupValidLaunchSites();
            return EditorDriver.ValidLaunchSites;
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

            LCItem oldLC = KerbalConstructionTime.Instance.EditorVessel.LC;
            var oldFac = KerbalConstructionTime.Instance.EditorVessel.FacilityBuiltIn;

            KerbalConstructionTime.Instance.EditorVessel = new BuildListVessel(ship, EditorLogic.fetch.launchSiteName, EditorLogic.FlagURL, false);
            // override LC in case of vessel editing
            if (KCTGameStates.EditorShipEditingMode)
            {
                KerbalConstructionTime.Instance.EditorVessel.LCID = KerbalConstructionTimeData.Instance.EditedVessel.LCID;
            }
            else
            {
                // Check if we switched editors
                if (oldFac != KerbalConstructionTime.Instance.EditorVessel.FacilityBuiltIn)
                {
                    if (oldFac == EditorFacility.VAB)
                    {
                        if (oldLC.LCType == LaunchComplexType.Pad)
                        {
                            // cache this off -- we swapped editors
                            KerbalConstructionTime.Instance.PreEditorSwapLCID = oldLC.ID;
                        }
                        // the BLV constructor sets our LC type to Hangar. But let's swap to it as well.
                        if (KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.LCType != LaunchComplexType.Hangar && KCTGameStates.ActiveKSC.Hangar.IsOperational)
                        {
                            KCTGameStates.ActiveKSC.SwitchLaunchComplex(KSCItem.HangarIndex);
                        }
                    }
                    else
                    {
                        // Try to recover a pad LC
                        bool swappedLC = false;
                        if (KCTGameStates.ActiveKSC.LaunchComplexCount > 1)
                        {
                            if (KerbalConstructionTime.Instance.PreEditorSwapLCID != Guid.Empty && KCTGameStates.ActiveKSC.SwitchToLaunchComplex(KerbalConstructionTime.Instance.PreEditorSwapLCID))
                            {
                                swappedLC = true;
                            }
                            else
                            {
                                int idx = KCTGameStates.ActiveKSC.GetLaunchComplexIdxToSwitchTo(true, true);
                                if (idx != -1)
                                {
                                    KCTGameStates.ActiveKSC.SwitchLaunchComplex(idx);
                                    swappedLC = true;
                                }
                            }
                            if (swappedLC)
                            {
                                KerbalConstructionTime.Instance.EditorVessel.LC = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
                            }
                        }
                    }
                }
            }

            if (EditorDriver.editorFacility == EditorFacility.VAB)
            {
                KCTGameStates.EditorRolloutCost = Formula.GetRolloutCost(KerbalConstructionTime.Instance.EditorVessel);
                KCTGameStates.EditorRolloutBP = Formula.GetRolloutBP(KerbalConstructionTime.Instance.EditorVessel);
            }
            else
            {
                // SPH lacks rollout times and costs
                KCTGameStates.EditorRolloutCost = 0;
                KCTGameStates.EditorRolloutBP = 0;
            }

            Tuple<float, List<string>> unlockInfo = GetVesselUnlockInfo(ship);
            KCTGameStates.EditorUnlockCosts = unlockInfo.Item1;
            KCTGameStates.EditorRequiredTechs = unlockInfo.Item2;
            RP0.ToolingGUI.GetUntooledPartsAndCost(out _, out float toolingCost);
            KCTGameStates.EditorToolingCosts = toolingCost;

            // It would be better to only do this if necessary, but eh.
            // It's not easy to know if various buried fields in the blv changed.
            // It would *also* be nice to not run the ER before the blv is ready
            // post craft-load, but...also eh. This is fine.
            RP0.Harmony.PatchEngineersReport.UpdateCraftStats();
        }

        public static bool IsApproximatelyEqual(double d1, double d2, double error = 0.01)
        {
            return (1 - error) <= (d1 / d2) && (d1 / d2) <= (1 + error);
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

            BuildListVessel ves = lc.Warehouse.Find(blv => blv.shipID == id);
            if (ves != null)
                return ves;

            ves = lc.BuildList.Find(blv => blv.shipID == id);
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
            try
            {
                KCTDebug.Log("Attempting to recover active vessel to storage.  listType: " + listType);
                GamePersistence.SaveGame("KCT_Backup", HighLogic.SaveFolder, SaveMode.OVERWRITE);

                KerbalConstructionTimeData.Instance.RecoveredVessel = new BuildListVessel(FlightGlobals.ActiveVessel, listType);

                KCTVesselData vData = FlightGlobals.ActiveVessel.GetKCTVesselData();
                KerbalConstructionTimeData.Instance.RecoveredVessel.KCTPersistentID = vData?.VesselID;
                KerbalConstructionTimeData.Instance.RecoveredVessel.FacilityBuiltIn = vData?.FacilityBuiltIn ?? EditorFacility.None;
                KerbalConstructionTimeData.Instance.RecoveredVessel.LCID = vData?.LCID ?? Guid.Empty;
                KerbalConstructionTimeData.Instance.RecoveredVessel.LandedAt = FlightGlobals.ActiveVessel.landedAt;

                //KCT_GameStates.recoveredVessel.type = listType;
                if (listType == BuildListVessel.ListType.SPH)
                    KerbalConstructionTimeData.Instance.RecoveredVessel.launchSite = "Runway";
                else
                    KerbalConstructionTimeData.Instance.RecoveredVessel.launchSite = "LaunchPad";

                //check for symmetry parts and remove those references if they can't be found
                KerbalConstructionTimeData.Instance.RecoveredVessel.RemoveMissingSymmetry();

                // debug, save to a file
                KerbalConstructionTimeData.Instance.RecoveredVessel.UpdateNodeAndSave("KCTVesselSave", false);

                //test if we can actually convert it
                var test = KerbalConstructionTimeData.Instance.RecoveredVessel.CreateShipConstructAndRelease();

                if (test != null)
                    ShipConstruction.CreateBackup(test);
                KCTDebug.Log("Load test reported success = " + (test == null ? "false" : "true"));
                if (test == null)
                {
                    KerbalConstructionTimeData.Instance.RecoveredVessel = new BuildListVessel();
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
                KerbalConstructionTimeData.Instance.RecoveredVessel = new BuildListVessel();
                ShipConstruction.ClearBackups();
                return false;
            }
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
            if (EditorLogic.fetch == null)
                return;

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
                else
                {
                    EditorLogic.fetch.switchEditorBtn.onClick.RemoveListener(OnEditorSwitch);
                    EditorLogic.fetch.switchEditorBtn.onClick.AddListener(OnEditorSwitch);
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

        private static void OnEditorSwitch()
        {
            KerbalConstructionTime.Instance.StartCoroutine(PostEditorSwitch());
        }

        private static System.Collections.IEnumerator PostEditorSwitch()
        {
            yield return new WaitForSeconds(0.1f);
            while (EditorDriver.fetch != null && EditorDriver.fetch.restartingEditor)
                yield return null;
            if (EditorDriver.fetch == null)
                yield break;

            KerbalConstructionTime.Instance.IsEditorRecalcuationRequired = true;
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
            var kctvm = v.FindVesselModuleImplementing<KCTVesselTracker>();
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
            return v.GetKCTVesselData()?.LCID.ToString("N");
        }

        public static string GetVesselLCModID(this Vessel v)
        {
            return v.GetKCTVesselData()?.LCModID.ToString("N");
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

            double ecmCost = -RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.PartOrUpgradeUnlock, -RealFuels.EntryCostManager.Instance.ConfigEntryCost(ecmPartsList));

            runningCost = -(float)RP0.CurrencyUtils.Funds(RP0.TransactionReasonsRP0.PartOrUpgradeUnlock, -runningCost);

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
            KCTDebug.Log($"Scrapping {b.shipName}");
            if (!b.IsFinished)
            {
                b.RemoveFromBuildList(out _);
            }
            else
            {
                b.RemoveFromBuildList(out _);
            }
            AddFunds(b.GetTotalCost(), TransactionReasonsRP0.VesselPurchase);
        }

        public static void ChangeEngineers(LCItem currentLC, int delta)
        {
            currentLC.Engineers += delta;
            KCTEvents.OnPersonnelChange.Fire();
            MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
            KCT_GUI.BuildRateForDisplay = null;
        }

        public static void ChangeEngineers(KSCItem ksc, int delta)
        {
            ksc.Engineers += delta;
            KCTEvents.OnPersonnelChange.Fire();
            MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
        }

        public static void ChangeResearchers(int delta)
        {
            KerbalConstructionTimeData.Instance.Researchers += delta;
            KCTEvents.OnPersonnelChange.Fire();
            MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
        }

        private const double ApplicantsPow = 0.92d;
        public static int ApplicantPacketsForScience(double sci) => (int)(Math.Pow(sci, ApplicantsPow) / 5d);

        public static double ScienceForNextApplicants()
        {
            int applicantsCur = ApplicantPacketsForScience(Math.Max(0d, KerbalConstructionTimeData.Instance.SciPointsTotal));
            return Math.Pow(5d * (applicantsCur + 1d), 1d / ApplicantsPow);
        }

        public static void GetConstructionTooltip(ConstructionBuildItem constr, int i, out string costTooltip, out string identifier)
        {
            identifier = constr.GetItemName() + i;
            costTooltip = $"Remaining Cost: √{constr.RemainingCost:N0}";
            if (constr is LCConstruction lcc)
            {
                if (lcc.lcData.lcType == LaunchComplexType.Pad)
                    costTooltip = $"Tonnage: {LCItem.SupportedMassAsPrettyTextCalc(lcc.lcData.massMax)}\nHuman-Rated: {(lcc.lcData.isHumanRated ? "Yes" : "No")}\n{costTooltip}";

                costTooltip = $"Dimensions: {LCItem.SupportedSizeAsPrettyTextCalc(lcc.lcData.sizeMax)}\n{costTooltip}";
            }
            costTooltip = $"{identifier}¶{costTooltip}";
        }

        private static float[] _padTons = null;
        public static float[] PadTons
        {
            get
            {
                if (_padTons == null)
                    LoadPadData();

                return _padTons;
            }
        }

        private static Vector3[] _padSizes = null;
        public static Vector3[] PadSizes
        {
            get
            {
                if (_padSizes == null)
                    LoadPadData();

                return _padSizes;
            }
        }

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

        public static bool IsVesselKCTRecovering(ProtoVessel v)
        {
            if (v == null)
                return false;

            if (!PresetManager.Instance.ActivePreset.GeneralSettings.Enabled)
                return false;

            if (KerbalConstructionTimeData.Instance.IsSimulatedFlight)
                return false;

            if (v.vesselRef.isEVA)
                return false;

            // Is also called at the start of the flight scene when recovering clamps & debris
            if (KerbalConstructionTimeData.Instance.RecoveredVessel?.IsValid != true)
            {
                KCTDebug.Log("Recovered vessel is null!");
                return false;
            }

            if (v.vesselName != KerbalConstructionTimeData.Instance.RecoveredVessel.shipName)
            {
                KCTDebug.Log($"Recovered vessel '{v.vesselName}' and '{KerbalConstructionTimeData.Instance.RecoveredVessel.shipName}' do not match ");
                return false;
            }

            return true;
        }

        public static int GetFacilityLevel(SpaceCenterFacility facility)
        {
            if (ScenarioUpgradeableFacilities.facilityStrings.TryGetValue(facility, out string str))
                return GetFacilityLevel(str);

            return GetFacilityLevel(facility.ToString());
        }

        public static int GetFacilityLevel(string facilityId)
        {
            facilityId = ScenarioUpgradeableFacilities.SlashSanitize(facilityId);
            if (!ScenarioUpgradeableFacilities.protoUpgradeables.TryGetValue(facilityId, out var value))
                return 0;

            if (value.facilityRefs.Count < 1)
                return 0;

            return value.facilityRefs[0].facilityLevel;
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
