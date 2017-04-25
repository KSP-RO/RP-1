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
		public float proceduralMassLimit = 1;
		private float oldProceduralMassLimit = 1;

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
		private bool needsTechInit = true;

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

		public new void Update()
		{
			if (HighLogic.LoadedSceneIsEditor) {
				DeserializeObjects();
				UpdateMaxValues();
				UpdateCurrentConfig();
				VerifyPart();

				SetInternalKSPFields();
			}
			else {
				SetSASServiceLevel();
				SetScienceContainer();
			}
			base.Update();
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
				maxDensityOfAvionics = (CurrentProceduralAvionicsTechNode.standardAvionicsDensity * 4) / 3;
				tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
				return DoMassCalculation();
			}
			else {
				//Log("Cannot compute mass yet");
				return 0;
			}
		}

		private float DoMassCalculation()
		{
			float controllablePercentage = GetControllableUtilizationPercentage();
			float massPercentage = (-1 * controllablePercentage * controllablePercentage) + (2 * controllablePercentage);
			return massPercentage * GetCurrentVolume() * GetCurrentDensity();
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
				//Log("Cannot compute cost yet");
				return 0;
			}
		}

		private float DoCostCalculation()
		{
			float controllablePercentage = GetControllableUtilizationPercentage();
			float costPercentage = controllablePercentage * controllablePercentage;
			return costPerControlledTon * 4 * costPercentage * proceduralMassLimit;
		}

		private float GetResourceRateMultiplier()
		{
			return (GetControllableUtilizationPercentage() + .5f) / 1000;
		}
		#endregion

		#region private utiliy functions
		private float GetControllableUtilizationPercentage()
		{
			return proceduralMassLimit / (GetCurrentVolume() * maxDensityOfAvionics * tonnageToMassRatio * 2);
		}

		private float GetMaximumControllableTonnage()
		{
			var maxAvionicsMass = GetCurrentVolume() * maxDensityOfAvionics;
			var maxForVolume = maxAvionicsMass * tonnageToMassRatio * 2;
			return Math.Min(maxForVolume, maximumTonnage);
		}

		private float GetMinimumControllableTonnage()
		{
			return minimumTonnage;
		}

		private void UpdateMaxValues()
		{
			// update mass limit value slider

			if (proceduralMassLimitEdit == null) {
				proceduralMassLimitEdit = (UI_FloatEdit)Fields["proceduralMassLimit"].uiControlEditor;
			}

			if (needsTechInit) {
				InitializeTechLimits();
				UpdateConfigSliders();
			}

			if (CurrentProceduralAvionicsConfig != null) {
				tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
				proceduralMassLimitEdit.maxValue = GetMaximumControllableTonnage();
				proceduralMassLimitEdit.minValue = GetMinimumControllableTonnage();

				// Set slide, small, and large slider increments to be 0.025%, 1%, and 10%
				var procMassDelta = proceduralMassLimitEdit.maxValue - proceduralMassLimitEdit.minValue;
				proceduralMassLimitEdit.incrementSlide = procMassDelta / 4000;
				proceduralMassLimitEdit.incrementSmall = procMassDelta / 100;
				proceduralMassLimitEdit.incrementLarge = procMassDelta / 10;
			}
			else {
				//Log("Cannot update max value yet");
				proceduralMassLimitEdit.maxValue = float.MaxValue;
				proceduralMassLimitEdit.minValue = 0;
			}
			if (proceduralMassLimit > proceduralMassLimitEdit.maxValue) {
				Log("Lowering procedural mass limit to new max value of ", proceduralMassLimitEdit.maxValue.ToString());
				proceduralMassLimit = proceduralMassLimitEdit.maxValue;
				// Don't know how to force the gui to refresh this
			}
		}

		private void UpdateConfigSliders()
		{
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
			needsTechInit = false;
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
			if (proceduralMassLimit == oldProceduralMassLimit) {
				return;
			}
			Log("verifying part");

			var maxAvionicsMass = GetCurrentVolume() * maxDensityOfAvionics;
			Log("new mass would be ", CalculateNewMass().ToString(), ", max avionics mass is ", maxAvionicsMass.ToString());
			if (maxAvionicsMass < CalculateNewMass()) {
				proceduralMassLimit = oldProceduralMassLimit;
				Log("resetting part");
			}
			else {
				oldProceduralMassLimit = proceduralMassLimit;
				Log("part verified");
			}
		}

		private float cachedVolume = 0;

		// Using reflection to see if this is a procedural part (that way, we don't need to have procedur parts as a dependency
		private float GetCurrentVolume()
		{
			// Volume won't change in flight, so we'll cache this, so we're not using reflection all the time
			if (HighLogic.LoadedSceneIsFlight && cachedVolume > 0) {
				return cachedVolume;
			}

			// Honestly, if you're not using procedural parts, then this is going to do some really funky things.
			// It's going to look like you have all this room to put stuff in, and thus aren't really worried
			// about "efficiency".  Your cost will be low, but your mass will be a lot higher than it would be expected.
			float currentShapeVolume = float.MaxValue;

			foreach (var module in part.Modules) {
				var moduleType = module.GetType();
				if (moduleType.FullName == "ProceduralParts.ProceduralPart") {
					// This would spam the logs unless we do some old/current caching.
					//Log("Procedural Parts detected"); 
					var reflectedShape = moduleType.GetProperty("CurrentShape").GetValue(module, null);
					currentShapeVolume = (float)reflectedShape.GetType().GetProperty("Volume").GetValue(reflectedShape, null);
				}
			}

			cachedVolume = currentShapeVolume;
			return currentShapeVolume;
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

			utilizationDisplay = String.Format("{0:0.#}%", GetControllableUtilizationPercentage() * 100);

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
				ppFieldsHidden = HideField(TCSmoduleName, "massDisplay") && HideField(TCSmoduleName , "volumeDisplay");
			}

			float baseCost = GetBaseCost();
			float baseMass = GetBaseMass();
			massDisplay = MathUtils.FormatMass(baseMass + GetModuleMass(0, ModifierStagingSituation.CURRENT));
			costDisplay = (baseCost + GetModuleCost(0, ModifierStagingSituation.CURRENT)).ToString();
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
