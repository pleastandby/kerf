using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Kerf.Model;

namespace Kerf.Engine
{
    public interface ICommand
    {
        string Description { get; }
        void Execute();
        void Unexecute();
    }

    public class CompositeCommand : ICommand
    {
        public string Description { get; private set; }
        private readonly List<ICommand> commands;

        public CompositeCommand(string desc, IEnumerable<ICommand> cmds)
        {
            Description = desc;
            commands = new List<ICommand>(cmds);
        }

        public void Execute()
        {
            for (int i = 0; i < commands.Count; i++)
            {
                commands[i].Execute();
            }
        }

        public void Unexecute()
        {
            for (int i = commands.Count - 1; i >= 0; i--)
            {
                commands[i].Unexecute();
            }
        }
    }

    public class CommandHistory
    {
        private readonly Stack<ICommand> undoStack = new Stack<ICommand>();
        private readonly Stack<ICommand> redoStack = new Stack<ICommand>();
        private readonly int maxHistoryDepth;

        private List<ICommand> batchCommands = null;
        private string batchDescription = null;

        public event EventHandler HistoryChanged;

        public bool CanUndo
        {
            get { return undoStack.Count > 0; }
        }

        public bool CanRedo
        {
            get { return redoStack.Count > 0; }
        }

        public string UndoActionName
        {
            get { return undoStack.Count > 0 ? undoStack.Peek().Description : ""; }
        }

        public string RedoActionName
        {
            get { return redoStack.Count > 0 ? redoStack.Peek().Description : ""; }
        }

        public bool IsDirty { get; private set; }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void MarkClean()
        {
            IsDirty = false;
        }

        public CommandHistory(int maxDepth = 100)
        {
            maxHistoryDepth = maxDepth;
            IsDirty = false;
        }

        public void BeginBatch(string description)
        {
            batchCommands = new List<ICommand>();
            batchDescription = description;
        }

        public void EndBatch()
        {
            if (batchCommands != null && batchCommands.Count > 0)
            {
                var composite = new CompositeCommand(batchDescription ?? "Multiple Actions", batchCommands);
                undoStack.Push(composite);
                redoStack.Clear();
                OnHistoryChanged();
            }
            batchCommands = null;
            batchDescription = null;
        }

        public void Execute(ICommand command)
        {
            if (command == null) return;

            command.Execute();

            if (batchCommands != null)
            {
                batchCommands.Add(command);
                return;
            }

            undoStack.Push(command);
            redoStack.Clear();

            if (undoStack.Count > maxHistoryDepth)
            {
                // Trim stack
                var temp = undoStack.ToArray();
                undoStack.Clear();
                for (int i = maxHistoryDepth - 1; i >= 0; i--)
                {
                    undoStack.Push(temp[i]);
                }
            }

            OnHistoryChanged();
        }

        public void Undo()
        {
            if (!CanUndo) return;

            var cmd = undoStack.Pop();
            cmd.Unexecute();
            redoStack.Push(cmd);
            OnHistoryChanged();
        }

        public void Redo()
        {
            if (!CanRedo) return;

            var cmd = redoStack.Pop();
            cmd.Execute();
            undoStack.Push(cmd);
            OnHistoryChanged();
        }

        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
            OnHistoryChanged();
        }

        private void OnHistoryChanged()
        {
            var handler = HistoryChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }

    // Concrete Commands

    public class AddLayerCommand : ICommand
    {
        public string Description { get { return "Add " + node.Name; } }
        private readonly Page page;
        private readonly LayerNode node;
        private readonly Action onRender;

        public AddLayerCommand(Page p, LayerNode n, Action renderCallback = null)
        {
            page = p;
            node = n;
            onRender = renderCallback;
        }

        public void Execute()
        {
            if (!page.Layers.Contains(node))
            {
                page.Layers.Add(node);
            }
            if (onRender != null) onRender();
        }

        public void Unexecute()
        {
            page.Layers.Remove(node);
            if (onRender != null) onRender();
        }
    }

    public class AddLayersCommand : ICommand
    {
        public string Description { get { return string.Format("Add {0} layer(s)", nodes.Count); } }
        private readonly Page page;
        private readonly List<LayerNode> nodes;
        private readonly Action onRender;

        public AddLayersCommand(Page p, IEnumerable<LayerNode> toAdd, Action renderCallback = null)
        {
            page = p;
            nodes = new List<LayerNode>(toAdd);
            onRender = renderCallback;
        }

