using ClickThroughFix;
using RealFuels.Tanks;
using RP0.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static RP0.ProceduralAvionics.ProceduralAvionicsUtils;

namespace RP0.ProceduralAvionics
{
    public partial class ModuleProceduralAvionics
    {
        private static readonly int _windowId = "RP0ProcAviWindow".GetHashCode();

        [KSPField]
        public string info1Text = string.Empty;
        [KSPField]
        public string info3Text = string.Empty;

        private Rect _windowRect = new Rect(267, 104, 400, 300);
        private GUIContent _gc;
        private string[] _avionicsConfigNames;
        private int _selectedConfigIndex = 0;
        private string _sControllableMass = "0";
        private float _newControlMass;
        private string _sECAmount = "400";
        private string _sExtraVolume = "0";
        private bool _showInfo1, _showInfo2, _showInfo3;
        private bool _showROTankSizeWarning;
        private bool _showSizeWarning;
        private bool _shouldResetUIHeight;
        private Dictionary<string, string> _tooltipTexts;

        public void OnGUI()
        {
            if (showGUI)
            {
                if (_avionicsConfigNames == null)
                {
                    _avionicsConfigNames = ProceduralAvionicsTechManager.GetAvailableConfigs().ToArray();
                    _selectedConfigIndex = _avionicsConfigNames.IndexOf(avionicsConfigName);
                    _tooltipTexts = new Dictionary<string, string>();
                }

                if (_shouldResetUIHeight && Event.current.type == EventType.Layout)
                {
                    _windowRect.height = 300;
                    _shouldResetUIHeight = false;
                }
                _windowRect = ClickThruBlocker.GUILayoutWindow(_windowId, _windowRect, WindowFunction, "Configure Procedural Avionics", HighLogic.Skin.window);
                Tooltip.Instance.ShowTooltip(_windowId, contentAlignment: TextAnchor.MiddleLeft);
            }
        }


        private void WindowFunction(int windowID)
        {
            GUILayout.BeginHorizontal();

            bool oldShowInfo = _showInfo1;
            _showInfo1 = GUILayout.Toggle(_showInfo1, "ⓘ", HighLogic.Skin.button, GUILayout.ExpandWidth(false), GUILayout.Height(20));
            if (oldShowInfo != _showInfo1)
            {
                var settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
                settings.AvionicsWindow_ShowInfo1 = _showInfo1;
                _shouldResetUIHeight = true;
            }

            if (_showInfo1)
                GUILayout.Label(info1Text);
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.Label("Choose the avionics type:", HighLogic.Skin.label);
            int oldConfigIdx = _selectedConfigIndex;
            _selectedConfigIndex = GUILayout.Toolbar(_selectedConfigIndex, _avionicsConfigNames, HighLogic.Skin.button);
            if (oldConfigIdx != _selectedConfigIndex)
            {
                _shouldResetUIHeight = true;
                _tooltipTexts.Clear();
            }

            string curCfgName = _avionicsConfigNames[_selectedConfigIndex];
            ProceduralAvionicsConfig curCfg = ProceduralAvionicsTechManager.GetProceduralAvionicsConfig(curCfgName);

            GUILayout.BeginHorizontal();

            oldShowInfo = _showInfo2;
            _showInfo2 = GUILayout.Toggle(_showInfo2, "ⓘ", HighLogic.Skin.button, GUILayout.ExpandWidth(false), GUILayout.Height(20));
            if (oldShowInfo != _showInfo2)
            {
                var settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
                settings.AvionicsWindow_ShowInfo2 = _showInfo2;
                _shouldResetUIHeight = true;
            }

            if (_showInfo2)
                GUILayout.Label(curCfg.description);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(7);

            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.Label("Choose the tech level:", HighLogic.Skin.label);
            foreach (ProceduralAvionicsTechNode techNode in curCfg.TechNodes.Values)
            {
                DrawAvionicsConfigSelector(curCfg, techNode);
            }
            GUILayout.EndVertical();

            GUILayout.Space(7);

            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.BeginHorizontal();

            oldShowInfo = _showInfo3;
            _showInfo3 = GUILayout.Toggle(_showInfo3, "ⓘ", HighLogic.Skin.button, GUILayout.ExpandWidth(false), GUILayout.Height(20));
            if (oldShowInfo != _showInfo3)
            {
                var settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
                settings.AvionicsWindow_ShowInfo3 = _showInfo3;
                _shouldResetUIHeight = true;
            }

            if (_showInfo3)
                GUILayout.Label(info3Text);
            GUILayout.EndHorizontal();

            if (!IsScienceCore)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginHorizontal(GUILayout.Width(250));
                GUILayout.Label("Controllable mass: ", HighLogic.Skin.label, GUILayout.Width(150));
                _sControllableMass = GUILayout.TextField(_sControllableMass, HighLogic.Skin.textField);
                GUILayout.Label("t", HighLogic.Skin.label);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.MaxWidth(50));
                float oldControlMass = _newControlMass;
                if (float.TryParse(_sControllableMass, out _newControlMass))
                {
                    float avionicsMass = GetShieldedAvionicsMass(_newControlMass);
                    GUILayout.Label($" ({avionicsMass * 1000:0.#} kg)", HighLogic.Skin.label, GUILayout.Width(150));
                }

