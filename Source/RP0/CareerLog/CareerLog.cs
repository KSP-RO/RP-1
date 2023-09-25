﻿using Contracts;
using Csv;
using RP0.DataTypes;
using RP0.Programs;
using System;
using System.Collections;
using System.IO;
using System.Text;
using UniLinq;
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

        [KSPField(isPersistant = true)]
        public int LoadedSaveVersion = CurrentVersion;

        public bool IsEnabled = false;

        private const int CurrentVersion = 1;

        private EventData<ResearchProject> onKctTechCompletedEvent;
        private EventData<FacilityUpgradeProject> onKctFacilityUpgradeQueuedEvent;
        private EventData<FacilityUpgradeProject> onKctFacilityUpgradeCancelEvent;
        private EventData<FacilityUpgradeProject> onKctFacilityUpgradeCompletedEvent;
        private EventData<LCConstructionProject, LaunchComplex> onKctLCConstructionQueuedEvent;
        private EventData<LCConstructionProject, LaunchComplex> onKctLCConstructionCancelEvent;
        private EventData<LCConstructionProject, LaunchComplex> onKctLCConstructionCompleteEvent;
        private EventData<LaunchComplex> onKctLCDismantledEvent;
        private EventData<PadConstructionProject, LCLaunchPad> onKctPadConstructionQueuedEvent;
        private EventData<PadConstructionProject, LCLaunchPad> onKctPadConstructionCancelEvent;
        private EventData<PadConstructionProject, LCLaunchPad> onKctPadConstructionCompletedEvent;
        private EventData<LCLaunchPad> onKctPadDismantledEvent;

        [KSPField(isPersistant = true)]
        private readonly PersistentDictionary<double, LogPeriod> _periodDict = new PersistentDictionaryValueTypeKey<double, LogPeriod>();
        [KSPField(isPersistant = true)]
        private readonly PersistentList<ContractEvent> _contractDict = new PersistentList<ContractEvent>();
        [KSPField(isPersistant = true)]
        private readonly PersistentList<LaunchEvent> _launchedVessels = new PersistentList<LaunchEvent>();
        [KSPField(isPersistant = true)]
        private readonly PersistentList<FailureEvent> _failures = new PersistentList<FailureEvent>();
        [KSPField(isPersistant = true)]
        private readonly PersistentList<LCLogItem> _lcs = new PersistentList<LCLogItem>();
        [KSPField(isPersistant = true)]
        private readonly PersistentList<FacilityConstruction> _facilityConstructions = new PersistentList<FacilityConstruction>();
        [KSPField(isPersistant = true)]
        private readonly PersistentList<LPConstruction> _lpConstructions = new PersistentList<LPConstruction>();
        [KSPField(isPersistant = true)]
        private readonly PersistentList<FacilityConstructionEvent> _facilityConstructionEvents = new PersistentList<FacilityConstructionEvent>();
        [KSPField(isPersistant = true)]
        private readonly PersistentList<TechResearchEvent> _techEvents = new PersistentList<TechResearchEvent>();
        [KSPField(isPersistant = true)]
        private readonly PersistentList<LeaderEvent> _leaderEvents = new PersistentList<LeaderEvent>();

        private bool _eventsBound = false;
        private bool _launched = false;
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
                if (!IsEnabled) return null;

                double time = Planetarium.GetUniversalTime();
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
            onKctTechCompletedEvent = GameEvents.FindEvent<EventData<ResearchProject>>("OnKctTechCompleted");
            if (onKctTechCompletedEvent != null)
            {
                onKctTechCompletedEvent.Add(OnKctTechCompleted);
            }

            onKctFacilityUpgradeQueuedEvent = GameEvents.FindEvent<EventData<FacilityUpgradeProject>>("OnKctFacilityUpgradeQueued");
            if (onKctFacilityUpgradeQueuedEvent != null)
            {
                onKctFacilityUpgradeQueuedEvent.Add(OnKctFacilityUpgdQueued);
            }

            onKctFacilityUpgradeCancelEvent = GameEvents.FindEvent<EventData<FacilityUpgradeProject>>("OnKctFacilityUpgradeCancel");
            if (onKctFacilityUpgradeCancelEvent != null)
            {
                onKctFacilityUpgradeCancelEvent.Add(OnKctFacilityUpgdCancel);
            }

            onKctFacilityUpgradeCompletedEvent = GameEvents.FindEvent<EventData<FacilityUpgradeProject>>("OnKctFacilityUpgradeComplete");
            if (onKctFacilityUpgradeCompletedEvent != null)
            {
                onKctFacilityUpgradeCompletedEvent.Add(OnKctFacilityUpgdComplete);
            }

            onKctLCConstructionQueuedEvent = GameEvents.FindEvent<EventData<LCConstructionProject, LaunchComplex>>("OnKctLCConstructionQueued");
            if (onKctLCConstructionQueuedEvent != null)
            {
                onKctLCConstructionQueuedEvent.Add(OnKctLCConstructionQueued);
            }

            onKctLCConstructionCancelEvent = GameEvents.FindEvent<EventData<LCConstructionProject, LaunchComplex>>("OnKctLCConstructionCancel");
            if (onKctLCConstructionCancelEvent != null)
            {
                onKctLCConstructionCancelEvent.Add(OnKctLCConstructionCancel);
            }

            onKctLCConstructionCompleteEvent = GameEvents.FindEvent<EventData<LCConstructionProject, LaunchComplex>>("OnKctLCConstructionComplete");
            if (onKctLCConstructionCompleteEvent != null)
            {
                onKctLCConstructionCompleteEvent.Add(OnKctLCConstructionComplete);
            }

            onKctLCDismantledEvent = GameEvents.FindEvent<EventData<LaunchComplex>>("OnKctLCDismantled");
            if (onKctLCDismantledEvent != null)
            {
                onKctLCDismantledEvent.Add(OnKctLCDismantled);
            }

            onKctPadConstructionQueuedEvent = GameEvents.FindEvent<EventData<PadConstructionProject, LCLaunchPad>>("OnKctPadConstructionQueued");
            if (onKctPadConstructionQueuedEvent != null)
            {
                onKctPadConstructionQueuedEvent.Add(OnKctPadConstructionQueued);
            }

            onKctPadConstructionCancelEvent = GameEvents.FindEvent<EventData<PadConstructionProject, LCLaunchPad>>("OnKctPadConstructionCancel");
            if (onKctPadConstructionCancelEvent != null)
            {
                onKctPadConstructionCancelEvent.Add(OnKctPadConstructionCancel);
            }

            onKctPadConstructionCompletedEvent = GameEvents.FindEvent<EventData<PadConstructionProject, LCLaunchPad>>("OnKctPadConstructionComplete");
            if (onKctPadConstructionCompletedEvent != null)
            {
                onKctPadConstructionCompletedEvent.Add(OnKctPadConstructionComplete);
            }

            onKctPadDismantledEvent = GameEvents.FindEvent<EventData<LCLaunchPad>>("OnKctPadDismantled");
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

            if (LoadedSaveVersion < CurrentVersion)
            {
                if (LoadedSaveVersion < 1)
                {
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
                                RP0Debug.LogError($"LOGPERIOD for {periodStart} already exists, skipping...");
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

                    foreach (ConfigNode n in node.GetNodes("LEADEREVENTS"))
                    {
                        foreach (ConfigNode ln in n.GetNodes("LEADEREVENT"))
                        {
                            var le = new LeaderEvent(ln);
                            _leaderEvents.Add(le);
                        }
                    }
                }

                LoadedSaveVersion = CurrentVersion;
            }
        }

        public void AddTechEvent(ResearchProject tech)
        {
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

            _techEvents.Add(new TechResearchEvent(Planetarium.GetUniversalTime())
            {
                NodeName = tech.ProtoNode.techID,
                YearMult = tech.YearBasedRateMult,
                ResearchRate = tech.BuildRate
            });
        }

        public void AddLeaderEvent(string leaderName, bool isAdd, double cost)
        {
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

            _leaderEvents.Add(new LeaderEvent(Planetarium.GetUniversalTime())
            {
                LeaderName = leaderName,
                IsAdd = isAdd,
                Cost = cost
            });
        }

        public void AddFailureEvent(Vessel v, string part, string type)
        {
            if (!IsEnabled) return;

            _failures.Add(new FailureEvent(Planetarium.GetUniversalTime())
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
                double rewardRep = _contractDict.Where(c => c.Type == ContractEventType.Complete && c.IsInPeriod(p))
                                                .Select(c => c.RepChange)
                                                .Sum();
                return new[]
                {
                    DTUtils.UTToDate(p.StartUT).ToString("yyyy-MM"),
                    p.NumEngineers.ToString(),
                    p.NumResearchers.ToString(),
                    p.CurrentFunds.ToString("F0"),
                    p.CurrentSci.ToString("F1"),
                    p.ScienceEarned.ToString("F1"),
                    rewardRep.ToString("F0"),
                    p.OtherFundsEarned.ToString("F0"),
                    p.LaunchFees.ToString("F0"),
                    p.MaintenanceFees.ToString("F0"),
                    p.ToolingFees.ToString("F0"),
                    p.EntryCosts.ToString("F0"),
                    p.ConstructionFees.ToString("F0"),
                    p.HiringResearchers.ToString("F0"),
                    p.HiringEngineers.ToString("F0"),
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
                                                                 .ToArray()),
                    string.Join(", ", _leaderEvents.Where(l => l.IsInPeriod(p))
                                                 .Select(l => l.LeaderName + ": " + (l.IsAdd ? "add" : "remove"))
                                                 .ToArray()),
                };
            });

            var columnNames = new[] { "Month", "Engineers", "Researchers", "Current Funds", "Current Sci", "Total sci earned", "Contract rep rewards", "Other funds earned", "Launch fees", "Maintenance", "Tooling", "Entry Costs", "Facility construction costs", "Hiring researchers", "Hiring engineers", "Other Fees", "Confidence", "Reputation", "Headlines Reputation", "Launches", "Accepted contracts", "Completed contracts", "Tech", "Facilities", "Leaders" };
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

            jsonToSend += "], \"leaderEvents\": [";

            for (var i = 0; i < _leaderEvents.Count; i++)
            {
                var dto = new LeaderEventDto(_leaderEvents[i]);
                if (i < _leaderEvents.Count - 1) jsonToSend += JsonUtility.ToJson(dto) + ",";
                else jsonToSend += JsonUtility.ToJson(dto);
            }

            jsonToSend += "] }";

            RP0Debug.Log("Request payload: " + jsonToSend);

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
                RP0Debug.LogError($"Error While Sending: {uwr.error}; {uwr.downloadHandler.text}");
            }
            else
            {
                onRequestSuccess();
                RP0Debug.Log("Received: " + uwr.downloadHandler.text);
            }
        }

        private CareerLogDto CreateLogDto(LogPeriod logPeriod)
        {
            return new CareerLogDto
            {
                careerUuid = SystemInfo.deviceUniqueIdentifier,
                startDate = DTUtils.UTToDate(logPeriod.StartUT).ToString("o"),
                endDate = DTUtils.UTToDate(logPeriod.EndUT).ToString("o"),
                numEngineers = logPeriod.NumEngineers,
                numResearchers = logPeriod.NumResearchers,
                efficiencyEngineers = logPeriod.EfficiencyEngineers,
                currentFunds = logPeriod.CurrentFunds,
                currentSci = logPeriod.CurrentSci,
                rndQueueLength = logPeriod.RnDQueueLength,
                scienceEarned = logPeriod.ScienceEarned,
                salaryEngineers = logPeriod.SalaryEngineers,
                salaryResearchers = logPeriod.SalaryResearchers,
                salaryCrew = logPeriod.SalaryCrew,
                programFunds = logPeriod.ProgramFunds,
                otherFundsEarned = logPeriod.OtherFundsEarned,
                launchFees = logPeriod.LaunchFees,
                vesselPurchase = logPeriod.VesselPurchase,
                vesselRecovery = logPeriod.VesselRecovery,
                lcMaintenance = logPeriod.LCMaintenance,
                facilityMaintenance = logPeriod.FacilityMaintenance,
                maintenanceFees = logPeriod.MaintenanceFees,
                trainingFees = logPeriod.TrainingFees,
                toolingFees = logPeriod.ToolingFees,
                entryCosts = logPeriod.EntryCosts,
                spentUnlockCredit = logPeriod.SpentUnlockCredit,
                constructionFees = logPeriod.ConstructionFees,
                hiringResearchers = logPeriod.HiringResearchers,
                hiringEngineers = logPeriod.HiringEngineers,
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
                _prevPeriod.RnDQueueLength = KerbalConstructionTimeData.Instance.TechList.Sum(t => t.scienceCost);
                _prevPeriod.NumEngineers = KerbalConstructionTimeData.Instance.TotalEngineers;
                _prevPeriod.NumResearchers = KerbalConstructionTimeData.Instance.Researchers;
                _prevPeriod.EfficiencyEngineers = KerbalConstructionTimeData.Instance.WeightedAverageEfficiencyEngineers;
                _prevPeriod.ScienceEarned = GetSciPointTotalFromKCT();
                _prevPeriod.FundsGainMult = HighLogic.CurrentGame.Parameters.Career.FundsGainMultiplier;
                _prevPeriod.SubsidySize = MaintenanceHandler.Instance.GetSubsidyAmount(_prevPeriod.StartUT, _prevPeriod.EndUT);
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
                DateTime dtNextPeriod = DTUtils.UTToDate(periodStartUt).AddMonths(LogPeriodMonths);
                double nextPeriodStart = (dtNextPeriod - DTUtils.Epoch).TotalSeconds;
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
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

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
            TransactionReasonsRP0 reasonRP0 = reason.RP0();

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

            if (reasonRP0 == TransactionReasonsRP0.VesselPurchase)
            {
                CurrentPeriod.VesselPurchase -= changeDelta;
                return;
            }

            if ((reasonRP0 & TransactionReasonsRP0.Rollouts) != 0)
            {
                CurrentPeriod.LaunchFees -= changeDelta;
                return;
            }

            if (reasonRP0 == TransactionReasonsRP0.VesselRecovery)
            {
                CurrentPeriod.VesselRecovery += changeDelta;
                return;
            }

            if (reasonRP0 == TransactionReasonsRP0.PartOrUpgradeUnlock)
            {
                CurrentPeriod.EntryCosts -= changeDelta;
                return;
            }

            if (reasonRP0 == TransactionReasonsRP0.StructureConstruction)
            {
                CurrentPeriod.ConstructionFees -= changeDelta;
                return;
            }

            if (reasonRP0 == TransactionReasonsRP0.HiringResearchers)
            {
                CurrentPeriod.HiringResearchers -= changeDelta;
                return;
            }

            if (reasonRP0 == TransactionReasonsRP0.HiringEngineers)
            {
                CurrentPeriod.HiringEngineers -= changeDelta;
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
            if (CareerEventScope.ShouldIgnore || !IsEnabled || c.AutoAccept) return;   // Do not record the Accept event for record contracts

            _contractDict.Add(new ContractEvent(Planetarium.GetUniversalTime())
            {
                Type = ContractEventType.Accept,
                RepChange = 0,
                DisplayName = c.Title,
                InternalName = GetContractInternalName(c)
            });
        }

        private void ContractCompleted(Contract c)
        {
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

            _contractDict.Add(new ContractEvent(Planetarium.GetUniversalTime())
            {
                Type = ContractEventType.Complete,
                RepChange = c.ReputationCompletion,
                DisplayName = c.Title,
                InternalName = GetContractInternalName(c)
            });
        }

        private void ContractCancelled(Contract c)
        {
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

            _contractDict.Add(new ContractEvent(Planetarium.GetUniversalTime())
            {
                Type = ContractEventType.Cancel,
                RepChange = 0,
                DisplayName = c.Title,
                InternalName = GetContractInternalName(c)
            });
        }

        private void ContractFailed(Contract c)
        {
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

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
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

            // KJR can clobber the vessel back to prelaunch state in case of clamp wobble. Need to exclude such events.
            if (!_launched && ev.from == Vessel.Situations.PRELAUNCH && ev.host == FlightGlobals.ActiveVessel)
            {
                RP0Debug.Log($"Launching {FlightGlobals.ActiveVessel?.vesselName}");

                _launched = true;
                _launchedVessels.Add(new LaunchEvent(Planetarium.GetUniversalTime())
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
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

            if (!HighLogic.CurrentGame.CrewRoster.Tourist.Any(c => c.name == data.sender))    // Do not count tourist/test animal deaths
            {
                CurrentPeriod.NumNautsKilled++;
            }
        }

        private string GetContractInternalName(Contract c)
        {
            if (c is ContractConfigurator.ConfiguredContract cc && cc.contractType != null)
                return cc.contractType.name;

            return string.Empty;
        }

        private float GetSciPointTotalFromKCT()
        {
            try
            {
                // KCT returns -1 if the player hasn't earned any sci yet
                return Math.Max(0, KerbalConstructionTimeData.Instance.SciPointsTotal);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return 0;
            }
        }

        private void OnKctTechCompleted(ResearchProject tech)
        {
            AddTechEvent(tech);
        }

        private void OnKctFacilityUpgdQueued(FacilityUpgradeProject data)
        {
            AddFacilityConstructionEvent(data, ConstructionState.Started);
        }

        private void OnKctFacilityUpgdCancel(FacilityUpgradeProject data)
        {
            AddFacilityConstructionEvent(data, ConstructionState.Cancelled);
        }

        private void OnKctFacilityUpgdComplete(FacilityUpgradeProject data)
        {
            AddFacilityConstructionEvent(data, ConstructionState.Completed);
        }

        private void OnKctLCConstructionQueued(LCConstructionProject data, LaunchComplex lc)
        {
            AddLCConstructionEvent(data, lc, ConstructionState.Started);
        }

        private void OnKctLCConstructionCancel(LCConstructionProject data, LaunchComplex lc)
        {
            AddLCConstructionEvent(data, lc, ConstructionState.Cancelled);
        }

        private void OnKctLCConstructionComplete(LCConstructionProject data, LaunchComplex lc)
        {
            AddLCConstructionEvent(data, lc, ConstructionState.Completed);
        }

        private void OnKctLCDismantled(LaunchComplex lc)
        {
            AddLCConstructionEvent(null, lc, ConstructionState.Dismantled);
        }

        private void OnKctPadConstructionQueued(PadConstructionProject data, LCLaunchPad lp)
        {
            AddPadConstructionEvent(data, lp, ConstructionState.Started);
        }

        private void OnKctPadConstructionCancel(PadConstructionProject data, LCLaunchPad lp)
        {
            AddPadConstructionEvent(data, lp, ConstructionState.Cancelled);
        }

        private void OnKctPadConstructionComplete(PadConstructionProject data, LCLaunchPad lp)
        {
            AddPadConstructionEvent(data, lp, ConstructionState.Completed);
        }

        private void OnKctPadDismantled(LCLaunchPad lp)
        {
            AddPadConstructionEvent(null, lp, ConstructionState.Dismantled);
        }

        private void AddFacilityConstructionEvent(FacilityUpgradeProject data, ConstructionState state)
        {
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;    // facility type can be null in case of third party mods that define custom facilities

            if (!_facilityConstructions.Any(fc => fc.FacilityID == data.uid))
            {
                _facilityConstructions.Add(new FacilityConstruction
                {
                    Facility = data.FacilityType,
                    FacilityID = data.uid,
                    Cost = data.cost,
                    NewLevel = data.upgradeLevel
                });
            }

            _facilityConstructionEvents.Add(new FacilityConstructionEvent(Planetarium.GetUniversalTime())
            {
                Facility = FacilityConstructionEvent.ParseFacilityType(data.FacilityType),
                FacilityID = data.uid,
                State = state
            });
        }

        private void AddLCConstructionEvent(LCConstructionProject data, LaunchComplex lc, ConstructionState state)
        {
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

            Guid modId = data?.modId ?? lc.ModID;    // Should only happen when LCs are created through code and thus do not have Construction items
            if (!_lcs.Any(logLC => logLC.ModID == modId))
            {
                var logItem = data == null ? new LCLogItem(lc) : new LCLogItem(data);
                logItem.ModID = modId;
                _lcs.Add(logItem);
            }

            _facilityConstructionEvents.Add(new FacilityConstructionEvent(Planetarium.GetUniversalTime())
            {
                Facility = FacilityType.LaunchComplex,
                State = state,
                FacilityID = modId
            });
        }

        private void AddPadConstructionEvent(PadConstructionProject data, LCLaunchPad lp, ConstructionState state)
        {
            if (CareerEventScope.ShouldIgnore || !IsEnabled) return;

            Guid id = data?.id ?? lp.id;
            LaunchComplex lc = data?.LC ?? lp.LC;
            if (!_lpConstructions.Any(lpc => lpc.LPID == id))
            {
                _lpConstructions.Add(new LPConstruction
                {
                    LPID = id,
                    LCID = lc.ID,
                    LCModID = lc.ModID,
                    Cost = data?.cost ?? 0
                });
            }

            _facilityConstructionEvents.Add(new FacilityConstructionEvent(Planetarium.GetUniversalTime())
            {
                Facility = FacilityType.LaunchPad,
                State = state,
                FacilityID = id
            });
        }
    }
}
