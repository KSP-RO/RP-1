using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public class LCItem : IConfigNode
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
        public PersistentList<KCT_LaunchPad> LaunchPads = new PersistentList<KCT_LaunchPad>();
        [Persistent]
        public KCTObservableList<BuildListVessel> BuildList = new KCTObservableList<BuildListVessel>();
        [Persistent]
        public KCTObservableList<BuildListVessel> Warehouse = new KCTObservableList<BuildListVessel>();
        [Persistent]
        public KCTObservableList<PadConstruction> PadConstructions = new KCTObservableList<PadConstruction>();
        [Persistent]
        public PersistentList<ReconRollout> Recon_Rollout = new PersistentList<ReconRollout>();
        [Persistent]
        public PersistentList<AirlaunchPrep> Airlaunch_Prep = new PersistentList<AirlaunchPrep>();

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
        public int MaxEngineersFor(BuildListVessel blv) => blv == null ? MaxEngineers : MaxEngineersFor(blv.GetTotalMass(), blv.buildPoints + blv.integrationPoints, blv.humanRated);

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

        public double RushRate => IsRushing ? PresetManager.Instance.ActivePreset.GeneralSettings.RushRateMult : 1d;
        public double RushSalary => IsRushing ? PresetManager.Instance.ActivePreset.GeneralSettings.RushSalaryMult : 1d;

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

        private KSCItem _ksc = null;

        public KSCItem KSC => _ksc;

        #region Observable funcs
        void added(int idx, ConstructionBuildItem pc) { _ksc.Constructions.Add(pc); }
        void removed(int idx, ConstructionBuildItem pc) { _ksc.Constructions.Remove(pc); }
        void updated() { RP0.MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate(); }

        void AddListeners()
        {
            PadConstructions.Added += added;
            PadConstructions.Removed += removed;
            PadConstructions.Updated += updated;

            BuildList.Updated += updated;
            Warehouse.Updated += updated;
        }
        #endregion

        public LCItem() { } // does not add listeners, instead adds them in Load.

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
                var pad = new KCT_LaunchPad(Guid.NewGuid(), Name + "-A", fracLevel);
                pad.isOperational = true;
                LaunchPads.Add(pad);
            }

            AddListeners();

            KerbalConstructionTimeData.Instance.RegisterLC(this);
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

        public bool IsEmpty => LCType == LaunchComplexType.Hangar && BuildList.Count == 0 && Warehouse.Count == 0 && Airlaunch_Prep.Count == 0 && Engineers == 0 && LCData.StartingHangar.Compare(this);

        public bool IsActive => BuildList.Count > 0 || Recon_Rollout.Count > 0 || Airlaunch_Prep.Count > 0;
        public bool CanDismantle => BuildList.Count == 0 && Warehouse.Count == 0 && !Recon_Rollout.Any(r => r.RRType != ReconRollout.RolloutReconType.Reconditioning) && Airlaunch_Prep.Count == 0;
        public bool CanModifyButton => BuildList.Count == 0 && Warehouse.Count == 0 && Recon_Rollout.Count == 0 && Airlaunch_Prep.Count == 0;
        public bool CanModifyReal => Recon_Rollout.Count == 0 && Airlaunch_Prep.Count == 0;
        public bool IsIdle => !IsActive;

        public ReconRollout GetReconditioning(string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.launchPadID == launchSite && ((IKCTBuildItem)r).GetItemName() == "LaunchPad Reconditioning");

        public ReconRollout GetReconRollout(ReconRollout.RolloutReconType type, string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => (type == ReconRollout.RolloutReconType.None ||  r.RRType == type) && r.launchPadID == launchSite);

        public void RecalculateBuildRates()
        {
            CalculateAndSetRates();
            foreach (var blv in BuildList)
                blv.UpdateBuildRate();

            foreach (var rr in Recon_Rollout)
                rr.UpdateBuildRate();

            foreach (var al in Airlaunch_Prep)
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

        public void Delete()
        {
            if (_efficiencySource == null)
                KerbalConstructionTimeData.Instance.LCToEfficiency.TryGetValue(this, out _efficiencySource);
            if (_efficiencySource != null)
                _efficiencySource.RemoveLC(this);
            else
                LCEfficiency.ClearEmpty();

            foreach (var lp in LaunchPads)
                KerbalConstructionTimeData.Instance.UnregsiterLP(lp);

            KerbalConstructionTimeData.Instance.UnregisterLC(this);

            int index = KSC.LaunchComplexes.IndexOf(this);
            KSC.LaunchComplexes.RemoveAt(index);
            if (KSC.ActiveLaunchComplexIndex >= index)
                --KSC.ActiveLaunchComplexIndex; // should not change active LC unless it was this
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);

            foreach (var rr in Recon_Rollout)
                rr.LC = this;

            foreach (var al in Airlaunch_Prep)
                al.LC = this;

            foreach (var blv in BuildList)
                blv.LinkToLC(this);
            foreach (var blv in Warehouse)
                blv.LinkToLC(this);

            AddListeners();
        }

        public void PostLoad(KSCItem ksc)
        {
            _ksc = ksc;
            int i = 0;
            foreach (var pc in PadConstructions)
                added(i++, pc);

            KerbalConstructionTimeData.Instance.RegisterLC(this);
            foreach(var lp in LaunchPads)
                KerbalConstructionTimeData.Instance.RegisterLP(lp);

            if (HighLogic.LoadedSceneIsEditor)
            {
                // Editor scene needs LC rates to show build time estimates
                CalculateAndSetRates();
            }
        }

        private void CalculateAndSetRates()
        {
            _strategyRateMultiplier = RP0.CurrencyUtils.Rate(LCType == LaunchComplexType.Pad ? RP0.TransactionReasonsRP0.RateIntegrationVAB : RP0.TransactionReasonsRP0.RateIntegrationSPH);
            _rate = Utilities.GetBuildRate(0, this, IsHumanRated, true);
            _rateHRCapped = Utilities.GetBuildRate(0, this, false, true);
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
