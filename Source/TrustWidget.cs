using UnityEngine;

namespace RP0
{
    public class TrustWidget : CurrencyWidget
    {
        public TMPro.TextMeshProUGUI text;

        private void Awake()
        {
            Trust.OnTrustChanged.Add(onTrustChange);
        }

        private void OnDestroy()
        {
            Trust.OnTrustChanged.Remove(onTrustChange);
        }

        private void onTrustChange(float trust, TransactionReasons reason)
        {
            text.text = trust.ToString("N1");
        }


        public override void DelayedStart()
        {
            if (Trust.Instance != null)
            {
                onTrustChange(Trust.CurrentTrust, TransactionReasons.None);
            }
        }

        public override bool OnAboutToStart()
        {
            return Trust.Instance != null;
        }
    }
}
