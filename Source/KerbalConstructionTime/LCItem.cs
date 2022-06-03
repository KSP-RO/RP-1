using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
            [Persistent] public LaunchComplexType lcType;
            [Persistent] public bool isHumanRated;

            public LCData() { }

            public LCData(string Name, float massMax, float massOrig, Vector3 sizeMax, LaunchComplexType lcType, bool isHumanRated)
            {
                this.Name = Name;
                this.massMax = massMax;
                this.massOrig = massOrig;
                this.sizeMax = sizeMax;
                this.lcType = lcType;
                this.isHumanRated = isHumanRated;
            }

            public LCData(LCData old)
            {
                Name = old.Name;
                massMax = old.massMax;
                massOrig = old.massOrig;
                sizeMax = old.sizeMax;
                lcType = old.lcType;
                isHumanRated = old.isHumanRated;
            }

            // NOTE: Not comparing name, which I think is correct here.
            public bool Compare(LCItem lc) => massMax == lc.MassMax && sizeMax == lc.SizeMax;
        }
        public static LCData StartingHangar = new LCData("Hangar", float.MaxValue, float.MaxValue, new Vector3(40f, 10f, 40f), LaunchComplexType.Hangar, true);
        public static LCData StartingLC1 = new LCData("Launch Complex 1", 1f, 1.5f, new Vector3(2f, 10f, 2f), LaunchComplexType.Pad, false);
        public static LCData StartingLC15 = new LCData("Launch Complex 1", 15f, 15f, new Vector3(5f, 20f, 5f), LaunchComplexType.Pad, false);

        public string Name;
        protected Guid _id;
        public Guid ID => _id;
        protected Guid _modID;
        public Guid ModID => _modID;
        public List<BuildListVessel> BuildList = new List<BuildListVessel>();
        public List<BuildListVessel> Warehouse = new List<BuildListVessel>();
        public SortedList<string, BuildListVessel> Plans = new SortedList<string, BuildListVessel>();
        public KCTObservableList<PadConstruction> PadConstructions = new KCTObservableList<PadConstruction>();
        public List<ReconRollout> Recon_Rollout = new List<ReconRollout>();
        public List<AirlaunchPrep> AirlaunchPrep = new List<AirlaunchPrep>();
        private double _rate;
        private double _rateHRCapped;
        public double Rate => _rate;
        public double RateHRCapped => _rateHRCapped;
        
        public int Engineers = 0;
        private static double RawMaxEngineers(float massMax, Vector3 sizeMax) =>
            massMax != float.MaxValue ? Math.Pow(massMax, 0.75d) : sizeMax.sqrMagnitude * 0.01d;
        public static int MaxEngineersCalc(float massMax, Vector3 sizeMax, bool isHuman) => 
            Math.Max(5, (int)Math.Ceiling(RawMaxEngineers(massMax, sizeMax) * (isHuman ? 1.5d : 1d)) * 5);

        private double _RawMaxEngineers => RawMaxEngineers(MassMax, SizeMax);
        public int MaxEngineers => MaxEngineersCalc(MassMax, SizeMax, IsHumanRated);
        public int MaxEngineersNonHR => Math.Max(5, (int)Math.Ceiling(_RawMaxEngineers)) * 5;
        public int MaxEngineersFor(double mass, double bp, bool humanRated)
        {
            if (LCType == LaunchComplexType.Pad)
                return IsHumanRated && !humanRated ? MaxEngineersNonHR : MaxEngineers;

            double tngMax = RawMaxEngineers((float)mass, Vector3.zero);
            if (IsHumanRated && humanRated)
                tngMax *= 1.5d;
            double bpMax = Math.Pow(bp * 0.000015d, 0.75d);
            return Math.Max(5, (int)Math.Ceiling(tngMax * 0.25d + bpMax * 0.75d) * 5);
        }
        public int MaxEngineersFor(BuildListVessel blv) => blv == null ? MaxEngineers : MaxEngineersFor(blv.GetTotalMass(), blv.BuildPoints + blv.IntegrationPoints, blv.IsHumanRated);

        public const double StartingEfficiency = 0.5d;
        public double EfficiencyEngineers = StartingEfficiency;
        public double LastEngineers = 0d;
        public bool IsRushing;
        public double RushRate => IsRushing ? PresetManager.Instance.ActivePreset.GeneralSettings.RushRateMult : 1d;
        public double RushSalary => IsRushing ? PresetManager.Instance.ActivePreset.GeneralSettings.RushSalaryMult : 1d;

        public bool IsOperational = false;
        public LaunchComplexType LCType = LaunchComplexType.Pad;
        public bool IsHumanRated = false;

        public float MassMax;
        public float MassOrig;
        public static float CalcMassMin(float massMax) => massMax == float.MaxValue ? 0f : Mathf.Floor(massMax * 0.75f);
        public float MassMin => CalcMassMin(MassMax);
        public Vector3 SizeMax;

        public List<KCT_LaunchPad> LaunchPads = new List<KCT_LaunchPad>();
        public int ActiveLaunchPadIndex = 0;

        public string SupportedMassAsPrettyText => MassMax == float.MaxValue ? "unlimited" : $"{MassMin:N0}-{MassMax:N0}t";

        public string SupportedSizeAsPrettyText => SizeMax.y == float.MaxValue ? "unlimited" : $"{SizeMax.z:N0}x{SizeMax.x:N0}x{SizeMax.y:N0}m";

        private KSCItem _ksc = null;

        public KSCItem KSC => _ksc;

        public LCItem(KSCItem ksc)
        {
            _ksc = ksc;
        }

        public LCItem(LCData lcData, KSCItem ksc) : this(lcData.Name, lcData.massMax, lcData.massOrig, lcData.sizeMax, lcData.lcType, lcData.isHumanRated, ksc) { }

        public LCItem(string lcName, float mMax, float mOrig, Vector3 sMax, LaunchComplexType lcType, bool isHuman, KSCItem ksc)
        {
            Name = lcName;

            _id = Guid.NewGuid();
            _modID = Guid.NewGuid();
            _ksc = ksc;
            LCType = lcType;
            IsHumanRated = isHuman;
            MassMax = mMax;
            MassOrig = mOrig;
            // back-compat
            if (mOrig == 0)
                MassOrig = MassMax;
            float fracLevel;

            KCT_GUI.GetPadStats(MassMax, sMax, IsHumanRated, out _, out _, out fracLevel);

            SizeMax = sMax;

            if (LCType == LaunchComplexType.Pad)
            {
                var pad = new KCT_LaunchPad(Name, fracLevel);
                pad.isOperational = true;
                LaunchPads.Add(pad);
            }

            PadConstructions.Added += added;
            PadConstructions.Removed += removed;

            void added(int idx, ConstructionBuildItem pc) { ksc.Constructions.Add(pc); }
            void removed(int idx, ConstructionBuildItem pc) { ksc.Constructions.Remove(pc); }
        }

        public void Modify(LCData data)
        {
            _modID = Guid.NewGuid();
            MassMax = data.massMax;
            MassOrig = data.massOrig;
            // back-compat
            if (MassOrig == 0)
                MassOrig = MassMax;
            IsHumanRated = data.isHumanRated;
            SizeMax = data.sizeMax;
            float fracLevel;

            KCT_GUI.GetPadStats(MassMax, SizeMax, IsHumanRated, out _, out _, out fracLevel);

            foreach (var pad in LaunchPads)
            {
                pad.fractionalLevel = fracLevel;
                pad.level = (int)fracLevel;
            }
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

        public bool IsEmpty => !BuildList.Any() && !Warehouse.Any() && !Recon_Rollout.Any() && !AirlaunchPrep.Any() &&
                    !PadConstructions.Any() && LaunchPads.Count < 2 && EfficiencyEngineers == StartingEfficiency && LastEngineers < 0.001d &&
                    (LCType == LaunchComplexType.Pad ? StartingLC1.Compare(this) || StartingLC15.Compare(this) : StartingHangar.Compare(this));

        public bool IsActive => BuildList.Any() || Recon_Rollout.Any() || AirlaunchPrep.Any();

        public bool CanModify => !IsActive && !PadConstructions.Any();

        public ReconRollout GetReconditioning(string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.LaunchPadID == launchSite && ((IKCTBuildItem)r).GetItemName() == "LaunchPad Reconditioning");

        public ReconRollout GetReconRollout(ReconRollout.RolloutReconType type, string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => (type == ReconRollout.RolloutReconType.None ||  r.RRType == type) && r.LaunchPadID == launchSite);

        public void RecalculateBuildRates()
        {
            _rate = Utilities.GetBuildRate(0, this, IsHumanRated, true);
            _rateHRCapped = Utilities.GetBuildRate(0, this, false, true);
            foreach (var blv in BuildList)
                blv.UpdateBuildRate();

            KCTDebug.Log($"Build rate for {Name} = {_rate:N3}, capped {_rateHRCapped:N3}");
        }

        public void SwitchToPrevLaunchPad() => SwitchLaunchPad(false);
        public void SwitchToNextLaunchPad() => SwitchLaunchPad(true);

        public void SwitchLaunchPad(bool forwardDirection)
        {
            if (LaunchPadCount < 2) return;

            int idx = ActiveLaunchPadIndex;
            KCT_LaunchPad pad;
            do
            {
                if (forwardDirection)
                {
                    idx = (idx + 1) % LaunchPads.Count;
                }
                else
                {
                    //Simple fix for mod function being "weird" in the negative direction
                    //http://stackoverflow.com/questions/1082917/mod-of-negative-number-is-melting-my-brain
                    idx = ((idx - 1) % LaunchPads.Count + LaunchPads.Count) % LaunchPads.Count;
                }
                pad = LaunchPads[idx];
            } while (!pad.isOperational);

            SwitchLaunchPad(idx);
        }

        public void Rename(string newName)
        {
            //find everything that references this launchpad by name and update the name reference

            Name = newName;
            // TODO: do we need to rename vessels?
        }

        public void SwitchLaunchPad(int LP_ID = -1, bool updateDestrNode = true)
        {
            if (LP_ID >= 0)
                ActiveLaunchPadIndex = LP_ID;

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
            node.AddValue("lcType", LCType);
            node.AddValue("massMax", MassMax);
            node.AddValue("massOrig", MassOrig);
            node.AddValue("sizeMax", SizeMax);
            node.AddValue("id", _id);
            node.AddValue("modID", _modID);
            node.AddValue("Engineers", Engineers);
            node.AddValue("EfficiencyEngineers", EfficiencyEngineers);
            node.AddValue("LastEngineers", LastEngineers);
            node.AddValue("IsRushing", IsRushing);
            node.AddValue("BuildRate", _rate);
            node.AddValue("BuildRateCapped", _rateHRCapped);
            node.AddValue("IsHumanRated", IsHumanRated);

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
                cn = ConfigNode.CreateConfigFromObject(storageItem, cn);
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
                rrCN = ConfigNode.CreateConfigFromObject(storageItem, rrCN);
                cnRR.AddNode(rrCN);
            }
            node.AddNode(cnRR);

            var cnAP = new ConfigNode("Airlaunch_Prep");
            foreach (AirlaunchPrep ap in AirlaunchPrep)
            {
                var storageItem = new AirlaunchPrepStorageItem();
                storageItem.FromAirlaunchPrep(ap);
                var cn = new ConfigNode("Airlaunch_Prep_Item");
                cn = ConfigNode.CreateConfigFromObject(storageItem, cn);
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
            EfficiencyEngineers = 0d;
            IsRushing = false;
            LastEngineers = 0;
            IsHumanRated = false;

            Name = node.GetValue("LCName");
            ActiveLaunchPadIndex = 0;
            node.TryGetValue("ActiveLPID", ref ActiveLaunchPadIndex);
            node.TryGetValue("operational", ref IsOperational);
            string lcTypeVal = node.GetValue("lcType");
            if (!string.IsNullOrEmpty(lcTypeVal))
                Enum.TryParse(lcTypeVal, out LCType);
            node.TryGetValue("massMax", ref MassMax);
            node.TryGetValue("massOrig", ref MassOrig);
            node.TryGetValue("sizeMax", ref SizeMax);
            node.TryGetValue("id", ref _id);
            if (!node.TryGetValue("modID", ref _modID))
                _modID = Guid.NewGuid();
            node.TryGetValue("Engineers", ref Engineers);
            node.TryGetValue("EfficiencyEngineers", ref EfficiencyEngineers);
            node.TryGetValue("IsRushing", ref IsRushing);
            node.TryGetValue("LastEngineers", ref LastEngineers);
            node.TryGetValue("BuildRate", ref _rate);
            node.TryGetValue("BuildRateCapped", ref _rateHRCapped);
            node.TryGetValue("IsHumanRated", ref IsHumanRated);

            //back-Compat
            if (node.HasValue("isPad"))
            {
                bool isPad = true;
                node.TryGetValue("isPad", ref isPad);
                LCType = isPad ? LaunchComplexType.Pad : LaunchComplexType.Hangar;
            }
            if (LCType == LaunchComplexType.Hangar)
                IsHumanRated = true;
            if (MassMax == -1f)
                MassMax = float.MaxValue;
            if (MassOrig == 0)
                MassOrig = MassMax;
            if (node.HasValue("Personnel"))
            {
                node.TryGetValue("Personnel", ref Engineers);
                node.TryGetValue("EfficiencyPersonnel", ref EfficiencyEngineers);
            }

            ConfigNode tmp = node.GetNode("BuildList");
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
                ConfigNode.LoadObjectFromConfig(tempRR, RRCN);
                Recon_Rollout.Add(tempRR.ToReconRollout());
            }

            if (node.TryGetNode("Airlaunch_Prep", ref tmp))
            {
                foreach (ConfigNode cn in tmp.GetNodes("Airlaunch_Prep_Item"))
                {
                    var storageItem = new AirlaunchPrepStorageItem();
                    ConfigNode.LoadObjectFromConfig(storageItem, cn);
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
                    ConfigNode.LoadObjectFromConfig(storageItem, cn);
                    PadConstructions.Add(storageItem.ToPadConstruction());
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
