using System;
using System.Collections.Generic;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class PadConstruction : IConstructionBuildItem
    {
        /// <summary>
        /// Index of the pad in KSCItem.LaunchPads.
        /// </summary>
        public int LaunchpadIndex = 0;
        public double Progress = 0, BP = 0, Cost = 0;
        public double BuildPoints() => BP;
        public double CurrentProgress() => Progress;
        public string Name;
        public bool UpgradeProcessed = false;

        private double _buildRate = -1d;

        public int BuildListIndex { get; set; }

        public double EstimatedTimeLeft
        {
            get
            {
                if (_buildRate > 0)
                {
                    return GetTimeLeft();
                }
                else
                {
                    double rate = Utilities.GetConstructionRate(LC.KSC) * KCTGameStates.EfficiencyEngineers;
                    return (BP - Progress) / rate;
                }
            }
        }

        private LCItem _lc = null;

        public LCItem LC
        {
            get
            {
                if (_lc == null)
                {
                    foreach (var ksc in KCTGameStates.KSCs)
                    {
                        foreach (var lc in ksc.LaunchComplexes)
                        {
                            if (lc.PadConstructions.Contains(this))
                            {
                                _lc = lc;
                                break;
                            }
                        }
                    }
                }
                return _lc;
            }
        }

        public PadConstruction()
        {
        }

        public PadConstruction(string name)
        {
            Name = name;
        }

        public double GetBuildRate()
        {
            if (_buildRate < 0)
                UpdateBuildRate(LC.KSC.Constructions.IndexOf(this));
            return _buildRate * KCTGameStates.EfficiencyEngineers;
        }

        public double UpdateBuildRate(int index)
        {
            double rate = Utilities.GetConstructionRate(index, LC.KSC, 0);
            if (rate < 0)
                rate = 0;

            _buildRate = rate;
            return _buildRate;
        }

        public string GetItemName() => $"{LC.Name}: {Name}";

        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.KSC;

        public double GetFractionComplete() => Progress / BP;

        public double GetTimeLeft() => (BP - Progress) / GetBuildRate();
        public double GetTimeLeftEst(double offset) => EstimatedTimeLeft;

        public void Cancel()
        {
            if (Cost > 0d && Utilities.CurrentGameIsCareer())
                Utilities.AddFunds(Cost, TransactionReasons.StructureConstruction);

            KCT_LaunchPad lp = LC.LaunchPads[LaunchpadIndex];
            int index = LC.LaunchPads.IndexOf(lp);
            LC.LaunchPads.RemoveAt(index);
            if (LC.ActiveLaunchPadIndex >= index)
                --LC.ActiveLaunchPadIndex; // should not change active pad.

            LC.PadConstructions.Remove(this);
            LC.KSC.RecalculateBuildRates(false);
        }

        public void IncrementProgress(double UTDiff)
        {
            if (!IsComplete()) AddProgress(GetBuildRate() * UTDiff);
            if (IsComplete() || !PresetManager.Instance.ActivePreset.GeneralSettings.KSCUpgradeTimes)
            {
                if (ScenarioUpgradeableFacilities.Instance != null && !KCTGameStates.ErroredDuringOnLoad)
                {
                    KCT_LaunchPad lp = LC.LaunchPads[LaunchpadIndex];
                    lp.isOperational = true;
                    lp.DestructionNode = new ConfigNode("DestructionState");
                    UpgradeProcessed = true;

                    try
                    {
                        KCTEvents.OnPadConstructionComplete?.Fire(this, lp);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }

        public bool IsComplete() => Progress >= BP;

        public void SetBP(double cost)
        {
            BP = CalculateBP(cost);
        }

        public static double CalculateBP(double cost)
        {
            var variables = new Dictionary<string, string>()
            {
                { "C", cost.ToString() },
                { "O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString() },
                { "Adm", "0" },
                { "AC", "0" },
                { "LP", "1" },
                { "MC", "0" },
                { "RD", "0" },
                { "RW", "0" },
                { "TS", "0" },
                { "SPH", "0" },
                { "VAB", "0" },
                { "Other", "0" }
            };

            double bp = MathParser.GetStandardFormulaValue("KSCUpgrade", variables);
            if (bp <= 0) { bp = 1; }

            return bp;
        }

        private void AddProgress(double amt)
        {
            Progress += amt;
            if (Progress > BP) Progress = BP;
        }
    }
}
