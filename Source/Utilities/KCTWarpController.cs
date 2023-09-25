﻿using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace RP0
{
    // Between Vessel (13) and Timing4 (19)
    [DefaultExecutionOrder(15)]
    class KCTWarpController : MonoBehaviour
    {
        private const string ModTag = "[KCTWarpController]";
        private double lastUT = 0;
        private int desiredWarpRate = 0;
        private bool warping = false;
        private bool isNextThing = false;
        internal IKCTBuildItem warpTarget;
        public static KCTWarpController Instance { get; private set; } = null;
        private static GameObject go = null;

        public static void Create(IKCTBuildItem warpTarget)
        {
            if (go is GameObject)
                go.DestroyGameObject();
            go = new GameObject("KCTWarpController");
            var controller = go.AddComponent<KCTWarpController>();
            if (warpTarget != null)
            {
                controller.isNextThing = false;
                controller.warpTarget = warpTarget;
            }
            else
            {
                controller.isNextThing = true;
                controller.warpTarget = KCTUtilities.GetNextThingToFinish();
            }
            Debug.Log($"{ModTag} Created for warp target {controller.warpTarget.GetItemName()}");
        }

        public void Awake()
        {
            if (Instance != null)
                Destroy(Instance);
            Instance = this;
        }

        public void OnDestroy()
        {
            Debug.Log($"{ModTag} {warpTarget.GetItemName()} OnDestroy.");
            if (Instance == this)
            {
                Destroy(this);
                Instance = null;
            }
        }

        public void Start()
        {
            lastUT = Planetarium.GetUniversalTime();
            if (warpTarget == null || warpTarget.IsComplete())
            {
                StopWarp();
                return;
            }

            // These KCTGameStates fields should fade out.
            desiredWarpRate = RampUpWarp(warpTarget);
            warping = true;
        }

        public void FixedUpdate()
        {
            if (KCT_GUI.IsPrimarilyDisabled || Instance is null || !(HighLogic.LoadedSceneIsFlight || HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.TRACKSTATION))
            {
                return;
            }
            
            int warpRate = TimeWarp.CurrentRateIndex;

            // if the player (or something else) has warped us down to 1x, exit.
            if (warpRate == 0)
            {
                Instance.gameObject.DestroyGameObject();
                return;
            }

            // If the target goes null or completes, stop warp and exit
            if (warpTarget == null || warpTarget.IsComplete())
            {
                StopWarp();
                return;
            }

            if (isNextThing)
            {
                var newTarget = KCTUtilities.GetNextThingToFinish();
                if (newTarget != warpTarget)
                {
                    warpTarget = newTarget;
                    // second check
                    if (warpTarget == null || warpTarget.IsComplete())
                    {
                        StopWarp();
                        return;
                    }
                }
            }

            Profiler.BeginSample("RP0WarpController");
            double remaining = warpTarget.GetTimeLeft();
            double UT = Planetarium.GetUniversalTime();
            double dT = UT - lastUT;
            if (dT > 0)
            {
                
                if (warping && warpRate < desiredWarpRate) //if something else changes the warp rate then release control to them, such as Kerbal Alarm Clock
                {
                    // This will prevent us warping up again--but note this does _not_ make us exit.
                    Debug.Log($"{ModTag} External warp change detected, backing off control.");
                    warping = false;
                }
                int nBuffer = 3;   // TODO: Make configurable
                if (!warping )
                    nBuffer = 1;
                if (warpRate > 0 && dT * nBuffer > Math.Max(remaining, 0))
                {
                    int newRate = warpRate;
                    //find next lower rate that will not step past the remaining time
                    while (newRate > 0 && TimeWarp.fetch.warpRates[newRate] * Planetarium.fetch.fixedDeltaTime * nBuffer > remaining)
                        newRate--;
                    RP0Debug.Log($"Warping down to {newRate} (step size: {TimeWarp.fetch.warpRates[newRate] * Planetarium.fetch.fixedDeltaTime})");
                    desiredWarpRate = newRate;
                    if (newRate == 0)
                        StopWarp();
                    else
                        TimeWarp.SetRate(newRate, true);
                }
            }
            lastUT = UT;
            Profiler.EndSample();
        }

        private int RampUpWarp(IKCTBuildItem item)
        {
            int newRate = TimeWarp.CurrentRateIndex;
            double timeLeft = item.GetTimeLeft();
            if (double.IsPositiveInfinity(timeLeft))
                timeLeft = KCTUtilities.GetNextThingToFinish().GetTimeLeft();
            while ((newRate + 1 < TimeWarp.fetch.warpRates.Length) &&
                   (timeLeft > TimeWarp.fetch.warpRates[newRate + 1] * Planetarium.fetch.fixedDeltaTime) &&
                   (newRate < KCTGameStates.Settings.MaxTimeWarp))
            {
                newRate++;
            }
            TimeWarp.SetRate(newRate, true);
            return newRate;
        }

        public void StopWarp()
        {
            Debug.Log($"{ModTag} Halting warp to target {warpTarget?.GetItemName() ?? "(null)"}");
            TimeWarp.SetRate(0, true);
            warping = false;
            gameObject.DestroyGameObject();
        }
    }
}
