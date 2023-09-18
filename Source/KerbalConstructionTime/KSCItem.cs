using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public class KSCItem : IConfigNode
    {
        [Persistent]
        public string KSCName;
        [Persistent]
        public int Engineers = 0;
        public int UnassignedEngineers => Engineers - LaunchComplexes.Sum(lc => lc.Engineers);
        [Persistent]
        public int ActiveLaunchComplexIndex = 0;

        [Persistent]
        public PersistentList<LCItem> LaunchComplexes = new PersistentList<LCItem>();
        [Persistent]
        public KCTObservableList<LCConstruction> LCConstructions = new KCTObservableList<LCConstruction>();
        [Persistent]
        public KCTObservableList<FacilityUpgrade> FacilityUpgrades = new KCTObservableList<FacilityUpgrade>();
                

        public List<ConstructionBuildItem> Constructions = new List<ConstructionBuildItem>();

        private bool _allowRecalcConstructions = true;

        public const int HangarIndex = 0;
        public LCItem Hangar => LaunchComplexes[HangarIndex];

        void added(int idx, ConstructionBuildItem item) { Constructions.Add(item); }
        void removed(int idx, ConstructionBuildItem item) { Constructions.Remove(item); }
        void updated()
        {
            if (_allowRecalcConstructions) RecalculateBuildRates(false);
            RP0.MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate();
        }

        private void AddListeners()
        {
            LCConstructions.Added += added;
            LCConstructions.Removed += removed;
            LCConstructions.Updated += updated;
            FacilityUpgrades.Added += added;
            FacilityUpgrades.Removed += removed;
            FacilityUpgrades.Updated += updated;
        }

        public KSCItem()
        {
            AddListeners();
        }

        public KSCItem(string name)
        {
            KSCName = name;

            AddListeners();
        }

        public LCItem ActiveLaunchComplexInstance => LaunchComplexes.Count > ActiveLaunchComplexIndex ? LaunchComplexes[ActiveLaunchComplexIndex] : null;

        public int LaunchComplexCount
        {
            get
            {
                int count = 0;
                foreach (LCItem lc in LaunchComplexes)
                    if (lc.IsOperational) 
                        ++count;
                return count;
            }
        }

        public int LaunchComplexCountPad
        {
            get
            {
                int count = 0;
                foreach (LCItem lc in LaunchComplexes)
                    if (lc.IsOperational && lc.LCType == LaunchComplexType.Pad)
                        ++count;
                return count;
            }
        }

        public bool IsAnyLCOperational
        {
            get
            {
                {
                    for (int i = LaunchComplexes.Count; i-- > 1;)
                        if (LaunchComplexes[i].IsOperational)
                            return true;

                    return false;
                }
            }
        }

        public bool IsEmpty => !FacilityUpgrades.Any() && !LCConstructions.Any() && LaunchComplexes.Count == 1 && Hangar.IsEmpty;

        public override string ToString() => KSCName;

        public void EnsureStartingLaunchComplexes()
        {
            if (LaunchComplexes.Count > 0) return;

            LCItem sph = new LCItem(LCData.StartingHangar, this);
            sph.IsOperational = true;
            LaunchComplexes.Add(sph);
        }

        public void RecalculateBuildRates(bool all = true)
        {
            if(all)
                foreach (LCItem lc in LaunchComplexes)
                    lc.RecalculateBuildRates();

            for (int j = 0; j < Constructions.Count; j++)
                Constructions[j].UpdateBuildRate(j);
        }

        public void SwitchToPrevLaunchComplex(bool padOnly = false) => SwitchLaunchComplex(false, padOnly);
        public void SwitchToNextLaunchComplex(bool padOnly = false) => SwitchLaunchComplex(true, padOnly);

        public int SwitchLaunchComplex(bool forwardDirection, bool padOnly, int startIndex = -1)
        {
            if (LaunchComplexCount < 2) return startIndex < 0 ? ActiveLaunchComplexIndex : startIndex;

            startIndex = GetLaunchComplexIdxToSwitchTo(forwardDirection, padOnly, startIndex);
            SwitchLaunchComplex(startIndex);

            return startIndex;
        }

        public void SwitchLaunchComplex(int LC_index, bool updateDestrNode = true)
        {
            if (LC_index < 0)
            {
                LC_index = ActiveLaunchComplexIndex;
            }
            else
            {
                if (LC_index == ActiveLaunchComplexIndex)
                    return;

                ActiveLaunchComplexIndex = LC_index;
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                if (!KCTGameStates.EditorShipEditingMode)
                    KerbalConstructionTime.Instance.EditorVessel.LCID = KCTGameStates.ActiveKSC.ActiveLaunchComplexInstance.ID;
                KerbalConstructionTime.Instance.StartCoroutine(CallbackUtil.DelayedCallback(0.02f, RP0.Harmony.PatchEngineersReport.UpdateCraftStats));
            }

            LaunchComplexes[LC_index].SwitchLaunchPad();
        }

        public int GetLaunchComplexIdxToSwitchTo(bool forwardDirection, bool padOnly, int startIndex = -1)
        {
            if (LaunchComplexCount < 2) return startIndex < 0 ? ActiveLaunchComplexIndex : startIndex;

            if (startIndex < 0)
                startIndex = ActiveLaunchComplexIndex;

            LCItem lc;
            int count = LaunchComplexes.Count;
            do
            {
                if (forwardDirection)
                {
                    ++startIndex;
                    if (startIndex == count)
                        startIndex = 0;
                }
                else
                {
                    if (startIndex == 0)
                        startIndex = count;
                    --startIndex;
                }
                lc = LaunchComplexes[startIndex];
            } while (!lc.IsOperational || (padOnly && lc.LCType != LaunchComplexType.Pad));

            return startIndex;
        }

        public bool SwitchToLaunchComplex(System.Guid lcid, bool skipDisabled = true)
        {
            for (int i = 0; i < LaunchComplexes.Count; ++i)
            {
                if (!LaunchComplexes[i].IsOperational && skipDisabled)
                    continue;

                if (LaunchComplexes[i].ID == lcid)
                {
                    SwitchLaunchComplex(i);
                    return true;
                }
            }

            return false;
        }

        public void Save(ConfigNode node)
        {
            KCTDebug.Log("Saving KSC " + KSCName);
            ConfigNode.CreateConfigFromObject(this, node);
        }

        public void Load(ConfigNode node)
        {
            _allowRecalcConstructions = false;
            ConfigNode.LoadObjectFromConfig(this, node);
            foreach (var lc in LaunchComplexes)
            {
                // This will link to us
                // and add the padconstructions
                lc.PostLoad(this);
            }
            _allowRecalcConstructions = true;
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
