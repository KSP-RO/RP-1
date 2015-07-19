using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0.DummyModules
{
    class DummyModuleGeiger : DummyModule
    {
        public override string GetInfo()
        {
            return "Contains a Geiger-Müller tube";
        }
    }
}
