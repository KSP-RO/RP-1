using UnityEngine;
using System.Collections.Generic;
using KSP.UI.Screens;
using UniLinq;
using ROUtils;

namespace RP0
{
    public class RFTagApplier : HostedSingleton
    {
        private ResourceTagType _lastTag;
        
        public RFTagApplier(SingletonHost host) : base(host) { }

        public override void Awake()
        {
            var values = System.Enum.GetValues(typeof(ResourceTagType));
            _lastTag = (ResourceTagType)values.GetValue(values.Length - 1);

            // Apply tags to RF MEC configs
            foreach (var ap in PartLoader.LoadedPartsList)
            {
                var part = ap.partPrefab;
                if (part == null)
                    continue;

                ModuleTagList mtl = null;
                for (int i = 0; i < part.Modules.Count; ++i)
                {
                    if (part.Modules[i] is ModuleTagList m)
                    {
                        mtl = m;
                        break;
                    }
                }
                if (mtl == null)
                    continue;

                // Special handling: sometimes we add engines to CMs or SMs
                // and we need to not further increase their Effective Cost
                if (mtl.tags.Contains("NoResourceCostMult"))
                    continue;

                bool found = false;
                for (int i = 0; i < part.Modules.Count; ++i)
                {
                    var m = part.Modules[i];
                    if (m is RealFuels.ModuleEngineConfigsBase mecb)
                    {
                        found = true;
                        foreach (var n in mecb.configs)
                        {
                            ApplyTagToConfig(n);
                            foreach (var s in n.GetNodes("SUBCONFIG"))
                            {
                                ApplyTagToConfig(s);
                            }
                        }
                        // do the same for current config so we can reload
                        ApplyTagToConfig(mecb.config);
                        mtl.UpdateEngineTags(mecb.config);
                    }
                    else if (m is RealFuels.Tanks.ModuleFuelTanks mft)
                    {
                        found = true;
                        mtl.UpdateTankTags(mft.type);
                    }
                }

                // Update moduleinfo
                if (found)
                {
                    string mName = mtl.GUIName ?? KSPUtil.PrintModuleName(mtl.moduleName);
                    foreach (var mi in ap.moduleInfos)
                    {
                        if (mi.moduleName == mName)
                        {
                            mi.info = mtl.GetInfo().Trim();
                            if (mtl.showUpgradesInModuleInfo && mtl.HasUpgrades())
                            {
                                mi.info = mi.info + "\n" + mtl.PrintUpgrades();
                            }
                            break;
                        }
                    }
                }
            }
        }

        private void ApplyTagToConfig(ConfigNode node)
        {
            ResourceTagType flags = ResourceTagType.None;
            foreach (var n in node._nodes.nodes)
            {
                if (n.name == "PROPELLANT")
                {
                    string res = n.GetValue("name");
                    flags |= Database.ResourceInfo.ResourceTagTypes.ValueOrDefault(res);
                }
            }
            if (flags == ResourceTagType.None)
                return;

            var tags = node.GetValuesList("tag");
            for (var t = (ResourceTagType)1; t <= _lastTag; ++t)
            {
                if ((flags & t) != 0)
                {
                    string s = t.ToString();
                    if (!tags.Contains(s))
                        node.AddValue("tag", s);
                }
            }
        }
    }
}
