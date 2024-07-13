using CommNet;
using KSP.UI;
using KSP.UI.Screens;
using ROUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UniLinq;
using UnityEngine;
using UnityEngine.Profiling;
using Upgradeables;

namespace RP0
{
    public static class KCTUtilities
    {
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

        public static double GetBuildRate(int index, LaunchComplex LC, bool isHumanRated, bool forceRecalc)
        {
            bool useCap = LC.IsHumanRated && !isHumanRated;
            // optimization: if we are checking index 0 use the cached rate, otherwise recalc
            if (forceRecalc || index != 0)
            {
                return Formula.GetVesselBuildRate(index, LC, useCap, 0);
            }

            return useCap ? LC.Rate : LC.RateHRCapped;
        }

        public static double GetBuildRate(LaunchComplex LC, double mass, double BP, bool isHumanRated, int delta = 0)
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

        public static double GetBuildRate(int index, ProjectType type, LaunchComplex LC, bool isHumanRated, int upgradeDelta = 0)
        {
            if (type == ProjectType.VAB ? LC.LCType == LaunchComplexType.Hangar : LC.LCType == LaunchComplexType.Pad)
                return 0.0001d;

            return Formula.GetVesselBuildRate(index, LC, LC.IsHumanRated && !isHumanRated, upgradeDelta);
        }

        public static double GetBuildRate(VesselProject ship)
        {
            int engCap = ship.LC.MaxEngineersFor(ship);
            int delta = 0;
            if (engCap < ship.LC.Engineers)
                delta = engCap - ship.LC.Engineers;

            return GetBuildRate(ship.LC.BuildList.IndexOf(ship), ship.Type, ship.LC, ship.humanRated, delta);
        }

        public static double GetConstructionRate(int index, LCSpaceCenter KSC, SpaceCenterFacility facilityType)
        {
            return Formula.GetConstructionBuildRate(index, KSC, facilityType);
        }

        public static double GetResearcherEfficiencyMultipliers()
        {
            return Database.SettingsSC.ResearcherEfficiency;
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

        public static double SpendFunds(double toSpend, TransactionReasons reason)
        {
            if (!KSPUtils.CurrentGameIsCareer())
                return 0;
            RP0Debug.Log($"Removing funds: {toSpend}, New total: {Funding.Instance.Funds - toSpend}");
            Funding.Instance.AddFunds(-toSpend, reason);
            return Funding.Instance.Funds;
        }

        public static double SpendFunds(double toSpend, TransactionReasonsRP0 reason)
        {
            return SpendFunds(toSpend, reason.Stock());
        }

        public static double AddFunds(double toAdd, TransactionReasons reason)
        {
            if (!KSPUtils.CurrentGameIsCareer())
                return 0;
            RP0Debug.Log($"Adding funds: {toAdd}, New total: {Funding.Instance.Funds + toAdd}");
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
            if (changeDelta <= 0f || SpaceCenterManagement.IsRefundingScience) return;

            SpaceCenterManagement.Instance.SciPointsTotal += changeDelta;
            RP0Debug.Log("Total sci points earned is now: " + SpaceCenterManagement.Instance.SciPointsTotal);
        }

        public static void TryAddVesselToBuildList() => TryAddVesselToBuildList(EditorLogic.fetch.launchSiteName);

        public static void TryAddVesselToBuildList(string launchSite)
        {
            if (string.IsNullOrEmpty(launchSite))
            {
                launchSite = EditorLogic.fetch.launchSiteName;
            }

            ProjectType type = EditorLogic.fetch.ship.shipFacility == EditorFacility.VAB ? ProjectType.VAB : ProjectType.SPH;

            if ((type == ProjectType.VAB) != (SpaceCenterManagement.Instance.ActiveSC.ActiveLC.LCType == LaunchComplexType.Pad))
            {
                string dialogStr;
                if (type == ProjectType.VAB)
                {
                    if (SpaceCenterManagement.Instance.ActiveSC.IsAnyLCOperational)
                        dialogStr = $"a launch complex. Please switch to a launch complex and try again.";
                    else
                        dialogStr = $"a launch complex. You must build a launch complex (or wait for a launch complex to finish building or renovating) before you can integrate this vessel.";

                }
                else
                {
                    if (SpaceCenterManagement.Instance.ActiveSC.Hangar.IsOperational)
                        dialogStr = $"the Hangar. Please switch to the Hangar as active launch complex and try again.";
                    else
                        dialogStr = $"the Hangar. You must wait for the Hangar to finish renovating before you can integrate this vessel.";
                }

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "editorChecksFailedPopup",
                        "Wrong Launch Complex!",
                            $"Warning! This vessel needs to be built in {dialogStr}",
                        "Acknowledged",
                        false,
                        HighLogic.UISkin).HideGUIsWhilePopup();
                return;
            }

            var vp = new VesselProject(EditorLogic.fetch.ship, launchSite, EditorLogic.FlagURL, true)
            {
                shipName = EditorLogic.fetch.shipNameField.text
            };

            TryAddVesselToBuildList(vp);
        }

