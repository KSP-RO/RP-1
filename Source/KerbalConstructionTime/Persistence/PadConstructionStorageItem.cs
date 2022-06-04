using System;

namespace KerbalConstructionTime
{
    public class PadConstructionStorageItem : ConstructionStorage
    {
        [Persistent]
        public int launchpadID = 0;

        [Persistent]
        public Guid id;

        public PadConstruction ToPadConstruction()
        {
            var p = new PadConstruction();
            LoadFields(p);
            p.LaunchpadIndex = launchpadID;
            p.ID = id;
            return p;
        }

        public PadConstructionStorageItem FromPadConstruction(PadConstruction pc)
        {
            SaveFields(pc);
            launchpadID = pc.LaunchpadIndex;
            id = pc.ID;
            return this;
        }

        public override void Load(ConfigNode node)
        {
            base.Load(node);
            node.TryGetValue(nameof(id), ref id);
        }

        public override void Save(ConfigNode node)
        {
            base.Save(node);
            node.AddValue(nameof(id), id);
        }
    }
}
