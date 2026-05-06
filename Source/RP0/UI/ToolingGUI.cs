using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Smooth.Slinq;
using RP0.Tooling;
using System;

namespace RP0
{
    public class ToolingGUI : UIBase
    {
        private struct UntooledPart
        {
            public string Name;
            public float ToolingCost;
            public float UntooledMultiplier;
            public float TotalCost;
            public Part Part;
        };

        private const float UpdateInterval = 0.5f;

        private string _currentToolingType;
        private string _currentToolingTitle;
        private bool _isToolingTempDisabled = false;
        private float _nextUpdate = 0f;
        private float _allTooledCost;
        private readonly List<UntooledPart> _untooledParts = new List<UntooledPart>();
        private Vector2 _toolingTypesScroll = new Vector2();
        private Vector2 _untooledTypesScroll = new Vector2();
        private Part _highlightedPart = null;
        private Part _editorHoveredPart = null;
        private Texture2D _highlightRowStyle = null;
        private readonly Dictionary<Part, Rect> _partRowRects = new Dictionary<Part, Rect>();

        private Texture2D HighlightRowTex
        {
            get
            {
                if (_highlightRowStyle == null)
                {
                    _highlightRowStyle = new Texture2D(1, 1);
                    _highlightRowStyle.SetPixel(0, 0, new Color(1f, 1f, 0f, 0.15f));
                    _highlightRowStyle.Apply();
                }
                return _highlightRowStyle;
            }
        }

        protected override void OnDestroy()
        {
            if (_highlightRowStyle != null)
                UnityEngine.Object.Destroy(_highlightRowStyle);
        }

        public UITab RenderToolingTab()
        {
            MaybeUpdate();

            Part newEditorHoveredPart = Mouse.HoveredPart;
            if (newEditorHoveredPart != null && EventSystem.current.IsPointerOverGameObject())
                newEditorHoveredPart = null;
            if (newEditorHoveredPart != _editorHoveredPart)
            {
                _editorHoveredPart = newEditorHoveredPart;
                if (_editorHoveredPart != null && _partRowRects.TryGetValue(_editorHoveredPart, out Rect r))
                {
                    float rowMid = r.y + r.height / 2f;
                    _untooledTypesScroll.y = Mathf.Max(0f, rowMid - 102f);
                }
            }

            if (!_isToolingTempDisabled && !ToolingManager.Instance.toolingEnabled)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Part tooling is disabled", HighLogic.Skin.label);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                return UITab.Tooling;
            }

