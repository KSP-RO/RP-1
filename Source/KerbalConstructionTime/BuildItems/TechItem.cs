using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class TechItem : IKCTBuildItem
    {
        public int ScienceCost;
        public string TechName, TechID;
        public double Progress;
        public ProtoTechNode ProtoNode;

        private double _buildRate = -1;

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
                    double rate = MathParser.ParseNodeRateFormula(ScienceCost, 0);
                    return (ScienceCost - Progress) / rate;
                }
            }
        }

        public TechItem(RDTech techNode)
        {
            ScienceCost = techNode.scienceCost;
            TechName = techNode.title;
            TechID = techNode.techID;
            Progress = 0;
            ProtoNode = ResearchAndDevelopment.Instance.GetTechState(TechID);

            KCTDebug.Log("techID = " + TechID);
            KCTDebug.Log("TimeLeft = " + TimeLeft);
        }

        public TechItem() {}

        public TechItem(string ID, string name, double prog, int sci)
        {
            TechID = ID;
            TechName = name;
            Progress = prog;
            ScienceCost = sci;
        }

        public double UpdateBuildRate(int index)
        {
            double rate = MathParser.ParseNodeRateFormula(ScienceCost, index);
            if (rate < 0)
                rate = 0;
            _buildRate = rate;
            return _buildRate;
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
                    KCTEvents.OnTechCompleted?.Fire(ProtoNode);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                KCTGameStates.TechList.Remove(this);
                if (PresetManager.PresetLoaded() && PresetManager.Instance.ActivePreset.GeneralSettings.TechUpgrades)
                    KCTGameStates.MiscellaneousTempUpgrades++;

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
