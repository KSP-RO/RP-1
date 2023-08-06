using UnityEngine;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToExistingCareerGames | ScenarioCreationOptions.AddToNewCareerGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class Confidence : ScenarioModule
    {
        public static Confidence Instance { get; private set; }

        [KSPField(isPersistant = true)]
        private float confidence = 0f;

        [KSPField(isPersistant = true)]
        private float confidenceEarned = 0f;

        public static float CurrentConfidence => Instance == null ? 0 : Instance.confidence;

        public static float AllConfidenceEarned => Instance == null ? 0 : Instance.confidenceEarned;

        public static EventData<float, TransactionReasons> OnConfidenceChanged = new EventData<float, TransactionReasons>("OnConfidenceChanged");

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

            GameEvents.Modifiers.OnCurrencyModified.Add(OnCurrenciesModified);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            GameEvents.Modifiers.OnCurrencyModified.Remove(OnCurrenciesModified);
        }

        public void AddConfidence(float delta, TransactionReasons reason)
        {
            float oldConfidence = confidence;
            confidence += delta;
            CurrencyModifierQueryRP0 data = new CurrencyModifierQueryRP0(reason.RP0(), 0d, 0d, 0d, delta, 0d);
            GameEvents.Modifiers.OnCurrencyModifierQuery.Fire(data);
            GameEvents.Modifiers.OnCurrencyModified.Fire(data);
            if (confidence < 0f)
                confidence = 0f;

            if (confidence != oldConfidence && delta != 0f)
            {
                delta = confidence - oldConfidence;
                OnConfidenceChanged.Fire(confidence, reason);
                if (delta > 0f)
                    confidenceEarned += delta;
            }
        }

        private void OnCurrenciesModified(CurrencyModifierQuery query)
        {
            float changeDelta = query.GetTotal(Currency.Science);
            // Annoyingly Kerbalism uses TransactionReason.None
            if (changeDelta > 0f && (query.reason == TransactionReasons.ScienceTransmission || query.reason == TransactionReasons.VesselRecovery || query.reason == TransactionReasons.None))
            {
                float conf;
                if (Programs.ProgramHandler.Settings != null)
                    conf = Programs.ProgramHandler.Settings.scienceToConfidence.Evaluate(System.Math.Max(0f, (float)KerbalConstructionTime.KerbalConstructionTimeData.Instance.SciPointsTotal)) * changeDelta;
                else
                    conf = changeDelta * 2f;

                AddConfidence(conf, query.reason);
            }
            if (query is CurrencyModifierQueryRP0 cmq)
            {
                changeDelta = (float)cmq.GetEffectDelta(CurrencyRP0.Confidence, true);
                if (changeDelta != 0)
                {
                    float oldConfidence = confidence;
                    confidence += changeDelta;
                    if (confidence < 0f)
                        confidence = 0f;
                    OnConfidenceChanged.Fire(confidence, query.reason);
                    // We have to check the input to see if this is a source or sink of confidence
                    // If it's a source, change confidenceEarned even if delta is negative, because this might
                    // be a "-15% confidence from contracts" leader
                    if (cmq.GetInput(CurrencyRP0.Confidence) > 0d)
                        confidenceEarned += changeDelta;
                }
            }
        }
    }
}
