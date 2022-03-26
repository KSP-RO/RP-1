using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class TechItem : IKCTBuildItem
    {
        private static readonly DateTime _epoch = new DateTime(1951, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public int ScienceCost;
        public int StartYear;
        public int EndYear;
        public string TechName, TechID;
        public double Progress;
        public ProtoTechNode ProtoNode;

        private double _buildRate = -1;
        private double _yearMult = -1;

        public double BuildRate
        {
            get
            {
                if (_buildRate < 0)
                {
                    UpdateBuildRate(Math.Max(KCTGameStates.TechList.IndexOf(this), 0));
                }
                return _buildRate;
            }
        }

        public double GetFractionComplete() => Progress / ScienceCost;

        public double TimeLeft => (ScienceCost - Progress) / BuildRate;

        public double EstimatedTimeLeft
        {
            get
            {
                if (BuildRate > 0)
                {
                    return TimeLeft;
                }
                else
                {
                    double rate = MathParser.ParseNodeRateFormula(ScienceCost, 0, 0) * KCTGameStates.EfficiencyResearchers;
                    rate *= YearBasedRateMult;
                    return (ScienceCost - Progress) / rate;
                }
            }
        }

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
            ScienceCost = techNode.scienceCost;
            TechName = techNode.title;
            TechID = techNode.techID;
            Progress = 0;
            ProtoNode = ResearchAndDevelopment.Instance.GetTechState(TechID);

            if (KerbalConstructionTime.TechNodePeriods.TryGetValue(TechID, out KCTTechNodePeriod period))
            {
                StartYear = period.startYear;
                EndYear = period.endYear;
            }

            KCTDebug.Log("techID = " + TechID);
            KCTDebug.Log("TimeLeft = " + TimeLeft);
        }

        public TechItem() {}

        public TechItem(string ID, string name, double prog, int sci, int startYear, int endYear)
        {
            TechID = ID;
            TechName = name;
            Progress = prog;
            ScienceCost = sci;
            StartYear = startYear;
            EndYear = endYear;
        }

        public double UpdateBuildRate(int index)
        {
            ForceRecalculateYearBasedRateMult();
            double rate = MathParser.ParseNodeRateFormula(ScienceCost, index, 0) * KCTGameStates.EfficiencyResearchers;
            if (rate < 0)
                rate = 0;

            if (rate != 0)
                rate *= YearBasedRateMult;

            _buildRate = rate;
            return _buildRate;
        }

        public void ForceRecalculateYearBasedRateMult()
        {
            _yearMult = -1;
        }

        public double CalculateYearBasedRateMult()
        {
            if (StartYear < 1) return 1;

            DateTime curDate = _epoch.AddSeconds(Utilities.GetUT());

            double diffYears = (curDate - new DateTime(StartYear, 1, 1)).TotalDays / 365.25;
            if (diffYears > 0)
            {
                diffYears = (curDate - new DateTime(EndYear, 12, 31, 23, 59, 59)).TotalDays / 365.25;
                diffYears = Math.Max(0, diffYears);
            }
            float? v = PresetManager.Instance.ActivePreset.FormulaSettings.YearBasedRateMult?.Evaluate((float)diffYears);
            return v ?? 1;
        }

        public void DisableTech()
        {
            ProtoNode.state = RDTech.State.Unavailable;
            ResearchAndDevelopment.Instance.SetTechState(TechID, ProtoNode);
        }

        public void EnableTech()
        {
            ProtoNode.state = RDTech.State.Available;
            ResearchAndDevelopment.Instance.SetTechState(TechID, ProtoNode);
        }

        public bool IsInList()
        {
            return KCTGameStates.TechList.FirstOrDefault(t => t.TechID == TechID) != null;
        }

        public string GetItemName() => TechName;

        public double GetBuildRate() => BuildRate;

        public double GetTimeLeft() => TimeLeft;
        public double GetTimeLeftEst() => EstimatedTimeLeft;

        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.TechNode;

        public bool IsComplete() => Progress >= ScienceCost;

        public void IncrementProgress(double UTDiff)
        {
            // Don't progress blocked items
            if (GetBlockingTech(KCTGameStates.TechList) != null)
                return;

            Progress += BuildRate * UTDiff;
            if (IsComplete() || !PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes)
            {
                if (ProtoNode == null) return;
                EnableTech();

                try
                {
                    KCTEvents.OnTechCompleted?.Fire(this);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                KCTGameStates.TechList.Remove(this);

                for (int j = 0; j < KCTGameStates.TechList.Count; j++)
                    KCTGameStates.TechList[j].UpdateBuildRate(j);
            }
        }

        public string GetBlockingTech(KCTObservableList<TechItem> techList)
        {
            string blockingTech = null;
            List<string> parentList = KerbalConstructionTimeData.techNameToParents[TechID];

            foreach (var t in techList)
            {
                if (parentList != null && parentList.Contains(t.TechID))
                {
                    blockingTech = t.TechName;
                    break;
                }
            }

            return blockingTech;
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
