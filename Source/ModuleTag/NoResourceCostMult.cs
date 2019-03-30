using System;
using System.Collections.Generic;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagNoResourceCostMult : ModuleTag
    {
        public override string GetInfo()
        {
            return "Already requires checking by integration teams and so resource toxicity/cryogenic level will not add to launch costs.";
        }
    }
}
