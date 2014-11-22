using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace RP0
{
    class ModuleShowInfo : PartModule
    {
        public override string GetInfo()
        {
            return "Part name: " + part.partName + "\nTech Required: " + part.partInfo.TechRequired + "\nEntry Cost: " + part.partInfo.entryCost;
        }
    }
}
