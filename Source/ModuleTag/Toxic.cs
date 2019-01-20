using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagToxic : ModuleTag
    {
        public override string GetInfo()
        {
            string str = string.Empty;
            str = "Uses toxic propellants that need to be handled carefully prior to launch. Currently, these do not multiply rollout costs.\n\n" +
                "<b><color=orange>Rollout Cost: Cost of This Part * 1.0</color></b>";
            return str;
        }
    }
}
