namespace KerbalConstructionTime
{
    public class KCTCostModifier
    {
        [Persistent] public string name;
        [Persistent] public string displayName;
        [Persistent] public string desc;
        [Persistent] public float partMult = 1;
        [Persistent] public float globalMult = 1;
    }
}
