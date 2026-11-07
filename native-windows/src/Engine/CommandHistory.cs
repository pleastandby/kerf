using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Kerf.Model;
using Kerf.Tools;
using Kerf.Engine.Animation;

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
            OnHistoryChanged();
        }

        public void MarkClean()
        {
            IsDirty = false;
            OnHistoryChanged();
        }

        public CommandHistory(int maxDepth = 100)
        {
            maxHistoryDepth = maxDepth;
            IsDirty = false;
        }

        public bool IsBatching
        {
            get { return batchCommands != null; }
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
            IsDirty = true;

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
            IsDirty = true;
            OnHistoryChanged();
        }

        public void Redo()
        {
            if (!CanRedo) return;

            var cmd = redoStack.Pop();
            cmd.Execute();
            undoStack.Push(cmd);
            IsDirty = true;
            OnHistoryChanged();
        }

        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
            IsDirty = false;
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
        private readonly string parentId;
        private readonly int index;
        private readonly Action onRender;

        public AddLayerCommand(Page p, LayerNode n, Action renderCallback = null)
            : this(p, n, n != null ? n.ParentId : null, -1, renderCallback)
        {
        }

        public AddLayerCommand(Page p, LayerNode n, string pId, int idx = -1, Action renderCallback = null)
        {
            page = p;
            node = n;
            parentId = pId;
            index = idx;
            onRender = renderCallback;
        }

        public void Execute()
        {
            if (!string.IsNullOrEmpty(parentId))
            {
                var parent = page.FindLayerById(parentId);
                if (parent != null)
                {
                    if (parent.Children == null) parent.Children = new List<LayerNode>();
                    if (!parent.Children.Contains(node))
                    {
                        node.ParentId = parent.Id;
                        if (index >= 0 && index <= parent.Children.Count)
                            parent.Children.Insert(index, node);
                        else
                            parent.Children.Add(node);
                    }
                }
                else
                {
                    node.ParentId = null;
                    if (!page.Layers.Contains(node))
                    {
                        if (index >= 0 && index <= page.Layers.Count)
                            page.Layers.Insert(index, node);
                        else
                            page.Layers.Add(node);
                    }
                }
            }
            else
            {
                node.ParentId = null;
                if (!page.Layers.Contains(node))
                {
                    if (index >= 0 && index <= page.Layers.Count)
                        page.Layers.Insert(index, node);
                    else
                        page.Layers.Add(node);
                }
            }
            if (onRender != null) onRender();
        }

        public void Unexecute()
        {
            if (!string.IsNullOrEmpty(parentId))
            {
                var parent = page.FindLayerById(parentId);
                if (parent != null && parent.Children != null)
                {
                    parent.Children.Remove(node);
                }
            }
            page.Layers.Remove(node);
            if (onRender != null) onRender();
        }
    }

    public class AddLayersCommand : ICommand
    {
        public string Description { get { return string.Format("Add {0} layer(s)", nodes.Count); } }
        private readonly Page page;
        private readonly List<LayerNode> nodes;
        private readonly List<string> parentIds;
        private readonly List<int> indices;
        private readonly Action onRender;

        public AddLayersCommand(Page p, IEnumerable<LayerNode> toAdd, Action renderCallback = null)
        {
            page = p;
            nodes = new List<LayerNode>(toAdd);
            parentIds = new List<string>();
            indices = new List<int>();
            foreach (var node in nodes)
            {
                LayerNode parent = null;
                List<LayerNode> container = page != null ? page.FindContainer(node, out parent) : null;
                parentIds.Add(parent != null ? parent.Id : node != null ? node.ParentId : null);
                indices.Add(container != null ? container.IndexOf(node) : -1);
            }
            onRender = renderCallback;
        }

        public void Execute()
        {
            // On redo, restore the original sibling slot instead of appending the
            // duplicate to the end of the parent. Sorting by saved index also keeps
            // multi-selection duplicates in their intended order.
            var order = Enumerable.Range(0, nodes.Count)
                .OrderBy(i => parentIds[i] ?? string.Empty)
                .ThenBy(i => indices[i] < 0 ? int.MaxValue : indices[i])
                .ToList();
            foreach (int i in order)
            {
                var n = nodes[i];
                if (page.FindLayerById(n.Id) == null)
                {
                    string parentId = parentIds[i];
                    if (!string.IsNullOrEmpty(parentId))
                    {
                        var parent = page.FindLayerById(parentId);
                        if (parent != null)
                        {
                            if (parent.Children == null) parent.Children = new List<LayerNode>();
                            if (!parent.Children.Contains(n))
                            {
                                n.ParentId = parent.Id;
                                parent.Children.Insert(Math.Max(0, Math.Min(indices[i] < 0 ? parent.Children.Count : indices[i], parent.Children.Count)), n);
                            }
                        }
                        else
                        {
                            n.ParentId = null;
                            if (!page.Layers.Contains(n))
                                page.Layers.Insert(Math.Max(0, Math.Min(indices[i] < 0 ? page.Layers.Count : indices[i], page.Layers.Count)), n);
                        }
                    }
                    else
                    {
                        n.ParentId = null;
                        if (!page.Layers.Contains(n))
                            page.Layers.Insert(Math.Max(0, Math.Min(indices[i] < 0 ? page.Layers.Count : indices[i], page.Layers.Count)), n);
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

    public class ModifyBoundsCommand : ICommand
    {
        public string Description { get { return "Modify Bounds"; } }
        private readonly LayerNode _layer;
        private readonly Rect _beforeBounds;
        private readonly Rect _afterBounds;
        private readonly Action _onRender;

        public ModifyBoundsCommand(LayerNode layer, Rect beforeBounds, Rect afterBounds, Action onRender = null)
        {
            _layer = layer;
            _beforeBounds = beforeBounds;
            _afterBounds = afterBounds;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_layer != null)
            {
                _layer.X = _afterBounds.X;
                _layer.Y = _afterBounds.Y;
                _layer.Width = _afterBounds.Width;
                _layer.Height = _afterBounds.Height;
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_layer != null)
            {
                _layer.X = _beforeBounds.X;
                _layer.Y = _beforeBounds.Y;
                _layer.Width = _beforeBounds.Width;
                _layer.Height = _beforeBounds.Height;
            }
            if (_onRender != null) _onRender();
        }
    }

    public class ModifyRotationCommand : ICommand
    {
        public string Description { get { return "Modify Rotation"; } }
        private readonly LayerNode _layer;
        private readonly double _beforeRot;
        private readonly double _afterRot;
        private readonly Action _onRender;

        public ModifyRotationCommand(LayerNode layer, double beforeRot, double afterRot, Action onRender = null)
        {
            _layer = layer;
            _beforeRot = beforeRot;
            _afterRot = afterRot;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_layer != null) _layer.Rotation = _afterRot;
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_layer != null) _layer.Rotation = _beforeRot;
            if (_onRender != null) _onRender();
        }
    }

    public class ModifyOpacityCommand : ICommand
    {
        public string Description { get { return "Modify Opacity"; } }
        private readonly LayerNode _layer;
        private readonly double _beforeOpacity;
        private readonly double _afterOpacity;
        private readonly Action _onRender;

        public ModifyOpacityCommand(LayerNode layer, double beforeOpacity, double afterOpacity, Action onRender = null)
        {
            _layer = layer;
            _beforeOpacity = beforeOpacity;
            _afterOpacity = afterOpacity;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_layer != null) _layer.Opacity = _afterOpacity;
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_layer != null) _layer.Opacity = _beforeOpacity;
            if (_onRender != null) _onRender();
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

    public class AddKeyframeCommand : ICommand
    {
        public string Description { get { return "Add Keyframe"; } }
        private readonly Track _track;
        private readonly Keyframe _keyframe;
        private Keyframe _replacedKeyframe;
        private readonly Action _onRender;

        public AddKeyframeCommand(Track track, Keyframe keyframe, Action onRender = null)
        {
            _track = track;
            _keyframe = keyframe;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_track == null || _keyframe == null) return;

            var existing = _track.FindKeyframeAt(_keyframe.Time, 1e-6);
            if (existing != null && existing.Id != _keyframe.Id)
            {
                _replacedKeyframe = existing.Clone(true);
                _track.RemoveKeyframe(existing.Id);
            }
            _track.AddKeyframe(_keyframe);
            _track.SortKeys();
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_track == null || _keyframe == null) return;

            _track.RemoveKeyframe(_keyframe.Id);
            if (_replacedKeyframe != null)
            {
                _track.AddKeyframe(_replacedKeyframe);
            }
            _track.SortKeys();
            if (_onRender != null) _onRender();
        }
    }

    public class ModifyKeyframeValueCommand : ICommand
    {
        public string Description { get { return "Modify Keyframe Value"; } }
        private readonly Keyframe _keyframe;
        private readonly double _beforeValue;
        private readonly double _afterValue;
        private readonly Action _onRender;

        public ModifyKeyframeValueCommand(Keyframe keyframe, double beforeValue, double afterValue, Action onRender = null)
        {
            _keyframe = keyframe;
            _beforeValue = beforeValue;
            _afterValue = afterValue;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_keyframe != null)
            {
                _keyframe.Value = _afterValue;
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_keyframe != null)
            {
                _keyframe.Value = _beforeValue;
            }
            if (_onRender != null) _onRender();
        }
    }

    public class MoveKeyframesCommand : ICommand
    {
        public string Description { get { return "Move Keyframes"; } }
        private readonly List<Keyframe> _keys;
        private readonly List<double> _oldTimes;
        private readonly List<double> _newTimes;
        private readonly List<Track> _tracks;
        private readonly Action _onRender;

        public MoveKeyframesCommand(Dictionary<Keyframe, double> before, Dictionary<Keyframe, double> after, Dictionary<Keyframe, Track> owners, Action onRender)
        {
            _keys = new List<Keyframe>(before.Keys);
            _oldTimes = new List<double>();
            _newTimes = new List<double>();
            _tracks = new List<Track>();
            foreach (var key in _keys)
            {
                _oldTimes.Add(before[key]);
                _newTimes.Add(after != null && after.ContainsKey(key) ? after[key] : key.Time);
                Track track;
                if (owners != null && owners.TryGetValue(key, out track) && track != null && !_tracks.Contains(track)) _tracks.Add(track);
            }
            _onRender = onRender;
        }

        public void Execute() { Apply(_newTimes); }
        public void Unexecute() { Apply(_oldTimes); }

        private void Apply(List<double> times)
        {
            for (int i = 0; i < _keys.Count; i++) _keys[i].Time = times[i];
            foreach (var track in _tracks)
            {
                if (track != null) track.SortKeys();
            }
            if (_onRender != null) _onRender();
        }
    }

    public class DeleteKeyframesCommand : ICommand
    {
        public string Description { get { return "Delete Keyframes"; } }
        private readonly Dictionary<Keyframe, Track> _keys;
        private readonly Action _onRender;

        public DeleteKeyframesCommand(Dictionary<Keyframe, Track> keys, Action onRender)
        {
            _keys = keys != null ? new Dictionary<Keyframe, Track>(keys) : new Dictionary<Keyframe, Track>();
            _onRender = onRender;
        }

        public void Execute()
        {
            foreach (var pair in _keys)
            {
                if (pair.Value != null && pair.Value.Keys != null)
                {
                    pair.Value.Keys.Remove(pair.Key);
                    pair.Value.SortKeys();
                }
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            foreach (var pair in _keys)
            {
                if (pair.Value != null)
                {
                    pair.Value.AddKeyframe(pair.Key);
                    pair.Value.SortKeys();
                }
            }
            if (_onRender != null) _onRender();
        }
    }

    public class DuplicateKeyframesCommand : ICommand
    {
        public string Description { get { return "Duplicate Keyframes"; } }
        private readonly List<Keyframe> _duplicatedKeys;
        private readonly Dictionary<Keyframe, Track> _owners;
        private readonly Action _onRender;

        public List<Keyframe> DuplicatedKeys { get { return _duplicatedKeys; } }

        public DuplicateKeyframesCommand(List<Keyframe> duplicatedKeys, Dictionary<Keyframe, Track> owners, Action onRender = null)
        {
            _duplicatedKeys = duplicatedKeys != null ? new List<Keyframe>(duplicatedKeys) : new List<Keyframe>();
            _owners = owners != null ? new Dictionary<Keyframe, Track>(owners) : new Dictionary<Keyframe, Track>();
            _onRender = onRender;
        }

        public DuplicateKeyframesCommand(IEnumerable<Keyframe> sourceKeys, Dictionary<Keyframe, Track> owners, double deltaTime, Action onRender = null)
        {
            _duplicatedKeys = new List<Keyframe>();
            _owners = new Dictionary<Keyframe, Track>();
            _onRender = onRender;

            if (sourceKeys != null && owners != null)
            {
                foreach (var kf in sourceKeys)
                {
                    Track track;
                    if (owners.TryGetValue(kf, out track) && track != null)
                    {
                        var clone = kf.Clone(false);
                        clone.Time = Math.Max(0.0, kf.Time + deltaTime);
                        _duplicatedKeys.Add(clone);
                        _owners[clone] = track;
                    }
                }
            }
        }

        public void Execute()
        {
            for (int i = 0; i < _duplicatedKeys.Count; i++)
            {
                var kf = _duplicatedKeys[i];
                Track track;
                if (_owners.TryGetValue(kf, out track) && track != null)
                {
                    track.AddKeyframe(kf);
                    track.SortKeys();
                }
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            for (int i = 0; i < _duplicatedKeys.Count; i++)
            {
                var kf = _duplicatedKeys[i];
                Track track;
                if (_owners.TryGetValue(kf, out track) && track != null)
                {
                    track.RemoveKeyframe(kf.Id);
                    track.SortKeys();
                }
            }
            if (_onRender != null) _onRender();
        }
    }

    public class ChangeKeyframeEasingCommand : ICommand
    {
        public string Description { get { return "Change Keyframe Easing"; } }
        private readonly Keyframe _keyframe;
        private readonly InterpMode _beforeInterp;
        private readonly InterpMode _afterInterp;
        private readonly EasingPreset _beforePreset;
        private readonly EasingPreset _afterPreset;
        private readonly Action _onRender;

        public ChangeKeyframeEasingCommand(Keyframe keyframe, InterpMode beforeInterp, InterpMode afterInterp, EasingPreset beforePreset, EasingPreset afterPreset, Action onRender = null)
        {
            _keyframe = keyframe;
            _beforeInterp = beforeInterp;
            _afterInterp = afterInterp;
            _beforePreset = beforePreset;
            _afterPreset = afterPreset;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_keyframe != null)
            {
                _keyframe.Interp = _afterInterp;
                _keyframe.Preset = _afterPreset;
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_keyframe != null)
            {
                _keyframe.Interp = _beforeInterp;
                _keyframe.Preset = _beforePreset;
            }
            if (_onRender != null) _onRender();
        }
    }

    public class KeyframeBezierSnapshot
    {
        public Keyframe Key { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public EasingPreset Preset { get; set; }
        public bool IsTangentBroken { get; set; }

        public KeyframeBezierSnapshot() { }

        public KeyframeBezierSnapshot(Keyframe key)
        {
            Key = key;
            if (key != null)
            {
                X1 = key.X1;
                Y1 = key.Y1;
                X2 = key.X2;
                Y2 = key.Y2;
                Preset = key.Preset;
                IsTangentBroken = key.IsTangentBroken;
            }
        }

        public KeyframeBezierSnapshot(Keyframe key, double x1, double y1, double x2, double y2, EasingPreset preset, bool isTangentBroken)
        {
            Key = key;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            Preset = preset;
            IsTangentBroken = isTangentBroken;
        }
    }

    public class ModifyBezierHandlesCommand : ICommand
    {
        public string Description { get { return "Modify Bézier Handles"; } }
        private readonly List<KeyframeBezierSnapshot> _beforeSnapshots;
        private readonly List<KeyframeBezierSnapshot> _afterSnapshots;
        private readonly Action _onRender;

        public ModifyBezierHandlesCommand(IEnumerable<KeyframeBezierSnapshot> beforeSnapshots, IEnumerable<KeyframeBezierSnapshot> afterSnapshots, Action onRender = null)
        {
            _beforeSnapshots = beforeSnapshots != null ? new List<KeyframeBezierSnapshot>(beforeSnapshots) : new List<KeyframeBezierSnapshot>();
            _afterSnapshots = afterSnapshots != null ? new List<KeyframeBezierSnapshot>(afterSnapshots) : new List<KeyframeBezierSnapshot>();
            _onRender = onRender;
        }

        public ModifyBezierHandlesCommand(KeyframeBezierSnapshot beforeSnapshot, KeyframeBezierSnapshot afterSnapshot, Action onRender = null)
        {
            _beforeSnapshots = new List<KeyframeBezierSnapshot>();
            if (beforeSnapshot != null) _beforeSnapshots.Add(beforeSnapshot);
            _afterSnapshots = new List<KeyframeBezierSnapshot>();
            if (afterSnapshot != null) _afterSnapshots.Add(afterSnapshot);
            _onRender = onRender;
        }

        public void Execute()
        {
            ApplySnapshots(_afterSnapshots);
        }

        public void Unexecute()
        {
            ApplySnapshots(_beforeSnapshots);
        }

        private void ApplySnapshots(List<KeyframeBezierSnapshot> snapshots)
        {
            if (snapshots != null)
            {
                for (int i = 0; i < snapshots.Count; i++)
                {
                    var snap = snapshots[i];
                    if (snap != null && snap.Key != null)
                    {
                        snap.Key.X1 = snap.X1;
                        snap.Key.Y1 = snap.Y1;
                        snap.Key.X2 = snap.X2;
                        snap.Key.Y2 = snap.Y2;
                        snap.Key.Preset = snap.Preset;
                        snap.Key.IsTangentBroken = snap.IsTangentBroken;
                    }
                }
            }
            if (_onRender != null) _onRender();
        }
    }

    public class ModifySpringParamsCommand : ICommand
    {
        public string Description { get { return "Modify Spring Parameters"; } }
        private readonly Keyframe _keyframe;
        private readonly double _beforeMass;
        private readonly double _afterMass;
        private readonly double _beforeStiffness;
        private readonly double _afterStiffness;
        private readonly double _beforeDamping;
        private readonly double _afterDamping;
        private readonly double _beforeVelocity;
        private readonly double _afterVelocity;
        private readonly Action _onRender;

        public ModifySpringParamsCommand(Keyframe keyframe,
            double beforeMass, double afterMass,
            double beforeStiffness, double afterStiffness,
            double beforeDamping, double afterDamping,
            double beforeVelocity, double afterVelocity,
            Action onRender = null)
        {
            _keyframe = keyframe;
            _beforeMass = beforeMass;
            _afterMass = afterMass;
            _beforeStiffness = beforeStiffness;
            _afterStiffness = afterStiffness;
            _beforeDamping = beforeDamping;
            _afterDamping = afterDamping;
            _beforeVelocity = beforeVelocity;
            _afterVelocity = afterVelocity;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_keyframe != null)
            {
                _keyframe.Mass = _afterMass;
                _keyframe.Stiffness = _afterStiffness;
                _keyframe.Damping = _afterDamping;
                _keyframe.InitialVelocity = _afterVelocity;
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_keyframe != null)
            {
                _keyframe.Mass = _beforeMass;
                _keyframe.Stiffness = _beforeStiffness;
                _keyframe.Damping = _beforeDamping;
                _keyframe.InitialVelocity = _beforeVelocity;
            }
            if (_onRender != null) _onRender();
        }
    }

    public class AddTrackCommand : ICommand
    {
        public string Description { get { return "Add Track"; } }
        private readonly Clip _clip;
        private readonly Track _track;
        private readonly Action _onRender;

        public AddTrackCommand(Clip clip, Track track, Action onRender = null)
        {
            _clip = clip;
            _track = track;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_clip != null && _track != null)
            {
                _clip.AddTrack(_track);
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_clip != null && _clip.Tracks != null && _track != null)
            {
                _clip.Tracks.Remove(_track);
            }
            if (_onRender != null) _onRender();
        }
    }

    public class RemoveTrackCommand : ICommand
    {
        public string Description { get { return "Remove Track"; } }
        private readonly Clip _clip;
        private readonly Track _track;
        private int _originalIndex;
        private readonly Action _onRender;

        public RemoveTrackCommand(Clip clip, Track track, Action onRender = null)
        {
            _clip = clip;
            _track = track;
            _originalIndex = (clip != null && clip.Tracks != null && track != null) ? clip.Tracks.IndexOf(track) : -1;
            _onRender = onRender;
        }

        public RemoveTrackCommand(Clip clip, Track track, int originalIndex, Action onRender = null)
        {
            _clip = clip;
            _track = track;
            _originalIndex = originalIndex;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_clip != null && _clip.Tracks != null && _track != null)
            {
                if (_originalIndex < 0)
                {
                    _originalIndex = _clip.Tracks.IndexOf(_track);
                }
                _clip.Tracks.Remove(_track);
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_clip != null && _clip.Tracks != null && _track != null)
            {
                int idx = Math.Max(0, Math.Min(_originalIndex >= 0 ? _originalIndex : _clip.Tracks.Count, _clip.Tracks.Count));
                if (!_clip.Tracks.Contains(_track))
                {
                    _clip.Tracks.Insert(idx, _track);
                }
            }
            if (_onRender != null) _onRender();
        }
    }

    public class AddClipCommand : ICommand
    {
        public string Description { get { return "Add Clip"; } }
        private readonly AnimationProjectData _project;
        private readonly Clip _clip;
        private uint _previousActiveClipId;
        private readonly Action _onRender;

        public AddClipCommand(AnimationProjectData project, Clip clip, Action onRender = null)
        {
            _project = project;
            _clip = clip;
            _previousActiveClipId = (project != null) ? project.ActiveClipId : 0;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_project != null && _clip != null)
            {
                if (_project.Clips == null) _project.Clips = new List<Clip>();
                if (!_project.Clips.Contains(_clip))
                {
                    _project.Clips.Add(_clip);
                }
                _previousActiveClipId = _project.ActiveClipId;
                _project.ActiveClipId = _clip.Id;
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_project != null && _clip != null)
            {
                if (_project.Clips != null)
                {
                    _project.Clips.Remove(_clip);
                }
                _project.ActiveClipId = _previousActiveClipId;
            }
            if (_onRender != null) _onRender();
        }
    }

    public class RemoveClipCommand : ICommand
    {
        public string Description { get { return "Remove Clip"; } }
        private readonly AnimationProjectData _project;
        private readonly Clip _clip;
        private int _originalIndex;
        private uint _previousActiveClipId;
        private readonly Action _onRender;

        public RemoveClipCommand(AnimationProjectData project, Clip clip, Action onRender = null)
        {
            _project = project;
            _clip = clip;
            _originalIndex = (project != null && project.Clips != null && clip != null) ? project.Clips.IndexOf(clip) : -1;
            _previousActiveClipId = (project != null) ? project.ActiveClipId : 0;
            _onRender = onRender;
        }

        public RemoveClipCommand(AnimationProjectData project, Clip clip, int originalIndex, uint previousActiveClipId, Action onRender = null)
        {
            _project = project;
            _clip = clip;
            _originalIndex = originalIndex;
            _previousActiveClipId = previousActiveClipId;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_project != null && _clip != null && _project.Clips != null)
            {
                if (_originalIndex < 0)
                {
                    _originalIndex = _project.Clips.IndexOf(_clip);
                }
                _previousActiveClipId = _project.ActiveClipId;
                _project.Clips.Remove(_clip);
                if (_project.ActiveClipId == _clip.Id)
                {
                    _project.ActiveClipId = _project.Clips.Count > 0 ? _project.Clips[0].Id : 0;
                }
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_project != null && _clip != null)
            {
                if (_project.Clips == null) _project.Clips = new List<Clip>();
                int idx = Math.Max(0, Math.Min(_originalIndex >= 0 ? _originalIndex : _project.Clips.Count, _project.Clips.Count));
                if (!_project.Clips.Contains(_clip))
                {
                    _project.Clips.Insert(idx, _clip);
                }
                _project.ActiveClipId = _previousActiveClipId;
            }
            if (_onRender != null) _onRender();
        }
    }

    public class AnimationClipDurationCommand : ICommand
    {
        public string Description { get { return "Change Clip Duration"; } }
        private readonly Clip _clip;
        private readonly double _beforeDuration;
        private readonly double _afterDuration;
        private readonly double _beforeFps;
        private readonly double _afterFps;
        private readonly LoopMode _beforeLoop;
        private readonly LoopMode _afterLoop;
        private readonly Action _onRender;

        public AnimationClipDurationCommand(Clip clip, double beforeDuration, double afterDuration, double beforeFps, double afterFps, LoopMode beforeLoop, LoopMode afterLoop, Action onRender = null)
        {
            _clip = clip;
            _beforeDuration = beforeDuration;
            _afterDuration = afterDuration;
            _beforeFps = beforeFps;
            _afterFps = afterFps;
            _beforeLoop = beforeLoop;
            _afterLoop = afterLoop;
            _onRender = onRender;
        }

        public AnimationClipDurationCommand(Clip clip, double afterDuration, double afterFps, LoopMode afterLoop, Action onRender = null)
        {
            _clip = clip;
            _beforeDuration = (clip != null) ? clip.Duration : 1.0;
            _afterDuration = afterDuration;
            _beforeFps = (clip != null) ? clip.Fps : 60.0;
            _afterFps = afterFps;
            _beforeLoop = (clip != null) ? clip.LoopMode : LoopMode.OneShot;
            _afterLoop = afterLoop;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_clip != null)
            {
                _clip.Duration = _afterDuration;
                _clip.Fps = _afterFps;
                _clip.LoopMode = _afterLoop;
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_clip != null)
            {
                _clip.Duration = _beforeDuration;
                _clip.Fps = _beforeFps;
                _clip.LoopMode = _beforeLoop;
            }
            if (_onRender != null) _onRender();
        }
    }

    public class ModifyCornerRadiusCommand : ICommand
    {
        public string Description { get { return "Change Corner Radius"; } }
        private readonly LayerNode _layer;
        private readonly double _beforeRadius;
        private readonly double _afterRadius;
        private readonly Action _onRender;

        public ModifyCornerRadiusCommand(LayerNode layer, double beforeRadius, double afterRadius, Action onRender)
        {
            _layer = layer;
            _beforeRadius = beforeRadius;
            _afterRadius = afterRadius;
            _onRender = onRender;
        }

        public void Execute()
        {
            if (_layer != null)
            {
                _layer.CornerRadius = _afterRadius;
                _layer.CornerRadiusTopLeft = _afterRadius;
                _layer.CornerRadiusTopRight = _afterRadius;
                _layer.CornerRadiusBottomRight = _afterRadius;
                _layer.CornerRadiusBottomLeft = _afterRadius;
            }
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            if (_layer != null)
            {
                _layer.CornerRadius = _beforeRadius;
                _layer.CornerRadiusTopLeft = _beforeRadius;
                _layer.CornerRadiusTopRight = _beforeRadius;
                _layer.CornerRadiusBottomRight = _beforeRadius;
                _layer.CornerRadiusBottomLeft = _beforeRadius;
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
        private readonly bool _oldIgnoreAutoLayout;
        private readonly bool _newIgnoreAutoLayout;
        private readonly Action _onRender;

        public ReorderLayerCommand(Page page, LayerNode source, string oldParentId, int oldIndex, string newParentId, int newIndex, Action onRender, bool? newIgnoreAutoLayout = null)
        {
            _page = page;
            _source = source;
            _oldParentId = oldParentId;
            _oldIndex = oldIndex;
            _newParentId = newParentId;
            _newIndex = newIndex;
            _oldIgnoreAutoLayout = source != null && source.IgnoreAutoLayout;
            _newIgnoreAutoLayout = newIgnoreAutoLayout.HasValue ? newIgnoreAutoLayout.Value : _oldIgnoreAutoLayout;
            _onRender = onRender;
        }

        public void Execute()
        {
            MoveTo(_newParentId, _newIndex, _newIgnoreAutoLayout);
        }

        public void Unexecute()
        {
            MoveTo(_oldParentId, _oldIndex, _oldIgnoreAutoLayout);
        }

        private void MoveTo(string parentId, int index, bool ignoreAutoLayout)
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
                if (parent != null && (parent.Type == ElementType.Frame || parent.Type == ElementType.Group))
                {
                    if (parent == _source || (!string.IsNullOrEmpty(parent.Id) && parent.Id == _source.Id) || Page.IsDescendant(_source, parent))
                    {
                        targetList = _page.Layers;
                        _source.ParentId = null;
                    }
                    else
                    {
                        if (parent.Children == null) parent.Children = new List<LayerNode>();
                        targetList = parent.Children;
                        _source.ParentId = parent.Id;
                    }
                }
                else
                {
                    targetList = _page.Layers;
                    _source.ParentId = null;
                }
            }

            _source.IgnoreAutoLayout = ignoreAutoLayout;
            int insertIdx = Math.Max(0, Math.Min(index, targetList.Count));
            targetList.Insert(insertIdx, _source);

            // Reflow both old and new containers so nested auto layout frames update immediately
            if (!string.IsNullOrEmpty(_oldParentId))
            {
                var oldP = _page.FindLayerById(_oldParentId);
                if (oldP != null) AutoLayoutEngine.ReflowHierarchy(_page, oldP);
            }
            if (!string.IsNullOrEmpty(parentId))
            {
                var newP = _page.FindLayerById(parentId);
                if (newP != null) AutoLayoutEngine.ReflowHierarchy(_page, newP);
            }
            else
            {
                AutoLayoutEngine.ReflowHierarchy(_page, _source);
            }

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

    public class AutoLayoutSelectionCommand : ICommand
    {
        public string Description { get { return "Add Auto Layout"; } }
        public LayerNode CreatedFrame { get { return _frame; } }

        public class ChildRecord
        {
            public LayerNode Layer;
            public string OriginalParentId;
            public int OriginalIndex;
            public double OriginalX;
            public double OriginalY;
            public double OriginalWidth;
            public double OriginalHeight;
        }

        private readonly Page _page;
        private readonly LayerNode _frame;
        private readonly List<ChildRecord> _records;
        private readonly ChildRecord _consumedBgRecord;
        private readonly string _frameParentId;
        private readonly int _frameInsertIndex;
        private readonly Action _onRender;

        public AutoLayoutSelectionCommand(Page page, LayerNode frame, List<ChildRecord> records, string frameParentId, int frameInsertIndex, Action onRender)
            : this(page, frame, records, frameParentId, frameInsertIndex, onRender, null)
        {
        }

        public AutoLayoutSelectionCommand(Page page, LayerNode frame, List<ChildRecord> records, string frameParentId, int frameInsertIndex, Action onRender, ChildRecord consumedBgRecord)
        {
            _page = page;
            _frame = frame;
            _records = records;
            _frameParentId = frameParentId;
            _frameInsertIndex = frameInsertIndex;
            _onRender = onRender;
            _consumedBgRecord = consumedBgRecord;
        }

        public void Execute()
        {
            List<LayerNode> frameContainer;
            if (string.IsNullOrEmpty(_frameParentId))
            {
                frameContainer = _page.Layers;
            }
            else
            {
                var p = _page.FindLayerById(_frameParentId);
                frameContainer = (p != null && p.Children != null) ? p.Children : _page.Layers;
            }

            if (_consumedBgRecord != null && _consumedBgRecord.Layer != null)
            {
                _page.RemoveLayerEverywhere(_consumedBgRecord.Layer);
            }

            foreach (var rec in _records)
            {
                _page.RemoveLayerEverywhere(rec.Layer);
                rec.Layer.ParentId = _frame.Id;
                if (_frame.Children == null) _frame.Children = new List<LayerNode>();
                if (!_frame.Children.Contains(rec.Layer)) _frame.Children.Add(rec.Layer);
            }

            _frame.ParentId = _frameParentId;
            int insertIdx = Math.Max(0, Math.Min(_frameInsertIndex, frameContainer.Count));
            if (!frameContainer.Contains(_frame))
            {
                frameContainer.Insert(insertIdx, _frame);
            }

            AutoLayoutEngine.ReflowHierarchy(_page, _frame);
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            _page.RemoveLayerEverywhere(_frame);
            if (_frame.Children != null)
            {
                _frame.Children.Clear();
            }

            if (_consumedBgRecord != null && _consumedBgRecord.Layer != null)
            {
                var bg = _consumedBgRecord.Layer;
                bg.X = _consumedBgRecord.OriginalX;
                bg.Y = _consumedBgRecord.OriginalY;
                bg.Width = _consumedBgRecord.OriginalWidth;
                bg.Height = _consumedBgRecord.OriginalHeight;
                bg.ParentId = _consumedBgRecord.OriginalParentId;

                List<LayerNode> bgContainer;
                if (string.IsNullOrEmpty(_consumedBgRecord.OriginalParentId))
                {
                    bgContainer = _page.Layers;
                }
                else
                {
                    var p = _page.FindLayerById(_consumedBgRecord.OriginalParentId);
                    bgContainer = (p != null && p.Children != null) ? p.Children : _page.Layers;
                }

                int bgIdx = Math.Max(0, Math.Min(_consumedBgRecord.OriginalIndex, bgContainer.Count));
                if (!bgContainer.Contains(bg))
                {
                    bgContainer.Insert(bgIdx, bg);
                }
            }

            // Restore all wrapped layers back to their original parent containers and coordinates
            foreach (var rec in _records)
            {
                rec.Layer.X = rec.OriginalX;
                rec.Layer.Y = rec.OriginalY;
                rec.Layer.Width = rec.OriginalWidth;
                rec.Layer.Height = rec.OriginalHeight;
                rec.Layer.ParentId = rec.OriginalParentId;

                List<LayerNode> origContainer;
                if (string.IsNullOrEmpty(rec.OriginalParentId))
                {
                    origContainer = _page.Layers;
                }
                else
                {
                    var p = _page.FindLayerById(rec.OriginalParentId);
                    origContainer = (p != null && p.Children != null) ? p.Children : _page.Layers;
                }

                int idx = Math.Max(0, Math.Min(rec.OriginalIndex, origContainer.Count));
                if (!origContainer.Contains(rec.Layer))
                {
                    origContainer.Insert(idx, rec.Layer);
                }
            }

            if (!string.IsNullOrEmpty(_frameParentId))
            {
                var p = _page.FindLayerById(_frameParentId);
                if (p != null)
                {
                    AutoLayoutEngine.ReflowHierarchy(_page, p);
                }
                else
                {
                    AutoLayoutEngine.ReflowAll(_page);
                }
            }
            else
            {
                AutoLayoutEngine.ReflowAll(_page);
            }

            if (_onRender != null) _onRender();
        }
    }

    public class ToggleAutoLayoutCommand : ICommand
    {
        public string Description { get { return "Toggle Auto Layout"; } }
        private readonly Page _page;
        private readonly LayerNode _frame;
        private readonly AutoLayoutData _oldLayout;
        private readonly AutoLayoutData _newLayout;
        private readonly Action _onRender;

        public ToggleAutoLayoutCommand(Page page, LayerNode frame, AutoLayoutData oldLayout, AutoLayoutData newLayout, Action onRender)
        {
            _page = page;
            _frame = frame;
            _oldLayout = oldLayout != null ? oldLayout.Clone() : null;
            _newLayout = newLayout != null ? newLayout.Clone() : null;
            _onRender = onRender;
        }

        public void Execute()
        {
            _frame.AutoLayout = _newLayout != null ? _newLayout.Clone() : null;
            AutoLayoutEngine.ReflowHierarchy(_page, _frame);
            if (_onRender != null) _onRender();
        }

        public void Unexecute()
        {
            _frame.AutoLayout = _oldLayout != null ? _oldLayout.Clone() : null;
            AutoLayoutEngine.ReflowHierarchy(_page, _frame);
            if (_onRender != null) _onRender();
        }
    }

    // =========================================================================
    // State Machine Engine Reversible Commands (Phase 6 - D-06)
    // =========================================================================

    public class NodeMoveRecord
    {
        public MachineState State { get; set; }
        public Point OldPos { get; set; }
        public Point NewPos { get; set; }

        public NodeMoveRecord() { }

        public NodeMoveRecord(MachineState state, Point oldPos, Point newPos)
        {
            State = state;
            OldPos = oldPos;
            NewPos = newPos;
        }

        public NodeMoveRecord(MachineState state, double oldX, double oldY, double newX, double newY)
            : this(state, new Point(oldX, oldY), new Point(newX, newY))
        {
        }
    }

    public class TransitionSnapshot
    {
        public double Duration { get; set; }
        public double? ExitTime { get; set; }
        public bool Interrupt { get; set; }
        public InterpMode Interpolation { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        public TransitionSnapshot()
        {
            Duration = 0.0;
            ExitTime = null;
            Interrupt = true;
            Interpolation = InterpMode.Cubic;
            X1 = 0.25;
            Y1 = 0.1;
            X2 = 0.25;
            Y2 = 1.0;
        }

        public TransitionSnapshot(StateTransition trans) : this()
        {
            if (trans != null)
            {
                Duration = trans.Duration;
                ExitTime = trans.ExitTime;
                Interrupt = trans.Interrupt;
                Interpolation = trans.Interpolation;
                X1 = trans.X1;
                Y1 = trans.Y1;
                X2 = trans.X2;
                Y2 = trans.Y2;
            }
        }

        public TransitionSnapshot(double duration, double? exitTime, bool interrupt, InterpMode interp, double x1, double y1, double x2, double y2)
        {
            Duration = duration;
            ExitTime = exitTime;
            Interrupt = interrupt;
            Interpolation = interp;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public TransitionSnapshot Clone()
        {
            TransitionSnapshot snap = new TransitionSnapshot();
            snap.Duration = Duration;
            snap.ExitTime = ExitTime;
            snap.Interrupt = Interrupt;
            snap.Interpolation = Interpolation;
            snap.X1 = X1;
            snap.Y1 = Y1;
            snap.X2 = X2;
            snap.Y2 = Y2;
            return snap;
        }
    }

    public class CreateStateNodeCommand : ICommand
    {
        public string Description
        {
            get { return "Create State " + (_state != null ? _state.Name : ""); }
        }

        private readonly MachineLayer _layer;
        private readonly MachineState _state;
        private int _index;
        private readonly Action _onChanged;

        public CreateStateNodeCommand(MachineLayer layer, MachineState state, Action onChanged = null)
        {
            _layer = layer;
            _state = state;
            _index = (layer != null && state != null && layer.States.Contains(state)) ? layer.States.IndexOf(state) : -1;
            _onChanged = onChanged;
        }

        public void Execute()
        {
            if (_layer != null && _state != null)
            {
                if (string.IsNullOrEmpty(_state.NodeId))
                {
                    _state.NodeId = "sm_node_" + Guid.NewGuid().ToString("N");
                }
                if (!_layer.States.Contains(_state))
                {
                    if (_index >= 0 && _index <= _layer.States.Count)
                    {
                        _layer.States.Insert(_index, _state);
                    }
                    else
                    {
                        _layer.AddState(_state);
                        _index = _layer.States.IndexOf(_state);
                    }
                }
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            if (_layer != null && _state != null)
            {
                _layer.States.Remove(_state);
            }
            if (_onChanged != null) _onChanged();
        }
    }

    public class DeleteStateNodesCommand : ICommand
    {
        public string Description
        {
            get { return string.Format("Delete {0} State(s)", _deletedStates.Count); }
        }

        private struct StateSlot
        {
            public MachineState State;
            public int Index;
        }

        private struct TransitionSlot
        {
            public StateTransition Transition;
            public int Index;
        }

        private readonly MachineLayer _layer;
        private readonly List<StateSlot> _deletedStates;
        private readonly List<TransitionSlot> _incidentTransitions;
        private readonly Action _onChanged;

        public DeleteStateNodesCommand(MachineLayer layer, MachineState state, Action onChanged = null)
            : this(layer, state != null ? new MachineState[] { state } : null, onChanged)
        {
        }

        public DeleteStateNodesCommand(MachineLayer layer, IEnumerable<MachineState> states, Action onChanged = null)
        {
            _layer = layer;
            _deletedStates = new List<StateSlot>();
            _incidentTransitions = new List<TransitionSlot>();
            _onChanged = onChanged;

            if (layer != null && states != null)
            {
                HashSet<string> deletedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<int> deletedStateIds = new HashSet<int>();
                HashSet<int> deletedIndices = new HashSet<int>();
                HashSet<MachineState> stateSet = new HashSet<MachineState>();

                foreach (var s in states)
                {
                    if (s == null || stateSet.Contains(s)) continue;
                    stateSet.Add(s);
                    if (!string.IsNullOrEmpty(s.NodeId)) deletedNodeIds.Add(s.NodeId);
                    deletedStateIds.Add(s.Id);
                    int idx = layer.States.IndexOf(s);
                    if (idx >= 0) deletedIndices.Add(idx);
                    _deletedStates.Add(new StateSlot { State = s, Index = idx });
                }
                _deletedStates.Sort((a, b) => a.Index.CompareTo(b.Index));

                if (layer.Transitions != null)
                {
                    HashSet<StateTransition> transSet = new HashSet<StateTransition>();
                    for (int i = 0; i < layer.Transitions.Count; i++)
                    {
                        var t = layer.Transitions[i];
                        if (t == null || transSet.Contains(t)) continue;

                        bool fromMatches = (!string.IsNullOrEmpty(t.FromStateId) && deletedNodeIds.Contains(t.FromStateId))
                                        || (string.IsNullOrEmpty(t.FromStateId) && (deletedStateIds.Contains(t.FromState) || deletedIndices.Contains(t.FromState)));
                        bool toMatches = (!string.IsNullOrEmpty(t.ToStateId) && deletedNodeIds.Contains(t.ToStateId))
                                      || (string.IsNullOrEmpty(t.ToStateId) && (deletedStateIds.Contains(t.ToState) || deletedIndices.Contains(t.ToState)));

                        if (fromMatches || toMatches)
                        {
                            transSet.Add(t);
                            _incidentTransitions.Add(new TransitionSlot { Transition = t, Index = i });
                        }
                    }
                    _incidentTransitions.Sort((a, b) => a.Index.CompareTo(b.Index));
                }
            }
        }

        public void Execute()
        {
            if (_layer != null)
            {
                for (int i = 0; i < _incidentTransitions.Count; i++)
                {
                    _layer.Transitions.Remove(_incidentTransitions[i].Transition);
                }
                for (int i = 0; i < _deletedStates.Count; i++)
                {
                    _layer.States.Remove(_deletedStates[i].State);
                }
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            if (_layer != null)
            {
                for (int i = 0; i < _deletedStates.Count; i++)
                {
                    var slot = _deletedStates[i];
                    if (!_layer.States.Contains(slot.State))
                    {
                        int targetIdx = Math.Max(0, Math.Min(slot.Index, _layer.States.Count));
                        _layer.States.Insert(targetIdx, slot.State);
                    }
                }
                for (int i = 0; i < _incidentTransitions.Count; i++)
                {
                    var slot = _incidentTransitions[i];
                    if (!_layer.Transitions.Contains(slot.Transition))
                    {
                        int targetIdx = Math.Max(0, Math.Min(slot.Index, _layer.Transitions.Count));
                        _layer.Transitions.Insert(targetIdx, slot.Transition);
                    }
                }
            }
            if (_onChanged != null) _onChanged();
        }
    }

    public class MoveStateNodesCommand : ICommand
    {
        public string Description
        {
            get { return "Move State Node(s)"; }
        }

        private readonly List<NodeMoveRecord> _records;
        private readonly Action _onChanged;

        public MoveStateNodesCommand(IEnumerable<NodeMoveRecord> records, Action onChanged = null)
        {
            _records = records != null ? new List<NodeMoveRecord>(records) : new List<NodeMoveRecord>();
            _onChanged = onChanged;
        }

        public void Execute()
        {
            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (r != null && r.State != null)
                {
                    r.State.GraphX = r.NewPos.X;
                    r.State.GraphY = r.NewPos.Y;
                }
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                if (r != null && r.State != null)
                {
                    r.State.GraphX = r.OldPos.X;
                    r.State.GraphY = r.OldPos.Y;
                }
            }
            if (_onChanged != null) _onChanged();
        }
    }

    public class RenameStateNodeCommand : ICommand
    {
        public string Description
        {
            get { return string.Format("Rename State to '{0}'", _newName); }
        }

        private readonly MachineState _state;
        private readonly string _oldName;
        private readonly string _newName;
        private readonly Action _onChanged;

        public RenameStateNodeCommand(MachineState state, string oldName, string newName, Action onChanged = null)
        {
            _state = state;
            _oldName = oldName;
            _newName = newName;
            _onChanged = onChanged;
        }

        public void Execute()
        {
            if (_state != null)
            {
                _state.Name = _newName;
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            if (_state != null)
            {
                _state.Name = _oldName;
            }
            if (_onChanged != null) _onChanged();
        }
    }

    public class CreateTransitionCommand : ICommand
    {
        public string Description
        {
            get { return "Create Transition"; }
        }

        private readonly MachineLayer _layer;
        private readonly StateTransition _transition;
        private int _index;
        private readonly Action _onChanged;

        public CreateTransitionCommand(MachineLayer layer, StateTransition transition, Action onChanged = null)
        {
            _layer = layer;
            _transition = transition;
            _index = (layer != null && transition != null && layer.Transitions.Contains(transition)) ? layer.Transitions.IndexOf(transition) : -1;
            _onChanged = onChanged;
        }

        public void Execute()
        {
            if (_layer != null && _transition != null)
            {
                if (string.IsNullOrEmpty(_transition.TransitionId))
                {
                    _transition.TransitionId = "sm_trans_" + Guid.NewGuid().ToString("N");
                }
                if (string.IsNullOrEmpty(_transition.FromStateId) && _transition.FromState >= 0 && _transition.FromState < _layer.States.Count && _layer.States[_transition.FromState] != null)
                {
                    _transition.FromStateId = _layer.States[_transition.FromState].NodeId;
                }
                if (string.IsNullOrEmpty(_transition.ToStateId) && _transition.ToState >= 0 && _transition.ToState < _layer.States.Count && _layer.States[_transition.ToState] != null)
                {
                    _transition.ToStateId = _layer.States[_transition.ToState].NodeId;
                }

                if (!_layer.Transitions.Contains(_transition))
                {
                    if (_index >= 0 && _index <= _layer.Transitions.Count)
                    {
                        _layer.Transitions.Insert(_index, _transition);
                    }
                    else
                    {
                        _layer.AddTransition(_transition);
                        _index = _layer.Transitions.IndexOf(_transition);
                    }
                }
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            if (_layer != null && _transition != null)
            {
                _layer.Transitions.Remove(_transition);
            }
            if (_onChanged != null) _onChanged();
        }
    }

    public class DeleteTransitionsCommand : ICommand
    {
        public string Description
        {
            get { return string.Format("Delete {0} Transition(s)", _slots.Count); }
        }

        private struct TransitionSlot
        {
            public StateTransition Transition;
            public int Index;
        }

        private readonly MachineLayer _layer;
        private readonly List<TransitionSlot> _slots;
        private readonly Action _onChanged;

        public DeleteTransitionsCommand(MachineLayer layer, StateTransition transition, Action onChanged = null)
            : this(layer, transition != null ? new StateTransition[] { transition } : null, onChanged)
        {
        }

        public DeleteTransitionsCommand(MachineLayer layer, IEnumerable<StateTransition> transitions, Action onChanged = null)
        {
            _layer = layer;
            _slots = new List<TransitionSlot>();
            _onChanged = onChanged;

            if (layer != null && transitions != null)
            {
                HashSet<StateTransition> set = new HashSet<StateTransition>();
                foreach (var t in transitions)
                {
                    if (t == null || set.Contains(t)) continue;
                    set.Add(t);
                    int idx = layer.Transitions.IndexOf(t);
                    _slots.Add(new TransitionSlot { Transition = t, Index = idx });
                }
                _slots.Sort((a, b) => a.Index.CompareTo(b.Index));
            }
        }

        public void Execute()
        {
            if (_layer != null)
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    _layer.Transitions.Remove(_slots[i].Transition);
                }
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            if (_layer != null)
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];
                    if (!_layer.Transitions.Contains(slot.Transition))
                    {
                        int targetIdx = Math.Max(0, Math.Min(slot.Index, _layer.Transitions.Count));
                        _layer.Transitions.Insert(targetIdx, slot.Transition);
                    }
                }
            }
            if (_onChanged != null) _onChanged();
        }
    }

    public class ModifyTransitionCommand : ICommand
    {
        public string Description
        {
            get { return "Modify Transition"; }
        }

        private readonly StateTransition _transition;
        private readonly TransitionSnapshot _oldSnapshot;
        private readonly TransitionSnapshot _newSnapshot;
        private readonly Action _onChanged;

        public ModifyTransitionCommand(StateTransition transition, TransitionSnapshot oldSnapshot, TransitionSnapshot newSnapshot, Action onChanged = null)
        {
            _transition = transition;
            _oldSnapshot = oldSnapshot != null ? oldSnapshot.Clone() : null;
            _newSnapshot = newSnapshot != null ? newSnapshot.Clone() : null;
            _onChanged = onChanged;
        }

        private void Apply(TransitionSnapshot s)
        {
            if (_transition == null || s == null) return;
            _transition.Duration = s.Duration;
            _transition.ExitTime = s.ExitTime;
            _transition.Interrupt = s.Interrupt;
            _transition.Interpolation = s.Interpolation;
            _transition.X1 = s.X1;
            _transition.Y1 = s.Y1;
            _transition.X2 = s.X2;
            _transition.Y2 = s.Y2;
        }

        public void Execute()
        {
            Apply(_newSnapshot);
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            Apply(_oldSnapshot);
            if (_onChanged != null) _onChanged();
        }
    }

    public class CreateInputCommand : ICommand
    {
        public string Description
        {
            get { return "Create Input " + (_input != null ? _input.Name : ""); }
        }

        private readonly StateMachineDefinition _def;
        private readonly MachineInput _input;
        private int _index;
        private readonly Action _onChanged;

        public CreateInputCommand(StateMachineDefinition def, MachineInput input, Action onChanged = null)
        {
            _def = def;
            _input = input;
            _index = (def != null && input != null && def.Inputs.Contains(input)) ? def.Inputs.IndexOf(input) : -1;
            _onChanged = onChanged;
        }

        public void Execute()
        {
            if (_def != null && _input != null)
            {
                if (string.IsNullOrEmpty(_input.InputId))
                {
                    _input.InputId = "sm_input_" + Guid.NewGuid().ToString("N");
                }
                if (!_def.Inputs.Contains(_input))
                {
                    if (_index >= 0 && _index <= _def.Inputs.Count)
                    {
                        _def.Inputs.Insert(_index, _input);
                    }
                    else
                    {
                        _def.AddInput(_input);
                        _index = _def.Inputs.IndexOf(_input);
                    }
                }
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            if (_def != null && _input != null)
            {
                _def.Inputs.Remove(_input);
            }
            if (_onChanged != null) _onChanged();
        }
    }

    public class DeleteInputCommand : ICommand
    {
        public string Description
        {
            get { return "Delete Input " + (_input != null ? _input.Name : ""); }
        }

        private class DependentConditionSlot
        {
            public ConditionGroup Group;
            public TransitionCondition Condition;
            public int Index;
        }

        private readonly StateMachineDefinition _def;
        private readonly MachineInput _input;
        private readonly int _inputIndex;
        private readonly List<DependentConditionSlot> _dependentConditions;
        private readonly Action _onChanged;

        public DeleteInputCommand(StateMachineDefinition def, MachineInput input, Action onChanged = null)
        {
            _def = def;
            _input = input;
            _inputIndex = (def != null && input != null) ? def.Inputs.IndexOf(input) : -1;
            _dependentConditions = new List<DependentConditionSlot>();
            _onChanged = onChanged;

            if (def != null && input != null && def.Layers != null)
            {
                string targetInputId = input.InputId;
                string targetName = input.Name;
                int targetIdx = _inputIndex;

                HashSet<TransitionCondition> conditionSet = new HashSet<TransitionCondition>();

                for (int l = 0; l < def.Layers.Count; l++)
                {
                    var layer = def.Layers[l];
                    if (layer == null || layer.Transitions == null) continue;

                    for (int t = 0; t < layer.Transitions.Count; t++)
                    {
                        var trans = layer.Transitions[t];
                        if (trans == null || trans.ConditionGroups == null) continue;

                        for (int g = 0; g < trans.ConditionGroups.Count; g++)
                        {
                            var grp = trans.ConditionGroups[g];
                            if (grp == null || grp.Conditions == null) continue;

                            for (int c = 0; c < grp.Conditions.Count; c++)
                            {
                                var cond = grp.Conditions[c];
                                if (cond == null || conditionSet.Contains(cond)) continue;

                                bool matches = false;
                                if (!string.IsNullOrEmpty(targetInputId) && string.Equals(cond.InputId, targetInputId, StringComparison.OrdinalIgnoreCase))
                                {
                                    matches = true;
                                }
                                else if (!string.IsNullOrEmpty(cond.PropertyName) &&
                                         (string.Equals(cond.PropertyName, targetInputId, StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(cond.PropertyName, targetName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    matches = true;
                                }
                                else if (string.IsNullOrEmpty(cond.InputId) && string.IsNullOrEmpty(cond.PropertyName) && targetIdx >= 0 && cond.InputIndex == targetIdx)
                                {
                                    matches = true;
                                }

                                if (matches)
                                {
                                    conditionSet.Add(cond);
                                    _dependentConditions.Add(new DependentConditionSlot
                                    {
                                        Group = grp,
                                        Condition = cond,
                                        Index = c
                                    });
                                }
                            }
                        }
                    }
                }
                _dependentConditions.Sort((a, b) => a.Index.CompareTo(b.Index));
            }
        }

        public void Execute()
        {
            if (_def != null && _input != null)
            {
                for (int i = 0; i < _dependentConditions.Count; i++)
                {
                    var slot = _dependentConditions[i];
                    if (slot.Group != null && slot.Group.Conditions != null)
                    {
                        slot.Group.Conditions.Remove(slot.Condition);
                    }
                }
                _def.Inputs.Remove(_input);
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            if (_def != null && _input != null)
            {
                if (!_def.Inputs.Contains(_input))
                {
                    int targetIdx = Math.Max(0, Math.Min(_inputIndex >= 0 ? _inputIndex : _def.Inputs.Count, _def.Inputs.Count));
                    _def.Inputs.Insert(targetIdx, _input);
                }

                for (int i = 0; i < _dependentConditions.Count; i++)
                {
                    var slot = _dependentConditions[i];
                    if (slot.Group != null && slot.Group.Conditions != null && !slot.Group.Conditions.Contains(slot.Condition))
                    {
                        int targetIdx = Math.Max(0, Math.Min(slot.Index, slot.Group.Conditions.Count));
                        slot.Group.Conditions.Insert(targetIdx, slot.Condition);
                    }
                }
            }
            if (_onChanged != null) _onChanged();
        }
    }

    public class ModifyInputCommand : ICommand
    {
        public string Description
        {
            get { return "Modify Input " + _newName; }
        }

        private readonly MachineInput _input;
        private readonly string _oldName;
        private readonly string _newName;
        private readonly double _oldInitial;
        private readonly double _newInitial;
        private readonly Action _onChanged;

        public ModifyInputCommand(MachineInput input, string oldName, string newName, double oldInitial, double newInitial, Action onChanged = null)
        {
            _input = input;
            _oldName = oldName;
            _newName = newName;
            _oldInitial = oldInitial;
            _newInitial = newInitial;
            _onChanged = onChanged;
        }

        public void Execute()
        {
            if (_input != null)
            {
                _input.Name = _newName;
                _input.InitialValue = _newInitial;
                _input.Value = _newInitial;
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            if (_input != null)
            {
                _input.Name = _oldName;
                _input.InitialValue = _oldInitial;
                _input.Value = _oldInitial;
            }
            if (_onChanged != null) _onChanged();
        }
    }

    public class ModifyConditionGroupsCommand : ICommand
    {
        public string Description
        {
            get { return "Modify Condition Groups"; }
        }

        private readonly StateTransition _transition;
        private readonly List<ConditionGroup> _oldGroups;
        private readonly List<ConditionGroup> _newGroups;
        private readonly Action _onChanged;

        public ModifyConditionGroupsCommand(
            StateTransition transition,
            IEnumerable<ConditionGroup> oldGroups,
            IEnumerable<ConditionGroup> newGroups,
            Action onChanged = null)
        {
            _transition = transition;
            _oldGroups = CloneGroups(oldGroups);
            _newGroups = CloneGroups(newGroups);
            _onChanged = onChanged;
        }

        private static List<ConditionGroup> CloneGroups(IEnumerable<ConditionGroup> groups)
        {
            var list = new List<ConditionGroup>();
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    if (g != null)
                    {
                        list.Add(g.Clone());
                    }
                }
            }
            return list;
        }

        public void Execute()
        {
            if (_transition != null)
            {
                _transition.ConditionGroups = CloneGroups(_newGroups);
            }
            if (_onChanged != null) _onChanged();
        }

        public void Unexecute()
        {
            if (_transition != null)
            {
                _transition.ConditionGroups = CloneGroups(_oldGroups);
            }
            if (_onChanged != null) _onChanged();
        }
    }

}
