using ClickThroughFix;
using RealFuels.Tanks;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static RP0.ProceduralAvionics.ProceduralAvionicsUtils;

namespace RP0.ProceduralAvionics
{
    public class ProceduralAvionicsWindow : MonoBehaviour
    {
        private static readonly int _windowId = "RP0ProcAviWindow".GetHashCode();

        private Rect _windowRect = new Rect(267, 104, 400, 300);
        private GUIContent _gc;
        private string[] _avionicsConfigNames;
        private int _selectedConfigIndex = 0;
        private float _newControlMass;
        private string _sECAmount = "400";
        private string _sExtraVolume = "0";
        private bool _showInfo1, _showInfo2, _showInfo3;
        private bool _showROTankSizeWarning;
        private bool _showSizeWarning;
        private bool _shouldResetUIHeight;
        private Dictionary<string, string> _tooltipTexts;
        private ModuleProceduralAvionics _module;
        private ModuleFuelTanks _rfPM;
        private Dictionary<string, FuelTank> _tanksDict = null;
        private FuelTank _ecTank;

        public string ControllableMass { get; set; }

        public void ShowForModule(ModuleProceduralAvionics module)
        {
            _module = module;
            ControllableMass = $"{module.controllableMass:0.###}";

            _rfPM = module.part.Modules.GetModule<ModuleFuelTanks>();
            FieldInfo fiDict = typeof(ModuleFuelTanks).GetField("tanksDict", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            _tanksDict = (Dictionary<string, FuelTank>)fiDict.GetValue(_rfPM);
            _ecTank = _tanksDict["ElectricCharge"];

            var settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
            _showInfo1 = settings.AvionicsWindow_ShowInfo1;
            _showInfo2 = settings.AvionicsWindow_ShowInfo2;
            _showInfo3 = settings.AvionicsWindow_ShowInfo3;

            RefreshDisplays();
        }

        public void OnTankDefinitionChanged()
        {
            // RF will regenerate all the FuelTank instances if Tank Definition is changed.
            // Thus need to refetch EC tank.
            _ecTank = _tanksDict["ElectricCharge"];
        }

        public void RefreshDisplays()
        {
            _sECAmount = $"{_ecTank.maxAmount:F0}";
            _sExtraVolume = $"{_rfPM.AvailableVolume:0.#}";
        }

        public void OnGUI()
        {
            if (_module != null && _module.showGUI)
            {
                if (_avionicsConfigNames == null)
                {
                    _avionicsConfigNames = ProceduralAvionicsTechManager.GetAvailableConfigs().ToArray();
                    _selectedConfigIndex = _avionicsConfigNames.IndexOf(_module.avionicsConfigName);
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
                GUILayout.Label(_module.info1Text);
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
                GUILayout.Label(_module.info3Text);
            GUILayout.EndHorizontal();

            if (!_module.IsScienceCore)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginHorizontal(GUILayout.Width(250));
                GUILayout.Label("Controllable mass: ", HighLogic.Skin.label, GUILayout.Width(150));
                ControllableMass = GUILayout.TextField(ControllableMass, HighLogic.Skin.textField);
                GUILayout.Label("t", HighLogic.Skin.label);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.MaxWidth(50));
                float oldControlMass = _newControlMass;
                if (float.TryParse(ControllableMass, out _newControlMass))
                {
                    float avionicsMass = _module.GetShieldedAvionicsMass(_newControlMass);
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
            _gc.tooltip = "Amount of excess tank volume";
            GUILayout.Label(_gc, HighLogic.Skin.label, GUILayout.Width(150));
            GUI.enabled = false;
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
            _gc.tooltip = "Applies the parameters above and resizes the part to have the optimal amount of volume";
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
                if (!_module.avionicsConfigName.Equals(curCfgName))
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
                                        curCfgName = _module.avionicsConfigName;
                                        _selectedConfigIndex = _avionicsConfigNames.IndexOf(curCfgName);
                                        _shouldResetUIHeight = true;

                                        _module.showGUI = false;
                                        _module.ShowGUIChanged(null, null);
                                    }, 140.0f, 30.0f, true),
                                new DialogGUIButton("Cancel", () => { }, 140.0f, 30.0f, true)
                                )),
                        false,
                        HighLogic.UISkin);
                }
                else
                {
                    _module.showGUI = false;
                    _module.ShowGUIChanged(null, null);
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

            bool isCurrent = techNode == _module.CurrentProceduralAvionicsTechNode;
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
                _module.avionicsTechLevel = techNode.name;
                _module.CurrentProceduralAvionicsConfig = curCfg;
                _module.avionicsConfigName = curCfg.name;
                _module.AvionicsConfigChanged();
                MonoUtilities.RefreshContextWindows(_module.part);
            }
        }

        private bool DrawUnlockButton(string curCfgName, ProceduralAvionicsTechNode techNode, int unlockCost)
        {
            bool switchedConfig = false;
            if (unlockCost <= 0) return switchedConfig;
            var cmq = CurrencyModifierQueryRP0.RunQuery(TransactionReasonsRP0.PartOrUpgradeUnlock, -unlockCost, 0d, 0d);
            double trueCost = -cmq.GetTotal(CurrencyRP0.Funds, false);
            double creditToUse = Math.Min(trueCost, UnlockCreditHandler.Instance.GetCreditAmount(techNode.TechNodeName));
            cmq.AddPostDelta(CurrencyRP0.Funds, creditToUse, true);
            GUI.enabled = techNode.IsAvailable && cmq.CanAfford();
            _gc.text = $"Unlock ({BuildCostString(Math.Max(0d, trueCost - creditToUse), trueCost)})";
            string tooltip = string.Empty;
            if (trueCost > 0) tooltip = $"Base cost: {BuildCostString(trueCost, trueCost)}\nCredit Applied: {BuildCostString(creditToUse, -1)}";
            if(techNode.IsAvailable) tooltip += (tooltip != string.Empty ? "\n" : string.Empty) + $"Needs tech: {techNode.TechNodeTitle}";
            _gc.tooltip = tooltip;
            if (GUILayout.Button(_gc, HighLogic.Skin.button, GUILayout.Width(120)))
            {
                switchedConfig = ModuleProceduralAvionics.PurchaseConfig(curCfgName, techNode);
            }
            GUI.enabled = true;

            return switchedConfig;
        }

        private void ApplyAvionicsSettings(bool shouldSeekVolume)
        {
            if (!float.TryParse(ControllableMass, out float newControlMass) || newControlMass < 0)
            {
                ScreenMessages.PostScreenMessage("Invalid controllable mass value");
                ControllableMass = $"{_module.controllableMass:0.###}";
                return;
            }

            if (!float.TryParse(_sECAmount, out float ecAmount) || ecAmount <= 0)
            {
                ScreenMessages.PostScreenMessage("EC amount needs to be larger than 0");
                _sECAmount = $"{_ecTank.maxAmount:F0}";
                return;
            }

            _module.controllableMass = newControlMass;
            if (shouldSeekVolume && _module.CanSeekVolume)
            {
                _module.SetProcPartVolumeLimit();
                ApplyCorrectProcTankVolume(0, ecAmount);
            }
            else
            {
                bool isOverVolumeLimit = _module.ClampControllableMass();
                if (isOverVolumeLimit)
                    _module.SetProcPartVolumeLimit();

                // ROTank probe cores do not support SeekVolume()
                _showROTankSizeWarning = isOverVolumeLimit && shouldSeekVolume && !_module.CanSeekVolume;
                _showSizeWarning = isOverVolumeLimit && !shouldSeekVolume;
                _module.UpdateControllableMassSlider();
                _module.SendRemainingVolume();

                _shouldResetUIHeight = true;
                MonoUtilities.RefreshContextWindows(_module.part);

                // In this case need to clamp EC amount to ensure that it stays within the available volume
                float avVolume = _module.GetAvionicsVolume();
                float m3AvailVol = _module.GetAvailableVolume();
                double m3CurVol = avVolume + _rfPM.totalVolume / 1000;    // l to m³, assume 100% RF utilization
                double m3MinVol = GetNeededProcTankVolume(0, ecAmount);
                double m3MissingVol = m3MinVol - m3CurVol;
                if (m3MissingVol > 0.0001)
                {
                    ecAmount = 1;    // Never remove the EC resource entirely
                    double m3AvailVolForEC = m3AvailVol;
                    if (m3AvailVolForEC > 0)
                    {
                        ecAmount = GetECAmountForVolume((float)m3AvailVolForEC);
                    }
                }
            }

            Log($"Applying RF tank amount values to {ecAmount}, currently has {_ecTank.amount}/{_ecTank.maxAmount}, volume {_rfPM.AvailableVolume}");
            _ecTank.maxAmount = ecAmount;
            _ecTank.amount = ecAmount;
            _rfPM.PartResourcesChanged();
            _rfPM.CalculateMass();
            _module.RefreshDisplays();
        }

        private void ApplyCorrectProcTankVolume(float extraVolumeLiters, float ecAmount)
        {
            float m3TotalVolume = GetNeededProcTankVolume(extraVolumeLiters, ecAmount);
            float avVolume = _module.GetAvionicsVolume();
            Log($"Applying volume {m3TotalVolume}; avionics: {avVolume * 1000}l; tanks: {(m3TotalVolume - avVolume) * 1000}l");
            _module.SeekVolume(m3TotalVolume);
        }

        private float GetNeededProcTankVolume(float extraVolumeLiters, float ecAmount)
        {
            float utilizationPercent = _rfPM.utilization;
            float utilization = utilizationPercent / 100;
            float avVolume = _module.GetAvionicsVolume();

            // The amount of final available volume that RF tanks get is calculated in 3 steps:
            // 1) ModuleProceduralAvionics.GetAvailableVolume()
            // 2) RF tank's utilization (the slider in the PAW)
            // 3) RF (internal) tank's per-resource utilization value.
            //    This is currently set at 1000 for EC which means that 1l of volume can hold 1000 units of EC.
            // The code below runs all these but in reversed order.

            float lVolStep3 = ecAmount / _ecTank.utilization;
            float lVolStep2 = (lVolStep3 + extraVolumeLiters) / utilization;
            lVolStep2 = Math.Max(lVolStep2, _module.CurrentProceduralAvionicsTechNode.reservedRFTankVolume);
            float m3VolStep2 = lVolStep2 / 1000;    // RF volumes are in liters but avionics uses m³
            float m3TotalVolume = Math.Max(avVolume + m3VolStep2, m3VolStep2);
            return m3TotalVolume;
        }

        private float GetECAmountForVolume(float m3Volume)
        {
            float utilizationPercent = _rfPM.utilization;
            float utilization = utilizationPercent / 100;

            float step3 = m3Volume * 1000 * utilization;
            float ecAmount = step3 * _ecTank.utilization;
            return ecAmount;
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

        private string BuildTechName(ProceduralAvionicsTechNode techNode) 
        {
            string title = techNode.dispName ?? techNode.name;
            return techNode.IsAvailable ? title : $"<color=orange>{title}</color>";
        }

        private string BuildCostString(double cost, double baseCost) =>
            (baseCost == 0 || HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch) ? string.Empty : $"{cost:N0}";

        private string ConstructTooltipForAvionicsTL(ProceduralAvionicsTechNode techNode)
        {
            var sb = StringBuilderCache.Acquire();
            if (!techNode.IsAvailable)
            {
                sb.AppendLine($"<color=orange>This tech level can be used in simulations but will prevent the vessel from being built until {techNode.TechNodeTitle} has been researched.</color>\n");
            }

            float calcMass = ModuleProceduralAvionics.GetStatsForTechNode(techNode, _newControlMass, out float massKG, out _, out float powerWatts);
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

        private static string BoolToYesNoString(bool b) => b ? "Yes" : "No";
    }
}
