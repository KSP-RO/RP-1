using KSPAPIExtensions;
using RealFuels.Tanks;
using RP0.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using static RP0.ProceduralAvionics.ProceduralAvionicsUtils;

namespace RP0.ProceduralAvionics
{
    public partial class ModuleProceduralAvionics : ModuleAvionics, IPartMassModifier, IPartCostModifier
    {
        private const string KwFormat = "{0:0.##}";
        private const string WFormat = "{0:0}";
        private const float FloatTolerance = 1.00001f;
        private const float InternalTanksTotalVolumeUtilization = 0.246f; //Max utilization for 2 spheres within a cylindrical container worst case scenario
        private const float InternalTanksAvailableVolumeUtilization = 0.5f;

        #region Fields and properties

        [KSPField(isPersistant = true, guiName = "Contr. Mass", guiActiveEditor = true, guiUnits = "\u2009t", groupName = PAWGroup, groupDisplayName = PAWGroup),
         UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.05f, sigFigs = 3, unit = "\u2009t")]
        public float controllableMass = -1;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Configuration", groupName = PAWGroup, groupDisplayName = PAWGroup), UI_ChooseOption(scene = UI_Scene.Editor)]
        public string avionicsConfigName;

        [KSPField(isPersistant = true)]
        public string avionicsTechLevel;

        [KSPField(guiActiveEditor = true, guiName = "Avionics Utilization", groupName = PAWGroup)]
        public string utilizationDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Power Requirements", groupName = PAWGroup)]
        public string powerRequirementsDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Avionics Mass", groupName = PAWGroup)]
        public string massDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Avionics Cost", groupName = PAWGroup)]
        public string costDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Configure", groupName = PAWGroup)]
        [UI_Toggle(enabledText = "Hide GUI", disabledText = "Show GUI")]
        [NonSerialized]
        public bool showGUI;

        public bool IsScienceCore => CurrentProceduralAvionicsTechNode.massExponent == 0 && CurrentProceduralAvionicsTechNode.powerExponent == 0 && CurrentProceduralAvionicsTechNode.costExponent == 0;

        private static bool _configsLoaded = false;

        private bool _started = false;
        private float _cachedVolume = float.MaxValue;
        private BaseEventDetails _cachedEventData = null;

        private PartModule _procPartPM;
        private PartModule _roTankPM;
        private ModuleFuelTanks _rfPM;
        private FuelTank _ecTank;
        private MethodInfo _seekVolumeMethod;
        private FieldInfo _rfIsDirtyField;
        private FieldInfo _procPartMinVolumeField;
        private PropertyInfo _procPartCurShapeProp;

        public ProceduralAvionicsConfig CurrentProceduralAvionicsConfig { get; private set; }

        public ProceduralAvionicsTechNode CurrentProceduralAvionicsTechNode
        {
            get
            {
                if (CurrentProceduralAvionicsConfig != null && avionicsTechLevel != null && CurrentProceduralAvionicsConfig.TechNodes.ContainsKey(avionicsTechLevel))
                {
                    return CurrentProceduralAvionicsConfig.TechNodes[avionicsTechLevel];
                }
                return new ProceduralAvionicsTechNode();
            }
        }

        public float Utilization => GetAvionicsMass() / MaxAvionicsMass;

        private float MaxAvionicsMass => (_cachedVolume - CurrentProceduralAvionicsTechNode.reservedRFTankVolume) * 
                                         CurrentProceduralAvionicsTechNode.avionicsDensity;

        public float InternalTanksVolume { get; private set; }

        #endregion

        #region Get Utilities
        protected override float GetInternalMassLimit() => !IsScienceCore ? controllableMass : 0;

        private bool ClampControllableMass()
        {
            var maxControllableMass = GetMaximumControllableMass();
            if (controllableMass > maxControllableMass * FloatTolerance)
            {
                Log($"Resetting procedural mass limit to {maxControllableMass}, was {controllableMass}");
                controllableMass = maxControllableMass;
                _sControllableMass = $"{controllableMass:0.###}";
                MonoUtilities.RefreshContextWindows(part);
                return true;
            }
            return false;
        }

