using System;
using System.Collections.Generic;
using UnityEngine;
using Upgradeables;

namespace KerbalConstructionTime
{
    public class FacilityUpgrade : IConstructionBuildItem
    {
        public SpaceCenterFacility? FacilityType;
        public int UpgradeLevel, CurrentLevel;
        public string Id, CommonName;
        public double Progress = 0, BP = 0, Cost = 0;
        public bool UpgradeProcessed = false;

        [Obsolete("Only used for migrating over to PadConstruction. Remove at a later date.")]
        public int LaunchpadID = 0;
        [Obsolete("Only used for migrating over to PadConstruction. Remove at a later date.")]
        public bool IsLaunchpad = false;

        public int BuildListIndex { get; set; }

        private double _buildRate = -1d;

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
                    double rate = Utilities.GetConstructionRate(KSC) * KCTGameStates.EfficiecnyEngineers;
                    return (BP - Progress) / rate;
                }
            }
        }

        private KSCItem _ksc = null;

        public FacilityUpgrade()
        {
        }

        public FacilityUpgrade(SpaceCenterFacility? type, string facilityID, int newLevel, int oldLevel, string name)
        {
            FacilityType = type;
            Id = facilityID;
            UpgradeLevel = newLevel;
            CurrentLevel = oldLevel;
            CommonName = name;

            KCTDebug.Log($"Upgrade of {name} requested from {oldLevel} to {newLevel}");
        }

        public void Downgrade()
        {
            KCTDebug.Log($"Downgrading {CommonName} to level {CurrentLevel}");
            foreach (UpgradeableFacility facility in GetFacilityReferencesById(Id))
            {
                KCTEvents.AllowedToUpgrade = true;
                facility.SetLevel(CurrentLevel);
            }
        }

        public void Upgrade()
        {
            KCTDebug.Log($"Upgrading {CommonName} to level {UpgradeLevel}");

            List<UpgradeableFacility> facilityRefs = GetFacilityReferencesById(Id);
            if (FacilityType == SpaceCenterFacility.VehicleAssemblyBuilding)
            {
                // Also upgrade the SPH to the same level as VAB when playing with unified build queue
                facilityRefs.AddRange(GetFacilityReferencesByType(SpaceCenterFacility.SpaceplaneHangar));
            }

            KCTEvents.AllowedToUpgrade = true;
            foreach (UpgradeableFacility facility in facilityRefs)
            {
                facility.SetLevel(UpgradeLevel);
            }

            int newLvl = Utilities.GetBuildingUpgradeLevel(Id);
            UpgradeProcessed = newLvl == UpgradeLevel;

            KCTDebug.Log($"Upgrade processed: {UpgradeProcessed} Current: {newLvl} Desired: {UpgradeLevel}");
        }

        public static List<UpgradeableFacility> GetFacilityReferencesById(string id)
        {
            return ScenarioUpgradeableFacilities.protoUpgradeables[id].facilityRefs;
        }

        public static List<UpgradeableFacility> GetFacilityReferencesByType(SpaceCenterFacility facilityType)
        {
            string internalId = ScenarioUpgradeableFacilities.SlashSanitize(facilityType.ToString());
            return GetFacilityReferencesById(internalId);
        }

        public void SetBP(double cost)
        {
            BP = CalculateBP(cost, FacilityType);
        }

        public bool AlreadyInProgress()
        {
            return KSC != null;
        }

        public KSCItem KSC
        {
            get
            {
                if (_ksc == null)
                {
                    _ksc = KCTGameStates.KSCs.Find(ksc => ksc.FacilityUpgrades.Find(ub => ub.Id == this.Id) != null);
                }
                return _ksc;
            }
        }

        public string GetItemName() => CommonName;

        public double GetBuildRate()
        {
            if (_buildRate < 0)
                UpdateBuildRate(KSC.Constructions.IndexOf(this));
            return _buildRate * KCTGameStates.EfficiecnyEngineers;
        }

        public double UpdateBuildRate(int index)
        {
            double rate = Utilities.GetConstructionRate(index, KSC, 0);
            if (rate < 0)
                rate = 0;

            _buildRate = rate;
            return _buildRate;
        }

        public double GetFractionComplete() => Progress / BP;

        public double GetTimeLeft() => (BP - Progress) / GetBuildRate();

        public bool IsComplete() => Progress >= BP;

        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.KSC;

        public void Cancel()
        {
            if (Cost > 0d && Utilities.CurrentGameIsCareer())
                Utilities.AddFunds(Cost, TransactionReasons.StructureConstruction);

            KSC.FacilityUpgrades.Remove(this);
            KSC.RecalculateBuildRates(false);
        }

        public void IncrementProgress(double UTDiff)
        {
            if (!IsComplete()) AddProgress(GetBuildRate() * UTDiff);
            if (IsComplete() || !PresetManager.Instance.ActivePreset.GeneralSettings.KSCUpgradeTimes)
            {
                if (ScenarioUpgradeableFacilities.Instance != null && !KCTGameStates.ErroredDuringOnLoad)
                {
                    Upgrade();

                    try
                    {
                        KCTEvents.OnFacilityUpgradeComplete?.Fire(this);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }

        public static double CalculateBP(double cost, SpaceCenterFacility? facilityType)
        {
            int isAdm = 0, isAC = 0, isLP = 0, isMC = 0, isRD = 0, isRW = 0, isTS = 0, isSPH = 0, isVAB = 0, isOther = 0;
            switch (facilityType)
            {
                case SpaceCenterFacility.Administration:
                    isAdm = 1;
                    break;
                case SpaceCenterFacility.AstronautComplex:
                    isAC = 1;
                    break;
                case SpaceCenterFacility.LaunchPad:
                    isLP = 1;
                    break;
                case SpaceCenterFacility.MissionControl:
                    isMC = 1;
                    break;
                case SpaceCenterFacility.ResearchAndDevelopment:
                    isRD = 1;
                    break;
                case SpaceCenterFacility.Runway:
                    isRW = 1;
                    break;
                case SpaceCenterFacility.TrackingStation:
                    isTS = 1;
                    break;
                case SpaceCenterFacility.SpaceplaneHangar:
                    isSPH = 1;
                    break;
                case SpaceCenterFacility.VehicleAssemblyBuilding:
                    isVAB = 1;
                    break;
                default:
                    isOther = 1;
                    break;
            }

            var variables = new Dictionary<string, string>()
            {
                { "C", cost.ToString() },
                { "O", PresetManager.Instance.ActivePreset.TimeSettings.OverallMultiplier.ToString() },
                { "Adm", isAdm.ToString() },
                { "AC", isAC.ToString() },
                { "LP", isLP.ToString() },
                { "MC", isMC.ToString() },
                { "RD", isRD.ToString() },
                { "RW", isRW.ToString() },
                { "TS", isTS.ToString() },
                { "SPH", isSPH.ToString() },
                { "VAB", isVAB.ToString() },
                { "Other", isOther.ToString() }
            };

            double bp = MathParser.GetStandardFormulaValue("KSCUpgrade", variables);
            if (bp <= 0) { bp = 1; }

            return bp;
        }

        public static double CalculateBuildTime(double cost, SpaceCenterFacility? facilityType, KSCItem KSC = null)
        {
            double bp = CalculateBP(cost, facilityType);
            double rateTotal = Utilities.GetConstructionRate(KSC) * KCTGameStates.EfficiecnyEngineers;

            return bp / rateTotal;
        }

        private void AddProgress(double amt)
        {
            Progress += amt;
            if (Progress > BP) Progress = BP;
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
