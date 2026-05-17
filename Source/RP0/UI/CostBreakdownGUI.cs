using KSPCommunityFixes;
using RealFuels;
using Smooth.Slinq;
using System;
using System.Collections;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RP0.UI
{
    public class CostBreakdownGUI : UIBase
    {
        private struct CostEntry
        {
            public string name;
            public double effectiveCost;
            public double effectiveCostModifier;
        }
        
        private float _lastUpdateTrigger = 0f;
        private readonly Dictionary<CostEntry, int> _partCosts = new Dictionary<CostEntry, int>();
        private readonly Dictionary<Part, CostEntry> _partMap = new Dictionary<Part, CostEntry>();
        private ILookup<CostEntry, Part> _partLookup;
        private readonly Dictionary<CostEntry, Rect> _costRects = new Dictionary<CostEntry, Rect>();
        private readonly List<KeyValuePair<CostEntry, int>> _cachedSortedPartCosts = new List<KeyValuePair<CostEntry, int>>();
        private readonly List<Part> _singlePart = new List<Part>(); // dummy list for calling GetEffectiveCost, to avoid extra allocations
        private Vector2 _partCostsScroll = new Vector2();
        private bool _isToolingTempDisabled = false;
        private IEnumerator _activeUpdate = null; 

        private static CostEntry DummyEntry = new CostEntry { name = "(n/a)", effectiveCost = -1, effectiveCostModifier = 0 };
        private CostEntry _hoveredEntry;
        private readonly List<Part> _cachedHighlightedParts = new List<Part>();
        private Part _hoveredPart;
        private Texture2D _highlightRowStyle = null;

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

        protected override void OnStart()
        {
            GameEvents.onEditorShipModified.Add(ShipModifiedEvent);
        }

        protected override void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(ShipModifiedEvent);
            if (_highlightRowStyle != null)
                UnityEngine.Object.Destroy(_highlightRowStyle);
        }

        public UITab RenderCostBreakdownTab()
        {
            VesselProject vessel = SpaceCenterManagement.Instance?.EditorVessel;
            if (vessel == null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("No vessel currently detected.", HighLogic.Skin.label, GUILayout.Width(500));
                GUILayout.EndHorizontal();
                return UITab.CostBreakdown;
            }

            if (_partCosts.Count == 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Either there are no parts, or cost analysis is still running.", HighLogic.Skin.label, GUILayout.Width(500));
                GUILayout.EndHorizontal();
                return UITab.CostBreakdown;
            }

            Part newHoveredPart = Mouse.HoveredPart;
            if (newHoveredPart != null && EventSystem.current.IsPointerOverGameObject())
                newHoveredPart = null;
            if (newHoveredPart != _hoveredPart)
            {
                _hoveredPart = newHoveredPart;
                if (_hoveredPart != null && _partMap.TryGetValue(_hoveredPart, out CostEntry entry) && _costRects.TryGetValue(entry, out Rect r))
                {
                    float rowMid = r.y + r.height / 2f;
                    _partCostsScroll.y = Mathf.Max(0f, rowMid - 150f);
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Total Effective Cost", "Effective cost is a metric that includes the various cost multiplier tags attached to each part and resource.\n" +
                "This is usually the preferred metric for cost comparison between different rockets."), HighLogic.Skin.label, GUILayout.Width(312));
            GUILayout.Label($"{vessel.effectiveCost:N2}", RightLabel, GUILayout.Width(144));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Effective Cost after Leader effects (conditional only)", "Unconditional integration speed buffs/debuffs are ignored."), HighLogic.Skin.label, GUILayout.Width(312));
            GUILayout.Label($"{vessel.ModifiedEC:N2}", RightLabel, GUILayout.Width(144));
            GUILayout.EndHorizontal();

            DrawHorizontalSeparator();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Part Name", BoldLabel, GUILayout.Width(312));
            GUILayout.Label(new GUIContent("Ind. Effective Cost", "Global effective-cost modifiers are not included unless they originate from the part."), BoldRightLabel, GUILayout.Width(144));
            GUILayout.EndHorizontal();

            _partCostsScroll = GUILayout.BeginScrollView(_partCostsScroll, GUILayout.Height(300), GUILayout.Width(500));

            CostEntry newHoveredEntry = DummyEntry;

            foreach (var kvp in _cachedSortedPartCosts)
            {
                CostEntry entry = kvp.Key;
                int multiplicity = kvp.Value;
                if (Event.current.type == EventType.Repaint && _hoveredPart != null && _partMap.TryGetValue(_hoveredPart, out CostEntry hoveredEntry) &&
                    entry.Equals(hoveredEntry) && _costRects.TryGetValue(hoveredEntry, out Rect highlightRect))
                {
                    GUI.DrawTexture(highlightRect, HighlightRowTex);
                }
                GUILayout.BeginHorizontal();
                string name = entry.name;
                if (multiplicity > 1)
                    name = $"{multiplicity} x " + name;
                GUILayout.Label(name, BoldLabel, GUILayout.Width(312));
                GUIContent costAndTooltip = new GUIContent(FormatCostAndMultiplier(entry.effectiveCost * multiplicity, entry.effectiveCostModifier), 
                                               FormatCostAndMultiplier(entry.effectiveCost, entry.effectiveCostModifier) + " per part");
                GUILayout.Label(costAndTooltip, RightLabel, GUILayout.Width(144));
                GUILayout.EndHorizontal();
                if (Event.current.type == EventType.Repaint)
                {
                    Rect rowRect = GUILayoutUtility.GetLastRect();
                    _costRects[entry] = rowRect;
                    if (rowRect.Contains(Event.current.mousePosition))
                        newHoveredEntry = entry;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.BeginVertical();
            try
            {
                RenderToolingPreviewButton();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint && !newHoveredEntry.Equals(_hoveredEntry))
            {
                ChangeHoveredEntry(newHoveredEntry);
            }

            return UITab.CostBreakdown;
        }

        private void ChangeHoveredEntry(CostEntry newHoveredEntry)
        {
            if (_cachedHighlightedParts.Count != 0)
            {
                foreach (Part p in _cachedHighlightedParts)
                {
                    p.SetHighlightDefault();
                }
                _cachedHighlightedParts.Clear();
            }
            _hoveredEntry = newHoveredEntry;
            if (!_hoveredEntry.Equals(DummyEntry) && _partLookup != null)
            {
                _cachedHighlightedParts.AddRange(_partLookup[_hoveredEntry]);
                foreach (Part p in _cachedHighlightedParts)
                {
                    p.SetHighlightColor(Color.yellow);
                    p.SetHighlightType(Part.HighlightType.AlwaysOn);
                }
            }
        }

        private void ShipModifiedEvent(ShipConstruct ship)
        {
            float now = Time.time;
            if (_lastUpdateTrigger < now)
            {
                if (_activeUpdate != null)
                    UIHolder.Instance?.StopCoroutine(_activeUpdate);
                _lastUpdateTrigger = now;
                _activeUpdate = Update(ship);
                UIHolder.Instance?.StartCoroutine(_activeUpdate);
            }
        }

        private IEnumerator Update(ShipConstruct ship) 
        {
            yield return null;
            // Stall a frame.

            var parts = ship?.Parts; 
            _partCosts.Clear();
            _partMap.Clear();
            _cachedSortedPartCosts.Clear();
            _partLookup = null;
            if (parts?.Count > 0)
            {
                for (int i = parts.Count - 1; i >= 0; --i)
                {
                    Part p = parts[i];
                    CostEntry entry = new CostEntry();
                    entry.name = GetPartDisplayName(p);
                    _singlePart.Clear();
                    _singlePart.Add(p);
                    entry.effectiveCost = VesselProject.GetEffectiveCost(_singlePart);
                    List<string> tags = ModuleTagList.GetTags(p);
                    if (tags?.Count > 0)
                        entry.effectiveCostModifier = Leaders.LeaderUtils.GetPartEffectiveCostEffect(tags);
                    else
                        entry.effectiveCostModifier = 1;
                    if (_partCosts.TryGetValue(entry, out int value))
                        _partCosts[entry] = value + 1;
                    else
                        _partCosts.Add(entry, 1);
                    _partMap[p] = entry;
                }
                _cachedSortedPartCosts.AddRange(_partCosts.OrderByDescending(kvp => kvp.Key.effectiveCost * kvp.Key.effectiveCostModifier * kvp.Value));
                _partLookup = _partMap.ToLookup(p => p.Value, p => p.Key);
            }
        }

        private void RenderToolingPreviewButton()
        {
            var c = new GUIContent("Press to preview fully tooled integration time & cost", "Hold the button pressed and watch the cost & time values change in the Integration Info window");
            if (GUILayout.RepeatButton(c, HighLogic.Skin.button, GUILayout.Width(500)))
            {
                if (!_isToolingTempDisabled)
                {
                    _isToolingTempDisabled = true;
                    ToolingManager.Instance.toolingEnabled = false;
                    GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
                }
            }
            else if (_isToolingTempDisabled && Event.current.type == EventType.Repaint)   // button events are handled on the Repaint pass
            {
                ToolingManager.Instance.toolingEnabled = HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
                _isToolingTempDisabled = false;
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
        }

        private static string FormatCostAndMultiplier(double cost, double multiplier)
        { 
            string ret = $"{cost * multiplier:N2}";
            const double eps = 0.001;
            if (Math.Abs(multiplier - 1) > eps)
            {
                ret = $"<color={CurrencyModifierQueryRP0.TextStylingColor(multiplier < 1, true)}>({LocalizationHandler.FormatRatioAsPercent(multiplier)})</color> " + ret;
            }
            return ret;
        }

        private static string GetPartDisplayName(Part p)
        {
            var toolingModule = p.FindModuleImplementingFast<ModuleTooling>();
            if (toolingModule != null) 
                // for single-tooling module parts we can insert this info to help disambiguate
                return $"{p.partInfo?.title} ({toolingModule.ToolingTypeTitle}) {toolingModule.GetToolingParameterInfo()}";
            
            var engineModule = p.FindModuleImplementingFast<ModuleEngineConfigsBase>();
            if (engineModule != null)
                return $"{p.partInfo?.title} ({engineModule.configurationDisplay})";

            return (p.partInfo?.title == null) ? "(unknown)" : p.partInfo.title;
        }
    }

}
