using System;
using System.Collections.Generic;
using UnityEngine;

namespace RP0.ConfigurableStart
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class PresetPickerGUI : MonoBehaviour
    {
        private Rect _selectionWindowRect = new Rect(267, 104, 400, 200);
        private Vector2 _kscScrollPos = Vector2.zero;
        private bool _shouldResetUIHeight = false;
        private bool _showUI = false;

        private string[] _loadedScenarioNames;
        private int _selectedScenarioIndex = 0;

        private string[] _availableKscIds;
        private string[] _availableKscDisplayNames;
        private int _selectedKscIndex = 0;

        public static PresetPickerGUI Instance { get; private set; }
        public bool Initialized { get; private set; }

        internal void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        internal void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetVisible(bool visible) => _showUI = visible;

        public void ToggleVisible() => _showUI = !_showUI;

        public void Setup(string[] names)
        {
            _loadedScenarioNames = names;

            if (KSCSwitcherInterop.IsKSCSwitcherInstalled)
            {
                InitKSCs();
            }

            Initialized = true;
        }

        public void OnGUI()
        {
            if (Initialized && _showUI)
            {
                if (_shouldResetUIHeight && Event.current.type == EventType.Layout)
                {
                    _selectionWindowRect.height = 300;
                    _shouldResetUIHeight = false;
                }

                _selectionWindowRect = GUILayout.Window(GetInstanceID(), _selectionWindowRect, RenderSelectionWindow, "Scenario Selector", HighLogic.Skin.window);
            }
        }

        private void RenderSelectionWindow(int windowID)
        {
            GUILayout.BeginVertical(HighLogic.Skin.box);
            {
                GUILayout.Label("Choose Scenario preset:", HighLogic.Skin.label);
                {
                    int oldConfigIdx = _selectedScenarioIndex;
                    _selectedScenarioIndex = GUILayout.SelectionGrid(_selectedScenarioIndex, _loadedScenarioNames, 1, HighLogic.Skin.button);
                    if (oldConfigIdx != _selectedScenarioIndex)
                    {
                        _shouldResetUIHeight = true;
                        RP0Debug.Log("Selected Scenario changed, updating values");
                        ScenarioHandler.Instance.SetCurrentScenarioFromName(_loadedScenarioNames[_selectedScenarioIndex]);
                    }
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(ScenarioHandler.Instance.CurrentScenario?.Description ?? "");
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            if (_availableKscIds != null)
            {
                GUILayout.Space(7);

                GUILayout.BeginVertical(HighLogic.Skin.box);
                {
                    GUILayout.Label("Choose starting KSC:", HighLogic.Skin.label);
                    _kscScrollPos = GUILayout.BeginScrollView(_kscScrollPos, GUILayout.Height(300));
                    int oldKscIdx = _selectedKscIndex;
                    _selectedKscIndex = GUILayout.SelectionGrid(_selectedKscIndex, _availableKscDisplayNames, 1, HighLogic.Skin.button);
                    GUILayout.EndScrollView();
                    if (oldKscIdx != _selectedKscIndex)
                    {
                        ScenarioHandler.Instance.SelectedKSC = _availableKscIds[_selectedKscIndex];
                    }
                }
                GUILayout.EndVertical();
            }

            GUILayout.Space(7);

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close", HighLogic.Skin.button))
            {
                _showUI = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void InitKSCs()
        {
            List<(string id, string displayName)> sites = KSCSwitcherInterop.GetAvailableSites();
            if (sites != null && sites.Count > 0)
            {
                _availableKscIds = new string[sites.Count];
                _availableKscDisplayNames = new string[sites.Count];
                for (int i = 0; i < sites.Count; i++)
                {
                    _availableKscIds[i] = sites[i].id;
                    _availableKscDisplayNames[i] = sites[i].displayName;
                }

                string currentKsc = KSCSwitcherInterop.GetActiveRSSKSC() ?? KSCSwitcherInterop.DefaultKscId;
                int defaultIdx = Array.IndexOf(_availableKscIds, currentKsc);
                _selectedKscIndex = defaultIdx >= 0 ? defaultIdx : 0;
                ScenarioHandler.Instance.SelectedKSC = _availableKscIds[_selectedKscIndex];
            }
        }
    }
}
