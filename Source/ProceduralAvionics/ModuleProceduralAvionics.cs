using KSPAPIExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static RP0.ProceduralAvionics.ProceduralAvionicsUtils;

namespace RP0.ProceduralAvionics
{
	class ModuleProceduralAvionics : ModuleAvionics, IPartMassModifier, IPartCostModifier
	{

		#region KSPFields, overrides, and class variables

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

		[KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Percent Utilization")]
		public string utilizationDisplay;

		[KSPField(isPersistant = false, guiActive = false, guiActiveEditor = true, guiName = "Power Requirements")]
		public string powerRequirementsDisplay;

		// The currently selected avionics config
		public ProceduralAvionicsConfig CurrentProceduralAvionicsConfig
		{
			get { return currentProceduralAvionicsConfig; }
		}

		public ProceduralAvionicsTechNode CurrentProceduralAvionicsTechNode
		{
			get
			{
				return CurrentProceduralAvionicsConfig.CurrentTechNode;
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
			try
			{
				if (GameSceneFilter.AnyInitializing.IsLoaded())
				{
					LoadAvionicsConfigs(node);
				}
			}
			catch (Exception ex)
			{
				Log("OnLoad exception: " + ex);
				throw;
			}
		}

		public new void Update()
		{
			if (HighLogic.LoadedSceneIsEditor)
			{
				DeserializeObjects();
				UpdateMaxValues();
				UpdateCurrentConfig();
				VerifyPart();

				SetInternalKSPFields();
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
			if (proceduralAvionicsConfigs == null && proceduralAvionicsConfigsSerialized != null)
			{
				Log("ConfigNode Deserialization needed");
				proceduralAvionicsConfigs = new Dictionary<string, ProceduralAvionicsConfig>();
				List<ProceduralAvionicsConfig> proceduralAvionicsConfigList = ObjectSerializer.Deserialize<List<ProceduralAvionicsConfig>>(proceduralAvionicsConfigsSerialized);
				foreach (var item in proceduralAvionicsConfigList)
				{
					Log("Deserialized " + item.name);
					proceduralAvionicsConfigs.Add(item.name, item);
				}
				Log("Deserialized " + proceduralAvionicsConfigs.Count + " configs");

			}
		}

		public void LoadAvionicsConfigs(ConfigNode node)
		{
			proceduralAvionicsConfigs = new Dictionary<string, ProceduralAvionicsConfig>();
			foreach (ConfigNode tNode in node.GetNodes("AVIONICSCONFIG"))
			{
				ProceduralAvionicsConfig config = new ProceduralAvionicsConfig();
				config.Load(tNode);
				config.InitializeTechNodes();
				proceduralAvionicsConfigs.Add(config.name, config);
				Log("Loaded AvionicsConfg: " + config.name);
			}

			List<ProceduralAvionicsConfig> configList = proceduralAvionicsConfigs.Values.ToList();
			proceduralAvionicsConfigsSerialized = ObjectSerializer.Serialize(configList);
			Log("Serialized configs");
		}

		private void UpdateCurrentConfig()
		{
			if (avionicsConfigName == oldAvionicsConfigName)
			{
				return;
			}
			Log("Setting config to " + avionicsConfigName);
			currentProceduralAvionicsConfig = proceduralAvionicsConfigs[avionicsConfigName];
			oldAvionicsConfigName = avionicsConfigName;
		}
		#endregion

		#region part attribute calculations
		private float CalculateNewMass()
		{
			if (HighLogic.LoadedSceneIsFlight)
			{
				return DoMassCalculation();
			}
			if (CurrentProceduralAvionicsConfig != null)
			{
				//Standard density is 4/3s of maximum density
				maxDensityOfAvionics = (CurrentProceduralAvionicsTechNode.standardAvionicsDensity * 4) / 3;
				tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
				return DoMassCalculation();
			}
			else
			{
				Log("Cannot compute mass yet");
				return 0;
			}
		}

		private float DoMassCalculation()
		{
			float controllablePercentage = GetControllableUtilizationPercentage();
			float massPercentage = (-1 * controllablePercentage * controllablePercentage) + (2 * controllablePercentage);
			return massPercentage * GetCurrentVolume() * GetCurrentDensity();
		}

		private float CalculateCost() {

			if (HighLogic.LoadedSceneIsFlight)
			{
				return DoCostCalculation();
			}
			if (CurrentProceduralAvionicsConfig != null)
			{
				costPerControlledTon = CurrentProceduralAvionicsTechNode.costPerControlledTon;
				return DoCostCalculation();
			}
			else
			{
				Log("Cannot compute cost yet");
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
			//update mass limit value slider

			if (proceduralMassLimitEdit == null)
			{
				proceduralMassLimitEdit = (UI_FloatEdit)Fields["proceduralMassLimit"].uiControlEditor;
			}

			if (needsTechInit)
			{
				InitializeTechLimits();
				UpdateConfigSliders();
			}

			if (CurrentProceduralAvionicsConfig != null)
			{
				tonnageToMassRatio = CurrentProceduralAvionicsTechNode.tonnageToMassRatio;
				proceduralMassLimitEdit.maxValue = GetMaximumControllableTonnage();
				proceduralMassLimitEdit.minValue = GetMinimumControllableTonnage();
			}
			else
			{
				Log("Cannot update max value yet");
				proceduralMassLimitEdit.maxValue = float.MaxValue;
				proceduralMassLimitEdit.minValue = 0;
			}
			if (proceduralMassLimit > proceduralMassLimitEdit.maxValue)
			{
				Log("Lowering procedural mass limit to new max value of " + proceduralMassLimitEdit.maxValue);
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

			avionicsConfigName = proceduralAvionicsConfigs.Keys.First();

			Log("Defaulted config to " + avionicsConfigName);
		}

		private void InitializeTechLimits()
		{
			if (HighLogic.CurrentGame == null)
			{
				return;
			}
			if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX)
			{
				foreach (var config in proceduralAvionicsConfigs.Values)
				{
					config.currentTechNodeName = GetBestTechConfig(config.TechNodes, false).name;
				}
			}
			else
			{
				if (ResearchAndDevelopment.Instance == null)
				{
					return;
				}

				var unsupportedConfigs = new List<string>();
				foreach (var config in proceduralAvionicsConfigs.Values)
				{
					var bestConfig = GetBestTechConfig(config.TechNodes, true);
					if (bestConfig == null)
					{
						unsupportedConfigs.Add(config.name);
					}
					else
					{
						config.currentTechNodeName = GetBestTechConfig(config.TechNodes, true).name;
					}
				}

				//remove configs not yet unlocked
				foreach (var unsupportedConfigName in unsupportedConfigs)
				{
					proceduralAvionicsConfigs.Remove(unsupportedConfigName);
				}

			}
			needsTechInit = false;
		}

		private ProceduralAvionicsTechNode GetBestTechConfig(Dictionary<String, ProceduralAvionicsTechNode> techNodes, bool limitToCurrentTech)
		{
			if (techNodes == null)
			{
				return null;
			}
			ProceduralAvionicsTechNode bestTechNode = null;
			foreach (var techNode in techNodes.Values)
			{
				if (bestTechNode == null || techNode.tonnageToMassRatio > bestTechNode.tonnageToMassRatio)
				{
					if (!limitToCurrentTech || ResearchAndDevelopment.GetTechnologyState(techNode.name) == RDTech.State.Available)
					{ 
						bestTechNode = techNode;
					}
				}
			}
			return bestTechNode;
		}

		private void VerifyPart()
		{
			if (proceduralMassLimit == oldProceduralMassLimit)
			{
				return;
			}
			Log("verifying part");

			var maxAvionicsMass = GetCurrentVolume() * maxDensityOfAvionics;
			Log("new mass would be " + CalculateNewMass() + ", max avionics mass is " + maxAvionicsMass);
			if (maxAvionicsMass < CalculateNewMass())
			{
				proceduralMassLimit = oldProceduralMassLimit;
				Log("resetting part");
			}
			else
			{
				oldProceduralMassLimit = proceduralMassLimit;
				Log("part verified");
			}
		}

		private float cachedVolume = 0;

		// Using reflection to see if this is a procedural part (that way, we don't need to have procedur parts as a dependency
		private float GetCurrentVolume()
		{
			// Volume won't change in flight, so we'll cache this, so we're not using reflection all the time
			if (HighLogic.LoadedSceneIsFlight && cachedVolume > 0)
			{
				return cachedVolume;
			}

			// Honestly, if you're not using procedural parts, then this is going to do some really funky things.
			// It's going to look like you have all this room to put stuff in, and thus aren't really worried
			// about "efficiency".  Your cost will be low, but your mass will be a lot higher than it would be expected.
			float currentShapeVolume = float.MaxValue;

			foreach (var module in part.Modules)
			{
				var moduleType = module.GetType();
				if (moduleType.FullName == "ProceduralParts.ProceduralPart")
				{
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

			utilizationDisplay = String.Format("{0:0.#}%", GetControllableUtilizationPercentage() * 100);

			string powerCosumption = String.Format("{0:0.##}", GetEnabledkW()) + " kW";
			if (GetDisabledkW() > 0)
			{
				powerCosumption += " / " + String.Format("{0:0.##}", GetDisabledkW()) + " kW";
			}

			powerRequirementsDisplay = powerCosumption; 
		}

		#endregion
	}
}
