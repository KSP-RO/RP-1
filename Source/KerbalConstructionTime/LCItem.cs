using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class LCItem
    {
        public struct StartingLCData
        {
            public string Name;
            public float massMax;
            public Vector3 sizeMax;
            public bool isPad;

            public StartingLCData(string Name, float massMax, Vector3 sizeMax, bool isPad)
            {
                this.Name = Name;
                this.massMax = massMax;
                this.sizeMax = sizeMax;
                this.isPad = isPad;
            }

            // NOTE: Not comparing name, which I think is correct here.
            public bool Compare(LCItem lc) => massMax == lc.massMax && sizeMax == lc.sizeMax;
        }
        public static StartingLCData StartingHangar = new StartingLCData("Hangar", -1f, new Vector3(40f, 10f, 40f), false);
        public static StartingLCData StartingLC = new StartingLCData("Launch Complex 1", 15f, new Vector3(5f, 20f, 5f), true);

        public string Name;
        protected Guid _id;
        public Guid ID => _id;
        public List<BuildListVessel> BuildList = new List<BuildListVessel>();
        public List<BuildListVessel> Warehouse = new List<BuildListVessel>();
        public SortedList<string, BuildListVessel> Plans = new SortedList<string, BuildListVessel>();
        public KCTObservableList<PadConstruction> PadConstructions = new KCTObservableList<PadConstruction>();
        public List<ReconRollout> Recon_Rollout = new List<ReconRollout>();
        public List<AirlaunchPrep> AirlaunchPrep = new List<AirlaunchPrep>();
        public double Rate => _rate;
        private double _rate;
        public int Personnel = 0;
        public int MaxPersonnel => Math.Max(1, (int)Math.Ceiling(massMax > 0f ? Math.Pow(massMax, 0.75d) : sizeMax.sqrMagnitude * 0.2d)) * 5;
        public double EfficiencyPersonnel = 1d;

        public bool isOperational = false;
        public bool isPad = true;

        public float massMax, massMin;
        public Vector3 sizeMax;

        public List<KCT_LaunchPad> LaunchPads = new List<KCT_LaunchPad>();
        public int ActiveLaunchPadID = 0;

        public string SupportedMassAsPrettyText => massMax == -1f ? "unlimited" : $"{massMin:N0}-{massMax:N0}t";

        public string SupportedSizeAsPrettyText => sizeMax.y == float.MaxValue ? "unlimited" : $"{sizeMax.z:N0}x{sizeMax.x:N0}x{sizeMax.y:N0}m";

        private KSCItem _ksc = null;

        public KSCItem KSC
        {
            get
            {
                // Set on create, shouldn't be needed.

                //if (_ksc == null)
                //{
                //    _ksc = KCTGameStates.KSCs.Find(ksc => ksc.LaunchComplexes.Contains(this));
                //}
                return _ksc;
            }
        }

        public LCItem(KSCItem ksc)
        {
            _ksc = ksc;
        }

        public LCItem(StartingLCData lcData, KSCItem ksc) : this(lcData.Name, lcData.massMax, lcData.sizeMax, lcData.isPad, ksc) { }

        public LCItem(string lcName, float mMax, Vector3 sMax, bool isLCPad, KSCItem ksc)
        {
            Name = lcName;

            _id = Guid.NewGuid();
            _ksc = ksc;
            isPad = isLCPad;
            massMax = mMax;
            float fracLevel;

            KCT_GUI.GetPadStats(massMax, sMax, out massMin, out _, out _, out fracLevel);

            sizeMax = sMax;

            if (isPad)
            {
                var pad = new KCT_LaunchPad(Name + "A", fracLevel, massMax, sizeMax);
                pad.isOperational = true;
                LaunchPads.Add(pad);
            }

            PadConstructions.Added += added;
            PadConstructions.Removed += removed;

            void added(int idx, IConstructionBuildItem pc) { ksc.Constructions.Add(pc); }
            void removed(int idx, IConstructionBuildItem pc) { ksc.Constructions.Remove(pc); }
        }

        public void Modify(float mMax, Vector3 sMax)
        {
            massMax = mMax;
            float fracLevel;

            KCT_GUI.GetPadStats(massMax, sMax, out massMin, out _, out _, out fracLevel);

            sizeMax = sMax;

            foreach (var pad in LaunchPads)
            {
                pad.fractionalLevel = fracLevel;
                pad.level = (int)fracLevel;
                pad.supportedMass = mMax;
                pad.supportedSize = sMax;
            }

            Personnel = 0;
        }

        public KCT_LaunchPad ActiveLPInstance => LaunchPads.Count > ActiveLaunchPadID ? LaunchPads[ActiveLaunchPadID] : null;

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
                    !PadConstructions.Any() && LaunchPads.Count < 2 && (isPad ? StartingLC : StartingHangar).Compare(this);

        public bool CanModify => !BuildList.Any() && !Recon_Rollout.Any() && !AirlaunchPrep.Any() && !PadConstructions.Any();

        public ReconRollout GetReconditioning(string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.LaunchPadID == launchSite && ((IKCTBuildItem)r).GetItemName() == "LaunchPad Reconditioning");

        public ReconRollout GetReconRollout(ReconRollout.RolloutReconType type, string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.RRType == type && r.LaunchPadID == launchSite);

        public void RecalculateBuildRates()
        {
            _rate = Utilities.GetBuildRate(0, this, true);
            foreach (var blv in BuildList)
                blv.UpdateBuildRate();

            KCTDebug.Log($"Build rate for {Name} = {_rate:N3}");
        }

        public void SwitchToPrevLaunchPad() => SwitchLaunchPad(false);
        public void SwitchToNextLaunchPad() => SwitchLaunchPad(true);

        public void SwitchLaunchPad(bool forwardDirection)
        {
            if (LaunchPadCount < 2) return;

            int idx = ActiveLaunchPadID;
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
            if (LP_ID < 0)
                LP_ID = ActiveLaunchPadID;
            else
                ActiveLaunchPadID = LP_ID;

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
            node.AddValue("ActiveLPID", ActiveLaunchPadID);
            node.AddValue("operational", isOperational);
            node.AddValue("isPad", isPad);
            node.AddValue("massMax", massMax);
            node.AddValue("massMin", massMin);
            node.AddValue("sizeMax", sizeMax);
            node.AddValue("id", _id);
            node.AddValue("Personnel", Personnel);
            node.AddValue("EfficiencyPersonnel", EfficiencyPersonnel);
            node.AddValue("BuildRate", _rate);

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
            Personnel = 0;
            EfficiencyPersonnel = 1d;

            Name = node.GetValue("LCName");
            ActiveLaunchPadID = 0;
            node.TryGetValue("ActiveLPID", ref ActiveLaunchPadID);
            node.TryGetValue("operational", ref isOperational);
            node.TryGetValue("isPad", ref isPad);
            node.TryGetValue("massMax", ref massMax);
            node.TryGetValue("massMin", ref massMin);
            node.TryGetValue("sizeMax", ref sizeMax);
            node.TryGetValue("id", ref _id);
            node.TryGetValue("Personnel", ref Personnel);
            node.TryGetValue("EfficiencyPersonnel", ref EfficiencyPersonnel);
            node.TryGetValue("BuildRate", ref _rate);

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
