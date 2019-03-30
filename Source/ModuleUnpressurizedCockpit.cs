using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KSP;

namespace RP0
{
    public class ModuleUnpressurizedCockpit : PartModule
    {
        [KSPField]
        public double crewDeathChance = 0.1d * (1d / 50d);

        public double nextCheck = -1d;
        public double checkInterval = 1d;

        public double gDamageAdder = 0d;

        protected System.Random rnd;

        public override string GetInfo()
        {
            return "Cockpit is unpressurized and will lead to crew death above 30km";
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
                if(nextCheck < 0d)
                    nextCheck = UT + checkInterval;
                else if (UT > nextCheck)
                {
                    nextCheck = UT + checkInterval;
                    if (part.staticPressureAtm * 101.325d < 1.2d)
                    {
                        bool kill = false;
                        for(int i = pC; i-- > 0;)
                        {
                            ProtoCrewMember pcm = part.protoModuleCrew[i];
                            pcm.gExperienced += (0.5d + rnd.NextDouble()) * gDamageAdder;
                            if (rnd.NextDouble() < crewDeathChance)
                            {
                                kill = true;
                                ScreenMessages.PostScreenMessage(vessel.vesselName + ": Crewmember " + pcm.name + " from exposure to near-vacuum.", 30.0f, ScreenMessageStyle.UPPER_CENTER);
                                FlightLogger.fetch.LogEvent("[" + KSPUtil.PrintTime(vessel.missionTime, 3, false) + "] "
                                                      + pcm.name + " died from exposure to near-vacuum.");
                                part.RemoveCrewmember(pcm);
                                pcm.Die();
                            }
                        }
                        if (kill)
                        {
                            if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
                            {
                                CameraManager.Instance.SetCameraFlight();
                            }
                        }
                    }
                }
            }
        }
    }
}
