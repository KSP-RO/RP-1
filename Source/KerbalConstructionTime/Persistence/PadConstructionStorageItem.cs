using System;

namespace KerbalConstructionTime
{
    public class PadConstructionStorageItem : ConstructionStorage
    {
        [Persistent]
        [Obsolete("Remove this after a week when everything is Guid'd up")]
        public int launchpadID = 0;

        [Persistent]
        public Guid id;

        // Back-Compat pass in the parent LC
        public PadConstruction ToPadConstruction(LCItem lc)
        {
            var p = new PadConstruction();
            LoadFields(p);

            if (id == Guid.Empty)
            {
                id = lc.LaunchPads[launchpadID].id;
            }

            p.ID = id;
            return p;
        }

        public PadConstructionStorageItem FromPadConstruction(PadConstruction pc)
        {
            SaveFields(pc);
            launchpadID = pc.LC.LaunchPads.IndexOf(pc.LC.LaunchPads.Find( p => p.id == pc.ID));
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
