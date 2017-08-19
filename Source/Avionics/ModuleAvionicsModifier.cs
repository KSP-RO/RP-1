using System;
using System.Collections.Generic;
using System.Text;
using KSP;
using UnityEngine;

namespace RP0
{
    class ModuleAvionicsModifier : PartModule
    {
        [KSPField]
        public float multiplier = 1f;
        public override string GetInfo()
        {
            return "This part contributes " + multiplier.ToString("P") + " of its mass to avionics requirements.";
        }
    }
}