        private float GetControllableMass(float avionicsMass) 
        {
            float res = GetInversePolynomial(avionicsMass * 1000, CurrentProceduralAvionicsTechNode.massExponent, CurrentProceduralAvionicsTechNode.massConstant, CurrentProceduralAvionicsTechNode.massFactor);
            if (float.IsNaN(res) || float.IsInfinity(res)) return 0;
            return (float)Math.Round(res, 3);   // Round to the nearest kg since that is the smallest increment on slider
        }
//        private float GetMaximumControllableMass() => FloorToSliderIncrement(GetControllableMass(MaxAvionicsMass));
        private float GetMaximumControllableMass() => GetControllableMass(MaxAvionicsMass);

        private float GetAvionicsMass() => GetPolynomial(GetInternalMassLimit(), CurrentProceduralAvionicsTechNode.massExponent, CurrentProceduralAvionicsTechNode.massConstant, CurrentProceduralAvionicsTechNode.massFactor) / 1000f;
        private float GetAvionicsCost() => GetPolynomial(GetInternalMassLimit(), CurrentProceduralAvionicsTechNode.costExponent, CurrentProceduralAvionicsTechNode.costConstant, CurrentProceduralAvionicsTechNode.costFactor);
        private float GetAvionicsVolume() => GetAvionicsMass() / CurrentProceduralAvionicsTechNode.avionicsDensity;

        private float GetShieldedAvionicsMass()
        {
            var avionicsMass = GetAvionicsMass();
            return avionicsMass + GetShieldingMass(avionicsMass);
        }

        private float GetShieldingMass(float avionicsMass) => Mathf.Pow(avionicsMass, 2f / 3) * CurrentProceduralAvionicsTechNode.shieldingMassFactor;

        protected override float GetEnabledkW() => GetPolynomial(GetInternalMassLimit(), CurrentProceduralAvionicsTechNode.powerExponent, CurrentProceduralAvionicsTechNode.powerConstant, CurrentProceduralAvionicsTechNode.powerFactor) / 1000f;
        protected override float GetDisabledkW() => GetEnabledkW() * CurrentProceduralAvionicsTechNode.disabledPowerFactor;

        private static float GetPolynomial(float value, float exponent, float constant, float factor) => (Mathf.Pow(value, exponent) + constant) * factor;
        private static float GetInversePolynomial(float value, float exponent, float constant, float factor) => Mathf.Pow(value / factor - constant, 1 / exponent);

        protected override bool GetToggleable() => CurrentProceduralAvionicsTechNode.disabledPowerFactor > 0;

        protected override string GetTonnageString() => "This part can be configured to allow control of vessels up to any mass.";

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => CurrentProceduralAvionicsTechNode.avionicsDensity > 0 ? GetShieldedAvionicsMass() : 0;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => CurrentProceduralAvionicsTechNode.avionicsDensity > 0 ? GetAvionicsCost() : 0;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        #endregion

        #region Callbacks

