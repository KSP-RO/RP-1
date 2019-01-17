using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagHabitable : ModuleTag
    {
        public override string GetInfo()
        {
            string str = string.Empty;
            str = "Contains habitable volume which requires extensive integration and testing of safety features. This greatly affects the overall rollout cost.\n\n" +
                "<b><color=orange>Rollout Cost: Cost of This Part * 4.0</color></b>";
            return str;
        }
    }
}
