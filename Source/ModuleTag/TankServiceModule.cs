using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagTankServiceModule : ModuleTag
    {
        public override string GetInfo()
        {
            string str = string.Empty;
            str = "Contains a Service Module Fuel Tank. These tanks are specially designed to hold many different resources so it increases the overall rollout cost.\n\n" +
                "<b><color=orange>Rollout Cost: Cost of This Part * 2.0</color></b>";
            return str;
        }
    }
}