                if (oldControlMass != _newControlMass)
                {
                    _tooltipTexts.Clear();
                }

                GUILayout.EndHorizontal();
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Width(250));
            GUILayout.Label("EC amount: ", HighLogic.Skin.label, GUILayout.Width(150));
            _sECAmount = GUILayout.TextField(_sECAmount, HighLogic.Skin.textField);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.MaxWidth(50));
            if (float.TryParse(_sECAmount, out float ecAmount))
            {
                GUILayout.Label($" ({_ecTank.mass * ecAmount:0.#} kg)", HighLogic.Skin.label, GUILayout.Width(150));
            }
            GUILayout.EndHorizontal();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.Width(250));
            _gc ??= new GUIContent();
            _gc.text = "Additional tank volume: ";
            _gc.tooltip = "How much tank volume will be left for other resources after applying the desired controllable mass and amount of EC.";
            GUILayout.Label(_gc, HighLogic.Skin.label, GUILayout.Width(150));
            GUI.enabled = _seekVolumeMethod != null;
            _sExtraVolume = GUILayout.TextField(_sExtraVolume, HighLogic.Skin.textField);
            GUI.enabled = true;
            GUILayout.Label("l", HighLogic.Skin.label);
            GUILayout.EndHorizontal();

            if (_showROTankSizeWarning)
            {
                GUILayout.Label("ROTanks does not currently support automatic resizing to correct dimensions. Increase the part size manually until it has sufficient volume.", HighLogic.Skin.label);
            }
            else if (_showSizeWarning)
            {
                GUILayout.Label("Not enough volume to apply parameters. Increase the part size manually until it has sufficient volume.", HighLogic.Skin.label);
            }

            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            _gc.text = "Apply (resize to fit)";
            _gc.tooltip = "Applies the parameters above and resizes the part to have the correct amount of volume";
            if (GUILayout.Button(_gc, HighLogic.Skin.button))
            {
                ApplyAvionicsSettings(shouldSeekVolume: true);
            }

            _gc.text = "Apply (preserve dimensions)";
            _gc.tooltip = "Tries to apply the parameters above but doesn't resize the part even if there isn't enough volume, or if there's extra volume";
            if (GUILayout.Button(_gc, HighLogic.Skin.button))
            {
                ApplyAvionicsSettings(shouldSeekVolume: false);
            }

            if (GUILayout.Button("Close", HighLogic.Skin.button))
            {
                if (!avionicsConfigName.Equals(curCfgName))
                {
                    PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog(
                            "ConfirmProceduralAvionicsClose",
                            "Your selected avionics type does not match the current tab shown. Close window anyway?",
                            "Avionics Mismatch",
                            HighLogic.UISkin,
                            new Rect(0.5f, 0.5f, 150f, 60f),
                            new DialogGUIFlexibleSpace(),
                            new DialogGUIVerticalLayout(
                                new DialogGUIFlexibleSpace(),
                                new DialogGUIButton("Yes",
                                    () =>
                                    {
                                        // Reset tab
                                        curCfgName = avionicsConfigName;
                                        _selectedConfigIndex = _avionicsConfigNames.IndexOf(curCfgName);
                                        _shouldResetUIHeight = true;

                                        showGUI = false;
                                    }, 140.0f, 30.0f, true),
                                new DialogGUIButton("Cancel", () => { }, 140.0f, 30.0f, true)
                                )),
                        false,
                        HighLogic.UISkin);
                }
                else
                {
                    showGUI = false;
                }
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();

            Tooltip.Instance.RecordTooltip(_windowId);
        }

        private void DrawAvionicsConfigSelector(ProceduralAvionicsConfig curCfg, ProceduralAvionicsTechNode techNode)
        {
            bool switchedConfig = false;
            int unlockCost = ProceduralAvionicsTechManager.GetUnlockCost(curCfg.name, techNode);

            _gc ??= new GUIContent();
            _gc.tooltip = GetTooltipTextForTechNode(techNode);

            bool isCurrent = techNode == CurrentProceduralAvionicsTechNode;
            if (isCurrent)
            {
                _gc.text = BuildTechName(techNode);
                GUILayout.BeginHorizontal();
                GUILayout.Toggle(true, _gc, HighLogic.Skin.button);
                DrawUnlockButton(curCfg.name, techNode, unlockCost);
                GUILayout.EndHorizontal();

                _gc.text = $"Sample container: {BoolToYesNoString(techNode.hasScienceContainer)}";
                _gc.tooltip = "Whether samples can be transferred and stored in the avionics unit.";
                GUILayout.Label(_gc, HighLogic.Skin.label);

                _gc.text = $"Can hibernate: {BoolToYesNoString(techNode.disabledPowerFactor > 0)}";
                _gc.tooltip = "Whether the avionics unit can enter hibernation mode that greatly reduces power consumption.";
                GUILayout.Label(_gc, HighLogic.Skin.label);

                _gc.text = $"Axial control: {BoolToYesNoString(techNode.allowAxial)}";
                _gc.tooltip = "Whether fore-aft translation is allowed despite having insufficient controllable mass or being outside the max range of Near-Earth avionics.";
                GUILayout.Label(_gc, HighLogic.Skin.label);
            }
            else
            {
                _gc.text = $"Switch to {BuildTechName(techNode)}";
                GUILayout.BeginHorizontal();
                switchedConfig = GUILayout.Button(_gc, HighLogic.Skin.button);
                switchedConfig |= DrawUnlockButton(curCfg.name, techNode, unlockCost);
                GUILayout.EndHorizontal();
            }

            if (switchedConfig)
            {
                Log("Configuration window changed, updating part window");
                _shouldResetUIHeight = true;
                _showROTankSizeWarning = false;
                _showSizeWarning = false;
                SetupConfigNameFields();
                avionicsTechLevel = techNode.name;
                CurrentProceduralAvionicsConfig = curCfg;
                avionicsConfigName = curCfg.name;
                AvionicsConfigChanged();
                MonoUtilities.RefreshContextWindows(part);
            }
        }

        private bool DrawUnlockButton(string curCfgName, ProceduralAvionicsTechNode techNode, int unlockCost)
        {
            bool switchedConfig = false;
            if (unlockCost <= 0) return switchedConfig;
            var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.RnDPartPurchase, unlockCost, 0d, 0d);
            double trueCost = -cmq.GetTotal(CurrencyRP0.Funds);
            double subsidyToUse = Math.Min(trueCost, UnlockSubsidyHandler.Instance.GetSubsidyAmount(techNode.TechNodeName));
            cmq.AddDeltaAuthorized(CurrencyRP0.Funds, subsidyToUse);
            GUI.enabled = techNode.IsAvailable && cmq.CanAfford();
            _gc.text = $"Unlock ({BuildCostString(Math.Max(0d, trueCost - subsidyToUse))})";
            string tooltip = $"Base cost: {BuildCostString(trueCost)}\nSubsidyApplied: {BuildCostString(subsidyToUse)}";
            if(techNode.IsAvailable) tooltip += $"\nNeeds tech: {techNode.TechNodeTitle}";
            if (GUILayout.Button(_gc, HighLogic.Skin.button, GUILayout.Width(120)))
            {
                switchedConfig = PurchaseConfig(curCfgName, techNode);
            }
            GUI.enabled = true;

            return switchedConfig;
        }

        private void ApplyAvionicsSettings(bool shouldSeekVolume)
        {
            if (!float.TryParse(_sControllableMass, out float newControlMass) || newControlMass < 0)
            {
                ScreenMessages.PostScreenMessage("Invalid controllable mass value");
                _sControllableMass = $"{controllableMass:0.###}";
                return;
            }
            if (!float.TryParse(_sExtraVolume, out float extraVolumeLiters) || extraVolumeLiters < 0)
            {
                ScreenMessages.PostScreenMessage("Invalid Additional volume value");
                _sExtraVolume = "0";
                return;
            }
            if (!float.TryParse(_sECAmount, out float ecAmount) || ecAmount <= 0)
            {
                ScreenMessages.PostScreenMessage("EC amount needs to be larger than 0");
                _sECAmount = $"{_ecTank.maxAmount:F0}";
                return;
            }

            controllableMass = newControlMass;
            bool canSeekVolume = _seekVolumeMethod != null && _seekVolumeMethod.GetParameters().Length == 2;
            if (shouldSeekVolume && canSeekVolume)
            {
                // Store and sum together the volume of all resources other than EC on this part
                double otherFuelVolume = 0;
                var otherTanks = new List<KeyValuePair<FuelTank, double>>();
                if (_tanksDict != null)
                {
                    foreach (FuelTank t in _tanksDict.Values)
                    {
                        if (t == _ecTank || t.maxAmount == 0) continue;
                        otherTanks.Add(new KeyValuePair<FuelTank, double>(t, t.maxAmount));
                        otherFuelVolume += t.maxAmount / t.utilization;
                    }
                }
                else
                {
                    foreach (FuelTank t in _tankList)
                    {
                        if (t == _ecTank || t.maxAmount == 0) continue;
                        otherTanks.Add(new KeyValuePair<FuelTank, double>(t, t.maxAmount));
                        otherFuelVolume += t.maxAmount / t.utilization;
                    }
                }

                SetProcPartVolumeLimit();
                ApplyCorrectProcTankVolume(extraVolumeLiters + (float)otherFuelVolume, ecAmount);

                // Restore all the pre-resize amounts in tanks
                foreach (KeyValuePair<FuelTank, double> kvp in otherTanks)
                {
                    FuelTank t = kvp.Key;
                    t.amount = t.maxAmount = kvp.Value;
                }
            }
            else
            {
                bool isOverVolumeLimit = ClampControllableMass();
                if (isOverVolumeLimit)
                    SetProcPartVolumeLimit();

                // ROTank probe cores do not support SeekVolume()
                _showROTankSizeWarning = isOverVolumeLimit && shouldSeekVolume && !canSeekVolume;
                _showSizeWarning = isOverVolumeLimit && !shouldSeekVolume;
                UpdateControllableMassSlider();
                SendRemainingVolume();

                _shouldResetUIHeight = true;
                MonoUtilities.RefreshContextWindows(part);
            }

            Log($"Applying RF tank amount values to {ecAmount}, currently has {_ecTank.amount}/{_ecTank.maxAmount}, volume {_rfPM.AvailableVolume}");
            _ecTank.maxAmount = ecAmount;
            _ecTank.amount = ecAmount;
            _rfPM.PartResourcesChanged();
            _rfPM.CalculateMass();
            RefreshDisplays();
        }

        private void ApplyCorrectProcTankVolume(float extraVolumeLiters, float ecAmount)
        {
            var fiUtil = typeof(ModuleFuelTanks).GetField("utilization", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            float utilizationPercent = (float)fiUtil.GetValue(_rfPM);
            float utilization = utilizationPercent / 100;
            float avVolume = GetAvionicsVolume();

            // The amount of final available volume that RF tanks get is calculated in 4 steps:
            // 1) ModuleProceduralAvionics.GetAvailableVolume()
            // 2) SphericalTankUtilities.GetSphericalTankVolume()
            // 3) RF tank's utilization (the slider in the PAW)
            // 4) RF (internal) tank's per-resource utilization value.
            //    This is currently set at 1000 for EC which means that 1l of volume can hold 1000 units of EC.
            // The code below runs all these but in reversed order.

            float lVolStep4 = ecAmount / _ecTank.utilization;
            float lVolStep3 = (lVolStep4 + extraVolumeLiters) / utilization;
            float m3VolStep3 = lVolStep3 / 1000;    // RF volumes are in liters but avionics uses m³
            float m3VolStep2 = SphericalTankUtilities.GetRequiredVolumeFromSphericalTankVolume(m3VolStep3);
            float m3TotalVolume = Math.Max((InternalTanksAvailableVolumeUtilization * avVolume + m3VolStep2) / InternalTanksAvailableVolumeUtilization,
                                           m3VolStep2 / InternalTanksTotalVolumeUtilization);

            Log($"Applying volume {m3TotalVolume}; avionics: {avVolume * 1000}l; tanks: {(m3TotalVolume - avVolume) * 1000}l");
            SeekVolume(m3TotalVolume);
        }

        private string GetTooltipTextForTechNode(ProceduralAvionicsTechNode techNode)
        {
            if (!_tooltipTexts.TryGetValue(techNode.name, out string tooltip))
            {
                tooltip = ConstructTooltipForAvionicsTL(techNode);
                _tooltipTexts[techNode.name] = tooltip;
            }

            return tooltip;
        }

        private string BuildTechName(ProceduralAvionicsTechNode techNode) => techNode.dispName ?? techNode.name;

        private string BuildCostString(double cost) =>
            (cost == 0 || HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch) ? string.Empty : $"{cost:N0}";

        private string ConstructTooltipForAvionicsTL(ProceduralAvionicsTechNode techNode)
        {
            var sb = StringBuilderCache.Acquire();
            float calcMass = GetStatsForTechNode(techNode, _newControlMass, out float massKG, out _, out float powerWatts);
            string indent = string.Empty;
            if (!techNode.IsScienceCore)
            {
                sb.AppendLine($"At {calcMass:0.##}t controllable mass:");
                indent = "  ";
            }
            sb.AppendLine($"{indent}Mass: {massKG:0.#}kg");
            sb.AppendLine($"{indent}Power consumption: {powerWatts:0.#}W");

            sb.AppendLine($"Axial control: {BoolToYesNoString(techNode.allowAxial)}");
            sb.AppendLine($"Can hibernate: {BoolToYesNoString(techNode.disabledPowerFactor > 0)}");
            sb.Append($"Sample container: {BoolToYesNoString(techNode.hasScienceContainer)}");

            return sb.ToStringAndRelease();
        }

        public static float GetStatsForTechNode(ProceduralAvionicsTechNode techNode, float controllableMass, out float massKG, out float cost, out float powerWatts)
        {
            if (techNode.IsScienceCore)
                controllableMass = 0f;
            else if (controllableMass <= 0)
                controllableMass = techNode.interplanetary ? 0.5f : 100f;
            
            massKG = GetAvionicsMass(techNode, controllableMass) * 1000;
            cost = GetAvionicsCost(controllableMass, techNode);
            powerWatts = GetEnabledkW(techNode, controllableMass) * 1000;
            return controllableMass;
        }

        private static string BoolToYesNoString(bool b) => b ? "Yes" : "No";
    }
}
