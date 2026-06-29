using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UniLinq;
using KACAlarm = RP0.KACWrapper.KACAPI.KACAlarm;
using KACAPI = RP0.KACWrapper.KACAPI;

namespace RP0.ModIntegrations
{
    /// <summary>
    /// Details pulled from a KAC transfer-window alarm. Numeric fields are NaN when they couldn't be
    /// determined (e.g. a non-TWP alarm with no parseable Notes).
    /// </summary>
    public struct KacTransferWindow
    {
        public string AlarmName { get; set; }
        public string OriginBodyName { get; set; }
        public double DepartureUT { get; set; }     // UT of the ejection burn
        public double TransferSeconds { get; set; } // travel time
        public double InsertionDV { get; set; }     // m/s
        public double PeriapsisAltKm { get; set; }  // capture periapsis altitude at the target
    }

    /// <summary>
    /// Adds methods for using alarms with KAC if available, otherwise falls back to the stock alarm clock.
    /// </summary>
    public static class AlarmHelper
    {
        private static bool UseKAC => KACWrapper.APIReady;

        /// <summary>
        /// Finds a KAC transfer-window alarm whose destination is <paramref name="targetBodyName"/> and
        /// extracts its transfer details. Origin/target come from the alarm's fields; departure UT,
        /// travel time, insertion ΔV and periapsis altitude are parsed from TWP's alarm Notes
        /// (GenerateTransferDetailsText). Any field that can't be parsed is left as NaN. Returns false if
        /// KAC isn't available or no matching transfer alarm exists. Stock alarms have no transfer type,
        /// so this only works with KAC installed.
        /// </summary>
        public static bool TryGetTransferWindowToTarget(string targetBodyName, string originBodyName, out KacTransferWindow info)
        {
            info = new KacTransferWindow
            {
                DepartureUT = double.NaN,
                TransferSeconds = double.NaN,
                InsertionDV = double.NaN,
                PeriapsisAltKm = double.NaN,
            };
            if (!UseKAC || string.IsNullOrEmpty(targetBodyName)) return false;

            double now = Planetarium.GetUniversalTime();
            KACAlarm best = null;
            foreach (KACAlarm a in KACWrapper.KAC.Alarms)
            {
                if (!IsTransferAlarmTo(a, targetBodyName, originBodyName)) continue;
                if (best == null || IsBetterWindow(a, best, now)) best = a;
            }
            if (best == null) return false;

            info.AlarmName = best.Name;
            info.OriginBodyName = best.XferOriginBodyName;
            ParseTransferNote(best.Notes, ref info);
            return true;
        }

        private static bool IsTransferAlarmTo(KACAlarm a, string targetBodyName, string originBodyName)
        {
            if (a.AlarmType != KACAPI.AlarmTypeEnum.Transfer && a.AlarmType != KACAPI.AlarmTypeEnum.TransferModelled)
                return false;
            if (!string.Equals(a.XferTargetBodyName, targetBodyName, StringComparison.OrdinalIgnoreCase))
                return false;
            // If an origin body is selected, the alarm's origin must match it too.
            return string.IsNullOrEmpty(originBodyName)
                || string.Equals(a.XferOriginBodyName, originBodyName, StringComparison.OrdinalIgnoreCase);
        }

        // Prefer the soonest still-upcoming window; otherwise the most recent past one.
        private static bool IsBetterWindow(KACAlarm candidate, KACAlarm current, double now)
        {
            bool candFuture = candidate.AlarmTime >= now, curFuture = current.AlarmTime >= now;
            if (candFuture && curFuture) return candidate.AlarmTime < current.AlarmTime;
            if (candFuture) return true;
            if (curFuture) return false;
            return candidate.AlarmTime > current.AlarmTime;
        }

        // Note regexes are precompiled and time-bounded so a pathological note can't hang the editor (S6444).
        // They key off symbols/units only ("UT:", "m/s", "@…km", "->"), never localizable label words.
        private static readonly TimeSpan NoteRegexTimeout = TimeSpan.FromMilliseconds(200);
        private static readonly Regex UtRegex = new Regex(@"UT:\s*([0-9]+(?:\.[0-9]+)?)", RegexOptions.None, NoteRegexTimeout);
        private static readonly Regex MetresPerSecRegex = new Regex(@"([0-9]+(?:\.[0-9]+)?)\s*m/s", RegexOptions.None, NoteRegexTimeout);
        private static readonly Regex PeriapsisRegex = new Regex(@"->[^(]*\(@\s*([0-9]+(?:\.[0-9]+)?)\s*km", RegexOptions.IgnoreCase, NoteRegexTimeout);

