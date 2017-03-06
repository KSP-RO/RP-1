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
		[KSPField]
		public float maxDensityOfAvionics = 1f; //metric tons / cubic meter, defaults to roughly 1/3 the density of aluminum

		[KSPField(isPersistant = true, guiName = "Tonnage", guiActive = false, guiActiveEditor = true, guiUnits = "T"),
		 UI_FloatEdit(scene = UI_Scene.Editor, minValue = 0f, incrementLarge = 10f, incrementSmall = 1f, incrementSlide = 0.1f, sigFigs = 1, unit = "T")]
		public float proceduralMassLimit = 1;
		private float oldProceduralMassLimit = 1;

		[KSPField(isPersistant = true, guiActiveEditor = true, guiActive = false, guiName = "Configuration"), UI_ChooseOption(scene = UI_Scene.Editor)]
		public string avionicsConfigName;
		private string oldAvionicsConfigName;

		private ProceduralAvionicsConfig currentProceduralAvionicsConfig;

		public ProceduralAvionicsConfig CurrentProceduralAvionicsConfig
		{
			get { return currentProceduralAvionicsConfig; }
		}

		private UI_FloatEdit proceduralMassLimitEdit;

		[SerializeField]
		public byte[] proceduralAvionicsConfigsSerialized; //public so it will persist from loading to using it

		private Dictionary<string, ProceduralAvionicsConfig> proceduralAvionicsConfigs;


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
				UnityEngine.Debug.Log("[ProcAvi] - OnLoad exception: " + ex);
				throw;
			}
		}

		private void DeserializeObjects()
		{
			if (proceduralAvionicsConfigs == null && proceduralAvionicsConfigsSerialized != null)
			{
				UnityEngine.Debug.Log("[ProcAvi] - deserialization needed");
				proceduralAvionicsConfigs = new Dictionary<string, ProceduralAvionicsConfig>();
				List<ProceduralAvionicsConfig> proceduralAvionicsConfigList = ObjectSerializer.Deserialize<List<ProceduralAvionicsConfig>>(proceduralAvionicsConfigsSerialized);
				foreach (var item in proceduralAvionicsConfigList)
				{
					UnityEngine.Debug.Log("[ProcAvi] - deserialized " + item.name);
					proceduralAvionicsConfigs.Add(item.name, item);
				}
				UnityEngine.Debug.Log("[ProcAvi] - deserialized " + proceduralAvionicsConfigs.Count + " configs");

				BaseField avionicsConfigField = Fields["avionicsConfigName"];
				avionicsConfigField.guiActiveEditor = true;
				UI_ChooseOption range = (UI_ChooseOption)avionicsConfigField.uiControlEditor;
				range.options = proceduralAvionicsConfigs.Keys.ToArray();

				avionicsConfigName = proceduralAvionicsConfigs.Keys.First();

				UnityEngine.Debug.Log("[ProcAvi] - Defaulted config to " + avionicsConfigName);
			}
		}

		public void LoadAvionicsConfigs(ConfigNode node)
		{
			var proceduralAvionicsConfigList = new List<ProceduralAvionicsConfig>();
			proceduralAvionicsConfigs = new Dictionary<string, ProceduralAvionicsConfig>();
			foreach (ConfigNode tNode in node.GetNodes("AVIONICSCONFIG"))
			{
				ProceduralAvionicsConfig config = new ProceduralAvionicsConfig();
				config.Load(tNode);
				proceduralAvionicsConfigList.Add(config);
				UnityEngine.Debug.Log("[ProcAvi] - Loaded AvionicsConfg: " + config.name);
			}

			proceduralAvionicsConfigsSerialized = ObjectSerializer.Serialize(proceduralAvionicsConfigList);
			UnityEngine.Debug.Log("[ProcAvi] - Serialized configs");

		}

		private void UpdateCurrentConfig()
		{
			if (avionicsConfigName == oldAvionicsConfigName)
			{
				return;
			}

			UnityEngine.Debug.Log("[ProcAvi] - Setting config to " + avionicsConfigName);

			currentProceduralAvionicsConfig = proceduralAvionicsConfigs[avionicsConfigName];

			oldAvionicsConfigName = avionicsConfigName;
		}

		protected override float getInternalMassLimit()
		{
			return proceduralMassLimit;
		}

		private void UpdateMaxValues()
		{
			//update mass limit value slider

			if (proceduralMassLimitEdit == null)
			{
				proceduralMassLimitEdit = (UI_FloatEdit)Fields["proceduralMassLimit"].uiControlEditor;
			}

			var ppModule = part.FindModuleImplementing<ProceduralParts.ProceduralPart>();
			var maxAvionicsMass = ppModule.CurrentShape.Volume * maxDensityOfAvionics;
			proceduralMassLimitEdit.maxValue = maxAvionicsMass;

		}

		private void VerifyPart()
		{
			if (proceduralMassLimit == oldProceduralMassLimit)
			{
				return;
			}
			UnityEngine.Debug.Log("[ProcAvi] - verifying part");
			var ppModule = part.FindModuleImplementing<ProceduralParts.ProceduralPart>();

			var maxAvionicsMass = ppModule.CurrentShape.Volume * maxDensityOfAvionics;
			UnityEngine.Debug.Log("[ProcAvi] - new mass would be " + CalculateNewMass() + ", max avionics mass is " + maxAvionicsMass);
			if (maxAvionicsMass < CalculateNewMass())
			{
				proceduralMassLimit = oldProceduralMassLimit;
				UnityEngine.Debug.Log("[ProcAvi] - resetting part");
			}
			else
			{
				oldProceduralMassLimit = proceduralMassLimit;
				UnityEngine.Debug.Log("[ProcAvi] - part verified");
			}
		}

		private float CalculateNewMass()
		{
			if (CurrentProceduralAvionicsConfig != null)
			{
				return proceduralMassLimit / CurrentProceduralAvionicsConfig.tonnageToMassRatio;
			}
			else
			{
				UnityEngine.Debug.Log("[ProcAvi] - Cannot compute mass yet");
				return 0;
			}
		}

		private float CalculateCost()
		{
			//TODO: define
			return proceduralMassLimit * 100;
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
	}
}