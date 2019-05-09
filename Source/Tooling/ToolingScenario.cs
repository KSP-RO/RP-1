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

        #region Instance

        private static ToolingManager _instance = null;
        public static ToolingManager Instance
        {
            get
            {
                return _instance;
            }
        }

        #endregion

        #endregion

        #region Overrides and Monobehaviour methods

        public override void OnAwake()
        {
            base.OnAwake();

            if (_instance != null)
            {
                GameObject.Destroy(_instance);
            }
            _instance = this;
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            ToolingDatabase.Load(node.GetNode("Tooling"));

            EnsureDefinitionsLoaded();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            ToolingDatabase.Save(node.AddNode("Tooling"));
        }

        #endregion

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
                    {
                        Debug.LogError("[ModuleTooling] Found duplicate definition: " + def.name);
                        continue;
                    }

                    toolingDefinitions.Add(def.name, def);
                }
            }
        }
    }
}
