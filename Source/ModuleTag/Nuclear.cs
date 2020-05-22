using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagNuclear : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains radioactive elements that must be carefully handled and integrated. Additional permits need to be secure increasing launch costs.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 5, cost of ALL Parts * 1.5</color></b>";
        }
    }
}
