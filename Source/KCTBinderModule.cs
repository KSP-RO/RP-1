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
        protected const double BuildRateOffset = -0.05001d;

        protected double nextTime = -1d;
        protected double checkInterval = 0.5d;
        protected const int padLevels = 10;
        protected int[] padCounts = new int[padLevels];

        protected bool skipOne = true;
        protected bool skipTwo = true;

        private EventData<RDTech> onKctTechQueuedEvent;

        public override void OnAwake()
        {
            base.OnAwake();

            KCT_GUI.UseAvailabilityChecker = true;
            KCT_GUI.AvailabilityChecker = CheckCrewForPart;
        }

        public void Start()
        {
            onKctTechQueuedEvent = GameEvents.FindEvent<EventData<RDTech>>("OnKctTechQueued");
            if (onKctTechQueuedEvent != null)
            {
                onKctTechQueuedEvent.Add(OnKctTechQueued);
            }

            StartCoroutine(CreateCoursesRoutine());
        }

        public void OnDestroy()
        {
            if (onKctTechQueuedEvent != null) onKctTechQueuedEvent.Remove(OnKctTechQueued);
        }

        public static bool CheckCrewForPart(ProtoCrewMember pcm, string partName)
        {
            // lolwut. But just in case.
            if (pcm == null)
                return false;

            bool requireTraining = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsTrainingEnabled;

            if (!requireTraining || EntryCostStorage.GetCost(partName) == 1)
                return true;

            partName = TrainingDatabase.SynonymReplace(partName);

            FlightLog.Entry ent = pcm.careerLog.Last();
            if (ent == null)
                return false;

            bool lacksMission = true;
            for (int i = pcm.careerLog.Entries.Count; i-- > 0;)
            {
                FlightLog.Entry e = pcm.careerLog.Entries[i];
                if (lacksMission)
                {
                    if (string.IsNullOrEmpty(e.type) || string.IsNullOrEmpty(e.target))
                        continue;

                    if (e.type == "TRAINING_mission" && e.target == partName)
                    {
                        double exp = CrewHandler.Instance.GetExpiration(pcm.name, e);
                        lacksMission = exp == 0d || exp < Planetarium.GetUniversalTime();
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(e.type) || string.IsNullOrEmpty(e.target))
                        continue;

                    if (e.type == "TRAINING_proficiency" && e.target == partName)
                        return true;
                }
            }
            return false;
        }

        protected void Update()
        {
            if (HighLogic.CurrentGame == null || KerbalConstructionTime.KerbalConstructionTime.instance == null)
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

            foreach (KCT_KSC ksc in KCT_GameStates.KSCs)
            {
                double buildRate = 0d;

                for (int i = ksc.VABRates.Count; i-- > 0;)
                    buildRate += Math.Max(0d, ksc.VABRates[i] + BuildRateOffset);

                for (int i = ksc.SPHRates.Count; i-- > 0;)
                    buildRate += Math.Max(0d, ksc.SPHRates[i] + BuildRateOffset);

                if (buildRate > 0.001d)
                    MaintenanceHandler.Instance.kctBuildRates[ksc.KSCName] = buildRate;

                for (int i = ksc.LaunchPads.Count; i-- > 0;)
                {
                    int lvl = ksc.LaunchPads[i].level;
                    if (lvl >= 0 && lvl < padLevels)
                        ++padCounts[lvl];
                }
            }
            double RDRate = KCT_MathParsing.ParseNodeRateFormula(10, 0, false);

            MaintenanceHandler.Instance.kctResearchRate = RDRate;
            MaintenanceHandler.Instance.kctPadCounts = padCounts;
        }

        private void OnKctTechQueued(RDTech data)
        {
            CrewHandler.Instance.AddCoursesForTechNode(data);
        }

        private IEnumerator CreateCoursesRoutine()
        {
            yield return new WaitForFixedUpdate();

            for (int i = 0; i < PartLoader.LoadedPartsList.Count; i++)
            {
                var ap = PartLoader.LoadedPartsList[i];
                if (ap.partPrefab.CrewCapacity > 0)
                {
                    var kctTech = KCT_GameStates.TechList.Find(t => t.techID == ap.TechRequired);
                    if (kctTech != null)
                    {
                        CrewHandler.Instance.AddPartCourses(ap);
                    }
                }
            }
        }
    }
}
