using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagHabitable : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains habitable volume which requires extensive integration and testing of safety features. This greatly affects the overall launch cost.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 4</color></b>";
        }
    }
}
