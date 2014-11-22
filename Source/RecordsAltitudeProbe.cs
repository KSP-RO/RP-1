using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;

namespace KSPAchievements
{
    public class RecordsAltitudeProbe : ProgressNode
    {
        public double record;
        Vessel vessel;
        public RecordsAltitudeProbe() : base("AltitudeRecordProbe", false)
        {
            OnIterateVessels = new Action<Vessel>(iterateVessels);

        }
        void OnVesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> vs)
		{
			if (vs.host.DiscoveryInfo.Level != DiscoveryLevels.Owned)
			{
				return;
			}
			/*if (vs.from == Vessel.Situations.FLYING)
			{
				if (vs.to == Vessel.Situations.SUB_ORBITAL)
				{
					record = (double)vs.host.mainBody.maxAtmosphereAltitude;
					Complete();
					OnStow();
					Terminate();
				}
			}*/
		}
		void iterateVessels(Vessel v)
		{
			if (v != FlightGlobals.ActiveVessel)
				return;
            //MonoBehaviour.print("**RA Iterating");
			if (v.mainBody == Planetarium.fetch.Home)
			{
				if (v.altitude > record)
				{
					record = v.altitude;
					if (vessel != v)
					{
						Reach();
                        vessel = v;
					}
					else
					{
						AchieveDate = Planetarium.GetUniversalTime();
						Achieve();
					}
				}
			}
		}
		protected override void OnLoad(ConfigNode node)
		{
            //Debug.Log("RecordsAltitudeProbe::OnLoad");
			if (node.HasValue("record"))
			{
				double.TryParse(node.GetValue("record"), out record);
			}
			if (IsComplete)
			{
				Terminate();
			}
		}
		protected override void OnSave(ConfigNode node)
		{
            //Debug.Log("RecordsAltitudeProbe::OnSave");
			node.AddValue("record", this.record);
		}
		private void Terminate()
		{
			OnIterateVessels = null;
			OnDeploy = null;
			OnStow = null;
		}
	}
}
