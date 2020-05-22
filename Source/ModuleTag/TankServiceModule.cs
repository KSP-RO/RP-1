using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagTankServiceModule : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains a Service Module Fuel Tank. These tanks are specially designed to hold many different resources so it increases the overall launch cost.\n\n" +
                   "<b><color=orange>Launch Cost: Cost of This Part * 2</color></b>";
        }
    }
}
