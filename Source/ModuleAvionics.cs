using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleAvionics : PartModule
    {
        [KSPField]
        public float massLimit = float.MaxValue; // default is unlimited

        public override string GetInfo()
        {
            string retStr = "This part allows control of vessels of ";
            if (massLimit < float.MaxValue)
                retStr += "up to " + massLimit.ToString("N1") + " tons.";
            else
                retStr += "any mass.";
            return retStr;
        }
    }
}
