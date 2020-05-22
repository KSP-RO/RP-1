using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagToxic : ModuleTag
    {
        public override string GetInfo()
        {
            return "Uses toxic propellants that need to be handled carefully prior to launch. Currently, these do not multiply launch costs.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 1</color></b>";
        }
    }
}
