﻿using System;
using ClickThroughFix;
using ContractConfigurator;
using Contracts;
using System.Collections;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using UnityEngine.Profiling;
using System.Reflection;
using System.Reflection.Emit;
using KSP.UI.Screens.DebugToolbar;
using KSP.Localization;

namespace RP0.Programs
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class ProgramHandler : ScenarioModule
    {
        private const int VERSION = 2;
        [KSPField(isPersistant = true)]
        public int LoadedSaveVersion = 0;

        private bool _ready = false;
        public bool Ready => _ready;

        // Back-compat - we have to handle this weirdly
        // because this relies on leaders being live
        private bool _upgrade_v02 = false;

        private static readonly int _windowId = "RP0ProgramsWindow".GetHashCode();

        private Rect _windowRect = new Rect(3, 40, 425, 600);
        private Vector2 _scrollPos = new Vector2();
        private readonly List<string> _expandedPrograms = new List<string>();

        public static ProgramHandler Instance { get; private set; }
        public static ProgramHandlerSettings Settings { get; private set; }
        public static List<Program> Programs { get; private set; }
        public static Dictionary<string, Program> ProgramDict { get; private set; }

        private static Dictionary<string, System.Type> programStrategies = new Dictionary<string, System.Type>();
        private static AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
                    new AssemblyName("RP0ProgramsDynamic"), AssemblyBuilderAccess.Run);
        private static ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("RP0ProgramsDynamicModule");

        public bool IsInAdmin { get; private set; }

        public List<Program> ActivePrograms { get; private set; } = new List<Program>();

        public List<Program> CompletedPrograms { get; private set; } = new List<Program>();

        public HashSet<string> DisabledPrograms { get; private set; } = new HashSet<string>();

        public int MaxProgramSlots => GameVariables.Instance.GetActiveStrategyLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Administration));

        public int ActiveProgramSlots
        {
            get
            {
                int pts = 0;
                foreach (var p in ActivePrograms)
                    pts += p.slots;

                return pts;
            }
        }

        public static void EnsurePrograms()
        {
            if (Programs == null)
            {
                Programs = new List<Program>();
                ProgramDict = new Dictionary<string, Program>();

                foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("RP0_PROGRAM"))
                {
                    Program p = new Program(n);
                    Programs.Add(p);
                    ProgramDict.Add(p.name, p);

                    if (programStrategies.ContainsKey(p.name))
                        continue;

                    TypeBuilder stratBuilder = moduleBuilder.DefineType("RP0.Programs." + p.name,
                        TypeAttributes.Public | TypeAttributes.Class, typeof(ProgramStrategy));
                    Type t = stratBuilder.CreateType();
                    programStrategies[p.name] = t;
                    if (Strategies.StrategySystem.StrategyTypes.Count > 0)
                        Strategies.StrategySystem.StrategyTypes.Add(t);
                }
            }
        }

        public static string PrettyPrintProgramName(string name)
        {
            return ProgramDict.TryGetValue(name, out Program p) ? p.title : name;
        }

        public override void OnAwake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            Instance = this;

            GameEvents.onGUIAdministrationFacilitySpawn.Add(ShowAdminGUI);
            GameEvents.onGUIAdministrationFacilityDespawn.Add(HideAdminGUI);
            GameEvents.Contract.onCompleted.Add(OnContractComplete);
            GameEvents.Contract.onAccepted.Add(OnContractAccept);
        }

        public void OnDestroy()
        {
            GameEvents.onGUIAdministrationFacilitySpawn.Remove(ShowAdminGUI);
            GameEvents.onGUIAdministrationFacilityDespawn.Remove(HideAdminGUI);
            GameEvents.Contract.onCompleted.Remove(OnContractComplete);
            GameEvents.Contract.onAccepted.Remove(OnContractAccept);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (Settings == null)
            {
                Settings = new ProgramHandlerSettings();
                foreach (ConfigNode cn in GameDatabase.Instance.GetConfigNodes("PROGRAMHANDLERSETTINGS"))
                    ConfigNode.LoadObjectFromConfig(Settings, cn);
            }

            EnsurePrograms();

            ConfigNode disableds = node.GetNode("DISABLEDPROGRAMS");
            if (disableds != null)
            {
                foreach (ConfigNode.Value v in disableds.values)
                {
                    DisabledPrograms.Add(v.name);
                }
            }

            foreach (ConfigNode cn in node.GetNodes("ACTIVEPROGRAM"))
            {
                string progName = cn.GetValue(nameof(Program.name));
                Program programTemplate = Programs.FirstOrDefault(p => p.name == progName);
                var program = new Program(programTemplate);
                program.Load(cn);
                ActivePrograms.Add(program);
            }

            foreach (ConfigNode cn in node.GetNodes("COMPLETEDPROGRAM"))
            {
                string progName = cn.GetValue(nameof(Program.name));
                Program programTemplate = Programs.FirstOrDefault(p => p.name == progName);
                var program = new Program(programTemplate);
                program.Load(cn);
                CompletedPrograms.Add(program);
            }

            _ready = true; // done BEFORE upgrading because we have to do hijinks there

            if (LoadedSaveVersion < VERSION)
            {
                if (LoadedSaveVersion < 1)
                {
                    List<Program> progs = new List<Program>();
                    progs.AddRange(ActivePrograms);
                    progs.AddRange(CompletedPrograms);
                    foreach (var p in progs)
                    {
                        if (p.name == "CrewedOrbit")
                        {
                            DisabledPrograms.Add("CrewedOrbitEarly");
                            DisabledPrograms.Add("CrewedOrbitAdv");

                            break;
                        }
                    }
                }
                if (LoadedSaveVersion < 2)
                {
                    _ready = false;
                    _upgrade_v02 = true;
                    // handled in OnLoadStrategiesComplete because we need to know what leaders are active
                }
            }

            LoadedSaveVersion = VERSION;
        }

        public void OnLoadStrategiesComplete()
        {
            if (_upgrade_v02)
            {
                foreach (var p in ActivePrograms)
                {
                    if (p.fracElapsed < 0d)
                    {
                        double durSec = p.DurationYears * (365.25d * 86400d);
                        p.fracElapsed = (p.lastPaymentUT - p.acceptedUT) / durSec;
                        p.deadlineUT = p.acceptedUT + durSec;
                    }
                }
                _ready = true;
                _upgrade_v02 = false;
            }
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            foreach (Program program in ActivePrograms)
            {
                var cn = new ConfigNode("ACTIVEPROGRAM");
                program.Save(cn);
                node.AddNode(cn);
            }

            foreach (Program program in CompletedPrograms)
            {
                var cn = new ConfigNode("COMPLETEDPROGRAM");
                program.Save(cn);
                node.AddNode(cn);
            }

            var disableds = new ConfigNode("DISABLEDPROGRAMS");
            foreach (var p in DisabledPrograms)
                disableds.AddValue(p, true);
            node.AddNode(disableds);
        }

        public void OnLeaderChange()
        {
            foreach (Program p in ActivePrograms)
            {
                p.OnLeaderChange();
            }
            RP0Debug.Log($"ProgramHandler clamped active program funding on leader change.");
        }

        public void ProcessFunding()
        {
            Profiler.BeginSample("RP0ProcessFunding");
            double fundsOld = Funding.Instance.Funds;
            foreach (Program p in ActivePrograms)
            {
                p.ProcessFunding();
            }
            RP0Debug.Log($"ProgramHandler added {Funding.Instance.Funds - fundsOld} funds.");
            Profiler.EndSample();
        }

        public double GetProgramFunding(double utOffset)
        {
            double programBudget = 0d;
            foreach (Program p in ActivePrograms)
            {
                programBudget += p.GetFundsForFutureTimestamp(Planetarium.GetUniversalTime() + utOffset) - p.GetFundsForFutureTimestamp(Planetarium.GetUniversalTime());
            }
            return programBudget;
        }

        public double GetDisplayProgramFunding(double utOffset)
        {
            return CurrencyUtils.Funds(TransactionReasonsRP0.ProgramFunding, GetProgramFunding(utOffset));
        }

        internal void OnGUI()
        {
            if (IsInAdmin && (DebugScreenSpawner.Instance?.screen?.isShown ?? false))
            {
                _windowRect = ClickThruBlocker.GUILayoutWindow(_windowId, _windowRect, WindowFunction, "Programs", HighLogic.Skin.window);
                Tooltip.Instance.ShowTooltip(_windowId, contentAlignment: TextAnchor.MiddleLeft);
            }
        }

        public float RepToConfidenceForContract(ConfiguredContract cc, bool isAwarding)
        {
            foreach (Program p in ActivePrograms)
            {
                if (p.optionalContracts.Contains(cc.contractType.name))
                {
                    return p.RepToConfidence;
                }
            }
            if (isAwarding || cc.ContractState != Contract.State.Completed)
                return 0f;

            // Since it's completed (and this is not part of the awarding step), check completed programs too.
            foreach (Program p in CompletedPrograms)
            {
                if (p.optionalContracts.Contains(cc.contractType.name))
                {
                    return p.RepToConfidence;
                }
            }

            return 0f;
        }

        private void OnContractAccept(Contract data)
        {
            if (KerbalConstructionTimeData.Instance.StartedProgram)
                KerbalConstructionTimeData.Instance.AcceptedContract = true;
        }

        private void OnContractComplete(Contract data)
        {
            StartCoroutine(ContractCompleteRoutine(data));
        }

        private IEnumerator ContractCompleteRoutine(Contract data)
        {
            // The contract will only be seen as completed after the ContractSystem has run its next update
            // This will happen within 1 or 2 frames of the contract completion event getting fired.
            yield return null;
            yield return null;

            for (int i = ActivePrograms.Count - 1; i >= 0; --i)
            {
                Program p = ActivePrograms[i];
                if (!p.CanComplete && p.AllObjectivesMet)
                {
                    p.MarkObjectivesComplete();
                }
            }

            if (data is ConfiguredContract cc)
            {
                // Handle KCT applicants
                int applicants = Database.SettingsSC.ContractApplicants.GetApplicantsFromContract(cc.contractType.name);
                if (applicants > 0)
                    KerbalConstructionTimeData.Instance.Applicants += applicants;

                // Handle Confidence
                float repToConf = RepToConfidenceForContract(cc, true);
                if (repToConf > 0f)
                {
                    float rep = 0;
                    foreach (var param in cc.AllParameters)
                        if (param.Optional && param.ReputationCompletion > 0 && param.State == ParameterState.Complete)
                            rep += param.ReputationCompletion;
                    Confidence.Instance.AddConfidence(repToConf * (rep + data.ReputationCompletion), TransactionReasons.ContractReward);
                }
            }
        }

        private void ShowAdminGUI()
        {
            IsInAdmin = true;
        }

        private void HideAdminGUI()
        {
            IsInAdmin = false;
        }

        private void WindowFunction(int windowID)
        {
            using (var scrollScope = new GUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollScope.scrollPosition;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Program Points: {ActiveProgramSlots}/{MaxProgramSlots}", HighLogic.Skin.label);
                GUILayout.EndHorizontal();

                foreach (Program p in ActivePrograms)
                {
                    DrawProgramSection(p);
                }

                foreach (Program p in Programs.Where(p => !ActivePrograms.Any(p2 => p.name == p2.name) &&
                                                          !CompletedPrograms.Any(p2 => p.name == p2.name)))
                {
                    DrawProgramSection(p);
                }

                foreach (Program p in CompletedPrograms)
                {
                    DrawProgramSection(p);
                }
            }

            GUI.DragWindow();

            Tooltip.Instance.RecordTooltip(_windowId);
        }

        private void DrawProgramSection(Program p)
        {
            bool isCompleted = p.IsComplete;
            bool isActive = p.IsActive;
            bool canAccept = p.CanAccept && p.MeetsConfidenceThreshold;
            bool canComplete = p.CanComplete;

            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.BeginHorizontal();

            bool oldIsExpanded = _expandedPrograms.Contains(p.name);
            bool newIsExpanded = GUILayout.Toggle(oldIsExpanded, "ⓘ", HighLogic.Skin.button, GUILayout.ExpandWidth(false), GUILayout.Height(20));
            if (oldIsExpanded && !newIsExpanded)
                _expandedPrograms.Remove(p.name);
            if (newIsExpanded && !oldIsExpanded)
                _expandedPrograms.Add(p.name);

            GUILayout.Label(p.title, HighLogic.Skin.label, GUILayout.ExpandWidth(true));

            if (isCompleted)
            {
                GUILayout.Label("Completed", HighLogic.Skin.label);
            }
            else if (!isActive)
            {
                GUI.enabled = ActiveProgramSlots < MaxProgramSlots;
                if (GUILayout.Button(canAccept ? "Accept" : "CHTAccept", HighLogic.Skin.button))
                {
                    ActivateProgram(p);
                    if (KSP.UI.Screens.Administration.Instance != null)
                        KSP.UI.Screens.Administration.Instance.RedrawPanels();
                }
                GUI.enabled = true;
            }
            else
            {
                if (GUILayout.Button(canComplete ? "Complete" : "CHTComplete", HighLogic.Skin.button))
                {
                    CompleteProgram(p);
                    if (KSP.UI.Screens.Administration.Instance != null)
                        KSP.UI.Screens.Administration.Instance.RedrawPanels();
                }
            }

            GUILayout.EndHorizontal();

            if (newIsExpanded)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(p.description, HighLogic.Skin.label);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Objectives: ", HighLogic.Skin.label);
                GUILayout.Label(p.objectivesPrettyText, HighLogic.Skin.label);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Total funds: ", HighLogic.Skin.label);
                GUILayout.Label($"{p.TotalFunding:N0}", HighLogic.Skin.label);
                GUILayout.EndHorizontal();

                if (isActive || isCompleted)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Funds paid out: ", HighLogic.Skin.label);
                    GUILayout.Label($"{p.fundsPaidOut:N0}", HighLogic.Skin.label);
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label("Nominal duration: ", HighLogic.Skin.label);
                GUILayout.Label($"{p.DurationYears:0.#} years", HighLogic.Skin.label);
                GUILayout.EndHorizontal();

                if (isActive || isCompleted)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Accepted: ", HighLogic.Skin.label);
                    GUILayout.Label(KSPUtil.dateTimeFormatter.PrintDateCompact(p.acceptedUT, false, false), HighLogic.Skin.label);
                    GUILayout.EndHorizontal();
                }

                if (isCompleted)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Completed: ", HighLogic.Skin.label);
                    GUILayout.Label(KSPUtil.dateTimeFormatter.PrintDateCompact(p.completedUT, false, false), HighLogic.Skin.label);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndVertical();
        }

        public void ActivateProgram(Program p)
        {
            if (p == null)
            {
                RP0Debug.LogError($"Error: Tried to accept null program!");
                return;
            }

            Program activeP = p.Accept();
            ActivePrograms.Add(activeP);
            foreach (string s in p.programsToDisableOnAccept)
                DisableProgram(s);

            ContractPreLoader.Instance.ResetGenerationFailure();

            ProgramStrategy ps = activeP.GetStrategy();
            if (ps == null)
                RP0Debug.LogError($"ProgramHandler Error! Couldn't find Strategy to match program {activeP.name}");
            else
                ps.SetProgram(activeP);


            KerbalConstructionTimeData.Instance.StartedProgram = true;
        }

        public void CompleteProgram(Program p)
        {
            List<StrategyConfigRP0> unlockedLeadersBef = GetAllUnlockedLeaders();

            ActivePrograms.Remove(p);
            CompletedPrograms.Add(p);
            p.Complete();
            // No change needed to ProgramStrategy because reference holds.
            ContractPreLoader.Instance?.ResetGenerationFailure();

            List<StrategyConfigRP0> unlockedLeadersAft = GetAllUnlockedLeaders();
            IEnumerable<StrategyConfigRP0> newLeaders = unlockedLeadersAft.Except(unlockedLeadersBef);

            ShowNotificationForNewLeaders(newLeaders);
        }

        private void DisableProgram(string s)
        {
            if (DisabledPrograms.Add(s))
                RP0Debug.Log($"Disabling program {s}");
            else
                RP0Debug.Log($"tried to disable program {s} but it already was!");
        }

        private static List<StrategyConfigRP0> GetAllUnlockedLeaders()
        {
            return Strategies.StrategySystem.Instance.SystemConfig.Strategies
                .OfType<StrategyConfigRP0>()
                .Where(s => s.DepartmentName != "Programs" && s.IsUnlocked())
                .ToList();
        }

        private static void ShowNotificationForNewLeaders(IEnumerable<StrategyConfigRP0> newLeaders)
        {
            string leaderString = string.Join("\n", newLeaders.Select(s => s.Title));
            if (!string.IsNullOrEmpty(leaderString))
            {
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             "LeaderUnlocked",
                                             Localizer.Format("#rp0_Leaders_LeadersUnlockedTitle"),
                                             Localizer.Format("#rp0_Leaders_LeadersUnlocked") + leaderString,
                                             Localizer.GetStringByTag("#autoLOC_190905"),
                                             true,
                                             HighLogic.UISkin).HideGUIsWhilePopup();
            }
        }
    }
}
