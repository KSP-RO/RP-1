using System;
using RP0.DataTypes;

namespace RP0
{
    public class LPConstruction : ConfigNodePersistenceBase, IConfigNode
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
    }
}
