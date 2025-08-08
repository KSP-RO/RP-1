using UnityEngine;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToExistingCareerGames | ScenarioCreationOptions.AddToNewCareerGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class Confidence : ScenarioModule
    {
        public static Confidence Instance { get; private set; }

        [KSPField(isPersistant = true)]
        private double confidence = 0d;

        [KSPField(isPersistant = true)]
        private double confidenceEarned = 0d;

        public static double CurrentConfidence => Instance == null ? 0d : Instance.confidence;

        public static double AllConfidenceEarned => Instance == null ? 0d : Instance.confidenceEarned;

        public static EventData<double, TransactionReasons> OnConfidenceChanged = new EventData<double, TransactionReasons>("OnConfidenceChanged");

        public override void OnAwake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
                RP0Debug.LogError("Error: duplicate Confidence instance!");
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

        public void SetConfidence(float value)
        {
            confidence = value;
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
            double sciDelta = query.GetInput(Currency.Science);
            double conf = 0d;
            // Annoyingly Kerbalism uses TransactionReason.None for science transmission
            if (!SpaceCenterManagement.IsRefundingScience && sciDelta > 0d
                && (query.reason == TransactionReasons.ScienceTransmission || query.reason == TransactionReasons.VesselRecovery || query.reason == TransactionReasons.None))
            {
                if (Programs.ProgramHandler.Settings != null)
                    conf = Programs.ProgramHandler.Settings.scienceToConfidence.Evaluate(System.Math.Max(0d, SpaceCenterManagement.Instance.SciPointsTotal)) * sciDelta;
                else
                    conf = sciDelta * 2d;

                conf = CurrencyUtils.Conf(TransactionReasonsRP0.ScienceTransmission, conf);
            }

            // We'll actually process the confidence change here and use GetTotal, instead of GetEffectDelta
            if (query is CurrencyModifierQueryRP0 cmq)
            {
                if (conf != 0d)
                    cmq.AddPostDelta(CurrencyRP0.Confidence, conf, false);

                double oldConfidence = confidence;

                confidence += (float)cmq.GetTotal(CurrencyRP0.Confidence, true);

                if (confidence < 0d)
                    confidence = 0d;

                if (confidence != oldConfidence)
                {
                    double delta = confidence - oldConfidence;
                    OnConfidenceChanged.Fire(confidence, cmq.reason);
                    if (delta > 0d)
                        confidenceEarned += delta;
                }
            }
        }
    }
}
