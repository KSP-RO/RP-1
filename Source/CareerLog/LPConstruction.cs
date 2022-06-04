using System;

namespace RP0
{
    public class LPConstruction : IConfigNode
    {
        [Persistent]
        public double Cost;

        [Persistent]
        public Guid LPID;

        [Persistent]
        public Guid LCID;

        [Persistent]
        public Guid LCModID;

        public LPConstruction()
        {
        }

        public LPConstruction(ConfigNode n)
        {
            Load(n);
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
            node.TryGetValue(nameof(LPID), ref LPID);
            node.TryGetValue(nameof(LCID), ref LCID);
            node.TryGetValue(nameof(LCModID), ref LCModID);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
            node.AddValue(nameof(LPID), LPID);
            node.AddValue(nameof(LCID), LCID);
            node.AddValue(nameof(LCModID), LCModID);
        }
    }
}
