using Contracts;
using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace ContractConfigurator.RP0
{
    public class AcceptContractBehaviour : ContractBehaviour
    {
        protected string _ccType;
        protected ContractEventType _evtType;

        public AcceptContractBehaviour()
        {
        }

        public AcceptContractBehaviour(string ccType, ContractEventType evtType)
        {
            _ccType = ccType;
            _evtType = evtType;
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            string sEvtType = null;
            node.TryGetValue("contractType", ref _ccType);
            node.TryGetValue("eventType", ref sEvtType);
            _evtType = (ContractEventType)Enum.Parse(typeof(ContractEventType), sEvtType);
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            node.AddValue("contractType", _ccType);
            node.AddValue("eventType", _evtType.ToString());
        }

        protected override void OnOffered()
        {
            base.OnOffered();

            if (_evtType != ContractEventType.Offered) return;

            TryAcceptContract();
        }

        protected override void OnAccepted()
        {
            base.OnAccepted();

            if (_evtType != ContractEventType.Accepted) return;

            TryAcceptContract();
        }

        protected override void OnCompleted()
        {
            base.OnCompleted();

            if (_evtType != ContractEventType.Completed) return;

            TryAcceptContract();
        }

        protected override void OnFailed()
        {
            base.OnFailed();

            if (_evtType != ContractEventType.Failed) return;

            TryAcceptContract();
        }

        protected override void OnCancelled()
        {
            base.OnCancelled();

            if (_evtType != ContractEventType.Cancelled) return;

            TryAcceptContract();
        }

        protected override void OnFinished()
        {
            base.OnFinished();

            if (_evtType != ContractEventType.Finished) return;

            TryAcceptContract();
        }

        protected override void OnDeclined()
        {
            base.OnDeclined();

            if (_evtType != ContractEventType.Declined) return;

            TryAcceptContract();
        }

        protected override void OnDeadlineExpired()
        {
            base.OnDeadlineExpired();

            if (_evtType != ContractEventType.DeadlineExpired) return;

            TryAcceptContract();
        }

        protected override void OnOfferExpired()
        {
            base.OnOfferExpired();

            if (_evtType != ContractEventType.OfferExpired) return;

            TryAcceptContract();
        }

        protected override void OnWithdrawn()
        {
            base.OnWithdrawn();

            if (_evtType != ContractEventType.Withdrawn) return;

            TryAcceptContract();
        }

        private void TryAcceptContract()
        {
            var contract = ConfiguredContract.CurrentContracts
                .Where(c => c?.contractType?.name.Equals(_ccType) ?? false && c.ContractState == Contract.State.Offered)
                .FirstOrDefault();

            contract?.Accept();
        }
    }

    public enum ContractEventType : int
    {
        Offered = 0,
        Accepted = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4,
        Finished = 5,
        Declined = 6,
        DeadlineExpired = 7,
        OfferExpired = 8,
        Withdrawn = 9
    }
}
