using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0.ModuleTags
{
    public class ModuleTagHabitable : ModuleTag
    {
        public override string GetInfo()
        {
            return "Contains habitable volume";
        }
    }
}
