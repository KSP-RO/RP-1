using UnityEngine;
using System.Collections.Generic;
using KSP.UI.Screens;
using UniLinq;

namespace RP0
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class HideEmptyNodes : MonoBehaviour
    {
        private class RDNodeData
        {
            public List<RDNode> children = new List<RDNode>();
        }

        private Dictionary<RDNode, RDNodeData> nodeData = new Dictionary<RDNode, RDNodeData>();
        private HashSet<RDNode> nonEmptyNodes = new HashSet<RDNode>();
        private HashSet<RDNode> deleteNodes = new HashSet<RDNode>();

        public void Awake()
        {
            DontDestroyOnLoad(this);

            RDTechTree.OnTechTreeSpawn.Add(OnUpdateRnD);
        }

        public void Destroy()
        {
            RDTechTree.OnTechTreeSpawn.Remove(OnUpdateRnD);
        }

        private void OnUpdateRnD(RDTechTree tree)
        {
            ProcessTree(tree);

            nodeData.Clear();
            nonEmptyNodes.Clear();
            deleteNodes.Clear();
        }

        private void ProcessTree(RDTechTree tree)
        {
            // pass 1: set up dict and non-empty node set
            foreach (var node in tree.controller.nodes)
            {
                nodeData[node] = new RDNodeData();
                RDTech tech = node.GetComponent<RDTech>();
                if (tech == null)
                {
                    Debug.LogError($"[RP-0]: Error, can't find RDTech component on {node.name} with description {node.description}");
                    return;
                }
                if (tech.partsAssigned.Any(ap => !ap.TechHidden) || PartUpgradeManager.Handler.GetUpgradesForTech(tech.techID).Count > 0)
                    nonEmptyNodes.Add(node);
            }

            // pass 2: fill children -- we need to do this ourselves, because
            // it's before the node is registered (which happens during setup,
            // at the same time the arrows are drawn).
            foreach (var node in tree.controller.nodes)
            {
                foreach (var p in node.parents)
                {
                    RDNodeData parentData;
                    if (!nodeData.TryGetValue(p.parent.node, out parentData))
                    {
                        Debug.LogError($"[RP-0]: Error! Null parent node for node {node.GetComponent<RDTech>().techID}");
                        return;
                    }

                    parentData.children.Add(node);
                }
            }

            // pass 3: Figure out what nodes to kill.
            foreach (var node in tree.controller.nodes)
            {
                ComputeDeletedNodes(node);
            }

            for (int i = tree.controller.nodes.Count - 1; i >= 0; --i)
            {
                if (deleteNodes.Contains(tree.controller.nodes[i]))
                {
                    GameObject.Destroy(tree.controller.nodes[i]);
                    tree.controller.nodes.RemoveAt(i);
                }
            }
        }

        private bool ComputeDeletedNodes(RDNode node)
        {
            // If this node is non-empty itself, bail.
            if (nonEmptyNodes.Contains(node))
                return false;

            // If this node has already been processed as a delete, bail.
            if (deleteNodes.Contains(node))
                return true;

            // Otherwise, recurse.
            RDNodeData data = nodeData[node];
            foreach (var c in data.children)
                if (!ComputeDeletedNodes(c))
                    return false;

            // We have now established that this node has
            // no non-empty children, so we can add this
            // node to the set to be deleted.
            deleteNodes.Add(node);

            // Return if we're not at the top level.
            return true;
        }
    }
}
