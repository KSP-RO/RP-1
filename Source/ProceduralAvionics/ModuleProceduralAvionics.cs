using KSPAPIExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RP0.ProceduralAvionics
{
	class ModuleProceduralAvionics : ModuleAvionics, IPartMassModifier, IPartCostModifier
	{

		#region KSPFields and class variables
		// This limits how the weight of the avionics equipment per space of volume
		// Metric tons / cubic meter, defaults to roughly 1/3 the density of aluminum
		[KSPField]
		public float maxDensityOfAvionics = 1f;

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
		public float tonnageToMassRatio;

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

		protected override float getInternalMassLimit()
		{
			return proceduralMassLimit;
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
				ProceduralAvionicsUtils.Log("OnLoad exception: " + ex);
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
				//TODO: change resource rate (we might need to make a ModuleProceduralCommand)

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
				ProceduralAvionicsUtils.Log("ConficNode Deserialization needed");
				proceduralAvionicsConfigs = new Dictionary<string, ProceduralAvionicsConfig>();
				List<ProceduralAvionicsConfig> proceduralAvionicsConfigList = ObjectSerializer.Deserialize<List<ProceduralAvionicsConfig>>(proceduralAvionicsConfigsSerialized);
				foreach (var item in proceduralAvionicsConfigList)
				{
					ProceduralAvionicsUtils.Log("Deserialized " + item.name);
					proceduralAvionicsConfigs.Add(item.name, item);
				}
				ProceduralAvionicsUtils.Log("Deserialized " + proceduralAvionicsConfigs.Count + " configs");

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
				ProceduralAvionicsUtils.Log("Loaded AvionicsConfg: " + config.name);
			}

			List<ProceduralAvionicsConfig> configList = proceduralAvionicsConfigs.Values.ToList();
			proceduralAvionicsConfigsSerialized = ObjectSerializer.Serialize(configList);
			ProceduralAvionicsUtils.Log("Serialized configs");
		}

		private void UpdateCurrentConfig()
		{
			if (avionicsConfigName == oldAvionicsConfigName)
			{
				return;
			}
			ProceduralAvionicsUtils.Log("Setting config to " + avionicsConfigName);
			currentProceduralAvionicsConfig = proceduralAvionicsConfigs[avionicsConfigName];
			oldAvionicsConfigName = avionicsConfigName;
		}
		#endregion

		#region part attribute calculations
		private float CalculateNewMass()
		{
			if (HighLogic.LoadedSceneIsFlight)
			{
				return proceduralMassLimit / tonnageToMassRatio;
			}
			if (CurrentProceduralAvionicsConfig != null)
			{
				return proceduralMassLimit / CurrentProceduralAvionicsConfig.CurrentTechNode.tonnageToMassRatio;
			}
			else
			{
				ProceduralAvionicsUtils.Log("Cannot compute mass yet");
				return 0;
			}
		}

		private float CalculateCost()
		{
			//TODO: define
			return proceduralMassLimit * 100;
		}
		#endregion

		#region private utiliy functions
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
				UpdateSliders();
			}

			if (CurrentProceduralAvionicsConfig != null)
			{
				var maxAvionicsMass = GetCurrentVolume() * maxDensityOfAvionics;
				proceduralMassLimitEdit.maxValue = maxAvionicsMass * CurrentProceduralAvionicsConfig.CurrentTechNode.tonnageToMassRatio;
			}
			else
			{
				ProceduralAvionicsUtils.Log("Cannot update max value yet");
				proceduralMassLimitEdit.maxValue = float.MaxValue;
			}
			if (proceduralMassLimit > proceduralMassLimitEdit.maxValue)
			{
				ProceduralAvionicsUtils.Log("Lowering procedural mass limit to new max value of " + proceduralMassLimitEdit.maxValue);
				proceduralMassLimit = proceduralMassLimitEdit.maxValue;
			}
		}

		private void UpdateSliders()
		{
			BaseField avionicsConfigField = Fields["avionicsConfigName"];
			avionicsConfigField.guiActiveEditor = true;
			UI_ChooseOption range = (UI_ChooseOption)avionicsConfigField.uiControlEditor;
			range.options = proceduralAvionicsConfigs.Keys.ToArray();

			avionicsConfigName = proceduralAvionicsConfigs.Keys.First();

			ProceduralAvionicsUtils.Log("Defaulted config to " + avionicsConfigName);
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
			ProceduralAvionicsUtils.Log("verifying part");

			var maxAvionicsMass = GetCurrentVolume() * maxDensityOfAvionics;
			ProceduralAvionicsUtils.Log("new mass would be " + CalculateNewMass() + ", max avionics mass is " + maxAvionicsMass);
			if (maxAvionicsMass < CalculateNewMass())
			{
				proceduralMassLimit = oldProceduralMassLimit;
				ProceduralAvionicsUtils.Log("resetting part");
			}
			else
			{
				oldProceduralMassLimit = proceduralMassLimit;
				ProceduralAvionicsUtils.Log("part verified");
			}
		}

		// Using reflection to see if this is a procedural part (that way, we don't need to have procedur parts as a dependency
		private float GetCurrentVolume()
		{
			float currentShapeVolume = float.MaxValue;

			foreach (var module in part.Modules)
			{
				var moduleType = module.GetType();
				if (moduleType.FullName == "ProceduralParts.ProceduralPart")
				{
					//ProceduralAvionicsUtils.Log("Procedural Parts detected"); //This would spam the logs unless we do some old/current caching
					var reflectedShape = moduleType.GetProperty("CurrentShape").GetValue(module, null);
					currentShapeVolume = (float)reflectedShape.GetType().GetProperty("Volume").GetValue(reflectedShape, null);
				}
			}
			return currentShapeVolume;
		}

		private void SetInternalKSPFields()
		{
			tonnageToMassRatio = CurrentProceduralAvionicsConfig.CurrentTechNode.tonnageToMassRatio;
		}

		#endregion
	}
}