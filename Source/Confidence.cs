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
            // We'll apply the total change in OnCurrenciesModified
            CurrencyModifierQueryRP0 data = new CurrencyModifierQueryRP0(reason.RP0(), 0d, 0d, 0d, delta, 0d);
            GameEvents.Modifiers.OnCurrencyModifierQuery.Fire(data);
            GameEvents.Modifiers.OnCurrencyModified.Fire(data);
        }

        private void OnCurrenciesModified(CurrencyModifierQuery query)
        {
            float sciDelta = query.GetInput(Currency.Science);
            float conf = 0f;
            // Annoyingly Kerbalism uses TransactionReason.None
            if (sciDelta > 0f && (query.reason == TransactionReasons.ScienceTransmission || query.reason == TransactionReasons.VesselRecovery || query.reason == TransactionReasons.None))
            {
                if (Programs.ProgramHandler.Settings != null)
                    conf = Programs.ProgramHandler.Settings.scienceToConfidence.Evaluate(System.Math.Max(0f, (float)KerbalConstructionTime.KerbalConstructionTimeData.Instance.SciPointsTotal)) * sciDelta;
                else
                    conf = sciDelta * 2f;

                conf = (float)CurrencyUtils.Conf(TransactionReasonsRP0.ScienceTransmission, conf);
            }

            // We'll actually process the confidence change here and use GetTotal, instead of GetEffectDelta
            if (query is CurrencyModifierQueryRP0 cmq)
            {
                if(conf != 0f)
                    cmq.AddPostDelta(CurrencyRP0.Confidence, conf, false);

                float oldConfidence = confidence;

                confidence += (float)cmq.GetTotal(CurrencyRP0.Confidence, true);

                if (confidence < 0f)
                    confidence = 0f;

                if (confidence != oldConfidence)
                {
                    float delta = confidence - oldConfidence;
                    OnConfidenceChanged.Fire(confidence, cmq.reason);
                    if (delta > 0f)
                        confidenceEarned += delta;
                }
            }
        }
    }
}
