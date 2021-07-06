namespace KerbalConstructionTime
{
    public class KCTVesselData : IConfigNode
    {
        [Persistent]
        public EditorFacility FacilityBuiltIn;

        [Persistent]
        public string VesselID = string.Empty;

        public static KCTVesselData Parse(KCTVesselTracker d)
        {
            return new KCTVesselData
            {
                FacilityBuiltIn = d.Data.FacilityBuiltIn,
                VesselID = d.Data.VesselID
            };
        }

        public KCTVesselData()
        {
        }

        public KCTVesselData(ConfigNode n)
        {
            Load(n);
        }

        public void Load(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }
    }
}
