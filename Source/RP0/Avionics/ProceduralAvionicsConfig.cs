using System;
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

        private Dictionary<string, ProceduralAvionicsTechNode> _techNodes;
        public Dictionary<string, ProceduralAvionicsTechNode> TechNodes
        {
            get
            {
                if (_techNodes == null)
                {
                    InitializeTechNodes();
                }
                return _techNodes;
            }
        }

        private ProceduralAvionicsTechNode[] _techNodesSorted;
        public ProceduralAvionicsTechNode[] TechNodesSorted
        {
            get
            {
                if (_techNodes == null)
                {
                    InitializeTechNodes();
                }
                return _techNodesSorted;
            }
        }

        public bool IsAvailable => TechNodes.Values.Any(node => node.IsAvailable);

        public void Load(ConfigNode node)
        {
            ProceduralAvionicsUtils.Log("Loading Config nodes");
            ConfigNode.LoadObjectFromConfig(this, node);
            _techNodes = new Dictionary<string, ProceduralAvionicsTechNode>();
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
                    _techNodes.Add(techNode.name, techNode);
                    ProceduralAvionicsUtils.Log("Loaded TechNode: " + techNode.name);
                }

                List<ProceduralAvionicsTechNode> techNodeList = _techNodes.Values.ToList();
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
            _techNodes = new Dictionary<string, ProceduralAvionicsTechNode>();
            var techNodeList = techNodesSerialized == null ? new List<ProceduralAvionicsTechNode>() : ObjectSerializer.Deserialize<List<ProceduralAvionicsTechNode>>(techNodesSerialized);
            _techNodesSorted = new ProceduralAvionicsTechNode[techNodeList.Count];
            foreach (var item in techNodeList)
            {
                ProceduralAvionicsUtils.Log("Deserialized " + item.name);
                _techNodes.Add(item.name, item);
                _techNodesSorted[item.techLevel] = item;
            }
            ProceduralAvionicsUtils.Log("Deserialized " + _techNodes.Count + " techNodes");
        }
    }
}
