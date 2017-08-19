using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP;

namespace RP0
{
    public class ModuleShowInfo : PartModule
    {
        public override string GetInfo()
        {
            string tmp = "";
            try
            {
                tmp += "Part name: " + part.name;
                // throws - tmp += "\nTech Required: " + part.partInfo.TechRequired;
                // throws - tmp += "\nEntry Cost: " + part.partInfo.entryCost;
            }
            catch (Exception e)
            {
                Debug.Log("**RP0 error getting info, exception " + e);
            }
            return tmp;
        }
    }
}
