using System;
using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    public static class KSPUtils
    {
        /// <summary>
        /// Use this method instead of Planetarium.GetUniversalTime().
        /// Fixes the KSP stupidity where wrong UT can be returned when reverting back to the Editor.
        /// </summary>
        /// <returns></returns>
        public static double GetUT()
        {
            return HighLogic.LoadedSceneIsEditor ? HighLogic.CurrentGame.UniversalTime : Planetarium.GetUniversalTime();
        }
    }
}
