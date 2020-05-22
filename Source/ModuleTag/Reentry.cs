using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagReentry : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains reentry equipment that needs proper integration and testing for safety. It will increase the overall launch cost.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 1.5</color></b>";
        }
    }
}
