using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagHumanRated : ModuleTag
    {
        public override string GetInfo()
        {
            string str = string.Empty;
            str = "Contains human-rated equipment that requires more time for integration and testing. Overall this raises the rollout costs by a VERY large amount.\n\n" +
                "<b><color=orange>Rollout Cost: Cost of ALL Parts * 1.25</color></b>";
            return str;
        }
    }
}
