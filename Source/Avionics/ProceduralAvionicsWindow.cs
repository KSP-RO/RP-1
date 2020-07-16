using RealFuels.Tanks;
using RP0.Utilities;
using System;
using System.Reflection;
using UnityEngine;
using static RP0.ProceduralAvionics.ProceduralAvionicsUtils;

namespace RP0.ProceduralAvionics
{
    public partial class ModuleProceduralAvionics
    {
        [KSPField]
        public string info1Text = string.Empty;
        [KSPField]
        public string info3Text = string.Empty;

        private Rect _windowRect = new Rect(267, 104, 400, 300);
        private string[] _avionicsConfigNames;
        private int _selectedConfigIndex = 0;
        private string _sControllableMass = "0";
        private string _sECAmount = "400";
        private string _sExtraVolume = "0";
        private bool _showInfo1, _showInfo2, _showInfo3;

        public void OnGUI()
        {
            if (showGUI)
            {
                if (_avionicsConfigNames == null)
                {
                    _avionicsConfigNames = ProceduralAvionicsTechManager.GetAvailableConfigs().ToArray();
                    _selectedConfigIndex = _avionicsConfigNames.IndexOf(avionicsConfigName);
                }

                _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, WindowFunction, "Configure Procedural Avionics", HighLogic.Skin.window);
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
            }

            if (_showInfo1)
                GUILayout.Label(info1Text);
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.Label("Choose the avionics type:", HighLogic.Skin.label);
            _selectedConfigIndex = GUILayout.Toolbar(_selectedConfigIndex, _avionicsConfigNames, HighLogic.Skin.button);
            string curCfgName = _avionicsConfigNames[_selectedConfigIndex];
            ProceduralAvionicsConfig curCfg = ProceduralAvionicsTechManager.GetProceduralAvionicsConfig(curCfgName);

            GUILayout.BeginHorizontal();

            oldShowInfo = _showInfo2;
            _showInfo2 = GUILayout.Toggle(_showInfo2, "ⓘ", HighLogic.Skin.button, GUILayout.ExpandWidth(false), GUILayout.Height(20));
            if (oldShowInfo != _showInfo1)
            {
                var settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
                settings.AvionicsWindow_ShowInfo2 = _showInfo2;
            }

            if (_showInfo2)
                GUILayout.Label(curCfg.description);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(7);

            GUILayout.BeginVertical(HighLogic.Skin.box);
            GUILayout.Label("Choose the tech level:", HighLogic.Skin.label);
            foreach (var techNode in curCfg.TechNodes.Values)
            {
                if (!techNode.IsAvailable)
                {
                    continue;
                }

                var switchedConfig = false;
                var unlockCost = ProceduralAvionicsTechManager.GetUnlockCost(curCfgName, techNode);
                if (unlockCost == 0)
                {
                    bool isCurrent = techNode == CurrentProceduralAvionicsTechNode;
                    if (isCurrent)
                    {
                        GUILayout.Toggle(true, BuildTechName(techNode), HighLogic.Skin.button);
                        GUILayout.Label("Sample container: " + (techNode.hasScienceContainer ? "Yes" : "No"), HighLogic.Skin.label);
                        GUILayout.Label("Can hibernate: " + (techNode.disabledPowerFactor > 0 ? "Yes" : "No"), HighLogic.Skin.label);
                    }
                    else if (GUILayout.Button("Switch to " + BuildTechName(techNode), HighLogic.Skin.button))
                    {
                        switchedConfig = true;
                    }
                }
                else if (Funding.Instance.Funds < unlockCost)
                {
                    GUILayout.Label($"Can't afford {BuildTechName(techNode)} BuildCostString(unlockCost)", HighLogic.Skin.label);
                }
                else if (GUILayout.Button($"Purchase {BuildTechName(techNode)} {BuildCostString(unlockCost)}", HighLogic.Skin.button))
                {
                    switchedConfig = true;
                    if (!HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
                    {
                        switchedConfig = ProceduralAvionicsTechManager.PurchaseConfig(curCfgName, techNode);
                    }
                    if (switchedConfig)
                    {
                        ProceduralAvionicsTechManager.SetMaxUnlockedTech(curCfgName, techNode.name);
                    }
                }

                if (switchedConfig)
                {
                    Log("Configuration window changed, updating part window");
                    SetupConfigNameFields();
                    avionicsTechLevel = techNode.name;
                    CurrentProceduralAvionicsConfig = curCfg;
                    avionicsConfigName = curCfgName;
                    AvionicsConfigChanged();
                    MonoUtilities.RefreshContextWindows(part);
                }
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
            }

            if (_showInfo3)
                GUILayout.Label(info3Text);
            GUILayout.EndHorizontal();

            if (!IsScienceCore)
            {
                GUILayout.BeginHorizontal(GUILayout.MaxWidth(250));
                GUILayout.Label("Controllable mass: ", HighLogic.Skin.label, GUILayout.Width(150));
                _sControllableMass = GUILayout.TextField(_sControllableMass, HighLogic.Skin.textField);
                GUILayout.Label("t", HighLogic.Skin.label);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal(GUILayout.MaxWidth(250));
            GUILayout.Label("EC amount: ", HighLogic.Skin.label, GUILayout.Width(150));
            _sECAmount = GUILayout.TextField(_sECAmount, HighLogic.Skin.textField);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.MaxWidth(250));
            GUILayout.Label("Additional tank volume: ", HighLogic.Skin.label, GUILayout.Width(150));
            _sExtraVolume = GUILayout.TextField(_sExtraVolume, HighLogic.Skin.textField);
            GUILayout.Label("l", HighLogic.Skin.label);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply", HighLogic.Skin.button))
            {
                ApplyAvionicsSettings();
            }

            if (GUILayout.Button("Close", HighLogic.Skin.button))
            {
                showGUI = false;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private void ApplyAvionicsSettings()
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
            if (_seekVolumeMethod != null && _seekVolumeMethod.GetParameters().Length == 2)
            {
                SetProcPartVolumeLimit();
                ApplyCorrectProcTankVolume(extraVolumeLiters, ecAmount);
            }
            else
            {
                // ROTank probe cores do not support SeekVolume()
                ClampControllableMass();
                MonoUtilities.RefreshContextWindows(part);
            }

            _rfPM.CalculateMass();
            Log($"Applying RF tank amount values to {ecAmount}, currently has {_ecTank.amount}/{_ecTank.maxAmount}, volume {_rfPM.AvailableVolume}");
            _ecTank.maxAmount = ecAmount;
            _ecTank.amount = ecAmount;
            _sExtraVolume = $"{_rfPM.AvailableVolume:0.#}";
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

        private string BuildTechName(ProceduralAvionicsTechNode techNode) => techNode.dispName ?? techNode.name;

        private string BuildCostString(int cost) =>
            (cost == 0 || HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch) ? string.Empty : $" ({cost:N})";
    }
}
