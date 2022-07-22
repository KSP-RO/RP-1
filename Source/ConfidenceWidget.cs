using UnityEngine;

namespace RP0
{
    public class ConfidenceWidget : CurrencyWidget
    {
        public TMPro.TextMeshProUGUI text;

        private void Awake()
        {
            Confidence.OnConfidenceChanged.Add(onConfidenceChanged);
        }

        private void OnDestroy()
        {
            Confidence.OnConfidenceChanged.Remove(onConfidenceChanged);
        }

        private void onConfidenceChanged(float confidence, TransactionReasons reason)
        {
            text.text = confidence.ToString("N1");
        }


        public override void DelayedStart()
        {
            if (Confidence.Instance != null)
            {
                onConfidenceChanged(Confidence.CurrentConfidence, TransactionReasons.None);
            }
        }

        public override bool OnAboutToStart()
        {
            return Confidence.Instance != null;
        }
    }
}
