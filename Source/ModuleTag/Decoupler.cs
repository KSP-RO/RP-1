using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagDecoupler : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains a decoupler that requires extensive integration and testing which increase the overall launch cost.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 3</color></b>";
        }
    }
}
