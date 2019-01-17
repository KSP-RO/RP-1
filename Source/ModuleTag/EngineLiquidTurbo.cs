using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagEngineLiquidTurbo : ModuleTag
    {
        public override string GetInfo()
        {
            string str = string.Empty;
            str = "Contains a pump-fed liquid rocket engine. The turbopump is a very complicated piece of machinery that requires extensive testing. Overall this will increase the rollout costs.\n\n" +
                "<b><color=orange>Rollout Cost: Cost of This Part * 4.0</color></b>";
            return str;
        }
    }
}