        // Parses TWP's alarm Notes, e.g.:
        //   Earth (@150km) -> Mars (@35km)
        //   Depart at: 5 Sep 1975, 05:39:25
        //   UT: 778743565
        //   Travel: 346 days , 04:02:28
        //   UT: 29908948
        //   Arrive at: ...
        //   Ejection Δv: 3832 m/s
        //   Insertion Δv: 2072 m/s
        //   Total Δv: 5904 m/s
        // Language-agnostic: the "UT:" values appear in a fixed order (departure, travel, arrival) and the
        // "m/s" values in a fixed order (ejection, insertion, total), so we read them positionally rather
        // than by the localizable labels. We take UT #0/#1 and m/s #1 (insertion). Periapsis is the "@…km"
        // after the "->" on the title line (symbols, not words).
        private static void ParseTransferNote(string notes, ref KacTransferWindow info)
        {
            if (string.IsNullOrEmpty(notes)) return;

            int utIndex = 0, dvIndex = 0;
            foreach (string raw in notes.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();

                Match utm = UtRegex.Match(line);
                if (utm.Success && double.TryParse(utm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double ut))
                {
                    if (utIndex == 0) info.DepartureUT = ut;
                    else if (utIndex == 1) info.TransferSeconds = ut;
                    utIndex++;
                }

                Match dvm = MetresPerSecRegex.Match(line);
                if (dvm.Success && double.TryParse(dvm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv))
                {
                    if (dvIndex == 1) info.InsertionDV = dv;  // ejection (#0), insertion (#1), total (#2)
                    dvIndex++;
                }
            }

            Match pm = PeriapsisRegex.Match(notes);
            if (pm.Success && double.TryParse(pm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pe))
                info.PeriapsisAltKm = pe;
        }

        /// <summary>
        /// Creates an alarm.
        /// </summary>
        /// <remarks>
        /// Do not pass in Contract or ContractAuto as the alarmType, KAC immediately deletes the alarm due to https://github.com/linuxgurugamer/KerbalAlarmClock/issues/14.
        /// </remarks>
        /// <returns>
        /// AlarmID
        /// </returns>
        public static string CreateAlarm(string title, string description, double UT, KACAPI.AlarmTypeEnum alarmType = KACAPI.AlarmTypeEnum.Raw)
        {
            if (UseKAC)
            {
                if (alarmType == KACAPI.AlarmTypeEnum.Contract || alarmType == KACAPI.AlarmTypeEnum.ContractAuto)
                {
                    alarmType = KACAPI.AlarmTypeEnum.Raw;
                }
                string alarmID = KACWrapper.KAC.CreateAlarm(alarmType, title, UT);
                if (!string.IsNullOrEmpty(alarmID))
                {
                    KACAlarm alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.ID == alarmID);

                    if (alarm != null)
                    {
                        alarm.AlarmAction = KACAPI.AlarmActionEnum.KillWarp;
                        alarm.Notes = description;
                        RP0Debug.Log($"Alarm created with ID: {alarmID}");
                        return alarmID;
                    }
                    else
                    {
                        RP0Debug.LogError($"KAC Alarm with ID: {alarmID} could not be found.");
                        return alarmID;
                    }
                }
                else
                {
                    RP0Debug.LogError($"KAC Alarm could not be created.");
                    return "";
                }
            }
            else
            {
                AlarmTypeRaw alarm = new AlarmTypeRaw
                {
                    title = title,
                    description = description,
                    ut = UT,
                };
                AlarmClockScenario.AddAlarm(alarm);
                alarm.actions.warp = AlarmActions.WarpEnum.KillWarp;
                if (alarm.Id != 0u)
                {
                    string alarmID = alarm.Id.ToString();
                    RP0Debug.Log($"Alarm created with ID: {alarmID}");
                    return alarmID;
                }
                else
                {
                    RP0Debug.LogError($"Stock Alarm could not be created.");
                    return "";
                }
            }
        }

