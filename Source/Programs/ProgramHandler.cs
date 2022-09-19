﻿using System;
using ClickThroughFix;
using ContractConfigurator;
using Contracts;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
using System.Reflection.Emit;
using KSP.UI.Screens.DebugToolbar;

namespace RP0.Programs
{
    [KSPScenario((ScenarioCreationOptions)480, new GameScenes[] { GameScenes.EDITOR, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION })]
    public class ProgramHandler : ScenarioModule
    {
        private static readonly int _windowId = "RP0ProgramsWindow".GetHashCode();

        private Rect _windowRect = new Rect(3, 40, 425, 600);
        private Vector2 _scrollPos = new Vector2();
        private readonly List<string> _expandedPrograms = new List<string>();
        private DebugScreen _dbgScreen;

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

        public int ActiveProgramLimit => GameVariables.Instance.GetActiveStrategyLimit(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.Administration));

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
        }

        public void OnDestroy()
        {
            GameEvents.onGUIAdministrationFacilitySpawn.Remove(ShowAdminGUI);
            GameEvents.onGUIAdministrationFacilityDespawn.Remove(HideAdminGUI);
            GameEvents.Contract.onCompleted.Remove(OnContractComplete);
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
        }

        public void ProcessFunding()
        {
            double fundsOld = Funding.Instance.Funds;
            foreach (Program p in ActivePrograms)
            {
                p.ProcessFunding();
            }
            Debug.Log($"[RP-0] ProgramHandler added {(Funding.Instance.Funds - fundsOld)} funds.");
        }

        internal void OnGUI()
        {
            if (IsInAdmin && (_dbgScreen?.isShown ?? false))
            {
                _windowRect = ClickThruBlocker.GUILayoutWindow(_windowId, _windowRect, WindowFunction, "Programs", HighLogic.Skin.window);
                Tooltip.Instance.ShowTooltip(_windowId, contentAlignment: TextAnchor.MiddleLeft);
            }
        }

        private void OnContractComplete(Contract data)
        {
            StartCoroutine(ContractCompleteRoutine());
        }

        private IEnumerator ContractCompleteRoutine()
        {
            // The contract will only be seen as completed after the ContractSystem has run it's next update
            yield return null;

            for (int i = ActivePrograms.Count - 1; i >= 0; --i)
            {
                Program p = ActivePrograms[i];
                if (!p.CanComplete && p.AllObjectivesMet)
                {
                    p.MarkObjectivesComplete();
                }
            }
        }

        private void ShowAdminGUI()
        {
            IsInAdmin = true;

            if (_dbgScreen == null)
            {
                var fi = typeof(DebugScreenSpawner).GetField("screen", BindingFlags.Instance | BindingFlags.NonPublic);
                _dbgScreen = (DebugScreen)fi.GetValue(DebugScreenSpawner.Instance);
            }
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
                GUILayout.Label($"Active programs: {ActivePrograms.Count}/{ActiveProgramLimit}", HighLogic.Skin.label);
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
            bool canAccept = p.CanAccept;
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
                GUI.enabled = ActivePrograms.Count < ActiveProgramLimit;
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
                GUILayout.Label($"{p.nominalDurationYears:0.#} years", HighLogic.Skin.label);
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

        public bool ActivateProgram(string name)
        {
            Program p = Programs.Find(p2 => p2.name == name);
            if (p == null)
                return false;

            ActivateProgram(p);
            return true;
        }

        private void ActivateProgram(Program p)
        {
            ActivePrograms.Add(p.Accept());
            ContractPreLoader.Instance.ResetGenerationFailure();
        }

        public bool CompleteProgram(string name)
        {
            Program p = ActivePrograms.Find(p2 => p2.name == name);
            if (p == null)
                return false;

            CompleteProgram(p);
            return true;
        }

        private void CompleteProgram(Program p)
        {
            ActivePrograms.Remove(p);
            CompletedPrograms.Add(p);
            p.Complete();
            ContractPreLoader.Instance.ResetGenerationFailure();
        }
    }
}
