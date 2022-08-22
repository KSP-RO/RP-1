using HarmonyLib;
using System.Collections.Generic;
using System;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(ConfigNode))]
    internal class PatchConfigNode
    {
        internal struct LoadState
        {
            public bool wasRemove;
            public ConfigNode.ReadLinkList links;
            public LoadState(bool r, ConfigNode.ReadLinkList l)
            {
                wasRemove = r;
                links = l;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("CreateConfigFromObject")]
        [HarmonyPatch(new System.Type[] { typeof(object), typeof(int), typeof(ConfigNode) })]
        internal static void Prefix_CreateConfigFromObject(object obj, int pass, ConfigNode node, ref ConfigNode __result, out ConfigNode.WriteLinkList __state)
        {
            // This will fail if nested, so we have to cache off the old value of this.
            __state = ConfigNode.writeLinks;
        }

        [HarmonyPostfix]
        [HarmonyPatch("CreateConfigFromObject")]
        [HarmonyPatch(new System.Type[] { typeof(object), typeof(int), typeof(ConfigNode) })]
        internal static void Postfix_CreateConfigFromObject(ConfigNode.WriteLinkList __state)
        {
            ConfigNode.writeLinks = __state;
        }

        [HarmonyPrefix]
        [HarmonyPatch("LoadObjectFromConfig")]
        [HarmonyPatch(new System.Type[] { typeof(object), typeof(ConfigNode), typeof(int), typeof(bool) })]
        internal static void Prefix_LoadObjectFromConfig(out LoadState __state)
        {
            // We have to cache off the old static values because they get clobbered, so this fails on nested calls.
            __state = new LoadState(ConfigNode.removeAfterUse, ConfigNode.readLinks);
        }

        [HarmonyPostfix]
        [HarmonyPatch("LoadObjectFromConfig")]
        [HarmonyPatch(new System.Type[] { typeof(object), typeof(ConfigNode), typeof(int), typeof(bool) })]
        internal static void Postfix_LoadObjectFromConfig(LoadState __state)
        {
            ConfigNode.removeAfterUse = __state.wasRemove;
            ConfigNode.readLinks = __state.links;
        }
    }
}
