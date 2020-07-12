using KSP.UI;
using PreFlightTests;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class BuildListVessel : IKCTBuildItem
    {
        public enum ListType { None, VAB, SPH, TechNode, Reconditioning, KSC };

        public double Progress, EffectiveCost, BuildPoints, IntegrationPoints;
        public string LaunchSite, Flag, ShipName;
        public int LaunchSiteID = -1;
        public ListType Type;
        public ConfigNode ShipNode;
        public Guid Id;
        public bool CannotEarnScience;
        public float Cost = 0, IntegrationCost;
        public float TotalMass = 0, DistanceFromKSC = 0;
        public int RushBuildClicks = 0;
        public int NumStages = 0;
        public int NumStageParts = 0;
        public double StagePartCost = 0;
        public float EmptyCost = 0, EmptyMass = 0;

        internal ShipConstruct _ship;
        private double _rushCost = -1;

        public double BuildRate => Utilities.GetBuildRate(this);

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

        private KSCItem _ksc = null;
        public KSCItem KSC
        {
            get
            {
                if (_ksc == null)
                {
                    _ksc = KCTGameStates.KSCs.FirstOrDefault(k => k.VABList.FirstOrDefault(s => s.Id == Id) != null ||
                                                                   k.VABWarehouse.FirstOrDefault(s => s.Id == Id) != null ||
                                                                   k.SPHList.FirstOrDefault(s => s.Id == Id) != null ||
                                                                   k.SPHWarehouse.FirstOrDefault(s => s.Id == Id) != null);
                }
                return _ksc;
            }
            set
            {
                _ksc = value;
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

        public BuildListVessel(ShipConstruct s, string ls, double effCost, double bP, string flagURL)
        {
            _ship = s;
            ShipNode = s.SaveShip();
            ShipName = s.shipName;
            Cost = s.GetShipCosts(out EmptyCost, out _);
            TotalMass = s.GetShipMass(true, out EmptyMass, out _);

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
            EffectiveCost = effCost;
            BuildPoints = bP;
            Progress = 0;
            Flag = flagURL;
            if (s.shipFacility == EditorFacility.VAB)
                Type = ListType.VAB;
            else if (s.shipFacility == EditorFacility.SPH)
                Type = ListType.SPH;
            else
                Type = ListType.None;
            Id = Guid.NewGuid();
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

            if (EffectiveCost == default)
            {
                // Can only happen in older saves that didn't have Effective cost persisted as a separate field
                // This code should be safe to remove after a while.
                EffectiveCost = Utilities.GetEffectiveCost(ShipNode.GetNodes("PART").ToList());
            }

            IntegrationPoints = MathParser.ParseIntegrationTimeFormula(this);
            IntegrationCost = (float)MathParser.ParseIntegrationCostFormula(this);
        }

        public BuildListVessel(string name, string ls, double effCost, double bP, double integrP, string flagURL, float spentFunds, float integrCost, int EditorFacility)
        {
            LaunchSite = ls;
            ShipName = name;
            EffectiveCost = effCost;
            BuildPoints = bP;
            IntegrationPoints = integrP;
            Progress = 0;
            Flag = flagURL;
            if (EditorFacility == (int)EditorFacilities.VAB)
                Type = ListType.VAB;
            else if (EditorFacility == (int)EditorFacilities.SPH)
                Type = ListType.SPH;
            else
                Type = ListType.None;
            CannotEarnScience = false;
            Cost = spentFunds;
            IntegrationCost = integrCost;
        }

        public BuildListVessel(ProtoVessel pvessel, ConfigNode vesselNode, ListType listType = ListType.None) //For recovered vessels
        {
            Id = Guid.NewGuid();
            ShipName = pvessel.vesselName;
            ShipNode = vesselNode;

            if (listType != ListType.None)
                Type = listType;

            Cost = Utilities.GetTotalVesselCost(ShipNode);
            EmptyCost = Utilities.GetTotalVesselCost(ShipNode, false);
            TotalMass = 0;
            EmptyMass = 0;

            HashSet<int> stages = new HashSet<int>();

            foreach (ProtoPartSnapshot p in pvessel.protoPartSnapshots)
            {
                stages.Add(p.inverseStageIndex);

                if (p.partPrefab != null && p.partPrefab.Modules.Contains<LaunchClamp>())
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

            BuildPoints = Utilities.GetBuildTime(ShipNode.GetNodes("PART").ToList());
            Flag = HighLogic.CurrentGame.flagURL;
            Progress = BuildPoints;

            DistanceFromKSC = 0; // (float)SpaceCenter.Instance.GreatCircleDistance(SpaceCenter.Instance.cb.GetRelSurfaceNVector(vessel.latitude, vessel.longitude));
            RushBuildClicks = 0;
        }

        /// <summary>
        /// For recovered vessels
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="listType"></param>
        public BuildListVessel(Vessel vessel, ListType listType = ListType.None)
        {
            Id = Guid.NewGuid();
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

                if (p.partPrefab != null && p.partPrefab.Modules.Contains<LaunchClamp>())
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

            EffectiveCost = Utilities.GetEffectiveCost(ShipNode.GetNodes("PART").ToList());
            BuildPoints = Utilities.GetBuildTime(EffectiveCost);
            Flag = HighLogic.CurrentGame.flagURL;

            DistanceFromKSC = (float)SpaceCenter.Instance.GreatCircleDistance(SpaceCenter.Instance.cb.GetRelSurfaceNVector(vessel.latitude, vessel.longitude));

            RushBuildClicks = 0;
            IntegrationPoints = MathParser.ParseIntegrationTimeFormula(this);
            IntegrationCost = (float)MathParser.ParseIntegrationCostFormula(this);

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
            // These are actually needed, do not comment them out
            VesselToSave.SetRotation(OriginalRotation);
            VesselToSave.SetPosition(OriginalPosition);
            //End of Claw's code. Thanks Claw!
            return cn;
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
                    SanitizeNode(Utilities.PartNameFromNode(part), module, templates);
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
            BuildListVessel ret = new BuildListVessel(ShipName, LaunchSite, EffectiveCost, BuildPoints, IntegrationPoints, Flag, Cost, IntegrationCost, (int)GetEditorFacility())
            {
                ShipNode = ShipNode.CreateCopy()
            };

            //refresh all inventory parts to new
            for (int i = ret.ExtractedPartNodes.Count - 1; i >= 0; i--)
            {
                ConfigNode part = ret.ExtractedPartNodes[i];
                ScrapYardWrapper.RefreshPart(part);
            }

            ret.Id = Guid.NewGuid();
            ret.TotalMass = TotalMass;
            ret.EmptyMass = EmptyMass;
            ret.Cost = Cost;
            ret.IntegrationCost = IntegrationCost;
            ret.EmptyCost = EmptyCost;
            ret.NumStageParts = NumStageParts;
            ret.NumStages = NumStages;
            ret.StagePartCost = StagePartCost;

            if (RecalcTime)
            {
                ret.EffectiveCost = Utilities.GetEffectiveCost(ret.ExtractedPartNodes);
                ret.BuildPoints = Utilities.GetBuildTime(ret.EffectiveCost);
                ret.IntegrationPoints = MathParser.ParseIntegrationTimeFormula(ret);
                ret.IntegrationCost = (float)MathParser.ParseIntegrationCostFormula(ret);
            }

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
            KCTDebug.Log("Brute force looking for "+ShipName);
            bool found = false;
            found = KSC.VABList.Exists(b => b.Id == Id);
            if (found) { Type = ListType.VAB; return; }
            found = KSC.VABWarehouse.Exists(b => b.Id == Id);
            if (found) { Type = ListType.VAB; return; }

            found = KSC.SPHList.Exists(b => b.Id == Id);
            if (found) { Type = ListType.SPH; return; }
            found = KSC.SPHWarehouse.Exists(b => b.Id == Id);
            if (found) { Type = ListType.SPH; return; }

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

            string tempFile = $"{KSPUtil.ApplicationRootPath}saves/{HighLogic.SaveFolder}/Ships/temp.craft";
            UpdateRFTanks();
            if (fillFuel)
                FillUnlockedFuelTanks();
            ShipNode.Save(tempFile);
            FlightDriver.StartWithNewLaunch(tempFile, Flag, LaunchSite, new VesselCrewManifest());
            KCTGameStates.LaunchFromTS = false;
            if (KCTGameStates.AirlaunchParams != null) KCTGameStates.AirlaunchParams.KSPVesselId = null;
        }

        public List<string> MeetsFacilityRequirements(bool highestFacility = true)
        {
            List<string> failedReasons = new List<string>();
            if (!Utilities.CurrentGameIsCareer())
                return failedReasons;

            ShipTemplate template = new ShipTemplate();
            template.LoadShip(ShipNode);

            if (Type == ListType.VAB)
            {
                KCT_LaunchPad selectedPad = highestFacility ? KCTGameStates.ActiveKSC.GetHighestLevelLaunchPad() : KCTGameStates.ActiveKSC.ActiveLPInstance;
                float launchpadNormalizedLevel = 1f * selectedPad.level / KCTGameStates.BuildingMaxLevelCache["LaunchPad"];

                double totalMass = GetTotalMass();
                if (totalMass > GameVariables.Instance.GetCraftMassLimit(launchpadNormalizedLevel, true))
                {
                    failedReasons.Add($"Mass limit exceeded, currently at {totalMass:N} tons");
                }
                if (ExtractedPartNodes.Count > GameVariables.Instance.GetPartCountLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.VehicleAssemblyBuilding), true))
                {
                    failedReasons.Add("Part Count limit exceeded");
                }
                CraftWithinSizeLimits sizeCheck = new CraftWithinSizeLimits(template, SpaceCenterFacility.LaunchPad, GameVariables.Instance.GetCraftSizeLimit(launchpadNormalizedLevel, true));
                if (!sizeCheck.Test())
                {
                    failedReasons.Add("Size limits exceeded");
                }
            }
            else if (Type == ListType.SPH)
            {
                double totalMass = GetTotalMass();
                if (totalMass > GameVariables.Instance.GetCraftMassLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Runway), false))
                {
                    failedReasons.Add($"Mass limit exceeded, currently at {totalMass:N} tons");
                }
                if (ExtractedPartNodes.Count > GameVariables.Instance.GetPartCountLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.SpaceplaneHangar), false))
                {
                    failedReasons.Add("Part Count limit exceeded");
                }
                CraftWithinSizeLimits sizeCheck = new CraftWithinSizeLimits(template, SpaceCenterFacility.Runway, GameVariables.Instance.GetCraftSizeLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Runway), false));
                if (!sizeCheck.Test())
                {
                    failedReasons.Add("Size limits exceeded");
                }
            }

            Dictionary<AvailablePart, int> lockedParts = GetLockedParts();
            if (lockedParts?.Count > 0)
            {
                var msg = Utilities.ConstructLockedPartsWarning(lockedParts);
                failedReasons.Add(msg);
            }

            return failedReasons;
        }

        public ListType FindTypeFromLists()
        {
            if (KSC == null  || KSC.VABList == null || KSC.SPHList == null)
            {
                Type = ListType.None;
                return Type;
            }
            BruteForceLocateVessel();
            return Type;
        }

        private void UpdateRFTanks()
        {
            var nodes = ShipNode.GetNodes("PART");
            for (int i = nodes.Count() - 1; i >= 0; i--)
            {
                ConfigNode cn = nodes[i];

                var modules = cn.GetNodes("MODULE");
                for (int im = modules.Count() - 1; im >= 0; im--)
                {
                    ConfigNode module = modules[im];
                    if (module.GetValue("name") == "ModuleFuelTanks")
                    {
                        if (module.HasValue("timestamp"))
                        {
                            KCTDebug.Log("Updating RF timestamp on a part");
                            module.SetValue("timestamp", Planetarium.GetUniversalTime().ToString());
                        }
                    }
                }
            }
        }

        public bool AreTanksFull()
        {
            foreach (ConfigNode p in ShipNode.GetNodes("PART"))
            {
                if (Utilities.PartIsProcedural(p))
                {
                    var resList = p.GetNodes("RESOURCE");
                    foreach (var res in resList)
                    {
                        if (GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(res.GetValue("name")))
                        {
                            bool flowState = bool.Parse(res.GetValue("flowState"));
                            if (flowState)
                            {
                                var maxAmt = float.Parse( res.GetValue("maxAmount"));
                                var amt = float.Parse(res.GetValue("amount"));
                                if (Math.Abs(amt-maxAmt) >= 1)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
                else
                {
                    var resList = p.GetNodes("RESOURCE");
                    foreach (var res in resList)
                    {
                        var name = res.GetValue("name");
                        if (GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(name))
                        {
                            bool flowState = bool.Parse(res.GetValue("flowState"));
                            if (flowState)
                            {
                                var maxAmt = float.Parse(res.GetValue("maxAmount"));
                                var amt = float.Parse(res.GetValue("amount"));
                                if (Math.Abs(amt - maxAmt) >= 1)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }
            return true;
        }

        private void FillUnlockedFuelTanks()
        {
            foreach (ConfigNode p in ShipNode.GetNodes("PART"))
            {
                //fill as part prefab would be filled?
                if (Utilities.PartIsProcedural(p))
                {
                    var resList = p.GetNodes("RESOURCE");
                    foreach (var res in resList)
                    {
                        if (GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(res.GetValue("name")))
                        {
                            bool flowState = bool.Parse(res.GetValue("flowState"));
                            if (flowState)
                            {
                                var maxAmt = res.GetValue("maxAmount");
                                res.SetValue("amount", maxAmt);
                            }
                        }
                    }
                }
                else
                {
                    var resList = p.GetNodes("RESOURCE");
                    foreach (var res in resList)
                    {
                        var name = res.GetValue("name");
                        if (GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(name))
                        {
                            bool flowState = bool.Parse(res.GetValue("flowState"));
                            if (flowState)
                            {
                                var maxAmt = res.GetValue("maxAmount");
                                res.SetValue("amount", maxAmt);
                            }
                        }
                    }
                }
            }
        }

        public double GetTotalMass()
        {
            if (TotalMass != 0 && EmptyMass != 0) return TotalMass;
            TotalMass = 0;
            EmptyMass = 0;
            for (int i = ExtractedPartNodes.Count - 1; i >= 0; i--)
            {
                ConfigNode p = ExtractedPartNodes[i];
                TotalMass += Utilities.GetPartMassFromNode(p, includeFuel: true, includeClamps: false);
                EmptyMass += Utilities.GetPartMassFromNode(p, includeFuel: false, includeClamps: false);
            }
            if (TotalMass < 0)
                TotalMass = 0;
            if (EmptyMass < 0)
                EmptyMass = 0;
            return TotalMass;
        }

        public double GetTotalCost()
        {
            if (Cost == 0 || EmptyCost == 0)
            {
                Cost = Utilities.GetTotalVesselCost(ShipNode);
                EmptyCost = Utilities.GetTotalVesselCost(ShipNode, false);
                IntegrationCost = (float)MathParser.ParseIntegrationCostFormula(this);
            }

            return Cost + IntegrationCost;
        }

        public double GetRushCost()
        {
            if (_rushCost > -1) return _rushCost;

            _rushCost = MathParser.ParseRushCostFormula(this);
            return _rushCost;
        }

        public bool DoRushBuild()
        {
            double rushCost = GetRushCost();
            if (Funding.Instance.Funds < rushCost) return false;

            double remainingBP = BuildPoints + IntegrationPoints - Progress;
            AddProgress(remainingBP * 0.1);
            Utilities.SpendFunds(rushCost, TransactionReasons.VesselRollout);
            ++RushBuildClicks;
            _rushCost = -1;    // force recalculation of rush cost

            return true;
        }

        public bool RemoveFromBuildList()
        {
            string typeName="";
            bool removed = false;
            KSC = null; //force a refind
            if (KSC == null) //I know this looks goofy, but it's a self-caching property that caches on "get"
            {
                KCTDebug.Log("Could not find the KSC to remove vessel!");
                return false;
            }
            if (Type == ListType.SPH)
            {

                removed = KSC.SPHWarehouse.Remove(this);
                if (!removed)
                {
                    removed = KSC.SPHList.Remove(this);
                }
                typeName="SPH";
            }
            else if (Type == ListType.VAB)
            {
                removed = KSC.VABWarehouse.Remove(this);
                if (!removed)
                {
                    removed = KSC.VABList.Remove(this);
                }
                typeName="VAB";
            }
            KCTDebug.Log("Removing " + ShipName + " from "+ typeName +" storage/list.");
            if (!removed)
            {
                KCTDebug.Log("Failed to remove ship from list! Performing direct comparison of ids...");
                foreach (BuildListVessel blv in KSC.SPHWarehouse)
                {
                    if (blv.Id == Id)
                    {
                        KCTDebug.Log("Ship found in SPH storage. Removing...");
                        removed = KSC.SPHWarehouse.Remove(blv);
                        break;
                    }
                }
                if (!removed)
                {
                    foreach (BuildListVessel blv in KSC.VABWarehouse)
                    {
                        if (blv.Id == Id)
                        {
                            KCTDebug.Log("Ship found in VAB storage. Removing...");
                            removed = KSC.VABWarehouse.Remove(blv);
                            break;
                        }
                    }
                }
                if (!removed)
                {
                    foreach (BuildListVessel blv in KSC.VABList)
                    {
                        if (blv.Id == Id)
                        {
                            KCTDebug.Log("Ship found in VAB List. Removing...");
                            removed = KSC.VABList.Remove(blv);
                            break;
                        }
                    }
                }
                if (!removed)
                {
                    foreach (BuildListVessel blv in KSC.SPHList)
                    {
                        if (blv.Id == Id)
                        {
                            KCTDebug.Log("Ship found in SPH list. Removing...");
                            removed = KSC.SPHList.Remove(blv);
                            break;
                        }
                    }
                }
            }
            if (removed) KCTDebug.Log("Sucessfully removed ship from storage.");
            else KCTDebug.Log("Still couldn't remove ship!");
            return removed;
        }

        public List<PseudoPart> GetPseudoParts()
        {
            List<PseudoPart> retList = new List<PseudoPart>();
            ConfigNode[] partNodes = ShipNode.GetNodes("PART");

            foreach (ConfigNode cn in partNodes)
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
                if (Utilities.GetAvailablePartByName(Utilities.PartNameFromNode(pNode)) == null)
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
                string name = Utilities.PartNameFromNode(pNode);
                if (Utilities.GetAvailablePartByName(name) == null)
                {
                    //invalid part detected!
                    missing.Add(name);
                }
            }
            return missing;
        }

        public bool AreAllPartsUnlocked()
        {
            if (ResearchAndDevelopment.Instance == null)
                return true;

            foreach (ConfigNode pNode in ShipNode.GetNodes("PART"))
            {
                if (!Utilities.PartIsUnlocked(pNode))
                    return false;
            }

            return true;
        }

        public Dictionary<AvailablePart, int> GetLockedParts()
        {
            var lockedPartsOnShip = new Dictionary<AvailablePart, int>();

            if (ResearchAndDevelopment.Instance == null)
                return lockedPartsOnShip;

            foreach (ConfigNode pNode in ShipNode.GetNodes("PART"))
            {
                string partName = Utilities.PartNameFromNode(pNode);
                if (!Utilities.PartIsUnlocked(partName))
                {
                    AvailablePart partInfoByName = PartLoader.getPartInfoByName(partName);
                    if (!lockedPartsOnShip.ContainsKey(partInfoByName))
                        lockedPartsOnShip.Add(partInfoByName, 1);
                    else
                        ++lockedPartsOnShip[partInfoByName];
                }
            }

            return lockedPartsOnShip;
        }

        public double ProgressPercent()
        {
            return 100 * (Progress / (BuildPoints + IntegrationPoints));
        }

        public string GetItemName() => ShipName;

        public double GetBuildRate() => BuildRate;

        public double GetTimeLeft()=> TimeLeft;

        public ListType GetListType() => Type;

        public bool IsComplete() => Progress >= BuildPoints + IntegrationPoints;

        public void IncrementProgress(double UTDiff)
        {
            double buildRate = Utilities.GetBuildRate(this);
            AddProgress(buildRate * UTDiff);
            if (IsComplete())
                Utilities.MoveVesselToWarehouse(this);
        }

        private double AddProgress(double toAdd)
        {
            Progress += toAdd;
            return Progress;
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
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
