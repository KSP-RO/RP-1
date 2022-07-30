using KSP.UI;
using KSP.UI.TooltipTypes;
using System;
using UnityEngine.EventSystems;

namespace RP0.UI
{
    public class TooltipController_TextFunc : TooltipController_Text
    {
        public Func<string> getStringAction = null;

        public override void OnTooltipSpawned(KSP.UI.Tooltip instance)
        {
            if (getStringAction != null)
                textString = getStringAction();

            base.OnTooltipSpawned(instance);
        }

        public override bool OnTooltipUpdate(KSP.UI.Tooltip instance)
        {
            if(continuousUpdate && tooltipInstance != null)
            {
                if (getStringAction != null)
                    textString = getStringAction();

                if (tooltipInstance.label.text != textString)
                    tooltipInstance.label.text = textString;
            }

            return OnTooltipAboutToSpawn();
        }
    }
}