        public void Execute()
        {
            foreach (var n in nodes)
            {
                if (page.FindLayerById(n.Id) == null)
                {
                    if (!string.IsNullOrEmpty(n.ParentId))
                    {
                        var parent = page.FindLayerById(n.ParentId);
                        if (parent != null)
                        {
                            if (parent.Children == null) parent.Children = new List<LayerNode>();
                            if (!parent.Children.Contains(n)) parent.Children.Add(n);
                        }
                        else
                        {
                            if (!page.Layers.Contains(n)) page.Layers.Add(n);
                        }
                    }
                    else
                    {
                        if (!page.Layers.Contains(n)) page.Layers.Add(n);
                    }
                }
            }
            if (onRender != null) onRender();
        }

        public void Unexecute()
        {
            foreach (var n in nodes)
            {
                page.RemoveLayerEverywhere(n);
            }
            if (onRender != null) onRender();
        }
    }

    public class DeleteLayersCommand : ICommand
    {
        public string Description { get { return string.Format("Delete {0} layer(s)", nodes.Count); } }
        private readonly Page page;
        private readonly List<LayerNode> nodes;
        private readonly List<LayerNode> parentNodes;
        private readonly List<int> originalIndices;
        private readonly Action onRender;

        public DeleteLayersCommand(Page p, IEnumerable<LayerNode> toDelete, Action renderCallback = null)
        {
            page = p;
            nodes = new List<LayerNode>(toDelete);
            parentNodes = new List<LayerNode>();
            originalIndices = new List<int>();

            foreach (var n in nodes)
            {
                LayerNode parent;
                var container = page.FindContainer(n, out parent);
                parentNodes.Add(parent);
                originalIndices.Add(container != null ? container.IndexOf(n) : -1);
            }
            onRender = renderCallback;
        }

        public void Execute()
        {
            foreach (var n in nodes)
            {
                page.RemoveLayerEverywhere(n);
            }
            if (onRender != null) onRender();
        }

        public void Unexecute()
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                var parent = parentNodes[i];
                int idx = originalIndices[i];
                var container = (parent != null && parent.Children != null) ? parent.Children : page.Layers;

