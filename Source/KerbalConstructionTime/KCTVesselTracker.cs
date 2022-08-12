using System;
using System.Collections;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace KerbalConstructionTime
{
    /// <summary>
    /// A VesselModule for keeping persistent vessel data across decouplings, dockings, recoveries and edits inside the VAB/SPH.
    /// </summary>
    public class KCTVesselTracker : VesselModule
    {
        public KCTVesselData Data = new KCTVesselData();
        public Dictionary<uint, KCTVesselData> DockedVesselData;

        protected override void OnStart()
        {
            base.OnStart();

            if (Data.VesselID == string.Empty)
            {
                Data.VesselID = Guid.NewGuid().ToString("N");
            }
        }

        protected override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            foreach (ConfigNode n in node.GetNodes("DATA"))
            {
                Data.Load(n);
            }

            foreach (ConfigNode n in node.GetNodes("DOCKED_DATA"))
            {
                uint launchID = default;
                if (!n.TryGetValue("launchID", ref launchID)) continue;

                DockedVesselData = DockedVesselData ?? new Dictionary<uint, KCTVesselData>();
                DockedVesselData[launchID] = new KCTVesselData(n);
            }
        }

        protected override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            var n = node.AddNode("DATA");
            Data.Save(n);

            if (DockedVesselData != null)
            {
                foreach (KeyValuePair<uint, KCTVesselData> kvp in DockedVesselData)
                {
                    var dn = node.AddNode("DOCKED_DATA");
                    kvp.Value.Save(dn);
                    dn.AddValue("launchID", kvp.Key);
                }
            }
        }
    }
}
