using System;
using System.Collections.Generic;
using UnityEngine;
using Upgradeables;

namespace KerbalConstructionTime
{
    public class FacilityUpgrade : IKCTBuildItem
    {
        public SpaceCenterFacility? FacilityType;
        public int UpgradeLevel, CurrentLevel, LaunchpadID = 0;
        public string Id, CommonName;
        public double Progress = 0, BP = 0, Cost = 0;
        public bool UpgradeProcessed = false, IsLaunchpad = false;

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
            if (IsLaunchpad)
            {
                KSC.LaunchPads[LaunchpadID].level = CurrentLevel;
                if (KCTGameStates.ActiveKSCName != KSC.KSCName || KCTGameStates.ActiveKSC.ActiveLaunchPadID != LaunchpadID)
                {
                    return;
                }
            }
            foreach (UpgradeableFacility facility in GetFacilityReferences())
            {
                KCTEvents.AllowedToUpgrade = true;
                facility.SetLevel(CurrentLevel);
            }
        }

        public void Upgrade()
        {
            KCTDebug.Log($"Upgrading {CommonName} to level {UpgradeLevel}");
            if (IsLaunchpad)
            {
                KSC.LaunchPads[LaunchpadID].level = UpgradeLevel;
                KSC.LaunchPads[LaunchpadID].DestructionNode = new ConfigNode("DestructionState");
                if (KCTGameStates.ActiveKSCName != KSC.KSCName || KCTGameStates.ActiveKSC.ActiveLaunchPadID != LaunchpadID)
                {
                    UpgradeProcessed = true;
                    return;
                }
                KSC.LaunchPads[LaunchpadID].Upgrade(UpgradeLevel);
            }
            KCTEvents.AllowedToUpgrade = true;
            foreach (UpgradeableFacility facility in GetFacilityReferences())
            {
                facility.SetLevel(UpgradeLevel);
            }
            int newLvl = Utilities.GetBuildingUpgradeLevel(Id);
            UpgradeProcessed = newLvl == UpgradeLevel;

            KCTDebug.Log($"Upgrade processed: {UpgradeProcessed} Current: {newLvl} Desired: {UpgradeLevel}");
        }

        public List<UpgradeableFacility> GetFacilityReferences()
        {
            return ScenarioUpgradeableFacilities.protoUpgradeables[Id].facilityRefs;
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
                    if (!IsLaunchpad)
                        _ksc = KCTGameStates.KSCs.Find(ksc => ksc.KSCTech.Find(ub => ub.Id == this.Id) != null);
                    else
                        _ksc = KCTGameStates.KSCs.Find(ksc => ksc.KSCTech.Find(ub => ub.Id == this.Id && ub.IsLaunchpad && ub.LaunchpadID == this.LaunchpadID) != null);
                }
                return _ksc;
            }
        }

        public string GetItemName() => CommonName;

        public double GetBuildRate()
        {
            double rateTotal = 0;
            if (KSC != null)
            {
                rateTotal = Utilities.GetBothBuildRateSum(KSC);
            }
            return rateTotal;
        }

        public double GetTimeLeft() => (BP - Progress) / ((IKCTBuildItem)this).GetBuildRate();

        public bool IsComplete() => Progress >= BP;

        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.KSC;

        public void IncrementProgress(double UTDiff)
        {
            if (!IsComplete()) AddProgress(GetBuildRate() * UTDiff);
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER && (IsComplete() || !PresetManager.Instance.ActivePreset.GeneralSettings.KSCUpgradeTimes))
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
            double rateTotal = Utilities.GetBothBuildRateSum(KSC ?? KCTGameStates.ActiveKSC);

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
