using System.Collections.Generic;
using UnityEngine;

namespace RP0
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, new GameScenes[] { GameScenes.EDITOR, GameScenes.SPACECENTER })]
    public class ToolingManager : ScenarioModule
    {
        #region Fields

        protected static ToolingDatabase database = new ToolingDatabase();
        protected static Dictionary<string, ToolingDefinition> toolingDefinitions = null;

        public bool toolingEnabled = true;
        public static ToolingManager Instance { get; private set; } = null;

        #endregion

        #region Overrides and Monobehaviour methods

        public override void OnAwake()
        {
            if (Instance != null)
                Destroy(Instance);
            Instance = this;

            GameEvents.OnGameSettingsApplied.Add(LoadSettings);
            GameEvents.onGameStateLoad.Add(LoadSettings);
        }

        public override void OnLoad(ConfigNode node)
        {
            ToolingDatabase.Load(node.GetNode("Tooling"));
            EnsureDefinitionsLoaded();
        }

        public override void OnSave(ConfigNode node)
        {
            ToolingDatabase.Save(node.AddNode("Tooling"));
        }
        
        public void OnDestroy()
        {
            GameEvents.OnGameSettingsApplied.Remove(LoadSettings);
            GameEvents.onGameStateLoad.Remove(LoadSettings);
        }

        #endregion

        protected void LoadSettings(ConfigNode n) => LoadSettings();
        protected void LoadSettings() => toolingEnabled = HighLogic.CurrentGame.Parameters.CustomParams<RP0Settings>().IsToolingEnabled;

        public ToolingDefinition GetToolingDefinition(string name)
        {
            EnsureDefinitionsLoaded();
            toolingDefinitions.TryGetValue(name, out ToolingDefinition def);
            return def;
        }

        private static void EnsureDefinitionsLoaded()
        {
            if (toolingDefinitions == null)
            {
                toolingDefinitions = new Dictionary<string, ToolingDefinition>();

                foreach (ConfigNode n in GameDatabase.Instance.GetConfigNodes("TOOLING_DEFINITION"))
                {
                    var def = new ToolingDefinition(n);
                    Debug.Log("[ModuleTooling] Loaded definition: " + def.name);
                    if (toolingDefinitions.ContainsKey(def.name))
                        Debug.LogError("[ModuleTooling] Found duplicate definition: " + def.name);
                    else
                        toolingDefinitions.Add(def.name, def);
                }
            }
        }
    }
}
