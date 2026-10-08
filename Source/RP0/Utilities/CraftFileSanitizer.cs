using System.Collections.Generic;
using System.Linq;

namespace RP0
{
    /// <summary>
    /// Strips flight-only PartModule state out of ship ConfigNodes that are written to .craft files.
    ///
    /// RP-1's vessel recovery deliberately carries flight scene state (accumulated engine run time,
    /// ignition counts, TestFlight reliability and failure bookkeeping, ...) over to the editor so that
    /// re-flying a recovered vessel keeps the same engine wear and engine refurbishment is costed. That
    /// state must never end up in a saved craft file though, otherwise building and launching that craft
    /// would start with used or even failed engines.
    ///
    /// The fields/nodes to reset are defined in the <c>SCMCraftFileModuleReset</c> config node so they
    /// can be tweaked without a recompile. A module reset is matched either by exact module name or by a
    /// name prefix (config entries ending in '*'); every matching reset is applied.
    /// </summary>
    public static class CraftFileSanitizer
    {
        private const string ConfigNodeName = "SCMCraftFileModuleReset";

        private class ModuleReset
        {
            public readonly List<KeyValuePair<string, string>> valuesToSet = new List<KeyValuePair<string, string>>();
            public readonly List<string> valuesToRemove = new List<string>();
            public readonly List<string> nodesToRemove = new List<string>();
        }

        private static Dictionary<string, ModuleReset> _exactResets;
        private static List<KeyValuePair<string, ModuleReset>> _prefixResets;

        private static void EnsureLoaded()
        {
            if (_exactResets != null)
                return;

            _exactResets = new Dictionary<string, ModuleReset>();
            _prefixResets = new List<KeyValuePair<string, ModuleReset>>();

            ConfigNode root = GameDatabase.Instance.GetConfigNodes(ConfigNodeName).FirstOrDefault();
            if (root == null)
                return;

            foreach (ConfigNode moduleNode in root.GetNodes("MODULE"))
            {
                string name = moduleNode.GetValue("name");
                if (string.IsNullOrEmpty(name))
                    continue;

                var reset = new ModuleReset();
                foreach (ConfigNode.Value val in moduleNode.values)
                {
                    switch (val.name)
                    {
                        case "name":
                            break;
                        case "removeValue":
                            reset.valuesToRemove.Add(val.value);
                            break;
                        case "removeNode":
                            reset.nodesToRemove.Add(val.value);
                            break;
                        default:
                            reset.valuesToSet.Add(new KeyValuePair<string, string>(val.name, val.value));
                            break;
                    }
                }

                if (name.EndsWith("*"))
                    _prefixResets.Add(new KeyValuePair<string, ModuleReset>(name.Substring(0, name.Length - 1), reset));
                else
                    _exactResets[name] = reset;
            }
        }

        /// <summary>
        /// Resets flight-only module state on every PART in the given ship node. Safe to call on
        /// freshly built (never-flown) ships: the reset values match a fresh part, so it is a no-op there.
        /// </summary>
        public static void StripFlightData(ConfigNode shipNode)
        {
            if (shipNode == null)
                return;

            EnsureLoaded();
            if (_exactResets.Count == 0 && _prefixResets.Count == 0)
                return;

            foreach (ConfigNode partNode in shipNode.GetNodes("PART"))
                StripNode(partNode);
        }

        private static void StripNode(ConfigNode node)
        {
            // Modules are matched by their "name" value rather than by the wrapping node's name,
            // since a persisted module can be nested under a node with an arbitrary name.
            string name = node.GetValue("name");
            if (!string.IsNullOrEmpty(name))
            {
                if (_exactResets.TryGetValue(name, out ModuleReset exact))
                    ApplyReset(node, exact);

                foreach (KeyValuePair<string, ModuleReset> kvp in _prefixResets)
                {
                    if (name.StartsWith(kvp.Key))
                        ApplyReset(node, kvp.Value);
                }
            }

            // Recurse through every child node: nested modules may be stored under any node name.
            foreach (ConfigNode child in node.GetNodes())
                StripNode(child);
        }

        private static void ApplyReset(ConfigNode node, ModuleReset reset)
        {
            foreach (KeyValuePair<string, string> kvp in reset.valuesToSet)
            {
                // Only reset fields that are actually present so the result matches a fresh part.
                if (node.HasValue(kvp.Key))
                    node.SetValue(kvp.Key, kvp.Value);
            }
            foreach (string v in reset.valuesToRemove)
                node.RemoveValues(v);
            foreach (string n in reset.nodesToRemove)
                node.RemoveNodes(n);
        }
    }
}
