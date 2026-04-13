using KSPCommunityFixes;
using RealFuels;
using Smooth.Slinq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RP0.UI
{
    public class CostBreakdownGUI : UIBase
    {
        private struct CostEntry
        {
            public string name;
            public double effectiveCost;
            public double effectiveCostModifier;

            public override bool Equals(object obj)
            {
                if (obj is CostEntry other)
                    return Equals(other);
                else
                    return false;
            }

            private bool Equals(CostEntry other)
            {
                return effectiveCost == other.effectiveCost && effectiveCostModifier == other.effectiveCostModifier && name == other.name;
            }

            public override int GetHashCode()
            {
                return (name, effectiveCost, effectiveCostModifier).GetHashCode();
            }
        }
        
        private float _lastUpdateTrigger = 0f;
        private readonly Dictionary<CostEntry, int> _partCosts = new Dictionary<CostEntry, int>();
        private readonly List<Part> _singlePart = new List<Part>(); // dummy list for calling GetEffectiveCost, to avoid extra allocations
        private Vector2 _partCostsScroll = new Vector2();
        private bool _isToolingTempDisabled = false;
        private IEnumerator _activeUpdate = null; 

        protected override void OnStart()
        {
            GameEvents.onEditorShipModified.Add(ShipModifiedEvent);
        }

        protected override void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(ShipModifiedEvent);
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
            foreach (var kvp in _partCosts.OrderByDescending(kvp => kvp.Key.effectiveCost * kvp.Key.effectiveCostModifier * kvp.Value))
            {
                CostEntry entry = kvp.Key;
                int multiplicity = kvp.Value;
                GUILayout.BeginHorizontal();
                string name = entry.name;
                if (multiplicity > 1)
                    name = $"{multiplicity} x " + name;
                GUILayout.Label(name, BoldLabel, GUILayout.Width(312));
                GUIContent costAndTooltip = new GUIContent(FormatCostAndMultiplier(entry.effectiveCost * multiplicity, entry.effectiveCostModifier), 
                                               FormatCostAndMultiplier(entry.effectiveCost, entry.effectiveCostModifier) + " per part");
                GUILayout.Label(costAndTooltip, RightLabel, GUILayout.Width(144));
                GUILayout.EndHorizontal();
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
            return UITab.CostBreakdown;
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
            if (parts?.Count > 0)
            {
                _partCosts.Clear();

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
                }
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
