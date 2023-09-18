using KerbalConstructionTime;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RP0
{
    public static class DTUtils
    {
        public static readonly DateTime Epoch = new DateTime(1951, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static readonly double DTMaxSeconds = DateTime.MaxValue.Ticks / TimeSpan.TicksPerSecond;

        private static readonly Regex _dtComponentRegex = new Regex("(?<y>[0-9]+)y|(?<d>[0-9]+)d|(?<h>[0-9]+)h|(?<m>[0-9]+)m|(?<s>[0-9]+)s", RegexOptions.Compiled);

        private const double MaxSecondsForDayDisplay = 7d * 86400d;
        private const double MaxTimeToDisplay = 100d * 365.25d * 86400d;

        public static DateTime UTToDate(double ut)
        {
            return Epoch.AddSeconds(ut);
        }

        public static string GetColonFormattedTime(double t, double extraTime = 0d, bool flip = false, bool showSeconds = true)
        {
            if (double.IsNaN(t) || double.IsInfinity(t))
                return "(infinity)";

            bool shouldUseDate = KCTGameStates.Settings.UseDates && t > MaxSecondsForDayDisplay;
            double timeCheck = (shouldUseDate ^ flip) ? extraTime + t : t;
            if (timeCheck > MaxTimeToDisplay)
                return "(infinity)";

            if (shouldUseDate ^ flip)
                return KSPUtil.dateTimeFormatter.PrintDateCompact(Planetarium.GetUniversalTime() + extraTime + t, false, showSeconds);

            return PrintTimeStampCompact(t, days: t > 86400d, years: false, seconds: showSeconds);
        }

        public static string GetFormattedTime(double t, double extraTime = 0d, bool allowDate = true)
        {
            if (double.IsNaN(t) || double.IsInfinity(t))
                return "(infinity)";

            bool shouldUseDate = KCTGameStates.Settings.UseDates && t > MaxSecondsForDayDisplay && allowDate;
            double timeCheck = shouldUseDate ? extraTime + t : t;
            if (timeCheck > MaxTimeToDisplay)
                return "(infinity)";

            if (shouldUseDate)
                return KSPUtil.dateTimeFormatter.PrintDate(Planetarium.GetUniversalTime() + extraTime + t, false, false);

            return KSPUtil.dateTimeFormatter.PrintTime(t, 4, explicitPositive: false);
        }

        public static GUIContent GetColonFormattedTimeWithTooltip(double t, string identifier, double extraTime = 0, bool showEst = false)
        {
            return new GUIContent(showEst ? $"Est: {GetColonFormattedTime(t, extraTime, flip: false, showSeconds: false)}" :
                                            GetColonFormattedTime(t, extraTime, flip: false, showSeconds: true),
                                  $"{identifier}¶{GetColonFormattedTime(t, extraTime, flip: true, showSeconds: true)}");
        }

        /// <summary>
        /// Converts a string containing time elements to UT.
        /// </summary>
        /// <param name="timeString"></param>
        /// <param name="isTimespan">If false will treat "1y 1d" as starting date (0). Otherwise will use 1 year and 1 day.</param>
        /// <param name="ut"></param>
        /// <returns>UT in seconds</returns>
        public static bool TryParseTimeString(string timeString, bool isTimespan, out double ut)
        {
            ut = 0;
            if (string.IsNullOrWhiteSpace(timeString)) return false;

            if (double.TryParse(timeString, out ut))
            {
                return true;
            }

            if (DateTime.TryParse(timeString, out DateTime dt))
            {
                ut = (dt - Epoch).TotalSeconds;
                return true;
            }

            try
            {
                MatchCollection matches = _dtComponentRegex.Matches(timeString);
                if (matches.Count == 0) return false;

                foreach (Match m in matches)
                {
                    ut += ParseDTComponent(m.Groups["y"], KSPUtil.dateTimeFormatter.Year, isTimespan);
                    ut += ParseDTComponent(m.Groups["d"], KSPUtil.dateTimeFormatter.Day, isTimespan);
                    ut += ParseDTComponent(m.Groups["h"], KSPUtil.dateTimeFormatter.Hour, false);
                    ut += ParseDTComponent(m.Groups["m"], KSPUtil.dateTimeFormatter.Minute, false);
                    ut += ParseDTComponent(m.Groups["s"], 1, false);
                }
            }
            catch
            {
                ut = 0;
                return false;
            }

            return true;
        }

        public static bool IsInvalidTime(double time)
        {
            return double.IsNaN(time) || double.IsPositiveInfinity(time) || double.IsNegativeInfinity(time) ||
                time > DTMaxSeconds;
        }

        private static string InvalidTimeStr(double time)
        {
            if (double.IsNaN(time))
            {
                return "NaN";
            }
            if (double.IsPositiveInfinity(time) || time > DTMaxSeconds)
            {
                return "+Inf";
            }
            if (double.IsNegativeInfinity(time))
            {
                return "-Inf";
            }
            return null;
        }

        /// <summary>
        /// Similar to the method in RSSTimeFormatter but is capable of omitting seconds.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="days"></param>
        /// <param name="years"></param>
        /// <param name="seconds"></param>
        /// <returns></returns>
        private static string PrintTimeStampCompact(double time, bool days = false, bool years = false, bool seconds = true)
        {
            // short-circuit if invalid time passed
            if (IsInvalidTime(time))
                return InvalidTimeStr(time);
            DateTime epoch = Epoch;
            DateTime target = epoch.AddSeconds(time);
            TimeSpan span = target - epoch;
            int dNum = span.Days;
            int yNum = dNum / 365;
            int subDays = dNum - yNum * 365;
            return string.Format("{0}{1}{2:D2}:{3:D2}{4}"
                , years ? string.Format("{0}y, ", yNum) : ""
                , days ? string.Format("{0}d, ", (years && subDays != 0) ? subDays : dNum) : ""
                , span.Hours
                , span.Minutes
                , seconds ? string.Format(":{0:D2}", span.Seconds) : ""
            );
        }

        private static int ParseDTComponent(Group g, int factor, bool substract1)
        {
            if (!string.IsNullOrEmpty(g.Value))
            {
                int val = int.Parse(g.Value);
                if (substract1) val--;
                return val * factor;
            }
            return 0;
        }
    }
}
