using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagNuclearRTG : ModuleTag
    {
        public override string GetInfo()
        {
            string str = string.Empty;
            str = "Contains radioactive elements that must be carefully handled and integrated. Additional permits need to be secure increasing rollout costs.\n\n" +
                "<b><color=orange>Rollout Cost: Cost of This Part * 3.0</color></b>";
            return str;
        }
    }
}
