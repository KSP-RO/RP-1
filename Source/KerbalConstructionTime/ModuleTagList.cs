using System.Collections.Generic;
using UnityEngine;

namespace KerbalConstructionTime
{
    public class ModuleTagList : PartModule
    {
        public const string PadInfrastructure = "PadInfrastructure";

        [KSPField]
        public bool isDynamic = false;

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
            foreach (var x in tags)
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

        public static List<string> GetTags(object p)
        {
            if (p is Part part)
            {
                ModuleTagList mod = part.FindModuleImplementing<ModuleTagList>();
                if (mod == null)
                    return null;

                if (!mod.isDynamic)
                    return mod.tags;

                List<string> list = new List<string>();
                list.AddRange(mod.tags);
                for (int i = 0; i < part.Modules.Count; ++i)
                {
                    PartModule m = part.Modules[i];
                    if (m is RealFuels.ModuleEngineConfigsBase mecb)
                    {
                        // the config node will be correct regardless of which type of MEC it is
                        foreach (var s in mecb.config.GetValuesList("tag"))
                            list.AddUnique(s);
                    }
                    else if (m is RealFuels.Tanks.ModuleFuelTanks mft)
                    {
                        if (!RealFuels.MFSSettings.tankDefinitions.TryGetValue(mft.type, out var td))
                            continue;
                        foreach (var s in td.tags)
                            list.AddUnique(s);
                    }
                }
                list.Sort();
                return list;
            }

            if (p is ConfigNode cn)
            {
                string name = Utilities.GetPartNameFromNode(cn);
                Part partRef = Utilities.GetAvailablePartByName(name).partPrefab;

                ModuleTagList mod = partRef.FindModuleImplementing<ModuleTagList>();
                if (mod == null)
                    return null;

                if (!mod.isDynamic)
                    return mod.tags;

                List<string> list = new List<string>();
                list.AddRange(mod.tags);

                int nextIdx = 0;
                for (int j = 0; j < partRef.Modules.Count; ++j)
                {
                    PartModule m = partRef.Modules[j];
                    string mName = string.Empty;
                    bool isEng = false;
                    if (m is RealFuels.ModuleEngineConfigsBase)
                    {
                        mName = m.GetType().ToString();
                        isEng = true;
                    }
                    else if (m is RealFuels.Tanks.ModuleFuelTanks)
                    {
                        mName = m.GetType().ToString();
                        isEng = false;
                    }

                    if (mName == string.Empty)
                        continue;

                    // Find matching module in nodes
                    for (int i = nextIdx; i < cn.nodes.Count; ++i)
                    {
                        ConfigNode n = cn.nodes[i];
                        if (n.name != "MODULE")
                            continue;

                        string nName = n.GetValue("name");
                        if (nName == mName)
                        {
                            nextIdx = i + 1;

                            if (isEng)
                            {
                                string config = n.GetValue("configuration");
                                RealFuels.ModuleEngineConfigsBase mecb = m as RealFuels.ModuleEngineConfigsBase;
                                ConfigNode cfg = mecb.configs.Find(c => c.GetValue("name") == config);
                                if (cfg != null)
                                {
                                    bool useBase = true;
                                    if (mecb is RealFuels.ModuleEngineConfigs mec && n.GetValue("activePatchName") is string patch && !string.IsNullOrEmpty(patch))
                                    {
                                        List<ConfigNode> subNodes = new List<ConfigNode>(cfg.GetNodes("SUBCONFIG"));
                                        if (subNodes.Find(s => s.GetValue("name") == patch) is ConfigNode subCfg)
                                        {
                                            foreach (var s in subCfg.GetValuesList("tag"))
                                            {
                                                useBase = false;
                                                list.AddUnique(s);
                                            }
                                        }
                                    }
                                    if (useBase)
                                    {
                                        foreach (var s in cfg.GetValuesList("tag"))
                                            list.AddUnique(s);
                                    }
                                }
                            }
                            else
                            {
                                string type = n.GetValue("type");
                                if (!string.IsNullOrEmpty(type) && RealFuels.MFSSettings.tankDefinitions.TryGetValue(type, out var td))
                                {
                                    foreach (var s in td.tags)
                                        list.AddUnique(s);
                                }
                            }

                            break;
                        }
                    }
                }
                list.Sort();
                return list;
            }

            return null;
        }
    }
}
