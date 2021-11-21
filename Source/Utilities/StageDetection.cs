
using System.Collections.Generic;

using UnityEngine;

namespace RP0
{
    public class StageDetection
    {
        public class StageTree
        {
            public class StageNode
            {
                public Part stageRoot;
                public List<StageNode> childStages;
                public List<Part> stageParts;
                public StageNode(Part stageRoot)
                {
                    this.stageRoot = stageRoot;
                    this.stageParts = new List<Part>();
                    this.childStages = new List<StageNode>();
                }
            }

            private StageNode root;
            private Dictionary<Part, StageNode> rootDict;
            public StageTree(StageNode root, Dictionary<Part, StageNode> rootDict)
            {
                this.root = root;
                this.rootDict = rootDict;
            }

            

            public StageNode PartBelongsTo(Part part)
            {
                while (part.parent != null)
                {
                    if(rootDict.TryGetValue(part, out StageNode stage))
                    {
                        return stage;
                    }
                    part = part.parent;
                }
                return null;
            }

            public void printTree()
            {
                Debug.Log("[RP-0] Stage detection: Printing Stage Tree:");
                printLine(root, "", false);

                void printLine(StageNode stage, string prefix, bool connector)
                {
                    Debug.Log(prefix+(connector?"|-":"")+"["+stage.stageRoot.name+"]");
                    foreach (Part p in stage.stageParts)
                    {
                        if (p!=stage.stageRoot)
                        {
                            Debug.Log(prefix+(connector?"| |-":"|-")+p.name);
                        }
                    }
                    foreach (StageNode child in stage.childStages)
                    {
                        printLine(child, prefix+"| ", true);
                    }
                }
            }
        }
        public StageTree BuildTreeFromShipConstruct(ShipConstruct sc)
            {
                var parts = sc.Parts;
                Debug.Log("[RP-0] Stage detection: part count: "+parts.Count);
                Part root = parts[0];
                while (root.parent != null)
                {
                     root = root.parent;
                }
                return BuildTree(root);
            }

            public StageTree BuildTree(Part root)
            {
                Debug.Log("[RP-0] Stage detection: Started BuildTree with root: "+root.name);
                var stageDictionary = new Dictionary<Part, StageTree.StageNode>();
                // One queue to store new stages, one to store parts in current stage
                Queue<StageTree.StageNode> newStages = new Queue<StageTree.StageNode>();
                Queue<Part> partsInStage = new Queue<Part>();
                var firstStage = new StageTree.StageNode(root);
                newStages.Enqueue(firstStage);
                // Debug.Log("[RP-0] Stage detection: Got to while loop");
                while (newStages.Count > 0)
                {
                    // Debug.Log("[RP-0] Stage detection: Started new run through while loop");
                    StageTree.StageNode currentStage = newStages.Dequeue();
                    stageDictionary.Add(currentStage.stageRoot, currentStage);
                    partsInStage.Enqueue(currentStage.stageRoot);
                    // Debug.Log("[RP-0] Stage detection: Got to inner while loop");
                    while (partsInStage.Count > 0)
                    {
                        Part currentPart = partsInStage.Dequeue();
                        var decoupledParts = GetDecoupledParts(currentPart);

                        // list includes parent - put currentPart in new stage IF IT ISN'T THE TOP PART IN A STAGE
                        bool addPartToStage = true;
                        // Debug.Log("[RP-0] Stage detection: Got to Parent check");
                        if (decoupledParts.Contains(currentPart.parent))
                        {
                            if (!stageDictionary.ContainsKey(currentPart))
                            {
                                // Do NOT add part to current stage!
                                var newStage = new StageTree.StageNode(currentPart);
                                currentStage.childStages.Add(newStage);
                                newStages.Enqueue(newStage);
                                addPartToStage = false;
                            }
                        }

                        // Debug.Log("[RP-0] Stage detection: Got past Parent check");


                        // another decoupled part - put in new stage
                        foreach (Part p in decoupledParts)
                        {
                            if (p != currentPart.parent)
                            {
                                var newStage = new StageTree.StageNode(p);
                                currentStage.childStages.Add(newStage);
                                newStages.Enqueue(newStage);
                            }
                        }

                        // Debug.Log("[RP-0] Stage detection: Got past decoupled parts loop");

                        // parts that are not decoupled - add to same stage queue
                        if (addPartToStage)
                        {
                            foreach (Part p in currentPart.children)
                            {
                                if (!decoupledParts.Contains(p))
                                {
                                    partsInStage.Enqueue(p);
                                }
                            }
                        }

                        // Debug.Log("[RP-0] Stage detection: Got past queueing of parts");

                        if (addPartToStage)
                        {
                            currentStage.stageParts.Add(currentPart);
                        }
                        // Debug.Log("[RP-0] Stage detection: Survived inner while loop");
                    }
                }
                return new StageTree(firstStage, stageDictionary);
            }
            public List<Part> GetDecoupledParts(Part part)
            {
                // Debug.Log("[RP-0] Stage detection: Entered GetDecoupledParts");
                List<Part> decoupledParts = new List<Part>();
                if(part.TryGetComponent<IStageSeparator>(out IStageSeparator separator))
                {
                    AttachNode node = null;
                    if (separator is ModuleAnchoredDecoupler) {
                        if (!(separator as ModuleAnchoredDecoupler).stagingEnabled) {
                            return null;
                        }
                        node = (separator as ModuleAnchoredDecoupler).ExplosiveNode;
                    } else if (separator is ModuleDecouple) {
                        if (!(separator as ModuleDecouple).stagingEnabled) {
                            return null;
                        }
                        node = (separator as ModuleDecouple).ExplosiveNode;
                    // } else if (separator is ModuleDockingNode) {
                    //     // if referenceNode.attachedPart is not null, then the port
                    //     // was attached in the editor and may be separated later,
                    //     // otherwise, need to check for the port having been docked.
                    //     // if referenceNode itself is null, then the port cannot be
                    //     // docked in the editor (eg, inline docking port)
                    //     var port = separator as ModuleDockingNode;
                    //     Part otherPart = null;
                    //     if (partMap.TryGetValue (port.dockedPartUId, out otherPart)) {
                    //         if (port.vesselInfo != null) {
                    //             var vi = port.vesselInfo;
                    //             cp.Add (otherPart, vi.name);
                    //             return;
                    //         }
                    //     }
                    //     node = port.referenceNode;
                    //     if (node == null) {
                    //         //Debug.LogFormat ("[RMResourceManager] docking port null");
                    //         return;
                    //     }
                    }
                    if (node == null) {
                        // separators detach on both ends (and all surface attachments?)
                        // and thus don't keep track of the node(s), so return the parent
                        Part p = (separator as PartModule).part;
                        if (p.parent != null) {
                            // cp.Add (p.parent, "separator");
                            decoupledParts.Add(p.parent);
                        }
                        return decoupledParts;
                    }
                    if (node.attachedPart != null) {
                        // cp.Add (node.attachedPart, "decoupler");
                        decoupledParts.Add(node.attachedPart);
                    }
                }
                // Debug.Log("[RP-0] Stage detection: Survived GetDecoupledParts");
                return decoupledParts;
            }
    }
}