using Contracts;
using Csv;
using KerbalConstructionTime;
using RP0.Programs;
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

        private static readonly DateTime _epoch = new DateTime(1951, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private EventData<TechItem> onKctTechCompletedEvent;
        private EventData<FacilityUpgrade> onKctFacilityUpgradeQueuedEvent;
        private EventData<FacilityUpgrade> onKctFacilityUpgradeCancelEvent;
        private EventData<FacilityUpgrade> onKctFacilityUpgradeCompletedEvent;
        private EventData<LCConstruction, LCItem> onKctLCConstructionQueuedEvent;
        private EventData<LCConstruction, LCItem> onKctLCConstructionCancelEvent;
        private EventData<LCConstruction, LCItem> onKctLCConstructionCompleteEvent;
        private EventData<LCItem> onKctLCDismantledEvent;
        private EventData<PadConstruction, KCT_LaunchPad> onKctPadConstructionQueuedEvent;
        private EventData<PadConstruction, KCT_LaunchPad> onKctPadConstructionCancelEvent;
        private EventData<PadConstruction, KCT_LaunchPad> onKctPadConstructionCompletedEvent;
        private EventData<KCT_LaunchPad> onKctPadDismantledEvent;
        private readonly Dictionary<double, LogPeriod> _periodDict = new Dictionary<double, LogPeriod>();
        private readonly List<ContractEvent> _contractDict = new List<ContractEvent>();
        private readonly List<LaunchEvent> _launchedVessels = new List<LaunchEvent>();
        private readonly List<FailureEvent> _failures = new List<FailureEvent>();
        private readonly List<LCLogItem> _lcs = new List<LCLogItem>();
        private readonly List<FacilityConstruction> _facilityConstructions = new List<FacilityConstruction>();
        private readonly List<LPConstruction> _lpConstructions = new List<LPConstruction>();
        private readonly List<FacilityConstructionEvent> _facilityConstructionEvents = new List<FacilityConstructionEvent>();
        private readonly List<TechResearchEvent> _techEvents = new List<TechResearchEvent>();
        private bool _eventsBound = false;
        private bool _launched = false;
        private MethodInfo _mInfContractName;
        private double _prevFundsChangeAmount;
        private TransactionReasons _prevFundsChangeReason;
        private LogPeriod _currentPeriod;

        public static CareerLog Instance { get; private set; }

        /// <summary>
        /// Default means to get hype for career not using Headlines
        /// </summary>
        public static Func<double> GetHeadlinesHype = () => { return 0; };

        public LogPeriod CurrentPeriod
        { 
            get
            {
                double time = KSPUtils.GetUT();
                while (time > NextPeriodStart)
                {
                    SwitchToNextPeriod();
                }

                if (_currentPeriod == null)
                {
                    _currentPeriod = GetOrCreatePeriod(CurPeriodStart);
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

        public void Start()
        {
            onKctTechCompletedEvent = GameEvents.FindEvent<EventData<TechItem>>("OnKctTechCompleted");
            if (onKctTechCompletedEvent != null)
            {
                onKctTechCompletedEvent.Add(OnKctTechCompleted);
            }

            onKctFacilityUpgradeQueuedEvent = GameEvents.FindEvent<EventData<FacilityUpgrade>>("OnKctFacilityUpgradeQueued");
            if (onKctFacilityUpgradeQueuedEvent != null)
            {
                onKctFacilityUpgradeQueuedEvent.Add(OnKctFacilityUpgdQueued);
            }

            onKctFacilityUpgradeCancelEvent = GameEvents.FindEvent<EventData<FacilityUpgrade>>("OnKctFacilityUpgradeCancel");
            if (onKctFacilityUpgradeCancelEvent != null)
            {
                onKctFacilityUpgradeCancelEvent.Add(OnKctFacilityUpgdCancel);
            }

            onKctFacilityUpgradeCompletedEvent = GameEvents.FindEvent<EventData<FacilityUpgrade>>("OnKctFacilityUpgradeComplete");
            if (onKctFacilityUpgradeCompletedEvent != null)
            {
                onKctFacilityUpgradeCompletedEvent.Add(OnKctFacilityUpgdComplete);
            }

            onKctLCConstructionQueuedEvent = GameEvents.FindEvent<EventData<LCConstruction, LCItem>>("OnKctLCConstructionQueued");
            if (onKctLCConstructionQueuedEvent != null)
            {
                onKctLCConstructionQueuedEvent.Add(OnKctLCConstructionQueued);
            }

            onKctLCConstructionCancelEvent = GameEvents.FindEvent<EventData<LCConstruction, LCItem>>("OnKctLCConstructionCancel");
            if (onKctLCConstructionCancelEvent != null)
            {
                onKctLCConstructionCancelEvent.Add(OnKctLCConstructionCancel);
            }

            onKctLCConstructionCompleteEvent = GameEvents.FindEvent<EventData<LCConstruction, LCItem>>("OnKctLCConstructionComplete");
            if (onKctLCConstructionCompleteEvent != null)
            {
                onKctLCConstructionCompleteEvent.Add(OnKctLCConstructionComplete);
            }

            onKctLCDismantledEvent = GameEvents.FindEvent<EventData<LCItem>>("OnKctLCDismantled");
            if (onKctLCDismantledEvent != null)
            {
                onKctLCDismantledEvent.Add(OnKctLCDismantled);
            }

            onKctPadConstructionQueuedEvent = GameEvents.FindEvent<EventData<PadConstruction, KCT_LaunchPad>>("OnKctPadConstructionQueued");
            if (onKctPadConstructionQueuedEvent != null)
            {
                onKctPadConstructionQueuedEvent.Add(OnKctPadConstructionQueued);
            }

            onKctPadConstructionCancelEvent = GameEvents.FindEvent<EventData<PadConstruction, KCT_LaunchPad>>("OnKctPadConstructionCancel");
            if (onKctPadConstructionCancelEvent != null)
            {
                onKctPadConstructionCancelEvent.Add(OnKctPadConstructionCancel);
            }

            onKctPadConstructionCompletedEvent = GameEvents.FindEvent<EventData<PadConstruction, KCT_LaunchPad>>("OnKctPadConstructionComplete");
            if (onKctPadConstructionCompletedEvent != null)
            {
                onKctPadConstructionCompletedEvent.Add(OnKctPadConstructionComplete);
            }

            onKctPadDismantledEvent = GameEvents.FindEvent<EventData<KCT_LaunchPad>>("OnKctPadDismantled");
            if (onKctPadDismantledEvent != null)
            {
                onKctPadDismantledEvent.Add(OnKctPadDismantled);
            }
        }

        public void OnDestroy()
        {
            GameEvents.onGameStateLoad.Remove(LoadSettings);
            GameEvents.OnGameSettingsApplied.Remove(SettingsChanged);

            if (onKctTechCompletedEvent != null) onKctTechCompletedEvent.Remove(OnKctTechCompleted);
            if (onKctFacilityUpgradeQueuedEvent != null) onKctFacilityUpgradeQueuedEvent.Remove(OnKctFacilityUpgdQueued);
            if (onKctFacilityUpgradeCancelEvent != null) onKctFacilityUpgradeCancelEvent.Remove(OnKctFacilityUpgdCancel);
            if (onKctFacilityUpgradeCompletedEvent != null) onKctFacilityUpgradeCompletedEvent.Remove(OnKctFacilityUpgdComplete);
            if (onKctLCConstructionQueuedEvent != null) onKctLCConstructionQueuedEvent.Remove(OnKctLCConstructionQueued);
            if (onKctLCConstructionCancelEvent != null) onKctLCConstructionCancelEvent.Remove(OnKctLCConstructionCancel);
            if (onKctLCConstructionCompleteEvent != null) onKctLCConstructionCompleteEvent.Remove(OnKctLCConstructionComplete);
            if (onKctLCDismantledEvent != null) onKctLCDismantledEvent.Remove(OnKctLCDismantled);
            if (onKctPadConstructionQueuedEvent != null) onKctPadConstructionQueuedEvent.Remove(OnKctPadConstructionQueued);
            if (onKctPadConstructionCancelEvent != null) onKctPadConstructionCancelEvent.Remove(OnKctPadConstructionCancel);
            if (onKctPadConstructionCompletedEvent != null) onKctPadConstructionCompletedEvent.Remove(OnKctPadConstructionComplete);
            if (onKctPadDismantledEvent != null) onKctPadDismantledEvent.Remove(OnKctPadDismantled);

            if (_eventsBound)
            {
                GameEvents.onVesselSituationChange.Remove(VesselSituationChange);
                GameEvents.onCrewKilled.Remove(CrewKilled);
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

            foreach (ConfigNode n in node.GetNodes("FAILUREEVENTS"))
            {
                foreach (ConfigNode fn in n.GetNodes("FAILUREEVENT"))
                {
                    var f = new FailureEvent(fn);
                    _failures.Add(f);
                }
            }

            foreach (ConfigNode n in node.GetNodes("LCS"))
            {
                foreach (ConfigNode fn in n.GetNodes("LC"))
                {
                    var lc = new LCLogItem(fn);
                    _lcs.Add(lc);
                }
            }

            foreach (ConfigNode n in node.GetNodes("FACILITYCONSTRUCTIONS"))
            {
                foreach (ConfigNode fn in n.GetNodes("FACILITYCONSTRUCTION"))
                {
                    var fc = new FacilityConstruction(fn);
                    _facilityConstructions.Add(fc);
                }
            }

            foreach (ConfigNode n in node.GetNodes("LPCONSTRUCTIONS"))
            {
                foreach (ConfigNode fn in n.GetNodes("LPCONSTRUCTION"))
                {
                    var fc = new LPConstruction(fn);
                    _lpConstructions.Add(fc);
                }
            }

            foreach (ConfigNode n in node.GetNodes("FACILITYCONSTREVENTS"))
            {
                foreach (ConfigNode fn in n.GetNodes("FACILITYCONSTREVENT"))
                {
                    var fc = new FacilityConstructionEvent(fn);
                    _facilityConstructionEvents.Add(fc);
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

            n = node.AddNode("FAILUREEVENTS");
            foreach (FailureEvent f in _failures)
            {
                f.Save(n.AddNode("FAILUREEVENT"));
            }

            n = node.AddNode("LCS");
            foreach (LCLogItem lc in _lcs)
            {
                lc.Save(n.AddNode("LC"));
            }

            n = node.AddNode("FACILITYCONSTRUCTIONS");
            foreach (FacilityConstruction fc in _facilityConstructions)
            {
                fc.Save(n.AddNode("FACILITYCONSTRUCTION"));
            }

            n = node.AddNode("LPCONSTRUCTIONS");
            foreach (LPConstruction lp in _lpConstructions)
            {
                lp.Save(n.AddNode("LPCONSTRUCTION"));
            }

            n = node.AddNode("FACILITYCONSTREVENTS");
            foreach (FacilityConstructionEvent fc in _facilityConstructionEvents)
            {
                fc.Save(n.AddNode("FACILITYCONSTREVENT"));
            }

            n = node.AddNode("TECHS");
            foreach (TechResearchEvent tr in _techEvents)
            {
                tr.Save(n.AddNode("TECH"));
            }
        }

        public static DateTime UTToDate(double ut)
        {
            return _epoch.AddSeconds(ut);
        }

        public void AddTechEvent(TechItem tech)
        {
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

            _techEvents.Add(new TechResearchEvent(KSPUtils.GetUT())
            {
                NodeName = tech.ProtoNode.techID,
                YearMult = tech.YearBasedRateMult,
                ResearchRate = tech.BuildRate
            });
        }

        public void AddFailureEvent(Vessel v, string part, string type)
        {
            if (!IsEnabled) return;

            _failures.Add(new FailureEvent(KSPUtils.GetUT())
            {
                VesselUID = v.GetKCTVesselId(),
                LaunchID = v.GetVesselLaunchId(),
                Part = part,
                Type = type
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
                return new[]
                {
                    UTToDate(p.StartUT).ToString("yyyy-MM"),
                    p.NumEngineers.ToString(),
                    p.NumResearchers.ToString(),
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
                    p.ConstructionFees.ToString("F0"),
                    p.OtherFees.ToString("F0"),
                    p.Confidence.ToString("F1"),
                    p.Reputation.ToString("F1"),
                    p.HeadlinesHype.ToString("F1"),
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
                    // TODO: LCs and LPs
                    string.Join(", ", _facilityConstructionEvents.Where(f => f.IsInPeriod(p))
                                                                 .Select(f => $"{f.Facility} ({(_facilityConstructions.FirstOrDefault(fc => fc.FacilityID == f.FacilityID)?.NewLevel ?? -1) + 1}) - {f.State}")
                                                                 .ToArray())
                };
            });

            var columnNames = new[] { "Month", "VAB", "SPH", "RnD", "Current Funds", "Current Sci", "Total sci earned", "Contract advances", "Contract rewards", "Contract penalties", "Other funds earned", "Launch fees", "Maintenance", "Tooling", "Entry Costs", "Facility construction costs", "Other Fees", "Confidence", "Reputation", "Headlines Reputation", "Launches", "Accepted contracts", "Completed contracts", "Tech", "Facilities" };
            var csv = CsvWriter.WriteToText(columnNames, rows, ',');
            File.WriteAllText(path, csv);
        }

        public void ExportToWeb(string serverUrl, string token, Action onRequestSuccess, Action<string> onRequestFail)
        {
            string url = $"{serverUrl.TrimEnd('/')}/{token}";
            StartCoroutine(PostRequestCareerLog(url, onRequestSuccess, onRequestFail));
        }

        private IEnumerator PostRequestCareerLog(string url, Action onRequestSuccess, Action<string> onRequestFail)
        {
            var logPeriods = _periodDict.Select(p => p.Value)
                .Select(CreateLogDto).ToArray();

            const string jsonVer = "2.0";
            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(GetType().Assembly.Location);
            string rp1Ver = fvi.FileVersion;

            // Create JSON structure for arrays - afaict not supported on this unity version out of the box
            var jsonToSend = "{ \"jsonVer\": \"" + jsonVer + "\", ";
            jsonToSend += "\"rp1Ver\": \"" + rp1Ver + "\", ";
            jsonToSend += "\"periods\": [";

            for (var i = 0; i < logPeriods.Length; i++)
            {
                if (i < logPeriods.Length - 1) jsonToSend += JsonUtility.ToJson(logPeriods[i]) + ",";
                else jsonToSend += JsonUtility.ToJson(logPeriods[i]);
            }

            jsonToSend += "], \"contractEvents\": [";

            for (var i = 0; i < _contractDict.Count; i++)
            {
                var dto = new ContractEventDto(_contractDict[i]);
                if (i < _contractDict.Count - 1) jsonToSend += JsonUtility.ToJson(dto) + ",";
                else jsonToSend += JsonUtility.ToJson(dto);
            }

            jsonToSend += "], \"facilityConstructions\": [";

            for (var i = 0; i < _facilityConstructions.Count; i++)
            {
                var dto = new FacilityConstructionDto(_facilityConstructions[i]);
                if (i < _facilityConstructions.Count - 1) jsonToSend += JsonUtility.ToJson(dto) + ",";
                else jsonToSend += JsonUtility.ToJson(dto);
            }

            jsonToSend += "], \"lcs\": [";

            for (var i = 0; i < _lcs.Count; i++)
            {
                var dto = new LCDto(_lcs[i]);
                if (i < _lcs.Count - 1) jsonToSend += JsonUtility.ToJson(dto) + ",";
                else jsonToSend += JsonUtility.ToJson(dto);
            }

            jsonToSend += "], \"lpConstructions\": [";

            for (var i = 0; i < _lpConstructions.Count; i++)
            {
                var dto = new LPConstructionDto(_lpConstructions[i]);
                if (i < _lpConstructions.Count - 1) jsonToSend += JsonUtility.ToJson(dto) + ",";
                else jsonToSend += JsonUtility.ToJson(dto);
            }

            jsonToSend += "], \"facilityEvents\": [";

            for (var i = 0; i < _facilityConstructionEvents.Count; i++)
            {
                var dto = new FacilityConstructionEventDto(_facilityConstructionEvents[i]);
                if (i < _facilityConstructionEvents.Count - 1) jsonToSend += JsonUtility.ToJson(dto) + ",";
                else jsonToSend += JsonUtility.ToJson(dto);
            }

            jsonToSend += "], \"techEvents\": [";

            for (var i = 0; i < _techEvents.Count; i++)
            {
                var dto = new TechResearchEventDto(_techEvents[i]);
                if (i < _techEvents.Count - 1) jsonToSend += JsonUtility.ToJson(dto) + ",";
                else jsonToSend += JsonUtility.ToJson(dto);
            }

            jsonToSend += "], \"launchEvents\": [";

            for (var i = 0; i < _launchedVessels.Count; i++)
            {
                var dto = new LaunchEventDto(_launchedVessels[i]);
                if (i < _launchedVessels.Count - 1) jsonToSend += JsonUtility.ToJson(dto) + ",";
                else jsonToSend += JsonUtility.ToJson(dto);
            }

            jsonToSend += "], \"failureEvents\": [";

            for (var i = 0; i < _failures.Count; i++)
            {
                var dto = new FailureEventDto(_failures[i]);
                if (i < _failures.Count - 1) jsonToSend += JsonUtility.ToJson(dto) + ",";
                else jsonToSend += JsonUtility.ToJson(dto);
            }

            jsonToSend += "], \"programs\": [";

            var allPrograms = ProgramHandler.Instance.CompletedPrograms.Concat(ProgramHandler.Instance.ActivePrograms).ToArray();
            for (var i = 0; i < allPrograms.Length; i++)
            {
                var dto = new ProgramDto(allPrograms[i]);
                if (i < allPrograms.Length - 1) jsonToSend += JsonUtility.ToJson(dto) + ",";
                else jsonToSend += JsonUtility.ToJson(dto);
            }

            jsonToSend += "] }";

            Debug.Log("[RP-0] Request payload: " + jsonToSend);

            var byteJson = new UTF8Encoding().GetBytes(jsonToSend);

            var uwr = new UnityWebRequest(url, "PATCH")
            {
                downloadHandler = new DownloadHandlerBuffer(),
                uploadHandler = new UploadHandlerRaw(byteJson)
            };

            #if DEBUG
            uwr.certificateHandler = new BypassCertificateHandler();
            #endif

            uwr.SetRequestHeader("Content-Type", "application/json");

            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError || uwr.isHttpError)
            {
                onRequestFail(uwr.error);
                Debug.Log($"Error While Sending: {uwr.error}; {uwr.downloadHandler.text}");
            }
            else
            {
                onRequestSuccess();
                Debug.Log("Received: " + uwr.downloadHandler.text);
            }
        }

        private CareerLogDto CreateLogDto(LogPeriod logPeriod)
        {
            return new CareerLogDto
            {
                careerUuid = SystemInfo.deviceUniqueIdentifier,
                startDate = UTToDate(logPeriod.StartUT).ToString("o"),
                endDate = UTToDate(logPeriod.EndUT).ToString("o"),
                numEngineers = logPeriod.NumEngineers,
                numResearchers = logPeriod.NumResearchers,
                efficiencyEngineers = logPeriod.EfficiencyEngineers,
                efficiencyResearchers = logPeriod.EfficiencyResearchers,
                currentFunds = logPeriod.CurrentFunds,
                currentSci = logPeriod.CurrentSci,
                scienceEarned = logPeriod.ScienceEarned,
                programFunds = logPeriod.ProgramFunds,
                otherFundsEarned = logPeriod.OtherFundsEarned,
                launchFees = logPeriod.LaunchFees,
                maintenanceFees = logPeriod.MaintenanceFees,
                toolingFees = logPeriod.ToolingFees,
                entryCosts = logPeriod.EntryCosts,
                constructionFees = logPeriod.ConstructionFees,
                otherFees = logPeriod.OtherFees,
                subsidySize = logPeriod.SubsidySize,
                subsidyPaidOut = logPeriod.SubsidyPaidOut,
                repFromPrograms = logPeriod.RepFromPrograms,
                fundsGainMult = logPeriod.FundsGainMult,
                numNautsKilled = logPeriod.NumNautsKilled,
                confidence = logPeriod.Confidence,
                reputation = logPeriod.Reputation,
                headlinesHype = logPeriod.HeadlinesHype
            };
        }

        private void SwitchToNextPeriod()
        {
            LogPeriod _prevPeriod = _currentPeriod ?? GetOrCreatePeriod(CurPeriodStart);
            if (_prevPeriod != null)
            {
                _prevPeriod.CurrentFunds = Funding.Instance.Funds;
                _prevPeriod.CurrentSci = ResearchAndDevelopment.Instance.Science;
                _prevPeriod.NumEngineers = KCTGameStates.TotalEngineers;
                _prevPeriod.NumResearchers = KCTGameStates.Researchers;
                _prevPeriod.EfficiencyEngineers = KCTGameStates.EfficiencyEngineers;
                _prevPeriod.EfficiencyResearchers = KCTGameStates.EfficiencyResearchers;
                _prevPeriod.ScienceEarned = GetSciPointTotalFromKCT();
                _prevPeriod.FundsGainMult = HighLogic.CurrentGame.Parameters.Career.FundsGainMultiplier;
                _prevPeriod.SubsidySize = MaintenanceHandler.Instance.GetSubsidyAmountForSeconds(_prevPeriod.EndUT - _prevPeriod.StartUT);
                _prevPeriod.Confidence = Confidence.CurrentConfidence;
                _prevPeriod.Reputation = Reputation.CurrentRep;
                _prevPeriod.HeadlinesHype = GetHeadlinesHype();
            }

            _currentPeriod = GetOrCreatePeriod(NextPeriodStart);
            CurPeriodStart = NextPeriodStart;
            NextPeriodStart = _currentPeriod.EndUT;
        }

        private LogPeriod GetOrCreatePeriod(double periodStartUt)
        {
            if (!_periodDict.TryGetValue(periodStartUt, out LogPeriod period))
            {
                DateTime dtNextPeriod = UTToDate(periodStartUt).AddMonths(LogPeriodMonths);
                double nextPeriodStart = (dtNextPeriod - _epoch).TotalSeconds;
                period = new LogPeriod(periodStartUt, nextPeriodStart);
                _periodDict.Add(periodStartUt, period);
            }
            return period;
        }

        private void LoadSettings(ConfigNode data)
        {
            IsEnabled = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().CareerLogEnabled;

            if (IsEnabled && !_eventsBound)
            {
                _eventsBound = true;
                GameEvents.onVesselSituationChange.Add(VesselSituationChange);
                GameEvents.onCrewKilled.Add(CrewKilled);
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
            if (CareerEventScope.ShouldIgnore) return;

            float fundsDelta = query.GetTotal(Currency.Funds);
            if (fundsDelta != 0f)
            {
                FundsChanged(fundsDelta, query.reason);
            }

            float repDelta = query.GetTotal(Currency.Reputation);
            if (repDelta != 0f)
            {
                RepChanged(repDelta, query.reason);
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

            if (reason == TransactionReasons.Mission)
            {
                CurrentPeriod.ProgramFunds += changeDelta;
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

            if (reason == TransactionReasons.StructureConstruction)
            {
                CurrentPeriod.ConstructionFees -= changeDelta;
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

        private void RepChanged(float repDelta, TransactionReasons reason)
        {
            if (reason == TransactionReasons.Mission)
            {
                CurrentPeriod.RepFromPrograms += repDelta;
                return;
            }
        }

        private void ContractAccepted(Contract c)
        {
            if (CareerEventScope.ShouldIgnore || c.AutoAccept) return;   // Do not record the Accept event for record contracts

            _contractDict.Add(new ContractEvent(KSPUtils.GetUT())
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
            if (CareerEventScope.ShouldIgnore) return;

            _contractDict.Add(new ContractEvent(KSPUtils.GetUT())
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
            if (CareerEventScope.ShouldIgnore) return;

            // KSP first takes the contract penalty and then fires the contract events
            double fundsChange = 0;
            if (_prevFundsChangeReason == TransactionReasons.ContractPenalty)
            {
                Debug.Log($"[RP-0] Found that {_prevFundsChangeAmount} was given as contract penalty");
                fundsChange = _prevFundsChangeAmount;
            }

            _contractDict.Add(new ContractEvent(KSPUtils.GetUT())
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
            if (CareerEventScope.ShouldIgnore) return;

            string internalName = GetContractInternalName(c);
            double ut = KSPUtils.GetUT();
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

        public void ProgramAccepted(Program p)
        {
            // TODO
        }

        public void ProgramObjectivesMet(Program p)
        {
            // TODO
        }

        public void ProgramCompleted(Program p)
        {
            // TODO
        }

        private void VesselSituationChange(GameEvents.HostedFromToAction<Vessel, Vessel.Situations> ev)
        {
            if (CareerEventScope.ShouldIgnore) return;

            // KJR can clobber the vessel back to prelaunch state in case of clamp wobble. Need to exclude such events.
            if (!_launched && ev.from == Vessel.Situations.PRELAUNCH && ev.host == FlightGlobals.ActiveVessel)
            {
                Debug.Log($"[RP-0] Launching {FlightGlobals.ActiveVessel?.vesselName}");

                _launched = true;
                _launchedVessels.Add(new LaunchEvent(KSPUtils.GetUT())
                {
                    VesselName = FlightGlobals.ActiveVessel?.vesselName,
                    VesselUID = ev.host.GetKCTVesselId(),
                    LaunchID = ev.host.GetVesselLaunchId(),
                    LCID = ev.host.GetVesselLCID(),
                    LCModID = ev.host.GetVesselLCModID(),
                    BuiltAt = ev.host.GetVesselBuiltAt() ?? EditorFacility.None    // KSP can't serialize nullables,
                });
            }
        }

        private void CrewKilled(EventReport data)
        {
            if (CareerEventScope.ShouldIgnore) return;

            CurrentPeriod.NumNautsKilled++;
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

        private float GetSciPointTotalFromKCT()
        {
            try
            {
                // KCT returns -1 if the player hasn't earned any sci yet
                return Math.Max(0, KCTGameStates.SciPointsTotal);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return 0;
            }
        }

        private void OnKctTechCompleted(TechItem tech)
        {
            if (CareerEventScope.ShouldIgnore) return;

            AddTechEvent(tech);
        }

        private void OnKctFacilityUpgdQueued(FacilityUpgrade data)
        {
            AddFacilityConstructionEvent(data, ConstructionState.Started);
        }

        private void OnKctFacilityUpgdCancel(FacilityUpgrade data)
        {
            AddFacilityConstructionEvent(data, ConstructionState.Cancelled);
        }

        private void OnKctFacilityUpgdComplete(FacilityUpgrade data)
        {
            AddFacilityConstructionEvent(data, ConstructionState.Completed);
        }

        private void OnKctLCConstructionQueued(LCConstruction data, LCItem lc)
        {
            AddLCConstructionEvent(data, lc, ConstructionState.Started);
        }

        private void OnKctLCConstructionCancel(LCConstruction data, LCItem lc)
        {
            AddLCConstructionEvent(data, lc, ConstructionState.Cancelled);
        }

        private void OnKctLCConstructionComplete(LCConstruction data, LCItem lc)
        {
            AddLCConstructionEvent(data, lc, ConstructionState.Completed);
        }

        private void OnKctLCDismantled(LCItem lc)
        {
            AddLCConstructionEvent(null, lc, ConstructionState.Dismantled);
        }

        private void OnKctPadConstructionQueued(PadConstruction data, KCT_LaunchPad lp)
        {
            AddPadConstructionEvent(data, lp, ConstructionState.Started);
        }

        private void OnKctPadConstructionCancel(PadConstruction data, KCT_LaunchPad lp)
        {
            AddPadConstructionEvent(data, lp, ConstructionState.Cancelled);
        }

        private void OnKctPadConstructionComplete(PadConstruction data, KCT_LaunchPad lp)
        {
            AddPadConstructionEvent(data, lp, ConstructionState.Completed);
        }

        private void OnKctPadDismantled(KCT_LaunchPad lp)
        {
            AddPadConstructionEvent(null, lp, ConstructionState.Dismantled);
        }

        private void AddFacilityConstructionEvent(FacilityUpgrade data, ConstructionState state)
        {
            if (CareerEventScope.ShouldIgnore || !IsEnabled || !data.FacilityType.HasValue) return;    // facility type can be null in case of third party mods that define custom facilities

            if (!_facilityConstructions.Any(fc => fc.FacilityID == data.ID))
            {
                _facilityConstructions.Add(new FacilityConstruction
                {
                    Facility = data.FacilityType.Value,
                    FacilityID = data.ID,
                    Cost = data.Cost,
                    NewLevel = data.UpgradeLevel
                });
            }

            _facilityConstructionEvents.Add(new FacilityConstructionEvent(KSPUtils.GetUT())
            {
                Facility = FacilityConstructionEvent.ParseFacilityType(data.FacilityType.Value),
                FacilityID = data.ID,
                State = state
            });
        }

        private void AddLCConstructionEvent(LCConstruction data, LCItem lc, ConstructionState state)
        {
            if (CareerEventScope.ShouldIgnore) return;

            Guid modId = data?.ModID ?? lc.ModID;    // Should only happen when LCs are created through code and thus do not have Construction items
            if (!_lcs.Any(logLC => logLC.ModID == modId))
            {
                var logItem = data == null ? new LCLogItem(lc) : new LCLogItem(data);
                logItem.ModID = modId;
                _lcs.Add(logItem);
            }

            _facilityConstructionEvents.Add(new FacilityConstructionEvent(KSPUtils.GetUT())
            {
                Facility = FacilityType.LaunchComplex,
                State = state,
                FacilityID = modId
            });
        }

        private void AddPadConstructionEvent(PadConstruction data, KCT_LaunchPad lp, ConstructionState state)
        {
            if (CareerEventScope.ShouldIgnore) return;

            Guid id = data?.ID ?? lp.id;
            LCItem lc = data?.LC ?? lp.LC;
            if (!_lpConstructions.Any(lpc => lpc.LPID == id))
            {
                _lpConstructions.Add(new LPConstruction
                {
                    LPID = id,
                    LCID = lc.ID,
                    LCModID = lc.ModID,
                    Cost = data?.Cost ?? 0
                });
            }

            _facilityConstructionEvents.Add(new FacilityConstructionEvent(KSPUtils.GetUT())
            {
                Facility = FacilityType.LaunchPad,
                State = state,
                FacilityID = id
            });
        }
    }
}
