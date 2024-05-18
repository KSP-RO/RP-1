using ROUtils.DataTypes;
using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace RP0
{
    public class LaunchComplex : IConfigNode
    {
        public const int MinEngineersConst = 1;
        public const int EngineersPerPacket = 10;

        public double accumEffic = 0d; // for UI use

        [Persistent(name = "LCName")]
        public string Name;
        [Persistent(name = "id")]
        private Guid _id;
        public Guid ID => _id;
        [Persistent(name = "modID")]
        private Guid _modID;
        public Guid ModID => _modID;
        [Persistent]
        public int Engineers = 0;
        [Persistent]
        public bool IsRushing;
        [Persistent(name = "operational")]
        public bool IsOperational = false;
        [Persistent]
        public int ActiveLaunchPadIndex = 0;

        [Persistent(name = "Stats")]
        private LCData _lcData = new LCData();
        public LCData Stats => _lcData;


        [Persistent]
        public PersistentList<LCLaunchPad> LaunchPads = new PersistentList<LCLaunchPad>();
        [Persistent]
        public PersistentObservableList<VesselProject> BuildList = new PersistentObservableList<VesselProject>();
        [Persistent]
        public PersistentObservableList<VesselProject> Warehouse = new PersistentObservableList<VesselProject>();
        [Persistent]
        public PersistentObservableList<PadConstructionProject> PadConstructions = new PersistentObservableList<PadConstructionProject>();
        [Persistent]
        public PersistentObservableList<ReconRolloutProject> Recon_Rollout = new PersistentObservableList<ReconRolloutProject>();
        [Persistent]
        public PersistentObservableList<VesselRepairProject> VesselRepairs = new PersistentObservableList<VesselRepairProject>();

        private double _rate;
        private double _rateHRCapped;
        public double Rate => _rate;
        public double RateHRCapped => _rateHRCapped;

        public override string ToString() => $"{Name} ({(IsOperational ? "Operational" : "Inop")})";

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
        public int MaxEngineersFor(VesselProject vp) => vp == null ? MaxEngineers : MaxEngineersFor(vp.GetTotalMass(), vp.buildPoints, vp.humanRated);

        private double _strategyRateMultiplier = 1d;
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

        public double RushRate => IsRushing ? Database.SettingsSC.RushRateMult : 1d;
        public double RushSalary => IsRushing ? Database.SettingsSC.RushSalaryMult : 1d;

        public LaunchComplexType LCType => _lcData.lcType;
        public bool IsHumanRated => _lcData.isHumanRated;
        public float MassMax => _lcData.massMax;
        public float MassOrig => _lcData.massOrig;
        public float MassMin => _lcData.MassMin;
        public Vector3 SizeMax => _lcData.sizeMax;
        public PersistentDictionaryValueTypes<string, double> ResourcesHandled => _lcData.resourcesHandled;
                

        public static string SupportedMassAsPrettyTextCalc(float mass) => mass == float.MaxValue ? "unlimited" : $"{LCData.CalcMassMin(mass):N0}-{mass:N0}t";
        public string SupportedMassAsPrettyText => SupportedMassAsPrettyTextCalc(MassMax);

        public static string SupportedSizeAsPrettyTextCalc(Vector3 size) => size.y == float.MaxValue ? "unlimited" : $"{size.z:N0}x{size.x:N0}x{size.y:N0}m";
        public string SupportedSizeAsPrettyText => SupportedSizeAsPrettyTextCalc(SizeMax);

        private LCSpaceCenter _ksc = null;

        public LCSpaceCenter KSC => _ksc;

        #region Observable funcs
        void added(int idx, ConstructionProject pc) { _ksc.Constructions.Add(pc); }
        void removed(int idx, ConstructionProject pc) { _ksc.Constructions.Remove(pc); }
        void updated() { MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate(); }
        void lcpUpdated()
        {
            RecalculateProjectBP();
            MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
        }

        void AddListeners()
        {
            PadConstructions.Added += added;
            PadConstructions.Removed += removed;
            PadConstructions.Updated += updated;

            BuildList.Updated += updated;
            Warehouse.Updated += updated;
            Recon_Rollout.Updated += lcpUpdated;
            VesselRepairs.Updated += lcpUpdated;
        }
        #endregion

        public LaunchComplex() { } // does not add listeners, instead adds them in Load.

        public LaunchComplex(LCData lcData, LCSpaceCenter ksc)
        {
            _ksc = ksc;
            _id = Guid.NewGuid();
            _modID = _id;
            _lcData.SetFrom(lcData);
            Name = _lcData.Name;

            if (_lcData.lcType == LaunchComplexType.Pad)
            {
                float fracLevel = _lcData.GetPadFracLevel();
                var pad = new LCLaunchPad(Guid.NewGuid(), Name + "-A", fracLevel);
                pad.isOperational = true;
                LaunchPads.Add(pad);
            }

            AddListeners();

            SpaceCenterManagement.Instance.RegisterLC(this);
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
            if (LCType != LaunchComplexType.Hangar)
                _efficiencySource = LCEfficiency.GetOrCreateEfficiencyForLC(this, false);

            RecalculateBuildRates();
        }

        public LCLaunchPad ActiveLPInstance => LaunchPads.Count > ActiveLaunchPadIndex && ActiveLaunchPadIndex >= 0 ? LaunchPads[ActiveLaunchPadIndex] : null;

        public int LaunchPadCount
        {
            get
            {
                int count = 0;
                foreach (LCLaunchPad lp in LaunchPads)
                    if (lp.isOperational) count++;
                return count;
            }
        }

        public bool IsEmpty => LCType == LaunchComplexType.Hangar && BuildList.Count == 0 && Warehouse.Count == 0 && Engineers == 0 && LCData.StartingHangar.Compare(this);

        public bool IsActive => BuildList.Count > 0 || GetAllLCOps().Any(op => !op.IsComplete());
        public bool CanDismantle => BuildList.Count == 0 && Warehouse.Count == 0 && 
                                    !Recon_Rollout.Any(r => r.RRType != ReconRolloutProject.RolloutReconType.Reconditioning) &&
                                    VesselRepairs.Count == 0;
        public bool CanModifyButton => BuildList.Count == 0 && Warehouse.Count == 0 && Recon_Rollout.Count == 0 && VesselRepairs.Count == 0;
        public bool CanModifyReal => Recon_Rollout.Count == 0 && VesselRepairs.Count == 0;
        public bool CanIntegrate => ProjectBPTotal == 0d;

        private double _projectBPTotal = -1d;
        public double ProjectBPTotal => _projectBPTotal < 0d ? RecalculateProjectBP() : _projectBPTotal;

        public double RecalculateProjectBP()
        {
            _projectBPTotal = 0d;
            foreach (var r in GetAllLCOps())
            {
                if (!r.IsBlocking || r.IsComplete())
                    continue;
                double amt = r.BP;
                if (amt < 0d)
                    amt = -amt;
                _projectBPTotal += amt;
            }
            return _projectBPTotal;
        }
        public double GetBlockingProjectTimeLeft()
        {
            if (_projectBPTotal == 0d)
                return 0d;

            return LCOpsProject.GetTotalBlockingProjectTime(this);
        }

        public ReconRolloutProject GetReconditioning(string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.launchPadID == launchSite && ((ISpaceCenterProject)r).GetItemName() == "LaunchPad Reconditioning");

        public ReconRolloutProject GetReconRollout(ReconRolloutProject.RolloutReconType type, string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => (type == ReconRolloutProject.RolloutReconType.None ||  r.RRType == type) && r.launchPadID == launchSite);

        public void RecalculateBuildRates()
        {
            CalculateAndSetRates();
            foreach (var vp in BuildList)
                vp.UpdateBuildRate();

            foreach (var op in GetAllLCOps())
                op.UpdateBuildRate();

            RecalculateProjectBP();

            RP0Debug.Log($"Build rate for {Name} = {_rate:N3}, capped {_rateHRCapped:N3}");
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
            LCLaunchPad pad;
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
            foreach (LCLaunchPad lp in LaunchPads)
            {
                var padState = lp.State;
                if (padState > state)
                    state = padState;
            }

            return state;
        }

        public LCLaunchPad FindFreeLaunchPad()
        {
            foreach (LCLaunchPad lp in LaunchPads)
            {
                if (lp.State == LaunchPadState.Free)
                    return lp;
            }

            return null;
        }

        public void Delete()
        {
            if (_efficiencySource == null)
                SpaceCenterManagement.Instance.LCToEfficiency.TryGetValue(this, out _efficiencySource);
            if (_efficiencySource != null)
                _efficiencySource.RemoveLC(this);
            else
                LCEfficiency.ClearEmpty();

            foreach (var lp in LaunchPads)
                SpaceCenterManagement.Instance.UnregsiterLP(lp);

            SpaceCenterManagement.Instance.UnregisterLC(this);
            if (SpaceCenterManagement.Instance.staffTarget.LCID == ID)
                SpaceCenterManagement.Instance.staffTarget.Clear();

            int index = KSC.LaunchComplexes.IndexOf(this);
            KSC.LaunchComplexes.RemoveAt(index);
            if (KSC.LCIndex >= index)
                --KSC.LCIndex; // should not change active LC unless it was this
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);

            if (SpaceCenterManagement.Instance.LoadedSaveVersion < SpaceCenterManagement.VERSION)
            {
                if (SpaceCenterManagement.Instance.LoadedSaveVersion < 6)
                {
                    var alNode = node.GetNode("Airlaunch_Prep");
                    if (alNode != null && alNode.nodes.Count > 0)
                    {
                        PersistentList<ReconRolloutProject> temp = new PersistentList<ReconRolloutProject>();
                        foreach (ConfigNode item in alNode.nodes)
                        {
                            ReconRolloutProject.RolloutReconType rrType = ReconRolloutProject.RolloutReconType.AirlaunchMount;
                            if (item.GetValue("direction") == "Unmount")
                                rrType = ReconRolloutProject.RolloutReconType.AirlaunchUnmount;

                            item.AddValue("RRType", rrType);
                            item.AddValue("launchPadID", "");
                        }
                        temp.Load(alNode);
                        Recon_Rollout.AddRange(temp);
                    }
                }
                if (SpaceCenterManagement.Instance.LoadedSaveVersion < 8)
                {
                    var keys = new System.Collections.Generic.List<string>(_lcData.resourcesHandled.Keys);
                    foreach (var k in keys)
                    {
                        _lcData.resourcesHandled[k] = Math.Ceiling(_lcData.resourcesHandled[k]);
                    }
                }
            }

            foreach (var rr in GetAllLCOps())
                rr.LC = this;

            foreach (var vp in BuildList)
                vp.LinkToLC(this);
            foreach (var vp in Warehouse)
                vp.LinkToLC(this);

            AddListeners();
        }

        public void PostLoad(LCSpaceCenter ksc)
        {
            _ksc = ksc;
            int i = 0;
            foreach (var pc in PadConstructions)
                added(i++, pc);

            SpaceCenterManagement.Instance.RegisterLC(this);
            foreach(var lp in LaunchPads)
                SpaceCenterManagement.Instance.RegisterLP(lp);

            if (HighLogic.LoadedSceneIsEditor)
            {
                // Editor scene needs LC rates to show build time estimates
                CalculateAndSetRates();
            }
        }

        public IEnumerable<LCOpsProject> GetAllLCOps()
        {
            foreach (var item in Recon_Rollout)
            {
                yield return item;
            }

            foreach (var item in VesselRepairs)
            {
                yield return item;
            }
        }

        private void CalculateAndSetRates()
        {
            _strategyRateMultiplier = CurrencyUtils.Rate(LCType == LaunchComplexType.Pad ? TransactionReasonsRP0.RateIntegrationVAB : TransactionReasonsRP0.RateIntegrationSPH);
            _rate = KCTUtilities.GetBuildRate(0, this, IsHumanRated, true);
            _rateHRCapped = KCTUtilities.GetBuildRate(0, this, false, true);
            KCT_GUI.BuildRateForDisplay = null;
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
