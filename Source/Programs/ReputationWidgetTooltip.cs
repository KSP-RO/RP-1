using KSP.UI;
using System;
using UnityEngine;

namespace RP0
{
    public class ReputationWidgetTooltip : KSP.UI.TooltipTypes.TooltipController_Text
    {
        double _lastSubsidy = 0;
        double _lastMaxRep = 0;
        public void UpdateText(MaintenanceHandler.SubsidyDetails details)
        {
            _lastSubsidy = details.subsidy;
            _lastMaxRep = details.maxRep;
            textString = $"{KSP.Localization.Localizer.Format("#rp0RepWidgetTooltip", details.minSubsidy.ToString("N0"), details.minRep.ToString("N0"), details.maxSubsidy.ToString("N0"), details.maxRep.ToString("N0"), details.subsidy.ToString("N0"))}";
        }

        public override bool OnTooltipAboutToSpawn()
        {
            continuousUpdate = true;
            return true;
        }

        public override void OnTooltipSpawned(KSP.UI.Tooltip instance)
        {
            UpdateText(MaintenanceHandler.Instance.GetSubsidyDetails());
            base.OnTooltipSpawned(instance);
        }

        // Polling, because at high warp this will definitely change from frame to frame.
        private void Update()
        {
            if (tooltipInstance != null)
            {
                var details = MaintenanceHandler.Instance.GetSubsidyDetails();
                if (_lastSubsidy != details.subsidy || _lastMaxRep != details.maxRep)
                    UpdateText(details);
            }
        }
    }
}
