using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleScienceCore : PartModule
    {
        public override string GetInfo()
        {
            return "This part alone only allows limited command interaction; you will not be allowed full attitude control unless another command part without this module is attached.";
        }
    }
}
