using System;
using System.Reflection;
using UnityEngine;
using ROUtils;

namespace RP0
{
    public static class RP0DTUtils
    {
        /// <summary>
        /// Durations longer than this are not passed on to the date formatter.
        /// RSSTimeFormatter turns whatever it's given into a DateTime (by adding the seconds to its epoch) and
        /// thus throws an ArgumentOutOfRangeException as soon as the result no longer fits within DateTime's
        /// range. Nearly all of the printing happens from GUI code where such an exception blanks the entire
        /// window, so print a placeholder instead. For durations the limit is deliberately far tighter than what
        /// DateTime supports, since nothing in RP-1 has a legitimate use for one this long.
        /// </summary>
        private const double MaxDurationToDisplay = 1000d * 365.25d * 86400d;

        /// <summary>
        /// Safety margin kept away from the exact UT where the date formatter starts throwing.
        /// </summary>
        private const double DateLimitMargin = 86400d;

        private const string InfinityStr = "(infinity)";
        private const string NeverStr = "(never)";

        private static IDateTimeFormatter _formatterForLimits;
        private static double _maxDateUT = double.MaxValue;
        private static double _minDateUT = double.MinValue;

        /// <summary>
        /// Whether the given duration is too long (or too invalid) to be worth printing.
        /// </summary>
        public static bool IsDurationOutOfRange(double time)
        {
            // Written as a negated range check so that NaN is caught as well.
            return !(time > -MaxDurationToDisplay && time < MaxDurationToDisplay);
        }

        /// <summary>
        /// Whether the given UT falls outside the range of dates that the current formatter is able to print.
        /// </summary>
        public static bool IsDateOutOfRange(double ut)
        {
            UpdateDateLimits();
            // Written as a negated range check so that NaN is caught as well.
            return !(ut > _minDateUT && ut < _maxDateUT);
        }

        /// <summary>
        /// Figures out the UT range that the current date formatter is able to handle. Formatters based on
        /// DateTime (RSSTimeFormatter and the like) throw once epoch + UT no longer fits within DateTime, and the
        /// epoch they use isn't exposed through IDateTimeFormatter - so fetch it through reflection. The stock
        /// formatters don't do any DateTime conversion at all and thus have no range to speak of.
        /// </summary>
        private static void UpdateDateLimits()
        {
            IDateTimeFormatter formatter = KSPUtil.dateTimeFormatter;
            if (ReferenceEquals(formatter, _formatterForLimits))
                return;

            _formatterForLimits = formatter;
            _maxDateUT = double.MaxValue;
            _minDateUT = double.MinValue;

            if (TryGetEpoch(formatter, out DateTime epoch))
            {
                _maxDateUT = (DateTime.MaxValue - epoch).TotalSeconds - DateLimitMargin;
                _minDateUT = (DateTime.MinValue - epoch).TotalSeconds + DateLimitMargin;
                RP0Debug.Log($"Date formatter {formatter.GetType()} uses epoch {epoch:yyyy-MM-dd}, printable UT range is {_minDateUT:N0} to {_maxDateUT:N0}");
            }
        }

        private static bool TryGetEpoch(IDateTimeFormatter formatter, out DateTime epoch)
        {
            epoch = default;
            if (formatter == null)
                return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = formatter.GetType().GetField("epoch", flags);
            if (field == null || field.FieldType != typeof(DateTime))
            {
                // Fall back to a lone DateTime field, in case another formatter names it differently.
                field = null;
                foreach (FieldInfo fi in formatter.GetType().GetFields(flags))
                {
                    if (fi.FieldType != typeof(DateTime))
                        continue;

                    if (field != null)
                        return false;    // More than one candidate, don't guess.

                    field = fi;
                }
                if (field == null)
                    return false;
            }

            epoch = (DateTime)field.GetValue(formatter);
            return true;
        }

        public static string PrintDate(double time, bool includeTime, bool includeSeconds = false, string outOfRangeStr = NeverStr)
        {
            return IsDateOutOfRange(time) ? outOfRangeStr : KSPUtil.PrintDate(time, includeTime, includeSeconds);
        }

        public static string PrintDateNew(double time, bool includeTime, string outOfRangeStr = NeverStr)
        {
            return IsDateOutOfRange(time) ? outOfRangeStr : KSPUtil.PrintDateNew(time, includeTime);
        }

        public static string PrintDateCompact(double time, bool includeTime, bool includeSeconds = false, string outOfRangeStr = NeverStr)
        {
            return IsDateOutOfRange(time) ? outOfRangeStr : KSPUtil.PrintDateCompact(time, includeTime, includeSeconds);
        }

        public static string PrintDateDelta(double time, bool includeTime, bool includeSeconds = false, bool useAbs = false, string outOfRangeStr = InfinityStr)
        {
            return IsDurationOutOfRange(time) ? outOfRangeStr : KSPUtil.PrintDateDelta(time, includeTime, includeSeconds, useAbs);
        }

        public static string PrintDateDeltaCompact(double time, bool includeTime, bool includeSeconds, bool useAbs = false, string outOfRangeStr = InfinityStr)
        {
            return IsDurationOutOfRange(time) ? outOfRangeStr : KSPUtil.PrintDateDeltaCompact(time, includeTime, includeSeconds, useAbs);
        }

        public static string PrintTime(double time, int valuesOfInterest, bool explicitPositive, string outOfRangeStr = InfinityStr)
        {
            return IsDurationOutOfRange(time) ? outOfRangeStr : KSPUtil.PrintTime(time, valuesOfInterest, explicitPositive);
        }

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
