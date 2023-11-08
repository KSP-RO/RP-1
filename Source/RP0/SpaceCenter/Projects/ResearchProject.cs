using System;
using System.Collections.Generic;
using UnityEngine;
using ROUtils.DataTypes;

namespace RP0
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
        FRSC = 1 << 12,
        AllStaged = Staged | FRSC,
        LiquidEngines = Hydrolox | RocketEngines | AllStaged,

        Solid = 1 << 13,
        NTR = 1 << 14,
        Ion = 1 << 15,
        Propulsion = LiquidEngines | NTR | Ion,
        
        LifeSupport = 1 << 16,

        Nuclear = 1 << 17,
        Power = 1 << 18,
        Electricity = Nuclear | Power,

        Comms = 1 << 19,

        Avionics = 1 << 20,

        Science = 1 << 21,

        Any = ~0,
    }

    public class TechPeriod : ConfigNodePersistenceBase
    {
        [Persistent] public string id;
        [Persistent] public int startYear;
        [Persistent] public int endYear;
    }

    public class ResearchProject : ISpaceCenterProject, IConfigNode
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
                if (Database.NodeTypes.TryGetValue(techID, out var type))
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
                    UpdateBuildRate(Math.Max(SpaceCenterManagement.Instance.TechList.IndexOf(this), 0));
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

        public ResearchProject(RDTech techNode)
        {
            scienceCost = techNode.scienceCost;
            techName = techNode.title;
            techID = techNode.techID;
            progress = 0;
            ProtoNode = new ProtoTechNode();
            ProtoNode.UpdateFromTechNode(techNode);
            // No need to feed this back into RnD yet--we'll do so on complete

            if (Database.TechNodePeriods.TryGetValue(techID, out TechPeriod period))
            {
                startYear = period.startYear;
                endYear = period.endYear;
            }

            RP0Debug.Log("techID = " + techID);
            RP0Debug.Log("TimeLeft = " + TimeLeft);
        }

        public ResearchProject() { }

        public override string ToString() => techID;

        public double UpdateBuildRate(int index)
        {
            ForceRecalculateYearBasedRateMult();
            double rate = Formula.GetResearchRate(scienceCost, index, 0) * KCTUtilities.GetResearcherEfficiencyMultipliers();
            if (rate < 0)
                rate = 0;

            if (rate != 0)
            {
                rate *= YearBasedRateMult;
                rate *= CurrencyUtils.Rate(TransactionReasonsRP0.RateResearch);
                rate *= Leaders.LeaderUtils.GetResearchRateEffect(nodeType, techID);
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
            if (startYear < 1d || Database.SettingsSC.YearBasedRateMult == null)
                return 1d;
            
            if (double.IsNaN(offset) || double.IsInfinity(offset) || offset * (1d / (86400d * 365.25d)) > 500d)
                return Database.SettingsSC.YearBasedRateMult.Evaluate(Database.SettingsSC.YearBasedRateMult.maxTime);

            DateTime curDate = _epoch.AddSeconds(Planetarium.GetUniversalTime() + offset);

            double diffYears = (curDate - new DateTime(startYear, 1, 1)).TotalDays / 365.25;
            if (diffYears > 0)
            {
                diffYears = (curDate - new DateTime(endYear, 12, 31, 23, 59, 59)).TotalDays / 365.25;
                diffYears = Math.Max(0, diffYears);
            }
            return Database.SettingsSC.YearBasedRateMult.Evaluate((float)diffYears);
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
            return SpaceCenterManagement.Instance.TechListHas(techID);
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
                double rate = Formula.GetResearchRate(scienceCost, 0, 0) * KCTUtilities.GetResearcherEfficiencyMultipliers();
                if (offset == 0d)
                    rate *= YearBasedRateMult;
                else
                    rate *= CalculateYearBasedRateMult(offset);

                rate *= CurrencyUtils.Rate(TransactionReasonsRP0.RateResearch);
                rate *= Leaders.LeaderUtils.GetResearchRateEffect(nodeType, techID);

                return (scienceCost - progress) / rate;
            }
        }

        public ProjectType GetProjectType() => ProjectType.TechNode;

        public bool IsComplete() => progress >= scienceCost;

        public double IncrementProgress(double UTDiff)
        {
            // Don't progress blocked items
            if (GetBlockingTech() != null)
                return 0d;

            double bR = BuildRate;
            if (bR == 0d && PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes)
                return 0d;

            double toGo = scienceCost - progress;
            double increment = bR * UTDiff;
            progress += increment;
            if (IsComplete() || !PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes)
            {
                if (ProtoNode == null)
                    return 0d;
                
                ProtoNode.state = RDTech.State.Available;
                if (ResearchAndDevelopment.Instance == null)
                    return 0d;

                ResearchAndDevelopment.Instance.SetTechState(techID, ProtoNode);

                // Shouldn't be needed - ProtoTechNode.UpdateFromTechNode(<RDTech>);
                // analytics doesn't make sense here - AnalyticsUtil.LogTechTreeNodeUnlocked(ProtoNode, ResearchAndDevelopment.Instance.Science);

                // Fire event with fake RDTech since we're outside the RnD screen
                GameObject go = new GameObject();
                try
                {
                    var rdt = go.AddComponent<RDTech>();
                    rdt.techID = techID;
                    rdt.techState = ProtoNode;
                    rdt.scienceCost = scienceCost;
                    rdt.Warmup(); // this will find speculative-level-hidden parts, but that's ok.
                    GameEvents.OnTechnologyResearched.Fire(new GameEvents.HostTargetAction<RDTech, RDTech.OperationResult>(rdt, RDTech.OperationResult.Successful));

                    // fire our own event
                    SCMEvents.OnTechCompleted.Fire(this);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                go.DestroyGameObject();

                SpaceCenterManagement.Instance.TechList.Remove(this);

                SpaceCenterManagement.Instance.RecalculateBuildRates(); // this might change other rates

                double portion = toGo / increment;
                UnlockCreditHandler.Instance.IncrementCreditTime(techID, portion * UTDiff);
                return (1d - portion) * UTDiff;
            }

            UnlockCreditHandler.Instance.IncrementCreditTime(techID, UTDiff);
            return 0d;
        }

        public string GetBlockingTech()
        {
            var techList = SpaceCenterManagement.Instance.TechList;
            string blockingTech = null;

            List<string> parentList;
            if (!Database.TechNameToParents.TryGetValue(techID, out parentList))
            {
                RP0Debug.LogError($"Could not find techToParent for tech {techID}");
                return null;
            }
            if (parentList == null)
                return null;

            foreach (var t in techList)
            {
                if (t == this)
                    break;

                if (parentList.Contains(t.techID))
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
