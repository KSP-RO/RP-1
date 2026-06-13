using UniLinq;
using KACAlarm = RP0.KACWrapper.KACAPI.KACAlarm;
using KACAPI = RP0.KACWrapper.KACAPI;

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
                        RP0Debug.LogWarning($"KAC Alarm with ID: {alarmID} could not be found.");
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