            _currentToolingType = _currentToolingTitle = null;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Tooling Types", HighLogic.Skin.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            int counter = 0;
            GUILayout.BeginHorizontal();
            foreach (string type in ToolingDatabase.toolings.Keys)
            {
                if (counter % 3 == 0 && counter != 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                counter++;
                string title = ToolingManager.Instance.GetTitleForTooling(type);
                if (GUILayout.Button(title, HighLogic.Skin.button))
                {
                    _currentToolingType = type;
                    _currentToolingTitle = title;
                }
            }
            GUILayout.EndHorizontal();

            Part pawPart = null;
            List<UIPartActionWindow> pawWindows = UIPartActionController.Instance?.windows;
            if (pawWindows != null && pawWindows.Count == 1)
                pawPart = pawWindows[0].part;
            Part rowHighlightPart = _editorHoveredPart ?? pawPart;

            Part newHighlightedPart = null;
            if (_isToolingTempDisabled || _untooledParts.Count > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Untooled Parts:", HighLogic.Skin.label, GUILayout.Width(312));
                GUILayout.Label("Tooling cost", RightLabel, GUILayout.Width(72));
                GUILayout.Label("Untooled", RightLabel, GUILayout.Width(72));
                GUILayout.Label("Tooled", RightLabel, GUILayout.Width(72));
                GUILayout.EndHorizontal();

                _untooledTypesScroll = GUILayout.BeginScrollView(_untooledTypesScroll, GUILayout.Height(204), GUILayout.Width(572));
                foreach (UntooledPart up in _untooledParts)
                {
                    if (Event.current.type == EventType.Repaint &&
                        up.Part != null && up.Part == rowHighlightPart &&
                        _partRowRects.TryGetValue(up.Part, out Rect highlightRect))
                    {
                        GUI.DrawTexture(highlightRect, HighlightRowTex);
                    }
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(up.Name, BoldLabel, GUILayout.Width(312));
                    GUILayout.Label($"√{-CurrencyUtils.Funds(TransactionReasonsRP0.ToolingPurchase, -up.ToolingCost):N0}", RightLabel, GUILayout.Width(72));
                    float untooledExtraCost = GetUntooledExtraCost(up);
                    GUILayout.Label($"√{-CurrencyUtils.Funds(TransactionReasonsRP0.VesselPurchase, -up.TotalCost):N0}", RightLabel, GUILayout.Width(72));
                    GUILayout.Label($"√{-CurrencyUtils.Funds(TransactionReasonsRP0.ToolingPurchase, -(up.TotalCost - untooledExtraCost)):N0}", RightLabel, GUILayout.Width(72));
                    GUILayout.EndHorizontal();
                    if (Event.current.type == EventType.Repaint && up.Part != null)
                    {
                        Rect rowRect = GUILayoutUtility.GetLastRect();
                        _partRowRects[up.Part] = rowRect;
                        if (rowRect.Contains(Event.current.mousePosition))
                            newHighlightedPart = up.Part;
                    }
                }
                GUILayout.EndScrollView();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Total vessel cost if all parts are tooled: {_allTooledCost:N0}");
                GUILayout.EndHorizontal();

                GUILayout.BeginVertical();
                try
                {
                    RenderToolingPreviewButton();
                    RenderToolAllButton();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                GUILayout.EndVertical();
            }

            if (Event.current.type == EventType.Repaint && newHighlightedPart != _highlightedPart)
            {
                if (_highlightedPart != null)
                    _highlightedPart.SetHighlightDefault();
                _highlightedPart = newHighlightedPart;
                if (_highlightedPart != null)
                {
                    _highlightedPart.SetHighlightColor(Color.yellow);
                    _highlightedPart.SetHighlightType(Part.HighlightType.AlwaysOn);
                }
            }

            return _currentToolingType == null ? UITab.Tooling : UITab.ToolingType;
        }

        public void RenderTypeTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Toolings for type {_currentToolingTitle}", HighLogic.Skin.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            Parameter[] parameters = Parameters.GetParametersForToolingType(_currentToolingType);
            DisplayTypeHeadings(parameters);
            _toolingTypesScroll = GUILayout.BeginScrollView(_toolingTypesScroll, GUILayout.Width(360), GUILayout.Height(300));
            try
            {
                var entries = ToolingDatabase.toolings[_currentToolingType];
                var values = new float[parameters.Length];
                DisplayRows(entries, 0, values, parameters);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndScrollView();
        }

        private static void RenderToolAllButton()
        {
            if (GUILayout.Button("Tool All", HighLogic.Skin.button))
            {
                GetUntooledPartsAndCost(out List<ModuleTooling> untooledParts, out float toolingCost);
                var cmq = UnlockCreditHandler.Instance.GetPrePostCostAndAffordability(toolingCost, TransactionReasonsRP0.ToolingPurchase, out double preCost, out double postCost, out double credit, out bool canAfford);
                string buttonText = canAfford ? "Purchase All Toolings" : "Can't Afford";
                string costline = cmq.GetCostLineOverride(true, false, true, true);
                if (string.IsNullOrEmpty(costline))
                    costline = "nothing";

                var dialog = new MultiOptionDialog(
                        "ConfirmAllToolingsPurchase",
                        $"Tooling for all untooled parts will cost {costline} (spending {credit:N0} unlock credit).",
                        "Tooling Purchase",
                        HighLogic.UISkin,
                        new Rect(0.5f, 0.5f, 150f, 60f),
                        new DialogGUIFlexibleSpace(),
                        new DialogGUIVerticalLayout(
                            new DialogGUIFlexibleSpace(),
                            new DialogGUIButton(buttonText, () => { if (canAfford) ToolAll(); }, 140f, 30f, true),
                            new DialogGUIButton("Close", () => { }, 140f, 30f, true)));

                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                             new Vector2(0.5f, 0.5f),
                                             dialog,
                                             false,
                                             HighLogic.UISkin).HideGUIsWhilePopup();
            }
        }

