using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace KerbalConstructionTime
{
    [Flags]
    public enum NodeType
    {
        None = 0,

        Materials = 1 << 0,
        Electronics = 1 << 2,
        Bluesky = Materials | Electronics,

        Flight = 1 << 3,
        Spaceplanes = 1 << 4,
        Aerospace = Flight | Spaceplanes,

        Command = 1 << 5,
        Stations = 1 << 6,
        HSF = Command | Stations,

        Crewed = Aerospace | HSF,

        RCS = 1 << 7,

        EDL = 1 << 8,

        Hydrolox = 1 << 9,
        RocketEngines = 1 << 10,
        Staged = 1 << 11,
        LiquidEngines = Hydrolox | RocketEngines | Staged,

        Solid = 1 << 12,
        NTR = 1 << 14,
        Ion = 1 << 13,
        Propulsion = LiquidEngines | NTR | Ion,
        
        LifeSupport = 1 << 15,

        Nuclear = 1 << 16,
        Power = 1 << 17,
        Electricity = Nuclear | Power,

        Comms = 1 << 18,

        Avionics = 1 << 19,

        Science = 1 << 20,

        Any = ~0,
    }

    public class TechItem : IKCTBuildItem, IConfigNode
    {
        private static readonly DateTime _epoch = new DateTime(1951, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [Persistent]
        public int scienceCost;
        [Persistent]
        public int startYear;
        [Persistent]
        public int endYear;
        [Persistent]
        public string techName;
        [Persistent]
        public string techID;
        [Persistent]
        public double progress;
        public ProtoTechNode ProtoNode;

        protected NodeType nodeType
        {
            get
            {
                if (KerbalConstructionTime.NodeTypes.TryGetValue(techID, out var type))
                    return type;

                return NodeType.None;
            }
        }

        private double _buildRate = -1;
        private double _yearMult = -1;

        public double BuildRate
        {
            get
            {
                if (_buildRate < 0)
                {
                    UpdateBuildRate(Math.Max(KerbalConstructionTimeData.Instance.TechList.IndexOf(this), 0));
                }
                return _buildRate;
            }
        }

        public double GetFractionComplete() => progress / scienceCost;

        public double TimeLeft => (scienceCost - progress) / BuildRate;

        public double YearBasedRateMult
        {
            get
            {
                if (_yearMult < 0)
                {
                    _yearMult = CalculateYearBasedRateMult();
                }
                return _yearMult;
            }
        }

        public TechItem(RDTech techNode)
        {
            scienceCost = techNode.scienceCost;
            techName = techNode.title;
            techID = techNode.techID;
            progress = 0;
            ProtoNode = ResearchAndDevelopment.Instance.GetTechState(techID);

            if (KerbalConstructionTime.TechNodePeriods.TryGetValue(techID, out KCTTechNodePeriod period))
            {
                startYear = period.startYear;
                endYear = period.endYear;
            }

            KCTDebug.Log("techID = " + techID);
            KCTDebug.Log("TimeLeft = " + TimeLeft);
        }

        public TechItem() {}

        public double UpdateBuildRate(int index)
        {
            ForceRecalculateYearBasedRateMult();
            double rate = Formula.GetResearchRate(scienceCost, index, 0) * Utilities.GetResearcherEfficiencyMultipliers();
            if (rate < 0)
                rate = 0;

            if (rate != 0)
            {
                rate *= YearBasedRateMult;
                rate *= RP0.CurrencyUtils.Rate(RP0.TransactionReasonsRP0.RateResearch);
                rate *= RP0.Leaders.LeaderUtils.GetResearchRateEffect(nodeType, techID);
            }

            _buildRate = rate;
            return _buildRate;
        }

        public void ForceRecalculateYearBasedRateMult()
        {
            _yearMult = -1;
        }

        public double CalculateYearBasedRateMult(double offset = 0)
        {
            if (startYear < 1d || PresetManager.Instance.ActivePreset.GeneralSettings.YearBasedRateMult == null)
                return 1d;
            
            if (double.IsNaN(offset) || double.IsInfinity(offset) || offset * (1d / (86400d * 365.25d)) > 500d)
                return PresetManager.Instance.ActivePreset.GeneralSettings.YearBasedRateMult.Evaluate(PresetManager.Instance.ActivePreset.GeneralSettings.YearBasedRateMult.maxTime);

            DateTime curDate = _epoch.AddSeconds(Utilities.GetUT() + offset);

            double diffYears = (curDate - new DateTime(startYear, 1, 1)).TotalDays / 365.25;
            if (diffYears > 0)
            {
                diffYears = (curDate - new DateTime(endYear, 12, 31, 23, 59, 59)).TotalDays / 365.25;
                diffYears = Math.Max(0, diffYears);
            }
            return PresetManager.Instance.ActivePreset.GeneralSettings.YearBasedRateMult.Evaluate((float)diffYears);
        }

        public void DisableTech()
        {
            ProtoNode.state = RDTech.State.Unavailable;
            ResearchAndDevelopment.Instance.SetTechState(techID, ProtoNode);
        }

        public void EnableTech()
        {
            ProtoNode.state = RDTech.State.Available;
            ResearchAndDevelopment.Instance.SetTechState(techID, ProtoNode);
        }

        public bool IsInList()
        {
            return KerbalConstructionTimeData.Instance.TechList.FirstOrDefault(t => t.techID == techID) != null;
        }

        public string GetItemName() => techName;

        public double GetBuildRate() => BuildRate;

        public double GetTimeLeft() => TimeLeft;
        public double GetTimeLeftEst(double offset)
        {
            if (BuildRate > 0)
            {
                return TimeLeft;
            }
            else
            {
                double rate = Formula.GetResearchRate(scienceCost, 0, 0) * Utilities.GetResearcherEfficiencyMultipliers();
                if (offset == 0d)
                    rate *= YearBasedRateMult;
                else
                    rate *= CalculateYearBasedRateMult(offset);
                return (scienceCost - progress) / rate;
            }
        }

        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.TechNode;

        public bool IsComplete() => progress >= scienceCost;

        public double IncrementProgress(double UTDiff)
        {
            // Don't progress blocked items
            if (GetBlockingTech(KerbalConstructionTimeData.Instance.TechList) != null)
                return 0d;

            double bR = BuildRate;
            if (bR == 0d && PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes)
                return 0d;

            double toGo = scienceCost - progress;
            double increment = bR * UTDiff;
            progress += increment;
            if (IsComplete() || !PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes)
            {
                if (ProtoNode == null) return 0d;
                EnableTech();

                try
                {
                    KCTEvents.OnTechCompleted.Fire(this);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                KerbalConstructionTimeData.Instance.TechList.Remove(this);

                KCTGameStates.RecalculateBuildRates(); // this might change other rates

                double portion = toGo / increment;
                RP0.UnlockSubsidyHandler.Instance.IncrementSubsidyTime(techID, portion * UTDiff);
                return (1d - portion) * UTDiff;
            }

            RP0.UnlockSubsidyHandler.Instance.IncrementSubsidyTime(techID, UTDiff);
            return 0d;
        }

        public string GetBlockingTech(KCTObservableList<TechItem> techList)
        {
            string blockingTech = null;

            List<string> parentList;
            if (!KerbalConstructionTimeData.techNameToParents.TryGetValue(techID, out parentList))
            {
                Debug.LogError($"[KCT] Could not find techToParent for tech {techID}");
                return null;
            }

            foreach (var t in techList)
            {
                if (parentList != null && parentList.Contains(t.techID))
                {
                    blockingTech = t.techName;
                    break;
                }
            }

            return blockingTech;
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
            ProtoNode = new ProtoTechNode(node.GetNode("ProtoNode"));
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
            var protoNode = new ConfigNode("ProtoNode");
            ProtoNode.Save(protoNode);
            node.AddNode(protoNode);
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
