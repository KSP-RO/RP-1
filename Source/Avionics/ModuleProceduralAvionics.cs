using KSPAPIExtensions;
using RP0.Utilities;
using System;
using System.Linq;
using UnityEngine;

using static RP0.ProceduralAvionics.ProceduralAvionicsUtils;

namespace RP0.ProceduralAvionics
{
    class ModuleProceduralAvionics : ModuleAvionics, IPartMassModifier, IPartCostModifier
    {
        private const string KwFormat = "{0:0.##}";
        private const string WFormat = "{0:0}";
        private const float FloatTolerance = 1.00001f;
        private const float InternalTanksTotalVolumeUtilization = 0.246f; //Max utilization for 2 spheres within a cylindrical container worst case scenario
        private const float InternalTanksAvailableVolumeUtilization = 0.5f;

        #region Fields

        [KSPField(isPersistant = true, guiName = "Contr. Mass", guiActive = false, guiActiveEditor = true, guiUnits = "\u2009t"),
         UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.05f, sigFigs = 3, unit = "\u2009t")]
        public float controllableMass = -1;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Configuration"), UI_ChooseOption(scene = UI_Scene.Editor)]
        public string avionicsConfigName;

        [KSPField(isPersistant = true)]
        public string avionicsTechLevel;

        [KSPField(isPersistant = true)]
        public float avionicsDensity;

        [KSPField(isPersistant = true)]
        public float massExponent;

        [KSPField(isPersistant = true)]
        public float massConstant;

        [KSPField(isPersistant = true)]
        public float massFactor;

        [KSPField(isPersistant = true)]
        public float shieldingMassFactor;

        [KSPField(isPersistant = true)]
        public float costExponent;

        [KSPField(isPersistant = true)]
        public float costConstant;

        [KSPField(isPersistant = true)]
        public float costFactor;

        [KSPField(isPersistant = true)]
        public float powerExponent;

        [KSPField(isPersistant = true)]
        public float powerConstant;

        [KSPField(isPersistant = true)]
        public float powerFactor;

        [KSPField(isPersistant = true)]
        public float disabledPowerFactor;

        [KSPField(isPersistant = true)]
        public bool hasScienceContainer = false;

        [KSPField(guiActiveEditor = true, guiName = "Avionics Utilization")]
        public string utilizationDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Power Requirements")]
        public string powerRequirementsDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Avionics Mass")]
        public string massDisplay;

        [KSPField(guiActiveEditor = true, guiName = "Avionics Cost")]
        public string costDisplay;

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

        private float MaxAvionicsMass => cachedVolume * avionicsDensity;

        public float InternalTanksVolume { get; private set; }

        #endregion
        protected override float GetInternalMassLimit() => controllableMass;

        private void ClampControllableMass()
        {
            var maxControllableMass = GetMaximumControllableMass();
            if (controllableMass > maxControllableMass * FloatTolerance)
            {
                Log($"Resetting procedural mass limit to {Mathf.Floor(maxControllableMass)}, was {controllableMass}");
                controllableMass = Mathf.Floor(maxControllableMass);
                MonoUtilities.RefreshContextWindows(part);
            }
        }

        private float GetControllableMass(float avionicsMass) => GetInversePolynomial(avionicsMass * 1000, massExponent, massConstant, massFactor);
//        private float GetMaximumControllableMass() => FloorToSliderIncrement(GetControllableMass(MaxAvionicsMass));
        private float GetMaximumControllableMass() => GetControllableMass(MaxAvionicsMass);

        private float GetAvionicsMass() => GetPolynomial(GetInternalMassLimit(), massExponent, massConstant, massFactor) / 1000f;
        private float GetAvionicsCost() => GetPolynomial(GetInternalMassLimit(), costExponent, costConstant, costFactor);
        private float GetAvionicsVolume() => GetAvionicsMass() / avionicsDensity;

        private float GetShieldedAvionicsMass()
        {
            var avionicsMass = GetAvionicsMass();
            return avionicsMass + GetShieldingMass(avionicsMass);
        }

        private float GetShieldingMass(float avionicsMass) => Mathf.Pow(avionicsMass, 2f / 3) * shieldingMassFactor;

        protected override float GetEnabledkW() => GetPolynomial(GetInternalMassLimit(), powerExponent, powerConstant, powerFactor) / 1000f;
        protected override float GetDisabledkW() => GetEnabledkW() * disabledPowerFactor;

