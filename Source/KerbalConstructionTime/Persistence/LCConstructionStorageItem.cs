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

        // Back-compat: pass in the parent KSC
        public LCConstruction ToLCConstruction(KSCItem ksc)
        {
            var lcc = new LCConstruction();
            LoadFields(lcc);
            if (lcID == Guid.Empty)
            {
                LCItem lc = ksc.LaunchComplexes.Find(l => l.Name == lcData.Name && !l.IsOperational);
                if (lc == null)
                {
                    lc = ksc.LaunchComplexes.Find(l => l.MassOrig == lcData.massOrig && !l.IsOperational);
                }
                if (lc == null)
                {
                    throw new Exception($"[KCT] Failed to find LC for LCConstructionStorageItem with name {lcData.Name}");
                }
                lcID = lc.ID;
            }
            lcc.LCID = lcID;
            lcc.IsModify = isModify;
            lcc.ModID = modId;
            lcc.LCData = new LCItem.LCData(lcData);

            if (KCTGameStates.LoadedSaveVersion < KCTGameStates.VERSION)
            {
                if (KCTGameStates.LoadedSaveVersion < 8)
                {
                    if (lcc.ModID == Guid.Empty)
                    {
                        lcc.ModID = Guid.NewGuid();
                    }
                    else
                    {
                        lcc.ModID = lcc.LCID;
                    }
                }
                if (KCTGameStates.LoadedSaveVersion < 12)
                {
                    lcc.LCData.Name = KCTGameStates.FindLCFromID(lcc.LCID)?.Name ?? lcc.LCData.Name;
                    if (string.IsNullOrEmpty(lcc.Name))
                        lcc.Name = lcc.LCData.Name;
                }
            }

            return lcc;
        }

        public LCConstructionStorageItem FromLCConstruction(LCConstruction lcc)
        {
            SaveFields(lcc);
            isModify = lcc.IsModify;
            modId = lcc.ModID;
            lcID = lcc.LCID;
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
