using System;
using System.Text.RegularExpressions;
using UnityEngine;
using ROUtils;

namespace RP0
{
    public static class RP0DTUtils
    {
        public static string GetColonFormattedTime(double t, double extraTime = 0d, bool flip = false, bool showSeconds = true)
        {
            return DTUtils.GetColonFormattedTime(t, KCTSettings.Instance.UseDates, extraTime, flip, showSeconds);
        }

        public static string GetFormattedTime(double t, double extraTime = 0d, bool allowDate = true)
        {
            return DTUtils.GetFormattedTime(t, KCTSettings.Instance.UseDates && allowDate, extraTime);
        }

        public static GUIContent GetColonFormattedTimeWithTooltip(double t, string identifier, double extraTime = 0, bool showEst = false)
        {
            return DTUtils.GetColonFormattedTimeWithTooltip(t, identifier, KCTSettings.Instance.UseDates, extraTime, showEst);
        }
    }
}
