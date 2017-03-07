using KSPAPIExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RP0
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
		 UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.1f, sigFigs = 1, unit = "T")]
		public float proceduralMassLimit = 1;
		private float oldProceduralMassLimit = 1;

		// We can have multiple configurations of avionics, for example: 
		//    boosters can have a high EC usage, but lower cost and mass
		//    probeCores can have a higher mass, but be very power efficient (and can even be turned off)
		[KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Configuration"), UI_ChooseOption(scene = UI_Scene.Editor)]
		public string avionicsConfigName;
		private string oldAvionicsConfigName;

		// The currently selected avionics config
		public ProceduralAvionicsConfig CurrentProceduralAvionicsConfig
		{
			get { return currentProceduralAvionicsConfig; }
		}

		protected override float getInternalMassLimit()
		{
			return proceduralMassLimit;
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
				//TODO: change resource rate (we might need to make a ModuleProceduralCommand)
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
				Log("deserialization needed");
				proceduralAvionicsConfigs = new Dictionary<string, ProceduralAvionicsConfig>();
				List<ProceduralAvionicsConfig> proceduralAvionicsConfigList = ObjectSerializer.Deserialize<List<ProceduralAvionicsConfig>>(proceduralAvionicsConfigsSerialized);
				foreach (var item in proceduralAvionicsConfigList)
				{
					Log("deserialized " + item.name);
					proceduralAvionicsConfigs.Add(item.name, item);
				}
				Log("deserialized " + proceduralAvionicsConfigs.Count + " configs");

				BaseField avionicsConfigField = Fields["avionicsConfigName"];
				avionicsConfigField.guiActiveEditor = true;
				UI_ChooseOption range = (UI_ChooseOption)avionicsConfigField.uiControlEditor;
				range.options = proceduralAvionicsConfigs.Keys.ToArray();

				avionicsConfigName = proceduralAvionicsConfigs.Keys.First();

				Log("Defaulted config to " + avionicsConfigName);
			}
		}

		public void LoadAvionicsConfigs(ConfigNode node)
		{
			proceduralAvionicsConfigs = new Dictionary<string, ProceduralAvionicsConfig>();
			foreach (ConfigNode tNode in node.GetNodes("AVIONICSCONFIG"))
			{
				ProceduralAvionicsConfig config = new ProceduralAvionicsConfig();
				config.Load(tNode);
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
			if (CurrentProceduralAvionicsConfig != null)
			{
				return proceduralMassLimit / CurrentProceduralAvionicsConfig.tonnageToMassRatio;
			}
			else
			{
				Log("Cannot compute mass yet");
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

			var maxAvionicsMass = GetCurrentVolume() * maxDensityOfAvionics;
			proceduralMassLimitEdit.maxValue = maxAvionicsMass * currentProceduralAvionicsConfig.tonnageToMassRatio;
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

		// Using reflection to see if this is a procedural part (that way, we don't need to have procedur parts as a dependency
		private float GetCurrentVolume()
		{
			float currentShapeVolume = float.MaxValue;

			foreach (var module in part.Modules)
			{
				var moduleType = module.GetType();
				if (moduleType.FullName == "ProceduralParts.ProceduralPart")
				{
					//Log("Procedural Parts detected"); //This would spam the logs unless we do some old/current caching
					var reflectedShape = moduleType.GetProperty("CurrentShape").GetValue(module, null);
					currentShapeVolume = (float)reflectedShape.GetType().GetProperty("Volume").GetValue(reflectedShape, null);
				}
			}
			return currentShapeVolume;
		}

		private void Log(string message)
		{
			UnityEngine.Debug.Log("[ProcAvi] - " + message);
		}
		#endregion
	}
}