using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;
using Contracts.Templates;
using KSPAchievements;

namespace Contracts
{
    /*class ProbeAltitudeRecord : AltitudeRecord
    {
        public override bool MeetRequirements()
		{
            if (ProgressTracking.Instance.NodeComplete("ReachedSpace"))
			{
				return false;
			}
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
			if (newTarget < 30000.0)
			{
				prestige = Contract.ContractPrestige.Trivial;
				targetAltitude = (double)(Mathf.Ceil((float)(newTarget + 10000.0) / 1000f) * 1000f);
			}
			else if (newTarget < 50000.0)
			{
				prestige = Contract.ContractPrestige.Significant;
				targetAltitude = (double)(Mathf.Ceil((float)(newTarget + 20000.0) / 1000f) * 1000f);
			}
			else
			{
				prestige = Contract.ContractPrestige.Exceptional;
				targetAltitude = (double)(Mathf.Ceil((float)((double)FlightGlobals.Bodies[1].maxAtmosphereAltitude * 0.8) / 1000f) * 1000f);
				if (newTarget > targetAltitude)
				{
					targetAltitude = (double)(Mathf.Ceil((float)(newTarget + 20000.0) / 1000f) * 1000f);
				}
			}
			AddParameter(new AltitudeRecord(targetAltitude), null);
			base.AddKeywordsRequired(new string[]
			{
				.(138103)
			});
			expiryType = Contract.DeadlineType.None;
			deadlineType = Contract.DeadlineType.None;
			float num2 = Mathf.Floor((float)(targetAltitude / 10000.0) * 100f) / 100f;
			base.SetFunds(num2 * 1000f, num2 * 5000f, null);
			base.SetReputation(num2 * 20f, null);
			return true;
		}
    }*/
}