        public override void OnLoad(ConfigNode node)
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING && !_configsLoaded)
            {
                try
                {
                    Log("Loading Avionics Configs");
                    ProceduralAvionicsTechManager.LoadAvionicsConfigs();
                    _configsLoaded = true;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            base.OnLoad(node);
        }

        public override void OnStart(StartState state)
        {
            Profiler.BeginSample("RP0ProcAvi OnStart");
            Log($"OnStart for {part} in {HighLogic.LoadedScene}");
            LoadPartModulesAndFields();
            SetFallbackConfigForLegacyCraft();
            SetupConfigNameFields();
            SetControllableMassForLegacyCraft();
            AvionicsConfigChanged();
            SetupGUI();
            base.OnStart(state);
            massLimit = controllableMass;
            _started = true;
            if (_cachedEventData != null)
                OnPartVolumeChanged(_cachedEventData);
            Profiler.EndSample();
        }

        public void Start()
        {
            // Delay SetScienceContainer to Unity.Start() to allow PartModule removal
            if (!HighLogic.LoadedSceneIsEditor)
                SetScienceContainer();
        }

        #endregion

        #region OnStart Utilities

        private void LoadPartModulesAndFields()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            _procPartPM = GetProcPartPM();
            if (_procPartPM != null)
            {
                BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                _procPartMinVolumeField = _procPartPM.GetType().GetField("volumeMin", flags);
                _procPartCurShapeProp = _procPartPM.GetType().GetProperty("CurrentShape", flags);
            }
            else    // is it ROTanks instead?
            {
                _roTankPM = GetROTankPM();
            }

            _rfPM = part.Modules.GetModule<ModuleFuelTanks>();
            _rfIsDirtyField = typeof(ModuleFuelTanks).GetField("massDirty", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            var fiTanks = typeof(ModuleFuelTanks).GetField("tankList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            var tankList = (FuelTankList)fiTanks.GetValue(_rfPM);
            _ecTank = tankList["ElectricCharge"];

            _seekVolumeMethod = GetSeekVolumeMethod();
        }

        private MethodInfo GetSeekVolumeMethod()
        {
            if (_procPartPM != null)
            {
                BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
                MethodInfo mi = _procPartPM.GetType().GetMethod("SeekVolume", flags);

                if (mi.GetParameters().Length == 1)
                    ShowOutdatedProcPartWarning();

                return mi;
            }
            return null;
        }

        private PartModule GetProcPartPM()
        {
            if (part.Modules.Contains("ProceduralPart"))
            {
                return part.Modules["ProceduralPart"];
            }
            return null;
        }

        private PartModule GetROTankPM()
        {
            if (part.Modules.Contains("ModuleROTank"))
            {
                return part.Modules["ModuleROTank"];
            }
            return null;
        }

        private void SetFallbackConfigForLegacyCraft()
        {
            if (HighLogic.LoadedSceneIsEditor && !ProceduralAvionicsTechManager.GetAvailableConfigs().Contains(avionicsConfigName))
            {
                string s = avionicsConfigName;
                avionicsConfigName = ProceduralAvionicsTechManager.GetPurchasedConfigs().First();
                Log($"Current config ({s}) not available, defaulting to {avionicsConfigName}");
            }
            if (string.IsNullOrEmpty(avionicsTechLevel))
            {
                avionicsTechLevel = ProceduralAvionicsTechManager.GetMaxUnlockedTech(avionicsConfigName);
                Log($"Defaulting avionics tech level to {avionicsTechLevel}");
            }
        }

        private void SetControllableMassForLegacyCraft()
        {
            if (controllableMass < 0)
            {
                controllableMass = HighLogic.LoadedSceneIsFlight ? float.MaxValue : 0;
            }
        }

        private void SetupConfigNameFields()
        {
            Fields[nameof(avionicsConfigName)].guiActiveEditor = true;
            var range = Fields[nameof(avionicsConfigName)].uiControlEditor as UI_ChooseOption;
            range.options = ProceduralAvionicsTechManager.GetPurchasedConfigs().ToArray();

            if (string.IsNullOrEmpty(avionicsConfigName))
            {
                avionicsConfigName = range.options[0];
                Log($"Defaulted config to {avionicsConfigName}");
            }
        }

        private void SetupGUI()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;

            Fields[nameof(controllableMass)].uiControlEditor.onFieldChanged = ControllableMassChanged;
            Fields[nameof(avionicsConfigName)].uiControlEditor.onFieldChanged = AvionicsConfigChanged;
            Fields[nameof(massLimit)].guiActiveEditor = false;

            var settings = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>();
            _showInfo1 = settings.AvionicsWindow_ShowInfo1;
            _showInfo2 = settings.AvionicsWindow_ShowInfo2;
            _showInfo3 = settings.AvionicsWindow_ShowInfo3;
        }

        #endregion

        #region UI Slider Tools

        private int GetSigFigs(float value)
        {
            var smallIncrementExponent = GetSmallIncrementExponent(value);
            return Math.Max(1 - (int)smallIncrementExponent, 0);
        }

        private float CeilingToSmallIncrement(float value)
        {
            var smallIncrement = GetSmallIncrement(value);
            return Mathf.Ceil(value / smallIncrement) * smallIncrement;
        }

        private float FloorToPrecision(float value, float precision) => Mathf.Floor(value / precision) * precision;

        private float GetSliderIncrement(float value)
        {
            var smallIncrement = GetSmallIncrement(value);
            return Math.Min(smallIncrement / 10, 1f);
        }

        private float GetSmallIncrement(float value)
        {
            var exponent = GetSmallIncrementExponent(value);
            return (float)Math.Pow(10, exponent);
        }

        private double GetSmallIncrementExponent(float maxValue)
        {
            var log = Math.Log(maxValue, 10);
            return Math.Max(Math.Floor(log - 1.3), -2);
        }

        private void UpdateControllableMassSlider()
        {
            Fields[nameof(controllableMass)].guiActiveEditor = !IsScienceCore;
            UI_FloatEdit controllableMassEdit = Fields[nameof(controllableMass)].uiControlEditor as UI_FloatEdit;

            if (CurrentProceduralAvionicsConfig != null && CurrentProceduralAvionicsTechNode != null)
            {
                // Formula for controllable mass given avionics mass is Mathf.Pow(1000*avionicsMass / massFactor - massConstant, 1 / massExponent)
                controllableMassEdit.maxValue = Mathf.Max(GetMaximumControllableMass() * 1.25f, 0.01f);
            }
            else
                controllableMassEdit.maxValue = 0.001f;
            Log($"UpdateControllableMassSlider() MaxCtrlMass: {controllableMassEdit.maxValue}");
            controllableMassEdit.minValue = 0;
            controllableMassEdit.incrementSmall = GetSmallIncrement(controllableMassEdit.maxValue);
            controllableMassEdit.incrementLarge = controllableMassEdit.incrementSmall * 10;
            controllableMassEdit.incrementSlide = GetSliderIncrement(controllableMassEdit.maxValue);
            controllableMassEdit.sigFigs = GetSigFigs(controllableMassEdit.maxValue);
            controllableMassEdit.maxValue = FloorToPrecision(controllableMassEdit.maxValue, controllableMassEdit.incrementSlide);
        }

        #endregion

        #region Events and Change Handlers

        private void ControllableMassChanged(BaseField arg1, object arg2)
        {
            Profiler.BeginSample("RP0ProcAvi ControllableMassChanged");
            Log($"ControllableMassChanged to {arg1.GetValue(this)} from {arg2}");
            if (float.IsNaN(controllableMass))
            {
                Debug.LogError("ProcAvi - ControllableMassChanged tried to set to NAN! Resetting to 0.");
                controllableMass = 0;
            }

            float preClampedMass = controllableMass;
            if (ClampControllableMass() && preClampedMass > massLimit && _procPartMinVolumeField != null)
            {
                // Looks like it was already at the limit but player tried to increase it further
                Log("At limit but should increase PP size");
                controllableMass = preClampedMass;
                SetProcPartVolumeLimit();
            }

            massLimit = controllableMass;
            _sControllableMass = $"{controllableMass:0.###}";
            StartCoroutine(DeferredRFTankChangeHandler());
            SendRemainingVolume();
            RefreshDisplays();
            Profiler.EndSample();
        }

        private void AvionicsConfigChanged(BaseField f, object obj)
        {
            avionicsTechLevel = ProceduralAvionicsTechManager.GetMaxUnlockedTech(avionicsConfigName);
            AvionicsConfigChanged();
        }

        private void AvionicsConfigChanged()
        {
            Profiler.BeginSample("RP0ProcAvi AvionicsConfigChanged");
            CurrentProceduralAvionicsConfig = ProceduralAvionicsTechManager.GetProceduralAvionicsConfig(avionicsConfigName);
            Log($"Avionics Config changed to: {avionicsConfigName}.  Tech: {avionicsTechLevel}");
            interplanetary = CurrentProceduralAvionicsTechNode.interplanetary;
            if (_started && HighLogic.LoadedSceneIsEditor)
            {
                // Don't fire these if cachedVolume isn't known yet.
                UpdateMassLimitsAndVolume();
                SetProcPartVolumeLimit();
            }
            if (!GetToggleable())
                systemEnabled = true;
            SetActionsAndGui();
            RefreshDisplays();
            if (HighLogic.LoadedSceneIsEditor)
            {
                _sControllableMass = $"{controllableMass:0.###}";
                StartCoroutine(DeferredRFTankChangeHandler());
                GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
            }
            Profiler.EndSample();
        }

        [KSPEvent]
        public void OnResourceInitialChanged(BaseEventDetails eventData)
        {
            if (_ecTank == null || eventData.Get<PartResource>("resource")?.part != _rfPM.part)
                return;

            StartCoroutine(DeferredRFTankChangeHandler());
        }

        [KSPEvent]
        public void OnPartVolumeChanged(BaseEventDetails eventData)
        {
            Profiler.BeginSample("RP0ProcAvi OnPartVolumeChanged");
            float volume = (float)eventData.Get<double>("newTotalVolume");
            Log($"OnPartVolumeChanged to {volume} from {_cachedVolume}");
            if (!_started)
            {
                Log("Delaying OnPartVolumeChanged until after Start()");
                _cachedEventData = eventData;
                return;
            }
            _cachedVolume = volume;

            UpdateMassLimitsAndVolume();
            RefreshDisplays();
            Profiler.EndSample();
        }

        private void UpdateMassLimitsAndVolume()
        {
            if (ClampControllableMass())
                SetProcPartVolumeLimit();
            UpdateControllableMassSlider();
            SendRemainingVolume();
        }

        private void SendRemainingVolume()
        {
            Profiler.BeginSample("RP0ProcAvi SendRemainingVolume");
            if (_started && _cachedVolume < float.MaxValue)
            {
                Events[nameof(OnPartVolumeChanged)].active = false;
                InternalTanksVolume = SphericalTankUtilities.GetSphericalTankVolume(GetAvailableVolume());
                float availVol = GetAvailableVolume();
                Log($"SendRemainingVolume():  Cached Volume: {_cachedVolume}. AvionicsVolume: {GetAvionicsVolume()}.  AvailableVolume: {availVol}.  Internal Tanks: {InternalTanksVolume}");
                SendVolumeChangedEvent(InternalTanksVolume);
                Events[nameof(OnPartVolumeChanged)].active = true;
            }
            Profiler.EndSample();
        }

        private float GetAvailableVolume() => Math.Max(Math.Min((_cachedVolume - GetAvionicsVolume()) * InternalTanksAvailableVolumeUtilization, _cachedVolume * InternalTanksTotalVolumeUtilization), 0);

        public void SendVolumeChangedEvent(double newVolume)
        {
            var data = new BaseEventDetails(BaseEventDetails.Sender.USER);
            data.Set<string>("volName", "Tankage");
            data.Set<double>("newTotalVolume", newVolume);
            part.SendEvent(nameof(OnPartVolumeChanged), data, 0);
        }

        private IEnumerator DeferredRFTankChangeHandler()
        {
            // ¯\_(ツ)_/¯
            // You never know when RF actually manages to update it's information

            yield return new WaitForFixedUpdate();

            int curFrame = 0, maxWaitFrames = 15;
            while ((bool)_rfIsDirtyField.GetValue(_rfPM) && curFrame++ < maxWaitFrames)
            {
                yield return new WaitForFixedUpdate();
            }

            if (_ecTank != null) _sECAmount = $"{_ecTank.maxAmount:F0}";
            if (_rfPM != null) _sExtraVolume = $"{_rfPM.AvailableVolume:0.#}";
        }

        #endregion

        private bool SetProcPartVolumeLimit()
        {
            if (_procPartPM != null && _procPartMinVolumeField != null && _procPartCurShapeProp != null)
            {
                Profiler.BeginSample("RP0ProcAvi SetProcPartVolumeLimit");
                float minVolume = GetAvionicsVolume();
                minVolume += CurrentProceduralAvionicsTechNode.reservedRFTankVolume;
                Log($"Setting PP min volume to {minVolume}");

                _procPartMinVolumeField.SetValue(_procPartPM, minVolume);
                var ppShape = _procPartCurShapeProp.GetValue(_procPartPM);

                BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                MethodInfo mi = ppShape?.GetType().GetMethod("AdjustDimensionBounds", flags);
                mi?.Invoke(ppShape, Array.Empty<object>());

                // Only resize if the difference is more than 0.1l
                float diff = Math.Abs(minVolume - _cachedVolume);
                if (diff > 0.0001 && minVolume > _cachedVolume)
                {
                    Log($"{minVolume} > {_cachedVolume}, diff {diff}, calling SeekVolume");
                    SeekVolume(minVolume);
                }
                Profiler.EndSample();

                return true;
            }

            Log("SetProcPartVolumeLimit not supported");
            return false;
        }

        private void SeekVolume(float targetVolume)
        {
            Profiler.BeginSample("RP0ProcAvi SeekVolume");
            if (_seekVolumeMethod != null)
            {
                Log($"SeekVolume() CurrentAvionicsVolume for max util: {GetAvionicsVolume()}, Desired Volume: {targetVolume}");
                try
                {
                    // New PP has extra argument for specifying in which direction the size should be nudged in case there isn't a precise fit.
                    // -1 = Floor; 0 = Nearest; 1 = Ceil
                    var args = _seekVolumeMethod.GetParameters().Length == 1 ? new object[] { targetVolume } :
                                                                               new object[] { targetVolume, 1 };
                    _seekVolumeMethod.Invoke(_procPartPM, args);
                }
                catch (Exception ex) { Debug.LogError($"{ex?.InnerException.Message ?? ex.Message}"); }
            }
            else
            {
                Log("SeekVolume not supported");
            }
            Profiler.EndSample();
        }

        private void ShowOutdatedProcPartWarning()
        {
            PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                                         new Vector2(0.5f, 0.5f),
                                         "ShowOutdatedProcPartWarning",
                                         "Outdated Procedural Parts version",
                                         "RP-1 has detected an outdated version of the Procedural Parts mod. Some procedural avionics features will now be disabled. For best experience please update to the latest Procedural Parts release.",
                                         "OK",
                                         false,
                                         HighLogic.UISkin);
        }

        /// <summary>
        /// For Kerbalism integration
        /// </summary>
        /// <returns></returns>
        public new static string BackgroundUpdate(Vessel v,
            ProtoPartSnapshot part_snapshot, ProtoPartModuleSnapshot module_snapshot,
            PartModule proto_part_module, Part proto_part,
            Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest,
            double elapsed_s) => ModuleAvionics.BackgroundUpdate(v, part_snapshot, module_snapshot, proto_part_module, proto_part, availableResources, resourceChangeRequest, elapsed_s);

        private void RefreshDisplays()
        {
            RefreshPowerDisplay();
            massDisplay = MathUtils.FormatMass(CurrentProceduralAvionicsTechNode.avionicsDensity > 0 ? GetShieldedAvionicsMass() : 0);
            costDisplay = $"{Mathf.Round(CurrentProceduralAvionicsTechNode.avionicsDensity > 0 ? GetAvionicsCost() : 0)}";
            utilizationDisplay = $"{Utilization * 100:0.#}%";
            Log($"RefreshDisplays() Controllable mass: {controllableMass}, mass: {massDisplay} cost: {costDisplay}, Utilization: {utilizationDisplay}");
        }

        private void RefreshPowerDisplay()
        {
            var powerConsumptionBuilder = StringBuilderCache.Acquire();
            AppendPowerString(powerConsumptionBuilder, GetEnabledkW());
            float dkW = GetDisabledkW();
            if (dkW > 0)
            {
                powerConsumptionBuilder.Append(" /");
                AppendPowerString(powerConsumptionBuilder, dkW);
            }
            powerRequirementsDisplay = powerConsumptionBuilder.ToStringAndRelease();
        }

        private void AppendPowerString(System.Text.StringBuilder builder, float val)
        {
            if (val >= 1)
                builder.AppendFormat(KwFormat, val).Append("\u2009kW");
            else
                builder.AppendFormat(WFormat, val * 1000).Append("\u2009W");
        }

        private void SetScienceContainer()
        {
            if (!CurrentProceduralAvionicsTechNode.hasScienceContainer)
            {
                if (part.FindModuleImplementing<ModuleScienceContainer>() is ModuleScienceContainer module)
                    part.RemoveModule(module);
            }
            Log($"Setting science container to {(CurrentProceduralAvionicsTechNode.hasScienceContainer ? "enabled." : "disabled.")}");
        }
    }
}
