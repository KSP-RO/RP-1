using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagInstruments : ModuleTag
    {
        public override string GetInfo()
        {
            string str = string.Empty;
            str = "Contains scientific instruments. Currently, these do not multiply rollout costs.\n\n" +
                "<b><color=orange>Rollout Cost: Cost of This Part * 1.0</color></b>";
            return str;
        }
    }
}
