﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class LCItem
    {
        public string LCName;
        public List<BuildListVessel> BuildList = new List<BuildListVessel>();
        public KCTObservableList<BuildListVessel> VABList = new KCTObservableList<BuildListVessel>();
        public List<BuildListVessel> VABWarehouse = new List<BuildListVessel>();
        public SortedList<string, BuildListVessel> VABPlans = new SortedList<string, BuildListVessel>();
        public KCTObservableList<BuildListVessel> SPHList = new KCTObservableList<BuildListVessel>();
        public List<BuildListVessel> SPHWarehouse = new List<BuildListVessel>();
        public SortedList<string, BuildListVessel> SPHPlans = new SortedList<string, BuildListVessel>();
        public List<PadConstruction> PadConstructions = new List<PadConstruction>();
        public List<int> VABUpgrades = new List<int>() { 0 };
        public List<int> SPHUpgrades = new List<int>() { 0 };
        public List<ReconRollout> Recon_Rollout = new List<ReconRollout>();
        public List<AirlaunchPrep> AirlaunchPrep = new List<AirlaunchPrep>();
        public List<double> VABRates = new List<double>(), SPHRates = new List<double>();
        public List<double> UpVABRates = new List<double>(), UpSPHRates = new List<double>();

        public bool isOperational = false;
        public bool isPad = true;

        public float massMax, massMin;
        public Vector3 sizeMax;

        public List<KCT_LaunchPad> LaunchPads = new List<KCT_LaunchPad>();
        public int ActiveLaunchPadID = 0;

        public string SupportedMassAsPrettyText => massMax == -1f ? "unlimited" : $"{massMin:N0}-{massMax:N0}t";

        public string SupportedSizeAsPrettyText => sizeMax.y == float.MaxValue ? "unlimited" : $"{sizeMax.x:N0}x{sizeMax.y:N0}m";

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

        public LCItem(string name, float mMax, Vector3 sMax, bool isLCPad, KSCItem ksc)
        {
            LCName = name;
            _ksc = ksc;
            isPad = isLCPad;
            massMax = mMax;
            float fracLevel;

            KCT_GUI.GetPadStats(massMax, sMax, out massMin, out _, out _, out fracLevel);

            sizeMax = sMax;

            if (isPad)
            {
                var pad = new KCT_LaunchPad(LCName + "A", fracLevel, massMax, sizeMax);
                pad.isOperational = true;
                LaunchPads.Add(pad);
            }

            VABList.Added += added;
            VABList.Removed += removed;
            SPHList.Added += added;
            SPHList.Removed += removed;

            void added(int idx, BuildListVessel vessel)
            {
                BuildList.Add(vessel);
            }
            void removed(int idx, BuildListVessel vessel)
            {
                BuildList.Remove(vessel);
            }
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

        public bool IsEmpty => !VABList.Any() && !VABWarehouse.Any() && !SPHList.Any() && !SPHWarehouse.Any() &&
                    VABUpgrades.All(i => i == 0) && SPHUpgrades.All(i => i == 0) && !Recon_Rollout.Any() && !AirlaunchPrep.Any() &&
                    !PadConstructions.Any() && LaunchPads.Count < 2 && LaunchPads.All(lp => lp.level < 1);

        public ReconRollout GetReconditioning(string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.LaunchPadID == launchSite && ((IKCTBuildItem)r).GetItemName() == "LaunchPad Reconditioning");

        public ReconRollout GetReconRollout(ReconRollout.RolloutReconType type, string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.RRType == type && r.LaunchPadID == launchSite);

        public void RecalculateBuildRates()
        {
            VABRates.Clear();
            SPHRates.Clear();
            double rate = 0.1;
            int index = 0;
            // These loops could clean up a little, is it intended to add a rate=0 in the loop as the last entry?
            while (rate > 0)
            {
                rate = MathParser.ParseBuildRateFormula(BuildListVessel.ListType.VAB, index, this);
                if (rate >= 0)
                    VABRates.Add(rate);
                index++;
            }
            rate = 0.1;
            index = 0;
            while (rate > 0)
            {
                rate = MathParser.ParseBuildRateFormula(BuildListVessel.ListType.SPH, index, this);
                if (rate >= 0)
                    SPHRates.Add(rate);
                index++;
            }

            var m = StringBuilderCache.Acquire();
            m.AppendLine("VAB Rates:");
            foreach (double v in VABRates)
                m.AppendLine($"{v}");

            m.AppendLine("SPH Rates:");
            foreach (double v in SPHRates)
                m.AppendLine($"{v}");

            KCTDebug.Log(m.ToStringAndRelease());
        }

        public void RecalculateUpgradedBuildRates()
        {
            UpVABRates.Clear();
            UpSPHRates.Clear();
            double rate = 0.1;
            int index = 0;
            while (rate > 0)
            {
                rate = MathParser.ParseBuildRateFormula(BuildListVessel.ListType.VAB, index, this, true);
                if (rate >= 0 && (index == 0 || VABRates[index - 1] > 0))
                    UpVABRates.Add(rate);
                else
                    break;
                index++;
            }
            rate = 0.1;
            index = 0;
            while (rate > 0)
            {
                rate = MathParser.ParseBuildRateFormula(BuildListVessel.ListType.SPH, index, this, true);
                if (rate >= 0 && (index == 0 || SPHRates[index - 1] > 0))
                    UpSPHRates.Add(rate);
                else
                    break;
                index++;
            }
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

            LCName = newName;
            // TODO: do we need to rename vessels?
        }

        public void SwitchLaunchPad(int LP_ID = -1, bool updateDestrNode = true)
        {
            if (LP_ID < 0)
                LP_ID = ActiveLaunchPadID;

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
            return blv;
        }

        public ConfigNode AsConfigNode()
        {
            KCTDebug.Log("Saving LC " + LCName);
            var node = new ConfigNode("LaunchComplex");
            node.AddValue("LCName", LCName);
            node.AddValue("ActiveLPID", ActiveLaunchPadID);
            node.AddValue("operational", isOperational);
            node.AddValue("isPad", isPad);
            node.AddValue("massMax", massMax);
            node.AddValue("massMin", massMin);
            node.AddValue("sizeMax", sizeMax);

            var cnVABUp = new ConfigNode("VABUpgrades");
            foreach (int upgrade in VABUpgrades)
            {
                cnVABUp.AddValue("Upgrade", upgrade.ToString());
            }
            node.AddNode(cnVABUp);

            var cnSPHUp = new ConfigNode("SPHUpgrades");
            foreach (int upgrade in SPHUpgrades)
            {
                cnSPHUp.AddValue("Upgrade", upgrade.ToString());
            }
            node.AddNode(cnSPHUp);

            var cnVABl = new ConfigNode("VABList");
            foreach (BuildListVessel blv in VABList)
            {
                blv.BuildListIndex = BuildList.IndexOf(blv);
                BuildVesselAndShipNodeConfigs(blv, ref cnVABl);
            }
            node.AddNode(cnVABl);

            var cnSPHl = new ConfigNode("SPHList");
            foreach (BuildListVessel blv in SPHList)
            {
                blv.BuildListIndex = BuildList.IndexOf(blv);
                BuildVesselAndShipNodeConfigs(blv, ref cnSPHl);
            }
            node.AddNode(cnSPHl);

            var cnVABWh = new ConfigNode("VABWarehouse");
            foreach (BuildListVessel blv in VABWarehouse)
            {
                BuildVesselAndShipNodeConfigs(blv, ref cnVABWh);
            }
            node.AddNode(cnVABWh);

            var cnSPHWh = new ConfigNode("SPHWarehouse");
            foreach (BuildListVessel blv in SPHWarehouse)
            {
                BuildVesselAndShipNodeConfigs(blv, ref cnSPHWh);
            }
            node.AddNode(cnSPHWh);

            var cnPadConstructions = new ConfigNode("PadConstructions");
            foreach (PadConstruction pc in PadConstructions)
            {
                var storageItem = new PadConstructionStorageItem();
                storageItem.FromPadConstruction(pc);
                var cn = new ConfigNode("PadConstruction");
                cn = ConfigNode.CreateConfigFromObject(storageItem, cn);
                cnPadConstructions.AddNode(cn);
            }
            node.AddNode(cnPadConstructions);

            var cnVABPlans = new ConfigNode("VABPlans");
            foreach (BuildListVessel blv in VABPlans.Values)
            {
                BuildVesselAndShipNodeConfigs(blv, ref cnVABPlans);
            }
            node.AddNode(cnVABPlans);

            var cnSPHPlans = new ConfigNode("SPHPlans");
            foreach (BuildListVessel blv in SPHPlans.Values)
            {
                BuildVesselAndShipNodeConfigs(blv, ref cnSPHPlans);
            }
            node.AddNode(cnSPHPlans);

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

            //Cache the regular rates
            var cnCachedVABRates = new ConfigNode("VABRateCache");
            foreach (double rate in VABRates)
            {
                cnCachedVABRates.AddValue("rate", rate);
            }
            node.AddNode(cnCachedVABRates);

            var cnCachedSPHRates = new ConfigNode("SPHRateCache");
            foreach (double rate in SPHRates)
            {
                cnCachedSPHRates.AddValue("rate", rate);
            }
            node.AddNode(cnCachedSPHRates);
            return node;
        }

        public LCItem FromConfigNode(ConfigNode node)
        {
            VABUpgrades.Clear();
            SPHUpgrades.Clear();
            VABList.Clear();
            VABWarehouse.Clear();
            SPHList.Clear();
            SPHWarehouse.Clear();
            VABPlans.Clear();
            SPHPlans.Clear();
            PadConstructions.Clear();
            Recon_Rollout.Clear();
            AirlaunchPrep.Clear();
            VABRates.Clear();
            SPHRates.Clear();

            LCName = node.GetValue("LCName");
            ActiveLaunchPadID = 0;
            node.TryGetValue("ActiveLPID", ref ActiveLaunchPadID);
            node.TryGetValue("operational", ref isOperational);
            node.TryGetValue("isPad", ref isPad);
            node.TryGetValue("massMax", ref massMax);
            node.TryGetValue("massMin", ref massMin);
            node.TryGetValue("sizeMax", ref sizeMax);

            ConfigNode vabUp = node.GetNode("VABUpgrades");
            foreach (string upgrade in vabUp.GetValues("Upgrade"))
            {
                VABUpgrades.Add(int.Parse(upgrade));
            }
            ConfigNode sphUp = node.GetNode("SPHUpgrades");
            foreach (string upgrade in sphUp.GetValues("Upgrade"))
            {
                SPHUpgrades.Add(int.Parse(upgrade));
            }

            ConfigNode tmp = node.GetNode("VABList");
            foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
            {
                VABList.Add(CreateBLVFromNode(cn));
            }

            tmp = node.GetNode("SPHList");
            foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
            {
                SPHList.Add(CreateBLVFromNode(cn));
            }

            BuildList.Sort((a, b) => a.BuildListIndex.CompareTo(b.BuildListIndex));

            tmp = node.GetNode("VABWarehouse");
            foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
            {
                VABWarehouse.Add(CreateBLVFromNode(cn));
            }

            tmp = node.GetNode("SPHWarehouse");
            foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
            {
                SPHWarehouse.Add(CreateBLVFromNode(cn));
            }

            if (node.TryGetNode("VABPlans", ref tmp))
            {
                if (tmp.HasNode("KCTVessel"))
                    foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
                    {
                        var blv = CreateBLVFromNode(cn);
                        VABPlans.Remove(blv.ShipName); 
                        VABPlans.Add(blv.ShipName, blv);
                    }
            }

            if (node.TryGetNode("SPHPlans", ref tmp))
            {
                if (tmp.HasNode("KCTVessel"))
                    foreach (ConfigNode cn in tmp.GetNodes("KCTVessel"))
                    {
                        var blv = CreateBLVFromNode(cn);
                        SPHPlans.Remove(blv.ShipName);
                        SPHPlans.Add(blv.ShipName, blv);
                    }
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

            if (node.HasNode("LaunchPads"))
            {
                LaunchPads.Clear();
                tmp = node.GetNode("LaunchPads");
                foreach (ConfigNode cn in tmp.GetNodes("KCT_LaunchPad"))
                {
                    var tempLP = new KCT_LaunchPad("LP0");
                    ConfigNode.LoadObjectFromConfig(tempLP, cn);
                    tempLP.DestructionNode = cn.GetNode("DestructionState");
                    if (tempLP.fractionalLevel == -1) tempLP.MigrateFromOldState();
                    LaunchPads.Add(tempLP);
                }
            }

            if (node.HasNode("PadConstructions"))
            {
                tmp = node.GetNode("PadConstructions");
                foreach (ConfigNode cn in tmp.GetNodes("PadConstruction"))
                {
                    var storageItem = new PadConstructionStorageItem();
                    ConfigNode.LoadObjectFromConfig(storageItem, cn);
                    PadConstructions.Add(storageItem.ToPadConstruction());
                }
            }

            if (node.HasNode("VABRateCache"))
            {
                foreach (string rate in node.GetNode("VABRateCache").GetValues("rate"))
                {
                    if (double.TryParse(rate, out double r))
                    {
                        VABRates.Add(r);
                    }
                }
            }

            if (node.HasNode("SPHRateCache"))
            {
                foreach (string rate in node.GetNode("SPHRateCache").GetValues("rate"))
                {
                    if (double.TryParse(rate, out double r))
                    {
                        SPHRates.Add(r);
                    }
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
