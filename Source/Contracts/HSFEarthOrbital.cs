using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;
using Contracts.Templates;
using KSPAchievements;

namespace Contracts
{
    class HSFEarthOrbital : Contract
    {
        public override bool MeetRequirements()
		{
			return true;
		}
        protected override string GetDescription()
        {
            return "We want you to launch a human being into orbit and return her/him safely home.";
        }
        protected override string GetSynopsys()
        {
            return "Orbit and safe return of a 'naut.";
        }
        protected override string GetTitle()
        {
            return "Human Spaceflight: " + Planetarium.fetch.Home.bodyName + " Orbital";
        }
        protected override string MessageCompleted()
        {
            return "Congratulations! You've orbited a 'naut and returned her/him safely home!";
        }
        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
        }
        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
        }
        protected override bool Generate()
		{
            bool havePod = false;
            foreach (AvailablePart p in PartLoader.LoadedPartsList)
            {
                if (p.partPrefab.CrewCapacity > 0 && ResearchAndDevelopment.PartTechAvailable(p))
                {
                    havePod = true;
                    break;
                }
            }
            // return false iff we have no crew pods AND we've not reached orbit
            // so you should get this if you've reached orbit, even if you haven't researched any pods yet.
            if (!havePod && !ProgressTracking.Instance.NodeComplete("Kerbin", "Orbit")) // SQUAD Y U HARDCODE
                return false;
            // one at a time
            if (base.ContractState != State.Active)
            {
                HSFEarthOrbital[] currentContracts = ContractSystem.Instance.GetCurrentContracts<HSFEarthOrbital>();
                for (int i = 0; i < currentContracts.Length; i++)
                {
                    if (currentContracts[i].ContractState == State.Offered || currentContracts[i].ContractState == State.Active)
                    {
                        return false;
                    }
                }
            }
			prestige = Contract.ContractPrestige.Exceptional;

            AddParameter(new Contracts.Parameters.HSFOrbital(Planetarium.fetch.Home), null);
            base.AddKeywords(new string[] { "HSF" } );
			expiryType = Contract.DeadlineType.None;
			deadlineType = Contract.DeadlineType.None;
            // RP-0 scale funds by 0.1x
			base.SetFunds(10000f, 50000f, null);
			base.SetReputation(480f, null);
			return true;
		}
    }
}
namespace Contracts.Parameters
{
    [Serializable]
	public class HSFOrbital : ContractParameter
	{
        protected string bodyName = "";
        protected CelestialBody body = null;
        private static CelestialBody GetBody(string bName)
        {
            for(int i = 0; i < FlightGlobals.Bodies.Count; i++)
                if(FlightGlobals.Bodies[i].name == bName)
                {
                    return FlightGlobals.Bodies[i];
                }
            return null;
        }
        public HSFOrbital()
        {
            SetDefault();
        }
        public HSFOrbital(string newBodyName)
        {
            bodyName = newBodyName;
            body = GetBody(bodyName);
        }
        public HSFOrbital(CelestialBody newBody)
        {
            body = newBody;
            bodyName = body.name;
        }
        protected void SetDefault()
        {
            try
            {
                body = Planetarium.fetch.Home;
                bodyName = body.name;
            }
            catch
            {
                bodyName = "Kerbin";
                body = GetBody(bodyName);
            }
        }
		protected override string GetHashString()
		{
			return ("HSFOrbital:" + bodyName).ToString();
		}
		protected override string GetTitle()
		{
			return "Return a crewed capsule from orbit.";
		}
		protected override void OnLoad(ConfigNode node)
		{
			if (node.HasValue("bodyName"))
			{
                bodyName = node.GetValue(bodyName);
                body = GetBody(bodyName);
			}
            if (bodyName == "" || (object)body == null)
            {
                SetDefault();
            }
		}
		protected override void OnSave(ConfigNode node)
		{
            node.AddValue("bodyName", bodyName);
		}
		protected override void OnRegister()
		{
			GameEvents.onVesselSituationChange.Add(new EventData<GameEvents.HostedFromToAction<Vessel, Vessel.Situations>>.OnEvent(this.OnVesselSituationChange));
		}
		protected override void OnUnregister()
		{
			GameEvents.onVesselSituationChange.Remove(new EventData<GameEvents.HostedFromToAction<Vessel, Vessel.Situations>>.OnEvent(this.OnVesselSituationChange));
		}

		protected void OnVesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> vs)
		{
			if (vs.to == Vessel.Situations.LANDED || vs.to == Vessel.Situations.SPLASHED)
			    if (vs.from == Vessel.Situations.FLYING)
				    if (vs.host.mainBody.isHomeWorld)
				    {
					    VesselTripLog vesselTripLog = VesselTripLog.FromVessel(vs.host);
					    if (vesselTripLog.Log.HasEntry(FlightLog.EntryType.Orbit, bodyName))
                            base.SetComplete();
				    }
		}
	}
}