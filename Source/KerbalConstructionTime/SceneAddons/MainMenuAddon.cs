using UnityEngine;
using System.Collections.Generic;
using static RP0.MiscUtils;

namespace KerbalConstructionTime
{

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class MainMenuAddon : MonoBehaviour
    {
        private ResourceTagType _lastTag;

        public void Start()
        {
            KCTDebug.Log("MainMenuAddon Start called");

            // Subscribe to events from KSP and other mods.
            // This is done as early as possible for the scene change events to work when loading into a save from main menu.
            if (!KCTEvents.Instance.SubscribedToEvents)
            {
                KCTEvents.Instance.SubscribeToEvents();
            }

            var values = System.Enum.GetValues(typeof(ResourceTagType));
            _lastTag = (ResourceTagType)values.GetValue(values.Length - 1);

            // Apply tags to RF MEC configs
            foreach (var ap in PartLoader.LoadedPartsList)
            {
                var part = ap.partPrefab;
                if (part == null)
                    continue;
                for (int i = 0; i < part.Modules.Count; ++i)
                {
                    var m = part.Modules[i];
                    if (m is RealFuels.ModuleEngineConfigsBase mecb)
                    {
                        foreach (var n in mecb.configs)
                        {
                            ApplyTagToConfig(n);
                            foreach (var s in n.GetNodes("SUBCONFIG"))
                            {
                                ApplyTagToConfig(s);
                            }
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
