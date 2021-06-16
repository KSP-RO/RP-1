using System.Collections.Generic;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class ModuleTagList : PartModule
    {
        [SerializeField] public List<string> tags;

        public override void OnLoad(ConfigNode node)
        {
            if (node.name != "CURRENTUPGRADE")
                tags = HighLogic.LoadedScene == GameScenes.LOADING ? node.GetValuesList("tag") : part.partInfo.partPrefab.GetComponent<ModuleTagList>()?.tags;
            else
                tags.AddRange(node.GetValuesList("tag"));
        }

        public override string GetInfo()
        {
            var str = StringBuilderCache.Acquire();
            foreach (var x in tags)
            {
                if (KerbalConstructionTime.KCTCostModifiers.TryGetValue(x, out var mod))
                {
                    str.AppendLine($"<b><color=green>{mod.displayName}</color></b>");
                    str.AppendLine(mod.desc);
                    if (mod.partMult != 1)
                        str.AppendLine($"<b><color=orange>Launch Cost: Part cost * {mod.partMult:F2}</color></b>");
                    if (mod.globalMult != 1)
                        str.AppendLine($"<b><color=orange>Launch Cost: ALL costs * {mod.globalMult:F2}</color></b>");
                    str.AppendLine();
                } else
                {
                    str.AppendLine($"<b><color=orange>{x}</color></b>\nUnconfigured Tag!\n");
                }
            }
            string s = str.ToStringAndRelease();
            return !string.IsNullOrEmpty(s) ? s : "None Specified";
        }
    }
}
