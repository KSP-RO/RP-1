namespace RP0
{
    public class KCTTechNodePeriod : RP0.DataTypes.ConfigNodePersistenceBase
    {
        [Persistent] public string id;
        [Persistent] public int startYear;
        [Persistent] public int endYear;
    }
}
