using System;
using System.Collections.Generic;

namespace RP0
{
    public class ToolingDefinition : IConfigNode
    {
        [Persistent]
        public string name;

        /// <summary>
        /// Text to show on the PAW action - for example "Tool Tank"
        /// </summary>
        [Persistent]
        public string toolingName;

        [Persistent]
        public float untooledMultiplier;

        [Persistent]
        public float finalToolingCostMultiplier;

        [Persistent]
        public float costMultiplierDL;

        [Persistent]
        public string costReducers;

        public ToolingDefinition() { }

        public ToolingDefinition(ConfigNode node)
        {
            Load(node);
        }

        public void Load(ConfigNode node)
        {
            if (!node.name.Equals("TOOLING_DEFINITION") || !node.HasValue("name"))
            {
                return;
            }

            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
