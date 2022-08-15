using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public enum LaunchComplexType
    {
        Hangar,
        Pad,
    }

    public class LCItem
    {
        public class LCData
        {
            [Persistent] public string Name;
            [Persistent] public float massMax;
            [Persistent] public float massOrig;
            [Persistent] public Vector3 sizeMax;
            [Persistent] public LaunchComplexType lcType = LaunchComplexType.Pad;
            [Persistent] public bool isHumanRated;
            [Persistent] public PersistentDictionaryValueTypes<string, double> resourcesHandled = new PersistentDictionaryValueTypes<string, double>();

            public float MaxPossibleMass => Mathf.Floor(massOrig * 2f);
            public float MinPossibleMass => Mathf.Ceil(massOrig * 0.5f);
            public static float CalcMassMin(float massMax) => massMax == float.MaxValue ? 0f : Mathf.Floor(massMax * 0.75f);
            public float MassMin => CalcMassMin(massMax);
            public static float CalcMassMaxFromMin(float massMin) => Mathf.Ceil(massMin / 0.75f);

            public LCData() { }

            public LCData(string Name, float massMax, float massOrig, Vector3 sizeMax, LaunchComplexType lcType, bool isHumanRated, PersistentDictionaryValueTypes<string, double> resourcesHandled)
            {
                this.Name = Name;
                this.massMax = massMax;
                this.massOrig = massOrig;
                this.sizeMax = sizeMax;
                this.lcType = lcType;
                this.isHumanRated = isHumanRated;
                foreach (var kvp in resourcesHandled)
                    this.resourcesHandled[kvp.Key] = kvp.Value;
                //TODO: If setting starting hangar, apply default resources, which are?
            }

            public LCData(LCData old)
            {
                SetFrom(old);
            }

            public LCData(LCItem lc)
            {
                SetFrom(lc);
            }

            public void SetFrom(LCData old)
            {
                Name = old.Name;
                massMax = old.massMax;
                massOrig = old.massOrig;
                sizeMax = old.sizeMax;
                lcType = old.lcType;
                isHumanRated = old.isHumanRated;

                resourcesHandled.Clear();
                foreach (var kvp in old.resourcesHandled)
                    resourcesHandled[kvp.Key] = kvp.Value;
            }

            public void SetFrom(LCItem lc)
            {
                SetFrom(lc._lcData);
            }

            // NOTE: Not comparing name, which I think is correct here.
            public bool Compare(LCItem lc) => massMax == lc.MassMax && sizeMax == lc.SizeMax && lcType == lc.LCType && isHumanRated == lc.IsHumanRated && PersistentDictionaryValueTypes<string, double>.AreEqual(resourcesHandled, lc.ResourcesHandled);
            public bool Compare(LCData data) => massMax == data.massMax && sizeMax == data.sizeMax && lcType == data.lcType && isHumanRated == data.isHumanRated && PersistentDictionaryValueTypes<string, double>.AreEqual(resourcesHandled, data.resourcesHandled);

            public float GetPadFracLevel()
            {
                float fractionalPadLvl = 0f;

                if (Utilities.PadTons != null)
                {
                    float unlimitedTonnageThreshold = 2500f;

                    if (massMax >= unlimitedTonnageThreshold)
                    {
                        int padLvl = Utilities.PadTons.Length - 1;
                        fractionalPadLvl = padLvl;
                    }
                    else
                    {
                        for (int i = 1; i < Utilities.PadTons.Length; i++)
                        {
                            if (massMax < Utilities.PadTons[i])
                            {
                                float lowerBound = Utilities.PadTons[i - 1];
                                float upperBound = Math.Min(Utilities.PadTons[i], unlimitedTonnageThreshold);
                                float fractionOverFullLvl = (massMax - lowerBound) / (upperBound - lowerBound);
                                fractionalPadLvl = (i - 1) + fractionOverFullLvl;

                                break;
                            }
                        }
                    }
                }

                return fractionalPadLvl;
            }

            public double GetCostStats(out double costPad, out double costVAB, out double costResources)
            {
                Vector3 padSize = sizeMax; // we tweak it later.
                
                HashSet<string> ignoredRes;
                if (lcType == LaunchComplexType.Pad)
                {
                    ignoredRes = GuiDataAndWhitelistItemsDatabase.PadIgnoreRes;

                    double mass = massMax;
                    costPad = Math.Max(0d, Math.Pow(mass, 0.65d) * 2000d + Math.Pow(Math.Max(mass - 350, 0), 1.5d) * 2d - 2000d) + 1000d;
                }
                else
                {
                    ignoredRes = GuiDataAndWhitelistItemsDatabase.HangarIgnoreRes;

                    costPad = 0f;
                    padSize.y *= 5f;
                }
                costVAB = padSize.sqrMagnitude * 25d + 100d;
                if (isHumanRated)
                {
                    costPad *= 1.5d;
                    costVAB *= 2d;
                }

                costPad *= 0.75d;
                costVAB *= 0.75d;

                costResources = 0d;
                foreach (var kvp in resourcesHandled)
                {
                    if (ignoredRes.Contains(kvp.Key))
                        continue;

                    costResources += Utilities.ResourceTankCost(kvp.Key, kvp.Value, lcType);
                }

                return costVAB + costPad + costResources;
            }

            private static HashSet<string> _resourceNames = new HashSet<string>();

            public double ResModifyCost(LCData old)
            {
                double totalCost = 0d;

                foreach (var res in old.resourcesHandled.Keys)
                    _resourceNames.Add(res);
                foreach (var res in resourcesHandled.Keys)
                    _resourceNames.Add(res);

                const double _DownsizeMult = -0.1d;
                foreach (var res in _resourceNames)
                {
                    old.resourcesHandled.TryGetValue(res, out double oldAmount);
                    resourcesHandled.TryGetValue(res, out double newAmount);

                    double delta = newAmount - oldAmount;
                    if (delta < 0d)
                        delta *= _DownsizeMult;

                    totalCost += Utilities.ResourceTankCost(res, delta, lcType);
                }

                return totalCost;
            }
        }
        public static readonly LCData StartingHangar = new LCData("Hangar", float.MaxValue, float.MaxValue, new Vector3(40f, 10f, 40f), LaunchComplexType.Hangar, true, new PersistentDictionaryValueTypes<string, double>());
        public static readonly LCData StartingLC = new LCData("Launch Complex 1", 1f, 1.5f, new Vector3(2f, 10f, 2f), LaunchComplexType.Pad, false, new PersistentDictionaryValueTypes<string, double>());

        public string Name;
        protected Guid _id;
        public Guid ID => _id;
        protected Guid _modID;
        public Guid ModID => _modID;
        public KCTObservableList<BuildListVessel> BuildList = new KCTObservableList<BuildListVessel>();
        public KCTObservableList<BuildListVessel> Warehouse = new KCTObservableList<BuildListVessel>();
        public SortedList<string, BuildListVessel> Plans = new SortedList<string, BuildListVessel>();
        public KCTObservableList<PadConstruction> PadConstructions = new KCTObservableList<PadConstruction>();
        public List<ReconRollout> Recon_Rollout = new List<ReconRollout>();
        public List<AirlaunchPrep> AirlaunchPrep = new List<AirlaunchPrep>();

        private LCData _lcData = new LCData();
        public LCData Stats => _lcData;

        private double _rate;
        private double _rateHRCapped;
        public double Rate => _rate;
        public double RateHRCapped => _rateHRCapped;

        public const int MinEngineersConst = 1;
        public const int EngineersPerPacket = 10;
        
        public int Engineers = 0;
        private static double RawMaxEngineers(float massMax, Vector3 sizeMax) =>
            massMax != float.MaxValue ? Math.Pow(massMax, 0.75d) : sizeMax.sqrMagnitude * 0.01d;
        public static int MaxEngineersCalc(float massMax, Vector3 sizeMax, bool isHuman) => 
            Math.Max(MinEngineersConst, (int)Math.Ceiling(RawMaxEngineers(massMax, sizeMax) * (isHuman ? 1.5d : 1d) * EngineersPerPacket));

        private double _RawMaxEngineers => RawMaxEngineers(MassMax, SizeMax);
        public int MaxEngineers => MaxEngineersCalc(MassMax, SizeMax, IsHumanRated);
        public int MaxEngineersNonHR => Math.Max(MinEngineersConst, (int)Math.Ceiling(_RawMaxEngineers * EngineersPerPacket));
        public int MaxEngineersFor(double mass, double bp, bool humanRated)
        {
            if (LCType == LaunchComplexType.Pad)
                return IsHumanRated && !humanRated ? MaxEngineersNonHR : MaxEngineers;

            double tngMax = RawMaxEngineers((float)mass, Vector3.zero);
            if (IsHumanRated && humanRated)
                tngMax *= 1.5d;
            double bpMax = Math.Pow(bp * 0.000015d, 0.75d);
            return Math.Max(MinEngineersConst, (int)Math.Ceiling((tngMax * 0.25d + bpMax * 0.75d) * EngineersPerPacket));
        }
        public int MaxEngineersFor(BuildListVessel blv) => blv == null ? MaxEngineers : MaxEngineersFor(blv.GetTotalMass(), blv.BuildPoints + blv.IntegrationPoints, blv.IsHumanRated);

        protected double _strategyRateMultiplier = 1d;
        public double StrategyRateMultiplier => _strategyRateMultiplier;

        private LCEfficiency _efficiencySource = null;
        public LCEfficiency EfficiencySource
        {
            get
            {
                if (LCType == LaunchComplexType.Hangar)
                    return null;

                if (_efficiencySource == null)
                    _efficiencySource = LCEfficiency.GetOrCreateEfficiencyForLC(this, true);

                return _efficiencySource;
            }
        }
        public double Efficiency
        {
            get
            {
                if (LCType == LaunchComplexType.Hangar)
                    return LCEfficiency.MaxEfficiency;

                return EfficiencySource.Efficiency;
            }
        }

        public bool IsRushing;
        public double RushRate => IsRushing ? PresetManager.Instance.ActivePreset.GeneralSettings.RushRateMult : 1d;
        public double RushSalary => IsRushing ? PresetManager.Instance.ActivePreset.GeneralSettings.RushSalaryMult : 1d;

        public bool IsOperational = false;

        public LaunchComplexType LCType => _lcData.lcType;
        public bool IsHumanRated => _lcData.isHumanRated;
        public float MassMax => _lcData.massMax;
        public float MassOrig => _lcData.massOrig;
        public float MassMin => _lcData.MassMin;
        public Vector3 SizeMax => _lcData.sizeMax;
        public PersistentDictionaryValueTypes<string, double> ResourcesHandled => _lcData.resourcesHandled;

        public List<KCT_LaunchPad> LaunchPads = new List<KCT_LaunchPad>();
        public int ActiveLaunchPadIndex = 0;

        public static string SupportedMassAsPrettyTextCalc(float mass) => mass == float.MaxValue ? "unlimited" : $"{LCData.CalcMassMin(mass):N0}-{mass:N0}t";
        public string SupportedMassAsPrettyText => SupportedMassAsPrettyTextCalc(MassMax);

        public static string SupportedSizeAsPrettyTextCalc(Vector3 size) => size.y == float.MaxValue ? "unlimited" : $"{size.z:N0}x{size.x:N0}x{size.y:N0}m";
        public string SupportedSizeAsPrettyText => SupportedSizeAsPrettyTextCalc(SizeMax);

        private KSCItem _ksc = null;

        public KSCItem KSC => _ksc;

        #region Observable funcs
        void added(int idx, ConstructionBuildItem pc) { _ksc.Constructions.Add(pc); }
        void removed(int idx, ConstructionBuildItem pc) { _ksc.Constructions.Remove(pc); }
        void updated() { KCTEvents.OnRP0MaintenanceChanged.Fire(); }
        #endregion

        public LCItem(KSCItem ksc)
        {
            _ksc = ksc;
        }

        public LCItem(LCData lcData, KSCItem ksc)
        {
            _ksc = ksc;
            _id = Guid.NewGuid();
            _modID = _id;
            _lcData.SetFrom(lcData);
            Name = _lcData.Name;

            if (_lcData.lcType == LaunchComplexType.Pad)
            {
                float fracLevel = _lcData.GetPadFracLevel();
                var pad = new KCT_LaunchPad(Guid.NewGuid(), Name, fracLevel);
                pad.isOperational = true;
                LaunchPads.Add(pad);
            }

            PadConstructions.Added += added;
            PadConstructions.Removed += removed;
            PadConstructions.Updated += updated;

            BuildList.Updated += updated;
            Warehouse.Updated += updated;

            _efficiencySource = LCEfficiency.GetOrCreateEfficiencyForLC(this, false);
        }

        public void Modify(LCData data, Guid modId)
        {
            _modID = modId;
            _lcData.SetFrom(data);

            if (_lcData.lcType == LaunchComplexType.Pad)
            {
                float fracLevel = _lcData.GetPadFracLevel();

                foreach (var pad in LaunchPads)
                {
                    pad.fractionalLevel = fracLevel;
                    pad.level = (int)fracLevel;
                }
            }

            // will create a new one if needed (it probably will be needed)
            // If it does, it will remove us from the old one, and then clear it if it's empty.
            _efficiencySource = LCEfficiency.GetOrCreateEfficiencyForLC(this, false);
        }

        public KCT_LaunchPad ActiveLPInstance => LaunchPads.Count > ActiveLaunchPadIndex && ActiveLaunchPadIndex >= 0 ? LaunchPads[ActiveLaunchPadIndex] : null;

        public int LaunchPadCount
        {
            get
            {
                int count = 0;
                foreach (KCT_LaunchPad lp in LaunchPads)
                    if (lp.isOperational) count++;
                return count;
            }
        }

        public bool IsEmpty => StartingHangar.Compare(this) && BuildList.Count == 0 && Warehouse.Count == 0 && Recon_Rollout.Count == 0 && AirlaunchPrep.Count == 0 &&
                    PadConstructions.Count == 0 && !KerbalConstructionTimeData.Instance.LCToEfficiency.ContainsKey(this);

        public bool IsActive => BuildList.Count > 0 || Recon_Rollout.Count > 0 || AirlaunchPrep.Count > 0;
        public bool CanModify => !BuildList.Any() && !Recon_Rollout.Any() && !AirlaunchPrep.Any() && !PadConstructions.Any();
        public bool IsIdle => !BuildList.Any() && !Recon_Rollout.Any() && !AirlaunchPrep.Any();

        public ReconRollout GetReconditioning(string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.LaunchPadID == launchSite && ((IKCTBuildItem)r).GetItemName() == "LaunchPad Reconditioning");

        public ReconRollout GetReconRollout(ReconRollout.RolloutReconType type, string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => (type == ReconRollout.RolloutReconType.None ||  r.RRType == type) && r.LaunchPadID == launchSite);

        public void RecalculateBuildRates()
        {
            _strategyRateMultiplier = RP0.CurrencyUtils.Rate(LCType == LaunchComplexType.Pad ? RP0.TransactionReasonsRP0.RateIntegrationVAB : RP0.TransactionReasonsRP0.RateIntegrationSPH);
            _rate = Utilities.GetBuildRate(0, this, IsHumanRated, true);
            _rateHRCapped = Utilities.GetBuildRate(0, this, false, true);
            foreach (var blv in BuildList)
                blv.UpdateBuildRate();

            foreach (var rr in Recon_Rollout)
                rr.UpdateBuildRate();

            foreach (var al in AirlaunchPrep)
                al.UpdateBuildRate();

            KCTDebug.Log($"Build rate for {Name} = {_rate:N3}, capped {_rateHRCapped:N3}");
        }

        public void SwitchToPrevLaunchPad() => SwitchLaunchPad(false);
        public void SwitchToNextLaunchPad() => SwitchLaunchPad(true);

        public void SwitchLaunchPad(bool forwardDirection)
        {
            if (LaunchPadCount < 2)
            {
                ActiveLaunchPadIndex = 0;
                return;
            }

            int idx = ActiveLaunchPadIndex;
            KCT_LaunchPad pad;
            int count = LaunchPads.Count;
            do
            {
                if (forwardDirection)
                {
                    ++idx;
                    if (idx == count)
                        idx = 0;
                }
                else
                {
                    if (idx == 0)
                        idx = count;
                    --idx;
                }
                pad = LaunchPads[idx];
            } while (!pad.isOperational);

            SwitchLaunchPad(idx);
        }

        public void Rename(string newName)
        {
            Name = newName;
            _lcData.Name = newName;
        }

        public void SwitchLaunchPad(int LP_ID = -1, bool updateDestrNode = true)
        {
            if (LP_ID >= 0)
            {
                if (ActiveLaunchPadIndex == LP_ID && ActiveLPInstance != null && ActiveLPInstance.isOperational)
                    return;

                ActiveLaunchPadIndex = LP_ID;
            }

            if (ActiveLPInstance == null)
            {
                for (ActiveLaunchPadIndex = 0; ActiveLaunchPadIndex < LaunchPads.Count; ++ActiveLaunchPadIndex)
                {
                    if (LaunchPads[ActiveLaunchPadIndex].isOperational)
                    {
                        break;
                    }
                }
                // failed to find
                if (ActiveLaunchPadIndex == LaunchPads.Count)
                {
                    ActiveLaunchPadIndex = 0;
                    return;
                }
            }

            //set the active LP's new state
            //activate new pad

            if (updateDestrNode)
                ActiveLPInstance?.RefreshDestructionNode();

            ActiveLPInstance?.SetActive();
        }

        public LaunchPadState GetBestLaunchPadState()
        {
            LaunchPadState state = LaunchPadState.None;
            foreach (KCT_LaunchPad lp in LaunchPads)
            {
                var padState = lp.State;
                if (padState > state)
                    state = padState;
            }

            return state;
        }

        public KCT_LaunchPad FindFreeLaunchPad()
        {
            foreach (KCT_LaunchPad lp in LaunchPads)
            {
                if (lp.State == LaunchPadState.Free)
                    return lp;
            }

            return null;
        }

        public void OnRemove()
        {
            if (_efficiencySource == null)
                KerbalConstructionTimeData.Instance.LCToEfficiency.TryGetValue(this, out _efficiencySource);
            if (_efficiencySource != null)
                _efficiencySource.RemoveLC(this);
            else
                LCEfficiency.ClearEmpty();
        }

        private void BuildVesselAndShipNodeConfigs(BuildListVessel blv, ref ConfigNode node)
        {
            var storageItem = new BuildListStorageItem();
            storageItem.FromBuildListVessel(blv);
            var cnTemp = new ConfigNode("KCTVessel");
            cnTemp = ConfigNode.CreateConfigFromObject(storageItem, cnTemp);
            var shipNode = new ConfigNode("ShipNode");
            blv.ShipNode.CopyTo(shipNode);
            cnTemp.AddNode(shipNode);
            node.AddNode(cnTemp);
        }

        private BuildListVessel CreateBLVFromNode(in ConfigNode cn)
        {
            var listItem = new BuildListStorageItem();
            ConfigNode.LoadObjectFromConfig(listItem, cn);
            BuildListVessel blv = listItem.ToBuildListVessel();
            blv.ShipNode = cn.GetNode("ShipNode");
            blv.LC = this;
            if (KCTGameStates.LoadedSaveVersion < KCTGameStates.VERSION)
            {
                if (KCTGameStates.LoadedSaveVersion < 10)
                {
                    blv.RecalculateFromNode(false);
                }

                if (KCTGameStates.LoadedSaveVersion < 11)
                {
                    HashSet<string> ignoredRes = _lcData.lcType == LaunchComplexType.Hangar ? GuiDataAndWhitelistItemsDatabase.HangarIgnoreRes : GuiDataAndWhitelistItemsDatabase.PadIgnoreRes;

                    foreach (var kvp in blv.resourceAmounts)
                    {
                        if (ignoredRes.Contains(kvp.Key)
                            || !GuiDataAndWhitelistItemsDatabase.ValidFuelRes.Contains(kvp.Key))
                            continue;

                        double mass = PartResourceLibrary.Instance.GetDefinition(kvp.Key).density * kvp.Value;
                        if (mass <= Formula.VesselMassMinForResourceValidation * blv.GetTotalMass())
                            continue;

                        _lcData.resourcesHandled[kvp.Key] = kvp.Value * 1.1d;
                    }
                }
            }
            blv.UpdateBuildRate();
            return blv;
        }

        public ConfigNode AsConfigNode()
        {
            KCTDebug.Log("Saving LC " + Name);
            var node = new ConfigNode("LaunchComplex");
            node.AddValue("LCName", Name);
            node.AddValue("ActiveLPID", ActiveLaunchPadIndex);
            node.AddValue("operational", IsOperational);
            node.AddValue("id", _id);
            node.AddValue("modID", _modID);
            node.AddValue("Engineers", Engineers);
            node.AddValue("IsRushing", IsRushing);

            var statsNode = new ConfigNode("Stats");
            ConfigNode.CreateConfigFromObject(_lcData, statsNode);
            node.AddNode(statsNode);

            var cnBuildl = new ConfigNode("BuildList");
            foreach (BuildListVessel blv in BuildList)
            {
                BuildVesselAndShipNodeConfigs(blv, ref cnBuildl);
            }
            node.AddNode(cnBuildl);

            var cnWh = new ConfigNode("Warehouse");
            foreach (BuildListVessel blv in Warehouse)
            {
                BuildVesselAndShipNodeConfigs(blv, ref cnWh);
            }
            node.AddNode(cnWh);

            var cnPadConstructions = new ConfigNode("PadConstructions");
            foreach (PadConstruction pc in PadConstructions)
            {
                pc.BuildListIndex = _ksc.Constructions.IndexOf(pc);
                var storageItem = new PadConstructionStorageItem();
                storageItem.FromPadConstruction(pc);
                var cn = new ConfigNode("PadConstruction");
                storageItem.Save(cn);
                cnPadConstructions.AddNode(cn);
            }
            node.AddNode(cnPadConstructions);

            var cnPlans = new ConfigNode("Plans");
            foreach (BuildListVessel blv in Plans.Values)
            {
                BuildVesselAndShipNodeConfigs(blv, ref cnPlans);
            }
            node.AddNode(cnPlans);

            var cnRR = new ConfigNode("Recon_Rollout");
            foreach (ReconRollout rr in Recon_Rollout)
            {
                var storageItem = new ReconRolloutStorageItem();
                storageItem.FromReconRollout(rr);
                var rrCN = new ConfigNode("Recon_Rollout_Item");
                storageItem.Save(rrCN);
                cnRR.AddNode(rrCN);
            }
            node.AddNode(cnRR);

            var cnAP = new ConfigNode("Airlaunch_Prep");
            foreach (AirlaunchPrep ap in AirlaunchPrep)
            {
                var storageItem = new AirlaunchPrepStorageItem();
                storageItem.FromAirlaunchPrep(ap);
                var cn = new ConfigNode("Airlaunch_Prep_Item");
                storageItem.Save(cn);
                cnAP.AddNode(cn);
            }
            node.AddNode(cnAP);

            var cnLPs = new ConfigNode("LaunchPads");
            foreach (KCT_LaunchPad lp in LaunchPads)
            {
                ConfigNode lpCN = lp.AsConfigNode();
                lpCN.AddNode(lp.DestructionNode);
                cnLPs.AddNode(lpCN);
            }
            node.AddNode(cnLPs);

            return node;
        }

        public LCItem FromConfigNode(ConfigNode node)
        {
            BuildList.Clear();
            Warehouse.Clear();
            Plans.Clear();
            PadConstructions.Clear();
            Recon_Rollout.Clear();
            AirlaunchPrep.Clear();
            LaunchPads.Clear();
            _rate = 0;
            _rateHRCapped = 0;
            Engineers = 0;
            IsRushing = false;

            Name = node.GetValue("LCName");
            ActiveLaunchPadIndex = 0;
            node.TryGetValue("ActiveLPID", ref ActiveLaunchPadIndex);
            if (ActiveLaunchPadIndex < 0)
                ActiveLaunchPadIndex = 0;
            node.TryGetValue("operational", ref IsOperational);
            node.TryGetValue("id", ref _id);
            if (!node.TryGetValue("modID", ref _modID) || _modID == (new Guid()) )
                _modID = Guid.NewGuid();
            node.TryGetValue("Engineers", ref Engineers);
            node.TryGetValue("IsRushing", ref IsRushing);

            ConfigNode tmp = node.GetNode("Stats");
            if (tmp != null)
                ConfigNode.LoadObjectFromConfig(_lcData, tmp);

            tmp = node.GetNode("BuildList");
            foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
            {
                BuildList.Add(CreateBLVFromNode(cn));
            }

            tmp = node.GetNode("Warehouse");
            foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
            {
                Warehouse.Add(CreateBLVFromNode(cn));
            }

            tmp = node.GetNode("Plans");
            foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
            {
                var blv = CreateBLVFromNode(cn);
                Plans.Remove(blv.ShipName); 
                Plans.Add(blv.ShipName, blv);
            }

            tmp = node.GetNode("Recon_Rollout");
            foreach (ConfigNode RRCN in tmp.GetNodes("Recon_Rollout_Item"))
            {
                var tempRR = new ReconRolloutStorageItem();
                tempRR.Load(RRCN);
                Recon_Rollout.Add(tempRR.ToReconRollout());
            }

            if (node.TryGetNode("Airlaunch_Prep", ref tmp))
            {
                foreach (ConfigNode cn in tmp.GetNodes("Airlaunch_Prep_Item"))
                {
                    var storageItem = new AirlaunchPrepStorageItem();
                    storageItem.Load(cn);
                    AirlaunchPrep.Add(storageItem.ToAirlaunchPrep());
                }
            }

            tmp = node.GetNode("LaunchPads");
            if (tmp != null)
            {
                foreach (ConfigNode cn in tmp.GetNodes("KCT_LaunchPad"))
                {
                    var tempLP = new KCT_LaunchPad("LP0");
                    ConfigNode.LoadObjectFromConfig(tempLP, cn);
                    if (!cn.TryGetValue(nameof(KCT_LaunchPad.id), ref tempLP.id) || tempLP.id == Guid.Empty)
                    {
                        tempLP.id = Guid.NewGuid();
                    }
                    tempLP.DestructionNode = cn.GetNode("DestructionState");
                    if (tempLP.fractionalLevel == -1) tempLP.MigrateFromOldState();
                    LaunchPads.Add(tempLP);
                }
            }

            tmp = node.GetNode("PadConstructions");
            if (tmp != null)
            {
                foreach (ConfigNode cn in tmp.GetNodes("PadConstruction"))
                {
                    var storageItem = new PadConstructionStorageItem();
                    storageItem.Load(cn);
                    PadConstructions.Add(storageItem.ToPadConstruction());
                }
            }

            if (KCTGameStates.LoadedSaveVersion < KCTGameStates.VERSION)
            {
                if (KCTGameStates.LoadedSaveVersion < 1)
                {
                    Engineers *= 2;
                }

                if (KCTGameStates.LoadedSaveVersion < 6 && LCType != LaunchComplexType.Hangar)
                {
                    double oldEffic = 0.5d;
                    node.TryGetValue("EfficiencyEngineers", ref oldEffic);

                    // we can't use the dict yet
                    bool createEffic = true;
                    foreach (var e in KerbalConstructionTimeData.Instance.LCEfficiencies)
                    {
                        if (e.Contains(_id) && e.Efficiency < oldEffic)
                        {
                            e.IncreaseEfficiency(oldEffic - e.Efficiency, false);
                            createEffic = false;
                            break;
                        }
                    }
                    if (createEffic)
                    {
                        LCEfficiency closest = LCEfficiency.FindClosest(this, out double closeness);
                        if (closeness == 1d && closest.Efficiency < oldEffic)
                        {
                            closest.IncreaseEfficiency(oldEffic - closest.Efficiency, false);
                            createEffic = false;
                        }
                    }
                    if (createEffic)
                    {
                        var e = LCEfficiency.GetOrCreateEfficiencyForLC(this, true);
                        if (e.Efficiency < oldEffic)
                            e.IncreaseEfficiency(oldEffic - e.Efficiency, false);
                    }
                }
                if (KCTGameStates.LoadedSaveVersion < 7)
                {
                    node.TryGetEnum<LaunchComplexType>("lcType", ref _lcData.lcType, LaunchComplexType.Pad);
                    node.TryGetValue("massMax", ref _lcData.massMax);
                    node.TryGetValue("massOrig", ref _lcData.massOrig);
                    node.TryGetValue("sizeMax", ref _lcData.sizeMax);
                    node.TryGetValue("IsHumanRated", ref _lcData.isHumanRated);
                }
                if (KCTGameStates.LoadedSaveVersion < 8)
                {
                    if (_id == Guid.Empty)
                        _id = Guid.NewGuid();
                    if (_modID == Guid.Empty)
                        _modID = Guid.NewGuid();

                    // check if we're the hangar
                    if (_ksc.LaunchComplexes.Count == 0)
                    {
                        if (_lcData.lcType != LaunchComplexType.Hangar || _lcData.massMax != float.MaxValue)
                            _lcData.SetFrom(StartingHangar);
                    }
                }
                if (KCTGameStates.LoadedSaveVersion < 12)
                {
                    _lcData.Name = Name;
                }
            }

            return this;
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
