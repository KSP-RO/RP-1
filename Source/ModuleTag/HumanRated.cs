using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagHumanRated : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains human-rated equipment that requires more time for integration and testing. Overall this raises the launch costs by a VERY large amount.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of ALL Parts * 1.25</color></b>";
        }
    }
}
