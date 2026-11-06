using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Kerf.Engine;
using Kerf.Engine.Animation;
using AnimTrack = Kerf.Engine.Animation.Track;

namespace Kerf.UI
{
    public struct GraphViewport
    {
        public double VMin;
        public double VMax;
        public double PixelsPerSecond;
        public double CanvasWidth;
        public double CanvasHeight;
        public double MarginY;

        public GraphViewport(double vMin, double vMax, double pps, double width, double height, double marginY = 24.0)
        {
            VMin = vMin;
            VMax = vMax;
            PixelsPerSecond = pps > 1.0 ? pps : 260.0;
            CanvasWidth = width > 10.0 ? width : 1200.0;
            CanvasHeight = height > 10.0 ? height : 200.0;
            MarginY = marginY;
        }

        public double TimeToX(double time)
        {
            return time * PixelsPerSecond;
        }

        public double ValueToY(double val)
        {
            double usableHeight = Math.Max(10.0, CanvasHeight - 2.0 * MarginY);
            double span = Math.Max(1e-6, VMax - VMin);
            return (CanvasHeight - MarginY) - ((val - VMin) / span) * usableHeight;
        }

        public double YToValue(double y)
        {
            double usableHeight = Math.Max(10.0, CanvasHeight - 2.0 * MarginY);
            double span = Math.Max(1e-6, VMax - VMin);
            return VMin + span * ((CanvasHeight - MarginY - y) / usableHeight);
        }

        public double XToTime(double x)
        {
            double pps = Math.Max(1.0, PixelsPerSecond);
            return Math.Max(0.0, x / pps);
        }

        public void AutoFit(AnimTrack track, ushort propKey = 0)
        {
            if (track == null || track.Keys == null || track.Keys.Count == 0)
            {
                SetDefaultRangeForProp(propKey);
                return;
            }

            double min = double.MaxValue;
            double max = double.MinValue;
            for (int i = 0; i < track.Keys.Count; i++)
            {
                var k = track.Keys[i];
                if (k.Value < min) min = k.Value;
                if (k.Value > max) max = k.Value;

                // Also consider overshoot control points if cubic
                if (k.Interp == InterpMode.Cubic && i + 1 < track.Keys.Count)
                {
                    double nextVal = track.Keys[i + 1].Value;
                    double delta = nextVal - k.Value;
                    double v1 = k.Value + k.Y1 * delta;
                    double v2 = k.Value + k.Y2 * delta;
                    if (v1 < min) min = v1;
                    if (v1 > max) max = v1;
                    if (v2 < min) min = v2;
                    if (v2 > max) max = v2;
                }
                else if (k.Interp == InterpMode.Spring && i + 1 < track.Keys.Count)
                {
                    // Spring peak overshoot estimation
                    double nextVal = track.Keys[i + 1].Value;
                    double delta = nextVal - k.Value;
                    double estPeak = k.Value + 1.45 * delta;
                    if (estPeak < min) min = estPeak;
                    if (estPeak > max) max = estPeak;
                }
            }

            double span = max - min;
            if (span < 1e-4)
            {
                SetDefaultRangeAroundValue(min, propKey);
            }
            else
            {
                // 15% margins per D-04
                double margin = span * 0.15;
                VMin = min - margin;
                VMax = max + margin;
            }
        }

        public void SetDefaultRangeForProp(ushort propKey)
        {
            PropKey pk = (PropKey)propKey;
            switch (pk)
            {
                case PropKey.Opacity:
                    VMin = -0.15;
                    VMax = 1.15;
                    break;
                case PropKey.ScaleX:
                case PropKey.ScaleY:
                    VMin = 0.0;
                    VMax = 2.0;
                    break;
                case PropKey.Rotation:
                    VMin = -180.0;
                    VMax = 180.0;
                    break;
                default:
                    VMin = -50.0;
                    VMax = 50.0;
                    break;
            }
        }

        public void SetDefaultRangeAroundValue(double val, ushort propKey)
        {
            PropKey pk = (PropKey)propKey;
            switch (pk)
            {
                case PropKey.Opacity:
                    VMin = Math.Max(-0.2, val - 0.2);
                    VMax = Math.Min(1.2, val + 0.2);
                    if (VMax - VMin < 0.2) { VMin = -0.1; VMax = 1.1; }
                    break;
                case PropKey.ScaleX:
                case PropKey.ScaleY:
                    VMin = Math.Max(0.0, val - 0.5);
                    VMax = val + 0.5;
                    break;
                case PropKey.Rotation:
                    VMin = val - 45.0;
                    VMax = val + 45.0;
                    break;
                default:
                    VMin = val - 50.0;
                    VMax = val + 50.0;
                    break;
            }
        }
    }

    public enum HandleKind
    {
        None = 0,
        Outgoing = 1, // K_i outgoing handle (X1, Y1)
        Incoming = 2  // K_next incoming handle (K_i.X2, K_i.Y2)
    }

