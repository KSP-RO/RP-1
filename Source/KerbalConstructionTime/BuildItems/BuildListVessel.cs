using KSP.UI;
using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using RP0.DataTypes;
using UnityEngine.Profiling;

namespace KerbalConstructionTime
{
    public class BuildListVessel : ConfigNodePersistenceBase, IKCTBuildItem, IConfigNode
    {
        public enum ClampsState
        {
            Untested,
            NoClamps,
            HasClamps,
        }

        public class TagsToEC: ConfigNodePersistenceBase, ICloneable
        {
            [Persistent] public PersistentListValueType<string> tags = new PersistentListValueType<string>();
            [Persistent] public double ec;

            public bool SameTags(List<string> b)
            {
                int iC = tags.Count;
                if (b.Count != iC)
                    return false;
                for (int i = iC; i-- > 0;)
                    if (tags[i] != b[i])
                        return false;

                return true;
            }

            public object Clone()
            {
                var ret = new TagsToEC();
                ret.tags.AddRange(tags);
                ret.ec = ec;
                return ret;
            }
        }

        public enum ListType { None, VAB, SPH, TechNode, Reconditioning, KSC, AirLaunch, Crew };

        [Persistent]
        public double progress;
        [Persistent]
        public double effectiveCost;
        [Persistent]
        public double buildPoints;
        [Persistent]
        public double integrationPoints;
        [Persistent]
        public string launchSite;
        [Persistent]
        public string flag;
        [Persistent]
        public string shipName;
        [Persistent]
        public int launchSiteIndex = -1;
        [Persistent]
        public ListType Type = ListType.None;
        [Persistent]
        public Guid shipID;
        [Persistent]
        public bool cannotEarnScience;
        [Persistent]
        public bool humanRated;
        [Persistent]
        public float cost = 0;
        [Persistent]
        public float integrationCost;
        [Persistent]
        public float mass = 0;
        [Persistent]
        public float kscDistance = 0;
        [Persistent]
        public int numStages = 0;
        [Persistent]
        public int numStageParts = 0;
        [Persistent]
        public double stagePartCost = 0;
        [Persistent]
        public float emptyCost = 0;
        [Persistent]
        public float emptyMass = 0;
        [Persistent]
        public ClampsState clampState = ClampsState.Untested;
        [Persistent]
        public EditorFacility FacilityBuiltIn;
        [Persistent]
        public string KCTPersistentID;
        [Persistent]
        public Vector3 ShipSize = Vector3.zero;
        [Persistent]
        public PersistentDictionaryValueTypes<string, double> resourceAmounts = new PersistentDictionaryValueTypes<string, double>();
        [Persistent]
        public PersistentHashSetValueType<string> globalTags = new PersistentHashSetValueType<string>();
        [Persistent]
        public PersistentList<TagsToEC> tagEffectiveCosts = new PersistentList<TagsToEC>();
        [Persistent]
        private bool tanksFull = true;
        [Persistent]
        private bool isCrewable = false;
        [Persistent]
        private PersistentHashSetValueType<string> partNames = new PersistentHashSetValueType<string>();
        [Persistent]
        private PersistentCompressedCraftNode ShipNodeCompressed = new PersistentCompressedCraftNode();

        public string LandedAt = "";
        private double _buildRate = -1d;
        private double _leaderEffect = -1d;
        public double LeaderEffect => _leaderEffect < 0 ? UpdateLeaderEffect() : _leaderEffect;

        internal ShipConstruct _ship;

        public double BuildRate => (_buildRate < 0 ? UpdateBuildRate() : _buildRate)
            * (LC == null ? 1d : _lc.Efficiency * _lc.RushRate);

        public bool IsValid => Type != ListType.None;

        private List<ConfigNode> ExtractedPartNodes => ShipNodeCompressed.Node.GetNodes("PART").ToList();

        public bool IsFinished => progress >= buildPoints + integrationPoints;

        public KSCItem KSC
        {
            get
            {
                if (LC == null)
                    return null;

                return LC.KSC;
            }
        }

        [Persistent]
        private Guid _lcID = Guid.Empty;
        public Guid LCID
        {
            get
            {
                return _lcID;
            }
            set
            {
                _lcID = value;
                if (_lcID == Guid.Empty)
                    _lc = null;
                else
                    _lc = KerbalConstructionTimeData.Instance.LC(_lcID);
            }
        }

