using System.Collections.Generic;
using System.Linq;

namespace RP0.ModIntegrations
{
    public static class KACMethods
    {
        /// <summary>
        /// Creates an alarm.
        /// Uses KAC if available, otherwise falls back to the stock alarm clock.
        /// </summary>
        /// <returns>
        /// AlarmID
        /// </returns>
        public static string CreateAlarm(string title, string description, double UT, KACWrapper.KACAPI.AlarmTypeEnum alarmType = KACWrapper.KACAPI.AlarmTypeEnum.Raw)
        { // dont set alarmType to contract, it seems to immediately delete itself?
            string s = "Alarm created with ID: ";
            if (KACWrapper.APIReady)
            {
                string alarmID = KACWrapper.KAC.CreateAlarm(alarmType, title, UT);
                if (!string.IsNullOrEmpty(alarmID))
                {
                    KACWrapper.KACAPI.KACAlarm alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.ID == alarmID);

                    alarm.AlarmAction = KACWrapper.KACAPI.AlarmActionEnum.KillWarp;
                    alarm.Notes = description;
                }
                RP0Debug.Log(s + alarmID);
                return alarmID;
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
                string alarmID = alarm.Id.ToString();
                RP0Debug.Log(s + alarmID);
                return alarmID;
            }
        }

        /// <summary>
        /// Deletes all alarms that contain (or start with) a certain string in their title.
        /// Uses KAC if available, otherwise falls back to the stock alarm clock.
        /// </summary>
        /// <returns>
        /// Success
        /// </returns>
        public static bool DeleteAllAlarmsWithTitle(string title, bool useStartsWith = false)
        {
            string s1 = "Alarm deleted with ID: ";
            string s2 = "Could not delete alarm with ID: ";
            bool successful = true;
            if (KACWrapper.APIReady)
            {
                bool predicate(KACWrapper.KACAPI.KACAlarm a) => useStartsWith ? a.Name.StartsWith(title) : a.Name.Contains(title);
                foreach (KACWrapper.KACAPI.KACAlarm alarm in KACWrapper.KAC.Alarms.Where(predicate).ToList())
                {
                    RP0Debug.Log(s1 + alarm.ID);
                    if (!KACWrapper.KAC.DeleteAlarm(alarm.ID))
                    {
                        RP0Debug.LogError(s2 + alarm.ID);
                        successful = false;
                    }
                }
                return successful;
            }
            else
            {
                List<uint> alarmsToRemove = new List<uint>();

                bool predicate(AlarmTypeBase a) => useStartsWith ? a.title.StartsWith(title) : a.title.Contains(title);
                foreach (uint id in AlarmClockScenario.Instance.alarms.Keys)
                {
                    if (AlarmClockScenario.Instance.alarms.TryGetValue(id, out AlarmTypeBase alarm) && alarm.title != null && predicate(alarm))
                    {
                        alarmsToRemove.Add(id);
                    }
                }

                foreach (uint id in alarmsToRemove)
                {
                    RP0Debug.Log(s1 + id);
                    if (!AlarmClockScenario.DeleteAlarm(id))
                    {
                        RP0Debug.LogError(s2 + id);
                        successful = false;
                    }
                }
                return successful;
            }
        }

        /// <summary>
        /// Deletes the alarm with a certain id (uint must be converted to string first).
        /// Uses KAC if available, otherwise falls back to the stock alarm clock.
        /// </summary>
        /// <returns>
        /// Success
        /// </returns>
        public static bool DeleteAlarmWithID(string id)
        {
            string s1 = "Alarm deleted with ID: ";
            string s2 = "Could not delete alarm with ID: ";
            bool successful;
            if (KACWrapper.APIReady)
            {
                successful = KACWrapper.KAC.DeleteAlarm(id);
                RP0Debug.Log((successful ? s1 : s2) + id);
                return successful;
            }
            else
            {
                if (uint.TryParse(id, out uint alarmID))
                {
                    successful = AlarmClockScenario.DeleteAlarm(alarmID);
                    RP0Debug.Log((successful ? s1 : s2) + id);
                    return successful;
                }
                else
                {
                    RP0Debug.LogError("Could not parse alarm ID " + id + " to uint");
                    return false;
                }
            }
        }

        /// <summary>
        /// Determines if an alarm with a certain id exists (uint must be converted to string first).
        /// Uses KAC if available, otherwise falls back to the stock alarm clock.
        /// </summary>
        /// <returns>
        /// Alarm exists
        /// </returns>
        public static bool AlarmExistsID(string id)
        {
            if (id == null) return false;
            if (KACWrapper.APIReady)
            {
                KACWrapper.KACAPI.KACAlarm alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.ID == id);
                if (alarm == null) return false;
                else return true;
            }
            else
            {
                if (uint.TryParse(id, out uint alarmID))
                {
                    return AlarmClockScenario.Instance.alarms.ContainsKey(alarmID);
                }
                else
                {
                    RP0Debug.LogError("Could not parse alarm ID " + id + " to uint");
                    return false;
                }
            }
        }

        /// <summary>
        /// Determines if an alarm that contains a certain string in its title exists.
        /// Uses KAC if available, otherwise falls back to the stock alarm clock.
        /// </summary>
        /// <returns>
        /// Alarm exists, out parameter id as string
        /// </returns>
        public static bool AlarmExistsTitle(string title, out string id)
        {
            id = "";
            if (title == null) return false;
            if (KACWrapper.APIReady)
            {
                KACWrapper.KACAPI.KACAlarm alarm = KACWrapper.KAC.Alarms.FirstOrDefault(a => a.Name.Contains(title));
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