    public class HandleHitInfo
    {
        public HandleKind Kind;
        public Keyframe OwnerKeyframe; // Starting keyframe of the segment owning X1, Y1, X2, Y2
        public Keyframe AttachedKeyframe; // Node where the handle is physically anchored
        public int SegmentIndex;
        public Point KnobPoint;
    }

    public class GraphEditorCanvas : Canvas
    {
        private GraphViewport _viewport;
        private AnimTrack _activeTrack;
        private Keyframe _selectedKeyframe;
        private double _playheadTime = 0.0;
        private double _duration = 2.0;
        private bool _isPanning = false;
        private Point _panStartMouse;
        private double _panStartVMin;
        private double _panStartVMax;

        private HandleHitInfo _activeDragHandle = null;
        private Point _dragStartMouse;
        private double _origX1, _origY1, _origX2, _origY2;
        private KeyframeBezierSnapshot _handleDragStartSnapshot;
        private List<KeyframeBezierSnapshot> _dragStartSnapshots;

        public event Action<Keyframe, AnimTrack> KeyframeModified;
        public event Action<AnimTrack> TrackModified;
        public event Action<Keyframe> KeyframeSelectionChanged;
        public event Action<KeyframeBezierSnapshot, KeyframeBezierSnapshot> TangentHandleDragCommitted;
        public event Action<List<KeyframeBezierSnapshot>, List<KeyframeBezierSnapshot>> TangentHandlesDragCommitted;

        public KeyframeBezierSnapshot HandleDragStartSnapshot { get { return _handleDragStartSnapshot; } }
        public List<KeyframeBezierSnapshot> DragStartSnapshots { get { return _dragStartSnapshots; } }

        private List<KeyframeBezierSnapshot> CaptureTrackBezierSnapshots(AnimTrack track)
        {
            var list = new List<KeyframeBezierSnapshot>();
            if (track != null && track.Keys != null)
            {
                for (int i = 0; i < track.Keys.Count; i++)
                {
                    list.Add(new KeyframeBezierSnapshot(track.Keys[i]));
                }
            }
            return list;
        }

        public GraphViewport Viewport
        {
            get { return _viewport; }
            set { _viewport = value; }
        }

        public AnimTrack ActiveTrack
        {
            get { return _activeTrack; }
            set
            {
                _activeTrack = value;
                Redraw();
            }
        }

        public Keyframe SelectedKeyframe
        {
            get { return _selectedKeyframe; }
            set
            {
                _selectedKeyframe = value;
                Redraw();
            }
        }

        public double PlayheadTime
        {
            get { return _playheadTime; }
            set
            {
                _playheadTime = value;
                Redraw();
            }
        }

        public double PixelsPerSecond
        {
            get { return _viewport.PixelsPerSecond; }
            set
            {
                _viewport.PixelsPerSecond = value;
                Redraw();
            }
        }

        public double Duration
        {
            get { return _duration; }
            set
            {
                _duration = Math.Max(0.1, value);
                Redraw();
            }
        }

        public GraphEditorCanvas()
        {
            Background = new SolidColorBrush(Color.FromRgb(19, 22, 29)); // #13161D dark background
            ClipToBounds = true;
            Focusable = true;

            _viewport = new GraphViewport(0.0, 100.0, 260.0, 1200.0, 200.0, 24.0);

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseWheel += OnMouseWheel;
            KeyDown += OnKeyDown;
            SizeChanged += (s, e) =>
            {
                _viewport.CanvasWidth = ActualWidth > 10.0 ? ActualWidth : _viewport.CanvasWidth;
                _viewport.CanvasHeight = ActualHeight > 10.0 ? ActualHeight : _viewport.CanvasHeight;
                Redraw();
            };
        }

        public void AutoFit()
        {
            ushort propKey = _activeTrack != null ? _activeTrack.Prop : (ushort)0;
            _viewport.AutoFit(_activeTrack, propKey);
            Redraw();
        }

        public void Redraw()
        {
            Children.Clear();

            double totalWidth = Math.Max(ActualWidth, Math.Max(_duration * _viewport.PixelsPerSecond + 300.0, 1200.0));
            double height = Math.Max(ActualHeight, 180.0);
            Width = totalWidth;
            Height = height;

            _viewport.CanvasWidth = totalWidth;
            _viewport.CanvasHeight = height;

            // 1. Draw Grid Lines and Value Ticks
            DrawGridLines(totalWidth, height);

            // 2. Draw Property Curve & Spring Waveforms
            if (_activeTrack != null && _activeTrack.Keys != null && _activeTrack.Keys.Count > 0)
            {
                DrawCurveSegments();
                DrawKeyframeHandles();
            }
            else
            {
                DrawNoTrackNotice();
            }

            // 3. Draw Synchronized Playhead Needle
            DrawPlayhead(height);
        }

