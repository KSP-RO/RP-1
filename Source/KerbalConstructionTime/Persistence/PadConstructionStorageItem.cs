using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class PadConstructionStorageItem : ConstructionStorage
    {
        [Persistent]
        public int launchpadID = 0;
        
        public PadConstruction ToPadConstruction()
        {
            var p = new PadConstruction();
            LoadFields(p);
            p.LaunchpadIndex = launchpadID;
            return p;
        }

        public PadConstructionStorageItem FromPadConstruction(PadConstruction pc)
        {
            SaveFields(pc);
            launchpadID = pc.LaunchpadIndex;
            return this;
        }
    }
}
