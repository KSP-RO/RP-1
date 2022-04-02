using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class LCConstructionStorageItem : ConstructionStorage
    {
        [Persistent]
        public int launchComplexID = 0;

        [Persistent]
        public bool isModify = false;
        

        [Persistent]
        public LCItem.LCData lcData = new LCItem.LCData();

        public LCConstruction ToLCConstruction()
        {
            var lc = new LCConstruction();
            LoadFields(lc);
            lc.LaunchComplexIndex = launchComplexID;
            lc.IsModify = isModify;
            lc.LCData = new LCItem.LCData(lcData);

            return lc;
        }

        public LCConstructionStorageItem FromLCConstruction(LCConstruction lcc)
        {
            SaveFields(lcc);
            launchComplexID = lcc.LaunchComplexIndex;
            isModify = lcc.IsModify;
            lcData = new LCItem.LCData(lcc.LCData);
            return this;
        }
    }
}
