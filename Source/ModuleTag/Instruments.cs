using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagInstruments : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains scientific instruments. Currently, these do not multiply launch costs.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 1</color></b>";
        }
    }
}
