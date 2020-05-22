using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagEngineLiquidPF : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains a pressure-fed liquid rocket engine that requires some additional testing. Overall this will increase the launch costs.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 1.75</color></b>";
        }
    }
}
