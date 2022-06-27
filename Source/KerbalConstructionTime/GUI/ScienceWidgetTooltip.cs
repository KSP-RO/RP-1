using KSP.UI;
using System;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class ScienceWidgetTooltip : KSP.UI.TooltipTypes.TooltipController_Text
    {
        protected static ScienceWidgetTooltip _instance = null;
        public static ScienceWidgetTooltip Instance => _instance;

        protected override void Awake()
        {
            base.Awake();
            if (_instance != null)
            {
                Debug.LogError("[RP-0] Error! ScienceWidgetTooltip already exists!");
                GameObject.Destroy(_instance);
            }
            _instance = this;
        }

        protected override void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            base.OnDestroy();
        }

        public void UpdateText()
        {
            textString = $"{KSP.Localization.Localizer.Format("#rp0ScienceWidgetTooltip", KCTGameStates.SciPointsTotal.ToString("N1"), Utilities.ScienceForNextApplicants().ToString("N1"))}";
        }

        public override bool OnTooltipAboutToSpawn()
        {
            continuousUpdate = true;
            return true;
        }

        public override void OnTooltipSpawned(Tooltip instance)
        {
            UpdateText();
            base.OnTooltipSpawned(instance);
        }
    }
}
