using System;

namespace KerbalConstructionTime
{
    public class LCConstructionStorageItem : ConstructionStorage
    {
        [Persistent]
        public int launchComplexID = 0;

        [Persistent]
        public bool isModify = false;

        [Persistent]
        public Guid modId;

        [Persistent]
        public LCItem.LCData lcData = new LCItem.LCData();

        public LCConstruction ToLCConstruction()
        {
            var lc = new LCConstruction();
            LoadFields(lc);
            lc.LaunchComplexIndex = launchComplexID;
            lc.IsModify = isModify;
            lc.ModID = modId;
            lc.LCData = new LCItem.LCData(lcData);

            return lc;
        }

        public LCConstructionStorageItem FromLCConstruction(LCConstruction lcc)
        {
            SaveFields(lcc);
            launchComplexID = lcc.LaunchComplexIndex;
            isModify = lcc.IsModify;
            modId = lcc.ModID;
            lcData = new LCItem.LCData(lcc.LCData);
            return this;
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            node.TryGetValue(nameof(modId), ref modId);
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            node.AddValue(nameof(modId), modId);
        }
    }
}
