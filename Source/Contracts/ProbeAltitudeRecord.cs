using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;
using Contracts.Templates;
using KSPAchievements;

namespace Contracts
{
    class ProbeAltitudeRecord : AltitudeRecord
    {
        public override bool MeetRequirements()
		{
            /*if (ProgressTracking.Instance.NodeComplete("ReachedSpace"))
			{
				return false;
			}*/
			RecordsAltitudeProbe recordsAltitude = ProgressTracking.Instance.FindNode("AltitudeRecordProbe") as RecordsAltitudeProbe;
			if (recordsAltitude != null)
			{
				if (targetAltitude > 0.0)
				{
					if (recordsAltitude.record > targetAltitude)
					{
						return false;
					}
				}
			}
			if (base.ContractState != State.Active)
			{
				ProbeAltitudeRecord[] currentContracts = ContractSystem.Instance.GetCurrentContracts<ProbeAltitudeRecord>();
				for (int i = 0; i < currentContracts.Length; i++)
				{
					if (currentContracts[i].TargetAltitude > TargetAltitude)
					{
						return false;
					}
				}
			}
			return true;
		}
        protected override string GetDescription()
        {
            return "We want you to reach a new high altitude with a sounding rocket.";
        }
        protected override string GetSynopsys()
        {
            return "Reach " + targetAltitude.ToString("N0") + "m with a sounding rocket.";
        }
        protected override string GetTitle()
        {
            return "Sounding Rocket: " + targetAltitude.ToString("N0") + "m.";
        }
        protected override string MessageCompleted()
        {
            return "Congratulations! You've exceeded " + targetAltitude.ToString("N0") + "m";
        }
        protected override void OnLoad(ConfigNode node)
        {
	        if (node.HasValue("alt"))
	        {
                targetAltitude = double.Parse(node.GetValue("alt"));
	        }
        }
        protected override void OnSave(ConfigNode node)
        {
            node.AddValue("alt", targetAltitude.ToString());
        }
        protected override bool Generate()
		{
			double newTarget = 10000.0;
			RecordsAltitudeProbe recordsAltitude = ProgressTracking.Instance.FindNode("AltitudeRecordProbe") as RecordsAltitudeProbe;
			if (recordsAltitude != null)
			{
				newTarget = recordsAltitude.record;
			}
            if ((object)FlightGlobals.Vessels != null)
            {
                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if ((object)v.orbit != null)
                        if (newTarget < v.orbit.ApA)
                            newTarget = v.orbit.ApA;
                }
            }
			if (newTarget < 30000.0)
			{
				prestige = Contract.ContractPrestige.Trivial;
				targetAltitude = (double)(Mathf.Ceil((float)(newTarget + 10000.0) / 1000f) * 1000f);
			}
			else if (newTarget < 100000.0)
			{
				prestige = Contract.ContractPrestige.Significant;
				targetAltitude = (double)(Mathf.Ceil((float)(newTarget + 20000.0) / 5000f) * 5000f);
			}
			else
			{
				prestige = Contract.ContractPrestige.Exceptional;
                targetAltitude = (double)(Mathf.Ceil((float)(newTarget * 1.5f) / 10000f) * 10000f);
                if (newTarget > (Planetarium.fetch.Home.sphereOfInfluence - Planetarium.fetch.Home.Radius) * 0.9f)
                    return false;
			}
			AddParameter(new Contracts.Parameters.ProbeAltitudeRecord(targetAltitude), null);
            base.AddKeywordsRequired("Record");
			expiryType = Contract.DeadlineType.None;
			deadlineType = Contract.DeadlineType.None;
            // RP-0 scale funds by 0.1x
            float reward = Mathf.Floor((float)(Math.Sqrt(Math.Sqrt(targetAltitude))));
			base.SetFunds(reward * 50f, reward * 250f, null);
			base.SetReputation(reward * 2f, null);
			return true;
		}
    }
}
namespace Contracts.Parameters
{
    [Serializable]
	public class ProbeAltitudeRecord : ContractParameter
	{
		protected double targetAltitude;
		public double TargetAltitude
		{
			get
			{
				return targetAltitude;
			}
		}
		public ProbeAltitudeRecord()
		{
		}
		public ProbeAltitudeRecord(double targetAlt)
		{
			targetAltitude = targetAlt;
		}
		protected override string GetHashString()
		{
			return targetAltitude.ToString();
		}
		protected override string GetTitle()
		{
			return "Reach " + targetAltitude.ToString("N0") + "m";
		}
		protected override void OnLoad(ConfigNode node)
		{
			if (node.HasValue("alt"))
			{
				targetAltitude = double.Parse(node.GetValue("alt"));
			}
		}
		protected override void OnSave(ConfigNode node)
		{
			node.AddValue("alt", targetAltitude.ToString());
		}
		protected override void OnRegister()
		{
			GameEvents.OnProgressAchieved.Add(new EventData<ProgressNode>.OnEvent(OnProgressNodeAchieved));
		}
		protected override void OnUnregister()
		{
			GameEvents.OnProgressAchieved.Remove(new EventData<ProgressNode>.OnEvent(OnProgressNodeAchieved));
		}
		private void OnProgressNodeAchieved(ProgressNode node)
		{
			RecordsAltitudeProbe recordsAltitude = node as RecordsAltitudeProbe;
			if (recordsAltitude != null)
			{
				if (recordsAltitude.record >= targetAltitude)
				{
					base.SetComplete();
				}
			}
		}
	}
}