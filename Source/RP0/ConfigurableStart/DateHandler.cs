using System;
using System.Text.RegularExpressions;

namespace RP0.ConfigurableStart
{
    public class DateHandler
    {
        private static readonly IDateTimeFormatter _timeFormatter = KSPUtil.dateTimeFormatter;

        public static long Epoch { get; set; } = 0;
        public static string GetFormattedDateString(long UT) => _timeFormatter.PrintDateCompact(UT, true);

        public static string GetDatePreview(string dateString)
        {
            if (!long.TryParse(dateString, out long newUT))
            {
                if (TryParseStockDate(dateString, out newUT)) { }
                else if (TryParseDate(dateString, out DateTime newEpoch))
                {
                    DateTime gameStart = new DateTime(1951, 01, 01, 0, 0, 0);

                    TimeSpan span = newEpoch.Subtract(gameStart);
                    newUT = TimeSpanToSeconds(span);
                }
                else { newUT = Epoch; }
            }

            if (newUT != 0)
                return GetFormattedDateString(newUT);
            
            return "Invalid date";
        }

        public static long GetUTFromDate(string dateString)
        {
            if (!long.TryParse(dateString, out long newUT))
            {
                if (TryParseStockDate(dateString, out newUT))
                {
                    RP0Debug.Log("Stock date format detected");
                }
                else if (TryParseDate(dateString, out DateTime newEpoch))
                {
                    RP0Debug.Log("RSS date format detected");

                    DateTime gameStart = new DateTime(1951, 01, 01, 0, 0, 0);

                    TimeSpan span = newEpoch.Subtract(gameStart);
                    newUT = TimeSpanToSeconds(span);
                }
                else
                {
                    RP0Debug.LogWarning("Unsupported date format");
                    newUT = Epoch;
                }
            }
            else
            {
                RP0Debug.Log("Epoch format detected");
            }

            return newUT;
        }

        private static long TimeSpanToSeconds(TimeSpan span)
        {
            long years = span.Days / 365;
            long days = span.Days - years * 365;
            long hours = span.Hours;
            long minutes = span.Minutes;
            long seconds = span.Seconds;

            long totalSeconds = years * _timeFormatter.Year + days * _timeFormatter.Day + hours * _timeFormatter.Hour + minutes * _timeFormatter.Minute + seconds;

            return totalSeconds;
        }

        private static bool TryParseDate(string dateString, out DateTime dateTime)
        {
            try
            {
                dateTime = DateTime.Parse(dateString);
                return true;
            }
            catch
            {
                dateTime = DateTime.MinValue;
                return false;
            }
        }

        private static bool TryParseStockDate(string dateString, out long newUT)
        {
            newUT = Epoch;
            var pattern = new Regex(@"^[Y]?\d{4}\-[dD]?\d+$");
            Match m = pattern.Match(dateString);

            if (m.Success)
            {
                string[] yearDayArray = m.Value.Split(new char[] { '-' }, 2);
                if (!long.TryParse(yearDayArray[0], out long years))
                    return false;
                if (!long.TryParse(yearDayArray[1], out long days))
                    return false;

                newUT += --years * _timeFormatter.Year + --days * _timeFormatter.Day;
            }

            return m.Success;
        }
    }
}
