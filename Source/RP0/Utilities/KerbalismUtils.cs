using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public static class KerbalismUtils
    {
        public static ExperimentSituations ToExperimentSituations(this KERBALISM.ScienceSituation sit)
        {
            int sitInt = 1 << (int)KERBALISM.ScienceSituationUtils.ToValidStockSituation(sit);
            return (ExperimentSituations)sitInt;
        }
    }
}
