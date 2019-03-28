using KSPAPIExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

using static RP0.ProceduralAvionics.ProceduralAvionicsUtils;

namespace RP0.ProceduralAvionics
{
	class ModuleProceduralAvionics : ModuleAvionics, IPartMassModifier, IPartCostModifier
	{

		#region KSPFields, overrides, and class variables

		const string kwFormat = "{0:0.##}";
		const string wFormat = "{0:0}";
        const float FLOAT_TOLERANCE = 1.00001f;
        const float FLOAT_TOLERANCE2 = 1.001f;

        // This controls how much the current part can control (in metric tons)
        [KSPField(isPersistant = true, guiName = "Tonnage", guiActive = false, guiActiveEditor = true, guiUnits = "\u2009t"),
		 UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.05f, sigFigs = 3, unit = "\u2009t")]
		public float proceduralMassLimit = 0;

		// We can have multiple configurations of avionics, for example: 
		//    boosters can have a high EC usage, but lower cost and mass
		//    probeCores can have a higher mass, but be very power efficient (and can even be turned off)
		[KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Configuration"), UI_ChooseOption(scene = UI_Scene.Editor)]
		public string avionicsConfigName;
		private string oldAvionicsConfigName;

		[KSPField(isPersistant = true)]
		public string avionicsTechLevel;
        private string oldAvionicsTechLevel;

		[KSPField(isPersistant = true)]
		public float maxDensityOfAvionics;

		[KSPField(isPersistant = true)]
		public float tonnageToMassRatio;

		[KSPField(isPersistant = true)]
		public float costPerControlledTon;

		[KSPField(isPersistant = true)]
		public float enabledkWPerTon;

		[KSPField(isPersistant = true)]
		public float disabledkWPerTon;

		[KSPField(isPersistant = true)]
		public float minimumTonnage;

		[KSPField(isPersistant = true)]
		public float maximumTonnage;

		[KSPField(isPersistant = true)]
		public int SASServiceLevel = -1;

		[KSPField(isPersistant = true)]
		public bool hasScienceContainer = false;

		[KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Percent Utilization")]
		public string utilizationDisplay;

		[KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Power Requirements")]
		public string powerRequirementsDisplay;

		[KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Mass")]
		public string massDisplay;

		[KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Cost")]
		public string costDisplay;

		// The currently selected avionics config
		public ProceduralAvionicsConfig CurrentProceduralAvionicsConfig {
			get { return currentProceduralAvionicsConfig; }
		}

		public ProceduralAvionicsTechNode CurrentProceduralAvionicsTechNode {
			get {
				if (CurrentProceduralAvionicsConfig != null && avionicsTechLevel != null && CurrentProceduralAvionicsConfig.TechNodes.ContainsKey(avionicsTechLevel))
				{
					return CurrentProceduralAvionicsConfig.TechNodes[avionicsTechLevel];
				}
				return null;
			}
		}

		private float GetCurrentDensity()
		{
			return maxDensityOfAvionics;
		}

		protected override float GetInternalMassLimit()
		{
            var oldLimit = proceduralMassLimit;
            ClampInternalMassLimit();
            if(proceduralMassLimit != oldLimit)
            {
                Log("WARNING: LIMIT WAS RESET IN GET");
            }
            return proceduralMassLimit;
		}

        private void ClampInternalMassLimit()
        {
            var max = GetMaximumControllableTonnage();
            if (max == 0)
            {
                //Sounds like we're not yet initiaziled, let's not change anything
                //Log("no max");
                return;
            }
            var min = GetMinimumControllableTonnage();

            bool changed = false;
            //if (max < min)
            //{
            //    if (proceduralMassLimit > 0)
            //    {
            //        proceduralMassLimit = 0;
            //        changed = true;
            //    }
            //}
            //else
            //{
                if (proceduralMassLimit > max * FLOAT_TOLERANCE)
                {
                    Log("Resetting procedural mass limit to max of ", max, ", was ", proceduralMassLimit);
                    proceduralMassLimit = max;
                    changed = true;
                }
                if (proceduralMassLimit * FLOAT_TOLERANCE < min)
                {
                    Log("Resetting procedural mass limit to min of ", min, ", was ", proceduralMassLimit);
                    proceduralMassLimit = min;
                    changed = true;
                }
            //}
            if (changed)
            {
                RefreshPartWindow();
            }
        }

		protected override float GetEnabledkW()
		{
			return enabledkWPerTon * GetInternalMassLimit();
		}

		protected override float GetDisabledkW()
		{
			return disabledkWPerTon * GetInternalMassLimit();
		}

		protected override bool GetToggleable()
		{
			return disabledkWPerTon > 0;
		}

		protected override string GetTonnageString()
		{
			return "This part can be configured to allow control of vessels up to any mass.";
		}

		private ProceduralAvionicsConfig currentProceduralAvionicsConfig;
		private UI_FloatEdit proceduralMassLimitEdit;

		#endregion

		#region event handlers
		public override void OnLoad(ConfigNode node)
		{
			try {
				Log("OnLoad called");
				if (GameSceneFilter.Loading.IsLoaded()) {
					Log("Loading Avionics Configs");
					ProceduralAvionicsTechManager.LoadAvionicsConfigs(node);
				}
			}
			catch (Exception ex) {
				Log("OnLoad exception: ", ex.Message);
				throw;
			}
		}

		private bool started = false;
		public new void Start()
		{
			Log("Start called");
			started = true;

			string config = ProceduralAvionicsTechManager.GetPurchasedConfigs()[0];
			Log("Default config to use: ", config);

			if (String.IsNullOrEmpty(avionicsTechLevel)) {
				avionicsTechLevel = ProceduralAvionicsTechManager.GetMaxUnlockedTech(
					String.IsNullOrEmpty(avionicsConfigName) ? config : avionicsConfigName);
				Log("No tech level set, using ", avionicsTechLevel);
			}

			if (String.IsNullOrEmpty(avionicsConfigName)) {
				Log("No config set, using ", config);
				avionicsConfigName = config;
			}

			UpdateConfigSliders();
			BindUIChangeCallbacks();

            AvionicsConfigChanged();

			if (cachedEventData != null) {
				OnPartVolumeChanged(cachedEventData);
			}

			base.Start();
			Log("Start finished");
		}

		private bool callbacksBound = false;
		private void BindUIChangeCallbacks()
		{
			if (!callbacksBound) {
				Fields["proceduralMassLimit"].uiControlEditor.onFieldChanged += MassLimitChanged;
				Fields["avionicsConfigName"].uiControlEditor.onFieldChanged += AvionicsConfigChanged;
				callbacksBound = true;
			}
		}

		private void MassLimitChanged(BaseField arg1, object arg2)
		{
			Log("Mass limit changed");
            ClampInternalMassLimit();
            SetMinVolume();
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
            currentProceduralAvionicsConfig = ProceduralAvionicsTechManager.GetProceduralAvionicsConfig(avionicsConfigName);
            Log("Setting tech node to ", avionicsTechLevel);
            oldAvionicsConfigName = avionicsConfigName;
            oldAvionicsTechLevel = avionicsTechLevel;
            SetInternalKSPFields();
            ResetTo100();
            ClampInternalMassLimit();
            SetMinVolume(true);
            UpdateMaxValues();
            OnConfigurationUpdated();
            RefreshDisplays();
		}


		private float cachedMinVolume = float.MaxValue;
		public void SetMinVolume(bool forceUpdate = false)
		{
			Log("Setting min volume for proceduralMassLimit of ", proceduralMassLimit);
			float minVolume = proceduralMassLimit / (tonnageToMassRatio * maxDensityOfAvionics) * FLOAT_TOLERANCE2;
			if (float.IsNaN(minVolume)) {
				return;
			}
			Log("min volume should be ", minVolume);
			cachedMinVolume = minVolume;

			PartModule ppModule = null;
			Type ppModuleType = null;
			foreach (PartModule module in part.Modules) {
				var moduleType = module.GetType();
				if (moduleType.FullName == "ProceduralParts.ProceduralPart") {
					ppModule = module;
					ppModuleType = moduleType;
					ppModuleType.GetField("volumeMin").SetValue(ppModule, minVolume);
					Log("Applied min volume");
				}
			}
			Log("minVolume: ", minVolume);
			Log("Comparing against cached volume of ", cachedVolume);
			if (forceUpdate || minVolume > cachedVolume) { // adding a buffer for floating point errors
				if (!forceUpdate) {
					Log("cachedVolume too low: ", cachedVolume);
				}
				//here we'll need to use reflection to update our part to have a min volume
				if (ppModule != null) {
					var reflectedShape = ppModuleType.GetProperty("CurrentShape").GetValue(ppModule, null);
					reflectedShape.GetType().GetMethod("ForceNextUpdate").Invoke(reflectedShape, new object[] { });
					Log("Volume fixed, refreshing part window");
				}
				RefreshPartWindow();
			}
		}

		public void FixedUpdate()
		{
			if (!HighLogic.LoadedSceneIsEditor) {
				SetSASServiceLevel();
				SetScienceContainer();
			}
		}

		public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
		{
			return CalculateNewMass();
		}

		public ModifierChangeWhen GetModuleMassChangeWhen()
		{
			return ModifierChangeWhen.FIXED;
		}

		public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
		{
			return CalculateCost();
		}

		public ModifierChangeWhen GetModuleCostChangeWhen()
		{
			return ModifierChangeWhen.FIXED;
		}

		#endregion


		#region part attribute calculations
		private float CalculateNewMass()
		{
			if (HighLogic.LoadedSceneIsFlight || maxDensityOfAvionics > 0) {
				return DoMassCalculation();
			}
			if (CurrentProceduralAvionicsConfig != null && CurrentProceduralAvionicsTechNode != null) {
                Log("Not yet initialized but getmass called!?");
                SetInternalKSPFields();
				return DoMassCalculation();
			}
			else {
				return 0;
			}
		}

		private float DoMassCalculation()
		{
			float controllablePercentage = GetControllableUtilizationPercentage();
			float massPercentage = (-1 * controllablePercentage * controllablePercentage) + (2 * controllablePercentage);
			float result = massPercentage * cachedVolume * GetCurrentDensity();
			return result;
		}

		private float CalculateCost()
		{
			if (HighLogic.LoadedSceneIsFlight) {
				return DoCostCalculation();
			}
			if (CurrentProceduralAvionicsConfig != null && CurrentProceduralAvionicsTechNode != null) {
				costPerControlledTon = CurrentProceduralAvionicsTechNode.costPerControlledTon;
				return DoCostCalculation();
			}
			else {
				return 0;
			}
		}

		private float DoCostCalculation()
		{
			float controllablePercentage = GetControllableUtilizationPercentage();
			float costPercentage = controllablePercentage * controllablePercentage;
			return costPerControlledTon * GetInternalMassLimit();
		}

		#endregion

		#region private utiliy functions
		private float GetControllableUtilizationPercentage()
		{
			return GetInternalMassLimit() / (cachedVolume * maxDensityOfAvionics * tonnageToMassRatio);
		}

		private float GetMaximumControllableTonnage()
		{
			var maxAvionicsMass = cachedVolume * maxDensityOfAvionics;
            //Log("max for volume before trunc: ", maxAvionicsMass * tonnageToMassRatio * 2);
            var maxForVolume = FloorToSliderIncrement(maxAvionicsMass * tonnageToMassRatio);
			return Math.Min(maxForVolume, maximumTonnage);
		}

        private float GetMinimumControllableTonnage()
		{
			if (CurrentProceduralAvionicsTechNode != null) {
				return CurrentProceduralAvionicsTechNode.minimumTonnage;
			}
			return minimumTonnage;
		}

		private void ResetTo100()
		{
            if(cachedVolume == float.MaxValue)
            {
                return;
            }
            proceduralMassLimit = cachedVolume * maxDensityOfAvionics * tonnageToMassRatio;
			Log("100% utilization calculated as ", proceduralMassLimit, " from ", cachedVolume, " ", maxDensityOfAvionics, " ", tonnageToMassRatio);
		}

		private void UpdateMaxValues()
		{
			// update mass limit value slider

			if (proceduralMassLimitEdit == null) {
				proceduralMassLimitEdit = (UI_FloatEdit)Fields["proceduralMassLimit"].uiControlEditor;
			}

            if (CurrentProceduralAvionicsConfig != null && CurrentProceduralAvionicsTechNode != null)
            {

                tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
                proceduralMassLimitEdit.maxValue = CeilingToSmallIncrement(GetMaximumControllableTonnage());
                proceduralMassLimitEdit.minValue = 0;

                var procMassDelta = proceduralMassLimitEdit.maxValue - proceduralMassLimitEdit.minValue;

                proceduralMassLimitEdit.incrementSmall = GetSmallIncrement(procMassDelta);
                proceduralMassLimitEdit.incrementLarge = proceduralMassLimitEdit.incrementSmall * 10;
                proceduralMassLimitEdit.incrementSlide = GetSliderIncrement(procMassDelta);
                proceduralMassLimitEdit.sigFigs = GetSigFigs(procMassDelta);
            }
            else
            {
                Log("Cannot update max value yet, CurrentProceduralAvionicsConfig is null");
                proceduralMassLimitEdit.maxValue = float.MaxValue;
                proceduralMassLimitEdit.minValue = 0;
            }
		}

        private int GetSigFigs(float value)
        {
            var smallIncrementExponent = GetSmallIncrementExponent(value);
            return Math.Max(1 - (int)smallIncrementExponent, 0);
        }

        private float CeilingToSmallIncrement(float value)
        {
            var smallIncrement = GetSmallIncrement(value);
            return (float) Math.Ceiling(value / smallIncrement) * smallIncrement;
        }

        private float FloorToSliderIncrement(float value)
        {
            float sliderIncrement = GetSliderIncrement(value);
            return (float) Math.Floor(value / sliderIncrement) * sliderIncrement;
        }

        private float GetSliderIncrement(float value)
        {
            var smallIncrement = GetSmallIncrement(value);
            return Math.Min(smallIncrement / 10, 1f);
        }

        private float GetSmallIncrement(float value)
        {
            var exponent = GetSmallIncrementExponent(value);
            return (float) Math.Pow(10, exponent);
        }

        private double GetSmallIncrementExponent(float procMassDelta)
        {
            var log = Math.Log(procMassDelta, 10);
            return Math.Max(Math.Floor(log - 1.3), -2);
        }

        private void UpdateConfigSliders()
		{
			Log("Updating Config Slider");
			BaseField avionicsConfigField = Fields["avionicsConfigName"];
			avionicsConfigField.guiActiveEditor = true;
			UI_ChooseOption range = (UI_ChooseOption)avionicsConfigField.uiControlEditor;
			range.options = ProceduralAvionicsTechManager.GetPurchasedConfigs().ToArray();

			if (String.IsNullOrEmpty(avionicsConfigName)) {
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
			if (!started) {
				Log("Not yet started, returning");
				cachedEventData = eventData;
				return;
			}
			try {
				float volume = (float)eventData.Get<double>("newTotalVolume");
				Log("volume changed to ", volume);
				if (volume * FLOAT_TOLERANCE < cachedMinVolume && cachedMinVolume != float.MaxValue) {
					Log("volume of ", volume, " is less than expected min volume of ", cachedMinVolume, " expecting another update");
					RefreshPartWindow();
					//assuming the part will be resized
					return;
				}
				Log("setting cachedVolume to ", volume);
				cachedVolume = volume;
                //Log("cached total volume set from eventData: ", cachedVolume);
                UpdateMaxValues();
                RefreshDisplays();
            }
			catch (Exception ex) {
				Log("error getting changed volume: ", ex);
			}
		}

		private void SetInternalKSPFields()
        {
            Log("Setting internal KSP fields");
            Log("avionics tech level: ", avionicsTechLevel);

            tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
            maxDensityOfAvionics = CurrentProceduralAvionicsTechNode.standardAvionicsDensity;
            costPerControlledTon = CurrentProceduralAvionicsTechNode.costPerControlledTon;
            enabledkWPerTon = CurrentProceduralAvionicsTechNode.enabledkWPerTon;
            disabledkWPerTon = CurrentProceduralAvionicsTechNode.disabledkWPerTon;

            minimumTonnage = CurrentProceduralAvionicsTechNode.minimumTonnage;
            maximumTonnage = CurrentProceduralAvionicsTechNode.maximumTonnage;

            SASServiceLevel = CurrentProceduralAvionicsTechNode.SASServiceLevel;
            hasScienceContainer = CurrentProceduralAvionicsTechNode.hasScienceContainer;
            interplanetary = CurrentProceduralAvionicsTechNode.interplanetary;
        }

        private void RefreshDisplays()
        {
            RefreshCostAndMassDisplays();

            utilizationDisplay = String.Format("{0:0.#}%", GetControllableUtilizationPercentage() * 100);
            Log("Utilization display: ", utilizationDisplay);

            RefreshPowerDisplay();
        }

        private void RefreshPowerDisplay()
        {
            StringBuilder powerConsumptionBuilder = StringBuilderCache.Acquire();
            Log("Getting power reqs: total tons", GetInternalMassLimit(), " ", enabledkWPerTon, " ", disabledkWPerTon);
            double kW = GetEnabledkW();
            if (kW >= 0.1)
            {
                powerConsumptionBuilder.AppendFormat(kwFormat, kW).Append(" kW");
            }
            else
            {
                powerConsumptionBuilder.AppendFormat(wFormat, kW * 1000).Append(" W");
            }
            double dkW = GetDisabledkW();
            if (dkW > 0)
            {
                powerConsumptionBuilder.Append(" /");
                if (dkW >= 0.1)
                {
                    powerConsumptionBuilder.AppendFormat(kwFormat, dkW).Append(" kW");
                }
                else
                {
                    powerConsumptionBuilder.AppendFormat(wFormat, dkW * 1000).Append(" W");
                }
            }

            powerRequirementsDisplay = powerConsumptionBuilder.ToStringAndRelease();
        }

        // creating a field for this so we don't need to look it up every update
        private ModuleSAS sasModule = null;
		private void SetSASServiceLevel()
		{
			if (SASServiceLevel >= 0) {
				if (sasModule == null) {
					sasModule = part.FindModuleImplementing<ModuleSAS>();
				}
				if (sasModule != null) {
					if (sasModule.SASServiceLevel != SASServiceLevel) {
						sasModule.SASServiceLevel = SASServiceLevel;
						Log("Setting SAS service level to ", SASServiceLevel);
					}
				}
			}
		}

		private bool scienceContainerFiltered = false;
		private void SetScienceContainer()
		{
			if (scienceContainerFiltered) {
				return;
			}
			if (!hasScienceContainer) {
				var module = part.FindModuleImplementing<ModuleScienceContainer>();
				if (module != null) {
					part.RemoveModule(module);
				}
			}
			Log("Setting science container to ", (hasScienceContainer ? "enabled." : "disabled."));
			scienceContainerFiltered = true;
		}

		bool ppFieldsHidden = false;
		string TCSmoduleName = "TankContentSwitcher";
		string PPModuleName = "ProceduralPart";

		private void RefreshCostAndMassDisplays()
		{
			if (!ppFieldsHidden) {
				ppFieldsHidden = HideField(TCSmoduleName, "massDisplay") && HideField(TCSmoduleName, "volumeDisplay");
			}

			float baseCost = GetBaseCost();
			float baseMass = GetBaseMass();
			massDisplay = MathUtils.FormatMass(baseMass + CalculateNewMass());
			costDisplay = (baseCost + CalculateCost()).ToString();
		}

		private bool HideField(string moduleName, string fieldName)
		{
			var field = GetBaseField(moduleName, fieldName);
			if (field == null) {
				Log("Field ", fieldName, " not found");
				return false;
			}
			field.guiActive = false;
			field.guiActiveEditor = false;
			return true;
		}

		private BaseField GetBaseField(string moduleName, string fieldName)
		{
			PartModule module = this;
			if (!String.IsNullOrEmpty(moduleName)) {
				module = part.Modules[moduleName];
				if (module == null) {
					Log("Module ", moduleName, " not found");
				}
			}
			return module.Fields[fieldName];
		}

		// Base cost comes from ProceduralPart
		private float GetBaseCost()
		{
			var ppModule = part.Modules[PPModuleName];
			if (ppModule != null) {
				var ppMassModule = (IPartCostModifier)ppModule;
				return ppMassModule.GetModuleCost(0, ModifierStagingSituation.CURRENT);
			}
			else {
				Log("Module ", PPModuleName, " not found");
			}
			return 0;
		}

		// Base mass comes from TankContentSwitcher
		private float GetBaseMass()
		{
			var tcsModule = part.Modules[TCSmoduleName];
			if (tcsModule != null) {
				var tcsMassModule = (IPartMassModifier)tcsModule;
				return tcsMassModule.GetModuleMass(0, ModifierStagingSituation.CURRENT);
			}
			else {
				Log("Module ", TCSmoduleName, " not found");
			}
			return 0;
		}

		#endregion

		#region Config GUI
		[KSPField(isPersistant = false, guiActiveEditor = true, guiActive = false, guiName = "Configure"),
		UI_Toggle(enabledText = "Hide GUI", disabledText = "Show GUI"),
		NonSerialized]
		public bool showGUI;

		private Rect windowRect = new Rect(200, Screen.height - 400, 400, 300);

		public void OnGUI()
		{
			if (showGUI) {
				windowRect = GUILayout.Window(GetInstanceID(), windowRect, WindowFunction, "Configure Procedural Avionics");
			}
		}

		private int selectedConfigIndex = 0;
		void WindowFunction(int windowID)
		{
			string[] configNames = ProceduralAvionicsTechManager.GetAvailableConfigs().ToArray();

			selectedConfigIndex = GUILayout.Toolbar(
				selectedConfigIndex,
				configNames);

			string guiAvionicsConfigName = configNames[selectedConfigIndex];

			ProceduralAvionicsConfig currentlyDisplayedConfigs =
				ProceduralAvionicsTechManager.GetProceduralAvionicsConfig(guiAvionicsConfigName);
			foreach (ProceduralAvionicsTechNode techNode in currentlyDisplayedConfigs.TechNodes.Values) {
				if (!techNode.IsAvailable) {
					continue;
				}
				if (techNode == CurrentProceduralAvionicsTechNode) {
					GUILayout.Label("Current Config: " + techNode.name);
					GUILayout.Label("Supported Tonnage: " + BuildTonnageString(techNode));
					GUILayout.Label("SAS Level: " + techNode.SASServiceLevel.ToString());
					GUILayout.Label("Storage Container: " + (techNode.hasScienceContainer ? "Yes" : "No"));
				}
				else {
					bool switchedConfig = false;
					int unlockCost = ProceduralAvionicsTechManager.GetUnlockCost(guiAvionicsConfigName, techNode);
					if (unlockCost == 0) {
						if (GUILayout.Button("Switch to " + BuildTechName(techNode))) {
							switchedConfig = true;
						}
					}
					else if (Funding.Instance.Funds < unlockCost) {
						GUILayout.Label("Can't afford " + BuildTechName(techNode) + BuildCostString(unlockCost));
					}
					else if (GUILayout.Button("Purchase " + BuildTechName(techNode) + BuildCostString(unlockCost))) {
						switchedConfig = true;
						if (!HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch) {
							switchedConfig = ProceduralAvionicsTechManager.PurchaseConfig(guiAvionicsConfigName, techNode);
						}
						if (switchedConfig) {
							ProceduralAvionicsTechManager.SetMaxUnlockedTech(guiAvionicsConfigName, techNode.name);
						}

					}
					if (switchedConfig) {
						Log("Configuration window changed, updating part window");
						UpdateConfigSliders();
						avionicsTechLevel = techNode.name;
						currentProceduralAvionicsConfig = currentlyDisplayedConfigs;
						avionicsConfigName = guiAvionicsConfigName;
						AvionicsConfigChanged();
                    }
				}
			}
			GUILayout.Label(" "); // blank space
			if (GUILayout.Button("Reset to 100%")) {
				ResetTo100();
				RefreshPartWindow();
			}
			if (GUILayout.Button("Close")) {
				showGUI = false;
			}

			GUI.DragWindow();
		}

		private string BuildTechName(ProceduralAvionicsTechNode techNode)
		{
			StringBuilder sbuilder = StringBuilderCache.Acquire();
			sbuilder.Append(techNode.name);
			if (techNode.maximumTonnage != float.MaxValue || techNode.minimumTonnage != 0) {
				sbuilder.Append(" [");
				sbuilder.Append(BuildTonnageString(techNode));
				sbuilder.Append("]");
			}
			sbuilder.Append(BuildSasAndScienceString(techNode));

			return sbuilder.ToStringAndRelease();
		}

		private static string BuildTonnageString(ProceduralAvionicsTechNode techNode)
		{
			StringBuilder sbuilder = StringBuilderCache.Acquire();
			if (techNode.minimumTonnage != 0) {
				sbuilder.Append(String.Format("{0:N}", techNode.minimumTonnage));
				if (techNode.maximumTonnage != float.MaxValue) {
					sbuilder.Append("-");
				}
				else {
					sbuilder.Append("+");
				}
			}
			if (techNode.maximumTonnage != float.MaxValue) {
				sbuilder.Append(String.Format("{0:N}", techNode.maximumTonnage));
			}
			if (sbuilder.Length == 0) {
				return "Unlimited";
			}
			else {
				sbuilder.Append("T");
			}
			return sbuilder.ToStringAndRelease();
		}

		private static string BuildSasAndScienceString(ProceduralAvionicsTechNode techNode)
		{
			StringBuilder sbuilder = StringBuilderCache.Acquire();
			sbuilder.Append(" {SAS: ");
			sbuilder.Append(techNode.SASServiceLevel);
			if (techNode.hasScienceContainer) {
				sbuilder.Append(", SC");
			}
			sbuilder.Append("}");

			return sbuilder.ToString();
		}

		private string BuildCostString(int cost)
		{
			if (cost == 0 || HighLogic.CurrentGame.Parameters.Difficulty.BypassEntryPurchaseAfterResearch) {
				return String.Empty;
			}
			return " (" + String.Format("{0:N}", cost) + ")";
		}

		#endregion

		private void RefreshPartWindow() //AGX: Refresh right-click part window to show/hide Groups slider
		{
			UIPartActionWindow[] partWins = FindObjectsOfType<UIPartActionWindow>();
			foreach (UIPartActionWindow partWin in partWins) {
				partWin.displayDirty = true;
			}
		}
	}
}
