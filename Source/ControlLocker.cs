using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KSP;
using UnityEngine;


namespace RP0
{
    class ControlLockerUtils
    {
        public static bool ShouldLock(List<Part> parts, bool countClamps, out float maxMass, out float vesselMass)
        {
            int crewCount = -1;
            maxMass = vesselMass = 0f;
            for (int i = 0; i < parts.Count; i++)
            {
                // add up mass
                float partMass = parts[i].mass + parts[i].GetResourceMass();

                // get modules
                bool cmd = false, science = false, avionics = false, clamp = false;
                float partAvionicsMass = 0f;
                ModuleCommand mC = null;
                for (int j = 0; j < parts[i].Modules.Count; j++)
                {
                    if (parts[i].Modules[j] is ModuleCommand)
                    {
                        cmd = true;
                        mC = parts[i].Modules[j] as ModuleCommand;
                    }
                    science |= parts[i].Modules[j] is ModuleScienceCore;
                    if ((parts[i].Modules[j]) is ModuleAvionics)
                    {
                        avionics = true;
                        partAvionicsMass += (parts[i].Modules[j] as ModuleAvionics).CurrentMassLimit;
                    }
                    if(parts[i].Modules[j] is LaunchClamp)
                    {
                        clamp = true;
                        partMass = 0f;
                    }
                }
                vesselMass += partMass; // done after the clamp check

                // switch based on modules

                // Do we have an unencumbered command module?
                // if we count clamps, they can give control. If we don't, this works only if the part isn't a clamp.
                if ((countClamps || !clamp) && cmd && !science && !avionics)
                    return false;
                if (cmd && avionics) // check if need crew
                {
                    if (mC.minimumCrew > 0) // if we need crew
                    {
                        if (crewCount < 0) // see if we cached crew
                        {
                            if (HighLogic.LoadedSceneIsFlight) // get from vessel
                                crewCount = parts[i].vessel.GetCrewCount();
                            else if (HighLogic.LoadedSceneIsEditor) // or crew manifest
                                crewCount = CMAssignmentDialog.Instance.GetManifest().GetAllCrew(false).Count;
                            else crewCount = 0; // or assume no crew (should never trip this)
                        }
                        if (mC.minimumCrew > crewCount)
                            avionics = false; // not operational
                    }
                }
                if (avionics)
                    maxMass += partAvionicsMass;
            }
            if (maxMass > vesselMass) // will only be reached if the best we have is avionics.
                return false; // unlock if our max avionics mass is >= vessel mass
            // NOTE: we don't update for fuel burnt, because avionics needs to be able to handle the size
            // as well as the fuel.

            // Otherwise, we lock yaw/pitch/roll.
            return true;
        }

        public static string FormatTime(double time)
        {
            int iTime = (int)time % 3600;
            int seconds = iTime % 60;
            int minutes = (iTime / 60) % 60;
            int hours = (iTime / 3600);
            return hours.ToString("D2")
                + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D2");
        }
    }
    /// <summary>
    /// This class will display a warning in the editor when there is insufficient avionics
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    class ControlLockerEditor : MonoBehaviour
    {
        // GUI
        static Rect windowPos = new Rect(500, 200, 0, 0);
        static bool guiEnabled = true;
        bool haveParts = false;
        static bool showOnlyWarning = false;

        // settings
        //static KSP.IO.PluginConfiguration config;
        static public KeyCode key = KeyCode.I;
        double deltaTime = 0d;
        const double UPDATEINTERVAL = 0.25d;

        // data
        bool isControlLocked = false;
        float maxMass, vesselMass;

        public void Start()
        {
            //config = KSP.IO.PluginConfiguration.CreateForType<ControlLockerEditor>
            enabled = true;
            //config.load();
            //windowPos = config.GetValue<Rect>
        }
        public void Update()
        {
            // toggle visibility
            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(key))
            {
                if (guiEnabled && showOnlyWarning && !isControlLocked)
                    showOnlyWarning = false;
                else
                    guiEnabled = !guiEnabled;
            }

            deltaTime += Time.deltaTime;
            if (deltaTime > UPDATEINTERVAL)
            {
                deltaTime = 0;
                haveParts = false;
                isControlLocked = false;
                List<Part> parts = null;
                if ((object)(EditorLogic.fetch.ship) != null)
                    parts = EditorLogic.fetch.ship.Parts;
                if (parts != null)
                {
                    if (parts.Count > 0)
                    {
                        isControlLocked = ControlLockerUtils.ShouldLock(parts, false, out maxMass, out vesselMass);
                        haveParts = true;
                    }
                }
            }
        }
        public void OnGUI()
        {
            if(guiEnabled && haveParts && (!showOnlyWarning || (showOnlyWarning && isControlLocked)))
                windowPos = GUILayout.Window("RP0ControlLocker".GetHashCode(), windowPos, DrawWindow, "Avionics");
        }
        public void DrawWindow(int windowID)
        {
            // Enable closing of the window tih "x"
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.padding = new RectOffset(5, 5, 3, 0);
            buttonStyle.margin = new RectOffset(1, 1, 1, 1);
            buttonStyle.stretchWidth = false;
            buttonStyle.stretchHeight = false;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.wordWrap = false;

            GUILayout.BeginVertical();

            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("X", buttonStyle))
            {
                guiEnabled = false;
            }
            GUILayout.EndHorizontal();

