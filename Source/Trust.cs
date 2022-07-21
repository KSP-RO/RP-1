using UnityEngine;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToExistingCareerGames | ScenarioCreationOptions.AddToNewCareerGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class Trust : ScenarioModule
    {
        public static Trust Instance { get; private set; }

        [KSPField(isPersistant = true)]
        private float trust = 0f;

        public static float CurrentTrust => Instance == null ? 0 : Instance.trust;

        public static EventData<float, TransactionReasons> OnTrustChanged;

        public override void OnAwake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
                Debug.LogError("[RP-0] Error: duplicate Trust instance!");
            }
            Instance = this;

            if (HighLogic.CurrentGame != null)
            {
                trust = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().StartingTrust;
            }

            GameEvents.OnScienceChanged.Add(OnScienceChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            GameEvents.OnScienceChanged.Remove(OnScienceChanged);
        }

        public void AddTrust(float delta, TransactionReasons reason)
        {
            float oldTrust = trust;
            trust = Mathf.Max(0f, trust + delta);
            if (trust != oldTrust)
                OnTrustChanged.Fire(trust, reason);
        }

        public void SetTrust(float newTrust, TransactionReasons reason)
        {
            float oldTrust = trust;
            trust = Mathf.Max(0f, newTrust);
            if (trust != oldTrust)
                OnTrustChanged.Fire(trust, reason);
        }

        private void OnScienceChanged(float sci, TransactionReasons reason)
        {
            if (sci > 0)
                AddTrust(2 * sci, reason);
        }
    }
}
