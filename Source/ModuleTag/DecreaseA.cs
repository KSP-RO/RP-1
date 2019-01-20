using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagDecreaseA : ModuleTag
    {
        public override string GetInfo()
        {
            string str = string.Empty;
            str = "Contains equipment that is easy to get ready for launch. Currently this does not multiply Rollout Costs.\n\n" +
                "<b><color=orange>Rollout Cost: Cost of This Part * 1.0</color></b>";
            return str;
        }
    }
}