            if (isControlLocked)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("WARNING", labelStyle);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Insufficient avionics!", labelStyle);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Supports: " + maxMass.ToString("N3") + "t", labelStyle);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Vessel:   " + vesselMass.ToString("N3") + "t", labelStyle);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Avionics OK!", labelStyle);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(showOnlyWarning ? "Set to show always" : "Set to hide if OK", buttonStyle))
            {
                showOnlyWarning = !showOnlyWarning;
            }
            GUILayout.EndHorizontal();

           

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

    }

    /// <summary>
    /// This class will lock controls if and only if avionics requirements exist and are not met
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class ControlLocker : MonoBehaviour
    {
        public int vParts = -1;
        public Vessel vessel = null;
        bool wasLocked = false;
        const ControlTypes lockmask = ControlTypes.YAW | ControlTypes.PITCH | ControlTypes.ROLL | ControlTypes.SAS | 
            ControlTypes.RCS | ControlTypes.THROTTLE | ControlTypes.WHEEL_STEER | ControlTypes.WHEEL_THROTTLE;
        const string lockID = "RP0ControlLocker";
        float maxMass, vesselMass;

        ScreenMessage message = new ScreenMessage("", 8f, ScreenMessageStyle.UPPER_CENTER);

        public bool ShouldLock()
        {
            if (vessel != FlightGlobals.ActiveVessel)
            {
                vParts = -1;
                vessel = FlightGlobals.ActiveVessel;
            }
            // if we have no active vessel, undo locks
            if ((object)vessel == null)
                return false;

            // Do we need to update?
            if (vessel.Parts.Count != vParts)
            {
                vParts = vessel.Parts.Count;
                return ControlLockerUtils.ShouldLock(vessel.Parts, true, out maxMass, out vesselMass);
            }
            return wasLocked;
        }

        public void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready)
            {
                InputLockManager.RemoveControlLock(lockID);
                return;
            }
            bool doLock = ShouldLock();
            if (doLock != wasLocked)
            {
                if (wasLocked)
                {
                    InputLockManager.RemoveControlLock(lockID);
                    message.message = "Avionics: Unlocking Controls";
                }
                else
                {
                    InputLockManager.SetControlLock(lockmask, lockID);
                    vessel.Autopilot.Disable();
                    message.message = "Insufficient Avionics, Locking Controls (supports "
                        + maxMass.ToString("N3") + "t, vessel " + vesselMass.ToString("N3") + "t)";
                }
                ScreenMessages.PostScreenMessage(message, true);
                FlightLogger.eventLog.Add("[" + ControlLockerUtils.FormatTime(vessel.missionTime) + "] "
                                              + message.message);
                wasLocked = doLock;
            }
        }
        public void OnDestroy()
        {
            InputLockManager.RemoveControlLock(lockID);
        }
    }
}
