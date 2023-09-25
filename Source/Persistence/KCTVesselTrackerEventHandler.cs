﻿using System.Collections;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class KCTVesselTrackerEventHandler : MonoBehaviour
    {
        private bool _isDockingInProgress = false;
        private bool _isUnDockingInProgress = false;

        private void Awake()
        {
            GameEvents.onPartDeCoupleNewVesselComplete.Add(OnPartDeCoupleNewVesselComplete);
            GameEvents.onPartCouple.Add(OnPartCouple);
            GameEvents.onPartUndock.Add(OnPartUndock);
            GameEvents.onVesselDocking.Add(OnVesselDocking);
            GameEvents.onVesselsUndocking.Add(OnVesselsUndocking);

            DontDestroyOnLoad(this);
        }

        private void OnPartUndock(Part data)
        {
            _isUnDockingInProgress = true;
            StartCoroutine(ResetUnDockingInProgress());
        }

        private void OnVesselDocking(uint persistentId1, uint persistentId2)
        {
            _isDockingInProgress = true;
            StartCoroutine(ResetDockingInProgress());
        }

        private IEnumerator ResetUnDockingInProgress()
        {
            yield return new WaitForEndOfFrame();
            _isUnDockingInProgress = false;
        }

        private IEnumerator ResetDockingInProgress()
        {
            yield return new WaitForEndOfFrame();
            _isDockingInProgress = false;
        }

        private void OnPartCouple(GameEvents.FromToAction<Part, Part> data)
        {
            if (_isDockingInProgress)
            {
                var vm1 = (KCTVesselTracker)data.from.vessel.vesselModules.Find(vm => vm is KCTVesselTracker);
                var vm2 = (KCTVesselTracker)data.to.vessel.vesselModules.Find(vm => vm is KCTVesselTracker);

                uint id1 = data.from.vessel.rootPart.launchID;
                uint id2 = data.to.vessel.rootPart.launchID;

                var dict1 = vm1.DockedVesselData ?? new Dictionary<uint, KCTVesselData>() { { id1, KCTVesselData.Parse(vm1) } };
                var dict2 = vm2.DockedVesselData ?? new Dictionary<uint, KCTVesselData>() { { id2, KCTVesselData.Parse(vm2) } };
                var combinedDict = new[] { dict1, dict2 }.SelectMany(dict => dict)
                         .ToLookup(pair => pair.Key, pair => pair.Value)
                         .ToDictionary(group => group.Key, group => group.First());

                vm1.DockedVesselData = vm2.DockedVesselData = combinedDict;
            }
        }

        private void OnVesselsUndocking(Vessel oldVessel, Vessel newVessel)
        {
            if (_isUnDockingInProgress)
            {
                var vm1 = (KCTVesselTracker)oldVessel.vesselModules.Find(vm => vm is KCTVesselTracker);
                if (!vm1?.DockedVesselData?.Any() ?? false) return;

                var vm2 = (KCTVesselTracker)newVessel.vesselModules.Find(vm => vm is KCTVesselTracker);
                var dict1 = vm1.DockedVesselData;
                var dict2 = new Dictionary<uint, KCTVesselData>(dict1);

                CleanAndAssignDockedVesselData(vm1, dict1);
                CleanAndAssignDockedVesselData(vm2, dict2);
            }
        }

        /// <summary>
        /// Removes all entries from the dictionary where no corresponding part with the same launchId is found on the vessel.
        /// After the cleaning process, dictionary will be assigned to vesselModule.
        /// Will also make sure that the new vessel that was created during undocking will get correct data assigned to it.
        /// </summary>
        /// <param name="vm">Tracking VesselModule</param>
        /// <param name="dict">Docking history dictionary to clean and assign to vm</param>
        private void CleanAndAssignDockedVesselData(KCTVesselTracker vm, Dictionary<uint, KCTVesselData> dict)
        {
            uint rootPartLaunchId = vm.Vessel.rootPart.launchID;
            uint[] keys = dict.Keys.ToArray();
            foreach (uint launchId in keys)
            {
                if (launchId == rootPartLaunchId) continue;
                if (!vm.Vessel.Parts.Any(p => p.launchID == launchId))
                {
                    dict.Remove(launchId);
                }
            }

            // Need to fill the vessel data for the new vessel that was just created during undocking.
            // Interestingly, OnPartDeCoupleNewVesselComplete doesn't get called for undocking.
            if (!vm.Data.IsInitialized && dict.TryGetValue(rootPartLaunchId, out KCTVesselData data))
            {
                vm.Data.SetFrom(data);
            }

            if (dict.Count == 1) dict = null;    // Only has data for the current vessel so no need to keep history
            vm.DockedVesselData = dict;
        }

        private void OnPartDeCoupleNewVesselComplete(Vessel v1, Vessel v2)
        {
            var vm1 = (KCTVesselTracker)v1.vesselModules.Find(vm => vm is KCTVesselTracker);
            var vm2 = (KCTVesselTracker)v2.vesselModules.Find(vm => vm is KCTVesselTracker);

            if (vm1 != null && vm2 != null)
            {
                vm2.Data.SetFrom(vm1.Data);
            }
         }
    }
}