        private void DrawGridLines(double totalWidth, double height)
        {
            double vSpan = Math.Max(1e-6, _viewport.VMax - _viewport.VMin);

            // Major and minor value steps
            double rawStep = vSpan / 6.0;
            double step = NiceNum(rawStep, true);

            double firstVal = Math.Ceiling(_viewport.VMin / step) * step;
            var lineBrush = new SolidColorBrush(Color.FromArgb(40, 75, 85, 105)); // Subtle horizontal grid line
            lineBrush.Freeze();
            var zeroLineBrush = new SolidColorBrush(Color.FromArgb(90, 120, 140, 180)); // 0 baseline
            zeroLineBrush.Freeze();
            var textBrush = new SolidColorBrush(Color.FromRgb(110, 125, 150));
            textBrush.Freeze();

            for (double v = firstVal; v <= _viewport.VMax; v += step)
            {
                double y = _viewport.ValueToY(v);
                if (y < 4 || y > height - 4) continue;

                bool isZero = Math.Abs(v) < 1e-6;
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = totalWidth,
                    Y2 = y,
                    Stroke = isZero ? zeroLineBrush : lineBrush,
                    StrokeThickness = isZero ? 1.2 : 0.8,
                    IsHitTestVisible = false
                };
                Children.Add(line);

                var label = new TextBlock
                {
                    Text = FormatValue(v),
                    FontSize = 9,
                    Foreground = textBrush,
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, 6);
                Canvas.SetTop(label, y - 12);
                Children.Add(label);
            }

            // Vertical Time Grid Lines
            double pps = _viewport.PixelsPerSecond;
            double secStep = 1.0;
            if (pps > 400.0) secStep = 0.5;
            if (pps > 800.0) secStep = 0.25;

            var timeLineBrush = new SolidColorBrush(Color.FromArgb(25, 75, 85, 105));
            timeLineBrush.Freeze();

