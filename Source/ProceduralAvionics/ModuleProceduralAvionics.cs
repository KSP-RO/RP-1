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

		// This controls how much the current part can control (in metric tons)
		[KSPField(isPersistant = true, guiName = "Tonnage", guiActive = false, guiActiveEditor = true, guiUnits = "T"),
		 UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.05f, sigFigs = 2, unit = "T")]
		public float proceduralMassLimit = 0;
		private float oldProceduralMassLimit = 0;

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
			var min = GetMinimumControllableTonnage();
			if (proceduralMassLimit > max) {
				Log("Resetting procedural mass limit to max of ", max.ToString()); 
				proceduralMassLimit = max;
			}
			if (proceduralMassLimit < min) {
				proceduralMassLimit = min;
				Log("Resetting procedural mass limit to min of ", min.ToString()); 
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

		[SerializeField]
		public byte[] proceduralAvionicsConfigsSerialized; //public so it will persist from loading to using it

		private Dictionary<string, ProceduralAvionicsConfig> proceduralAvionicsConfigs;
		#endregion

		#region event handlers
		public override void OnLoad(ConfigNode node)
		{
			try {
				if (GameSceneFilter.AnyInitializing.IsLoaded()) {
					LoadAvionicsConfigs(node);
				}
			}
			catch (Exception ex) {
				Log("OnLoad exception: ", ex.ToString());
				throw;
			}
		}

		public new void Start()
		{
			Log("Start called");
			DeserializeObjects();
			InitializeTechLimits();
			UpdateConfigSliders();
			BindUIChangeCallbacks();


			UpdateMaxValues();
			UpdateCurrentConfig();
			VerifyPart();

			SetInternalKSPFields();

			base.Start();
			Log("Start finished");
		}

		private bool callbacksBound = false;
		private void BindUIChangeCallbacks()
		{
			if (!callbacksBound) {
				string[] editorNames = new string[] { "proceduralMassLimit", "avionicsConfigName" };
				foreach (var editorName in editorNames) {
					Fields[editorName].uiControlEditor.onFieldChanged += UIChanged;
				}
				callbacksBound = true;
			}

		}

		private void UIChanged(BaseField arg1, object arg2)
		{
			GetInternalMassLimit(); //reset within bounds
			UpdateMaxValues();
			UpdateCurrentConfig();
			VerifyPart();

			SetInternalKSPFields();
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
		private void DeserializeObjects()
		{
			if (proceduralAvionicsConfigs == null && proceduralAvionicsConfigsSerialized != null) {
				Log("ConfigNode Deserialization needed");
				proceduralAvionicsConfigs = new Dictionary<string, ProceduralAvionicsConfig>();
				List<ProceduralAvionicsConfig> proceduralAvionicsConfigList = ObjectSerializer.Deserialize<List<ProceduralAvionicsConfig>>(proceduralAvionicsConfigsSerialized);
				foreach (var item in proceduralAvionicsConfigList) {
					Log("Deserialized ", item.name);
					proceduralAvionicsConfigs.Add(item.name, item);
				}
				Log("Deserialized ", proceduralAvionicsConfigs.Count.ToString(), " configs");
			}
		}

		public void LoadAvionicsConfigs(ConfigNode node)
		{
			proceduralAvionicsConfigs = new Dictionary<string, ProceduralAvionicsConfig>();
			foreach (ConfigNode tNode in node.GetNodes("AVIONICSCONFIG")) {
				ProceduralAvionicsConfig config = new ProceduralAvionicsConfig();
				config.Load(tNode);
				config.InitializeTechNodes();
				proceduralAvionicsConfigs.Add(config.name, config);
				Log("Loaded AvionicsConfg: ", config.name);
			}

			List<ProceduralAvionicsConfig> configList = proceduralAvionicsConfigs.Values.ToList();
			proceduralAvionicsConfigsSerialized = ObjectSerializer.Serialize(configList);
			Log("Serialized configs");
		}

		private void UpdateCurrentConfig()
		{
			if (avionicsConfigName == oldAvionicsConfigName) {
				return;
			}
			Log("Setting config to ", avionicsConfigName);
			currentProceduralAvionicsConfig = proceduralAvionicsConfigs[avionicsConfigName];
			oldAvionicsConfigName = avionicsConfigName;
		}
		#endregion

		#region part attribute calculations
		private float CalculateNewMass()
		{
			if (HighLogic.LoadedSceneIsFlight) {
				return DoMassCalculation();
			}
			if (CurrentProceduralAvionicsConfig != null) {
				//Standard density is 4/3s of maximum density
				//Log("Current Tech node standard density: ", CurrentProceduralAvionicsTechNode.standardAvionicsDensity.ToString());
				maxDensityOfAvionics = (CurrentProceduralAvionicsTechNode.standardAvionicsDensity * 4) / 3;
				tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
				return DoMassCalculation();
			}
			else {
				Log("Cannot compute mass yet");
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
			if (CurrentProceduralAvionicsConfig != null) {
				costPerControlledTon = CurrentProceduralAvionicsTechNode.costPerControlledTon;
				return DoCostCalculation();
			}
			else {
				Log("Cannot compute cost yet");
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
			return GetInternalMassLimit() / (cachedVolume * maxDensityOfAvionics * tonnageToMassRatio * 2);
		}

		private float GetMaximumControllableTonnage()
		{
			var maxAvionicsMass = cachedVolume * maxDensityOfAvionics;
			var maxForVolume = UtilMath.RoundToPlaces(maxAvionicsMass * tonnageToMassRatio * 2, 2);
			var maxControllableTonnage = Math.Min(maxForVolume, maximumTonnage);
			//Log("Max contrallabe Tonnage is ", maxControllableTonnage.ToString());
			return maxControllableTonnage;
		}

		private float GetMinimumControllableTonnage()
		{
			if (CurrentProceduralAvionicsTechNode != null) {
				return CurrentProceduralAvionicsTechNode.minimumTonnage;
			}
			return minimumTonnage;
		}

		private void UpdateMaxValues()
		{
			// update mass limit value slider

			if (proceduralMassLimitEdit == null) {
				proceduralMassLimitEdit = (UI_FloatEdit)Fields["proceduralMassLimit"].uiControlEditor;
			}

			if (CurrentProceduralAvionicsConfig != null) {

				tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
				proceduralMassLimitEdit.maxValue = GetMaximumControllableTonnage();
				proceduralMassLimitEdit.minValue = GetMinimumControllableTonnage();

				// Set slide, small, and large slider increments to be (at most) 0.025%, 1%, and 10%
				var procMassDelta = proceduralMassLimitEdit.maxValue - proceduralMassLimitEdit.minValue;

				//we'll start at a large incerement of 1, and work up from there
				int largeIncrement = 1;
				while (largeIncrement  < procMassDelta) {
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
			range.options = proceduralAvionicsConfigs.Keys.ToArray();

			if (String.IsNullOrEmpty(avionicsConfigName)) {
				avionicsConfigName = proceduralAvionicsConfigs.Keys.First();
				Log("Defaulted config to ", avionicsConfigName);
			}
		}

		private void InitializeTechLimits()
		{
			if (HighLogic.CurrentGame == null) {
				return;
			}
			if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX) {
				foreach (var config in proceduralAvionicsConfigs.Values) {
					config.currentTechNodeName = GetBestTechConfig(config.TechNodes, false).name;
				}
			}
			else {
				if (ResearchAndDevelopment.Instance == null) {
					return;
				}

				var unsupportedConfigs = new List<string>();
				foreach (var config in proceduralAvionicsConfigs.Values) {
					var bestConfig = GetBestTechConfig(config.TechNodes, true);
					if (bestConfig == null) {
						unsupportedConfigs.Add(config.name);
					}
					else {
						config.currentTechNodeName = GetBestTechConfig(config.TechNodes, true).name;
					}
				}

				//remove configs not yet unlocked
				foreach (var unsupportedConfigName in unsupportedConfigs) {
					proceduralAvionicsConfigs.Remove(unsupportedConfigName);
				}

			}
			Log("Tech limits initialized");
		}

		private ProceduralAvionicsTechNode GetBestTechConfig(Dictionary<String, ProceduralAvionicsTechNode> techNodes, bool limitToCurrentTech)
		{
			if (techNodes == null) {
				return null;
			}
			ProceduralAvionicsTechNode bestTechNode = null;
			foreach (var techNode in techNodes.Values) {
				if (bestTechNode == null || techNode.tonnageToMassRatio > bestTechNode.tonnageToMassRatio) {
					if (!limitToCurrentTech || ResearchAndDevelopment.GetTechnologyState(techNode.name) == RDTech.State.Available) {
						bestTechNode = techNode;
					}
				}
			}
			return bestTechNode;
		}

		private void VerifyPart()
		{
			if (GetInternalMassLimit() == oldProceduralMassLimit) {
				return;
			}
			Log("verifying part");

			Log("Volume: ", cachedVolume.ToString());

			//This has a side effect of setting maxDensityOfAvionics, so we need to call that first
			CalculateNewMass();
			Log("maxDensityOfAvionics: ", maxDensityOfAvionics.ToString());
			var maxAvionicsMass = cachedVolume * maxDensityOfAvionics;
			Log("new mass would be ", CalculateNewMass().ToString(), ", max avionics mass is ", maxAvionicsMass.ToString());
			if (maxAvionicsMass < CalculateNewMass()) {
				proceduralMassLimit = oldProceduralMassLimit;
				Log("resetting part");
			}
			else {
				oldProceduralMassLimit = GetInternalMassLimit();
				Log("part verified");
			}
		}

		private float cachedVolume = float.MaxValue;

		[KSPEvent]
		public void OnPartVolumeChanged(BaseEventData eventData)
		{
			Log("OnPartVolumeChanged called");
			try {
				cachedVolume = (float)eventData.Get<double>("newTotalVolume");
				//Log("cached total volume set from eventData: ", cachedVolume.ToString());
				UIChanged(null, null);
			}
			catch (Exception ex) {
				Log("error getting changed volume: ", ex.ToString());
			}
		}

		private void SetInternalKSPFields()
		{
			tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
			costPerControlledTon = CurrentProceduralAvionicsTechNode.costPerControlledTon;
			enabledProceduralW = CurrentProceduralAvionicsTechNode.enabledProceduralW;
			disabledProceduralW = CurrentProceduralAvionicsTechNode.disabledProceduralW;

			minimumTonnage = CurrentProceduralAvionicsTechNode.minimumTonnage;
			maximumTonnage = CurrentProceduralAvionicsTechNode.maximumTonnage;

			SASServiceLevel = CurrentProceduralAvionicsTechNode.SASServiceLevel;
			hasScienceContainer = CurrentProceduralAvionicsTechNode.hasScienceContainer;

			utilizationDisplay = String.Format("{0:0.#}%", GetControllableUtilizationPercentage() * 200);

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

			UpdateCostAndMassDisplays();
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
						Log("Setting SAS service level to ", SASServiceLevel.ToString());
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
	}
}