        /// <summary>
        /// Finds the alarm with a certain id and updates its properties.
        /// </summary>
        /// <returns>
        /// Success
        /// </returns>
        public static bool ChangeAlarm(string id, string title, string description, double UT)
        {
            if (UseKAC)
            {
                KACAlarm alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.ID == id);
                if (alarm == null)
                {
                    RP0Debug.LogError($"KAC Alarm with ID: {id} could not be found.");
                    return false;
                }
                alarm.Name = title;
                alarm.Notes = description;
                alarm.AlarmTime = UT;
                // KAC does not let you change the alarm type of an alarm after it has been made
                RP0Debug.Log($"Alarm with ID: {id} changed to have title: {title}, description: {description}, and time: {UT}.");
                return true;
            }
            else
            {
                if (!uint.TryParse(id, out uint alarmID))
                {
                    RP0Debug.LogError("Could not parse stock alarm ID " + id + " to uint");
                    return false;
                }
                if (!AlarmClockScenario.Instance.alarms.TryGetValue(alarmID, out AlarmTypeBase alarm) || alarm == null)
                {
                    RP0Debug.LogError("Could not find stock alarm with ID " + id);
                    return false;
                }
                alarm.title = title;
                alarm.description = description;
                alarm.ut = UT;
                RP0Debug.Log($"Alarm with ID: {id} changed to have title: {title}, description: {description}, and time: {UT}.");
                return true;
            }
        }

        /// <summary>
        /// Deletes the alarm with a certain id.
        /// </summary>
        /// <returns>
        /// Success
        /// </returns>
        public static bool DeleteAlarmWithID(string id)
        {
            bool successful;
            if (UseKAC)
            {
                successful = KACWrapper.KAC.DeleteAlarm(id);
                RP0Debug.Log((successful ? "Alarm deleted with ID: " : "Could not delete alarm with ID: ") + id);
                return successful;
            }
            else
            {
                if (uint.TryParse(id, out uint alarmID))
                {
                    successful = AlarmClockScenario.DeleteAlarm(alarmID);
                    RP0Debug.Log((successful ? "Alarm deleted with ID: " : "Could not delete alarm with ID: ") + id);
                    return successful;
                }
                else
                {
                    RP0Debug.LogError("Could not parse stock alarm ID " + id + " to uint");
                    return false;
                }
            }
        }

        /// <summary>
        /// Deletes all alarms that contain (or start with) a certain string in their title.
        /// </summary>
        /// <returns>
        /// Success
        /// </returns>
        public static bool DeleteAllAlarmsWithTitle(string title, bool useStartsWith = false)
        {
            bool successful = true;
            bool foundAlarm = false;
            bool hasTitle(string alarmTitle) => alarmTitle != null && (useStartsWith ? alarmTitle.StartsWith(title) : alarmTitle.Contains(title));
            if (UseKAC)
            {
                foreach (KACAlarm alarm in KACWrapper.KAC.Alarms.Where(a => hasTitle(a.Name)).ToList())
                {
                    foundAlarm = true;
                    string id = alarm.ID;
                    if (!KACWrapper.KAC.DeleteAlarm(id))
                    {
                        RP0Debug.LogError($"Could not delete alarm with ID: {id}");
                        successful = false;
                    }
                    else RP0Debug.Log($"Alarm deleted with ID: {id}");
                }
                if (!foundAlarm)
                {
                    RP0Debug.LogWarning($"No alarms found with title: {title}");
                    successful = false;
                }
                return successful;
            }
            else
            {
                foreach (uint id in AlarmClockScenario.Instance.alarms.Keys
                    .Where(id => AlarmClockScenario.Instance.alarms.TryGetValue(id, out AlarmTypeBase alarm) && hasTitle(alarm.title)).ToList())
                {
                    foundAlarm = true;
                    if (!AlarmClockScenario.DeleteAlarm(id))
                    {
                        RP0Debug.LogError($"Could not delete alarm with ID: {id}");
                        successful = false;
                    }
                    else RP0Debug.Log($"Alarm deleted with ID: {id}");
                }
                if (!foundAlarm)
                {
                    RP0Debug.LogWarning($"No alarms found with title: {title}");
                    successful = false;
                }
                return successful;
            }
        }

        /// <summary>
        /// Determines if an alarm with a certain id exists.
        /// </summary>
        /// <returns>
        /// Alarm exists
        /// </returns>
        public static bool AlarmExistsID(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (UseKAC)
            {
                KACAlarm alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.ID == id);
                return alarm != null;
            }
            else
            {
                if (uint.TryParse(id, out uint alarmID))
                {
                    return AlarmClockScenario.Instance.alarms.ContainsKey(alarmID);
                }
                else
                {
                    RP0Debug.LogError("Could not parse stock alarm ID " + id + " to uint");
                    return false;
                }
            }
        }

        /// <summary>
        /// Determines if an alarm that contains a certain string in its title exists.
        /// </summary>
        /// <returns>
        /// Alarm exists, out parameter id as string
        /// </returns>
        public static bool AlarmExistsTitle(string title, out string id)
        {
            id = "";
            if (string.IsNullOrEmpty(title)) return false;
            if (UseKAC)
            {
                KACAlarm alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.Name.Contains(title));
                if (alarm == null) return false;
                else
                {
                    id = alarm.ID;
                    return true;
                }
            }
            else
            {
                foreach (uint alarmID in AlarmClockScenario.Instance.alarms.Keys)
                {
                    if (AlarmClockScenario.Instance.alarms.TryGetValue(alarmID, out AlarmTypeBase alarm) && alarm.title != null && alarm.title.Contains(title))
                    {
                        id = alarmID.ToString();
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
