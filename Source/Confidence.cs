using UnityEngine;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToExistingCareerGames | ScenarioCreationOptions.AddToNewCareerGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class Confidence : ScenarioModule
    {
        public static Confidence Instance { get; private set; }

        [KSPField(isPersistant = true)]
        private float confidence = 0f;

        public static float CurrentConfidence => Instance == null ? 0 : Instance.confidence;

        public static EventData<float, TransactionReasons> OnConfidenceChanged;

        public override void OnAwake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
                Debug.LogError("[RP-0] Error: duplicate Confidence instance!");
            }
            Instance = this;

            if (HighLogic.CurrentGame != null)
            {
                confidence = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().StartingConfidence;
            }

            GameEvents.OnScienceChanged.Add(OnScienceChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            GameEvents.OnScienceChanged.Remove(OnScienceChanged);
        }

        public void AddConfidence(float delta, TransactionReasons reason)
        {
            float oldConfidence = confidence;
            confidence = Mathf.Max(0f, confidence + delta);
            if (confidence != oldConfidence)
                OnConfidenceChanged.Fire(confidence, reason);
        }

        public void SetConfidence(float newConfidence, TransactionReasons reason)
        {
            float oldConfidence = confidence;
            confidence = Mathf.Max(0f, newConfidence);
            if (confidence != oldConfidence)
                OnConfidenceChanged.Fire(confidence, reason);
        }

        private void OnScienceChanged(float sci, TransactionReasons reason)
        {
            if (sci > 0)
                AddConfidence(2 * sci, reason);
        }
    }
}
