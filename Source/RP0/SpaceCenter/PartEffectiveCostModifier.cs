using ROUtils.DataTypes;

namespace RP0
{
    public class PartEffectiveCostModifier : ConfigNodePersistenceBase
    {
        [Persistent] public string name;
        [Persistent] public string displayName;
        [Persistent] public string desc;
        [Persistent] public float partMult = 1;
        [Persistent] public float globalMult = 1;
        [Persistent] public bool isHumanRating = false;
    }
}
