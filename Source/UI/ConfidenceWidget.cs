using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KSP.UI.TooltipTypes;
using KSP.Localization;

namespace RP0.UI
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

        public static void CreateConfidenceWidget(GameObject confidenceWidgetObj)
        {
            confidenceWidgetObj.name = "ConfidenceWidget";

            GameObject.Destroy(confidenceWidgetObj.GetComponent<ReputationWidget>());

            var frameImage = (Image)confidenceWidgetObj.GetComponentInChildren(typeof(Image));
            frameImage.sprite = Sprite.Create(GameDatabase.Instance.GetTexture("RP-0/Resources/confidence_background", false), frameImage.sprite.rect, frameImage.sprite.pivot);

            var img = GameObject.Instantiate(new GameObject("Background"), confidenceWidgetObj.transform, worldPositionStays: false).AddComponent<Image>();
            img.color = new Color32(58, 58, 63, 255);
            img.rectTransform.anchorMin = frameImage.rectTransform.anchorMin;
            img.rectTransform.anchorMax = frameImage.rectTransform.anchorMax;
            img.rectTransform.anchoredPosition = frameImage.rectTransform.anchoredPosition;
            img.rectTransform.sizeDelta = ((RectTransform)confidenceWidgetObj.transform).sizeDelta;    // No idea why the frame image transform is larger than the component itself

            var textComp = GameObject.Instantiate(new GameObject("Text"), confidenceWidgetObj.transform, worldPositionStays: false).AddComponent<TextMeshProUGUI>();
            textComp.alignment = TextAlignmentOptions.Right;
            textComp.color = XKCDColors.KSPBadassGreen;
            textComp.fontSize = 22;
            textComp.rectTransform.localPosition = new Vector3(-9, -1, 0);
            textComp.fontStyle = FontStyles.Bold;

            var confidenceWidget = confidenceWidgetObj.AddComponent<ConfidenceWidget>();
            confidenceWidget.text = textComp;
            confidenceWidget.DelayedStart();

            // Add tooltip
            var tooltip = confidenceWidgetObj.AddComponent<TooltipController_TextFunc>();
            var prefab = AssetBase.GetPrefab<Tooltip_Text>("Tooltip_Text");
            tooltip.prefab = prefab;
            tooltip.getStringAction = GetTooltipTextConf;
            tooltip.continuousUpdate = true;
        }
        private static string GetTooltipTextConf()
        {
            return Localizer.Format("#rp0ConfidenceWidgetTooltip", Confidence.AllConfidenceEarned.ToString("N0"));
        }
    }
}
