using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Kerf.Model;

namespace Kerf.Tools
{
    /// <summary>
    /// Structured result of an insertion target resolution.
    /// Specifies the target container (Frame or Group, or null for root canvas)
    /// and the zero-based insertion index.
    /// </summary>
    public class InsertionTargetResult
    {
        /// <summary>
        /// Resolved target container. If null, targets the root canvas (Page.Layers).
        /// </summary>
        public LayerNode Container { get; set; }

        /// <summary>
        /// Zero-based insertion index inside Container.Children (or Page.Layers).
        /// -1 indicates append to the end.
        /// </summary>
        public int InsertionIndex { get; set; }

        /// <summary>
        /// The reference layer that established this insertion context (e.g. selected child or container).
        /// </summary>
        public LayerNode ContextReferenceNode { get; set; }

        public bool IsRootCanvas
        {
            get { return Container == null; }
        }

        public InsertionTargetResult()
        {
            Container = null;
            InsertionIndex = -1;
            ContextReferenceNode = null;
        }

        public InsertionTargetResult(LayerNode container, int insertionIndex, LayerNode contextRef = null)
        {
            Container = container;
            InsertionIndex = insertionIndex;
            ContextReferenceNode = contextRef;
        }
    }

    /// <summary>
    /// Centralized Insertion Target Resolver.
    /// Implements Figma-like deterministic container targeting when creating, pasting,
    /// duplicating, or inserting objects into Frames, Groups, and Auto Layout containers.
    /// Coordinates strictly with selection depth and hierarchy context without arbitrary timers.
    /// </summary>
    public static class InsertionTargetResolver
    {
        /// <summary>
        /// Determines whether a node is eligible to serve as an insertion container.
        /// Container must be non-null, visible, unlocked, and of type Frame or Group.
        /// </summary>
        public static bool IsContainerEligible(LayerNode node)
        {
            if (node == null) return false;
            if (!node.IsVisible || node.IsLocked) return false;
            return node.Type == ElementType.Frame || node.Type == ElementType.Group;
        }

        /// <summary>
        /// Validates that an object can be inserted into the container.
        /// Prevents self-insertion and cyclic ancestry.
        /// </summary>
        public static bool IsObjectCompatible(LayerNode container, LayerNode objectToInsert)
        {
            if (objectToInsert == null) return false;
            if (container == null) return true; // Root canvas supports any object
            if (container == objectToInsert) return false; // Cannot insert container into itself
            if (Kerf.Model.Page.IsDescendant(objectToInsert, container)) return false; // Cycle prevention
            return true;
        }

        /// <summary>
        /// Resolves the active insertion container and insertion index based on current selection,
        /// hierarchy depth, and optional explicit context.
        ///
        /// Rules:
        /// 1. If explicitContext is provided and eligible, target is explicitContext.
        /// 2. If currentSelection is non-empty:
        ///    - If primary selected node is an eligible container (Frame or Group):
        ///      -> Target is that container! (Appends at end of its children).
        ///    - If primary selected node is a child element inside a container:
        ///      -> Target is the child's parent container!
        ///      -> Insertion index is immediately after the selected child!
        ///    - If the resolved container is locked or hidden:
        ///      -> Climbs up to nearest unlocked, visible container ancestor.
        /// 3. If currentSelection is empty:
        ///    -> Target is root canvas (null).
        /// </summary>
        public static InsertionTargetResult ResolveTarget(
            Page page,
            IList<LayerNode> currentSelection,
            LayerNode explicitContext = null)
        {
            if (page == null) return new InsertionTargetResult(null, -1);

            // 1. Explicit Insertion Context
            if (explicitContext != null && IsContainerEligible(explicitContext))
            {
                int defaultIdx = explicitContext.Children != null ? explicitContext.Children.Count : 0;
                return new InsertionTargetResult(explicitContext, defaultIdx, explicitContext);
            }

            // 2. Active Selection Context
            if (currentSelection != null && currentSelection.Count > 0)
            {
                var primary = currentSelection[currentSelection.Count - 1];
                if (primary != null)
                {
                    // Case A: The selected layer is itself an eligible container
                    if (IsContainerEligible(primary))
                    {
                        int endIdx = primary.Children != null ? primary.Children.Count : 0;
                        return new InsertionTargetResult(primary, endIdx, primary);
                    }

                    // Case B: The selected layer is a child inside a container
                    LayerNode parentContainer;
                    var containerList = page.FindContainer(primary, out parentContainer);
                    if (parentContainer != null)
                    {
                        if (IsContainerEligible(parentContainer))
                        {
                            int insertIdx = containerList != null ? containerList.IndexOf(primary) + 1 : -1;
                            return new InsertionTargetResult(parentContainer, insertIdx, primary);
                        }
                        else
                        {
                            // Container is locked or hidden: climb to nearest eligible ancestor
                            var eligibleAncestor = FindNearestEligibleAncestor(page, parentContainer);
                            if (eligibleAncestor != null)
                            {
                                int endIdx = eligibleAncestor.Children != null ? eligibleAncestor.Children.Count : 0;
                                return new InsertionTargetResult(eligibleAncestor, endIdx, primary);
                            }
                        }
                    }
                }
            }

            // 3. Empty Selection Context -> Root Canvas
            int rootIndex = page.Layers != null ? page.Layers.Count : 0;
            return new InsertionTargetResult(null, rootIndex);
        }

