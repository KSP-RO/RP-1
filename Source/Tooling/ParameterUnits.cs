using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0.Tooling
{
    public static class Parameters
    {
        //Headings
        private static Parameter[] DiameterLengthParameters = new[] {new Parameter("Diameter", "m"), new Parameter("Length", "m") };
        private static Parameter[] AvionicsParameters = new[] { new Parameter("Diameter", "m"), new Parameter("Length", "m"), new Parameter("Contr. Mass", "t") };

        public static Parameter[] GetParametersForToolingType(string type)
        {
            var mainType = type.Substring(0, Math.Max(type.IndexOf('-'), 0));
            switch (mainType)
            {
                case ModuleToolingProcAvionics.MainToolingType:
                    return AvionicsParameters;
                default:
                    return DiameterLengthParameters;
            }
        }
    }
}