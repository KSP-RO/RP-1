using ContractConfigurator.Parameters;
using System;
using System.Collections.Generic;
using UnityEngine;
using RP0;

namespace ContractConfigurator.RP0
{
    public class DownrangeDistance : VesselParameter
    {
        protected static Dictionary<string, DownrangeDistance> CompletedParams;

        protected bool triggered = false;
        protected double distance = 0;
        protected double curDist = 0;
        protected double markLatitude, markLongitude;

        private float lastUpdate = 0.0f;
        private const float UPDATE_FREQUENCY = 1f;

        public DownrangeDistance() : base(null)
        {
        }

        public DownrangeDistance(string title, double distance) : base(title)
        {
            this.distance = distance;
        }

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);

            node.AddValue("distance", distance);
            node.AddValue("markLatitude", markLatitude);
            node.AddValue("markLongitude", markLongitude);
            node.AddValue("triggered", triggered);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);

            node.TryGetValue("distance", ref distance);
            node.TryGetValue("markLatitude", ref markLatitude);
            node.TryGetValue("markLongitude", ref markLongitude);
            node.TryGetValue("triggered", ref triggered);
        }

        protected override string GetParameterTitle()
        {
            if (FlightGlobals.ActiveVessel == null)
            {
                return $"Must achieve a downrange distance of at least {(distance / 1000.0):N0} km";
            }
            else
            {
                return $"Downrange distance {(curDist / 1000.0):N0} / {(distance / 1000.0):N0} km";
            }
        }

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (!triggered) return false;

            // The following distance calculation code is from MechJebModuleFlightRecorder
            CelestialBody markBody = FlightGlobals.GetHomeBody();
            Vector3d markVector = markBody.GetSurfaceNVector(markLatitude, markLongitude);
            Vector3d vesselVector = vessel.CoMD - markBody.transform.position;
            curDist = markBody.Radius * Vector3d.Angle(markVector, vesselVector) * UtilMath.Deg2Rad;

            return curDist > distance;
        }

        protected override void AwardCompletion()
        {
            base.AwardCompletion();

            RP0Debug.Log("DownrangeDistance AwardCompletion");

            var cc = (ConfiguredContract)Root;
            if (cc.AutoAccept)
            {
                string contractName = ConfiguredContract.contractTypeName(cc);
                RP0Debug.Log("Contract name: " + contractName);

                GameEvents.onGameSceneSwitchRequested.Add(SceneChangeInProgress);

                if (CompletedParams == null)
                {
                    CompletedParams = new Dictionary<string, DownrangeDistance>();
                }

                if (CompletedParams.ContainsKey(contractName))
                {
                    CompletedParams[contractName] = this;
                }
                else
                {
                    CompletedParams.Add(contractName, this);
                }
            }
        }

        protected override void OnRegister()
        {
            base.OnRegister();
            GameEvents.onLaunch.Add(OnLaunch);

            try
            {
                var cc = (ConfiguredContract)Root;
                string contractName = ConfiguredContract.contractTypeName(cc);

                if (cc.AutoAccept && CompletedParams != null && CompletedParams.ContainsKey(contractName))
                {
                    RP0Debug.Log("Carrying starting point over to new contract...");
                    DownrangeDistance oldParam = CompletedParams[contractName];
                    triggered = oldParam.triggered;
                    curDist = oldParam.curDist;
                    markLatitude = oldParam.markLatitude;
                    markLongitude = oldParam.markLongitude;
                }
            }
            catch (Exception ex)
            {
                RP0Debug.LogError("OnRegisterError: " + ex);
            }
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.onLaunch.Remove(OnLaunch);
        }

        protected void OnLaunch(EventReport er)
        {
            markLatitude = FlightGlobals.ActiveVessel.latitude;
            markLongitude = FlightGlobals.ActiveVessel.longitude;
            triggered = true;
        }

        protected override void OnUpdate()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            base.OnUpdate();

            if (Time.fixedTime - lastUpdate > UPDATE_FREQUENCY)
            {
                lastUpdate = Time.fixedTime;

                CheckVessel(v);

                // Force a call to GetTitle to update the contracts app
                GetTitle();
            }
        }

        private void SceneChangeInProgress(GameEvents.FromToAction<GameScenes, GameScenes> evt)
        {
            RP0Debug.Log("SceneChangeInProgress");
            GameEvents.onGameSceneSwitchRequested.Remove(SceneChangeInProgress);
            CompletedParams = null;
        }
    }
}
