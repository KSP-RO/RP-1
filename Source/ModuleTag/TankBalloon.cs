using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagTankBalloon : ModuleTag
    {
        public override string GetInfo()
        {
            string str = string.Empty;
            str = "Contains a Balloon Tank which are very difficult to handle properly and must be pressurized properly at all times increasing the overall rollout costs.\n\n" +
                "<b><color=orange>Rollout Cost: Cost of This Part * 2.5</color></b>";
            return str;
        }
    }
}