                if (idx >= 0 && idx <= container.Count)
                {
                    container.Insert(idx, n);
                }
                else
                {
                    container.Add(n);
                }
            }
            if (onRender != null) onRender();
        }
    }

    public class TransformNodesCommand : ICommand
    {
        public string Description { get { return "Transform Objects"; } }
        private readonly List<LayerNode> nodes;
        private readonly List<Rect> oldBounds;
        private readonly List<Rect> newBounds;
        private readonly Action onRender;

        public TransformNodesCommand(IEnumerable<LayerNode> targets, IEnumerable<Rect> before, IEnumerable<Rect> after, Action renderCallback)
        {
            nodes = new List<LayerNode>(targets);
            oldBounds = new List<Rect>(before);
            newBounds = new List<Rect>(after);
            onRender = renderCallback;
        }

        public void Execute()
        {
            for (int i = 0; i < nodes.Count && i < newBounds.Count; i++)
            {
                var n = nodes[i];
                var r = newBounds[i];
                n.X = r.X;
                n.Y = r.Y;
                n.Width = r.Width;
                n.Height = r.Height;
            }
            if (onRender != null) onRender();
        }

        public void Unexecute()
        {
            for (int i = 0; i < nodes.Count && i < oldBounds.Count; i++)
            {
                var n = nodes[i];
                var r = oldBounds[i];
                n.X = r.X;
                n.Y = r.Y;
                n.Width = r.Width;
                n.Height = r.Height;
            }
            if (onRender != null) onRender();
        }
    }

    public class MoveNodesDeltaCommand : ICommand
    {
        public string Description { get { return "Move Objects"; } }
        private readonly List<LayerNode> nodes;
        private readonly double deltaX;
        private readonly double deltaY;
        private readonly Action onRender;

        public MoveNodesDeltaCommand(IEnumerable<LayerNode> targets, double dx, double dy, Action renderCallback)
        {
            nodes = new List<LayerNode>(targets);
            deltaX = dx;
            deltaY = dy;
            onRender = renderCallback;
        }

        public void Execute()
        {
            foreach (var n in nodes)
            {
                n.X += deltaX;
                n.Y += deltaY;

                if (n.Geometry != null && n.Geometry.Nodes != null)
                {
                    foreach (var bn in n.Geometry.Nodes)
                    {
                        bn.Anchor = new Point(bn.Anchor.X + deltaX, bn.Anchor.Y + deltaY);
                        bn.InHandle = new Point(bn.InHandle.X + deltaX, bn.InHandle.Y + deltaY);
                        bn.OutHandle = new Point(bn.OutHandle.X + deltaX, bn.OutHandle.Y + deltaY);
                    }
                }
            }
            if (onRender != null) onRender();
        }

        public void Unexecute()
        {
            foreach (var n in nodes)
            {
                n.X -= deltaX;
                n.Y -= deltaY;

                if (n.Geometry != null && n.Geometry.Nodes != null)
                {
                    foreach (var bn in n.Geometry.Nodes)
                    {
                        bn.Anchor = new Point(bn.Anchor.X - deltaX, bn.Anchor.Y - deltaY);
                        bn.InHandle = new Point(bn.InHandle.X - deltaX, bn.InHandle.Y - deltaY);
                        bn.OutHandle = new Point(bn.OutHandle.X - deltaX, bn.OutHandle.Y - deltaY);
                    }
                }
            }
            if (onRender != null) onRender();
        }
    }

    public class MoveLayersPositionsCommand : ICommand
    {
        public string Description { get { return "Move Layers"; } }
        private readonly List<LayerNode> _nodes;
        private readonly List<Point> _oldPositions;
        private readonly List<Point> _newPositions;
        private readonly List<VectorGeometry> _oldGeometries;
        private readonly List<VectorGeometry> _newGeometries;
        private readonly Action _onRender;

        public MoveLayersPositionsCommand(
            Dictionary<LayerNode, Point> beforePositions,
            Dictionary<LayerNode, Point> afterPositions,
            Dictionary<LayerNode, VectorGeometry> beforeGeos,
            Dictionary<LayerNode, VectorGeometry> afterGeos,
            Action onRender)
        {
            _nodes = new List<LayerNode>(beforePositions.Keys);
            _oldPositions = new List<Point>();
            _newPositions = new List<Point>();
            _oldGeometries = new List<VectorGeometry>();
            _newGeometries = new List<VectorGeometry>();

            foreach (var n in _nodes)
            {
                _oldPositions.Add(beforePositions[n]);
                _newPositions.Add(afterPositions.ContainsKey(n) ? afterPositions[n] : new Point(n.X, n.Y));
                _oldGeometries.Add((beforeGeos != null && beforeGeos.ContainsKey(n) && beforeGeos[n] != null) ? beforeGeos[n].Clone() : null);
                _newGeometries.Add((afterGeos != null && afterGeos.ContainsKey(n) && afterGeos[n] != null) ? afterGeos[n].Clone() : null);
            }
            _onRender = onRender;
        }

        public void Execute()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                n.X = _newPositions[i].X;
                n.Y = _newPositions[i].Y;
                if (_newGeometries[i] != null && n.Type == ElementType.Path)
                {
                    n.Geometry = _newGeometries[i].Clone();
                }
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                n.X = _oldPositions[i].X;
                n.Y = _oldPositions[i].Y;
                if (_oldGeometries[i] != null && n.Type == ElementType.Path)
                {
                    n.Geometry = _oldGeometries[i].Clone();
                }
            }
            if (_onRender != null) _onRender();
        }
    }

    public class TransformLayersCommand : ICommand
    {
        public string Description { get { return "Transform Layers"; } }
        private readonly List<LayerNode> _nodes;
        private readonly List<Rect> _oldBounds;
        private readonly List<Rect> _newBounds;
        private readonly List<double> _oldRotations;
        private readonly List<double> _newRotations;
        private readonly List<VectorGeometry> _oldGeos;
        private readonly List<VectorGeometry> _newGeos;
        private readonly Action _onRender;

        public TransformLayersCommand(
            Dictionary<LayerNode, Rect> beforeBounds,
            Dictionary<LayerNode, Rect> afterBounds,
            Dictionary<LayerNode, double> beforeRots,
            Dictionary<LayerNode, double> afterRots,
            Dictionary<LayerNode, VectorGeometry> beforeGeos,
            Dictionary<LayerNode, VectorGeometry> afterGeos,
            Action onRender)
        {
            _nodes = new List<LayerNode>(beforeBounds.Keys);
            _oldBounds = new List<Rect>();
            _newBounds = new List<Rect>();
            _oldRotations = new List<double>();
            _newRotations = new List<double>();
            _oldGeos = new List<VectorGeometry>();
            _newGeos = new List<VectorGeometry>();

            foreach (var n in _nodes)
            {
                _oldBounds.Add(beforeBounds[n]);
                _newBounds.Add(afterBounds.ContainsKey(n) ? afterBounds[n] : n.GetBounds());
                _oldRotations.Add((beforeRots != null && beforeRots.ContainsKey(n)) ? beforeRots[n] : n.Rotation);
                _newRotations.Add((afterRots != null && afterRots.ContainsKey(n)) ? afterRots[n] : n.Rotation);
                _oldGeos.Add((beforeGeos != null && beforeGeos.ContainsKey(n) && beforeGeos[n] != null) ? beforeGeos[n].Clone() : null);
                _newGeos.Add((afterGeos != null && afterGeos.ContainsKey(n) && afterGeos[n] != null) ? afterGeos[n].Clone() : null);
            }
            _onRender = onRender;
        }

        public void Execute()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                var r = _newBounds[i];
                n.X = r.X;
                n.Y = r.Y;
                n.Width = r.Width;
                n.Height = r.Height;
                n.Rotation = _newRotations[i];
                if (_newGeos[i] != null && n.Type == ElementType.Path)
                {
                    n.Geometry = _newGeos[i].Clone();
                }
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                var r = _oldBounds[i];
                n.X = r.X;
                n.Y = r.Y;
                n.Width = r.Width;
                n.Height = r.Height;
                n.Rotation = _oldRotations[i];
                if (_oldGeos[i] != null && n.Type == ElementType.Path)
                {
                    n.Geometry = _oldGeos[i].Clone();
                }
            }
            if (_onRender != null) _onRender();
        }
    }

    public class ModifyVectorGeometryCommand : ICommand
    {
        public string Description { get { return "Edit Vector Path"; } }
        private readonly LayerNode _layer;
        private readonly VectorGeometry _beforeGeo;
        private readonly VectorGeometry _afterGeo;
        private readonly Rect _beforeBounds;
        private readonly Rect _afterBounds;
        private readonly Action _onRender;

        public ModifyVectorGeometryCommand(LayerNode layer, VectorGeometry before, VectorGeometry after, Action onRender)
        {
            _layer = layer;
            _beforeGeo = (before != null) ? before.Clone() : null;
            _afterGeo = (after != null) ? after.Clone() : null;
            _beforeBounds = (before != null) ? Kerf.Tools.BezierVectorEngine.CalculateBounds(before) : (layer != null ? layer.GetBounds() : new Rect(0, 0, 0, 0));
            _afterBounds = (after != null) ? Kerf.Tools.BezierVectorEngine.CalculateBounds(after) : (layer != null ? layer.GetBounds() : new Rect(0, 0, 0, 0));
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_layer != null && _afterGeo != null)
            {
                _layer.Geometry = _afterGeo.Clone();
                _layer.X = _afterBounds.X;
                _layer.Y = _afterBounds.Y;
                _layer.Width = Math.Max(2, _afterBounds.Width);
                _layer.Height = Math.Max(2, _afterBounds.Height);
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_layer != null && _beforeGeo != null)
            {
                _layer.Geometry = _beforeGeo.Clone();
                _layer.X = _beforeBounds.X;
                _layer.Y = _beforeBounds.Y;
                _layer.Width = Math.Max(2, _beforeBounds.Width);
                _layer.Height = Math.Max(2, _beforeBounds.Height);
            }
            if (_onRender != null) _onRender();
        }
    }

    public class ModifyTypographyCommand : ICommand
    {
        public string Description { get { return "Edit Text"; } }
        private readonly LayerNode _layer;
        private readonly TypographyData _before;
        private readonly TypographyData _after;
        private readonly Rect _beforeBounds;
        private readonly Rect _afterBounds;
        private readonly Action _onRender;

        public ModifyTypographyCommand(LayerNode layer, TypographyData before, TypographyData after, Rect beforeBounds, Rect afterBounds, Action onRender)
        {
            _layer = layer;
            _before = before != null ? before.Clone() : null;
            _after = after != null ? after.Clone() : null;
            _beforeBounds = beforeBounds;
            _afterBounds = afterBounds;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_layer != null && _after != null)
            {
                _layer.Typography = _after.Clone();
                _layer.Width = _afterBounds.Width;
                _layer.Height = _afterBounds.Height;
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_layer != null && _before != null)
            {
                _layer.Typography = _before.Clone();
                _layer.Width = _beforeBounds.Width;
                _layer.Height = _beforeBounds.Height;
            }
            if (_onRender != null) _onRender();
        }
    }

    public class ReorderLayerCommand : ICommand
    {
        public string Description { get { return "Reorder Layer " + _source.Name; } }
        private readonly Page _page;
        private readonly LayerNode _source;
        private readonly string _oldParentId;
        private readonly int _oldIndex;
        private readonly string _newParentId;
        private readonly int _newIndex;
        private readonly Action _onRender;

        public ReorderLayerCommand(Page page, LayerNode source, string oldParentId, int oldIndex, string newParentId, int newIndex, Action onRender)
        {
            _page = page;
            _source = source;
            _oldParentId = oldParentId;
            _oldIndex = oldIndex;
            _newParentId = newParentId;
            _newIndex = newIndex;
            _onRender = onRender;
        }

        public void Execute()
        {
            MoveTo(_newParentId, _newIndex);
        }

        public void Unexecute()
        {
            MoveTo(_oldParentId, _oldIndex);
        }

        private void MoveTo(string parentId, int index)
        {
            _page.RemoveLayerEverywhere(_source);
            List<LayerNode> targetList;
            if (string.IsNullOrEmpty(parentId))
            {
                targetList = _page.Layers;
                _source.ParentId = null;
            }
            else
            {
                var parent = _page.FindLayerById(parentId);
                if (parent != null)
                {
                    if (parent.Children == null) parent.Children = new List<LayerNode>();
                    targetList = parent.Children;
                    _source.ParentId = parent.Id;
                }
                else
                {
                    targetList = _page.Layers;
                    _source.ParentId = null;
                }
            }

            int insertIdx = Math.Max(0, Math.Min(index, targetList.Count));
            targetList.Insert(insertIdx, _source);
            if (_onRender != null) _onRender();
        }
    }

    public class FrameSelectionCommand : ICommand
    {
        public string Description { get { return "Frame Selection"; } }
        private readonly Page _page;
        private readonly LayerNode _frame;
        private readonly List<LayerNode> _children;
        private readonly string _origParentId;
        private readonly int _origIndex;
        private readonly Action _onRender;

        public FrameSelectionCommand(Page page, LayerNode frame, IEnumerable<LayerNode> children, string origParentId, int origIndex, Action onRender)
        {
            _page = page;
            _frame = frame;
            _children = new List<LayerNode>(children);
            _origParentId = origParentId;
            _origIndex = origIndex;
            _onRender = onRender;
        }

        public void Execute()
        {
            List<LayerNode> container;
            if (string.IsNullOrEmpty(_origParentId))
            {
                container = _page.Layers;
            }
            else
            {
                var p = _page.FindLayerById(_origParentId);
                container = (p != null && p.Children != null) ? p.Children : _page.Layers;
            }

            foreach (var child in _children)
            {
                _page.RemoveLayerEverywhere(child);
                child.ParentId = _frame.Id;
                if (_frame.Children == null) _frame.Children = new List<LayerNode>();
                if (!_frame.Children.Contains(child)) _frame.Children.Add(child);
            }

            int idx = Math.Max(0, Math.Min(_origIndex, container.Count));
            if (!container.Contains(_frame))
            {
                container.Insert(idx, _frame);
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            List<LayerNode> container;
            if (string.IsNullOrEmpty(_origParentId))
            {
                container = _page.Layers;
            }
            else
            {
                var p = _page.FindLayerById(_origParentId);
                container = (p != null && p.Children != null) ? p.Children : _page.Layers;
            }

            _page.RemoveLayerEverywhere(_frame);

            int idx = Math.Max(0, Math.Min(_origIndex, container.Count));
            for (int i = 0; i < _children.Count; i++)
            {
                var child = _children[i];
                child.ParentId = _origParentId;
                container.Insert(Math.Min(idx + i, container.Count), child);
            }

            _frame.Children.Clear();
            if (_onRender != null) _onRender();
        }
    }
}


