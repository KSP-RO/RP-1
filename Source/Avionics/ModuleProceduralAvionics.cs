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

        [KSPField(isPersistant = true, guiName = "Contr. Mass", guiActive = false, guiActiveEditor = true, guiUnits = "\u2009t"),
         UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.05f, sigFigs = 3, unit = "\u2009t")]
        public float controllableMass = -1;

        [KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Configuration"), UI_ChooseOption(scene = UI_Scene.Editor)]
        public string avionicsConfigName;
        private string oldAvionicsConfigName;

        [KSPField(isPersistant = true)]
        public string avionicsTechLevel;
        private string oldAvionicsTechLevel;

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

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Avionics Utilization")]
        public string utilizationDisplay;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Power Requirements")]
        public string powerRequirementsDisplay;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Avionics Mass")]
        public string massDisplay;

        [KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Avionics Cost")]
        public string costDisplay;

        public bool IsScienceCore => massExponent == 0 && powerExponent == 0 && costExponent == 0;

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

        protected override float GetInternalMassLimit()
        {
            if(IsScienceCore)
            {
                return 0;
            }

            var oldLimit = controllableMass;
            ClampControllableMass();
            if (controllableMass != oldLimit)
            {
                Log("WARNING: LIMIT WAS RESET IN GET");
            }
            return controllableMass;
        }

        private void ClampControllableMass()
        {
            var maxControllableMass = GetMaximumControllableMass();
            if (controllableMass > maxControllableMass * FloatTolerance)
            {
                Log("Resetting procedural mass limit to max of ", maxControllableMass, ", was ", controllableMass);
                controllableMass = maxControllableMass;
                RefreshPartWindow();
            }
        }

        private float GetMaximumControllableMass() => FloorToSliderIncrement(GetControllableMass(MaxAvionicsMass));

        private float GetControllableMass(float avionicsMass) => GetInversePolynomial(avionicsMass * 1000, massExponent, massConstant, massFactor);

        private float GetShieldedAvionicsMass()
        {
            var avionicsMass = GetAvionicsMass();
            return avionicsMass + GetShieldingMass(avionicsMass);
        }

        private float GetAvionicsMass() => GetPolynomial(GetInternalMassLimit(), massExponent, massConstant, massFactor) / 1000f;

        private float GetShieldingMass(float avionicsMass) => Mathf.Pow(avionicsMass, 2f / 3) * shieldingMassFactor;

        private float GetAvionicsCost() => GetPolynomial(GetInternalMassLimit(), costExponent, costConstant, costFactor);

        protected override float GetEnabledkW() => GetPolynomial(GetInternalMassLimit(), powerExponent, powerConstant, powerFactor) / 1000f;

        private static float GetPolynomial(float value, float exponent, float constant, float factor) => (Mathf.Pow(value, exponent) + constant) * factor;

        private static float GetInversePolynomial(float value, float exponent, float constant, float factor) => Mathf.Pow(value / factor - constant, 1 / exponent);

        protected override float GetDisabledkW() => GetEnabledkW() * disabledPowerFactor;

        protected override bool GetToggleable() => disabledPowerFactor > 0;

        protected override string GetTonnageString() => "This part can be configured to allow control of vessels up to any mass.";

        private UI_FloatEdit controllableMassEdit;

        public override void OnLoad(ConfigNode node)
        {
            try
            {
                Log("OnLoad called");
                if (GameSceneFilter.Loading.IsLoaded())
                {
                    Log("Loading Avionics Configs");
                    ProceduralAvionicsTechManager.LoadAvionicsConfigs(node);
                }
            }
            catch (Exception ex)
            {
                Log("OnLoad exception: ", ex.Message);
                throw;
            }
        }

        private bool started = false;
        public new void Start()
        {
            SetFallbackConfigForLegacyCraft();
            UpdateConfigSliders();
            BindUIChangeCallbacks();
            SetControllableMassForLegacyCraft();
            AvionicsConfigChanged();
            InjectCachedEventData();
            base.Start();
            SetScienceContainerIfNeeded();
            started = true;
            Log("Start finished");
        }

        private void SetScienceContainerIfNeeded()
        {
            if (!HighLogic.LoadedSceneIsEditor)
            {
                SetScienceContainer();
            }
        }

        private void InjectCachedEventData()
        {
            if (cachedEventData != null)
            {
                OnPartVolumeChanged(cachedEventData);
            }
        }

        private void SetFallbackConfigForLegacyCraft()
        {
            if (HighLogic.LoadedSceneIsEditor && !ProceduralAvionicsTechManager.GetAvailableConfigs().Contains(avionicsConfigName))
            {
                Log($"No valid config set ({avionicsConfigName})");
                avionicsConfigName = ProceduralAvionicsTechManager.GetPurchasedConfigs().First();
            }
            if (string.IsNullOrEmpty(avionicsTechLevel))
            {
                avionicsTechLevel = ProceduralAvionicsTechManager.GetMaxUnlockedTech(avionicsConfigName);
                Log("No tech level set, using ", avionicsTechLevel);
            }
        }

        private void SetControllableMassForLegacyCraft()
        {
            if (controllableMass < 0)
            {
                controllableMass = HighLogic.LoadedSceneIsFlight ? float.MaxValue : 0;
            }
        }

        private bool callbacksBound = false;
        private void BindUIChangeCallbacks()
        {
            if (!callbacksBound)
            {
                Fields[nameof(controllableMass)].uiControlEditor.onFieldChanged += ControllableMassChanged;
                Fields[nameof(avionicsConfigName)].uiControlEditor.onFieldChanged += AvionicsConfigChanged;
                callbacksBound = true;
            }
        }

        private void ControllableMassChanged(BaseField arg1, object arg2)
        {
            Log("Mass limit changed");
            ClampControllableMass();
            SetMinVolume();
            SendRemainingVolume();
            RefreshDisplays();
        }

        private void AvionicsConfigChanged(BaseField arg1, object arg2)
        {
            avionicsTechLevel = ProceduralAvionicsTechManager.GetMaxUnlockedTech(avionicsConfigName);

            AvionicsConfigChanged();
        }

        private void AvionicsConfigChanged()
        {
            if (avionicsConfigName == oldAvionicsConfigName && avionicsTechLevel == oldAvionicsTechLevel)
            {
                return;
            }
            Log("Setting config to ", avionicsConfigName);
            CurrentProceduralAvionicsConfig = ProceduralAvionicsTechManager.GetProceduralAvionicsConfig(avionicsConfigName);
            Log("Setting tech node to ", avionicsTechLevel);
            oldAvionicsConfigName = avionicsConfigName;
            oldAvionicsTechLevel = avionicsTechLevel;
            SetInternalKSPFields();
            ClampControllableMass();
            SetMinVolume(true);
            UpdateControllableMassSlider();
            SendRemainingVolume();
            OnConfigurationUpdated();
            RefreshDisplays();
        }


        private float cachedMinVolume = float.MaxValue;
        public void SetMinVolume(bool forceUpdate = false)
        {
            Log("Setting min volume for proceduralMassLimit of ", controllableMass);
            float minVolume = GetAvionicsMass() / avionicsDensity * FloatTolerance;
            if (float.IsNaN(minVolume))
            {
                return;
            }
            Log("min volume should be ", minVolume);
            cachedMinVolume = minVolume;

            PartModule ppModule = null;
            Type ppModuleType = null;
            foreach (var module in part.Modules)
            {
                var moduleType = module.GetType();
                if (moduleType.FullName == "ProceduralParts.ProceduralPart")
                {
                    ppModule = module;
                    ppModuleType = moduleType;
                    ppModuleType.GetField("volumeMin").SetValue(ppModule, minVolume);
                    Log("Applied min volume");
                }
            }
            Log("minVolume: ", minVolume);
            Log("Comparing against cached volume of ", cachedVolume);
            if (forceUpdate || minVolume > cachedVolume)
            {
                if (!forceUpdate)
                {
                    Log("cachedVolume too low: ", cachedVolume);
                }
                if (ppModule != null)
                {
                    var reflectedShape = ppModuleType.GetProperty("CurrentShape").GetValue(ppModule, null);
                    reflectedShape.GetType().GetMethod("ForceNextUpdate").Invoke(reflectedShape, new object[] { });
                    Log("Volume fixed, refreshing part window");
                }
                RefreshPartWindow();
            }
        }

        public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) => GetMassSafely();
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.FIXED;
        public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) => GetCostSafely();
        public ModifierChangeWhen GetModuleCostChangeWhen() => ModifierChangeWhen.FIXED;

        private float GetMassSafely()
        {
            return avionicsDensity > 0 ? GetShieldedAvionicsMass() : 0;
        }

        private float GetCostSafely()
        {
            return avionicsDensity > 0 ? GetAvionicsCost() : 0;
        }

        private void UpdateControllableMassSlider()
        {
            if (controllableMassEdit == null)
            {
                controllableMassEdit = (UI_FloatEdit)Fields[nameof(controllableMass)].uiControlEditor;
            }

            if (CurrentProceduralAvionicsConfig != null && CurrentProceduralAvionicsTechNode != null)
            {
                if (IsScienceCore)
                {
                    Fields[nameof(controllableMass)].guiActiveEditor = false;
                }
                else
                {
                    ConfigureControllableMassSliderForRegularAvionics();
                }
            }
            else
            {
                Log("WARNING: Cannot update max value yet, CurrentProceduralAvionicsConfig is null");
            }
        }

        private void ConfigureControllableMassSliderForRegularAvionics()
        {
            Fields[nameof(controllableMass)].guiActiveEditor = true;

            controllableMassEdit.maxValue = CeilingToSmallIncrement(GetMaximumControllableMass());
            controllableMassEdit.minValue = 0;

            controllableMassEdit.incrementSmall = GetSmallIncrement(controllableMassEdit.maxValue);
            controllableMassEdit.incrementLarge = controllableMassEdit.incrementSmall * 10;
            controllableMassEdit.incrementSlide = GetSliderIncrement(controllableMassEdit.maxValue);
            controllableMassEdit.sigFigs = GetSigFigs(controllableMassEdit.maxValue);
        }

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

        private void UpdateConfigSliders()
        {
            Log("Updating Config Slider");
            var avionicsConfigField = Fields[nameof(avionicsConfigName)];
            avionicsConfigField.guiActiveEditor = true;
            var range = (UI_ChooseOption)avionicsConfigField.uiControlEditor;
            range.options = ProceduralAvionicsTechManager.GetPurchasedConfigs().ToArray();

            if (string.IsNullOrEmpty(avionicsConfigName))
            {
                avionicsConfigName = range.options[0];
                Log("Defaulted config to ", avionicsConfigName);
            }
        }

        private float cachedVolume = float.MaxValue;
        private BaseEventDetails cachedEventData = null;

        [KSPEvent]
        public void OnPartVolumeChanged(BaseEventDetails eventData)
        {
            Log("OnPartVolumeChanged called");
            if (!started)
            {
                Log("Not yet started, returning");
                cachedEventData = eventData;
                return;
            }
            try
            {
                float volume = (float)eventData.Get<double>("newTotalVolume");
                Log("volume changed to ", volume);
                if (volume * FloatTolerance < cachedMinVolume && cachedMinVolume != float.MaxValue)
                {
                    Log("volume of ", volume, " is less than expected min volume of ", cachedMinVolume, " expecting another update");
                    RefreshPartWindow();
                    //assuming the part will be resized
                    return;
                }
                Log("setting cachedVolume to ", volume);
                cachedVolume = volume;
                SendRemainingVolume();
                UpdateControllableMassSlider();
                RefreshDisplays();
            }
            catch (Exception ex)
            {
                Log("error getting changed volume: ", ex);
            }
        }

        private void SendRemainingVolume()
        {
            if (cachedVolume == float.MaxValue)
            {
                return;
            }
            Events[nameof(OnPartVolumeChanged)].active = false;
            InternalTanksVolume = SphericalTankUtilities.GetSphericalTankVolume(GetAvailableVolume());
            SendVolumeChangedEvent(InternalTanksVolume);
            Events[nameof(OnPartVolumeChanged)].active = true;
        }

        private float GetAvailableVolume() => Math.Max(Math.Min((cachedVolume - GetAvionicsVolume()) * InternalTanksAvailableVolumeUtilization, cachedVolume * InternalTanksTotalVolumeUtilization), 0);
        private float GetAvionicsVolume() => GetAvionicsMass() / avionicsDensity;

        public void SendVolumeChangedEvent(double newVolume)
        {
            var data = new BaseEventDetails(BaseEventDetails.Sender.USER);
            data.Set<string>("volName", "Tankage");
            data.Set<double>("newTotalVolume", newVolume);
            part.SendEvent(nameof(OnPartVolumeChanged), data, 0);
        }

        private void SetInternalKSPFields()
        {
            Log("Setting internal KSP fields");
            Log("avionics tech level: ", avionicsTechLevel);

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
            RefreshCostAndMassDisplays();

            utilizationDisplay = String.Format("{0:0.#}%", Utilization * 100);
            Log("Utilization display: ", utilizationDisplay);

            RefreshPowerDisplay();
        }

        private void RefreshPowerDisplay()
        {
            var powerConsumptionBuilder = StringBuilderCache.Acquire();
            double kW = GetEnabledkW();
            if (kW >= 1)
            {
                powerConsumptionBuilder.AppendFormat(KwFormat, kW).Append("\u2009kW");
            }
            else
            {
                powerConsumptionBuilder.AppendFormat(WFormat, kW * 1000).Append("\u2009W");
            }
            double dkW = GetDisabledkW();
            if (dkW > 0)
            {
                powerConsumptionBuilder.Append(" /");
                if (dkW >= 0.1)
                {
                    powerConsumptionBuilder.AppendFormat(KwFormat, dkW).Append("\u2009kW");
                }
                else
                {
                    powerConsumptionBuilder.AppendFormat(WFormat, dkW * 1000).Append("\u2009W");
                }
            }

            powerRequirementsDisplay = powerConsumptionBuilder.ToStringAndRelease();
        }

        private void SetScienceContainer()
        {
            if (!hasScienceContainer)
            {
                var module = part.FindModuleImplementing<ModuleScienceContainer>();
                if (module != null)
                {
                    part.RemoveModule(module);
                }
            }
            Log("Setting science container to ", hasScienceContainer ? "enabled." : "disabled.");
        }

        private void RefreshCostAndMassDisplays()
        {
            massDisplay = MathUtils.FormatMass(GetMassSafely());
            costDisplay = Mathf.Round(GetCostSafely()).ToString();
        }

        [KSPField(isPersistant = false, guiActiveEditor = true, guiActive = false, guiName = "Configure"),
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
                        UpdateConfigSliders();
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

        private string BuildCostString(int cost)
        {
            if (cost == 0 || HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch)
            {
                return string.Empty;
            }
            return " (" + string.Format("{0:N}", cost) + ")";
        }

        private void RefreshPartWindow()
        {
            UIPartActionWindow[] partWins = FindObjectsOfType<UIPartActionWindow>();
            foreach (var partWin in partWins)
            {
                partWin.displayDirty = true;
            }
        }
    }
}
