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
		const float FLOAT_ERROR_ALLOWANCE = 1.002F;

		// This controls how much the current part can control (in metric tons)
		[KSPField(isPersistant = true, guiName = "Tonnage", guiActive = false, guiActiveEditor = true, guiUnits = "T"),
		 UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.05f, sigFigs = 2, unit = "T")]
		public float proceduralMassLimit = 0;

		// We can have multiple configurations of avionics, for example: 
		//    boosters can have a high EC usage, but lower cost and mass
		//    probeCores can have a higher mass, but be very power efficient (and can even be turned off)
		[KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Configuration"), UI_ChooseOption(scene = UI_Scene.Editor)]
		public string avionicsConfigName;
		private string oldAvionicsConfigName;

		[KSPField(isPersistant = true)]
		public float maxDensityOfAvionics;

		[KSPField(isPersistant = true)]
		public float tonnageToMassRatio;

		[KSPField(isPersistant = true)]
		public float costPerControlledTon;

		[KSPField(isPersistant = true)]
		public float enabledProceduralW;

		[KSPField(isPersistant = true)]
		public float disabledProceduralW;

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
				if (CurrentProceduralAvionicsConfig != null) {
					return CurrentProceduralAvionicsConfig.CurrentTechNode;
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
			var max = GetMaximumControllableTonnage();
			if (max == 0) {
				//Sounds like we're not yet initiaziled, let's not change anything
				Log("no max");
				return proceduralMassLimit;
			}
			var min = GetMinimumControllableTonnage();
			bool changed = false;
			if (proceduralMassLimit > (max * FLOAT_ERROR_ALLOWANCE)) {
				Log("Resetting procedural mass limit to max of ", max, ", was ", proceduralMassLimit);
				proceduralMassLimit = max;
				changed = true;
			}
			if ((proceduralMassLimit * FLOAT_ERROR_ALLOWANCE) < min) {
				Log("Resetting procedural mass limit to min of ", min, ", was ", proceduralMassLimit);
				proceduralMassLimit = min;
				changed = true;
			}
			if (changed) {
				RefreshPartWindow();
			}
			return proceduralMassLimit;
		}

		protected override float GetEnabledkW()
		{
			return enabledProceduralW * GetResourceRateMultiplier() * GetMaximumControllableTonnage() / 2;
		}

		protected override float GetDisabledkW()
		{
			return disabledProceduralW * GetResourceRateMultiplier() * GetMaximumControllableTonnage() / 2;
		}

		protected override bool GetToggleable()
		{
			return disabledProceduralW > 0;
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
				if (GameSceneFilter.AnyInitializing.IsLoaded()) {
					ProceduralAvionicsTechManager.LoadAvionicsConfigs(node);
				}
			}
			catch (Exception ex) {
				Log("OnLoad exception: ", ex.Message);
				throw;
			}
		}

		public new void Start()
		{
			Log("Start called");
			UpdateConfigSliders();
			BindUIChangeCallbacks();

			UpdateMaxValues();
			UpdateCurrentConfig();

			Log("Setting internal ksp fields");
			SetInternalKSPFields();
			Log("Done setting internal ksp fields");

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
			SetMinVolume();
			SetInternalKSPFields();
		}

		private void AvionicsConfigChanged(BaseField arg1, object arg2)
		{
			AvionicsConfigChanged();
			ResetTo100();
		}

		private void AvionicsConfigChanged()
		{
			SetMinVolume();
			GetInternalMassLimit(); //reset within bounds
			UpdateMaxValues();
			UpdateCurrentConfig();

			SetInternalKSPFields();
		}


		private float cachedMinVolue = float.MaxValue;
		public void SetMinVolume(bool forceUpdate = false)
		{
			Log("Setting min volume for proceduralMassLimit of ", proceduralMassLimit);
			float minVolume = proceduralMassLimit / (2 * tonnageToMassRatio * maxDensityOfAvionics);
			if (float.IsNaN(minVolume)) {
				return;
			}
			Log("min volume sholud be ", minVolume);
			cachedMinVolue = minVolume;

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
			//Log("minVolume: ", minVolume);
			Log("Comparing against cached volume of ", cachedVolume);
			if (forceUpdate || minVolume > (cachedVolume * FLOAT_ERROR_ALLOWANCE)) { // adding a buffer for floating point errors
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

		#region config loading and serialization

		private void UpdateCurrentConfig()
		{
			if (avionicsConfigName == oldAvionicsConfigName) {
				return;
			}
			Log("Setting config to ", avionicsConfigName);
			currentProceduralAvionicsConfig =
				ProceduralAvionicsTechManager.GetProceduralAvionicsConfig(avionicsConfigName);
			oldAvionicsConfigName = avionicsConfigName;
			SetMinVolume(true);
		}
		#endregion

		#region part attribute calculations
		private float CalculateNewMass()
		{
			if (HighLogic.LoadedSceneIsFlight) {
				return DoMassCalculation();
			}
			if (CurrentProceduralAvionicsConfig != null && CurrentProceduralAvionicsTechNode != null) {
				//Standard density is 4/3s of maximum density
				//Log("Current Tech node standard density: ", CurrentProceduralAvionicsTechNode.standardAvionicsDensity);
				maxDensityOfAvionics = (CurrentProceduralAvionicsTechNode.standardAvionicsDensity * 4) / 3;
				tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
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
			return costPerControlledTon * 4 * costPercentage * GetInternalMassLimit();
		}

		private float GetResourceRateMultiplier()
		{
			return (GetControllableUtilizationPercentage() + .5f) / 1000;
		}
		#endregion

		#region private utiliy functions
		private float GetControllableUtilizationPercentage()
		{
			/*
			Log("Internal mass limit: ", GetInternalMassLimit());
			Log("cachedVolume: ", cachedVolume);
			Log("maxDensityOfAvionics: ", maxDensityOfAvionics);
			Log("tonnageToMassRatio: ", tonnageToMassRatio);
			*/
			return GetInternalMassLimit() / (cachedVolume * maxDensityOfAvionics * tonnageToMassRatio * 2);
		}

		private float GetMaximumControllableTonnage()
		{
			var maxAvionicsMass = cachedVolume * maxDensityOfAvionics;
			var maxForVolume = UtilMath.RoundToPlaces(maxAvionicsMass * tonnageToMassRatio * 2, 2);
			var maxControllableTonnage = Math.Min(maxForVolume, maximumTonnage);
			return maxControllableTonnage;
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
			float value = cachedVolume * maxDensityOfAvionics * tonnageToMassRatio;
			Log("100% utilization calculated as ", value);
			proceduralMassLimit = value;
		}

		private void UpdateMaxValues()
		{
			// update mass limit value slider

			if (proceduralMassLimitEdit == null) {
				proceduralMassLimitEdit = (UI_FloatEdit)Fields["proceduralMassLimit"].uiControlEditor;
			}

			if (CurrentProceduralAvionicsConfig != null && CurrentProceduralAvionicsTechNode != null) {

				tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
				proceduralMassLimitEdit.maxValue = GetMaximumControllableTonnage();
				proceduralMassLimitEdit.minValue = GetMinimumControllableTonnage();

				// Set slide, small, and large slider increments to be (at most) 0.025%, 1%, and 10%
				var procMassDelta = proceduralMassLimitEdit.maxValue - proceduralMassLimitEdit.minValue;

				//we'll start at a large incerement of 1, and work up from there
				int largeIncrement = 1;
				while (largeIncrement < procMassDelta) {
					largeIncrement *= 2;
				}

				float largeIncFloat = (float)largeIncrement;

				proceduralMassLimitEdit.incrementSlide = largeIncFloat / 4000;
				proceduralMassLimitEdit.incrementSmall = largeIncFloat / 100;
				proceduralMassLimitEdit.incrementLarge = largeIncFloat / 10;

				//There's some weirdness going on here that makes the slider not match up with min and max values, 
				//but it's so small i don't think i need to investigate it further
			}
			else {
				Log("Cannot update max value yet, CurrentProceduralAvionicsConfig is null");
				proceduralMassLimitEdit.maxValue = float.MaxValue;
				proceduralMassLimitEdit.minValue = 0;
			}
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

		[KSPEvent]
		public void OnPartVolumeChanged(BaseEventData eventData)
		{
			Log("OnPartVolumeChanged called");
			try {
				float volume = (float)eventData.Get<double>("newTotalVolume");
				Log("volume changed to ", volume);
				if (volume * FLOAT_ERROR_ALLOWANCE < cachedMinVolue && cachedMinVolue != float.MaxValue) {
					Log("volume of ", volume, " is less than expected min volume of ", cachedMinVolue, " expecting another update");
					RefreshPartWindow();
					//assuming the part will be resized
					return;
				}
				Log("setting cachedVolume to ", volume);
				cachedVolume = volume;
				//Log("cached total volume set from eventData: ", cachedVolume);
				AvionicsConfigChanged();
			}
			catch (Exception ex) {
				Log("error getting changed volume: ", ex);
			}
		}

		private void SetInternalKSPFields()
		{
			Log("Setting internal KSP fields");
			tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
			costPerControlledTon = CurrentProceduralAvionicsTechNode.costPerControlledTon;
			enabledProceduralW = CurrentProceduralAvionicsTechNode.enabledProceduralW;
			disabledProceduralW = CurrentProceduralAvionicsTechNode.disabledProceduralW;

			minimumTonnage = CurrentProceduralAvionicsTechNode.minimumTonnage;
			maximumTonnage = CurrentProceduralAvionicsTechNode.maximumTonnage;

			SASServiceLevel = CurrentProceduralAvionicsTechNode.SASServiceLevel;
			hasScienceContainer = CurrentProceduralAvionicsTechNode.hasScienceContainer;

			UpdateCostAndMassDisplays();

			utilizationDisplay = String.Format("{0:0.#}%", GetControllableUtilizationPercentage() * 200);
			Log("Utilization display: ", utilizationDisplay);

			StringBuilder powerConsumptionBuilder = StringBuilderCache.Acquire();
			if (GetEnabledkW() >= 0.1) {
				powerConsumptionBuilder.AppendFormat(kwFormat, GetEnabledkW()).Append(" kW");
			}
			else {
				powerConsumptionBuilder.AppendFormat(wFormat, GetEnabledkW() * 1000).Append(" W");
			}
			if (GetDisabledkW() > 0) {
				powerConsumptionBuilder.Append(" /");
				if (GetDisabledkW() >= 0.1) {
					powerConsumptionBuilder.AppendFormat(kwFormat, GetDisabledkW()).Append(" kW");
				}
				else {
					powerConsumptionBuilder.AppendFormat(wFormat, GetDisabledkW() * 1000).Append(" W");
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
		private void UpdateCostAndMassDisplays()
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
				windowRect = GUI.Window(GetInstanceID(), windowRect, WindowFunction, "Configure Procedural Avionics");
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
				if (!ProceduralAvionicsTechManager.TechIsEnabled) {
					continue;
				}
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
					int unlockCost = GetUnlockCost(guiAvionicsConfigName, techNode);
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

							// shouldn't need this since we only show this if you can afford it
							// but just in case...
							if (Funding.Instance.Funds >= unlockCost) {
								Funding.Instance.AddFunds(-unlockCost, TransactionReasons.RnDPartPurchase);
								ProceduralAvionicsTechManager
									.SetMaxUnlockedTech(guiAvionicsConfigName, techNode.name);
							}
							else {
								switchedConfig = false;
							}
						}

					}
					if (switchedConfig) {
						Log("Configuration window changed, updating part window");
						UpdateConfigSliders();
						currentlyDisplayedConfigs.currentTechNodeName = techNode.name;
						currentProceduralAvionicsConfig = currentlyDisplayedConfigs;
						avionicsConfigName = guiAvionicsConfigName;
						AvionicsConfigChanged();
						SetMinVolume(true);
						ResetTo100();
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
			sbuilder.Append("T");
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
			if (cost == 0) {
				return String.Empty;
			}
			return " (" + String.Format("{0:N}", cost) + ")";
		}

		private int GetUnlockCost(string avionicsConfigName, ProceduralAvionicsTechNode techNode)
		{
			string currentUnlockedTech = ProceduralAvionicsTechManager
				.GetMaxUnlockedTech(avionicsConfigName);
			int alreadyPaidCost = 0;
			if (!String.IsNullOrEmpty(currentUnlockedTech)) {
				alreadyPaidCost = ProceduralAvionicsTechManager
					.GetProceduralAvionicsConfig(avionicsConfigName)
					.TechNodes[currentUnlockedTech].unlockCost;
			}
			int priceDiff = techNode.unlockCost - alreadyPaidCost;
			return (priceDiff > 0 ? priceDiff : 0);
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
