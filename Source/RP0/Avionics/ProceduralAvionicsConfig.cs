﻿using System;
using System.Collections.Generic;
using UniLinq;
using ROUtils;

namespace RP0.ProceduralAvionics
{
    [Serializable]
    public class ProceduralAvionicsConfig : IConfigNode
    {
        [Persistent]
        public string name;

        [Persistent]
        public string description;

        public byte[] techNodesSerialized;

        private Dictionary<String, ProceduralAvionicsTechNode> techNodes;
        public Dictionary<String, ProceduralAvionicsTechNode> TechNodes
        {
            get
            {
                if (techNodes == null)
                {
                    InitializeTechNodes();
                }
                return techNodes;
            }
        }
        private ProceduralAvionicsTechNode[] techNodesSorted;
        public ProceduralAvionicsTechNode[] TechNodesSorted
        {
            get
            {
                if (techNodes == null)
                {
                    InitializeTechNodes();
                }
                return techNodesSorted;
            }
        }

        public void Load(ConfigNode node)
        {
            ProceduralAvionicsUtils.Log("Loading Config nodes");
            ConfigNode.LoadObjectFromConfig(this, node);
            techNodes = new Dictionary<string, ProceduralAvionicsTechNode>();
            if (name == null)
            {
                name = node.GetValue("name");
            }
            if (node.HasNode("TECHLIMIT"))
            {
                foreach (ConfigNode tNode in node.GetNodes("TECHLIMIT"))
                {
                    ProceduralAvionicsTechNode techNode = new ProceduralAvionicsTechNode();
                    techNode.Load(tNode);
                    techNodes.Add(techNode.name, techNode);
                    ProceduralAvionicsUtils.Log("Loaded TechNode: " + techNode.name);
                }

                List<ProceduralAvionicsTechNode> techNodeList = techNodes.Values.ToList();
                techNodesSerialized = ObjectSerializer.Serialize(techNodeList);
                ProceduralAvionicsUtils.Log("Serialized TechNodes");
            }
            else
            {
                ProceduralAvionicsUtils.Log("No technodes found for " + name);
            }
        }

        public void Save(ConfigNode node)
        {
            ConfigNode.CreateConfigFromObject(this, node);
        }

        public void InitializeTechNodes()
        {
            ProceduralAvionicsUtils.Log("TechNode deserialization needed");
            techNodes = new Dictionary<string, ProceduralAvionicsTechNode>();
            var techNodeList = techNodesSerialized == null ? new List<ProceduralAvionicsTechNode>() : ObjectSerializer.Deserialize<List<ProceduralAvionicsTechNode>>(techNodesSerialized);
            techNodesSorted = new ProceduralAvionicsTechNode[techNodeList.Count];
            foreach (var item in techNodeList)
            {
                ProceduralAvionicsUtils.Log("Deserialized " + item.name);
                techNodes.Add(item.name, item);
                techNodesSorted[item.techLevel] = item;
            }
            ProceduralAvionicsUtils.Log("Deserialized " + techNodes.Count + " techNodes");
        }
    }
}
