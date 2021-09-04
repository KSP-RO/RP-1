using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    /// <summary>
    /// A VesselModule for keeping persistent vessel data across decouplings, dockings, recoveries and edits inside the VAB/SPH.
    /// </summary>
    public class KCTVesselTracker : VesselModule
    {
        private static bool _isBoundToEvents = false;
        private static bool _isDockingInProgress = false;
        private static bool _isUnDockingInProgress = false;

        public KCTVesselData Data = new KCTVesselData();
        public Dictionary<uint, KCTVesselData> DockedVesselData;

        protected override void OnAwake()
        {
            base.OnAwake();

            if (!_isBoundToEvents)
            {
                GameEvents.onPartDeCoupleNewVesselComplete.Add(OnPartDeCoupleNewVesselComplete);
                GameEvents.onPartCouple.Add(OnPartCouple);
                GameEvents.onPartUndock.Add(OnPartUndock);
                GameEvents.onVesselDocking.Add(OnVesselDocking);
                GameEvents.onVesselsUndocking.Add(OnVesselsUndocking);
                _isBoundToEvents = true;
            }
        }

        protected override void OnStart()
        {
            base.OnStart();

            if (Data.VesselID == string.Empty)
            {
                Data.VesselID = Guid.NewGuid().ToString("N");
            }
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            foreach (ConfigNode n in node.GetNodes("DATA"))
            {
                Data.Load(n);
            }

            foreach (ConfigNode n in node.GetNodes("DOCKED_DATA"))
            {
                uint launchID = default;
                if (!n.TryGetValue("launchID", ref launchID)) continue;

                DockedVesselData = DockedVesselData ?? new Dictionary<uint, KCTVesselData>();
                DockedVesselData[launchID] = new KCTVesselData(n);
            }
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            var n = node.AddNode("DATA");
            Data.Save(n);

            if (DockedVesselData != null)
            {
                foreach (KeyValuePair<uint, KCTVesselData> kvp in DockedVesselData)
                {
                    var dn = node.AddNode("DOCKED_DATA");
                    kvp.Value.Save(dn);
                    dn.AddValue("launchID", kvp.Key);
                }
            }
        }

        private void OnPartUndock(Part data)
        {
            _isUnDockingInProgress = true;
            KerbalConstructionTime.Instance.StartCoroutine(ResetUnDockingInProgress());
        }

        private void OnVesselDocking(uint persistentId1, uint persistentId2)
        {
            _isDockingInProgress = true;
            KerbalConstructionTime.Instance.StartCoroutine(ResetDockingInProgress());
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

            if (dict.Count == 1) dict = null;    // Only has data for the current vessel so no need to keep history
            vm.DockedVesselData = dict;
        }

        private void OnPartDeCoupleNewVesselComplete(Vessel v1, Vessel v2)
        {
            var vm1 = (KCTVesselTracker)v1.vesselModules.Find(vm => vm is KCTVesselTracker);
            var vm2 = (KCTVesselTracker)v2.vesselModules.Find(vm => vm is KCTVesselTracker);

            if (vm1 != null && vm2 != null)
            {
                vm2.Data.VesselID = vm1.Data.VesselID;
                vm2.Data.LaunchID = vm1.Data.LaunchID;
                vm2.Data.FacilityBuiltIn = vm1.Data.FacilityBuiltIn;
            }
         }
    }
}
