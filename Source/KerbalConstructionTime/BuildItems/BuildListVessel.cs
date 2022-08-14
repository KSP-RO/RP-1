using KSP.UI;
using PreFlightTests;
using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public class BuildListVessel : IKCTBuildItem
    {
        public enum ListType { None, VAB, SPH, TechNode, Reconditioning, KSC, AirLaunch, Crew };

        public double Progress, EffectiveCost, BuildPoints, IntegrationPoints;
        public string LaunchSite, Flag, ShipName;
        public int LaunchSiteIndex = -1;
        public ListType Type;
        public ConfigNode ShipNode;
        public Guid Id;
        public bool CannotEarnScience;
        public bool IsHumanRated;
        public float Cost = 0, IntegrationCost;
        public float TotalMass = 0, DistanceFromKSC = 0;
        public int RushBuildClicks = 0;
        public int NumStages = 0;
        public int NumStageParts = 0;
        public double StagePartCost = 0;
        public float EmptyCost = 0, EmptyMass = 0;
        public EditorFacility FacilityBuiltIn;
        public string KCTPersistentID;
        public string LandedAt = "";
        private double _buildRate = -1d;

        public PersistentDictionaryValueTypes<string, double> resourceAmounts = new PersistentDictionaryValueTypes<string, double>();
        public PersistentHashSetValueType<string> globalTags = new PersistentHashSetValueType<string>();

        internal ShipConstruct _ship;

        public Vector3 ShipSize = Vector3.zero;

        public double BuildRate => (_buildRate < 0 ? UpdateBuildRate() : _buildRate)
            * LC.Efficiency * LC.RushRate;

        public double TimeLeft
        {
            get
            {
                if (BuildRate > 0)
                    return (IntegrationPoints + BuildPoints - Progress) / BuildRate;
                else
                    return double.PositiveInfinity;
            }
        }

        public List<Part> ExtractedParts
        {
            get
            {
                List<Part> temp = new List<Part>();
                foreach (PseudoPart pp in GetPseudoParts())
                {
                    Part p = Utilities.GetAvailablePartByName(pp.Name).partPrefab;
                    p.craftID = pp.Uid;
                    temp.Add(p);
                }
                return temp;
            }
        }

        public List<ConfigNode> ExtractedPartNodes => ShipNode.GetNodes("PART").ToList();

        public bool IsFinished => Progress >= BuildPoints + IntegrationPoints;

        public KSCItem KSC
        {
            get
            {
                if (LC == null)
                    return null;

                return LC.KSC;
            }
        }

        private Guid _lcID;
        public Guid LCID
        {
            get
            {
                return _lcID;
            }
            set
            {
                _lc = null; // force a refind
                _lcID = value;
            }
        }
        private LCItem _lc = null;

        public LCItem LC
        {
            get
            {
                if (_lc == null)
                {
                    foreach (var ksc in KCTGameStates.KSCs)
                    {
                        foreach (var lc in ksc.LaunchComplexes)
                        {
                            if (lc.BuildList.FirstOrDefault(s => s.Id == Id) != null ||
                                    lc.Warehouse.FirstOrDefault(s => s.Id == Id) != null)
                            {
                                _lc = lc;
                                break;
                            }
                        }
                    }
                    if (_lc == null)
                    {
                        _lc = KCTGameStates.FindLCFromID(_lcID);
                    }
                }
                return _lc;
            }
            set
            {
                _lc = value;
                if (_lc != null)
                    _lcID = _lc.ID;
            }
        }

        private bool? _allPartsValid;
        public bool AllPartsValid
        {
            get
            {
                if (_allPartsValid == null)
                    _allPartsValid = AreAllPartsValid();
                return (bool)_allPartsValid;
            }
        }

        /// <summary>
        /// The default crew to use when assigning crew
        /// </summary>
        public List<string> DesiredManifest { set; get; } = new List<string>();

        public BuildListVessel(ShipConstruct s, string ls, string flagURL)
        {
            _ship = s;
            ShipNode = s.SaveShip();
            // Override KSP sizing of the ship construct
            ShipSize = Utilities.GetShipSize(s, true);
            ShipNode.SetValue("size", KSPUtil.WriteVector(ShipSize));
            ShipName = s.shipName;
            Cost = Utilities.GetTotalVesselCost(ShipNode, true);
            EmptyCost = Utilities.GetTotalVesselCost(ShipNode, false);
            TotalMass = Utilities.GetShipMass(s, true, out EmptyMass, out _);

            EffectiveCost = GetEffectiveCost(EditorLogic.fetch.ship.Parts);
            BuildPoints = Formula.GetVesselBuildPoints(EffectiveCost);

            HashSet<int> stages = new HashSet<int>();
            NumStageParts = 0;
            StagePartCost = 0d;

            foreach (Part p in s.Parts)
            {
                if (p.stagingOn)
                {
                    stages.Add(p.inverseStage);
                    ++NumStageParts;
                    StagePartCost += p.GetModuleCosts(p.partInfo.cost, ModifierStagingSituation.CURRENT) + p.partInfo.cost;
                }
            }
            NumStages = stages.Count;

            LaunchSite = ls;
            Progress = 0;
            Flag = flagURL;
            if (s.shipFacility == EditorFacility.VAB)
            {
                Type = ListType.VAB;
                FacilityBuiltIn = EditorFacility.VAB;
                _lc = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
                if (_lc.LCType == LaunchComplexType.Hangar)
                    KCTDebug.LogError($"ERROR: Tried to add vessel {ShipName} to LC {_lc.Name} but vessel is type VAB!");
            }
            else if (s.shipFacility == EditorFacility.SPH)
            {
                Type = ListType.SPH;
                FacilityBuiltIn = EditorFacility.SPH;
                _lc = KCTGameStates.ActiveKSC.Hangar;
            }
            else
                Type = ListType.None;

            if(_lc != null && !_lc.IsOperational)
                KCTDebug.LogError($"ERROR: Tried to add vessel {ShipName} to LC {_lc.Name} but LC is not operational!");

            Id = Guid.NewGuid();
            KCTPersistentID = Guid.NewGuid().ToString("N");
            CannotEarnScience = false;

            //get the crew from the editorlogic
            DesiredManifest = new List<string>();
            if (CrewAssignmentDialog.Instance?.GetManifest()?.CrewCount > 0)
            {
                foreach (ProtoCrewMember crew in CrewAssignmentDialog.Instance.GetManifest().GetAllCrew(true) ?? new List<ProtoCrewMember>())
                {
                    DesiredManifest.Add(crew?.name ?? string.Empty);
                }
            }

            IntegrationPoints = Formula.GetIntegrationBP(this);
            IntegrationCost = (float)Formula.GetIntegrationCost(this);
        }

        public BuildListVessel(string name, string ls, double effCost, double bP, double integrP, string flagURL, float spentFunds, float integrCost, int editorFacility, bool isHuman)
        {
            LaunchSite = ls;
            ShipName = name;
            EffectiveCost = effCost;
            BuildPoints = bP;
            IntegrationPoints = integrP;
            Progress = 0;
            Flag = flagURL;
            IsHumanRated = isHuman;
            if (editorFacility == (int)EditorFacilities.VAB)
            {
                Type = ListType.VAB;
                FacilityBuiltIn = EditorFacility.VAB;
            }
            else if (editorFacility == (int)EditorFacilities.SPH)
            {
                Type = ListType.SPH;
                FacilityBuiltIn = EditorFacility.SPH;
            }
            else
                Type = ListType.None;
            CannotEarnScience = false;
            Cost = spentFunds;
            IntegrationCost = integrCost;
        }

        /// <summary>
        /// For recovered vessels
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="listType"></param>
        public BuildListVessel(Vessel vessel, ListType listType = ListType.None)
        {
            Id = Guid.NewGuid();
            KCTPersistentID = Guid.NewGuid().ToString("N");
            ShipName = vessel.vesselName;
            ShipNode = FromInFlightVessel(vessel, listType);
            if (listType != ListType.None)
                Type = listType;

            Cost = Utilities.GetTotalVesselCost(ShipNode);
            EmptyCost = Utilities.GetTotalVesselCost(ShipNode, false);
            TotalMass = 0;
            EmptyMass = 0;

            HashSet<int> stages = new HashSet<int>();

            foreach (ProtoPartSnapshot p in vessel.protoVessel.protoPartSnapshots)
            {
                stages.Add(p.inverseStageIndex);

                if (p.partPrefab != null)
                {
                    if (p.partPrefab.Modules.Contains<LaunchClamp>() || p.partPrefab.HasTag("PadInfrastructure"))
                        continue;
                }

                if (p.parent != null && p.parent.partPrefab != null && p.parent.partPrefab.HasTag("PadInfrastructure"))
                    continue;

                TotalMass += p.mass;
                EmptyMass += p.mass;

                foreach (ProtoPartResourceSnapshot rsc in p.resources)
                {
                    PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(rsc.resourceName);
                    if (def != null)
                        TotalMass += def.density * (float)rsc.amount;
                }
            }
            CannotEarnScience = true;
            NumStages = stages.Count;
            // FIXME ignore stageable part count and cost - it'll be fixed when we put this back in the editor.

            EffectiveCost = GetEffectiveCost(vessel.parts);
            BuildPoints = Formula.GetVesselBuildPoints(EffectiveCost);
            Flag = HighLogic.CurrentGame.flagURL;

            DistanceFromKSC = (float)SpaceCenter.Instance.GreatCircleDistance(SpaceCenter.Instance.cb.GetRelSurfaceNVector(vessel.latitude, vessel.longitude));

            RushBuildClicks = 0;
            IntegrationPoints = Formula.GetIntegrationBP(this);
            IntegrationCost = (float)Formula.GetIntegrationCost(this);

            Progress = BuildPoints + IntegrationPoints;
        }

        private ConfigNode FromInFlightVessel(Vessel VesselToSave, ListType listType)
        {
            //This code is taken from InflightShipSave by Claw, using the CC-BY-NC-SA license.
            //This code thus is licensed under the same license, despite the GPLv3 license covering original KCT code
            //See https://github.com/ClawKSP/InflightShipSave

            string ShipName = VesselToSave.vesselName;
            ShipConstruct ConstructToSave = new ShipConstruct(ShipName, "", VesselToSave.parts[0]);

            Quaternion OriginalRotation = VesselToSave.vesselTransform.rotation;
            Vector3 OriginalPosition = VesselToSave.vesselTransform.position;

            if (listType == ListType.SPH)
            {
                VesselToSave.SetRotation(new Quaternion((float)Math.Sqrt(0.5), 0, 0, (float)Math.Sqrt(0.5)));
            }
            else
            {
                VesselToSave.SetRotation(new Quaternion(0, 0, 0, 1));
            }
            Vector3 ShipSize = ShipConstruction.CalculateCraftSize(ConstructToSave);
            VesselToSave.SetPosition(new Vector3(0, Math.Min(ShipSize.y + 2, 15), 0));    //Try to limit the max height we put the ship at

            ConfigNode cn = ConstructToSave.SaveShip();
            SanitizeShipNode(cn);
            // override KSP sizing of the ship construct
            cn.SetValue("size", KSPUtil.WriteVector(Utilities.GetShipSize(ConstructToSave, true)));
            // These are actually needed, do not comment them out
            VesselToSave.SetRotation(OriginalRotation);
            VesselToSave.SetPosition(OriginalPosition);
            //End of Claw's code. Thanks Claw!
            return cn;
        }

        public void RecalculateFromNode(bool setValues = true)
        {
            bool oldHR = IsHumanRated;
            double ec = GetEffectiveCost(ExtractedPartNodes);
            if (setValues)
            {
                EffectiveCost = ec;
                BuildPoints = Formula.GetVesselBuildPoints(EffectiveCost);
                IntegrationPoints = Formula.GetIntegrationBP(this);
            }
            else
            {
                IsHumanRated = oldHR;
            }
        }

        private ConfigNode SanitizeShipNode(ConfigNode node)
        {
            //PART, MODULE -> clean experiments, repack chutes, disable engines
            string filePath = $"{KSPUtil.ApplicationRootPath}GameData/RP-0/KCT/KCT_ModuleTemplates.cfg";
            ConfigNode ModuleTemplates = ConfigNode.Load(filePath);
            ConfigNode[] templates = ModuleTemplates.GetNodes("MODULE");

            foreach(ConfigNode part in node.GetNodes("PART"))
            {
                foreach(ConfigNode module in part.GetNodes("MODULE"))
                {
                    SanitizeNode(Utilities.GetPartNameFromNode(part), module, templates);
                }

                // Remove all waste resources
                var resList = part.GetNodes("RESOURCE");
                foreach (var res in resList)
                {
                    if (GuiDataAndWhitelistItemsDatabase.WasteRes.Contains(res.GetValue("name")))
                    {
                        res.SetValue("amount", 0);
                    }
                }
            }
            return node;
        }

        private void SanitizeNode(string partName, ConfigNode module, ConfigNode[] templates)
        {
            string name = module.GetValue("name");

            if (module.HasNode("ScienceData"))
            {
                module.RemoveNodes("ScienceData");
            }
            if (name == "Log")
                module.ClearValues();

            ConfigNode template = templates.FirstOrDefault(t => t.GetValue("name") == name && (!t.HasValue("parts") || t.GetValue("parts").Split(',').Contains(partName)));
            if (template == null) return;

            foreach (ConfigNode.Value val in template.values)
            {
                module.SetValue(val.name, val.value);
            }

            foreach (ConfigNode node in template.GetNodes())    //This should account for nested nodes, like RealChutes' PARACHUTE node
            {
                if (module.HasNode(node.name))
                {
                    for (int i1 = node.values.Count - 1; i1 >= 0; i1--)
                    {
                        ConfigNode.Value val = node.values[i1];
                        module.GetNode(node.name).SetValue(val.name, val.value);
                    }
                }
            }

            foreach (ConfigNode node in module.GetNodes("MODULE"))
                SanitizeNode(partName, node, templates);
        }

        public BuildListVessel CreateCopy(bool RecalcTime)
        {
            BuildListVessel ret = new BuildListVessel(ShipName, LaunchSite, EffectiveCost, BuildPoints, IntegrationPoints, Flag, Cost, IntegrationCost, (int)GetEditorFacility(), IsHumanRated)
            {
                ShipNode = ShipNode.CreateCopy(),
                _lc = _lc,
                globalTags = globalTags.Clone(),
                resourceAmounts = resourceAmounts.Clone()
            };

            ret.Id = Guid.NewGuid();
            ret.KCTPersistentID = Guid.NewGuid().ToString("N");
            ret.TotalMass = TotalMass;
            ret.EmptyMass = EmptyMass;
            ret.Cost = Cost;
            ret.IntegrationCost = IntegrationCost;
            ret.EmptyCost = EmptyCost;
            ret.NumStageParts = NumStageParts;
            ret.NumStages = NumStages;
            ret.StagePartCost = StagePartCost;
            ret.ShipSize = ShipSize;

            if (RecalcTime)
            {
                ret.EffectiveCost = GetEffectiveCost(ret.ExtractedPartNodes);
            }
            // Safe to always do these, they're cheap.
            ret.BuildPoints = Formula.GetVesselBuildPoints(ret.EffectiveCost);
            ret.IntegrationPoints = Formula.GetIntegrationBP(ret);
            ret.IntegrationCost = (float)Formula.GetIntegrationCost(ret);

            return ret;
        }

        public EditorFacilities GetEditorFacility()
        {
            EditorFacilities ret = EditorFacilities.NONE;
            if (Type == ListType.None)
            {
                BruteForceLocateVessel();
            }

            if (Type == ListType.VAB)
                ret = EditorFacilities.VAB;
            else if (Type == ListType.SPH)
                ret = EditorFacilities.SPH;

            return ret;
        }

        public void BruteForceLocateVessel()
        {
            KCTDebug.Log($"Brute force looking for {ShipName}");
            bool found = false;
            // This is weird, but we're forcing a reacquire of the LC
            LC = null;
            if (LC != null)
            {
                found = LC.BuildList.Exists(b => b.Id == Id);
                if (found) { Type = ListType.VAB; return; }
                found = LC.Warehouse.Exists(b => b.Id == Id);
                if (found) { Type = ListType.VAB; return; }
            }

            if (!found)
            {
                KCTDebug.Log("Still can't find ship even after checking every list...");
            }
        }

        /// <summary>
        /// Use this only within the Editor scene. Otherwise it can cause issues with other mods
        /// because initializing the ShipConstruct will cause the OnLoad for Parts and PartModules to be called.
        /// Some mods (like FAR for example) assume that this can only happen during the LoadScreen or Editor scene
        /// and freak out.
        /// </summary>
        /// <returns></returns>
        public ShipConstruct GetShip()
        {
            if (_ship?.Parts?.Count > 0)    //If the parts are there, then the ship is loaded
            {
                return _ship;
            }
            else if (ShipNode != null)    //Otherwise load the ship from the ConfigNode
            {
                if (_ship == null) _ship = new ShipConstruct();
                _ship.LoadShip(ShipNode);
            }
            return _ship;
        }

        public void Launch(bool fillFuel = false)
        {
            HighLogic.CurrentGame.editorFacility = GetEditorFacility() == EditorFacilities.VAB ? EditorFacility.VAB : EditorFacility.SPH;

            LCItem lc = KCTGameStates.FindLCFromID(_lcID);
            if (lc == null)
                lc = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
            else
                KCTGameStates.ActiveKSC.SwitchLaunchComplex(KCTGameStates.ActiveKSC.LaunchComplexes.IndexOf(lc));

            string tempFile = $"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/Ships/temp.craft";
            UpdateRFTanks();
            if (fillFuel)
                FillUnlockedFuelTanks();
            ShipNode.Save(tempFile);
            string launchSiteName = LaunchSite;
            if (launchSiteName == "LaunchPad")
            {
                KCT_LaunchPad pad;
                if (LaunchSiteIndex >= 0)
                    pad = lc.LaunchPads[LaunchSiteIndex];
                else
                    pad = lc.ActiveLPInstance;
                
                launchSiteName = pad.launchSiteName;
            }

            Utilities.CleanupDebris(launchSiteName);
            if (KCTGameStates.AirlaunchParams != null) KCTGameStates.AirlaunchParams.KSPVesselId = null;
            FlightDriver.StartWithNewLaunch(tempFile, Flag, launchSiteName, new VesselCrewManifest());
        }

        public List<string> MeetsFacilityRequirements(bool skipMinMass, bool highestFacility = false)
        {
            List<string> failedReasons = new List<string>();
            if (!Utilities.CurrentGameIsCareer())
                return failedReasons;

            // Use blv's existing LC if available, else use active complex
            LCItem selectedLC = LC != null ? LC : (Type == ListType.VAB ?
                highestFacility ? KCTGameStates.ActiveKSC.GetHighestLevelLaunchComplex() : KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance :
                Type == ListType.SPH ?
                    KCTGameStates.ActiveKSC.Hangar :
                    null);

            double totalMass = GetTotalMass();
            if (totalMass > selectedLC.MassMax)
            {
                failedReasons.Add($"Mass limit exceeded, currently at {totalMass:N} tons, max {selectedLC.MassMax:N}");
            }
            if (!skipMinMass && totalMass < selectedLC.MassMin)
            {
                failedReasons.Add($"Mass minimum exceeded, currently at {totalMass:N} tons, min {selectedLC.MassMin:N}");
            }
            HashSet<string> ignoredRes = selectedLC.LCType == LaunchComplexType.Hangar ? GuiDataAndWhitelistItemsDatabase.HangarIgnoreRes : GuiDataAndWhitelistItemsDatabase.PadIgnoreRes;
            foreach (var kvp in resourceAmounts)
            {
                if (ignoredRes.Contains(kvp.Key)
                    || !GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(kvp.Key))
                    continue;

                if (selectedLC.ResourcesHandled.TryGetValue(kvp.Key, out double lcAmount) && lcAmount >= kvp.Value)
                    continue;

                double mass = PartResourceLibrary.Instance.GetDefinition(kvp.Key).density * kvp.Value;
                if (mass <= Formula.VesselMassMinForResourceValidation * totalMass)
                    continue;

                failedReasons.Add($"Insufficient {kvp.Key} at LC: {kvp.Value:N0} required, {lcAmount:N0} available. Modify LC.");
            }

            // Facility doesn't matter here.
            CraftWithinSizeLimits sizeCheck = new CraftWithinSizeLimits(GetShipSize(), ShipName, SpaceCenterFacility.LaunchPad, selectedLC.SizeMax);
            if (!sizeCheck.Test())
            {
                failedReasons.Add("Size limits exceeded");
            }

            if (IsHumanRated && !selectedLC.IsHumanRated)
            {
                failedReasons.Add("Vessel is human-rated but launch complex is not");
            }

            if (HasClamps() && selectedLC.LCType == LaunchComplexType.Hangar)
            {
                failedReasons.Add("Has launch clamps/GSE but is launching from runway");
            }

            return failedReasons;
        }

        private bool HasClamps()
        {
            foreach (var p in ExtractedPartNodes)
            {
                AvailablePart aPart = Utilities.GetAvailablePartByName(Utilities.GetPartNameFromNode(p));

                if (aPart != null && (aPart.partPrefab.Modules.Contains<LaunchClamp>() || aPart.partPrefab.HasTag("PadInfrastructure") ))
                    return true;
            }

            return false;
        }

        public ListType FindTypeFromLists()
        {
            Type = ListType.None;
            BruteForceLocateVessel();
            return Type;
        }

        private void UpdateRFTanks()
        {
            foreach (var cn in ShipNode.GetNodes("PART"))
            {
                foreach (var module in cn.GetNodes("MODULE"))
                {
                    if (module.GetValue("name") == "ModuleFuelTanks")
                    {
                        if (module.HasValue("timestamp"))
                        {
                            KCTDebug.Log("Updating RF timestamp on a part");
                            module.SetValue("timestamp", Utilities.GetUT().ToString());
                        }
                    }
                }
            }
        }

        public bool AreTanksFull()
        {
            foreach (ConfigNode p in ShipNode.GetNodes("PART"))
            {
                foreach (var res in p.GetNodes("RESOURCE"))
                {
                    if (GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(res.GetValue("name")) &&
                        bool.Parse(res.GetValue("flowState")))
                    {
                        var maxAmt = float.Parse(res.GetValue("maxAmount"));
                        var amt = float.Parse(res.GetValue("amount"));
                        if (Math.Abs(amt - maxAmt) >= 1)
                            return false;
                    }
                }
            }
            return true;
        }

        private void FillUnlockedFuelTanks()
        {
            foreach (ConfigNode p in ShipNode.GetNodes("PART"))
            {
                var resList = p.GetNodes("RESOURCE");
                foreach (var res in resList)
                {
                    if (GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(res.GetValue("name")) &&
                        bool.Parse(res.GetValue("flowState")))
                    {
                        var maxAmt = res.GetValue("maxAmount");
                        res.SetValue("amount", maxAmt);
                    }
                }
            }
        }

        public double GetTotalMass()
        {
            if (TotalMass != 0 && EmptyMass != 0) return TotalMass;
            TotalMass = 0;
            EmptyMass = 0;
            foreach (var p in ExtractedPartNodes)
            {
                TotalMass += Utilities.GetPartMassFromNode(p, includeFuel: true, includeClamps: false);
                EmptyMass += Utilities.GetPartMassFromNode(p, includeFuel: false, includeClamps: false);
            }
            TotalMass = Math.Max(TotalMass, 0);
            EmptyMass = Math.Max(EmptyMass, 0);
            return TotalMass;
        }

        public Vector3 GetShipSize()
        {
            if (ShipSize.sqrMagnitude > 0)
                return ShipSize;

            ShipTemplate template = new ShipTemplate();
            template.LoadShip(ShipNode);
            ShipSize = template.GetShipSize();
            
            return ShipSize;
        }

        public double GetTotalCost()
        {
            if (Cost == 0 || EmptyCost == 0)
            {
                Cost = Utilities.GetTotalVesselCost(ShipNode);
                EmptyCost = Utilities.GetTotalVesselCost(ShipNode, false);
                IntegrationCost = (float)Formula.GetIntegrationCost(this);
            }

            return Cost + IntegrationCost;
        }

        public double GetRushEfficiencyCost()
        {
            double effic = LC.Efficiency;
            double newEffic = effic * 0.9d;
            return effic - newEffic;
        }

        public bool RemoveFromBuildList(out int oldIndex)
        {
            bool removed = false;
            oldIndex = -1;
            LC = null; //force a refind
            if (LC == null) //I know this looks goofy, but it's a self-caching property that caches on "get"
            {
                KCTDebug.Log("Could not find the LC to remove vessel!");
                return false;
            }
            else
            {
                oldIndex = LC.Warehouse.IndexOf(this);
                if (oldIndex >= 0)
                {
                    removed = true;
                    LC.Warehouse.RemoveAt(oldIndex);
                    oldIndex = -1; // report notfound for removed from warehouse
                }
                if (!removed)
                {
                    oldIndex = LC.BuildList.IndexOf(this);
                    if (oldIndex >= 0)
                    {
                        removed = true;
                        LC.BuildList.RemoveAt(oldIndex);
                        LC.RecalculateBuildRates();
                    }
                }
            }
            KCTDebug.Log($"Removing {ShipName} from {LC.Name} storage/list.");
            if (!removed)
            {
                KCTDebug.Log("Failed to remove ship from list! Performing direct comparison of ids...");

                for (int i = LC.BuildList.Count; i-- > 0;)
                {
                    BuildListVessel blv = LC.BuildList[i];
                    if (blv.Id == Id)
                    {
                        KCTDebug.Log("Ship found in BuildList. Removing...");
                        removed = true;
                        LC.BuildList.RemoveAt(i);
                        oldIndex = i;
                        LC.RecalculateBuildRates();
                        break;
                    }
                }
                if (!removed)
                {
                    for( int i = LC.Warehouse.Count; i-- > 0;)
                    {
                        BuildListVessel blv = LC.Warehouse[i];
                        if (blv.Id == Id)
                        {
                            KCTDebug.Log("Ship found in Warehouse list. Removing...");
                            oldIndex = -1; // report notfound for removed from warehouse
                            removed = true;
                            LC.Warehouse.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            if (removed)
            {
                KCTDebug.Log("Sucessfully removed vessel from LC.");
            }
            else 
                KCTDebug.Log("Still couldn't remove ship!");
            return removed;
        }

        public List<PseudoPart> GetPseudoParts()
        {
            List<PseudoPart> retList = new List<PseudoPart>();

            foreach (ConfigNode cn in ShipNode.GetNodes("PART"))
            {
                string name = cn.GetValue("part");
                string pID;
                if (name != null)
                {
                    string[] split = name.Split('_');
                    name = split[0];
                    pID = split[1];
                }
                else
                {
                    name = cn.GetValue("name");
                    pID = cn.GetValue("uid");
                }

                PseudoPart returnPart = new PseudoPart(name, pID);
                retList.Add(returnPart);
            }
            return retList;
        }

        public bool AreAllPartsValid()
        {
            //loop through the ship's parts and check if any don't have AvailableParts that match.

            bool valid = true;
            foreach (ConfigNode pNode in ShipNode.GetNodes("PART"))
            {
                if (Utilities.GetAvailablePartByName(Utilities.GetPartNameFromNode(pNode)) == null)
                {
                    valid = false;
                    break;
                }
            }

            return valid;
        }

        public List<string> GetMissingParts()
        {
            List<string> missing = new List<string>();
            foreach (ConfigNode pNode in ShipNode.GetNodes("PART"))
            {
                string name = Utilities.GetPartNameFromNode(pNode);
                if (Utilities.GetAvailablePartByName(name) == null)
                {
                    //invalid part detected!
                    missing.Add(name);
                }
            }
            return missing;
        }

        public Dictionary<AvailablePart, PartPurchasability> GetPartsWithPurchasability()
        {
            var res = new Dictionary<AvailablePart, PartPurchasability>();

            if (ResearchAndDevelopment.Instance == null)
                return res;

            List<AvailablePart> apList = new List<AvailablePart>();
            foreach (ConfigNode pNode in ShipNode.GetNodes("PART"))
            {
                string partName = Utilities.GetPartNameFromNode(pNode);
                AvailablePart part = PartLoader.getPartInfoByName(partName);
                apList.Add(part);
            }

            res = Utilities.GetPartsWithPurchasability(apList);
            return res;
        }

        public double GetEffectiveCost(List<Part> parts)
        {
            double totalEffectiveCost = 0;
            foreach (Part p in parts)
            {
                totalEffectiveCost += GetEffectiveCostInternal(p);
            }

            double globalMultiplier = ApplyGlobalCostModifiers() * RP0.Leaders.LeaderUtils.GetGlobalEffectiveCostEffect(globalTags, resourceAmounts);
            double multipliedCost = totalEffectiveCost * globalMultiplier;
            KCTDebug.Log($"Total eff cost: {totalEffectiveCost}; global mult: {globalMultiplier}; multiplied cost: {multipliedCost}");

            return multipliedCost;
        }


        public double GetEffectiveCost(List<ConfigNode> parts)
        {
            double totalEffectiveCost = 0;
            foreach (ConfigNode p in parts)
            {
                totalEffectiveCost += GetEffectiveCostInternal(p);
            }

            double globalMultiplier = ApplyGlobalCostModifiers() * RP0.Leaders.LeaderUtils.GetGlobalEffectiveCostEffect(globalTags, resourceAmounts);
            double multipliedCost = totalEffectiveCost * globalMultiplier;
            KCTDebug.Log($"Total eff cost: {totalEffectiveCost}; global mult: {globalMultiplier}; multiplied cost: {multipliedCost}");

            return multipliedCost;
        }
        
        // A little silly, but made to mirror ShipConstruction.GetPartCostsAndMass
        private static void GetPartCostsAndMass(Part p, out float dryCost, out float fuelCost, out float dryMass, out float fuelMass, Dictionary<string, double> resources)
        {
            dryCost = (float)GetPartCosts(p, false);
            fuelCost = (float)GetPartCosts(p) - dryCost;
            dryMass = p.mass;
            double fMass = 0;
            for (int i = p.Resources.Count; i-- > 0;)
            {
                PartResource res = p.Resources[i];
                fMass += res.amount * res.info.density;
                resources.TryGetValue(res.resourceName, out double amt);
                amt += res.maxAmount;
                resources[res.resourceName] = amt;
            }
            fuelMass = (float)fMass;
        }

        private static Dictionary<string, double> _resourceAmounts = new Dictionary<string, double>();
        private static HashSet<string> _tags = new HashSet<string>();

        private double GetEffectiveCostInternal(object o)
        {
            if (!(o is Part) && !(o is ConfigNode))
                return 0;

            string name = (o as Part)?.partInfo.name ?? Utilities.GetPartNameFromNode(o as ConfigNode);
            Part partRef = o as Part ?? Utilities.GetAvailablePartByName(name).partPrefab;

            float dryCost;
            float fuelCost;
            float dryMass;
            float fuelMass;

            if (o is ConfigNode)
                ShipConstruction.GetPartCostsAndMass(o as ConfigNode, Utilities.GetAvailablePartByName(name), out dryCost, out fuelCost, out dryMass, out fuelMass);
            else
            {
                GetPartCostsAndMass(partRef, out dryCost, out fuelCost, out dryMass, out fuelMass, _resourceAmounts);
            }

            float wetMass = dryMass + fuelMass;
            float cost = dryCost + fuelCost;

            double partMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetPartVariable(name);
            double moduleMultiplier = ApplyModuleCostModifiers(partRef, out bool applyResourceMods);

            // Resource contents may not match the prefab (ie, ModularFuelTanks implementation)
            double resourceMultiplier = 1d;

            if (o is ConfigNode)
            {
                var resourceNames = applyResourceMods ? new List<string>() : null;
                foreach (ConfigNode rNode in (o as ConfigNode).GetNodes("RESOURCE"))
                {
                    string rName = rNode.GetValue("name");
                    _resourceAmounts[rName] = double.Parse(rNode.GetValue("maxAmount"));
                    resourceNames?.Add(rName);
                }
                if (applyResourceMods)
                    resourceMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetResourceVariable(resourceNames);
            }
            else if (applyResourceMods)
                resourceMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetResourceVariable(partRef.Resources);

            GatherGlobalModifiers(_tags, partRef);
            foreach (var s in _tags)
                globalTags.Add(s);

            foreach (var kvp in _resourceAmounts)
            {
                resourceAmounts.TryGetValue(kvp.Key, out double amt);
                amt += kvp.Value;
                resourceAmounts[kvp.Key] = amt;
            }

            
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
            effectiveCost *= RP0.Leaders.LeaderUtils.GetPartEffectiveCostEffect(_tags, _resourceAmounts, name);

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

            _tags.Clear();
            _resourceAmounts.Clear();

            return effectiveCost;
        }

        public static void GatherGlobalModifiers(HashSet<string> modifiers, Part p)
        {
            PresetManager.Instance.ActivePreset.PartVariables.SetGlobalVariables(modifiers, p.Modules);
            if (p.Modules.GetModule<ModuleTagList>() is ModuleTagList pm)
                foreach (var x in pm.tags)
                    if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod) && mod.globalMult != 1)
                        modifiers.Add(mod.name);
        }

        public double ApplyGlobalCostModifiers()
        {
            IsHumanRated = false;
            double costMod = PresetManager.Instance.ActivePreset.PartVariables.GetGlobalVariable(globalTags);
            foreach (var x in globalTags)
            {
                if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod))
                {
                    costMod *= mod.globalMult;
                    IsHumanRated |= mod.isHumanRating;
                }
            }
            return costMod;
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

        public bool AreAllPartsUnlocked() => GetPartsWithPurchasability().Values.All(v => v.Status == PurchasabilityStatus.Purchased);

        public string GetItemName() => ShipName;

        public double GetBuildRate() => BuildRate;

        public double UpdateBuildRate()
        {
            _buildRate = Utilities.GetBuildRate(this) * LC?.StrategyRateMultiplier ?? 1d;
            if (_buildRate < 0d)
                _buildRate = 0d;

            return _buildRate;
        }

        public double GetFractionComplete() => Progress / (BuildPoints + IntegrationPoints);

        public double GetTimeLeft() => TimeLeft;
        public double GetTimeLeftEst(double offset)
        {
            if (BuildRate > 0)
                return TimeLeft;

            double bp = BuildPoints + IntegrationPoints;
            double rate = Utilities.GetBuildRate(LC, GetTotalMass(), bp, IsHumanRated)
                        * LC.Efficiency * LC.StrategyRateMultiplier;
            return (bp - Progress) / rate;
        }

        public ListType GetListType() => Type;

        public bool IsComplete() => Progress >= BuildPoints + IntegrationPoints;

        public double IncrementProgress(double UTDiff)
        {
            double bR = BuildRate;
            if (bR == 0)
                return 0d;

            double toGo = BuildPoints + IntegrationPoints - Progress;
            double amt = bR * UTDiff;
            Progress += bR * UTDiff;
            if (IsComplete())
            {
                Utilities.MoveVesselToWarehouse(this);
                if (amt > toGo)
                    return (1d - toGo / amt) * UTDiff;
            }

            return 0d;
        }
    }

    public class PseudoPart
    {
        public string Name;
        public uint Uid;

        public PseudoPart(string PartName, uint ID)
        {
            Name = PartName;
            Uid = ID;
        }

        public PseudoPart(string PartName, string ID)
        {
            Name = PartName;
            Uid = uint.Parse(ID);
        }
    }

    public enum PurchasabilityStatus { Unavailable = 0, Purchasable = 1, Purchased = 2 }

    public struct PartPurchasability
    {
        public PurchasabilityStatus Status;
        public int PartCount;

        public PartPurchasability(PurchasabilityStatus status, int partCount)
        {
            Status = status;
            PartCount = partCount;
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