            for (double t = 0.0; t <= _duration + 1.0; t += secStep)
            {
                double x = _viewport.TimeToX(t);
                var vLine = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = timeLineBrush,
                    StrokeThickness = 0.8,
                    IsHitTestVisible = false
                };
                Children.Add(vLine);
            }
        }

        private void DrawNoTrackNotice()
        {
            var text = new TextBlock
            {
                Text = "Click a property track or keyframe in the timeline to edit its curve",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 115, 140)),
                FontFamily = new FontFamily("Segoe UI"),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(text, 24);
            Canvas.SetTop(text, 24);
            Children.Add(text);
        }

        private void DrawCurveSegments()
        {
            var keys = _activeTrack.Keys;
            if (keys == null || keys.Count == 0) return;

            // Sort keys by time for rendering
            var sorted = new List<Keyframe>(keys);
            sorted.Sort((a, b) => a.Time.CompareTo(b.Time));

            var curveBrush = new SolidColorBrush(Color.FromRgb(6, 182, 212)); // Primary curve Teal #06B6D4
            curveBrush.Freeze();
            var curvePen = new Pen(curveBrush, 2.0);
            curvePen.Freeze();

            // 1. Draw flat pre-start line
            double startX = _viewport.TimeToX(sorted[0].Time);
            double startY = _viewport.ValueToY(sorted[0].Value);
            if (startX > 0)
            {
                var preLine = new Line
                {
                    X1 = 0,
                    Y1 = startY,
                    X2 = startX,
                    Y2 = startY,
                    Stroke = curveBrush,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection(new double[] { 3, 3 }),
                    IsHitTestVisible = false
                };
                Children.Add(preLine);
            }

            // 2. Draw segments
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var kA = sorted[i];
                var kB = sorted[i + 1];

                double xA = _viewport.TimeToX(kA.Time);
                double yA = _viewport.ValueToY(kA.Value);
                double xB = _viewport.TimeToX(kB.Time);
                double yB = _viewport.ValueToY(kB.Value);
                double dt = kB.Time - kA.Time;
                double dv = kB.Value - kA.Value;

                if (dt <= 1e-6) continue;

                if (kA.Interp == InterpMode.Hold)
                {
                    var pathGeo = new PathGeometry();
                    var fig = new PathFigure { StartPoint = new Point(xA, yA), IsClosed = false };
                    fig.Segments.Add(new LineSegment(new Point(xB, yA), true));
                    fig.Segments.Add(new LineSegment(new Point(xB, yB), true));
                    pathGeo.Figures.Add(fig);

                    var path = new Path { Data = pathGeo, Stroke = curveBrush, StrokeThickness = 2.0, IsHitTestVisible = false };
                    Children.Add(path);
                }
                else if (kA.Interp == InterpMode.Linear)
                {
                    var line = new Line
                    {
                        X1 = xA,
                        Y1 = yA,
                        X2 = xB,
                        Y2 = yB,
                        Stroke = curveBrush,
                        StrokeThickness = 2.0,
                        IsHitTestVisible = false
                    };
                    Children.Add(line);
                }
                else if (kA.Interp == InterpMode.Cubic)
                {
                    // Control points P1 and P2 in canvas space
                    double c1T = kA.Time + kA.X1 * dt;
                    double c1V = kA.Value + kA.Y1 * dv;
                    double c2T = kA.Time + kA.X2 * dt;
                    double c2V = kA.Value + kA.Y2 * dv;

                    Point p0 = new Point(xA, yA);
                    Point p1 = new Point(_viewport.TimeToX(c1T), _viewport.ValueToY(c1V));
                    Point p2 = new Point(_viewport.TimeToX(c2T), _viewport.ValueToY(c2V));
                    Point p3 = new Point(xB, yB);

                    var pathGeo = new PathGeometry();
                    var fig = new PathFigure { StartPoint = p0, IsClosed = false };
                    fig.Segments.Add(new BezierSegment(p1, p2, p3, true));
                    pathGeo.Figures.Add(fig);

                    var path = new Path { Data = pathGeo, Stroke = curveBrush, StrokeThickness = 2.0, IsHitTestVisible = false };
                    Children.Add(path);
                }
                else if (kA.Interp == InterpMode.Spring)
                {
                    // Sample spring waveform ODE at regular intervals
                    int sampleCount = Math.Max(20, (int)(dt * 120.0));
                    var pathGeo = new PathGeometry();
                    var fig = new PathFigure { StartPoint = new Point(xA, yA), IsClosed = false };

                    double settleT = -1.0;
                    for (int s = 1; s <= sampleCount; s++)
                    {
                        double frac = (double)s / sampleCount;
                        double currT = kA.Time + frac * dt;
                        double localT = currT - kA.Time;
                        double springP = EasingEngine.EvaluateSpring(localT, kA.Mass, kA.Stiffness, kA.Damping, kA.InitialVelocity);
                        double sampleVal = kA.Value + springP * dv;

                        double ptX = _viewport.TimeToX(currT);
                        double ptY = _viewport.ValueToY(sampleVal);
                        fig.Segments.Add(new LineSegment(new Point(ptX, ptY), true));

                        if (settleT < 0 && Math.Abs(springP - 1.0) < 0.005 && localT > 0.15)
                        {
                            settleT = currT;
                        }
                    }

                    pathGeo.Figures.Add(fig);
                    var springBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber/Orange for spring #F59E0B
                    springBrush.Freeze();
                    var path = new Path { Data = pathGeo, Stroke = springBrush, StrokeThickness = 2.0, IsHitTestVisible = false };
                    Children.Add(path);

                    if (settleT > 0)
                    {
                        double sx = _viewport.TimeToX(settleT);
                        var settleLine = new Line
                        {
                            X1 = sx,
                            Y1 = _viewport.ValueToY(kB.Value) - 10,
                            X2 = sx,
                            Y2 = _viewport.ValueToY(kB.Value) + 10,
                            Stroke = new SolidColorBrush(Color.FromArgb(140, 245, 158, 11)),
                            StrokeThickness = 1.0,
                            StrokeDashArray = new DoubleCollection(new double[] { 2, 2 }),
                            IsHitTestVisible = false
                        };
                        Children.Add(settleLine);
                    }
                }
            }

            // 3. Draw flat post-end line
            var last = sorted[sorted.Count - 1];
            double endX = _viewport.TimeToX(last.Time);
            double endY = _viewport.ValueToY(last.Value);
            double canvasW = _viewport.CanvasWidth;
            if (endX < canvasW)
            {
                var postLine = new Line
                {
                    X1 = endX,
                    Y1 = endY,
                    X2 = canvasW,
                    Y2 = endY,
                    Stroke = curveBrush,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection(new double[] { 3, 3 }),
                    IsHitTestVisible = false
                };
                Children.Add(postLine);
            }
        }

        private void DrawKeyframeHandles()
        {
            var keys = _activeTrack.Keys;
            if (keys == null || keys.Count == 0) return;

            var sorted = new List<Keyframe>(keys);
            sorted.Sort((a, b) => a.Time.CompareTo(b.Time));

            var whiskerPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 89, 99, 118)), 1.2);
            whiskerPen.Freeze();

            var incomingGripBrush = new SolidColorBrush(Color.FromRgb(6, 182, 212)); // Teal #06B6D4
            incomingGripBrush.Freeze();
            var outgoingGripBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber #F59E0B
            outgoingGripBrush.Freeze();
            var gripBorderPen = new Pen(Brushes.White, 1.2);
            gripBorderPen.Freeze();

            for (int i = 0; i < sorted.Count; i++)
            {
                var kf = sorted[i];
                double kfX = _viewport.TimeToX(kf.Time);
                double kfY = _viewport.ValueToY(kf.Value);
                Point kfPoint = new Point(kfX, kfY);
                bool isSelected = (_selectedKeyframe != null && _selectedKeyframe.Id == kf.Id);

                // Outgoing handle: from kf to control point 1 of segment i
                if (i < sorted.Count - 1 && kf.Interp == InterpMode.Cubic)
                {
                    var nextKf = sorted[i + 1];
                    double dt = nextKf.Time - kf.Time;
                    double dv = nextKf.Value - kf.Value;
                    if (dt > 1e-6)
                    {
                        double c1T = kf.Time + kf.X1 * dt;
                        double c1V = kf.Value + kf.Y1 * dv;
                        Point p1 = new Point(_viewport.TimeToX(c1T), _viewport.ValueToY(c1V));

                        // Whisker line
                        var whisker = new Line
                        {
                            X1 = kfPoint.X,
                            Y1 = kfPoint.Y,
                            X2 = p1.X,
                            Y2 = p1.Y,
                            Stroke = whiskerPen.Brush,
                            StrokeThickness = whiskerPen.Thickness,
                            IsHitTestVisible = false
                        };
                        Children.Add(whisker);

                        // Amber Outgoing Grip Knob (10px hit-test radius)
                        var knob = new Ellipse
                        {
                            Width = 8,
                            Height = 8,
                            Fill = outgoingGripBrush,
                            Stroke = Brushes.White,
                            StrokeThickness = 1.2,
                            Cursor = Cursors.Hand,
                            Tag = new HandleHitInfo
                            {
                                Kind = HandleKind.Outgoing,
                                OwnerKeyframe = kf,
                                AttachedKeyframe = kf,
                                SegmentIndex = i,
                                KnobPoint = p1
                            }
                        };
                        Canvas.SetLeft(knob, p1.X - 4);
                        Canvas.SetTop(knob, p1.Y - 4);
                        Children.Add(knob);
                    }
                }

                // Incoming handle: to kf from control point 2 of segment i-1
                if (i > 0)
                {
                    var prevKf = sorted[i - 1];
                    if (prevKf.Interp == InterpMode.Cubic)
                    {
                        double dt = kf.Time - prevKf.Time;
                        double dv = kf.Value - prevKf.Value;
                        if (dt > 1e-6)
                        {
                            double c2T = prevKf.Time + prevKf.X2 * dt;
                            double c2V = prevKf.Value + prevKf.Y2 * dv;
                            Point p2 = new Point(_viewport.TimeToX(c2T), _viewport.ValueToY(c2V));

                            // Whisker line
                            var whisker = new Line
                            {
                                X1 = kfPoint.X,
                                Y1 = kfPoint.Y,
                                X2 = p2.X,
                                Y2 = p2.Y,
                                Stroke = whiskerPen.Brush,
                                StrokeThickness = whiskerPen.Thickness,
                                IsHitTestVisible = false
                            };
                            Children.Add(whisker);

                            // Teal Incoming Grip Knob (10px hit-test radius)
                            var knob = new Ellipse
                            {
                                Width = 8,
                                Height = 8,
                                Fill = incomingGripBrush,
                                Stroke = Brushes.White,
                                StrokeThickness = 1.2,
                                Cursor = Cursors.Hand,
                                Tag = new HandleHitInfo
                                {
                                    Kind = HandleKind.Incoming,
                                    OwnerKeyframe = prevKf,
                                    AttachedKeyframe = kf,
                                    SegmentIndex = i - 1,
                                    KnobPoint = p2
                                }
                            };
                            Canvas.SetLeft(knob, p2.X - 4);
                            Canvas.SetTop(knob, p2.Y - 4);
                            Children.Add(knob);
                        }
                    }
                }

                // Fixed Keyframe Diamond Node (D-05: Keyframe node positions are fixed on graph canvas)
                var diamond = new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(kfX, kfY - 5),
                        new Point(kfX + 5, kfY),
                        new Point(kfX, kfY + 5),
                        new Point(kfX - 5, kfY)
                    },
                    Fill = isSelected ? new SolidColorBrush(Color.FromRgb(255, 255, 255)) : new SolidColorBrush(Color.FromRgb(6, 182, 212)),
                    Stroke = isSelected ? new SolidColorBrush(Color.FromRgb(6, 182, 212)) : new SolidColorBrush(Color.FromRgb(24, 30, 42)),
                    StrokeThickness = 1.5,
                    Cursor = Cursors.Arrow,
                    ToolTip = string.Format("Keyframe at {0:0.00}s = {1:0.00}\n(Edit time/value in timeline/inspector)", kf.Time, kf.Value)
                };
                diamond.MouseLeftButtonDown += (s, e) =>
                {
                    _selectedKeyframe = kf;
                    if (KeyframeSelectionChanged != null) KeyframeSelectionChanged(kf);
                    Redraw();
                    e.Handled = true;
                };
                Children.Add(diamond);
            }
        }

        private void DrawPlayhead(double height)
        {
            double px = _viewport.TimeToX(_playheadTime);
            var needleBrush = new SolidColorBrush(Color.FromRgb(70, 239, 177)); // KerfBrand.MintBrush (#46EFB1)
            needleBrush.Freeze();

            var line = new Line
            {
                X1 = px,
                Y1 = 0,
                X2 = px,
                Y2 = height,
                Stroke = needleBrush,
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };
            Children.Add(line);

            // Small playhead marker at top
            var tag = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(px - 4, 0),
                    new Point(px + 4, 0),
                    new Point(px + 4, 6),
                    new Point(px, 10),
                    new Point(px - 4, 6)
                },
                Fill = needleBrush,
                IsHitTestVisible = false
            };
            Children.Add(tag);
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            Point pt = e.GetPosition(this);

            // 1. Check if clicking on a tangent handle grip knob (10px hit-test)
            HandleHitInfo hitHandle = FindHandleNearPoint(pt, 12.0);
            if (hitHandle != null)
            {
                StartHandleDragInternal(hitHandle, pt);
                CaptureMouse();
                e.Handled = true;
                return;
            }

            // 2. Otherwise start vertical/horizontal canvas panning
            _isPanning = true;
            _panStartMouse = pt;
            _panStartVMin = _viewport.VMin;
            _panStartVMax = _viewport.VMax;
            CaptureMouse();
            e.Handled = true;
        }

        public void StartHandleDrag(Keyframe ownerKf, HandleKind kind, Point? startMouse = null)
        {
            if (ownerKf == null || _activeTrack == null || _activeTrack.Keys == null) return;
            int segIdx = _activeTrack.Keys.IndexOf(ownerKf);
            if (segIdx < 0) return;
            Point mousePt = startMouse.HasValue ? startMouse.Value : new Point(0, 0);
            var hit = new HandleHitInfo
            {
                Kind = kind,
                OwnerKeyframe = ownerKf,
                AttachedKeyframe = (kind == HandleKind.Incoming && segIdx + 1 < _activeTrack.Keys.Count) ? _activeTrack.Keys[segIdx + 1] : ownerKf,
                SegmentIndex = segIdx,
                KnobPoint = mousePt
            };
            StartHandleDragInternal(hit, mousePt);
        }

        private void StartHandleDragInternal(HandleHitInfo hitHandle, Point mousePt)
        {
            _activeDragHandle = hitHandle;
            _dragStartMouse = mousePt;
            _origX1 = hitHandle.OwnerKeyframe.X1;
            _origY1 = hitHandle.OwnerKeyframe.Y1;
            _origX2 = hitHandle.OwnerKeyframe.X2;
            _origY2 = hitHandle.OwnerKeyframe.Y2;

            _selectedKeyframe = hitHandle.AttachedKeyframe;
            if (KeyframeSelectionChanged != null) KeyframeSelectionChanged(_selectedKeyframe);

            _handleDragStartSnapshot = new KeyframeBezierSnapshot(hitHandle.OwnerKeyframe);
            _dragStartSnapshots = CaptureTrackBezierSnapshots(_activeTrack);
        }

        public void MoveHandleDrag(Point currentMouse)
        {
            if (_activeDragHandle != null)
            {
                ProcessHandleDrag(currentMouse);
            }
        }

        public void CompleteHandleDrag()
        {
            if (_activeDragHandle != null)
            {
                var beforeSingle = _handleDragStartSnapshot;
                var afterSingle = (_activeDragHandle.OwnerKeyframe != null) ? new KeyframeBezierSnapshot(_activeDragHandle.OwnerKeyframe) : null;
                var beforeList = _dragStartSnapshots;
                var afterList = CaptureTrackBezierSnapshots(_activeTrack);

                _activeDragHandle = null;
                try { ReleaseMouseCapture(); } catch { }

                bool changed = false;
                if (beforeSingle != null && afterSingle != null)
                {
                    if (Math.Abs(beforeSingle.X1 - afterSingle.X1) > 1e-6 || Math.Abs(beforeSingle.Y1 - afterSingle.Y1) > 1e-6 ||
                        Math.Abs(beforeSingle.X2 - afterSingle.X2) > 1e-6 || Math.Abs(beforeSingle.Y2 - afterSingle.Y2) > 1e-6 ||
                        beforeSingle.Preset != afterSingle.Preset || beforeSingle.IsTangentBroken != afterSingle.IsTangentBroken)
                    {
                        changed = true;
                    }
                }
                if (!changed && beforeList != null && afterList != null && beforeList.Count == afterList.Count)
                {
                    for (int i = 0; i < beforeList.Count; i++)
                    {
                        var b = beforeList[i];
                        var a = afterList[i];
                        if (Math.Abs(b.X1 - a.X1) > 1e-6 || Math.Abs(b.Y1 - a.Y1) > 1e-6 ||
                            Math.Abs(b.X2 - a.X2) > 1e-6 || Math.Abs(b.Y2 - a.Y2) > 1e-6 ||
                            b.Preset != a.Preset || b.IsTangentBroken != a.IsTangentBroken)
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (changed)
                {
                    if (TangentHandleDragCommitted != null)
                    {
                        TangentHandleDragCommitted(beforeSingle, afterSingle);
                    }
                    if (TangentHandlesDragCommitted != null)
                    {
                        TangentHandlesDragCommitted(beforeList, afterList);
                    }
                }

                if (TrackModified != null) TrackModified(_activeTrack);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point pt = e.GetPosition(this);

            if (_activeDragHandle != null)
            {
                ProcessHandleDrag(pt);
                e.Handled = true;
                return;
            }

            if (_isPanning)
            {
                double dy = pt.Y - _panStartMouse.Y;
                double usableH = Math.Max(10.0, _viewport.CanvasHeight - 2.0 * _viewport.MarginY);
                double vSpan = _panStartVMax - _panStartVMin;
                double deltaV = (dy / usableH) * vSpan; // dragging down decreases VMin/VMax (moves graph down)

                _viewport.VMin = _panStartVMin + deltaV;
                _viewport.VMax = _panStartVMax + deltaV;
                Redraw();
                e.Handled = true;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_activeDragHandle != null)
            {
                CompleteHandleDrag();
                e.Handled = true;
                return;
            }

            if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void ProcessHandleDrag(Point currentMouse)
        {
            if (_activeDragHandle == null || _activeTrack == null || _activeTrack.Keys == null) return;

            var ownerKf = _activeDragHandle.OwnerKeyframe;
            var attachedKf = _activeDragHandle.AttachedKeyframe;
            int segIdx = _activeDragHandle.SegmentIndex;

            var sorted = new List<Keyframe>(_activeTrack.Keys);
            sorted.Sort((a, b) => a.Time.CompareTo(b.Time));

            if (segIdx < 0 || segIdx >= sorted.Count - 1) return;
            var kA = sorted[segIdx];
            var kB = sorted[segIdx + 1];

            double dt = kB.Time - kA.Time;
            double dv = kB.Value - kA.Value;
            if (dt <= 1e-6) return;

            // Convert currentMouse to time & value
            double mouseT = _viewport.XToTime(currentMouse.X);
            double mouseV = _viewport.YToValue(currentMouse.Y);

            bool isAltHeld = (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt));
            bool isShiftHeld = (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift));

            if (isAltHeld)
            {
                // Alt breaks continuity
                attachedKf.IsTangentBroken = true;
            }

            // Dragging Outgoing handle of kA: modifies ownerKf.X1, ownerKf.Y1
            if (_activeDragHandle.Kind == HandleKind.Outgoing)
            {
                double rawX1 = (mouseT - kA.Time) / dt;
                // Horizontal progress clamped to [0.0, 1.0] per D-07
                double x1 = Math.Max(0.0, Math.Min(1.0, rawX1));

                // Vertical progress allows free vertical overshoot per D-07
                double y1 = (Math.Abs(dv) > 1e-6) ? ((mouseV - kA.Value) / dv) : (mouseV - kA.Value);

                // Shift-drag snaps flat (0 slope) or 45 deg, magnetic snap to 0.0 / 1.0 per D-08
                if (isShiftHeld)
                {
                    y1 = ApplyShiftSnap(x1, y1);
                }

                ownerKf.X1 = x1;
                ownerKf.Y1 = y1;
                // Auto-switch to Custom Bézier per D-11
                ownerKf.Preset = EasingPreset.CustomBezier;

                // Collinear 180 deg continuity enforcement per D-06 (if not broken and has previous segment)
                if (!attachedKf.IsTangentBroken && segIdx > 0)
                {
                    var prevKf = sorted[segIdx - 1];
                    if (prevKf.Interp == InterpMode.Cubic)
                    {
                        AlignIncomingHandleCollinear(kA, prevKf, x1, y1, dt, dv);
                        prevKf.Preset = EasingPreset.CustomBezier;
                    }
                }
            }
            // Dragging Incoming handle of kB: modifies ownerKf.X2, ownerKf.Y2
            else if (_activeDragHandle.Kind == HandleKind.Incoming)
            {
                double rawX2 = (mouseT - kA.Time) / dt;
                // Horizontal progress clamped to [0.0, 1.0] per D-07
                double x2 = Math.Max(0.0, Math.Min(1.0, rawX2));

                // Vertical progress allows free vertical overshoot per D-07
                double y2 = (Math.Abs(dv) > 1e-6) ? ((mouseV - kA.Value) / dv) : 1.0 + (mouseV - kB.Value);

                if (isShiftHeld)
                {
                    y2 = ApplyShiftSnap(1.0 - x2, y2 - 1.0) + 1.0;
                }

                ownerKf.X2 = x2;
                ownerKf.Y2 = y2;
                // Auto-switch to Custom Bézier per D-11
                ownerKf.Preset = EasingPreset.CustomBezier;

                // Collinear 180 deg continuity enforcement per D-06 (if not broken and has next segment)
                if (!attachedKf.IsTangentBroken && segIdx + 1 < sorted.Count - 1)
                {
                    var nextKf = sorted[segIdx + 1];
                    if (nextKf.Interp == InterpMode.Cubic)
                    {
                        AlignOutgoingHandleCollinear(nextKf, sorted[segIdx + 2], x2, y2, dt, dv);
                        nextKf.Preset = EasingPreset.CustomBezier;
                    }
                }
            }

            Redraw();

            // Trigger real-time callback per D-12
            if (KeyframeModified != null)
            {
                KeyframeModified(ownerKf, _activeTrack);
            }
        }

        private static double ApplyShiftSnap(double dx, double dy)
        {
            // Magnetic snap to 0.0 and 1.0 if within 0.05
            if (Math.Abs(dy - 0.0) < 0.05) return 0.0;
            if (Math.Abs(dy - 1.0) < 0.05) return 1.0;

            // Angle snap: 0 deg (flat) or 45 deg
            double angle = Math.Atan2(dy, Math.Max(1e-4, dx));
            double deg = angle * 180.0 / Math.PI;

            if (Math.Abs(deg) < 15.0) return 0.0; // Snap flat (0 slope)
            if (Math.Abs(deg - 45.0) < 15.0) return dx * 1.0;
            if (Math.Abs(deg + 45.0) < 15.0) return dx * -1.0;

            return dy;
        }

        public static void AlignIncomingHandleCollinear(Keyframe centerKf, Keyframe prevSegmentKf, double outX1, double outY1, double dtOut, double dvOut)
        {
            double dtIn = centerKf.Time - prevSegmentKf.Time;
            double dvIn = centerKf.Value - prevSegmentKf.Value;
            if (dtIn <= 1e-6 || dtOut <= 1e-6) return;

            // Outgoing slope S = (outY1 * dvOut) / (outX1 * dtOut)
            double sOut = (Math.Abs(outX1) > 1e-6) ? (outY1 * dvOut) / (outX1 * dtOut) : 0.0;

            // Incoming handle length in time = 1.0 - prevSegmentKf.X2
            double inLenT = Math.Max(0.05, 1.0 - prevSegmentKf.X2);

            // To match S_out: ( (1 - Y2) * dvIn ) / ( inLenT * dtIn ) = S_out
            if (Math.Abs(dvIn) > 1e-6)
            {
                double targetY2 = 1.0 - (sOut * inLenT * dtIn) / dvIn;
                prevSegmentKf.Y2 = targetY2;
            }
            else
            {
                prevSegmentKf.Y2 = 1.0;
            }
        }

        public static void AlignOutgoingHandleCollinear(Keyframe centerKf, Keyframe nextSegmentKf, double inX2, double inY2, double dtIn, double dvIn)
        {
            double dtOut = nextSegmentKf.Time - centerKf.Time;
            double dvOut = nextSegmentKf.Value - centerKf.Value;
            if (dtIn <= 1e-6 || dtOut <= 1e-6) return;

            // Incoming slope S = ( (1 - inY2) * dvIn ) / ( (1 - inX2) * dtIn )
            double inLenT = Math.Max(1e-4, 1.0 - inX2);
            double sIn = ((1.0 - inY2) * dvIn) / (inLenT * dtIn);

            // Outgoing handle length in time = centerKf.X1
            double outLenT = Math.Max(0.05, centerKf.X1);

            // To match S_in: ( centerKf.Y1 * dvOut ) / ( outLenT * dtOut ) = S_in
            if (Math.Abs(dvOut) > 1e-6)
            {
                double targetY1 = (sIn * outLenT * dtOut) / dvOut;
                centerKf.Y1 = targetY1;
            }
            else
            {
                centerKf.Y1 = 0.0;
            }
        }

        private HandleHitInfo FindHandleNearPoint(Point pt, double radius)
        {
            double rSq = radius * radius;
            foreach (UIElement child in Children)
            {
                var el = child as FrameworkElement;
                if (el != null && el.Tag is HandleHitInfo)
                {
                    var info = (HandleHitInfo)el.Tag;
                    double dx = pt.X - info.KnobPoint.X;
                    double dy = pt.Y - info.KnobPoint.Y;
                    if (dx * dx + dy * dy <= rSq)
                    {
                        return info;
                    }
                }
            }
            return null;
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // Ctrl+Wheel: Zoom Value Axis per D-04
                double factor = e.Delta > 0 ? 0.85 : 1.15;
                Point pt = e.GetPosition(this);
                double mouseVal = _viewport.YToValue(pt.Y);

                double newVMin = mouseVal - (mouseVal - _viewport.VMin) * factor;
                double newVMax = mouseVal + (_viewport.VMax - mouseVal) * factor;

                if (newVMax - newVMin > 1e-4)
                {
                    _viewport.VMin = newVMin;
                    _viewport.VMax = newVMax;
                    Redraw();
                }
                e.Handled = true;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F)
            {
                // 'F' key auto-fits active track per D-04
                AutoFit();
                e.Handled = true;
            }
        }

        private static string FormatValue(double v)
        {
            if (Math.Abs(v) < 1e-4) return "0";
            if (Math.Abs(v) >= 1000.0) return string.Format(CultureInfo.InvariantCulture, "{0:0.0}k", v / 1000.0);
            if (Math.Abs(v) >= 10.0) return string.Format(CultureInfo.InvariantCulture, "{0:0.#}", v);
            return string.Format(CultureInfo.InvariantCulture, "{0:0.##}", v);
        }

        private static double NiceNum(double range, bool round)
        {
            double exponent = Math.Floor(Math.Log10(Math.Max(1e-6, range)));
            double fraction = range / Math.Pow(10, exponent);
            double niceFraction;

            if (round)
            {
                if (fraction < 1.5) niceFraction = 1;
                else if (fraction < 3) niceFraction = 2;
                else if (fraction < 7) niceFraction = 5;
                else niceFraction = 10;
            }
            else
            {
                if (fraction <= 1) niceFraction = 1;
                else if (fraction <= 2) niceFraction = 2;
                else if (fraction <= 5) niceFraction = 5;
                else niceFraction = 10;
            }

            return niceFraction * Math.Pow(10, exponent);
        }
    }
}
