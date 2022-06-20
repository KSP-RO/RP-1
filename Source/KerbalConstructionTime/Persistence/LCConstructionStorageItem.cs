using System;

namespace KerbalConstructionTime
{
    public class LCConstructionStorageItem : ConstructionStorage
    {
        [Persistent]
        public bool isModify = false;

        [Persistent]
        public Guid modId;

        [Persistent]
        public Guid lcID;

        [Persistent]
        public LCItem.LCData lcData = new LCItem.LCData();

        public LCConstruction ToLCConstruction()
        {
            var lcc = new LCConstruction();
            LoadFields(lcc);
            lcc.LCID = lcID;
            lcc.IsModify = isModify;
            lcc.ModID = modId;

            lcc.LCData = new LCItem.LCData(lcData);

            return lcc;
        }

        public LCConstructionStorageItem FromLCConstruction(LCConstruction lcc)
        {
            SaveFields(lcc);
            isModify = lcc.IsModify;
            modId = lcc.ModID;
            lcData = new LCItem.LCData(lcc.LCData);
            return this;
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            node.TryGetValue(nameof(modId), ref modId);
            node.TryGetValue(nameof(lcID), ref lcID);
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            node.AddValue(nameof(modId), modId);
            node.AddValue(nameof(lcID), lcID);
        }
    }
}
