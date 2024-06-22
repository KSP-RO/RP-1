using System.Collections.Generic;
using UnityEngine;
using ROUtils.DataTypes;

namespace RP0
{
    public class ModuleTagList : PartModule
    {
        public const string PadInfrastructure = "PadInfrastructure";

        [KSPField(isPersistant = true)]
        public PersistentListValueType<string> engineTags = new PersistentListValueType<string>();

        [KSPField(isPersistant = true)]
        public PersistentListValueType<string> tankTags = new PersistentListValueType<string>();

        [SerializeField] public List<string> tags;

        public bool HasPadInfrastructure => tags.Contains(PadInfrastructure);

        public override void OnLoad(ConfigNode node)
        {
            if (node.name != "CURRENTUPGRADE")
            {
                if (HighLogic.LoadedScene == GameScenes.LOADING)
                {
                    tags = node.GetValuesList("tag");
                    tags.Sort();
                }
                else
                {
                    tags = part.partInfo.partPrefab.GetComponent<ModuleTagList>()?.tags;
                }
            }
            else
            {
                tags.AddRange(node.GetValuesList("tag"));
                tags.Sort();
            }
        }

        public override string GetInfo()
        {
            var str = StringBuilderCache.Acquire();
            List<string> combinedTags = new List<string>();
            combinedTags.AddRange(tags);
            combinedTags.AddRange(engineTags);
            combinedTags.AddRange(tankTags);
            combinedTags.Sort();
            foreach (var x in combinedTags)
            {
                if (Database.KCTCostModifiers.TryGetValue(x, out var mod))
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

        public void UpdateEngineTags(ConfigNode node)
        {
            engineTags.Clear();
            // the config node will be correct regardless of which type of MEC it is
            foreach (var v in node._values.values)
                if (v.name == "tag")
                    engineTags.AddUnique(v.value);
        }

        public void UpdateTankTags(string type)
        {
            tankTags.Clear();
            if (!RealFuels.MFSSettings.tankDefinitions.TryGetValue(type, out var td))
                return;
            foreach (var s in td.tags)
                tankTags.AddUnique(s);
        }

        private static PersistentListValueType<string> _tempTags = new PersistentListValueType<string>();

        public static List<string> GetTags(object p)
        {
            List<string> combinedTags = new List<string>();
            if (p is Part part)
            {
                ModuleTagList mod = part.FindModuleImplementing<ModuleTagList>();
                if (mod == null)
                    return null;

                combinedTags.AddRange(mod.tags);
                combinedTags.AddRange(mod.engineTags);
                combinedTags.AddRange(mod.tankTags);
                combinedTags.Sort();
                return combinedTags;
            }
            else if (p is ConfigNode cn)
            {
                string name = KCTUtilities.GetPartNameFromNode(cn);
                Part partRef = KCTUtilities.GetAvailablePartByName(name).partPrefab;

                ModuleTagList mod = partRef.FindModuleImplementing<ModuleTagList>();
                if (mod == null)
                    return null;

                combinedTags.AddRange(mod.tags);

                foreach (var node in cn._nodes.nodes)
                {
                    if (node.name != "MODULE")
                        continue;
                    if (node.GetValue("name") != "ModuleTagList")
                        continue;

                    foreach (var n in node.nodes.nodes)
                    {
                        if (n.name != nameof(engineTags) && n.name != nameof(tankTags))
                            continue;
                        _tempTags.Load(n);
                        combinedTags.AddRange(_tempTags);
                    }
                    
                    _tempTags.Clear();
                    combinedTags.Sort();
                    return combinedTags;
                }
            }
            return null;
        }
    }
}
