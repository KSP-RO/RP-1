using Contracts;
using Csv;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

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
        private LogPeriod _prevPeriod;
        private LogPeriod _currentPeriod;

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
                    DateTime dtNextPeriod = _epoch.AddSeconds(NextPeriodStart).AddMonths(LogPeriodMonths);
                    CurPeriodStart = NextPeriodStart;
                    NextPeriodStart = (dtNextPeriod - _epoch).TotalSeconds;
                    _prevPeriod = _currentPeriod;
                    _currentPeriod = null;
                }

                if (_currentPeriod == null)
                {
                    if (!_periodDict.TryGetValue(CurPeriodStart, out _currentPeriod))
                    {
                        _currentPeriod = new LogPeriod(CurPeriodStart, NextPeriodStart);
                        _periodDict.Add(CurPeriodStart, _currentPeriod);

                        if (_prevPeriod != null)
                        {
                            _prevPeriod.CurrentFunds = Funding.Instance.Funds;
                            _prevPeriod.CurrentSci = ResearchAndDevelopment.Instance.Science;
                            _prevPeriod.VABUpgrades = GetKCTUpgradeCounts(SpaceCenterFacility.VehicleAssemblyBuilding);
                            _prevPeriod.SPHUpgrades = GetKCTUpgradeCounts(SpaceCenterFacility.SpaceplaneHangar);
                            _prevPeriod.RnDUpgrades = GetKCTUpgradeCounts(SpaceCenterFacility.ResearchAndDevelopment);
                            _prevPeriod.ScienceEarned = GetSciPointTotalFromKCT();
                        }
                    }
                }

                return _currentPeriod;
            }
        }

        public override void OnAwake()
        {
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
            base.OnLoad(node);

            foreach (ConfigNode n in node.GetNodes("LOGPERIODS"))
            {
                foreach (ConfigNode pn in n.GetNodes("LOGPERIOD"))
                {
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
                    var c = new ContractEvent(cn);
                    _contractDict.Add(c);
                }
            }

            foreach (ConfigNode n in node.GetNodes("LAUNCHEVENTS"))
            {
                foreach (ConfigNode ln in n.GetNodes("LAUNCHEVENT"))
                {
                    var l = new LaunchEvent(ln);
                    _launchedVessels.Add(l);
                }
            }

            foreach (ConfigNode n in node.GetNodes("FACILITYCONSTRUCTIONS"))
            {
                foreach (ConfigNode fn in n.GetNodes("FACILITYCONSTRUCTION"))
                {
                    var fc = new FacilityConstructionEvent(fn);
                    _facilityConstructions.Add(fc);
                }
            }

            foreach (ConfigNode n in node.GetNodes("TECHS"))
            {
                foreach (ConfigNode tn in n.GetNodes("TECH"))
                {
                    var te = new TechResearchEvent(tn);
                    _techEvents.Add(te);
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            var n = node.AddNode("LOGPERIODS");
            foreach (LogPeriod e in _periodDict.Values)
            {
                e.Save(n.AddNode("LOGPERIOD"));
            }

            n = node.AddNode("CONTRACTS");
            foreach (ContractEvent c in _contractDict)
            {
                c.Save(n.AddNode("CONTRACT"));
            }

            n = node.AddNode("LAUNCHEVENTS");
            foreach (LaunchEvent l in _launchedVessels)
            {
                l.Save(n.AddNode("LAUNCHEVENT"));
            }

            n = node.AddNode("FACILITYCONSTRUCTIONS");
            foreach (FacilityConstructionEvent fc in _facilityConstructions)
            {
                fc.Save(n.AddNode("FACILITYCONSTRUCTION"));
            }

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
                                  .Select(p => 
            {
                double advanceFunds = _contractDict.Where(c => c.Type == ContractEventType.Accept && c.IsInPeriod(p))
                                                   .Select(c => c.FundsChange)
                                                   .Sum();

                double rewardFunds = _contractDict.Where(c => c.Type == ContractEventType.Complete && c.IsInPeriod(p))
                                                  .Select(c => c.FundsChange)
                                                  .Sum();

                double failureFunds = -_contractDict.Where(c => (c.Type == ContractEventType.Cancel || c.Type == ContractEventType.Fail) && c.IsInPeriod(p))
                                                    .Select(c => c.FundsChange)
                                                    .Sum();

                double constructionFees = _facilityConstructions.Where(f => f.State == ConstructionState.Started && f.IsInPeriod(p))
                                                                .Select(c => c.Cost)
                                                                .Sum();
                return new[]
                {
                    _epoch.AddSeconds(p.StartUT).ToString("yyyy-MM"),
                    p.VABUpgrades.ToString(),
                    p.SPHUpgrades.ToString(),
                    p.RnDUpgrades.ToString(),
                    p.CurrentFunds.ToString("F0"),
                    p.CurrentSci.ToString("F1"),
                    p.ScienceEarned.ToString("F1"),
                    advanceFunds.ToString("F0"),
                    rewardFunds.ToString("F0"),
                    failureFunds.ToString("F0"),
                    p.OtherFundsEarned.ToString("F0"),
                    p.LaunchFees.ToString("F0"),
                    p.MaintenanceFees.ToString("F0"),
                    p.ToolingFees.ToString("F0"),
                    p.EntryCosts.ToString("F0"),
                    constructionFees.ToString("F0"),
                    (p.OtherFees - constructionFees).ToString("F0"),
                    string.Join(", ", _launchedVessels.Where(l => l.IsInPeriod(p))
                                                      .Select(l => l.VesselName)
                                                      .ToArray()),
                    string.Join(", ", _contractDict.Where(c => c.Type == ContractEventType.Accept && c.IsInPeriod(p))
                                                   .Select(c => $"{c.DisplayName}")
                                                   .ToArray()),
                    string.Join(", ", _contractDict.Where(c => c.Type == ContractEventType.Complete && c.IsInPeriod(p))
                                                   .Select(c => $"{c.DisplayName}")
                                                   .ToArray()),
                    string.Join(", ", _techEvents.Where(t => t.IsInPeriod(p))
                                                 .Select(t => t.NodeName)
                                                 .ToArray()),
                    string.Join(", ", _facilityConstructions.Where(f => f.IsInPeriod(p))
                                                            .Select(f => $"{f.Facility} ({f.NewLevel + 1}) - {f.State}")
                                                            .ToArray())
                };
            });

            var columnNames = new[] { "Month", "VAB", "SPH", "RnD", "Current Funds", "Current Sci", "Total sci earned", "Contract advances", "Contract rewards", "Contract penalties", "Other funds earned", "Launch fees", "Maintenance", "Tooling", "Entry Costs", "Facility construction costs", "Other Fees", "Launches", "Accepted contracts", "Completed contracts", "Tech", "Facilities" };
            var csv = CsvWriter.WriteToText(columnNames, rows, ',');
            File.WriteAllText(path, csv);
        }


        public void ExportToWeb()
        {
            StartCoroutine(PostRequestCareerLog("https://kspro-rp1-analytics.herokuapp.com/api/careerlogs"));
        }

        private IEnumerator PostRequestCareerLog(string url)
        {
            var logPeriods = _periodDict.Select(p => p.Value)
                .Select(CreateLogDto).ToArray();

            // Create JSON structure for arrays - afaict not supported on this unity version out of the box
            var jsonToSend = "[";

            for (var i = 0; i < logPeriods.Length; i++)
            {
                if (i < logPeriods.Length - 1) jsonToSend += JsonUtility.ToJson(logPeriods[i]) + ",";
                else jsonToSend += JsonUtility.ToJson(logPeriods[i]) + "]";
            }

            var byteJson = new UTF8Encoding().GetBytes(jsonToSend);

            var uwr = new UnityWebRequest(url, "POST")
            { downloadHandler = new DownloadHandlerBuffer(), uploadHandler = new UploadHandlerRaw(byteJson) };

            uwr.SetRequestHeader("Content-Type", "application/json");

            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError)
            {
                Debug.Log("Error While Sending: " + uwr.error);
            }
            else
            {
                Debug.Log("Received: " + uwr.downloadHandler.text);
            }
        }

        private CareerLogDto CreateLogDto(LogPeriod logPeriod)
        {
            double advanceFunds = _contractDict
                .Where(c => c.Type == ContractEventType.Accept && c.IsInPeriod(logPeriod))
                .Select(c => c.FundsChange)
                .Sum();

            double rewardFunds = _contractDict
                .Where(c => c.Type == ContractEventType.Complete && c.IsInPeriod(logPeriod))
                .Select(c => c.FundsChange)
                .Sum();

            double failureFunds = -_contractDict.Where(c =>
                    (c.Type == ContractEventType.Cancel || c.Type == ContractEventType.Fail) && c.IsInPeriod(logPeriod))
                .Select(c => c.FundsChange)
                .Sum();

            double constructionFees = _facilityConstructions
                .Where(f => f.State == ConstructionState.Started && f.IsInPeriod(logPeriod))
                .Select(c => c.Cost)
                .Sum();


            return new CareerLogDto
            {
                careerUuid = SystemInfo.deviceUniqueIdentifier,
                epoch = _epoch.AddSeconds(logPeriod.StartUT).ToString("yyyy-MM"),
                vabUpgrades = logPeriod.VABUpgrades.ToString(),
                sphUpgrades = logPeriod.SPHUpgrades.ToString(),
                rndUpgrades = logPeriod.RnDUpgrades.ToString(),
                currentFunds = logPeriod.CurrentFunds.ToString("F0"),
                currentSci = logPeriod.CurrentSci.ToString("F1"),
                scienceEarned = logPeriod.ScienceEarned.ToString("F1"),
                advanceFunds = advanceFunds.ToString("F0"),
                rewardFunds = rewardFunds.ToString("F0"),
                failureFunds = failureFunds.ToString("F0"),
                otherFundsEarned = logPeriod.OtherFundsEarned.ToString("F0"),
                launchFees = logPeriod.LaunchFees.ToString("F0"),
                maintenanceFees = logPeriod.MaintenanceFees.ToString("F0"),
                toolingFees = logPeriod.ToolingFees.ToString("F0"),
                entryCosts = logPeriod.EntryCosts.ToString("F0"),
                constructionFees = logPeriod.OtherFees.ToString("F0"),
                otherFees = (logPeriod.OtherFees - constructionFees).ToString("F0"),
                launchedVessels = _launchedVessels.Where(l => l.UT >= logPeriod.StartUT && l.UT < logPeriod.EndUT)
                    .Select(l => l.VesselName)
                    .ToArray(),
                contractEvents = _contractDict.Where(c =>
                        c.Type == ContractEventType.Complete && c.UT >= logPeriod.StartUT && c.UT < logPeriod.EndUT)
                    .Select(c => c.DisplayName)
                    .ToArray(),
                techEvents = _techEvents.Where(t => t.UT >= logPeriod.StartUT && t.UT < logPeriod.EndUT)
                    .Select(t => t.NodeName)
                    .ToArray(),
                facilityConstructions = _facilityConstructions
                    .Where(f => f.State == ConstructionState.Completed && f.UT >= logPeriod.StartUT &&
                                f.UT < logPeriod.EndUT)
                    .Select(f => $"{f.Facility} ({f.NewLevel + 1})")
                    .ToArray()
            };
        }


        private void LoadSettings(ConfigNode data)
        {
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
                CurrentPeriod.MaintenanceFees -= changeDelta;
                return;
            }

            if (CareerEventScope.Current?.EventType == CareerEventType.Tooling)
            {
                CurrentPeriod.ToolingFees -= changeDelta;
                return;
            }

            if (reason == TransactionReasons.VesselRollout || reason == TransactionReasons.VesselRecovery)
            {
                CurrentPeriod.LaunchFees -= changeDelta;
                return;
            }

            if (reason == TransactionReasons.RnDPartPurchase)
            {
                CurrentPeriod.EntryCosts -= changeDelta;
                return;
            }

            if (changeDelta > 0)
            {
                CurrentPeriod.OtherFundsEarned += changeDelta;
            }
            else
            {
                CurrentPeriod.OtherFees -= changeDelta;
            }
        }

        private void ContractAccepted(Contract c)
        {
            if (c.AutoAccept) return;   // Do not record the Accept event for record contracts

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
