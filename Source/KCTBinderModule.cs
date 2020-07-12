using System;
using System.Collections;
using KerbalConstructionTime;
using RP0.Crew;
using UnityEngine;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class KCTBinderModule : ScenarioModule
    {
        // FIXME if we change the min build rate, FIX THIS.
        protected const double BuildRateOffset = -0.0001d;

        protected double nextTime = -1d;
        protected double checkInterval = 0.5d;
        protected const int padLevels = 10;
        protected int[] padCounts = new int[padLevels];

        protected bool skipOne = true;
        protected bool skipTwo = true;

        private EventData<RDTech> onKctTechQueuedEvent;
        private EventData<ProtoTechNode> onKctTechCompletedEvent;
        private EventData<FacilityUpgrade> onKctFacilityUpgradeQueuedEvent;
        private EventData<FacilityUpgrade> onKctFacilityUpgradeCompletedEvent;

        public override void OnAwake()
        {
            base.OnAwake();

            CareerLog.FnGetKCTUpgdCounts = GetKCTUpgradeCounts;
            CareerLog.FnGetKCTSciPoints = GetSciPointTotalFromKCT;
            KCT_GUI.UseAvailabilityChecker = true;
            KCT_GUI.AvailabilityChecker = CheckCrewForPart;
        }

        public void Start()
        {
            onKctTechQueuedEvent = GameEvents.FindEvent<EventData<RDTech>>("OnKctTechQueued");
            if (onKctTechQueuedEvent != null)
            {
                onKctTechQueuedEvent.Add(OnKctTechQueued);
                Debug.Log($"[RP-0] Bound to OnKctTechQueued");
            }

            onKctTechCompletedEvent = GameEvents.FindEvent<EventData<ProtoTechNode>>("OnKctTechCompleted");
            if (onKctTechCompletedEvent != null)
            {
                onKctTechCompletedEvent.Add(OnKctTechCompleted);
                Debug.Log($"[RP-0] Bound to OnKctTechCompleted");
            }

            onKctFacilityUpgradeQueuedEvent = GameEvents.FindEvent<EventData<FacilityUpgrade>>("OnKctFacilityUpgradeQueued");
            if (onKctFacilityUpgradeQueuedEvent != null)
            {
                onKctFacilityUpgradeQueuedEvent.Add(OnKctFacilityUpgdQueued);
                Debug.Log($"[RP-0] Bound to OnKctFacilityUpgradeQueued");
            }

            onKctFacilityUpgradeCompletedEvent = GameEvents.FindEvent<EventData<FacilityUpgrade>>("OnKctFacilityUpgradeComplete");
            if (onKctFacilityUpgradeCompletedEvent != null)
            {
                onKctFacilityUpgradeCompletedEvent.Add(OnKctFacilityUpgdComplete);
                Debug.Log($"[RP-0] Bound to OnKctFacilityUpgradeComplete");
            }

            StartCoroutine(CreateCoursesRoutine());
        }

        public void OnDestroy()
        {
            if (onKctTechQueuedEvent != null) onKctTechQueuedEvent.Remove(OnKctTechQueued);
            if (onKctTechCompletedEvent != null) onKctTechCompletedEvent.Remove(OnKctTechCompleted);
            if (onKctFacilityUpgradeQueuedEvent != null) onKctFacilityUpgradeQueuedEvent.Remove(OnKctFacilityUpgdQueued);
            if (onKctFacilityUpgradeCompletedEvent != null) onKctFacilityUpgradeCompletedEvent.Remove(OnKctFacilityUpgdComplete);
        }

        public static int GetKCTUpgradeCounts(SpaceCenterFacility facility)
        {
            return KerbalConstructionTime.Utilities.SpentUpgradesFor(facility);
        }

        public static float GetSciPointTotalFromKCT()
        {
            // KCT returns -1 if the player hasn't earned any sci yet
            return Math.Max(0, KCTGameStates.SciPointsTotal);
        }

        public static bool CheckCrewForPart(ProtoCrewMember pcm, string partName)
        {
            // lolwut. But just in case.
            if (pcm == null)
                return false;

            bool requireTraining = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsTrainingEnabled;

            if (!requireTraining || EntryCostStorage.GetCost(partName) == 1)
                return true;

            return CrewHandler.Instance.NautHasTrainingForPart(pcm, partName);
        }

        protected void Update()
        {
            if (HighLogic.CurrentGame == null || KerbalConstructionTime.KerbalConstructionTime.Instance == null)
                return;

            if (skipTwo)
            {
                if (skipOne)
                {
                    skipOne = false;
                    return;
                }

                skipTwo = false;
                return;
            }

            if (MaintenanceHandler.Instance == null)
                return;

            double time = Planetarium.GetUniversalTime();
            if (nextTime > time)
                return;

            nextTime = time + checkInterval;

            for (int i = padCounts.Length; i-- > 0;)
                padCounts[i] = 0;

            foreach (KSCItem ksc in KCTGameStates.KSCs)
            {
                double buildRate = 0d;

                for (int i = ksc.VABRates.Count; i-- > 0;)
                    buildRate += Math.Max(0d, ksc.VABRates[i] + BuildRateOffset);

                for (int i = ksc.SPHRates.Count; i-- > 0;)
                    buildRate += Math.Max(0d, ksc.SPHRates[i] + BuildRateOffset);

                if (buildRate < 0.01d) continue;

                MaintenanceHandler.Instance.kctBuildRates[ksc.KSCName] = buildRate;

                for (int i = ksc.LaunchPads.Count; i-- > 0;)
                {
                    int lvl = ksc.LaunchPads[i].level;
                    if (lvl >= 0 && lvl < padLevels)
                        ++padCounts[lvl];
                }
            }
            double RDRate = MathParser.ParseNodeRateFormula(10, 0, false);

            MaintenanceHandler.Instance.kctResearchRate = RDRate;
            MaintenanceHandler.Instance.kctPadCounts = padCounts;
        }

        private void OnKctTechQueued(RDTech data)
        {
            Debug.Log($"[RP-0] OnKctTechQueued");
            CrewHandler.Instance.AddCoursesForTechNode(data);
        }

        private void OnKctTechCompleted(ProtoTechNode data)
        {
            Debug.Log($"[RP-0] OnKctTechCompleted");
            CareerLog.Instance?.AddTechEvent(data.techID);
        }

        private void OnKctFacilityUpgdQueued(FacilityUpgrade data)
        {
            Debug.Log($"[RP-0] OnKctFacilityUpgdQueued");
            if (!data.FacilityType.HasValue) return;    // can be null in case of third party mods that define custom facilities

            CareerLog.Instance?.AddFacilityConstructionEvent(data.FacilityType.Value, data.UpgradeLevel, data.Cost, ConstructionState.Started);
        }

        private void OnKctFacilityUpgdComplete(FacilityUpgrade data)
        {
            Debug.Log($"[RP-0] OnKctFacilityUpgdComplete");
            if (!data.FacilityType.HasValue) return;    // can be null in case of third party mods that define custom facilities

            CareerLog.Instance?.AddFacilityConstructionEvent(data.FacilityType.Value, data.UpgradeLevel, data.Cost, ConstructionState.Completed);
        }

        private IEnumerator CreateCoursesRoutine()
        {
            yield return new WaitForFixedUpdate();

            for (int i = 0; i < PartLoader.LoadedPartsList.Count; i++)
            {
                var ap = PartLoader.LoadedPartsList[i];
                if (ap.partPrefab.CrewCapacity > 0)
                {
                    var kctTech = KCTGameStates.TechList.Find(t => t.TechID == ap.TechRequired);
                    if (kctTech != null)
                    {
                        CrewHandler.Instance.AddPartCourses(ap);
                    }
                }
            }
        }
    }
}
