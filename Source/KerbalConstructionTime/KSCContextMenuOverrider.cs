using KSP.UI;
using KSP.UI.Screens;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Upgradeables;

namespace KerbalConstructionTime
{
    /// <summary>
    /// This class attempts to override the KSC Upgrade buttons so that KCT can implement it's own form of KSC upgrading
    /// </summary>
    public class KSCContextMenuOverrider
    {
        private static Dictionary<string, Dictionary<int, string>> _techGatings = null;

        private readonly KSCFacilityContextMenu _menu;

        public static bool AreTextsUpdated { get; set; } = false;

        public KSCContextMenuOverrider(KSCFacilityContextMenu menu)
        {
            _menu = menu;

            if (!AreTextsUpdated)
            {
                AreTextsUpdated = OverrideFacilityDescriptions();
            }
        }

        public IEnumerator OnContextMenuSpawn()
        {
            yield return new WaitForFixedUpdate();
            if (PresetManager.Instance.ActivePreset.GeneralSettings.KSCUpgradeTimes && _menu != null)
            {
                SpaceCenterBuilding hostBuilding = GetMember<SpaceCenterBuilding>("host");
                KCTDebug.Log($"Trying to override upgrade button of menu for {hostBuilding.facilityName}");
                Button button = GetMember<Button>("UpgradeButton");
                if (button == null)
                {
                    KCTDebug.Log("Could not find UpgradeButton by name, using index instead.");
                    button = GetMember<Button>(2);
                }

                if (button != null)
                {
                    KCTDebug.Log("Found upgrade button, overriding it.");
                    button.onClick = new Button.ButtonClickedEvent();    //Clear existing KSP listener
                    button.onClick.AddListener(HandleUpgrade);

                    if ((PresetManager.Instance.ActivePreset.GeneralSettings.DisableLPUpgrades &&
                         GetFacilityID().IndexOf("launchpad", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (PresetManager.Instance.ActivePreset.GeneralSettings.CommonBuildLine &&
                         GetFacilityID().IndexOf("SpaceplaneHangar", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        button.interactable = false;
                        var hov = button.gameObject.GetComponent<UIOnHover>();
                        hov.gameObject.DestroyGameObject();
                    }
                }
                else
                {
                    throw new Exception("UpgradeButton not found. Cannot override.");
                }
            }
        }

        private bool OverrideFacilityDescriptions()
        {
            if (ScenarioUpgradeableFacilities.Instance == null)
                return false;

            try
            {
                Dictionary<string, ScenarioUpgradeableFacilities.ProtoUpgradeable> upgrades = ScenarioUpgradeableFacilities.protoUpgradeables;
                foreach (ScenarioUpgradeableFacilities.ProtoUpgradeable upgrade in upgrades.Values)
                {
                    foreach (UpgradeableFacility facilityUpgrade in upgrade.facilityRefs)
                    {
                        for (int i = 0; i < facilityUpgrade.UpgradeLevels.Length; i++)
                        {
                            UpgradeableObject.UpgradeLevel upgradeLevel = facilityUpgrade.UpgradeLevels[i];
                            UpdateFacilityLevelStats(upgradeLevel, i);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }

            return true;
        }

        private void UpdateFacilityLevelStats(UpgradeableObject.UpgradeLevel lvl, int lvlIdx)
        {
            // levelText appears to be unused by KSP itself. We can use it to store original level stats.
            // Restoring old values is necessary because those persist between scene changes but some of them are based on current KCT settings.
            if (lvl.levelText == null)
            {
                lvl.levelText = ScriptableObject.CreateInstance<KSCUpgradeableLevelText>();
                lvl.levelText.facility = lvl.levelStats.facility;
                lvl.levelText.linePrefix = lvl.levelStats.linePrefix;
                lvl.levelText.textBase = lvl.levelStats.textBase;
            }
            else
            {
                lvl.levelStats.textBase = lvl.levelText.textBase;
            }

            KCTDebug.Log($"Overriding level stats text for {lvl.levelStats.facility} lvl {lvlIdx}");

            SpaceCenterFacility facilityType = lvl.levelStats.facility;
            if (facilityType == SpaceCenterFacility.VehicleAssemblyBuilding ||
                facilityType == SpaceCenterFacility.SpaceplaneHangar)
            {
                if (PresetManager.Instance.ActivePreset.GeneralSettings.CommonBuildLine &&
                    facilityType == SpaceCenterFacility.SpaceplaneHangar)
                {
                    lvl.levelStats.linePrefix = string.Empty;
                    lvl.levelStats.textBase = "Upgrade the VAB instead";
                }
                else
                {
                    lvl.levelStats.textBase += $"\n{lvlIdx + 1} build queue{(lvlIdx > 0 ? "s" : string.Empty)}";
                    if (lvlIdx > 0)
                        lvl.levelStats.textBase += $"\n+{lvlIdx * 25}% build rate";
                }
            }
            else if (facilityType == SpaceCenterFacility.ResearchAndDevelopment &&
                     lvlIdx > 0)
            {
                lvl.levelStats.textBase += $"\n+{lvlIdx * 25}% research rate";
            }
            else if (facilityType == SpaceCenterFacility.Administration)
            {
                lvl.levelStats.linePrefix = string.Empty;
                lvl.levelStats.textBase = "This facility is currently unused";
            }
            else if (facilityType == SpaceCenterFacility.AstronautComplex &&
                     lvlIdx > 0)
            {
                lvl.levelStats.textBase += $"\n{lvlIdx * 25}% shorter R&R times";
                lvl.levelStats.textBase += $"\n{lvlIdx * 25}% shorter training times";
            }
            else if (facilityType == SpaceCenterFacility.LaunchPad &&
                     PresetManager.Instance.ActivePreset.GeneralSettings.DisableLPUpgrades)
            {
                lvl.levelStats.linePrefix = string.Empty;
                lvl.levelStats.textBase = "<color=\"red\"><b>Launchpads cannot be upgraded. Build a new launchpad from the KCT Build List tab instead.</b></color>";
            }
        }

        internal T GetMember<T>(string name)
        {
            MemberInfo member = _menu.GetType().GetMember(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)?.FirstOrDefault();
            if (member == null)
            {
                KCTDebug.Log($"Member was null when trying to find '{name}'", true);
                return default;
            }
            object o = Utilities.GetMemberInfoValue(member, _menu);
            if (o is T)
            {
                return (T)o;
            }
            return default;
        }

        internal T GetMember<T>(int index)
        {
            IEnumerable<MemberInfo> memberList = _menu.GetType().GetMembers(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy).Where(m => m.ToString().Contains(typeof(T).ToString()));
            KCTDebug.Log($"Found {memberList.Count()} matches for {typeof(T)}");
            MemberInfo member = memberList.Count() >= index ? memberList.ElementAt(index) : null;
            if (member == null)
            {
                KCTDebug.Log($"Member was null when trying to find element at index {index} for type '{typeof(T)}'", true);
                return default;
            }
            object o = Utilities.GetMemberInfoValue(member, _menu);
            if (o is T)
            {
                return (T)o;
            }
            return default;
        }

        protected static void CheckLoadDict()
        {
            if (_techGatings != null)
                return;

            _techGatings = new Dictionary<string, Dictionary<int, string>>();
            ConfigNode node = null;
            foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("KCTBUILDINGTECHS"))
                node = n;

            if (node == null)
                return;

            foreach (ConfigNode n in node.nodes)
            {
                string fac = "SpaceCenter/" + n.name;
                var lst = new Dictionary<int, string>();

                foreach (ConfigNode.Value v in n.values)
                    lst.Add(int.Parse(v.name), v.value);

                _techGatings.Add(fac, lst);
            }
        }

        protected string GetTechGate(string facId, int level)
        {
            CheckLoadDict();
            if (_techGatings == null)
                return string.Empty;

            if (_techGatings.TryGetValue(facId, out var d))
                if (d.TryGetValue(level, out string node))
                    return node;

            return string.Empty;
        }

        internal void ProcessUpgrade()
        {
            int oldLevel = GetMember<int>("level");
            KCTDebug.Log($"Upgrading from level {oldLevel}");

            string facilityID = GetFacilityID();
            SpaceCenterFacility? facilityType = GetFacilityType();

            string gate = GetTechGate(facilityID, oldLevel + 1);
            KCTDebug.Log($"Gate for {facilityID}: {gate}");
            if (!string.IsNullOrEmpty(gate))
            {
                if (ResearchAndDevelopment.GetTechnologyState(gate) != RDTech.State.Available)
                {
                    PopupDialog.SpawnPopupDialog(new MultiOptionDialog("kctUpgradePadConfirm",
                            $"Can't upgrade this facility. Requires {KerbalConstructionTimeData.techNameToTitle[gate]}.",
                            "Lack Tech to Upgrade",
                            HighLogic.UISkin,
                            new DialogGUIButton("Ok", () => { })),
                            false,
                            HighLogic.UISkin);

                    return;
                }
            }

            var upgrading = new FacilityUpgrade(facilityType, facilityID, oldLevel + 1, oldLevel, facilityID.Split('/').Last())
            {
                IsLaunchpad = facilityID.ToLower().Contains("launchpad")
            };

            if (upgrading.IsLaunchpad)
            {
                upgrading.LaunchpadID = KCTGameStates.ActiveKSC.ActiveLaunchPadID;
                if (upgrading.LaunchpadID > 0)
                    upgrading.CommonName += KCTGameStates.ActiveKSC.ActiveLPInstance.name;
            }

            if (!upgrading.AlreadyInProgress())
            {
                float cost = GetMember<float>("upgradeCost");

                if (Funding.CanAfford(cost))
                {
                    Utilities.SpendFunds(cost, TransactionReasons.Structures);
                    KCTGameStates.ActiveKSC.KSCTech.Add(upgrading);
                    upgrading.SetBP(cost);
                    upgrading.Cost = cost;

                    try
                    {
                        KCTEvents.OnFacilityUpgradeQueued?.Fire(upgrading);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }

                    ScreenMessages.PostScreenMessage("Facility upgrade requested!", 4f, ScreenMessageStyle.UPPER_CENTER);
                    KCTDebug.Log($"Facility {facilityID} upgrade requested to lvl {oldLevel + 1} for {cost} funds, resulting in a BP of {upgrading.BP}");
                }
                else
                {
                    KCTDebug.Log("Couldn't afford to upgrade.");
                    ScreenMessages.PostScreenMessage("Not enough funds to upgrade facility!", 4f, ScreenMessageStyle.UPPER_CENTER);
                }
            }
            else if (oldLevel + 1 != upgrading.CurrentLevel)
            {
                ScreenMessages.PostScreenMessage("Facility is already being upgraded!", 4f, ScreenMessageStyle.UPPER_CENTER);
                KCTDebug.Log($"Facility {facilityID} tried to upgrade to lvl {oldLevel + 1} but already in list!");
            }
        }

        internal void HandleUpgrade()
        {
            if (GetFacilityID().ToLower().Contains("launchpad"))
            {
                PopupDialog.SpawnPopupDialog(new MultiOptionDialog("kctUpgradePadConfirm",
                            "Upgrading this launchpad will render it unusable until the upgrade finishes.\n\nAre you sure you want to?",
                            "Upgrade Launchpad?",
                            HighLogic.UISkin,
                            new DialogGUIButton("Yes", ProcessUpgrade),
                            new DialogGUIButton("No", () => { })),
                            false,
                            HighLogic.UISkin);
            }
            else
                ProcessUpgrade();

            _menu.Dismiss(KSCFacilityContextMenu.DismissAction.None);
        }

        public string GetFacilityID()
        {
            return GetMember<SpaceCenterBuilding>("host").Facility.id;
        }

        public SpaceCenterFacility? GetFacilityType()
        {
            var scb = GetMember<SpaceCenterBuilding>("host");
            if (scb is AdministrationFacility) return SpaceCenterFacility.Administration;
            if (scb is AstronautComplexFacility) return SpaceCenterFacility.AstronautComplex;
            if (scb is LaunchSiteFacility lpFacility && lpFacility.facilityType == EditorFacility.VAB) return SpaceCenterFacility.LaunchPad;
            if (scb is LaunchSiteFacility rwFacility && rwFacility.facilityType == EditorFacility.SPH) return SpaceCenterFacility.Runway;
            if (scb is MissionControlBuilding) return SpaceCenterFacility.MissionControl;
            if (scb is RnDBuilding) return SpaceCenterFacility.ResearchAndDevelopment;
            if (scb is SpacePlaneHangarBuilding) return SpaceCenterFacility.SpaceplaneHangar;
            if (scb is TrackingStationBuilding) return SpaceCenterFacility.TrackingStation;
            if (scb is VehicleAssemblyBuilding) return SpaceCenterFacility.VehicleAssemblyBuilding;

            // Some mods define custom facilities
            return null;
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