        public static void TryAddVesselToBuildList(VesselProject vp, bool skipPartChecks = false, LaunchComplex overrideLC = null)
        {
            if (overrideLC != null)
                vp.LCID = overrideLC.ID;

            var v = new VesselBuildValidator
            {
                CheckPartAvailability = !skipPartChecks,
                CheckPartConfigs = !skipPartChecks,
                CheckUntooledParts = !skipPartChecks,
                SuccessAction = AddVesselToBuildList
            };
            v.ProcessVessel(vp);
        }

        public static void AddVesselToBuildList(VesselProject vp) => AddVesselToBuildList(vp, true);

        public static void AddVesselToBuildList(VesselProject vp, bool spendFunds)
        {
            if (spendFunds)
                SpendFunds(vp.GetTotalCost(), TransactionReasonsRP0.VesselPurchase);

            if (vp.Type == ProjectType.SPH)
                vp.launchSite = "Runway";
            else
                vp.launchSite = "LaunchPad";

            LaunchComplex lc = vp.LC;
            if (lc != null)
            {
                lc.BuildList.Add(vp);
            }
            else
            {
                RP0Debug.LogError($"Error! Tried to add {vp.shipName} to build list but couldn't find LC! KSC {SpaceCenterManagement.Instance.ActiveSC.KSCName} and active LC {SpaceCenterManagement.Instance.ActiveSC.ActiveLC}");
                return;
            }

            try
            {
                SCMEvents.OnVesselAddedToBuildQueue.Fire(vp);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            RP0Debug.Log($"Added {vp.shipName} to build list at {lc.Name} at {SpaceCenterManagement.Instance.ActiveSC.KSCName}. Cost: {vp.cost}.");
            RP0Debug.Log("Launch site is " + vp.launchSite);
            string text = $"Added {vp.shipName} to integration list at {lc.Name}.";
            var message = new ScreenMessage(text, 4f, ScreenMessageStyle.UPPER_CENTER);
            ScreenMessages.PostScreenMessage(message);
        }

        /// <summary>
        /// Validates and saves the vessel edits as a new buildlist item.
        /// </summary>
        /// <param name="editableShip">Must be the pre-edits (i.e what was initially loaded into the edit session) state of the vessel</param>
        public static void TrySaveShipEdits(VesselProject editableShip)
        {
            // Load the current editor state as a fresh BuildListVessel
            string launchSite = EditorLogic.fetch.launchSiteName;
            var postEditShip = new VesselProject(EditorLogic.fetch.ship, launchSite, EditorLogic.FlagURL, true)
            {
                shipName = EditorLogic.fetch.shipNameField.text,
                FacilityBuiltIn = editableShip.FacilityBuiltIn,
                KCTPersistentID = editableShip.KCTPersistentID,
                LCID = editableShip.LCID
            };

            double usedShipsCost = editableShip.GetTotalCost();
            foreach (VesselProject v in SpaceCenterManagement.Instance.MergedVessels)
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

        private static void SaveShipEdits(double oldCost, VesselProject editableShip, VesselProject newShip)
        {
            double costDelta;
            if (KSPUtils.CurrentGameIsCareer() && (costDelta = oldCost - newShip.cost) != 0d)
            {
                Funding.Instance.AddFunds((float)costDelta, TransactionReasonsRP0.VesselPurchase.Stock());
            }

            AddVesselToBuildList(newShip, false);

            int oldIdx;
            editableShip.RemoveFromBuildList(out oldIdx);
            if (KCTSettings.Instance.InPlaceEdit && oldIdx >= 0)
            {
                // Remove and reinsert at right place.
                // We *could* insert at the right place to start with, but
                // that requires changing AddVesselToBuildList, which is used as
                // a void delegate elsewhere, so...
                List<VesselProject> lst = newShip.LC.BuildList;
                lst.RemoveAt(lst.Count - 1);
                lst.Insert(oldIdx, newShip);
            }
            newShip.LC.RecalculateBuildRates();

            GetShipEditProgress(editableShip, out double progressBP, out _, out _);
            newShip.progress = progressBP;
            RP0Debug.Log($"Finished? {newShip.IsFinished}");
            if (newShip.IsFinished)
            {
                newShip.MoveVesselToWarehouse();
            }

            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);

            SpaceCenterManagement.ClearVesselEditMode();

            RP0Debug.Log("Edits saved.");

            HighLogic.LoadScene(GameScenes.SPACECENTER);
        }

        public static void GetShipEditProgress(VesselProject ship, out double newProgressBP, out double originalCompletionPercent, out double newCompletionPercent)
        {
            double origTotalBP;
            double oldProgressBP;

            if (SpaceCenterManagement.Instance.MergedVessels.Count == 0)
            {
                origTotalBP = ship.buildPoints;
                oldProgressBP = ship.IsFinished ? origTotalBP : ship.progress;
            }
            else
            {
                double totalEffectiveCost = ship.effectiveCost;
                foreach (VesselProject v in SpaceCenterManagement.Instance.MergedVessels)
                {
                    totalEffectiveCost += v.effectiveCost;
                }

                origTotalBP = oldProgressBP = Formula.GetVesselBuildPoints(totalEffectiveCost);
                oldProgressBP *= (1 - Database.SettingsSC.MergingTimePenalty);
            }

            double newTotalBP = SpaceCenterManagement.Instance.EditorVessel.buildPoints;
            double totalBPDiff = Math.Abs(newTotalBP - origTotalBP);
            newProgressBP = Math.Max(0, oldProgressBP - (1.1 * totalBPDiff));
            originalCompletionPercent = oldProgressBP / origTotalBP;
            newCompletionPercent = newProgressBP / newTotalBP;
        }

        public static void UnlockExperimentalParts(List<AvailablePart> availableParts)
        {
            // this will spend the funds, which is why we set costsFunds=false below.
            UnlockCreditHandler.Instance.SpendCreditAndCost(availableParts);

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
            }

            EditorPartList.Instance?.Refresh();
            EditorPartList.Instance?.Refresh(EditorPartList.State.PartsList);
            if (HighLogic.LoadedSceneIsEditor)
                SpaceCenterManagement.Instance.IsEditorRecalcuationRequired = true;
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
            if (!nodeIsInList) return SpaceCenterManagement.Instance.TechListHas(ap.TechRequired);

            bool nodeIsUnlocked = ptn.state == RDTech.State.Available;
            bool partNotPurchased = !ptn.partsPurchased.Contains(ap);

            return nodeIsUnlocked && partNotPurchased;
        }

