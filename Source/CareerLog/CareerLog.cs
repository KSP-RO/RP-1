using Contracts;
using Csv;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RP0
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class CareerLog : ScenarioModule
    {
        [KSPField]
        public int LogPeriodMonths = 1;

        [KSPField(isPersistant = true)]
        public double CurPeriodStart = 0;

        [KSPField(isPersistant = true)]
        public double NextPeriodStart = 0;

        public bool IsEnabled = false;

        private static readonly DateTime _epoch = new DateTime(1951, 1, 1);

        private readonly Dictionary<double, LogPeriod> _periodDict = new Dictionary<double, LogPeriod>();
        private readonly List<ContractEvent> _contractDict = new List<ContractEvent>();
        private readonly List<LaunchEvent> _launchedVessels = new List<LaunchEvent>();
        private readonly List<FacilityConstructionEvent> _facilityConstructions = new List<FacilityConstructionEvent>();
        private readonly List<TechResearchEvent> _techEvents = new List<TechResearchEvent>();
        private bool _eventsBound = false;
        private bool _launched = false;
        private MethodInfo _mInfContractName;
        private double _prevFundsChangeAmount;
        private TransactionReasons _prevFundsChangeReason;

        public static Func<SpaceCenterFacility, int> FnGetKCTUpgdCounts;
        public static Func<float> FnGetKCTSciPoints;

        public static CareerLog Instance { get; private set; }

        public LogPeriod CurrentPeriod
        { 
            get
            {
                double time = Planetarium.GetUniversalTime();
                while (time > NextPeriodStart)
                {
                    Debug.Log($"[RP-0] CareerLog switching current period: {time} > {NextPeriodStart}");
                    DateTime dtNextPeriod = _epoch.AddSeconds(NextPeriodStart).AddMonths(LogPeriodMonths);
                    CurPeriodStart = NextPeriodStart;
                    NextPeriodStart = (dtNextPeriod - _epoch).TotalSeconds;
                    Debug.Log($"[RP-0] CareerLog new period: {CurPeriodStart} to {NextPeriodStart}");
                }

                if (!_periodDict.TryGetValue(CurPeriodStart, out LogPeriod curPeriod))
                {
                    Debug.Log($"[RP-0] CareerLog current period not found in dict, adding...");
                    curPeriod = new LogPeriod(CurPeriodStart, NextPeriodStart);
                    _periodDict.Add(CurPeriodStart, curPeriod);

                    curPeriod.CurrentFunds = Funding.Instance.Funds;
                    curPeriod.CurrentSci = ResearchAndDevelopment.Instance.Science;
                    curPeriod.VABUpgrades = GetKCTUpgradeCounts(SpaceCenterFacility.VehicleAssemblyBuilding);
                    curPeriod.SPHUpgrades = GetKCTUpgradeCounts(SpaceCenterFacility.SpaceplaneHangar);
                    curPeriod.RnDUpgrades = GetKCTUpgradeCounts(SpaceCenterFacility.ResearchAndDevelopment);
                    curPeriod.ScienceEarned = GetSciPointTotalFromKCT();
                }

                return curPeriod;
            }
        }

        public override void OnAwake()
        {
            Debug.Log($"[RP-0] CareerLog OnAwake");
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;

            GameEvents.OnGameSettingsApplied.Add(SettingsChanged);
            GameEvents.onGameStateLoad.Add(LoadSettings);
        }

        public void OnDestroy()
        {
            Debug.Log($"[RP-0] CareerLog OnDestroy");
            GameEvents.onGameStateLoad.Remove(LoadSettings);
            GameEvents.OnGameSettingsApplied.Remove(SettingsChanged);

            if (_eventsBound)
            {
                GameEvents.onVesselSituationChange.Remove(VesselSituationChange);
                GameEvents.Modifiers.OnCurrencyModified.Remove(CurrenciesModified);
                GameEvents.Contract.onAccepted.Remove(ContractAccepted);
                GameEvents.Contract.onCompleted.Remove(ContractCompleted);
                GameEvents.Contract.onFailed.Remove(ContractFailed);
                GameEvents.Contract.onCancelled.Remove(ContractCancelled);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            Debug.Log($"[RP-0] CareerLog OnLoad");
            base.OnLoad(node);

            foreach (ConfigNode n in node.GetNodes("LOGPERIODS"))
            {
                foreach (ConfigNode pn in n.GetNodes("LOGPERIOD"))
                {
                    //Debug.Log($"[RP-0] CareerLog OnLoad LOGPERIOD :: {pn}");

                    var lp = new LogPeriod(pn);
                    double periodStart = lp.StartUT;
                    try
                    {
                        _periodDict.Add(periodStart, lp);
                    }
                    catch
                    {
                        Debug.LogError($"[RP-0] LOGPERIOD for {periodStart} already exists, skipping...");
                    }
                }
            }

            foreach (ConfigNode n in node.GetNodes("CONTRACTS"))
            {
                foreach (ConfigNode cn in n.GetNodes("CONTRACT"))
                {
                    //Debug.Log($"[RP-0] CareerLog OnLoad CONTRACT :: {cn}");
                    var c = new ContractEvent(cn);
                    _contractDict.Add(c);
                }
            }

            foreach (ConfigNode n in node.GetNodes("LAUNCHEVENTS"))
            {
                foreach (ConfigNode ln in n.GetNodes("LAUNCHEVENT"))
                {
                    //Debug.Log($"[RP-0] CareerLog OnLoad LAUNCHEVENT :: {ln}");
                    var l = new LaunchEvent(ln);
                    _launchedVessels.Add(l);
                }
            }

            foreach (ConfigNode n in node.GetNodes("FACILITYCONSTRUCTIONS"))
            {
                foreach (ConfigNode fn in n.GetNodes("FACILITYCONSTRUCTION"))
                {
                    //Debug.Log($"[RP-0] CareerLog OnLoad FACILITYCONSTRUCTION :: {fn}");
                    var fc = new FacilityConstructionEvent(fn);
                    _facilityConstructions.Add(fc);
                }
            }

            foreach (ConfigNode n in node.GetNodes("TECHS"))
            {
                foreach (ConfigNode tn in n.GetNodes("TECHS"))
                {
                    //Debug.Log($"[RP-0] CareerLog OnLoad TECHS :: {fn}");
                    var te = new TechResearchEvent(tn);
                    _techEvents.Add(te);
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            Debug.Log($"[RP-0] CareerLog OnSave _periodDict count: {_periodDict?.Count}");
            var n = node.AddNode("LOGPERIODS");
            foreach (LogPeriod e in _periodDict.Values)
            {
                Debug.Log($"[RP-0] CareerLog saving period: {e.StartUT}");
                e.Save(n.AddNode("LOGPERIOD"));
            }

            Debug.Log($"[RP-0] CareerLog OnSave _contractDict count: {_contractDict?.Count}");
            n = node.AddNode("CONTRACTS");
            foreach (ContractEvent c in _contractDict)
            {
                c.Save(n.AddNode("CONTRACT"));
            }

            Debug.Log($"[RP-0] CareerLog OnSave _launchedVessels count: {_launchedVessels?.Count}");
            n = node.AddNode("LAUNCHEVENTS");
            foreach (LaunchEvent l in _launchedVessels)
            {
                l.Save(n.AddNode("LAUNCHEVENT"));
            }

            Debug.Log($"[RP-0] CareerLog OnSave _facilityConstructions count: {_facilityConstructions?.Count}");
            n = node.AddNode("FACILITYCONSTRUCTIONS");
            foreach (FacilityConstructionEvent fc in _facilityConstructions)
            {
                fc.Save(n.AddNode("FACILITYCONSTRUCTION"));
            }

            Debug.Log($"[RP-0] CareerLog OnSave _techEvents count: {_techEvents?.Count}");
            n = node.AddNode("TECHS");
            foreach (TechResearchEvent tr in _techEvents)
            {
                tr.Save(n.AddNode("TECH"));
            }
        }

        public void AddTechEvent(string nodeName)
        {
            if (!IsEnabled) return;

            _techEvents.Add(new TechResearchEvent(Planetarium.GetUniversalTime())
            {
                NodeName = nodeName
            });
        }

        public void AddFacilityConstructionEvent(SpaceCenterFacility facility, int newLevel, double cost, ConstructionState state)
        {
            if (!IsEnabled) return;

            _facilityConstructions.Add(new FacilityConstructionEvent(Planetarium.GetUniversalTime())
            {
                Facility = facility,
                NewLevel = newLevel,
                Cost = cost,
                State = state
            });
        }

        public void ExportToFile(string path)
        {
            var rows = _periodDict.Select(p => p.Value)
                                  .Select(p => new[] 
            {
                _epoch.AddSeconds(p.StartUT).ToString("yy-MM"),
                p.VABUpgrades.ToString(),
                p.SPHUpgrades.ToString(),
                p.RnDUpgrades.ToString(),
                p.CurrentFunds.ToString(),
                p.CurrentSci.ToString(),
                p.ScienceEarned.ToString(),
                (p.OtherFundsEarned + p.ContractRewards).ToString(),
                p.LaunchFees.ToString(),
                p.MaintenanceFees.ToString(),
                p.ToolingFees.ToString(),
                p.EntryCosts.ToString(),
                p.OtherFees.ToString(),
                string.Join(", ", _launchedVessels.Where(l => l.UT >= p.StartUT && l.UT < p.EndUT)
                                                  .Select(l => l.VesselName)
                                                  .ToArray()),
                string.Join(", ", _contractDict.Where(c => c.Type == ContractEventType.Complete && c.UT >= p.StartUT && c.UT < p.EndUT)
                                               .Select(c => c.DisplayName)
                                               .ToArray()),
                string.Join(", ", _techEvents.Where(t => t.UT >= p.StartUT && t.UT < p.EndUT)
                                               .Select(t => t.NodeName)
                                               .ToArray()),
                string.Join(", ", _facilityConstructions.Where(f => f.State == ConstructionState.Completed && f.UT >= p.StartUT && f.UT < p.EndUT)
                                               .Select(f => $"{f.Facility} ({f.NewLevel + 1})")
                                               .ToArray())
            });

            var columnNames = new[] { "Month", "VAB", "SPH", "RnD", "Current Funds", "Current Sci", "Total sci earned", "+Funds", "Launch fees", "Maintenance", "Tooling", "Entry Costs", "Other Fees", "Launches", "Contracts", "Tech", "Facilities" };
            var csv = CsvWriter.WriteToText(columnNames, rows, ',');
            File.WriteAllText(path, csv);
        }

        private void LoadSettings(ConfigNode data)
        {
            Debug.Log($"[RP-0] CareerLog LoadSettings");
            IsEnabled = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().CareerLogEnabled;

            if (IsEnabled && !_eventsBound)
            {
                _eventsBound = true;
                GameEvents.onVesselSituationChange.Add(VesselSituationChange);
                GameEvents.Modifiers.OnCurrencyModified.Add(CurrenciesModified);
                GameEvents.Contract.onAccepted.Add(ContractAccepted);
                GameEvents.Contract.onCompleted.Add(ContractCompleted);
                GameEvents.Contract.onFailed.Add(ContractFailed);
                GameEvents.Contract.onCancelled.Add(ContractCancelled);
            }
        }

        private void SettingsChanged()
        {
            Debug.Log($"[RP-0] CareerLog SettingsChanged");
            LoadSettings(null);
        }

        private void CurrenciesModified(CurrencyModifierQuery query)
        {
            float changeDelta = query.GetTotal(Currency.Funds);
            if (changeDelta != 0f)
            {
                FundsChanged(changeDelta, query.reason);
            }
        }

        private void FundsChanged(float changeDelta, TransactionReasons reason)
        {
            Debug.Log($"[RP-0] FundsChanged {changeDelta} for {reason}");

            _prevFundsChangeAmount = changeDelta;
            _prevFundsChangeReason = reason;

            if (reason == TransactionReasons.ContractPenalty || reason == TransactionReasons.ContractDecline ||
                reason == TransactionReasons.ContractAdvance || reason == TransactionReasons.ContractReward)
            {
                CurrentPeriod.ContractRewards += changeDelta;
                return;
            }

            if (CareerEventScope.Current?.EventType == CareerEventType.Maintenance)
            {
                Debug.Log($"[RP-0] Adding {changeDelta} to maintenance fees");
                CurrentPeriod.MaintenanceFees -= changeDelta;
                return;
            }

            if (CareerEventScope.Current?.EventType == CareerEventType.Tooling)
            {
                Debug.Log($"[RP-0] Adding {changeDelta} to tooling fees");
                CurrentPeriod.ToolingFees -= changeDelta;
                return;
            }

            if (reason == TransactionReasons.VesselRollout || reason == TransactionReasons.VesselRecovery)
            {
                Debug.Log($"[RP-0] Adding {changeDelta} to launch fees");
                CurrentPeriod.LaunchFees -= changeDelta;
                return;
            }

            if (reason == TransactionReasons.RnDPartPurchase)
            {
                Debug.Log($"[RP-0] Adding {changeDelta} to entry costs");
                CurrentPeriod.EntryCosts -= changeDelta;
                return;
            }

            if (changeDelta > 0)
            {
                Debug.Log($"[RP-0] Adding {changeDelta} to OtherFundsEarned");
                CurrentPeriod.OtherFundsEarned += changeDelta;
            }
            else
            {
                Debug.Log($"[RP-0] Adding {changeDelta} to other fees");
                CurrentPeriod.OtherFees -= changeDelta;
            }
        }

        private void ContractAccepted(Contract c)
        {
            _contractDict.Add(new ContractEvent(Planetarium.GetUniversalTime())
            {
                Type = ContractEventType.Accept,
                FundsChange = c.FundsAdvance,
                RepChange = 0,
                DisplayName = c.Title,
                InternalName = GetContractInternalName(c)
            });
        }

        private void ContractCompleted(Contract c)
        {
            _contractDict.Add(new ContractEvent(Planetarium.GetUniversalTime())
            {
                Type = ContractEventType.Complete,
                FundsChange = c.FundsCompletion,
                RepChange = c.ReputationCompletion,
                DisplayName = c.Title,
                InternalName = GetContractInternalName(c)
            });
        }

        private void ContractCancelled(Contract c)
        {
            // KSP first takes the contract penalty and then fires the contract events
            double fundsChange = 0;
            if (_prevFundsChangeReason == TransactionReasons.ContractPenalty)
            {
                Debug.Log($"[RP-0] Found that {_prevFundsChangeAmount} was given as contract penalty");
                fundsChange = _prevFundsChangeAmount;
            }

            _contractDict.Add(new ContractEvent(Planetarium.GetUniversalTime())
            {
                Type = ContractEventType.Cancel,
                FundsChange = fundsChange,
                RepChange = 0,
                DisplayName = c.Title,
                InternalName = GetContractInternalName(c)
            });
        }

        private void ContractFailed(Contract c)
        {
            string internalName = GetContractInternalName(c);
            double ut = Planetarium.GetUniversalTime();
            if (_contractDict.Any(c2 => c2.UT == ut && c2.InternalName == internalName))
            {
                // This contract was actually cancelled, not failed
                return;
            }

            _contractDict.Add(new ContractEvent(ut)
            {
                Type = ContractEventType.Fail,
                FundsChange = c.FundsFailure,
                RepChange = c.ReputationFailure,
                DisplayName = c.Title,
                InternalName = internalName
            });
        }

        private void VesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> ev)
        {
            // KJR can clobber the vessel back to prelaunch state in case of clamp wobble. Need to exclude such events.
            if (!_launched && ev.from == Vessel.Situations.PRELAUNCH && ev.host == FlightGlobals.ActiveVessel)
            {
                Debug.Log($"[RP-0] Launching {FlightGlobals.ActiveVessel?.vesselName}");

                _launched = true;
                _launchedVessels.Add(new LaunchEvent(Planetarium.GetUniversalTime())
                {
                    VesselName = FlightGlobals.ActiveVessel?.vesselName
                });
            }
        }

        private string GetContractInternalName(Contract c)
        {
            if (_mInfContractName == null)
            {
                Assembly ccAssembly = AssemblyLoader.loadedAssemblies.First(a => a.assembly.GetName().Name == "ContractConfigurator").assembly;
                Type ccType = ccAssembly.GetType("ContractConfigurator.ConfiguredContract", true);
                _mInfContractName = ccType.GetMethod("contractTypeName", BindingFlags.Public | BindingFlags.Static);
            }

            return (string)_mInfContractName.Invoke(null, new object[] { c });
        }

        private int GetKCTUpgradeCounts(SpaceCenterFacility facility)
        {
            try
            {
                return FnGetKCTUpgdCounts == null ? 0 : FnGetKCTUpgdCounts(facility);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return 0;
            }
        }

        private float GetSciPointTotalFromKCT()
        {
            try
            {
                return FnGetKCTSciPoints == null ? 0 : FnGetKCTSciPoints();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return 0;
            }
        }
    }
}
