
using System.Collections.Generic;

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
                }
            }

            private StageNode root;
            private Dictionary<Part, StageNode> rootDict;
            public StageTree(StageNode root, Dictionary<Part, StageNode> rootDict)
            {
                this.root = root;
                this.rootDict = rootDict;
            }

            public StageTree BuildTreeFromShipConstruct(ShipConstruct sc)
            {
                var parts = sc.Parts;
                Part root = parts[0];
                while (root.parent != null)
                {
                     root = root.parent;
                }
                return BuildTree(root);
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

            public StageTree BuildTree(Part root)
            {
                var stageDictionary = new Dictionary<Part, StageNode>();
                // One queue to store new stages, one to store parts in current stage
                Queue<StageNode> newStages = new Queue<StageNode>();
                Queue<Part> partsInStage = new Queue<Part>();
                var firstStage = new StageNode(root);
                newStages.Enqueue(firstStage);
                while (newStages.Count > 0)
                {
                    StageNode currentStage = newStages.Dequeue();
                    stageDictionary.Add(currentStage.stageRoot, currentStage);
                    partsInStage.Enqueue(currentStage.stageRoot);
                    while (partsInStage.Count > 0)
                    {
                        Part currentPart = partsInStage.Dequeue();
                        var decoupledParts = GetDecoupledParts(currentPart);

                        // list includes parent - put currentPart in new stage IF IT ISN'T THE TOP PART IN A STAGE
                        bool addPartToStage = true;
                        if (decoupledParts.Contains(currentPart.parent))
                        {
                            if (!stageDictionary.ContainsKey(currentPart))
                            {
                                // Do NOT add part to current stage!
                                var newStage = new StageNode(currentPart);
                                currentStage.childStages.Add(newStage);
                                newStages.Enqueue(newStage);
                                addPartToStage = false;
                            }
                        }

                        // another decoupled part - put in new stage
                        foreach (Part p in decoupledParts)
                        {
                            if (p != currentPart.parent)
                            {
                                var newStage = new StageNode(p);
                                currentStage.childStages.Add(newStage);
                                newStages.Enqueue(newStage);
                            }
                        }

                        // parts that are not decoupled - add to same stage queue
                        foreach (Part p in currentPart.children)
                        {
                            if (!decoupledParts.Contains(p))
                            {
                                partsInStage.Enqueue(p);
                            }
                        }

                        if (addPartToStage)
                        {
                            currentStage.stageParts.Add(currentPart);
                        }
                    }
                }
                return new StageTree(firstStage, stageDictionary);
            }

            // Dictionary<uint, Part> partMap = new Dictionary<uint, Part>();

            List<Part> GetDecoupledParts(Part part)
            {
                List<Part> decoupledParts = null;
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
                return decoupledParts;
            }
        }
    }
}