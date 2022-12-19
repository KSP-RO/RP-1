using ContractConfigurator.Parameters;
using Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using CCParams = ContractConfigurator.Parameters;

namespace ContractConfigurator.RP0
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class ContractClobberer : MonoBehaviour
    {
        private const string CrewedDurRecordContractName = "recordCrewedDuration";

        /// <summary>
        /// Used for carrying duration over from a completed record to the newly accepted one.
        /// </summary>
        private ConfiguredContract _prevCompletedCrewRecord;

        internal void Start()
        {

            GameEvents.Contract.onCompleted.Add(OnContractComplete);
            GameEvents.Contract.onAccepted.Add(OnContractAccepted);
        }

        internal void OnDestroy()
        {
            GameEvents.Contract.onCompleted.Remove(OnContractComplete);
            GameEvents.Contract.onAccepted.Remove(OnContractAccepted);
        }

        public void OnContractComplete(Contract c)
        {
            if (c is ConfiguredContract cc && cc.contractType?.name == CrewedDurRecordContractName)
            {
                _prevCompletedCrewRecord = cc;
            }
        }

        public void OnContractAccepted(Contract c)
        {
            if (_prevCompletedCrewRecord != null && c is ConfiguredContract cc &&
                cc.contractType?.name == CrewedDurRecordContractName)
            {
                try
                {
                    var oldVpg = _prevCompletedCrewRecord.GetParameter<VesselParameterGroup>();
                    var oldDur = oldVpg?.GetAllDescendents().OfType<CCParams.Duration>().FirstOrDefault();
                    var fInf = typeof(CCParams.Duration).GetField("endTimes", BindingFlags.Instance | BindingFlags.NonPublic);
                    var oldDict = (Dictionary<Guid, double>)fInf.GetValue(oldDur);

                    var newVpg = cc.GetParameter<VesselParameterGroup>();
                    var newDur = newVpg?.GetAllDescendents().OfType<CCParams.Duration>().FirstOrDefault();
                    var newDict = (Dictionary<Guid, double>)fInf.GetValue(newDur);

                    var pInf = oldDur.GetType().GetProperty("duration", BindingFlags.Instance | BindingFlags.NonPublic);

                    foreach (KeyValuePair<Guid, double> kvp in oldDict)
                    {
                        var dOld = (double)pInf.GetValue(oldDur);
                        var dNew = (double)pInf.GetValue(newDur);
                        var start = kvp.Value - dOld;
                        var newEnd = start + dNew;
                        newDict[kvp.Key] = newEnd;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
