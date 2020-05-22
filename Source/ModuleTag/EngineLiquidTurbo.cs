using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagEngineLiquidTurbo : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains a pump-fed liquid rocket engine. The turbopump is a very complicated piece of machinery that requires extensive testing. Overall this will increase the launch costs.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 4</color></b>";
        }
    }
}
