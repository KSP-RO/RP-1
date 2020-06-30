using System;

namespace RP0.Tooling
{
    public static class Parameters
    {
        //Headings
        private static readonly Parameter[] _diameterLengthParameters = new[] { new Parameter("Diameter", "m"), new Parameter("Length", "m") };
        private static readonly Parameter[] _avionicsParameters = new[] { new Parameter("Contr. Mass", "t"), new Parameter("Diameter", "m"), new Parameter("Length", "m") };

        public static Parameter[] GetParametersForToolingType(string type)
        {
            var mainType = type.Substring(0, Math.Max(type.IndexOf('-'), 0));
            switch (mainType)
            {
                case ModuleToolingProcAvionics.MainToolingType:
                    return _avionicsParameters;
                default:
                    return _diameterLengthParameters;
            }
        }
    }
}