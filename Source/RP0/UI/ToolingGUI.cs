using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Smooth.Slinq;
using RP0.Tooling;
using System;
using UniLinq;

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
        private List<string> _currentUnderlyingTypes;
        // Last part we auto-switched the bucket on. Reset to null when no PAW is open or the user
        // manually clicked a bucket button, so subsequent PAWs trigger the auto-switch again.
        private Part _lastAutoSwitchPart;

        // Per-frame cache of the Refit-button context, recomputed at the top of RenderTypeTab so
        // DisplayRow doesn't redo PawTarget / GetModule<ModuleFuelTanks> / GetGroupingKey on every
        // row x OnGUI pass. _rowRefitDisabledTip is non-null when the button is disabled (no PAW or
        // wrong bucket) -- DisplayRow shows it instead of computing a per-row "Refit X to ..." tip.
        private Part _rowPawTarget;
        private bool _rowRefitEnabled;
        private string _rowRefitDisabledTip;

        // Cached merged-entries list for the current bucket. ToolingDatabase.Generation bumps on
        // any tooling-DB mutation, so we recompute only when the DB or the bucket changes.
        private string _mergedCacheKey;
        private int _mergedCacheGeneration = -1;
        private List<ToolingEntry> _mergedCache;
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
            _currentUnderlyingTypes = null;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Tooling Types", HighLogic.Skin.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var grouped_list = GetGroupedToolingTypes().ToList();
            TryAutoSwitchBucket(grouped_list);

            int counter = 0;
            GUILayout.BeginHorizontal();
            foreach (var grouped in grouped_list)
            {
                if (counter % 3 == 0 && counter != 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                counter++;
                string title = GetGroupTitle(grouped.Key);
                if (GUILayout.Button(title, HighLogic.Skin.button))
                {
                    _currentToolingType = grouped.Key;
                    _currentToolingTitle = title;
                    _currentUnderlyingTypes = grouped.Value;
                    // Suppress auto-switch until the PAW target changes, so a manual pick sticks.
                    _lastAutoSwitchPart = ToolingPartResizer.PawTarget();
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

        // Switches the current bucket to match whatever part has its PAW open, if that part's tank
        // type maps to one of the buckets we know about. Only fires once per PAW target so a
        // manual bucket pick survives until the user opens a different part's PAW.
        private void TryAutoSwitchBucket(List<KeyValuePair<string, List<string>>> grouped_list)
        {
            var pawForAutoSwitch = ToolingPartResizer.PawTarget();
            if (pawForAutoSwitch == null) { _lastAutoSwitchPart = null; return; }
            if (pawForAutoSwitch == _lastAutoSwitchPart) return;
            _lastAutoSwitchPart = pawForAutoSwitch;
            var pawType = ToolingPartResizer.CurrentTankType(pawForAutoSwitch);
            if (string.IsNullOrEmpty(pawType)) return;
            var bucketKey = GetGroupingKey(pawType);
            var match = grouped_list.FirstOrDefault(g => g.Key == bucketKey);
            if (match.Value == null) return;
            _currentToolingType = bucketKey;
            _currentToolingTitle = GetGroupTitle(bucketKey);
            _currentUnderlyingTypes = match.Value;
        }

        // Returns the ordered list of tooling-type buttons to show, paired with the DB keys each one covers.
        // Avionics types are collapsed by category letter (all tech levels of "Avionics-N*" share one entry).
        // Tank types are collapsed by construction (Stringer / Isogrid / Balloon / Fuselage / ServiceModule)
        // so a player can search for an existing tooled diameter without picking material first. Non-tank,
        // non-avionics types pass through unchanged.
        private static IEnumerable<KeyValuePair<string, List<string>>> GetGroupedToolingTypes()
        {
            var groups = new Dictionary<string, List<string>>();
            var order = new List<string>();
            foreach (string type in ToolingDatabase.toolings.Keys)
            {
                string key = GetGroupingKey(type);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    groups[key] = list;
                    order.Add(key);
                }
                list.Add(type);
            }
            order.Sort((a, b) => string.Compare(GetGroupTitle(a), GetGroupTitle(b), StringComparison.OrdinalIgnoreCase));
            foreach (var key in order)
                yield return new KeyValuePair<string, List<string>>(key, groups[key]);
        }

        // Category keys for tank groupings. Prefix "Tank-" is reused so GetParametersForToolingType
        // routes them to the diameter/length parameter set the same way bare "Tank-*" DB keys would.
        // Conventional / Isogrid / Balloon / Fuselage each have a non-HP and an HP variant; ServiceModule
        // and Shielded have no HP. HP buttons only appear when at least one underlying type matched.
        private const string TankConventional   = "Tank-Conventional";
        private const string TankConventionalHP = "Tank-ConventionalHP";
        private const string TankIsogrid        = "Tank-Isogrid";
        private const string TankIsogridHP     = "Tank-IsogridHP";
        private const string TankBalloon        = "Tank-Balloon";
        private const string TankBalloonHP      = "Tank-BalloonHP";
        private const string TankFuselage       = "Tank-Fuselage";
        private const string TankFuselageHP     = "Tank-FuselageHP";
        private const string TankServiceModule  = "Tank-ServiceModule";
        private const string TankShieldedKey    = "Tank-Shielded";
        private const string TankCryogenic      = "Tank-Cryogenic";
        private const string TankOther          = "Tank-Other";

        private static readonly Dictionary<string, string> _groupTitles = new Dictionary<string, string>
        {
            { TankConventional,   "Conventional Tanks"    },
            { TankConventionalHP, "HP Conventional Tanks" },
            { TankIsogrid,        "Isogrid Tanks"         },
            { TankIsogridHP,      "HP Isogrid Tanks"      },
            { TankBalloon,        "Balloon Tanks"         },
            { TankBalloonHP,      "HP Balloon Tanks"      },
            { TankFuselage,       "Fuselage"              },
            { TankFuselageHP,     "HP Fuselage"           },
            { TankServiceModule,  "Service Modules"       },
            { TankShieldedKey,    "Shielded Tanks"        },
            { TankCryogenic,      "Cryogenic Tanks"       },
            { TankOther,          "Other Tanks"           },
        };

        private static string GetGroupTitle(string groupKey)
            => _groupTitles.TryGetValue(groupKey, out var t) ? t : ToolingManager.Instance.GetTitleForTooling(groupKey);

        private static string GetGroupingKey(string toolingType)
        {
            string avionicsPrefix = ModuleToolingProcAvionics.MainToolingType + "-";
            if (toolingType.StartsWith(avionicsPrefix) && toolingType.Length > avionicsPrefix.Length)
                return toolingType.Substring(0, avionicsPrefix.Length + 1);

            if (!IsTankTooling(toolingType)) return toolingType;

            // Single-bucket categories (no HP variants in the data).
            if (toolingType == "ServiceModule" || toolingType.StartsWith("SM-")) return TankServiceModule;
            if (toolingType == "TankShielded") return TankShieldedKey;
            if (toolingType == "Cryogenic" || toolingType == "BalloonCryo") return TankCryogenic;

            bool hp = toolingType.EndsWith("-HP");
            if (toolingType.StartsWith("Tank-Iso-"))                          return hp ? TankIsogridHP  : TankIsogrid;
            if (toolingType.StartsWith("Tank-Balloon-") || toolingType == "Balloon")
                                                                              return hp ? TankBalloonHP  : TankBalloon;
            if (toolingType == "Fuselage")                                    return hp ? TankFuselageHP : TankFuselage;
            if (toolingType.StartsWith("Tank-Sep-"))                          return hp ? TankConventionalHP : TankConventional;
            return TankOther;  // legacy RF types we don't know how to place: Default, ElectricPropulsion, etc.
        }


        // RF TANK_DEFINITION names that RP-1 patches (see GameData/RP-1/ProcCosts.cfg), plus the RP-1
        // tank tooling families. Anything not in this set is treated as non-tank and passes through —
        // matters when grouping the Conventional fallback so we don't sweep ProcAvionics /
        // PayloadFairing / Structural / Battery / CrewTube etc. into it.
        // NOTE: "Structural" is intentionally *not* in this list — it's the toolingType assigned to
        // proceduralStructural / proceduralNoseCone / ROT-ModularCargoBay (and the legacy RF Structural
        // tank type). It passes through to get its own button so structural-skin tooling is searchable
        // without picking material first, the same way the tank categories work.
        private static bool IsTankTooling(string toolingType)
        {
            if (toolingType.StartsWith("Tank-") || toolingType.StartsWith("SM-")) return true;
            switch (toolingType)
            {
                case "Default":
                case "Cryogenic":
                case "Fuselage":
                case "ServiceModule":
                case "Balloon":
                case "BalloonCryo":
                case "ElectricPropulsion":
                case "TankShielded":
                    return true;
                default:
                    return false;
            }
        }

        public void RenderTypeTab()
        {
            // Same auto-switch behaviour as RenderToolingTab: opening a PAW on a different tank
            // jumps into that tank's bucket. Needed here because the user is already inside a
            // bucket page (RenderToolingTab doesn't run), and otherwise the page is stuck.
            TryAutoSwitchBucket(GetGroupedToolingTypes().ToList());

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Toolings for type {_currentToolingTitle}", HighLogic.Skin.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            Parameter[] parameters = Parameters.GetParametersForToolingType(_currentToolingType);
            DisplayTypeHeadings(parameters);
            UpdateRowRefitContext();
            // Match the Untooled Parts list width above (3*80 + 312 + scrollbar padding) so the
            // tooling rows have room for the subcategory-prefixed badges ("Modular Booster Tank - Al").
            _toolingTypesScroll = GUILayout.BeginScrollView(_toolingTypesScroll, GUILayout.Width(572), GUILayout.Height(300));
            try
            {
                var values = new float[parameters.Length];
                DisplayRows(GetEntriesForCurrentBucket(), 0, values, parameters);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndScrollView();
        }

        // For grouped types (e.g. "Avionics-N") merge entries across all underlying tech-level keys.
        // For pass-through types _currentUnderlyingTypes contains the single matching DB key.
        // Cached per (bucket, ToolingDatabase.Generation): the merge is O(N*M*log M) work, and the
        // result is invariant unless toolings is mutated.
        private List<ToolingEntry> GetEntriesForCurrentBucket()
        {
            if (_currentUnderlyingTypes == null)
                return ToolingDatabase.toolings[_currentToolingType];
            int gen = ToolingDatabase.Generation;
            if (_mergedCache == null || _mergedCacheKey != _currentToolingType || _mergedCacheGeneration != gen)
            {
                _mergedCache = ToolingDatabase.GetMergedEntries(_currentUnderlyingTypes);
                _mergedCacheKey = _currentToolingType;
                _mergedCacheGeneration = gen;
            }
            return _mergedCache;
        }

        // Resolves the PAW target, current RF tank type, and bucket-match check ONCE per frame so
        // DisplayRow can read pre-computed state instead of re-walking UIPartActionController and
        // Part.Modules per row.
        private void UpdateRowRefitContext()
        {
            _rowPawTarget = ToolingPartResizer.PawTarget();
            if (_rowPawTarget == null)
            {
                _rowRefitEnabled = false;
                _rowRefitDisabledTip = "Open a part's PAW (right-click) to enable.";
                return;
            }
            string currentType = ToolingPartResizer.CurrentTankType(_rowPawTarget);
            bool bucketMatch = currentType != null && GetGroupingKey(currentType) == _currentToolingType;
            _rowRefitEnabled = bucketMatch;
            _rowRefitDisabledTip = bucketMatch
                ? null
                : $"{_rowPawTarget.partInfo?.title} is a {currentType ?? "non-tank"} part -- switch to its bucket to refit.";
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

        private void DisplayRow(float[] values, Parameter[] parameters, ToolingEntry leaf)
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
            if (leaf?.Sources != null && leaf.Sources.Count > 0)
            {
                var materials = leaf.Sources
                    .OrderBy(ExtractTrailingInt)
                    .ThenBy(MaterialLabel)
                    .Select(MaterialLabel);
                GUILayout.Label($"  [{string.Join(", ", materials)}]", HighLogic.Skin.label);

                // Refit button: writes these dims (and switches material if needed) to the part
                // whose PAW is currently open. Disabled when the PAW's tank type belongs to a
                // different bucket - refit only crosses materials within the same construction
                // family (for a Tank-Sep-* part we don't want an Isogrid Tanks refit to apply).
                // PAW target, current type, and bucket-match are resolved once per frame in
                // UpdateRowRefitContext; only the per-row "Refit X to ..." tip and targetType need
                // the row's leaf.Sources, and we skip both when the button is disabled.
                if (parameters.Length == 2)
                {
                    GUILayout.FlexibleSpace();
                    var prev = GUI.enabled;
                    GUI.enabled = _rowRefitEnabled;
                    string tip;
                    string targetType = null;
                    if (_rowRefitDisabledTip != null)
                    {
                        tip = _rowRefitDisabledTip;
                    }
                    else
                    {
                        targetType = ToolingPartResizer.PickRfType(_rowPawTarget, leaf.Sources);
                        tip = $"Refit {_rowPawTarget.partInfo?.title} to {targetType} at d={values[0]:F3}m, L={values[1]:F3}m";
                    }
                    if (GUILayout.Button(new GUIContent("Refit", tip), HighLogic.Skin.button, GUILayout.Width(60), GUILayout.Height(20)))
                        ToolingPartResizer.Resize(_rowPawTarget, values[0], values[1], targetType);
                    GUI.enabled = prev;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DisplayRows(List<ToolingEntry> entries, int parameterIndex, float[] values, Parameter[] parameters)
        {
            if (entries == null) return;
            foreach (var toolingEntry in entries)
            {
                values[parameterIndex] = toolingEntry.Value;
                if (parameterIndex + 1 == parameters.Length)
                    DisplayRow(values, parameters, toolingEntry);
                else
                    DisplayRows(toolingEntry.Children, parameterIndex + 1, values, parameters);
            }
        }

        // Drops the construction segment from Tank-* keys since the bucket title already conveys
        // it ("Conventional Tanks", "Isogrid Tanks", ...). SM-* keeps its numeral suffix. Other
        // dashed keys (Avionics-N3) drop the prefix; bare names pass through.
        //   "Tank-Sep-Al"      -> "Al"
        //   "Tank-Iso-AlCu-HP" -> "AlCu"   (HP redundant — button title says HP)
        //   "SM-II"            -> "SM-II"  (bare numeral would be ambiguous)
        //   "Avionics-N3"      -> "N3"
        //   "Cryogenic"        -> "Cryogenic"
        private static string MaterialLabel(string sourceType)
        {
            if (sourceType.StartsWith("Tank-"))
            {
                var rest = sourceType.Substring(5);
                int dash = rest.IndexOf('-');
                var material = dash >= 0 ? rest.Substring(dash + 1) : rest;
                if (material.EndsWith("-HP")) material = material.Substring(0, material.Length - 3);
                return material;
            }
            if (sourceType.StartsWith("SM-")) return sourceType;
            int dashG = sourceType.IndexOf('-');
            return dashG >= 0 ? sourceType.Substring(dashG + 1) : sourceType;
        }

        // Pulls the trailing integer off a key so "Avionics-N3" sorts before "Avionics-N10".
        private static int ExtractTrailingInt(string s)
        {
            int i = s.Length;
            while (i > 0 && char.IsDigit(s[i - 1])) i--;
            return i < s.Length && int.TryParse(s.Substring(i), out int n) ? n : 0;
        }
    }
}
