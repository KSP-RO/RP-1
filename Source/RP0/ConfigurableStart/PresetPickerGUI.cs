using UnityEngine;

namespace RP0.ConfigurableStart
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class PresetPickerGUI : MonoBehaviour
    {
        private Rect _selectionWindowRect = new Rect(267, 104, 400, 200);
        private bool _shouldResetUIHeight = false;
        private bool _showUI = false;

        private string[] _loadedScenarioNames;
        private int _selectedScenarioIndex = 0;

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
    }
}
