using UnityEngine;

namespace RP0.ConfigurableStart
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class PresetPickerGUI : MonoBehaviour
    {
        //TODO: find a better starting position
        private static Rect _selectionWindowRect = new Rect(267, 104, 400, 200);
        private static bool _shouldResetUIHeight = false;
        private static bool _showUI = false;

        private static string[] _loadedScenarioNames;
        private static int _selectedScenarioIndex = 0;

        public static PresetPickerGUI Instance { get; private set; }
        public bool Initialized { get; private set; }

        public void Awake()
        {
            if (Instance != null)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public static void SetVisible(bool visible) => _showUI = visible;

        public static void ToggleVisible() => _showUI = !_showUI;

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

                _selectionWindowRect = GUILayout.Window(GetInstanceID(), _selectionWindowRect, SelectionWindow, "Scenario Selector", HighLogic.Skin.window);
            }
        }

        private static void SelectionWindow(int windowID)
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
                        ScenarioHandler.SetCurrentScenarioFromName(_loadedScenarioNames[_selectedScenarioIndex]);
                    }
                }
                GUILayout.BeginHorizontal();
                GUILayout.Label(ScenarioHandler.CurrentScenario?.Description ?? "");
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
