using UnityEngine;

namespace KerbalConstructionTime
{
    public static class KCTDebug
    {
        public static void LogError(object message)
        {
            Log(message, true);
        }

        public static void Log(object message, bool always = false)
        {
        #if DEBUG
            bool isBetaVersion = true;
        #else
            bool isBetaVersion = always;
        #endif
            if (KCTGameStates.Settings.Debug || isBetaVersion)
            {
                Debug.Log("[KCT] " + message);
            }
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
