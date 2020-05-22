using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagEngineSolid : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains a solid rocket motor. These do not multiply the launch costs as they are much easier to integrate and test than liquid engines.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 1.05</color></b>";
        }
    }
}