        private LCItem _lc = null;
        public LCItem LC
        {
            get
            {
                if (_lc == null)
                {
                    _lc = KerbalConstructionTimeData.Instance.LC(_lcID);
                }
                return _lc;
            }
            set
            {
                _lc = value;
                if (_lc == null)
                    _lcID = Guid.Empty;
                else
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
        public List<string> desiredManifest { set; get; } = new List<string>();

        public BuildListVessel() { }

        public BuildListVessel(ShipConstruct s, string ls, string flagURL, bool storeConstruct)
        {
            Profiler.BeginSample("RP0BuildListVessel ctor");
            _ship = s;
            CacheClamps(s.parts);

            StorePartNames(s.parts);
            // Override KSP sizing of the ship construct
            ShipSize = Utilities.GetShipSize(s, true, false);

            if (storeConstruct)
                StoreShipConstruct(s);

            shipName = s.shipName;
            cost = Utilities.GetTotalVesselCost(s.parts, true);
            emptyCost = Utilities.GetTotalVesselCost(s.parts, false);
            mass = Utilities.GetShipMass(s, true, out emptyMass, out _);
            tanksFull = AreTanksFull(s.parts);
            isCrewable = IsCrewable(s.parts);

            effectiveCost = GetEffectiveCost(s.parts);
            buildPoints = Formula.GetVesselBuildPoints(effectiveCost);

            HashSet<int> stages = new HashSet<int>();
            numStageParts = 0;
            stagePartCost = 0d;

            Profiler.BeginSample("RP0StageCosts");
            foreach (Part p in s.Parts)
            {
                if (p.stagingOn)
                {
                    stages.Add(p.inverseStage);
                    ++numStageParts;
                    stagePartCost += p.GetModuleCosts(p.partInfo.cost, ModifierStagingSituation.CURRENT) + p.partInfo.cost;
                }
            }
            numStages = stages.Count;
            Profiler.EndSample();

            launchSite = ls;
            progress = 0;
            flag = flagURL;
            // This is always called from within the editor, so the shipconstruct will have a facility.
            if (s.shipFacility == EditorFacility.SPH)
            {
                Type = ListType.SPH;
                FacilityBuiltIn = EditorFacility.SPH;
                _lc = KCTGameStates.ActiveKSC.Hangar;
            }
            else
            {
                Type = ListType.VAB;
                FacilityBuiltIn = EditorFacility.VAB;
                _lc = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance;
                if (_lc.LCType == LaunchComplexType.Hangar)
                    KCTDebug.LogError($"ERROR: Tried to add vessel {shipName} to LC {_lc.Name} but vessel is type VAB!");
            }

            if(_lc != null && !_lc.IsOperational)
                KCTDebug.LogError($"ERROR: Tried to add vessel {shipName} to LC {_lc.Name} but LC is not operational!");

            shipID = Guid.NewGuid();
            KCTPersistentID = Guid.NewGuid().ToString("N");
            cannotEarnScience = false;

            //get the crew from the editorlogic
            desiredManifest = new List<string>();
            if (CrewAssignmentDialog.Instance?.GetManifest()?.CrewCount > 0)
            {
                foreach (ProtoCrewMember crew in CrewAssignmentDialog.Instance.GetManifest().GetAllCrew(true) ?? new List<ProtoCrewMember>())
                {
                    desiredManifest.Add(crew?.name ?? string.Empty);
                }
            }

            integrationPoints = Formula.GetIntegrationBP(this);
            integrationCost = (float)Formula.GetIntegrationCost(this);
            Profiler.EndSample();
        }

        public BuildListVessel(string name, string ls, double effCost, double bP, double integrP, string flagURL, float spentFunds, float integrCost, EditorFacility editorFacility, bool isHuman)
        {
            launchSite = ls;
            shipName = name;
            effectiveCost = effCost;
            buildPoints = bP;
            integrationPoints = integrP;
            progress = 0;
            flag = flagURL;
            humanRated = isHuman;
            Type = editorFacility == EditorFacility.VAB ? ListType.VAB : ListType.SPH;
            cannotEarnScience = false;
            cost = spentFunds;
            integrationCost = integrCost;
            FacilityBuiltIn = editorFacility;
        }

        /// <summary>
        /// For recovered vessels
        /// </summary>
        /// <param name="vessel"></param>
        /// <param name="listType"></param>
        public BuildListVessel(Vessel vessel, ListType listType)
        {
            shipID = Guid.NewGuid();
            KCTPersistentID = Guid.NewGuid().ToString("N");
            shipName = vessel.vesselName;
            StorePartNames(vessel.parts);
            ShipNodeCompressed.Node = FromInFlightVessel(vessel, listType);
            // don't compress and release, we need to make later changes
            Type = listType;
            FacilityBuiltIn = listType == ListType.SPH ? EditorFacility.SPH : EditorFacility.VAB;

            CacheClamps(vessel.parts);
            cost = Utilities.GetTotalVesselCost(vessel.parts);
            emptyCost = Utilities.GetTotalVesselCost(vessel.parts, false);
            mass = 0;
            emptyMass = 0;
            tanksFull = AreTanksFull(vessel.parts);
            isCrewable = IsCrewable(vessel.parts);

            HashSet<int> stages = new HashSet<int>();

            foreach (ProtoPartSnapshot p in vessel.protoVessel.protoPartSnapshots)
            {
                stages.Add(p.inverseStageIndex);

                if (p.IsClampOrChild())
                    continue;

                mass += p.mass;
                emptyMass += p.mass;

                foreach (ProtoPartResourceSnapshot rsc in p.resources)
                {
                    PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(rsc.resourceName);
                    if (def != null)
                        mass += def.density * (float)rsc.amount;
                }
            }
            cannotEarnScience = true;
            numStages = stages.Count;
            // FIXME ignore stageable part count and cost - it'll be fixed when we put this back in the editor.

            effectiveCost = GetEffectiveCost(vessel.parts);
            buildPoints = Formula.GetVesselBuildPoints(effectiveCost);
            flag = HighLogic.CurrentGame.flagURL;

            kscDistance = (float)SpaceCenter.Instance.GreatCircleDistance(SpaceCenter.Instance.cb.GetRelSurfaceNVector(vessel.latitude, vessel.longitude));

            integrationPoints = Formula.GetIntegrationBP(this);
            integrationCost = (float)Formula.GetIntegrationCost(this);

            progress = buildPoints + integrationPoints;
        }

        public override string ToString() => $"{Type}: {shipName}";

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
            cn.SetValue("size", KSPUtil.WriteVector(Utilities.GetShipSize(ConstructToSave, true, true)));
            // These are actually needed, do not comment them out
            VesselToSave.SetRotation(OriginalRotation);
            VesselToSave.SetPosition(OriginalPosition);
            //End of Claw's code. Thanks Claw!
            return cn;
        }

        private void StoreShipConstruct(ShipConstruct s)
        {
            if (s == null)
            {
                Debug.LogError("[RP-0] BLV: Tried to store a null shipconstruct. Probably copying a vessel outside the editor and the source had a null shipnode.");
                return;
            }

            Profiler.BeginSample("RP0SaveShip");
            ShipNodeCompressed.Node = s.SaveShip();
            Profiler.EndSample();
            ShipNodeCompressed.Node.SetValue("size", KSPUtil.WriteVector(ShipSize));
        }

        public void RecalculateFromNode(bool setValues = true)
        {
            bool oldHR = humanRated;
            double ec = GetEffectiveCost(ExtractedPartNodes);
            if (setValues)
            {
                effectiveCost = ec;
                buildPoints = Formula.GetVesselBuildPoints(effectiveCost);
                integrationPoints = Formula.GetIntegrationBP(this);
            }
            else
            {
                humanRated = oldHR;
            }
        }

        private ConfigNode SanitizeShipNode(ConfigNode node)
        {
            //PART, MODULE -> clean experiments, repack chutes, disable engines
            string filePath = $"{KSPUtil.ApplicationRootPath}GameData/RP-1/KCT/KCT_ModuleTemplates.cfg";
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

        public BuildListVessel CreateCopy()
        {
            BuildListVessel ret = new BuildListVessel(shipName, launchSite, effectiveCost, buildPoints, integrationPoints, flag, cost, integrationCost, FacilityBuiltIn, humanRated);
            ret._lc = _lc;
            ret._lcID = _lcID;
            ret.globalTags = globalTags.Clone() as PersistentHashSetValueType<string>;
            ret.tagEffectiveCosts = tagEffectiveCosts.Clone() as PersistentList<TagsToEC>;
            ret.partNames = partNames.Clone() as PersistentHashSetValueType<string>;
            ret.resourceAmounts = resourceAmounts.Clone() as PersistentDictionaryValueTypes<string, double>;
            if (ShipNodeCompressed.IsEmpty)
                ret.StoreShipConstruct(EditorLogic.fetch?.ship);
            else
                ret.ShipNodeCompressed.Copy(ShipNodeCompressed);
            ret.shipID = Guid.NewGuid();
            ret.KCTPersistentID = Guid.NewGuid().ToString("N");
            ret.mass = mass;
            ret.emptyMass = emptyMass;
            ret.cost = cost;
            ret.integrationCost = integrationCost;
            ret.emptyCost = emptyCost;
            ret.numStageParts = numStageParts;
            ret.numStages = numStages;
            ret.stagePartCost = stagePartCost;
            ret.ShipSize = ShipSize;
            ret.tanksFull = tanksFull;
            ret.isCrewable = isCrewable;

            ret.ShipNodeCompressed.CompressAndRelease();

            // Safe to always do these, they're cheap.
            ret.buildPoints = Formula.GetVesselBuildPoints(ret.effectiveCost);
            ret.integrationPoints = Formula.GetIntegrationBP(ret);
            ret.integrationCost = (float)Formula.GetIntegrationCost(ret);

            return ret;
        }

        public EditorFacilities GetEditorFacility()
        {
            return Type == ListType.SPH ? EditorFacilities.SPH : EditorFacilities.VAB;
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
            else if (HighLogic.LoadedSceneIsEditor)
            {
                KCTDebug.LogError("BLV tried to get ShipConstruct from within editor but _ship was null! Returning EditorLogic's shipconstruct.");
                return EditorLogic.fetch.ship;
            }
            return _ship;
        }

        public void UpdateNodeAndSave(string file, bool release = true)
        {
            ShipNodeCompressed.Node.SetValue("ship", shipName);
            ShipNodeCompressed.Node.Save(file);
            if (release)
                ShipNodeCompressed.CompressAndRelease();
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
            UpdateNodeAndSave(tempFile);
            string launchSiteName = launchSite;
            if (launchSiteName == "LaunchPad")
            {
                if (launchSiteIndex >= 0)
                    lc.SwitchLaunchPad(launchSiteIndex);

                KCT_LaunchPad pad = lc.ActiveLPInstance;
                
                launchSiteName = pad.launchSiteName;
            }

            Utilities.CleanupDebris(launchSiteName);
            KerbalConstructionTimeData.Instance.AirlaunchParams.KSPVesselId = Guid.Empty;
            FlightDriver.StartWithNewLaunch(tempFile, flag, launchSiteName, new VesselCrewManifest());
        }

        public bool ResourcesOK(LCData stats, List<string> failedReasons = null)
        {
            bool pass = true;
            HashSet<string> ignoredRes = stats.lcType == LaunchComplexType.Hangar ? GuiDataAndWhitelistItemsDatabase.HangarIgnoreRes : GuiDataAndWhitelistItemsDatabase.PadIgnoreRes;
            double massMin = Math.Max(Formula.ResourceValidationAbsoluteMassMin, Formula.ResourceValidationRatioOfVesselMassMin * mass);

            foreach (var kvp in resourceAmounts)
            {
                if (ignoredRes.Contains(kvp.Key)
                    || !GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(kvp.Key))
                    continue;

                if (stats.resourcesHandled.TryGetValue(kvp.Key, out double lcAmount) && lcAmount >= kvp.Value)
                    continue;

                if (PartResourceLibrary.Instance.GetDefinition(kvp.Key).density * kvp.Value <= massMin)
                    continue;

                if (failedReasons == null)
                    return false;

                pass = false;
                failedReasons.Add($"Insufficient {kvp.Key} at LC: {kvp.Value:N0} required, {lcAmount:N0} available. Renovate or Upgrade {(stats.lcType == LaunchComplexType.Pad ? "LC" : "the Hangar")}.");
            }

            return pass;
        }

        public bool MeetsFacilityRequirements(List<string> failedReasons)
        {
            // Use blv's existing LC if available, else use active complex
            LCItem selectedLC;
            if (LC == null)
            {
                selectedLC = Type == ListType.VAB ? KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance : KCTGameStates.ActiveKSC.Hangar;
            }
            else
            {
                selectedLC = LC;
            }

            return MeetsFacilityRequirements(selectedLC.Stats, failedReasons);
        }

        public bool MeetsFacilityRequirements(LCData stats, List<string> failedReasons)
        {
            if (!Utilities.CurrentGameIsCareer())
                return true;

            double totalMass = GetTotalMass();
            if (totalMass > stats.massMax)
            {
                if (failedReasons == null)
                    return false;

                failedReasons.Add($"Mass limit exceeded, currently at {totalMass:N} tons, max {stats.massMax:N}");
            }
            if (totalMass < stats.MassMin)
            {
                if (failedReasons == null)
                    return false;

                failedReasons.Add($"Mass minimum exceeded, currently at {totalMass:N} tons, min {stats.MassMin:N}");
            }
            if (!ResourcesOK(stats, failedReasons) && failedReasons == null)
                return false;

            // Facility doesn't matter here.
            Vector3 size = GetShipSize();
            if (size.x > stats.sizeMax.x || size.y > stats.sizeMax.y || size.z > stats.sizeMax.z)
            {
                if (failedReasons == null)
                    return false;

                failedReasons.Add("Size limits exceeded");
            }

            if (humanRated && !stats.isHumanRated)
            {
                if (failedReasons == null)
                    return false;

                failedReasons.Add("Vessel is human-rated but launch complex is not");
            }

            if (HasClamps() && stats.lcType == LaunchComplexType.Hangar)
            {
                if (failedReasons == null)
                    return false;

                failedReasons.Add("Has launch clamps/GSE but is launching from runway");
            }

            return failedReasons == null || failedReasons.Count == 0;
        }

        private bool HasClamps()
        {
            return clampState == ClampsState.HasClamps;
        }

        private void CacheClamps(List<Part> parts)
        {
            Profiler.BeginSample("RP0CacheClamps");
            clampState = ClampsState.NoClamps;

            foreach (var p in parts)
            {
                if (Utilities.IsClamp(p))
                {
                    clampState = ClampsState.HasClamps;
                    break;
                }
            }
            Profiler.EndSample();
        }

        private void UpdateRFTanks()
        {
            foreach (var cn in ShipNodeCompressed.Node.GetNodes("PART"))
            {
                foreach (var module in cn.GetNodes("MODULE"))
                {
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

        private bool AreTanksFull(List<Part> parts)
        {
            Profiler.BeginSample("RP0AreTanksFull");
            foreach (var p in parts)
            {
                foreach (var r in p.Resources)
                {
                    if (r.flowState && GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(r.resourceName) &&
                        Math.Abs(r.amount - r.maxAmount) >= 1)
                    {
                        Profiler.EndSample();
                        return false;
                    }
                }
            }

            Profiler.EndSample();
            return true;
        }

        public bool AreTanksFull() => tanksFull;

        private void FillUnlockedFuelTanks()
        {
            foreach (ConfigNode p in ShipNodeCompressed.Node.GetNodes("PART"))
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

        private void StorePartNames(List<Part> parts)
        {
            Profiler.BeginSample("RP0StorePartNames");
            partNames.Clear();
            foreach (var p in parts)
                partNames.Add(p.partInfo.name);
            Profiler.EndSample();
        }

        public bool IsCrewable() => isCrewable;

        private bool IsCrewable(List<Part> parts)
        {
            foreach (var p in parts)
                if (p.CrewCapacity > 0)
                    return true;

            return false;
        }

        public double GetTotalMass()
        {
            return mass;
        }

        public Vector3 GetShipSize()
        {
            if (ShipSize.sqrMagnitude > 0)
                return ShipSize;

            ShipTemplate template = new ShipTemplate();
            template.LoadShip(ShipNodeCompressed.Node);
            ShipSize = template.GetShipSize();
            ShipNodeCompressed.CompressAndRelease();
            
            return ShipSize;
        }

        public double GetTotalCost()
        {
            if (cost == 0 || emptyCost == 0)
            {
                cost = Utilities.GetTotalVesselCost(ShipNodeCompressed.Node);
                emptyCost = Utilities.GetTotalVesselCost(ShipNodeCompressed.Node, false);
                integrationCost = (float)Formula.GetIntegrationCost(this);
                ShipNodeCompressed.CompressAndRelease();
            }

            return cost + integrationCost;
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
            if (LC == null)
            {
                KCTDebug.Log("Could not find the LC to remove vessel!");
                return false;
            }
            else
            {
                oldIndex = _lc.Warehouse.IndexOf(this);
                if (oldIndex >= 0)
                {
                    removed = true;
                    _lc.Warehouse.RemoveAt(oldIndex);
                    oldIndex = -1; // report notfound for removed from warehouse
                }
                if (!removed)
                {
                    oldIndex = _lc.BuildList.IndexOf(this);
                    if (oldIndex >= 0)
                    {
                        removed = true;
                        _lc.BuildList.RemoveAt(oldIndex);
                        _lc.RecalculateBuildRates();
                    }
                }
            }
            KCTDebug.Log($"Removing {shipName} from {_lc.Name} storage/list.");
            if (!removed)
            {
                // This will happen when we launch the vessel (or we remove an edited vessel)
                // because the BLV instance is not the same instance as what's stored in the LC, just a copy.
                // We could try to be smarter, but eh. At least we're going to check warehouse first
                // since launching is the usual case.
                KCTDebug.Log("Failed to remove ship from list! Performing direct comparison of ids...");

                for (int i = _lc.Warehouse.Count; i-- > 0;)
                {
                    BuildListVessel blv = _lc.Warehouse[i];
                    if (blv.shipID == shipID)
                    {
                        KCTDebug.Log("Ship found in Warehouse list. Removing...");
                        oldIndex = -1; // report notfound for removed from warehouse
                        removed = true;
                        _lc.Warehouse.RemoveAt(i);
                        break;
                    }
                }
                if (!removed)
                {
                    for (int i = _lc.BuildList.Count; i-- > 0;)
                    {
                        BuildListVessel blv = _lc.BuildList[i];
                        if (blv.shipID == shipID)
                        {
                            KCTDebug.Log("Ship found in BuildList. Removing...");
                            removed = true;
                            _lc.BuildList.RemoveAt(i);
                            oldIndex = i;
                            _lc.RecalculateBuildRates();
                            break;
                        }
                    }
                }
            }

            if (removed)
                KCTDebug.Log("Sucessfully removed vessel from LC.");
            else 
                KCTDebug.Log("Still couldn't remove ship!");

            return removed;
        }

        public List<PseudoPart> GetPseudoParts()
        {
            List<PseudoPart> retList = new List<PseudoPart>();

            foreach (ConfigNode cn in ShipNodeCompressed.Node.GetNodes("PART"))
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
            if (partNames.Count > 0)
            {
                foreach (var s in partNames)
                    if (Utilities.GetAvailablePartByName(s) == null)
                        return false;
            }

            return true;
        }

        public List<string> GetMissingParts()
        {
            List<string> missing = new List<string>();
            foreach(var name in partNames)
            {
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
            foreach (string partName in partNames)
            {
                AvailablePart part = PartLoader.getPartInfoByName(partName);
                apList.Add(part);
            }

            res = Utilities.GetPartsWithPurchasability(apList);
            return res;
        }

        private double GetResourceEffectiveCost()
        {
            double total = 0d;
            foreach (var kvp in resourceAmounts)
            {
                double mult = PresetManager.Instance.ActivePreset.PartVariables.GetResourceVariableMult(kvp.Key) - 1d;
                if (mult == 0d)
                    continue;

                total += PresetManager.Instance.ActivePreset.GeneralSettings.EffectiveCostPerLiterPerResourceMult * mult * kvp.Value;
            }
            return total;
        }

        public double GetEffectiveCost(List<Part> parts)
        {
            Profiler.BeginSample("RP0GetEffectiveCost");
            resourceAmounts.Clear();
            globalTags.Clear();
            tagEffectiveCosts.Clear();
            double totalEffectiveCost = 0;
            foreach (Part p in parts)
            {
                totalEffectiveCost += GetEffectiveCostInternal(p);
            }
            totalEffectiveCost += GetResourceEffectiveCost();

            double globalMultiplier = ApplyGlobalCostModifiers();
            double multipliedCost = totalEffectiveCost * globalMultiplier;
            KCTDebug.Log($"Total eff cost: {totalEffectiveCost}; global mult: {globalMultiplier}; multiplied cost: {multipliedCost}");

            Profiler.EndSample();
            return multipliedCost;
        }

        public double GetEffectiveCost(List<ConfigNode> parts)
        {
            resourceAmounts.Clear();
            globalTags.Clear();
            tagEffectiveCosts.Clear();
            double totalEffectiveCost = 0;
            foreach (ConfigNode p in parts)
            {
                totalEffectiveCost += GetEffectiveCostInternal(p);
            }
            totalEffectiveCost += GetResourceEffectiveCost();

            double globalMultiplier = ApplyGlobalCostModifiers();
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

        private static Dictionary<string, double> _tempResourceAmounts = new Dictionary<string, double>();
        private static List<string> _tempTags = null;

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
                GetPartCostsAndMass(partRef, out dryCost, out fuelCost, out dryMass, out fuelMass, _tempResourceAmounts);
            }

            float wetMass = dryMass + fuelMass;
            float cost = dryCost + fuelCost;

            double partMultiplier = PresetManager.Instance.ActivePreset.PartVariables.GetPartVariable(name);

            // Resource contents may not match the prefab (ie, ModularFuelTanks implementation)
            double resourceMultiplier = 1d;

            if (o is ConfigNode)
            {
                foreach (ConfigNode rNode in (o as ConfigNode).GetNodes("RESOURCE"))
                {
                    string rName = rNode.GetValue("name");
                    _tempResourceAmounts[rName] = double.Parse(rNode.GetValue("maxAmount"));
                }
            }

            // TODO: Add support for upgraded tags here
            double moduleMultiplier = FindApplyTags(partRef);

            foreach (var kvp in _tempResourceAmounts)
            {
                resourceAmounts.TryGetValue(kvp.Key, out double amt);
                amt += kvp.Value;
                resourceAmounts[kvp.Key] = amt;
            }
            
            double effectiveCost = partMultiplier * resourceMultiplier * moduleMultiplier * cost;

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

            UpdateTagECs(effectiveCost);

            KCTDebug.Log($"Eff cost for {name}: {effectiveCost} (cost: {cost}; dryCost: {dryCost}; wetMass: {wetMass}; dryMass: {dryMass}; partMultiplier: {partMultiplier}; resourceMultiplier: {resourceMultiplier}; moduleMultiplier: {moduleMultiplier})");

            _tempTags = null;
            _tempResourceAmounts.Clear();

            return effectiveCost;
        }

        private void UpdateTagECs(double ec)
        {
            if (_tempTags == null)
                return;

            foreach (var t in tagEffectiveCosts)
            {
                if (!t.SameTags(_tempTags))
                    continue;

                t.ec += ec;
                return;
            }

            var holder = new TagsToEC();
            holder.tags.AddRange(_tempTags);
            holder.ec = ec;
            tagEffectiveCosts.Add(holder);
        }

        public double ApplyGlobalCostModifiers()
        {
            humanRated = false;
            double costMod = 1d;
            foreach (var x in globalTags)
            {
                if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod))
                {
                    costMod *= mod.globalMult;
                    humanRated |= mod.isHumanRating;
                }
            }
            return costMod;
        }

        private double FindApplyTags(Part p)
        {
            double mult = 1;
            if (p.Modules.GetModule<ModuleTagList>() is ModuleTagList pm)
            {
                _tempTags = pm.tags;
                foreach (var x in pm.tags)
                {
                    if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod))
                    {
                        mult *= mod.partMult;
                        if (mod.globalMult != 1d)
                            globalTags.Add(mod.name);
                    }
                }
            }
            return mult;
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

        public string GetItemName() => shipName;

        public double GetBuildRate() => BuildRate;

        public double UpdateLeaderEffect()
        {
            double modifiedEC = effectiveCost;
            double globalMult = ApplyGlobalCostModifiers();
            foreach (var t in tagEffectiveCosts)
            {
                double ec = t.ec * RP0.Leaders.LeaderUtils.GetPartEffectiveCostEffect(t.tags);
                modifiedEC += (ec - t.ec) * globalMult;
            }
            modifiedEC *= RP0.Leaders.LeaderUtils.GetGlobalEffectiveCostEffect(globalTags, resourceAmounts);
            double modifiedBP = Formula.GetVesselBuildPoints(modifiedEC);
            if (modifiedBP < 1d)
                modifiedBP = 1d;

            _leaderEffect = buildPoints / modifiedBP;
            return _leaderEffect;
        }

        public double UpdateBuildRate()
        {
            if (LC == null)
                return 0d;

            _buildRate = Utilities.GetBuildRate(this) * LC.StrategyRateMultiplier * UpdateLeaderEffect();
            if (_buildRate < 0d)
                _buildRate = 0d;

            return _buildRate;
        }

        public double GetFractionComplete() => progress / (buildPoints + integrationPoints);

        public double GetTimeLeft()
        {
            return GetTimeLeft(out _);
        }

        public double GetTimeLeft(out double newEff)
        {
            newEff = LC.Efficiency;
            if (BuildRate > 0)
            {
                double bpLeft = integrationPoints + buildPoints - progress;
                if (LC.Efficiency == LCEfficiency.MaxEfficiency)
                    return bpLeft / BuildRate;

                return CalculateTimeLeftForBuildRate(bpLeft, BuildRate / LC.Efficiency, newEff, out newEff);
            }
            else
                return double.PositiveInfinity;
        }

        public double GetTimeLeftEst(double offset)
        {
            return GetTimeLeftEst(offset, LC.Efficiency, out _);
        }

        public double GetTimeLeftEst(double offset, double startingEff, out double newEff)
        {
            if (BuildRate > 0)
            {
                return GetTimeLeft(out newEff);
            }
            newEff = LC.Efficiency;
            double bp = buildPoints + integrationPoints;
            double rate = Utilities.GetBuildRate(LC, GetTotalMass(), bp, humanRated) * LC.StrategyRateMultiplier * LeaderEffect;
            double bpLeft = bp - progress;
            if (newEff == LCEfficiency.MaxEfficiency)
                return (bpLeft - progress) / (rate * newEff);

            return CalculateTimeLeftForBuildRate(bpLeft, rate, startingEff, out newEff);
        }

        /// <summary>
        /// Dumb version of this: we'll just see-saw for four iterations
        /// And hope we get reasonably close. If we're less than a day
        /// away, just report the time for the current efficiency with no increase
        /// </summary>
        /// <param name="bp"></param>
        /// <param name="rate"></param>
        /// <returns></returns>
        public double CalculateTimeLeftForBuildRate(double bp, double rate, double startingEff, out double newEff)
        {
            newEff = startingEff;
            double timeLeft = bp / (rate * LC.Efficiency);
            if (timeLeft < 86400d)
                return timeLeft;

            for (int i = 0; i < 4; ++i)
            {
                timeLeft = bp / (rate * LC.EfficiencySource.PredictWeightedEfficiency(timeLeft, LC.Engineers / LC.MaxEngineers, out newEff, startingEff));
            }
            return timeLeft;
        }

        public ListType GetListType() => Type;

        public bool IsComplete() => progress >= buildPoints + integrationPoints;

        public double IncrementProgress(double UTDiff)
        {
            double bR = BuildRate;
            if (bR == 0)
                return 0d;

            double toGo = buildPoints + integrationPoints - progress;
            double amt = bR * UTDiff;
            progress += bR * UTDiff;
            if (IsComplete())
            {
                Utilities.MoveVesselToWarehouse(this);
                if (amt > toGo)
                    return (1d - toGo / amt) * UTDiff;
            }

            return 0d;
        }

        public void ReleaseShipNode()
        {
            ShipNodeCompressed.CompressAndRelease();
        }

        public void RemoveMissingSymmetry()
        {
            //loop through, find all sym = lines and find the part they reference
            int referencesRemoved = 0;
            foreach (ConfigNode partNode in ShipNodeCompressed.Node.GetNodes("PART"))
            {
                List<string> toRemove = new List<string>();
                foreach (string symPart in partNode.GetValues("sym"))
                {
                    //find the part in the ship
                    if (ShipNodeCompressed.Node.GetNodes("PART").FirstOrDefault(cn => cn.GetValue("part") == symPart) == null)
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

        public ShipConstruct CreateShipConstructAndRelease()
        {
            var sc = new ShipConstruct();
            if (!sc.LoadShip(ShipNodeCompressed.Node))
                sc = null;

            ReleaseShipNode();

            return sc;
        }

        public void LinkToLC(LCItem lc)
        {
            LC = lc;
            if (lc == null)
            {
                _buildRate = 0d;
                return;
            }
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
