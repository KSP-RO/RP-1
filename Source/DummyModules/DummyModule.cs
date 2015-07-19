using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0.DummyModules
{
    class DummyModule : PartModule, IModuleInfo
    {
        public string GetModuleTitle()
        {
            return "Contract Module";
        }
        public override string GetInfo()
        {
            return "NONE SPECIFIED";
        }
    }
}