        private static LayerNode FindNearestEligibleAncestor(Page page, LayerNode node)
        {
            if (page == null || node == null) return null;
            string parentId = node.ParentId;
            while (!string.IsNullOrEmpty(parentId))
            {
                var p = page.FindLayerById(parentId);
                if (p == null) break;
                if (IsContainerEligible(p)) return p;
                parentId = p.ParentId;
            }
            return null;
        }

        /// <summary>
        /// Inserts an object into the resolved target container, updates ParentId,
        /// preserves layout configuration, and recalculates Auto Layout if applicable.
        /// </summary>
        public static bool InsertObject(Page page, InsertionTargetResult target, LayerNode objectToInsert, bool reflowAutoLayout = true)
        {
            if (page == null || objectToInsert == null) return false;
            if (target == null) target = new InsertionTargetResult(null, -1);

            if (!IsObjectCompatible(target.Container, objectToInsert))
            {
                return false;
            }

            if (target.Container != null)
            {
                objectToInsert.ParentId = target.Container.Id;
                if (target.Container.Children == null)
                {
                    target.Container.Children = new List<LayerNode>();
                }

                if (target.InsertionIndex >= 0 && target.InsertionIndex <= target.Container.Children.Count)
                {
                    target.Container.Children.Insert(target.InsertionIndex, objectToInsert);
                }
                else
                {
                    target.Container.Children.Add(objectToInsert);
                }

                if (reflowAutoLayout && target.Container.AutoLayout != null && target.Container.AutoLayout.Mode != AutoLayoutMode.None)
                {
                    AutoLayoutEngine.ReflowRecursive(target.Container);
                }
            }
            else
            {
                objectToInsert.ParentId = null;
                if (page.Layers == null) page.Layers = new List<LayerNode>();

                if (target.InsertionIndex >= 0 && target.InsertionIndex <= page.Layers.Count)
                {
                    page.Layers.Insert(target.InsertionIndex, objectToInsert);
                }
                else
                {
                    page.Layers.Add(objectToInsert);
                }
            }

            return true;
        }

        /// <summary>
        /// Inserts multiple objects into the resolved target container,
        /// preserving their internal parent-child relationships and relative hierarchy.
        /// </summary>
        public static bool InsertMultipleObjects(
            Page page,
            InsertionTargetResult target,
            IList<LayerNode> objectsToInsert,
            bool reflowAutoLayout = true)
        {
            if (page == null || objectsToInsert == null || objectsToInsert.Count == 0) return false;
            if (target == null) target = new InsertionTargetResult(null, -1);

            var topLevel = new List<LayerNode>();
            var allIds = new HashSet<string>(objectsToInsert.Where(o => o != null && !string.IsNullOrEmpty(o.Id)).Select(o => o.Id));

            foreach (var obj in objectsToInsert)
            {
                if (obj == null) continue;
                // If this object's parent is also among the objects to insert, keep it internal!
                if (!string.IsNullOrEmpty(obj.ParentId) && allIds.Contains(obj.ParentId))
                {
                    continue;
                }
                topLevel.Add(obj);
            }

            int currentInsertIndex = target.InsertionIndex;
            foreach (var topObj in topLevel)
            {
                var singleTarget = new InsertionTargetResult(target.Container, currentInsertIndex, target.ContextReferenceNode);
                InsertObject(page, singleTarget, topObj, reflowAutoLayout: false);
                if (currentInsertIndex >= 0)
                {
                    currentInsertIndex++;
                }
            }

            if (reflowAutoLayout && target.Container != null && target.Container.AutoLayout != null && target.Container.AutoLayout.Mode != AutoLayoutMode.None)
            {
                AutoLayoutEngine.ReflowRecursive(target.Container);
            }

            return true;
        }
    }
}