        public static bool AddExperimentalPart(AvailablePart ap)
        {
            if (ap is null || !KSPUtils.CurrentGameIsCareer() || ResearchAndDevelopment.IsExperimentalPart(ap))
                return false;

            ResearchAndDevelopment.AddExperimentalPart(ap);
            return true;
        }

        public static bool RemoveExperimentalPart(AvailablePart ap)
        {
            if (ap is null || !KSPUtils.CurrentGameIsCareer())
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

        private static void _checkTime(in ISpaceCenterProject item, ref double shortestTime, ref ISpaceCenterProject closest)
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

        public static ISpaceCenterProject GetNextThingToFinish()
        {
            ISpaceCenterProject thing = null;
            if (SpaceCenterManagement.Instance.ActiveSC == null)
                return null;
            double shortestTime = double.PositiveInfinity;
            foreach (LCSpaceCenter KSC in SpaceCenterManagement.Instance.KSCs)
            {
                foreach (LaunchComplex LC in KSC.LaunchComplexes)
                {
                    if (!LC.IsOperational)
                        continue;
                    foreach (ISpaceCenterProject vp in LC.BuildList)
                        _checkTime(vp, ref shortestTime, ref thing);
                    foreach (ISpaceCenterProject rr in LC.GetAllLCOps())
                        _checkTime(rr, ref shortestTime, ref thing);
                }
                foreach (ISpaceCenterProject ub in KSC.Constructions)
                    _checkTime(ub, ref shortestTime, ref thing);
            }
            foreach (ResearchProject tech in SpaceCenterManagement.Instance.TechList)
            {
                if (tech.GetBlockingTech() == null)   // Ignore items that are blocked
                    _checkTime(tech, ref shortestTime, ref thing);
            }
            foreach (ISpaceCenterProject course in Crew.CrewHandler.Instance.TrainingCourses)
                _checkTime(course, ref shortestTime, ref thing);
            if (SpaceCenterManagement.Instance.fundTarget.IsValid)
                _checkTime(SpaceCenterManagement.Instance.fundTarget, ref shortestTime, ref thing);
            if (SpaceCenterManagement.Instance.staffTarget.IsValid)
                _checkTime(SpaceCenterManagement.Instance.staffTarget, ref shortestTime, ref thing);

            return thing;
        }

        public static void DisableModFunctionality()
        {
            DisableSimulationLocks();
            InputLockManager.RemoveControlLock(SpaceCenterManagement.KCTLaunchLock);
            KCT_GUI.HideAll();
        }

        public static List<string> GetLaunchSites(bool isVAB)
        {
            EditorDriver.editorFacility = isVAB ? EditorFacility.VAB : EditorFacility.SPH;
            EditorDriver.setupValidLaunchSites();
            return EditorDriver.ValidLaunchSites;
        }

        public static bool IsLaunchFacilityIntact(ProjectType type)
        {
            bool intact = true;
            if (type == ProjectType.VAB)
            {
                intact = new PreFlightTests.FacilityOperational("LaunchPad", "LaunchPad").Test();
            }
            else if (type == ProjectType.SPH)
            {
                if (!new PreFlightTests.FacilityOperational("Runway", "Runway").Test())
                    intact = false;
            }
            return intact;
        }

        public static bool IsApproximatelyEqual(double d1, double d2, double error = 0.01)
        {
            return (1 - error) <= (d1 / d2) && (d1 / d2) <= (1 + error);
        }

        public static bool ReconditioningActive(LaunchComplex LC, string launchSite = "LaunchPad")
        {
            if (LC == null) LC = SpaceCenterManagement.Instance.ActiveSC.ActiveLC;
            return LC.GetReconditioning(launchSite) is ReconRolloutProject;
        }

        public static VesselProject FindVPByID(LaunchComplex hintLC, Guid id)
        {
            VesselProject b;
            if (hintLC != null)
            {
                b = FindVPByIDInLC(id, hintLC);
                if (b != null)
                    return b;
            }

            foreach (LCSpaceCenter ksc in SpaceCenterManagement.Instance.KSCs)
            {
                if (FindVPByID(id, ksc) is VesselProject vp)
                    return vp;
            }

            return null;
        }

        public static VesselProject FindVPByIDInLC(Guid id, LaunchComplex lc)
        {

            VesselProject ves = lc.Warehouse.Find(vp => vp.shipID == id);
            if (ves != null)
                return ves;

            ves = lc.BuildList.Find(vp => vp.shipID == id);
            if (ves != null)
                return ves;

            return null;
        }

        public static VesselProject FindVPByID(Guid id, LCSpaceCenter ksc)
        {
            if (ksc != null)
            {
                foreach (LaunchComplex lc in ksc.LaunchComplexes)
                {
                    VesselProject ves = FindVPByIDInLC(id, lc);
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

        public static bool RecoverActiveVesselToStorage(ProjectType listType)
        {
            try
            {
                RP0Debug.Log("Attempting to recover active vessel to storage.  listType: " + listType);
                GamePersistence.SaveGame("KCT_Backup", HighLogic.SaveFolder, SaveMode.OVERWRITE);

                SpaceCenterManagement.Instance.RecoveredVessel = new VesselProject(FlightGlobals.ActiveVessel, listType);

                KCTVesselData vData = FlightGlobals.ActiveVessel.GetKCTVesselData();
                SpaceCenterManagement.Instance.RecoveredVessel.KCTPersistentID = vData?.VesselID;
                SpaceCenterManagement.Instance.RecoveredVessel.FacilityBuiltIn = vData?.FacilityBuiltIn ?? EditorFacility.None;
                SpaceCenterManagement.Instance.RecoveredVessel.LCID = vData?.LCID ?? Guid.Empty;
                SpaceCenterManagement.Instance.RecoveredVessel.LandedAt = FlightGlobals.ActiveVessel.landedAt;

                //KCT_GameStates.recoveredVessel.type = listType;
                if (listType == ProjectType.SPH)
                    SpaceCenterManagement.Instance.RecoveredVessel.launchSite = "Runway";
                else
                    SpaceCenterManagement.Instance.RecoveredVessel.launchSite = "LaunchPad";

                //check for symmetry parts and remove those references if they can't be found
                SpaceCenterManagement.Instance.RecoveredVessel.RemoveMissingSymmetry();

                // debug, save to a file
                SpaceCenterManagement.Instance.RecoveredVessel.UpdateNodeAndSave("KCTVesselSave", false);

                //test if we can actually convert it
                var test = SpaceCenterManagement.Instance.RecoveredVessel.CreateShipConstructAndRelease();

                if (test != null)
                    ShipConstruction.CreateBackup(test);
                RP0Debug.Log("Load test reported success = " + (test == null ? "false" : "true"));
                if (test == null)
                {
                    SpaceCenterManagement.Instance.RecoveredVessel = new VesselProject();
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
                RP0Debug.LogError("Error while recovering craft into inventory.");
                RP0Debug.LogError("error: " + ex);
                SpaceCenterManagement.Instance.RecoveredVessel = new VesselProject();
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

            if (EditorLogic.fetch == null)
                return;

            if (KCTSettings.Instance.OverrideLaunchButton)
            {
                if (SpaceCenterManagement.EditorShipEditingMode)
                {
                    // Prevent switching between VAB and SPH in edit mode.
                    // Bad things will happen if the edits are saved in another mode than the initial one.
                    EditorLogic.fetch.switchEditorBtn.onClick.RemoveAllListeners();
                    EditorLogic.fetch.switchEditorBtn.onClick.AddListener(() =>
                    {
                        PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "cannotSwitchEditor",
                            "Cannot switch editor!",
                            "Switching between VAB and SPH is not allowed while editing a vessel.",
                            "Acknowledged", false, HighLogic.UISkin).HideGUIsWhilePopup();
                    });
                }
                else
                {
                    EditorLogic.fetch.switchEditorBtn.onClick.RemoveListener(OnEditorSwitch);
                    EditorLogic.fetch.switchEditorBtn.onClick.AddListener(OnEditorSwitch);
                }

                EditorLogic.fetch.launchBtn.onClick.RemoveAllListeners();
                EditorLogic.fetch.launchBtn.onClick.AddListener(() => { SpaceCenterManagement.ShowLaunchAlert(null); });

                if (SpaceCenterManagement.Instance == null)
                    return;

                if (!SpaceCenterManagement.Instance.IsLaunchSiteControllerDisabled)
                {
                    SpaceCenterManagement.Instance.IsLaunchSiteControllerDisabled = true;
                    UILaunchsiteController controller = UnityEngine.Object.FindObjectOfType<UILaunchsiteController>();
                    if (controller == null)
                    {
                        RP0Debug.Log("UILaunchsiteController is null");
                    }
                    else
                    {
                        RP0Debug.Log("Killing UILaunchsiteController");
                        UnityEngine.Object.Destroy(controller);
                    }
                }
            }
            else if (SpaceCenterManagement.Instance != null)
            {
                InputLockManager.SetControlLock(ControlTypes.EDITOR_LAUNCH, SpaceCenterManagement.KCTLaunchLock);
                if (!SpaceCenterManagement.Instance.IsLaunchSiteControllerDisabled)
                {
                    SpaceCenterManagement.Instance.IsLaunchSiteControllerDisabled = true;
                    RP0Debug.Log("Attempting to disable launchsite specific buttons");
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
            SpaceCenterManagement.Instance.StartCoroutine(PostEditorSwitch());
        }

        private static System.Collections.IEnumerator PostEditorSwitch()
        {
            yield return new WaitForSeconds(0.1f);
            while (EditorDriver.fetch != null && EditorDriver.fetch.restartingEditor)
                yield return null;
            if (EditorDriver.fetch == null)
                yield break;

            SpaceCenterManagement.Instance.IsEditorRecalcuationRequired = true;
        }

        /// <summary>
        /// Check whether the part has a tag with specified name defined using the ModuleTagList PartModule.
        /// </summary>
        /// <param name="p">Part to check</param>
        /// <param name="tag">Name of the tag to check</param>
        /// <returns>True if Part has ModuleTagList PM and a tag with given name is defined in that PM</returns>
        public static bool HasTag(this Part p, string tag)
        {
            return ModuleTagList.GetTags(p)?.Contains(tag) ?? false;
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
            string reqTech = Database.SettingsSC.VABRecoveryTech;
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
            RP0Debug.Log("Making simulation backup file.");
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

            RP0Debug.Log("Swapping persistent.sfs with simulation backup file.");
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

        public static void CleanupDebris(string launchSiteName)
        {
            if (KCTSettings.Instance.CleanUpKSCDebris)
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
                            RP0Debug.LogError($"Config validation failed for {p.name}");
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

            double ecmCost = -CurrencyUtils.Funds(TransactionReasonsRP0.PartOrUpgradeUnlock, -RealFuels.EntryCostManager.Instance.ConfigEntryCost(ecmPartsList));

            runningCost = -(float)CurrencyUtils.Funds(TransactionReasonsRP0.PartOrUpgradeUnlock, -runningCost);

            List<string> techList = SortAndFilterTechListForFinalNodes(pendingTech);
            float totalCost = runningCost + Convert.ToSingle(ecmCost);
            RP0Debug.Log($"Vessel parts unlock cost check. Total: {totalCost}, Raw cost: {runningCost}, ECM cost: {ecmCost}");
            return new Tuple<float, List<string>>(totalCost, techList);
        }

        public static List<string> SortAndFilterTechListForFinalNodes(HashSet<string> input)
        {
            HashSet<string> blacklist = new HashSet<string>();
            SortedList<string, string> slist = new SortedList<string, string>();
            foreach (string s in input)
            {
                foreach (string parent in Database.TechNameToParents[s])
                {
                    blacklist.Add(parent);
                }
            }
            foreach (string s in input)
            {
                if (!blacklist.Contains(s))
                {
                    // sort our result, depth into the tree then alpha
                    int depth = Database.TechNameToParents[s].Count();
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
                    if (PartIsUnlocked(part))
                        status = PurchasabilityStatus.Purchased;
                    else if (ResearchAndDevelopment.GetTechnologyState(part.TechRequired) == RDTech.State.Available)
                        status = PurchasabilityStatus.Purchasable;
                    res.Add(part, new PartPurchasability(status, 1));
                }
            }
            return res;
        }

        public static void ScrapVessel(VesselProject b)
        {
            RP0Debug.Log($"Scrapping {b.shipName}");
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

        public static void HireStaff(bool isResearch, int workerAmount, LaunchComplex lc = null)
        {
            // Use up applicants first
            int workersToHire = Math.Max(0, workerAmount - SpaceCenterManagement.Instance.Applicants);

            // Note: have to pass base, not modified, cost here, since the CMQ reruns
            SpendFunds(workersToHire * Database.SettingsSC.HireCost, isResearch ? TransactionReasonsRP0.HiringResearchers : TransactionReasonsRP0.HiringEngineers);
            if (isResearch)
            {
                ChangeResearchers(workerAmount);
                SpaceCenterManagement.Instance.UpdateTechTimes();
            }
            else
            {
                LCSpaceCenter ksc = lc?.KSC ?? SpaceCenterManagement.Instance.ActiveSC;
                ChangeEngineers(ksc, workerAmount);
                if (lc != null)
                    ChangeEngineers(lc, workerAmount);
            }
            SpaceCenterManagement.Instance.Applicants = Math.Max(0, SpaceCenterManagement.Instance.Applicants - workerAmount);
            if (SpaceCenterManagement.Instance.Applicants == 0)
                SpaceCenterManagement.Instance.HiredStarterApplicants = true;
        }

        public static void ChangeEngineers(LaunchComplex currentLC, int delta)
        {
            currentLC.Engineers += delta;
            SCMEvents.OnPersonnelChange.Fire();
            MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
            currentLC.RecalculateBuildRates();
            KCT_GUI.BuildRateForDisplay = null;
        }

        public static void ChangeEngineers(LCSpaceCenter ksc, int delta)
        {
            ksc.Engineers += delta;
            SCMEvents.OnPersonnelChange.Fire();
            MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
        }

        public static void ChangeResearchers(int delta)
        {
            SpaceCenterManagement.Instance.Researchers += delta;
            SCMEvents.OnPersonnelChange.Fire();
            MaintenanceHandler.Instance.ScheduleMaintenanceUpdate();
        }

        private const double ApplicantsPow = 0.92d;
        public static int ApplicantPacketsForScience(double sci) => (int)(Math.Pow(sci, ApplicantsPow) / 5d);

        public static double ScienceForNextApplicants()
        {
            int applicantsCur = ApplicantPacketsForScience(Math.Max(0d, SpaceCenterManagement.Instance.SciPointsTotal));
            return Math.Pow(5d * (applicantsCur + 1d), 1d / ApplicantsPow);
        }

        public static void GetConstructionTooltip(ConstructionProject constr, int i, out string costTooltip, out string identifier)
        {
            identifier = constr.GetItemName() + i;
            costTooltip = $"Remaining Cost: √{constr.RemainingCost:N0}";
            if (constr is LCConstructionProject lcc)
            {
                if (lcc.lcData.lcType == LaunchComplexType.Pad)
                    costTooltip = $"Tonnage: {LaunchComplex.SupportedMassAsPrettyTextCalc(lcc.lcData.massMax)}\nHuman-Rated: {(lcc.lcData.isHumanRated ? "Yes" : "No")}\n{costTooltip}";

                costTooltip = $"Dimensions: {LaunchComplex.SupportedSizeAsPrettyTextCalc(lcc.lcData.sizeMax)}\n{costTooltip}";
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
            var upgdFacility = LCLaunchPad.GetUpgradeableFacilityReference();
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

            if (SpaceCenterManagement.Instance.IsSimulatedFlight)
                return false;

            if (v.vesselRef.isEVA)
                return false;

            // Is also called at the start of the flight scene when recovering clamps & debris
            if (SpaceCenterManagement.Instance.RecoveredVessel?.IsValid != true)
            {
                RP0Debug.Log("Recovered vessel is null!");
                return false;
            }

            if (v.vesselName != SpaceCenterManagement.Instance.RecoveredVessel.shipName)
            {
                RP0Debug.Log($"Recovered vessel '{v.vesselName}' and '{SpaceCenterManagement.Instance.RecoveredVessel.shipName}' do not match ");
                return false;
            }

            return true;
        }

        public static void DoAirlaunch(AirlaunchParams launchParams)
        {
            ROUtils.HyperEdit_Utilities.DoAirlaunch(launchParams.KscDistance, launchParams.KscAzimuth, launchParams.LaunchAzimuth, launchParams.Altitude, launchParams.Velocity);
        }

        public static int GetFacilityLevel(SpaceCenterFacility facility)
        {
            return MathUtils.GetIndexFromNorm(ScenarioUpgradeableFacilities.GetFacilityLevel(facility), Database.GetFacilityLevelCount(facility));
        }

        public static void SetFacilityLevel(SpaceCenterFacility scf, int level)
        {
            string facId = ScenarioUpgradeableFacilities.SlashSanitize(scf.ToString());
            ScenarioUpgradeableFacilities.ProtoUpgradeable upgradable = ScenarioUpgradeableFacilities.protoUpgradeables[facId];

            bool levelWasSet = false;
            if (upgradable.facilityRefs.Count > 0)
            {
                // The facilityRefs are only available when the space center facilities are physically spawned.
                // For instance they aren't found in TS scene or when going far enough away from home body.
                levelWasSet = true;
                foreach (UpgradeableFacility upgd in upgradable.facilityRefs)
                {
                    RP0Debug.Log($"Setting facility {upgd.id} upgrade level through standard path");
                    upgd.SetLevel(level);
                }
            }

            if (!levelWasSet)
            {
                RP0Debug.Log($"Failed to set facility {scf} upgrade level through standard path, using fallback");
                int maxLevel = Database.GetFacilityLevelCount(scf) - 1;
                double normLevel = maxLevel == 0 ? 1d : level / (double)maxLevel;
                upgradable.configNode.SetValue("lvl", normLevel);

                // Note that OnKSCFacilityUpgrading and OnKSCFacilityUpgraded events are not fired through this code path
                // Still, we need to let RA know that it needs a reset to account for the finished upgrade.
                if (scf == SpaceCenterFacility.TrackingStation)
                {
                    ClobberRACommnet();
                }
            }
        }

        private static void ClobberRACommnet()
        {
            var mInf = CommNetScenario.Instance?.GetType().GetMethod("ApplyTSLevelChange", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (mInf != null)
            {
                mInf.Invoke(CommNetScenario.Instance, new object[0]);
            }
            else
            {
                RP0Debug.LogError($"Failed to call ApplyTSLevelChange() on RA CommNetScenario");
            }
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
