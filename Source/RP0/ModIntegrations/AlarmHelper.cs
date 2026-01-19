using System.Linq;
using KACAPI = RP0.KACWrapper.KACAPI;
using KACAlarm = RP0.KACWrapper.KACAPI.KACAlarm;

namespace RP0.ModIntegrations
{
    /// <summary>
    /// Adds methods for using alarms with KAC if available, otherwise falls back to the stock alarm clock.
    /// </summary>
    public static class AlarmHelper
    {
        private static bool UseKAC => KACWrapper.APIReady;

        /// <summary>
        /// Creates an alarm.
        /// </summary>
        /// <remarks>
        /// Do not pass in Contract or ContractAuto as the alarmType, KAC seems to immediately delete the alarm for some reason.
        /// </remarks>
        /// <returns>
        /// AlarmID
        /// </returns>
        public static string CreateAlarm(string title, string description, double UT, KACAPI.AlarmTypeEnum alarmType = KACAPI.AlarmTypeEnum.Raw)
        {
            string s = "Alarm created with ID: ";
            if (UseKAC)
            {
                if (alarmType == KACAPI.AlarmTypeEnum.Contract || alarmType == KACAPI.AlarmTypeEnum.ContractAuto)
                { // no idea what causes KAC to immediately delete these alarms, so just don't allow them
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
                        RP0Debug.Log(s + alarmID);
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
                string alarmID = alarm.Id.ToString();
                RP0Debug.Log(s + alarmID);
                return alarmID;
            }
        }

        /// <summary>
        /// Deletes the alarm with a certain id (uint must be converted to string first).
        /// </summary>
        /// <remarks>
        /// KAC gives a null reference if you delete an alarm while the end alarm window is open. This isn't a problem with the stock end alarm window, so I'll just say it's KAC's fault.
        /// </remarks>
        /// <returns>
        /// Success
        /// </returns>
        public static bool DeleteAlarmWithID(string id)
        {
            string s1 = "Alarm deleted with ID: ";
            string s2 = "Could not delete alarm with ID: ";
            bool successful;
            if (UseKAC)
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
        /// Deletes all alarms that contain (or start with) a certain string in their title.
        /// </summary>
        /// <remarks>
        /// KAC gives a null reference if you delete an alarm while the end alarm window is open. This isn't a problem with the stock end alarm window, so I'll just say it's KAC's fault.
        /// </remarks>
        /// <returns>
        /// Success
        /// </returns>
        public static bool DeleteAllAlarmsWithTitle(string title, bool useStartsWith = false)
        {
            string s1 = "Alarm deleted with ID: ";
            string s2 = "Could not delete alarm with ID: ";
            string s3 = "No alarms found with title: " + title;
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
                        RP0Debug.LogError(s2 + id);
                        successful = false;
                    }
                    else RP0Debug.Log(s1 + id);
                }
                if (!foundAlarm)
                {
                    RP0Debug.LogWarning(s3);
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
                        RP0Debug.LogError(s2 + id);
                        successful = false;
                    }
                    else RP0Debug.Log(s1 + id);
                }
                if (!foundAlarm)
                {
                    RP0Debug.LogWarning(s3);
                    successful = false;
                }
                return successful;
            }
        }

        /// <summary>
        /// Determines if an alarm with a certain id exists (uint must be converted to string first).
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
                    RP0Debug.LogError("Could not parse alarm ID " + id + " to uint");
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
