using RealFuels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RP0.ProceduralAvionics
{
	/// <summary>
	/// This clas does all the work of determining which tech nodes are available
	/// </summary>
	public static class ProceduralAvionicsTechManager
	{
		private static List<ProceduralAvionicsConfig> allTechNodes;

		private static Dictionary<string, string> unlockedTech;

		#region calls made duirng OnLoad, OnSave, other initialization

		public static void LoadAvionicsConfigs(ConfigNode node)
		{
			allTechNodes = new List<ProceduralAvionicsConfig>();
			foreach (ConfigNode tNode in node.GetNodes("AVIONICSCONFIG")) {
				ProceduralAvionicsConfig config = new ProceduralAvionicsConfig();
				config.Load(tNode);
				config.InitializeTechNodes();
				allTechNodes.Add(config);
				ProceduralAvionicsUtils.Log("Loaded AvionicsConfg: ", config.name);
			}

		}

		internal static void SetUnlockedTechState(string param)
		{
			ProceduralAvionicsUtils.Log("Setting unlocked tech state");
			unlockedTech = new Dictionary<string, string>();
			if (param != null) {
				string[] typeStrings = param.Split('|');
				if (typeStrings.Length > 1) {
					for (int i = 0; i < typeStrings.Length; i += 2) {
						unlockedTech.Add(typeStrings[i], typeStrings[i + 1]);
					}
				}
			}
			ProceduralAvionicsUtils.Log("unlocked tech has ", unlockedTech.Keys.Count.ToString(), " nodes");

			//At this point, we can go through our configs and see if we have any that need to be unlocked
			foreach (ProceduralAvionicsConfig config in allTechNodes) {
				if (!unlockedTech.ContainsKey(config.name)) {
					//We don't have max level for this config, should we?
					ProceduralAvionicsTechNode freeTech = 
						config.TechNodes.Values.Where(techNode => GetUnlockCost(config.name, techNode) <= 1).FirstOrDefault();
					if (freeTech != null) {
						unlockedTech.Add(config.name, freeTech.name);
					}
				}
			}
		}

		#endregion

		public static bool TechIsEnabled {
			get {
				return HighLogic.CurrentGame != null &&
					ResearchAndDevelopment.Instance != null &&
					(HighLogic.CurrentGame.Mode == Game.Modes.CAREER ||
					HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX);
			}
		}


		public static List<string> GetAvailableConfigs()
		{
			//ProceduralAvionicsUtils.Log("Getting Available configs, procedural avionics has ", allTechNodes.Count, " nodes loaded");
			List<string> availableConfigs = new List<string>();
			foreach (var config in allTechNodes) {
				if (!TechIsEnabled || (config.TechNodes.Values.Where(node => node.IsAvailable).Count() > 0)) {
					availableConfigs.Add(config.name);
				}
			}
			return availableConfigs;
		}

		public static List<string> GetPurchasedConfigs()
		{
			if (TechIsEnabled) {
				return unlockedTech.Keys.ToList();
			}
			else {
				return allTechNodes.Select(node => node.name).ToList();
			}
		}

		internal static object GetUnlockedTechState()
		{
			StringBuilder builder = StringBuilderCache.Acquire();
			foreach (string unlockedTechType in unlockedTech.Keys) {
				if (builder.Length != 0) {
					builder.Append("|");
				}
				builder.Append(unlockedTechType);
				builder.Append("|");
				builder.Append(unlockedTech[unlockedTechType]);
			}
			string state = builder.ToStringAndRelease();
			ProceduralAvionicsUtils.Log("Unlocked Tech state:", state);
			return state;
		}

		internal static void SetMaxUnlockedTech( string avionicsConfigName, string techNodeName)
		{
			ProceduralAvionicsUtils.Log("Unlocking ", techNodeName, " for ", avionicsConfigName);
			if (!unlockedTech.ContainsKey(avionicsConfigName)) {
				ProceduralAvionicsUtils.Log("Unlocking for the first time");
				unlockedTech.Add(avionicsConfigName, techNodeName);
			}
			else {
				ProceduralAvionicsUtils.Log("Unlocking new level");
				unlockedTech[avionicsConfigName] = techNodeName;
			}
		}

		internal static string GetMaxUnlockedTech(string avionicsConfigName)
		{
			if (!TechIsEnabled) {
				var techNodesForConfig = allTechNodes.Where(config => config.name == avionicsConfigName).FirstOrDefault();
				var tn = techNodesForConfig.TechNodes.Values.Last().name;
				return tn;
			}
			if (unlockedTech.ContainsKey(avionicsConfigName)) {
				return unlockedTech[avionicsConfigName];
			}
			return String.Empty;
		}

		internal static int GetUnlockCost(string avionicsConfigName, ProceduralAvionicsTechNode techNode)
		{
			if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return 0;

			string ecmName = GetEcmName(avionicsConfigName, techNode);
			double cost = EntryCostManager.Instance.ConfigEntryCost(ecmName);

			return (int)cost;
		}

		internal static bool PurchaseConfig(string avionicsConfigName, ProceduralAvionicsTechNode techNode)
		{
			string ecmName = GetEcmName(avionicsConfigName, techNode);
			return EntryCostManager.Instance.PurchaseConfig(ecmName);
		}

		private static string GetEcmName(string avionicsConfigName, ProceduralAvionicsTechNode techNode)
		{
			return $"{avionicsConfigName}-{techNode.name}";
		}

		public static ProceduralAvionicsConfig GetProceduralAvionicsConfig(string configName)
		{
			return allTechNodes.Where(config => config.name == configName).FirstOrDefault();
		}
	}
}
