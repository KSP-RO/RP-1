using UnityEngine;

namespace RP0
{
    public class ModuleUnpressurizedCockpit : PartModule
    {
        /// <summary>
        /// Chance to die (%) per 1s interval.
        /// </summary>
        [KSPField]
        public double crewDeathChance = 0.1d * (1d / 50d);

        /// <summary>
        /// Altitude in meters above which the crew can be killed.
        /// </summary>
        [KSPField]
        public double crewDeathAltitude = 16000;

        public double nextCheck = -1d;
        public double checkInterval = 1d;

        public double gDamageAdder = 0d;

        protected System.Random rnd;
        protected double pressureAtKillAltitude;

        public override string GetInfo()
        {
            return $"Cockpit is unpressurized and will lead to crew death above {crewDeathAltitude / 1000:0.#}km";
        }

        public override void OnAwake()
        {
            base.OnAwake();
            gDamageAdder = PhysicsGlobals.KerbalGThresholdLOC * (1d / 5d);
            rnd = new System.Random();
        }

        protected void FixedUpdate()
        {
            int pC;
            if (HighLogic.LoadedSceneIsFlight && part.CrewCapacity > 0 && (pC = part.protoModuleCrew.Count) > 0)
            {
                double UT = Planetarium.GetUniversalTime();
                if (nextCheck < 0d)
                    nextCheck = UT + checkInterval;
                else if (UT > nextCheck)
                {
                    if (pressureAtKillAltitude == default)
                    {
                        pressureAtKillAltitude = FlightGlobals.GetHomeBody().GetPressureAtm(crewDeathAltitude);
                    }

                    nextCheck = UT + checkInterval;
                    if (part.staticPressureAtm < pressureAtKillAltitude)
                    {
                        ScreenMessages.PostScreenMessage($"Cockpit is above the safe altitude which will lead to crew death", 1f, ScreenMessageStyle.UPPER_CENTER, XKCDColors.Red);

                        bool killed = false;
                        for (int i = pC; i-- > 0;)
                        {
                            ProtoCrewMember pcm = part.protoModuleCrew[i];
                            pcm.gExperienced += (0.5d + rnd.NextDouble()) * gDamageAdder;
                            if (rnd.NextDouble() < crewDeathChance)
                            {
                                killed = true;
                                ScreenMessages.PostScreenMessage($"{vessel.vesselName}: Crewmember {pcm.name} has died from exposure to near-vacuum.", 30.0f, ScreenMessageStyle.UPPER_CENTER, XKCDColors.Red);
                                FlightLogger.fetch.LogEvent($"[{KSPUtil.PrintTime(vessel.missionTime, 3, false)}] {pcm.name} died from exposure to near-vacuum.");
                                part.RemoveCrewmember(pcm);
                                pcm.Die();
                            }
                        }

                        if (killed && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
                        {
                            CameraManager.Instance.SetCameraFlight();
                        }
                    }
                }
            }
        }
    }
}