        private static void ToolAll()
        {
            GetUntooledPartsAndCost(out List<ModuleTooling> untooledParts, out float toolingCost);

            UnlockCreditHandler.Instance.GetPrePostCostAndAffordability(toolingCost, TransactionReasonsRP0.ToolingPurchase, out _, out _, out _, out bool canAfford);
            if (canAfford)
            {
                ModuleTooling.PurchaseToolingBatch(untooledParts);
                untooledParts.ForEach(mt =>
                {
                    mt.Events["ToolingEvent"].guiActiveEditor = false;
                });
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        public static void GetUntooledPartsAndCost(out List<ModuleTooling> parts, out float toolingCost)
        {
            parts = EditorLogic.fetch.ship.Parts.Slinq().SelectMany(p => p.FindModulesImplementing<ModuleTooling>().Slinq())
                                                        .Where(mt => !mt.IsUnlocked())
                                                        .ToList();
            toolingCost = ModuleTooling.PurchaseToolingBatch(parts, isSimulation: true);
        }

        private void RenderToolingPreviewButton()
        {
            var c = new GUIContent("Press to preview fully tooled integration time & cost", "Hold the button pressed and watch the cost & time values change in the Integration Info window");
            if (GUILayout.RepeatButton(c, HighLogic.Skin.button))
            {
                if (!_isToolingTempDisabled)
                {
                    _isToolingTempDisabled = true;
                    ToolingManager.Instance.toolingEnabled = false;
                    Update();
                    GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                }
            }
            else if (_isToolingTempDisabled && Event.current.type == EventType.Repaint)   // button events are handled on the Repaint pass
            {
                ToolingManager.Instance.toolingEnabled = HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
                _isToolingTempDisabled = false;
                Update();
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        private void MaybeUpdate()
        {
            if (HighLogic.LoadedSceneIsEditor && Time.time > _nextUpdate)
            {
                _nextUpdate = Time.time + UpdateInterval;
                Update();
            }
        }

        private static HashSet<Part> _parts = new HashSet<Part>();

        private void Update()
        {
            _untooledParts.Clear();
            float totalUntooledExtraCost = 0;

            if (EditorLogic.fetch?.ship?.Parts.Count > 0)
            {
                for (int i = EditorLogic.fetch.ship.Parts.Count; i-- > 0;)
                {
                    Part p = EditorLogic.fetch.ship.Parts[i];
                    for (int j = p.Modules.Count; j-- > 0;)
                    {
                        if (p.Modules[j] is ModuleTooling mT && !mT.IsUnlocked())
                        {
                            UntooledPart up;
                            up.Name = $"{p.partInfo.title} ({mT.ToolingTypeTitle}) {mT.GetToolingParameterInfo()}";
                            up.ToolingCost = mT.GetToolingCost();
                            up.UntooledMultiplier = mT.untooledMultiplier;
                            up.Part = p;
                            if (_parts.Contains(p))
                            {
                                up.TotalCost = 0f;
                            }
                            else
                            {
                                _parts.Add(p);
                                up.TotalCost = p.GetModuleCosts(p.partInfo.cost) + p.partInfo.cost;
                            }
                            _untooledParts.Add(up);
                            totalUntooledExtraCost += GetUntooledExtraCost(up);
                        }
                    }
                }
            }

            _parts.Clear();
            _allTooledCost = EditorLogic.fetch.ship.GetShipCosts(out _, out _) - totalUntooledExtraCost;

            if (_highlightedPart != null && !_untooledParts.Exists(up => up.Part == _highlightedPart))
            {
                _highlightedPart.SetHighlightDefault();
                _highlightedPart = null;
            }
        }

        private static float GetUntooledExtraCost(UntooledPart uP)
        {
            return uP.ToolingCost * uP.UntooledMultiplier;
        }

        private void DisplayTypeHeadings(Parameter[] parameters)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(parameters[0].Title, HighLogic.Skin.label, GUILayout.Width(80));
            for (int i = 1; i < parameters.Length; ++i)
            {
                GUILayout.Label("×", HighLogic.Skin.label);
                GUILayout.Label(parameters[i].Title, HighLogic.Skin.label, GUILayout.Width(80));
            }
            GUILayout.EndHorizontal();
        }

        private string GetToolingMargin(float value, string unit)
        {
            if (value > 1e-6f)
            {
                return $"Margin: {ToolingDatabase.toolingMargin * 100:F3}%\nLow Tolerance: {ToolingDatabase.GetLowComparison(value):F3} {unit}\nHigh Tolerance: {ToolingDatabase.GetHighComparison(value):F3} {unit}";
            }
            else
            {
                return "";
            }
        }

        private void DisplayRow(float[] values, Parameter[] parameters)
        {
            string toolingMargin = GetToolingMargin(values[0], parameters[0].Unit);
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent($"{values[0]:F3} {parameters[0].Unit}", toolingMargin), HighLogic.Skin.label, GUILayout.Width(80));
            for (int i = 1; i < values.Length; ++i)
            {
                GUILayout.Label("×", HighLogic.Skin.label);
                toolingMargin = GetToolingMargin(values[i], parameters[i].Unit);
                GUILayout.Label(new GUIContent($"{values[i]:F3} {parameters[i].Unit}", toolingMargin), HighLogic.Skin.label, GUILayout.Width(80));
            }
            GUILayout.EndHorizontal();
        }

        private void DisplayRows(List<ToolingEntry> entries, int parameterIndex, float[] values, Parameter[] parameters)
        {
            if (entries == null) return;
            if (parameterIndex == parameters.Length)
            {
                DisplayRow(values, parameters);
                return;
            }

            foreach (var toolingEntry in entries)
            {
                values[parameterIndex] = toolingEntry.Value;
                DisplayRows(toolingEntry.Children, parameterIndex + 1, values, parameters);
            }
        }
    }
}