        private static float GetPolynomial(float value, float exponent, float constant, float factor) => (Mathf.Pow(value, exponent) + constant) * factor;
        private static float GetInversePolynomial(float value, float exponent, float constant, float factor) => Mathf.Pow(value / factor - constant, 1 / exponent);

        protected override bool GetToggleable() => disabledPowerFactor > 0;

        protected override string GetTonnageString() => "This part can be configured to allow control of vessels up to any mass.";

        public override void OnLoad(ConfigNode node)
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING)
            {
                Log("Loading Avionics Configs");
                ProceduralAvionicsTechManager.LoadAvionicsConfigs(node);
            }
        }

        private bool started = false;

        public override void OnStart(StartState state)
        {
            Log($"OnStart in {HighLogic.LoadedScene}");
            SetFallbackConfigForLegacyCraft();
            SetupConfigNameFields();
            SetControllableMassForLegacyCraft();
            AvionicsConfigChanged();
            base.OnStart(state);
            SetScienceContainerIfNeeded();
            Fields[nameof(controllableMass)].uiControlEditor.onFieldChanged = ControllableMassChanged;
            Fields[nameof(avionicsConfigName)].uiControlEditor.onFieldChanged = AvionicsConfigChanged;
            started = true;
            if (cachedEventData != null)
                OnPartVolumeChanged(cachedEventData);
            Log("OnStart finished");
        }

        private void SetScienceContainerIfNeeded()
        {
            if (!HighLogic.LoadedSceneIsEditor)
            {
                SetScienceContainer();
            }
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
            Log("SetupConfigSelectors()");
            Fields[nameof(avionicsConfigName)].guiActiveEditor = true;
            var range = Fields[nameof(avionicsConfigName)].uiControlEditor as UI_ChooseOption;
            range.options = ProceduralAvionicsTechManager.GetPurchasedConfigs().ToArray();

            if (string.IsNullOrEmpty(avionicsConfigName))
            {
                avionicsConfigName = range.options[0];
                Log($"Defaulted config to {avionicsConfigName}");
            }
        }



        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => avionicsDensity > 0 ? GetShieldedAvionicsMass() : 0;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => avionicsDensity > 0 ? GetAvionicsCost() : 0;
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        private void UpdateControllableMassSlider()
        {
            UI_FloatEdit controllableMassEdit = Fields[nameof(controllableMass)].uiControlEditor as UI_FloatEdit;

            if (CurrentProceduralAvionicsConfig != null && CurrentProceduralAvionicsTechNode != null)
            {
                // Formula for controllable mass given avionics mass is Mathf.Pow(1000*avionicsMass / massFactor - massConstant, 1 / massExponent)
                // This is NaN is avionicsMass*1000 < massConstant * massFactor, so avoid that case or detect it.
                float maxVal = GetMaximumControllableMass();
                if (float.IsNaN(maxVal))
                    maxVal = 0;
                controllableMassEdit.maxValue = Mathf.Max(maxVal, 0.001f);
            }
            else
                controllableMassEdit.maxValue = 0.001f;
            Log($"UpdateControllableMassSlider() MaxCtrlMass: {controllableMassEdit.maxValue}");
            controllableMassEdit.minValue = 0;
            controllableMassEdit.incrementSmall = GetSmallIncrement(controllableMassEdit.maxValue);
            controllableMassEdit.incrementLarge = controllableMassEdit.incrementSmall * 10;
            controllableMassEdit.incrementSlide = GetSliderIncrement(controllableMassEdit.maxValue);
            controllableMassEdit.sigFigs = GetSigFigs(controllableMassEdit.maxValue);
        }

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

        private float FloorToSliderIncrement(float value)
        {
            float sliderIncrement = GetSliderIncrement(value);
            return Mathf.Floor(value / sliderIncrement) * sliderIncrement;
        }

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

        #endregion

        private float cachedVolume = float.MaxValue;
        private BaseEventDetails cachedEventData = null;

        #region Events and Change Handlers

        private void ControllableMassChanged(BaseField arg1, object arg2)
        {
            Log($"ControllableMassChanged to {arg1.GetValue(this)} from {arg2}");
            if (float.IsNaN(controllableMass))
            {
                Debug.LogError("ProcAvi - ControllableMassChanged tried to set to NAN! Resetting to 0.");
                controllableMass = 0;
            }
            ClampControllableMass();
            SendRemainingVolume();
            RefreshDisplays();
        }

        private void AvionicsConfigChanged(BaseField arg1, object arg2)
        {
            avionicsTechLevel = ProceduralAvionicsTechManager.GetMaxUnlockedTech(avionicsConfigName);
            Log($"AvionicsConfigChanged event to {arg1.GetValue(this)} from {arg2}");
            AvionicsConfigChanged();
        }

        private void AvionicsConfigChanged()
        {
            Log("AvionicsConfigChanged()");
            CurrentProceduralAvionicsConfig = ProceduralAvionicsTechManager.GetProceduralAvionicsConfig(avionicsConfigName);
            Log($"Avionics Config: {avionicsConfigName}.  Tech: {avionicsTechLevel}");
            SetInternalKSPFields();
            ClampControllableMass();
            if (started)
            {
                // Don't fire these if cachedVolume isn't known yet.
                Log("UpdateControllableMassSlider in AvionicsConfigChanged");
                UpdateControllableMassSlider();
                SendRemainingVolume();
            }
            OnConfigurationUpdated();
            RefreshDisplays();
        }

        [KSPEvent]
        public void OnPartVolumeChanged(BaseEventDetails eventData)
        {
            float volume = (float)eventData.Get<double>("newTotalVolume");
            Log($"OnPartVolumeChanged to {volume} from {cachedVolume}");
            if (!started)
            {
                Log("Delaying OnPartVolumeChanged until after Start()");
                cachedEventData = eventData;
                return;
            }
            cachedVolume = volume;
            ClampControllableMass();
            UpdateControllableMassSlider();
            SendRemainingVolume();
            RefreshDisplays();
        }

        private void SendRemainingVolume()
        {
            if (started && cachedVolume < float.MaxValue)
            {
                Events[nameof(OnPartVolumeChanged)].active = false;
                InternalTanksVolume = SphericalTankUtilities.GetSphericalTankVolume(GetAvailableVolume());
                float availVol = GetAvailableVolume();
                Log($"SendRemainingVolume():  Cached Volume: {cachedVolume}. AvionicsVolume: {GetAvionicsVolume()}.  AvailableVolume: {availVol}.  Internal Tanks: {InternalTanksVolume}");
                SendVolumeChangedEvent(InternalTanksVolume);
                Events[nameof(OnPartVolumeChanged)].active = true;
            }
        }

        private float GetAvailableVolume() => Math.Max(Math.Min((cachedVolume - GetAvionicsVolume()) * InternalTanksAvailableVolumeUtilization, cachedVolume * InternalTanksTotalVolumeUtilization), 0);

        public void SendVolumeChangedEvent(double newVolume)
        {
            var data = new BaseEventDetails(BaseEventDetails.Sender.USER);
            data.Set<string>("volName", "Tankage");
            data.Set<double>("newTotalVolume", newVolume);
            part.SendEvent(nameof(OnPartVolumeChanged), data, 0);
        }

        #endregion

        private void SetInternalKSPFields()
        {
            massExponent = CurrentProceduralAvionicsTechNode.massExponent;
            massConstant = CurrentProceduralAvionicsTechNode.massConstant;
            massFactor = CurrentProceduralAvionicsTechNode.massFactor;
            shieldingMassFactor = CurrentProceduralAvionicsTechNode.shieldingMassFactor;
            costExponent = CurrentProceduralAvionicsTechNode.costExponent;
            costConstant = CurrentProceduralAvionicsTechNode.costConstant;
            costFactor = CurrentProceduralAvionicsTechNode.costFactor;
            powerExponent = CurrentProceduralAvionicsTechNode.powerExponent;
            powerConstant = CurrentProceduralAvionicsTechNode.powerConstant;
            powerFactor = CurrentProceduralAvionicsTechNode.powerFactor;
            disabledPowerFactor = CurrentProceduralAvionicsTechNode.disabledPowerFactor;
            avionicsDensity = CurrentProceduralAvionicsTechNode.avionicsDensity;
            hasScienceContainer = CurrentProceduralAvionicsTechNode.hasScienceContainer;
            interplanetary = CurrentProceduralAvionicsTechNode.interplanetary;
        }

        private void RefreshDisplays()
        {
            RefreshPowerDisplay();
            massDisplay = MathUtils.FormatMass(avionicsDensity > 0 ? GetShieldedAvionicsMass() : 0);
            costDisplay = $"{Mathf.Round(avionicsDensity > 0 ? GetAvionicsCost() : 0)}";
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
            if (!hasScienceContainer)
            {
                if (part.FindModuleImplementing<ModuleScienceContainer>() is ModuleScienceContainer module)
                    part.RemoveModule(module);
            }
            Log($"Setting science container to {(hasScienceContainer ? "enabled." : "disabled.")}");
        }

        [KSPField(guiActiveEditor = true, guiName = "Configure"),
        UI_Toggle(enabledText = "Hide GUI", disabledText = "Show GUI"),
        NonSerialized]
        public bool showGUI;

        private Rect windowRect = new Rect(200, Screen.height - 400, 400, 300);

        public void OnGUI()
        {
            if (showGUI)
            {
                windowRect = GUILayout.Window(GetInstanceID(), windowRect, WindowFunction, "Configure Procedural Avionics");
            }
        }

        private int selectedConfigIndex = 0;
        void WindowFunction(int windowID)
        {
            var configNames = ProceduralAvionicsTechManager.GetAvailableConfigs().ToArray();
            selectedConfigIndex = GUILayout.Toolbar(selectedConfigIndex, configNames);
            var guiAvionicsConfigName = configNames[selectedConfigIndex];
            var currentlyDisplayedConfigs = ProceduralAvionicsTechManager.GetProceduralAvionicsConfig(guiAvionicsConfigName);
            foreach (var techNode in currentlyDisplayedConfigs.TechNodes.Values)
            {
                if (!techNode.IsAvailable)
                {
                    continue;
                }
                if (techNode == CurrentProceduralAvionicsTechNode)
                {
                    GUILayout.Label("Current Config: " + techNode.name);
                    GUILayout.Label("Storage Container: " + (techNode.hasScienceContainer ? "Yes" : "No"));
                }
                else
                {
                    var switchedConfig = false;
                    var unlockCost = ProceduralAvionicsTechManager.GetUnlockCost(guiAvionicsConfigName, techNode);
                    if (unlockCost == 0)
                    {
                        if (GUILayout.Button("Switch to " + BuildTechName(techNode)))
                        {
                            switchedConfig = true;
                        }
                    }
                    else if (Funding.Instance.Funds < unlockCost)
                    {
                        GUILayout.Label("Can't afford " + BuildTechName(techNode) + BuildCostString(unlockCost));
                    }
                    else if (GUILayout.Button("Purchase " + BuildTechName(techNode) + BuildCostString(unlockCost)))
                    {
                        switchedConfig = true;
                        if (!HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
                        {
                            switchedConfig = ProceduralAvionicsTechManager.PurchaseConfig(guiAvionicsConfigName, techNode);
                        }
                        if (switchedConfig)
                        {
                            ProceduralAvionicsTechManager.SetMaxUnlockedTech(guiAvionicsConfigName, techNode.name);
                        }

                    }
                    if (switchedConfig)
                    {
                        Log("Configuration window changed, updating part window");
                        SetupConfigNameFields();
                        avionicsTechLevel = techNode.name;
                        CurrentProceduralAvionicsConfig = currentlyDisplayedConfigs;
                        avionicsConfigName = guiAvionicsConfigName;
                        AvionicsConfigChanged();
                    }
                }
            }
            GUILayout.Label(" ");
            if (GUILayout.Button("Close"))
            {
                showGUI = false;
            }

            GUI.DragWindow();
        }

        private string BuildTechName(ProceduralAvionicsTechNode techNode)
        {
            var sbuilder = StringBuilderCache.Acquire();
            sbuilder.Append(techNode.name);
            sbuilder.Append(BuildSasAndScienceString(techNode));

            return sbuilder.ToStringAndRelease();
        }

        private static string BuildSasAndScienceString(ProceduralAvionicsTechNode techNode) => techNode.hasScienceContainer ? " {SC}" : "";

        private string BuildCostString(int cost) =>
            (cost == 0 || HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch) ? string.Empty : $" ({cost:N})";
    }
}
