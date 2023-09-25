﻿namespace RP0
{
    public class KCTCostModifier : DataTypes.ConfigNodePersistenceBase
    {
        [Persistent] public string name;
        [Persistent] public string displayName;
        [Persistent] public string desc;
        [Persistent] public float partMult = 1;
        [Persistent] public float globalMult = 1;
        [Persistent] public bool isHumanRating = false;
    }
}
