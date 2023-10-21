using System;
using System.Reflection;
using System.Linq;
using KERBALISM;

namespace RP0
{
    public static class KerbalismUtils
    {
        private static bool _needCheck = true;
        private static Version _version = null;
        private static Assembly _assembly = null;

        public static Assembly Assembly
        {
            get
            {
                Check();
                return _assembly;
            }
        }

        private static void Check()
        {
            if (_needCheck)
            {
                _needCheck = false;
                _assembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.name.StartsWith("Kerbalism", StringComparison.OrdinalIgnoreCase))?.assembly;
                if (_assembly != null)
                    _version = new Version(_assembly.GetName().Version.ToString());
                RP0Debug.Log("Kerbalism version: " + (_version?.ToString() ?? "assembly not found"), true);
            }
        }

        public static bool IsValidToPatch(Version v, bool isMax)
        {
            Check();

            if (_version == null)
                return false;

            return isMax ? _version <= v : _version >= v;
        }

        public static ExperimentSituations ToExperimentSituations(this KERBALISM.ScienceSituation sit)
        {
            int sitInt = 1 << (int)ScienceSituationUtils.ToValidStockSituation(sit);
            return (ExperimentSituations)sitInt;
        }

        private static bool _needRateStrings = true;
        private static string[] _rateStrings = new string[5];
        private static bool[] _rateMatches = new bool[5] { true, true, true, true, true };
        private static readonly double[] _rateMults = new double[5] {
            1d,
            1d / 60d,
            1d / (60d * 60d),
            1d / 86400d,
            1d / (86400d * 365d) };

        public static bool HumanRateToSI(ref string rate, string unit, double baseMult = 1d, int sigFigs = 3)
        {
            int numStart = -1, numStop = -1, rStart = -1, rEnd = -1;
            int len = rate.Length;
            if (_needRateStrings)
            {
                _needRateStrings = false;
                _rateStrings[0] = Local.Generic_perSecond;
                _rateStrings[1] = Local.Generic_perMinute;
                _rateStrings[2] = Local.Generic_perHour;
                _rateStrings[3] = Local.Generic_perDay;
                _rateStrings[4] = Local.Generic_perYear;
            }
            for (int i = 0; i < len; ++i)
            {
                if (rate[i] == '/')
                {
                    numStop = i - 1;
                    rStart = i;
                    for (int j = i + 1; j < len; ++j)
                    {
                        if (rate[j] == '<')
                        {
                            rEnd = j - 1;
                            break;
                        }
                    }
                    if (rEnd == -1)
                        rEnd = len - 1;

                    for (int j = i; j-- > 0;)
                    {
                        if (rate[j] == '>')
                        {
                            numStart = j + 1;
                            break;
                        }
                    }
                    if (numStart == -1)
                        numStart = 0;

                    break;
                }
            }

            if (numStop == -1)
                return false;

            int end = rEnd - rStart + 1;
            for (int i = 0; i < end; ++i)
            {
                char c = rate[i + rStart];
                for (int j = 0; j < 5; ++j)
                {
                    if (_rateMatches[j])
                    {
                        if (c != _rateStrings[j][i])
                            _rateMatches[j] = false;
                    }
                }
            }
            int rateIdx = 0;
            for (; rateIdx < 5; ++rateIdx)
            {
                if (_rateMatches[rateIdx])
                    break;
            }
            // reset for next
            _rateMatches[0] = _rateMatches[1] = _rateMatches[2] = _rateMatches[3] = _rateMatches[4] = true;

            // we could parse in place but eh.
            string numStr = rate.Substring(numStart, numStop - numStart + 1);
            if (!double.TryParse(numStr, out var value))
                return false;

            value *= _rateMults[rateIdx] * baseMult;

            string result = KSPUtil.PrintSI(value, unit, sigFigs);
            if (numStart > 0)
                result = rate.Substring(0, numStart) + result;
            if (rEnd + 1 < len)
                result += rate.Substring(rEnd + 1, len - rEnd - 1);

            rate = result;
            return true;
        }
    }
}
