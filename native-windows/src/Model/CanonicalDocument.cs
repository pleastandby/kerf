using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Kerf.Model
{
    public enum ElementType
    {
        Frame,
        Group,
        Rectangle,
        Ellipse,
        Star,
        Polygon,
        Line,
        Arrow,
        Path,
        Text,
        Image,
        ComponentInstance,
        Slice
    }

    public enum AutoLayoutMode
    {
        None,
        Horizontal,
        Vertical,
        Wrap,
        Grid
    }

    public enum AlignItems
    {
        Start,
        Center,
        End,
        Baseline
    }

    public enum JustifyContent
    {
        Start,
        Center,
        End,
        SpaceBetween,
        SpaceAround,
        SpaceEvenly
    }

    public enum SizingMode
    {
        Fixed,
        Hug,
        Fill
    }

    public enum ConstraintType
    {
        Left,
        Right,
        Center,
        LeftRight,
        Scale,
        Top,
        Bottom,
        TopBottom
    }

    public enum PaintType
    {
        Solid,
        LinearGradient,
        RadialGradient,
        Image
    }

    public enum StrokeAlignment
    {
        Inside,
        Center,
        Outside
    }

    public enum StrokeCap
    {
        Flat,
        Round,
        Square
    }

    public enum StrokeJoin
    {
        Miter,
        Round,
        Bevel
    }

    public enum EffectType
    {
        DropShadow,
        InnerShadow,
        LayerBlur,
        BackgroundBlur,
        LiquidGlass,
        Grain,
        Feather
    }

    public enum BlendMode
    {
        Normal,
        Multiply,
        Screen,
        Overlay,
        Darken,
        Lighten,
        ColorDodge,
        ColorBurn
    }

    public enum TextAlign
    {
        Left,
        Center,
        Right,
        Justify
    }

    public enum TextCase
    {
        Original,
        Upper,
        Lower,
        Title
    }

    public enum TextDecorationMode
    {
        None,
        Underline,
        Strikethrough
    }

    public enum VerticalAlign
    {
        Top,
        Middle,
        Bottom
    }

    public enum TextAutoResize
    {
        AutoWidth,
        AutoHeight,
        Fixed
    }

    public enum MaskType
    {
        Alpha,
        Vector,
        Luminance
    }

    public enum BooleanOp
    {
        None,
        Union,
        Subtract,
        Intersect,
        Exclude
    }

    public enum BezierNodeType
    {
        Corner,
        Smooth,
        Symmetric,
        Split
    }

    public enum TriggerType
    {
        OnClick,
        OnHover,
        OnDrag,
        AfterDelay
    }

    public enum ActionType
    {
        Navigate,
        OpenOverlay,
        ScrollTo,
        Back
    }

    public enum AnimationType
    {
        Instant,
        Dissolve,
        SmartAnimate,
        SlideLeft,
        SlideRight
    }

    public class BezierNode
    {
        public Point Anchor { get; set; }
        public Point InHandle { get; set; }
        public Point OutHandle { get; set; }
        public BezierNodeType Type { get; set; }

        public BezierNode()
        {
            Type = BezierNodeType.Corner;
        }

        public BezierNode(double x, double y)
        {
            Anchor = new Point(x, y);
            InHandle = new Point(x, y);
            OutHandle = new Point(x, y);
            Type = BezierNodeType.Corner;
        }

        public BezierNode Clone()
        {
            return new BezierNode
            {
                Anchor = new Point(Anchor.X, Anchor.Y),
                InHandle = new Point(InHandle.X, InHandle.Y),
                OutHandle = new Point(OutHandle.X, OutHandle.Y),
                Type = this.Type
            };
        }
    }

    public class VectorGeometry
    {
        public List<BezierNode> Nodes { get; set; }
        public bool IsClosed { get; set; }
        public string SvgPathData { get; set; }

        public VectorGeometry()
        {
            Nodes = new List<BezierNode>();
            IsClosed = false;
        }

        public VectorGeometry Clone()
        {
            var vg = new VectorGeometry
            {
                IsClosed = this.IsClosed,
                SvgPathData = this.SvgPathData,
                Nodes = new List<BezierNode>()
            };
            if (Nodes != null)
            {
                foreach (var n in Nodes)
                {
                    vg.Nodes.Add(n.Clone());
                }
            }
            return vg;
        }
    }

    public class GradientStopData
    {
        public double Position { get; set; }
        public string ColorHex { get; set; }
        public double Opacity { get; set; }
        public double Midpoint { get; set; }

        public GradientStopData()
        {
            Opacity = 1.0;
            Midpoint = 0.5;
        }

        public GradientStopData(double pos, string hex, double op = 1.0, double mid = 0.5)
        {
            Position = pos;
            ColorHex = hex;
            Opacity = op;
            Midpoint = mid;
        }

        public GradientStopData Clone()
        {
            return new GradientStopData(Position, ColorHex, Opacity, Midpoint);
        }
    }

    public class Paint
    {
        public PaintType Type { get; set; }
        public string ColorHex { get; set; }
        public double Opacity { get; set; }
        public bool Enabled { get; set; }

        // Gradients
        public double GradientAngle { get; set; }
        public Point? GradientStart { get; set; }
        public Point? GradientEnd { get; set; }
        public Point GradientCenter { get; set; }
        public double GradientRadius { get; set; }
        public List<GradientStopData> GradientStops { get; set; }

        // Image
        public string ImageRef { get; set; }
        public string ImageFillMode { get; set; } // Fill, Fit, Crop, Tile

        public Paint()
        {
            Type = PaintType.Solid;
            ColorHex = "#D9D9D9";
            Opacity = 1.0;
            Enabled = true;
            GradientAngle = 90.0;
            GradientStart = null;
            GradientEnd = null;
            GradientCenter = new Point(0.5, 0.5);
            GradientRadius = 0.5;
            GradientStops = new List<GradientStopData>();
            ImageFillMode = "Fill";
        }

        public static Paint Solid(string hex, double opacity = 1.0)
        {
            return new Paint
            {
                Type = PaintType.Solid,
                ColorHex = hex,
                Opacity = opacity,
                Enabled = true
            };
        }

        public static Paint LinearGradient(string startHex, string endHex, double angle = 90.0)
        {
            var p = new Paint
            {
                Type = PaintType.LinearGradient,
                GradientAngle = angle,
                Enabled = true
            };
            p.GradientStops.Add(new GradientStopData(0.0, startHex, 1.0));
            p.GradientStops.Add(new GradientStopData(1.0, endHex, 1.0));
            return p;
        }

        public Paint Clone()
        {
            var p = new Paint
            {
                Type = this.Type,
                ColorHex = this.ColorHex,
                Opacity = this.Opacity,
                Enabled = this.Enabled,
                GradientAngle = this.GradientAngle,
                GradientStart = this.GradientStart,
                GradientEnd = this.GradientEnd,
                GradientCenter = new Point(this.GradientCenter.X, this.GradientCenter.Y),
                GradientRadius = this.GradientRadius,
                ImageRef = this.ImageRef,
                ImageFillMode = this.ImageFillMode,
                GradientStops = new List<GradientStopData>()
            };
            if (GradientStops != null)
            {
                foreach (var s in GradientStops)
                {
                    p.GradientStops.Add(s.Clone());
                }
            }
            return p;
        }
    }

    public class StrokeData
    {
        public Paint Paint { get; set; }
        public double Width { get; set; }
        public StrokeAlignment Alignment { get; set; }
        public StrokeCap Cap { get; set; }
        public StrokeJoin Join { get; set; }
        public List<double> DashPattern { get; set; }
        public bool Enabled { get; set; }

        public StrokeData()
        {
            Paint = Paint.Solid("#000000");
            Width = 1.0;
            Alignment = StrokeAlignment.Center;
            Cap = StrokeCap.Flat;
            Join = StrokeJoin.Miter;
            DashPattern = new List<double>();
            Enabled = true;
        }

        public StrokeData Clone()
        {
            var s = new StrokeData
            {
                Paint = this.Paint != null ? this.Paint.Clone() : Paint.Solid("#000000"),
                Width = this.Width,
                Alignment = this.Alignment,
                Cap = this.Cap,
                Join = this.Join,
                Enabled = this.Enabled,
                DashPattern = new List<double>()
            };
            if (DashPattern != null)
            {
                s.DashPattern.AddRange(DashPattern);
            }
            return s;
        }
    }

    public class EffectData
    {
        public EffectType Type { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double Direction { get; set; } // Angle in degrees (0 to 360)
        public double Distance { get; set; }  // Radial offset magnitude
        public double Blur { get; set; }
        public double Spread { get; set; }
        public double Intensity { get; set; } // Grain intensity or Glass frosted strength
        public double Scale { get; set; }     // Grain scale
        public string ColorHex { get; set; }
        public double Opacity { get; set; }
        public bool Enabled { get; set; }
        public bool IsExpanded { get; set; }

        public EffectData()
        {
            Type = EffectType.DropShadow;
            OffsetX = 0;
            OffsetY = 4;
            Direction = 90.0;
            Distance = 4.0;
            Blur = 8;
            Spread = 0;
            Intensity = 0.25;
            Scale = 1.0;
            ColorHex = "#000000";
            Opacity = 0.25;
            Enabled = true;
            IsExpanded = true;
        }

        public void UpdateOffsetsFromPolar()
        {
            double rad = Direction * (Math.PI / 180.0);
            OffsetX = Math.Round(Distance * Math.Cos(rad), 2);
            OffsetY = Math.Round(Distance * Math.Sin(rad), 2);
        }

        public void UpdatePolarFromOffsets()
        {
            Distance = Math.Round(Math.Sqrt(OffsetX * OffsetX + OffsetY * OffsetY), 2);
            double deg = Math.Atan2(OffsetY, OffsetX) * (180.0 / Math.PI);
            if (deg < 0) deg += 360.0;
            Direction = Math.Round(deg, 1);
        }

        public EffectData Clone()
        {
            return new EffectData
            {
                Type = this.Type,
                OffsetX = this.OffsetX,
                OffsetY = this.OffsetY,
                Direction = this.Direction,
                Distance = this.Distance,
                Blur = this.Blur,
                Spread = this.Spread,
                Intensity = this.Intensity,
                Scale = this.Scale,
                ColorHex = this.ColorHex,
                Opacity = this.Opacity,
                Enabled = this.Enabled,
                IsExpanded = this.IsExpanded
            };
        }
    }

    public class TypographyData
    {
        public string Text { get; set; }
        public string FontFamily { get; set; }
        public double FontSize { get; set; }
        public int FontWeight { get; set; }
        public bool IsItalic { get; set; }
        public double LineHeight { get; set; }
        public double LetterSpacing { get; set; }
        public TextAlign Align { get; set; }
        public VerticalAlign VerticalAlign { get; set; }
        public TextAutoResize AutoResize { get; set; }
        public TextCase Case { get; set; }
        public TextDecorationMode Decoration { get; set; }

        public TypographyData()
        {
            Text = "Typography";
            FontFamily = "Segoe UI, Arial, sans-serif";
            FontSize = 16.0;
            FontWeight = 400;
            IsItalic = false;
            LineHeight = 1.3;
            LetterSpacing = 0.0;
            Align = TextAlign.Left;
            VerticalAlign = VerticalAlign.Top;
            AutoResize = TextAutoResize.AutoWidth;
            Case = TextCase.Original;
            Decoration = TextDecorationMode.None;
        }

        public TypographyData Clone()
        {
            return new TypographyData
            {
                Text = this.Text,
                FontFamily = this.FontFamily,
                FontSize = this.FontSize,
                FontWeight = this.FontWeight,
                IsItalic = this.IsItalic,
                LineHeight = this.LineHeight,
                LetterSpacing = this.LetterSpacing,
                Align = this.Align,
                VerticalAlign = this.VerticalAlign,
                AutoResize = this.AutoResize,
                Case = this.Case,
                Decoration = this.Decoration
            };
        }
    }

    public class AutoLayoutData
    {
        public AutoLayoutMode Mode { get; set; }
        public double Gap { get; set; }
        public double PaddingTop { get; set; }
        public double PaddingRight { get; set; }
        public double PaddingBottom { get; set; }
        public double PaddingLeft { get; set; }
        public AlignItems AlignItems { get; set; }
        public JustifyContent JustifyContent { get; set; }
        public SizingMode SizingH { get; set; }
        public SizingMode SizingV { get; set; }
        public int GridColumns { get; set; }

        public AutoLayoutData()
        {
            Mode = AutoLayoutMode.None;
            Gap = 0.0;
            PaddingTop = 16.0;
            PaddingRight = 16.0;
            PaddingBottom = 16.0;
            PaddingLeft = 16.0;
            AlignItems = AlignItems.Start;
            JustifyContent = JustifyContent.Start;
            SizingH = SizingMode.Hug;
            SizingV = SizingMode.Hug;
            GridColumns = 2;
        }

        public AutoLayoutData Clone()
        {
            return new AutoLayoutData
            {
                Mode = this.Mode,
                Gap = this.Gap,
                PaddingTop = this.PaddingTop,
                PaddingRight = this.PaddingRight,
                PaddingBottom = this.PaddingBottom,
                PaddingLeft = this.PaddingLeft,
                AlignItems = this.AlignItems,
                JustifyContent = this.JustifyContent,
                SizingH = this.SizingH,
                SizingV = this.SizingV,
                GridColumns = this.GridColumns
            };
        }
    }

    public class PrototypeData
    {
        public TriggerType Trigger { get; set; }
        public ActionType Action { get; set; }
        public string TargetNodeId { get; set; }
        public AnimationType Animation { get; set; }
        public int DurationMs { get; set; }

        public PrototypeData()
        {
            Trigger = TriggerType.OnClick;
            Action = ActionType.Navigate;
            TargetNodeId = null;
            Animation = AnimationType.Instant;
            DurationMs = 200;
        }

        public PrototypeData Clone()
        {
            return new PrototypeData
            {
                Trigger = this.Trigger,
                Action = this.Action,
                TargetNodeId = this.TargetNodeId,
                Animation = this.Animation,
                DurationMs = this.DurationMs
            };
        }
    }

    public class LayerNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public ElementType Type { get; set; }

        // Transform & Bounds in parent coordinates
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
        public bool FlipH { get; set; }
        public bool FlipV { get; set; }

        // Corner Radius
        public double CornerRadius { get; set; }
        public double CornerRadiusTopLeft { get; set; }
        public double CornerRadiusTopRight { get; set; }
        public double CornerRadiusBottomRight { get; set; }
        public double CornerRadiusBottomLeft { get; set; }

        // Fills, Strokes, Effects
        public List<Paint> Fills { get; set; }
        public List<StrokeData> Strokes { get; set; }
        public List<EffectData> Effects { get; set; }

        // Opacity & Blend Mode
        public double Opacity { get; set; }
        public BlendMode BlendMode { get; set; }

        // Geometry & Typography
        public VectorGeometry Geometry { get; set; }
        public TypographyData Typography { get; set; }

        // Layout & Constraints
        public AutoLayoutData AutoLayout { get; set; }
        public bool IgnoreAutoLayout { get; set; }
        public SizingMode LayoutSizingH { get; set; }
        public SizingMode LayoutSizingV { get; set; }
        public double MinWidth { get; set; }
        public double MaxWidth { get; set; }
        public double MinHeight { get; set; }
        public double MaxHeight { get; set; }
        public ConstraintType ConstraintH { get; set; }
        public ConstraintType ConstraintV { get; set; }

        // Masking, Clipping & Boolean
        public bool ClipContent { get; set; }
        public bool IsMask { get; set; }
        public MaskType MaskType { get; set; }
        public BooleanOp BooleanOperation { get; set; }

        // Components & Instances
        public string ComponentId { get; set; }
        public bool IsMasterComponent { get; set; }

        // Prototyping
        public PrototypeData Prototype { get; set; }

        // Flags & Hierarchy
        public bool IsVisible { get; set; }
        public bool IsLocked { get; set; }
        public bool IsCollapsed { get; set; }
        public string ParentId { get; set; }
        public List<LayerNode> Children { get; set; }

        public LayerNode()
        {
            Id = Guid.NewGuid().ToString().Substring(0, 8);
            Name = "Layer";
            Type = ElementType.Rectangle;
            X = 100;
            Y = 100;
            Width = 120;
            Height = 80;
            Rotation = 0.0;
            ScaleX = 1.0;
            ScaleY = 1.0;
            CornerRadius = 0.0;

            Fills = new List<Paint> { Paint.Solid("#D9D9D9") };
            Strokes = new List<StrokeData>();
            Effects = new List<EffectData>();

            Opacity = 1.0;
            BlendMode = BlendMode.Normal;

            Geometry = new VectorGeometry();
            Typography = new TypographyData();
            AutoLayout = new AutoLayoutData();
            IgnoreAutoLayout = false;
            LayoutSizingH = SizingMode.Fixed;
            LayoutSizingV = SizingMode.Fixed;
            MinWidth = 0.0;
            MaxWidth = 100000.0;
            MinHeight = 0.0;
            MaxHeight = 100000.0;
            ConstraintH = ConstraintType.Left;
            ConstraintV = ConstraintType.Top;

            ClipContent = false;
            IsMask = false;
            MaskType = MaskType.Alpha;
            BooleanOperation = BooleanOp.None;

            IsVisible = true;
            IsLocked = false;
            IsCollapsed = false;
            ParentId = null;
            Children = new List<LayerNode>();
        }

        public Rect GetBounds()
        {
            return new Rect(X, Y, Math.Max(1, Width), Math.Max(1, Height));
        }

        public Matrix GetTransformMatrix()
        {
            var mat = Matrix.Identity;
            if (Rotation != 0.0)
            {
                mat.RotateAt(Rotation, X + Width / 2.0, Y + Height / 2.0);
            }
            if (ScaleX != 1.0 || ScaleY != 1.0)
            {
                mat.ScaleAt(ScaleX, ScaleY, X + Width / 2.0, Y + Height / 2.0);
            }
            return mat;
        }

        public LayerNode Clone()
        {
            var n = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = this.Name + " Copy",
                Type = this.Type,
                X = this.X,
                Y = this.Y,
                Width = this.Width,
                Height = this.Height,
                Rotation = this.Rotation,
                ScaleX = this.ScaleX,
                ScaleY = this.ScaleY,
                FlipH = this.FlipH,
                FlipV = this.FlipV,
                CornerRadius = this.CornerRadius,
                CornerRadiusTopLeft = this.CornerRadiusTopLeft,
                CornerRadiusTopRight = this.CornerRadiusTopRight,
                CornerRadiusBottomRight = this.CornerRadiusBottomRight,
                CornerRadiusBottomLeft = this.CornerRadiusBottomLeft,
                Opacity = this.Opacity,
                BlendMode = this.BlendMode,
                ConstraintH = this.ConstraintH,
                ConstraintV = this.ConstraintV,
                ClipContent = this.ClipContent,
                IsMask = this.IsMask,
                MaskType = this.MaskType,
                BooleanOperation = this.BooleanOperation,
                ComponentId = this.ComponentId,
                IsMasterComponent = this.IsMasterComponent,
                IgnoreAutoLayout = this.IgnoreAutoLayout,
                LayoutSizingH = this.LayoutSizingH,
                LayoutSizingV = this.LayoutSizingV,
                MinWidth = this.MinWidth,
                MaxWidth = this.MaxWidth,
                MinHeight = this.MinHeight,
                MaxHeight = this.MaxHeight,
                IsVisible = this.IsVisible,
                IsLocked = this.IsLocked,
                IsCollapsed = this.IsCollapsed,
                ParentId = this.ParentId,
                Fills = new List<Paint>(),
                Strokes = new List<StrokeData>(),
                Effects = new List<EffectData>(),
                Children = new List<LayerNode>()
            };

            if (this.Fills != null)
            {
                foreach (var f in this.Fills) n.Fills.Add(f.Clone());
            }
            if (this.Strokes != null)
            {
                foreach (var s in this.Strokes) n.Strokes.Add(s.Clone());
            }
            if (this.Effects != null)
            {
                foreach (var e in this.Effects) n.Effects.Add(e.Clone());
            }
            if (this.Geometry != null)
            {
                n.Geometry = this.Geometry.Clone();
            }
            if (this.Typography != null)
            {
                n.Typography = this.Typography.Clone();
            }
            if (this.AutoLayout != null)
            {
                n.AutoLayout = this.AutoLayout.Clone();
            }
            if (this.Prototype != null)
            {
                n.Prototype = this.Prototype.Clone();
            }
            if (this.Children != null)
            {
                foreach (var child in this.Children)
                {
                    var childClone = child.Clone();
                    childClone.ParentId = n.Id;
                    n.Children.Add(childClone);
                }
            }

            return n;
        }
    }

    public class GuideLine
    {
        public string Id { get; set; }
        public bool IsVertical { get; set; }
        public double Position { get; set; }

        public GuideLine()
        {
            Id = Guid.NewGuid().ToString().Substring(0, 8);
        }

        public GuideLine(bool isVert, double pos)
        {
            Id = Guid.NewGuid().ToString().Substring(0, 8);
            IsVertical = isVert;
            Position = pos;
        }
    }

    public class Page
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string BackgroundColor { get; set; }
        public bool ShowGrid { get; set; }
        public string GridType { get; set; } // dots, lines, isometric, none
        public double GridSize { get; set; }
        public List<GuideLine> Guides { get; set; }
        public List<LayerNode> Layers { get; set; }

        public Page()
        {
            Id = Guid.NewGuid().ToString().Substring(0, 8);
            Name = "Page 1";
            BackgroundColor = "#1E1E22";
            ShowGrid = true;
            GridType = "dots";
            GridSize = 32.0;
            Guides = new List<GuideLine>();
            Layers = new List<LayerNode>();
        }

        public Page Clone()
        {
            var p = new Page
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = this.Name + " Copy",
                BackgroundColor = this.BackgroundColor,
                ShowGrid = this.ShowGrid,
                GridType = this.GridType,
                GridSize = this.GridSize,
                Guides = new List<GuideLine>(),
                Layers = new List<LayerNode>()
            };
            if (Guides != null)
            {
                foreach (var g in Guides) p.Guides.Add(new GuideLine(g.IsVertical, g.Position));
            }
            if (Layers != null)
            {
                foreach (var l in Layers) p.Layers.Add(l.Clone());
            }
            return p;
        }

        public List<LayerNode> FindContainer(LayerNode node)
        {
            LayerNode parent;
            return FindContainer(node, out parent);
        }

        public List<LayerNode> FindContainer(LayerNode node, out LayerNode parent)
        {
            parent = null;
            if (node == null || Layers == null) return null;
            if (Layers.Contains(node)) return Layers;

            foreach (var root in Layers)
            {
                var container = FindContainerRecursive(root, node, out parent);
                if (container != null) return container;
            }
            return null;
        }

        private List<LayerNode> FindContainerRecursive(LayerNode current, LayerNode target, out LayerNode parent)
        {
            parent = null;
            if (current == null || current.Children == null) return null;

            if (current.Children.Contains(target))
            {
                parent = current;
                return current.Children;
            }

            foreach (var child in current.Children)
            {
                var res = FindContainerRecursive(child, target, out parent);
                if (res != null) return res;
            }
            return null;
        }

        public bool RemoveLayerEverywhere(LayerNode node)
        {
            if (node == null || Layers == null) return false;
            if (Layers.Remove(node)) return true;

            foreach (var root in Layers)
            {
                if (RemoveRecursive(root, node)) return true;
            }
            return false;
        }

        private bool RemoveRecursive(LayerNode current, LayerNode target)
        {
            if (current == null || current.Children == null) return false;
            if (current.Children.Remove(target)) return true;

            foreach (var child in current.Children)
            {
                if (RemoveRecursive(child, target)) return true;
            }
            return false;
        }

        public LayerNode FindLayerById(string id)
        {
            if (string.IsNullOrEmpty(id) || Layers == null) return null;
            foreach (var root in Layers)
            {
                if (root.Id == id) return root;
                var found = FindLayerByIdRecursive(root, id);
                if (found != null) return found;
            }
            return null;
        }

        private LayerNode FindLayerByIdRecursive(LayerNode current, string id)
        {
            if (current == null || current.Children == null) return null;
            foreach (var child in current.Children)
            {
                if (child.Id == id) return child;
                var found = FindLayerByIdRecursive(child, id);
                if (found != null) return found;
            }
            return null;
        }

        public int CountAllLayers()
        {
            int count = 0;
            CountRecursive(Layers, ref count);
            return count;
        }

        public IEnumerable<LayerNode> GetAllLayersFlat()
        {
            var list = new List<LayerNode>();
            CollectLayersFlat(Layers, list);
            return list;
        }

        private void CollectLayersFlat(List<LayerNode> nodes, List<LayerNode> output)
        {
            if (nodes == null) return;
            foreach (var n in nodes)
            {
                output.Add(n);
                if (n.Children != null && n.Children.Count > 0)
                {
                    CollectLayersFlat(n.Children, output);
                }
            }
        }

        private void CountRecursive(List<LayerNode> nodes, ref int count)
        {
            if (nodes == null) return;
            count += nodes.Count;
            foreach (var n in nodes)
            {
                if (n.Children != null && n.Children.Count > 0)
                {
                    CountRecursive(n.Children, ref count);
                }
            }
        }

        public static bool IsDescendant(LayerNode potentialParent, LayerNode potentialChild)
        {
            if (potentialParent == null || potentialChild == null) return false;
            if (potentialParent == potentialChild) return true;
            if (potentialParent.Children == null) return false;
            foreach (var child in potentialParent.Children)
            {
                if (child == potentialChild || IsDescendant(child, potentialChild)) return true;
            }
            return false;
        }

        public LayerNode FindMasterComponent(string componentId)
        {
            if (string.IsNullOrEmpty(componentId)) return null;
            return GetAllLayersFlat().FirstOrDefault(l => l.IsMasterComponent && l.ComponentId == componentId);
        }

        public List<LayerNode> FindInstancesOfComponent(string componentId)
        {
            if (string.IsNullOrEmpty(componentId)) return new List<LayerNode>();
            return GetAllLayersFlat().Where(l => !l.IsMasterComponent && l.ComponentId == componentId).ToList();
        }
    }

    public class KerfDocument
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SchemaVersion { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
        public List<Page> Pages { get; set; }
        public string ActivePageId { get; set; }
        public Dictionary<string, string> AssetLibrary { get; set; } // id -> data URI or path

        public KerfDocument()
        {
            Id = Guid.NewGuid().ToString();
            Name = "Untitled Project";
            SchemaVersion = "3.0.0";
            CreatedAt = DateTime.UtcNow.Ticks;
            UpdatedAt = DateTime.UtcNow.Ticks;
            Pages = new List<Page>();
            var defaultPage = new Page { Name = "Page 1" };
            Pages.Add(defaultPage);
            ActivePageId = defaultPage.Id;
            AssetLibrary = new Dictionary<string, string>();
        }

        public Page GetActivePage()
        {
            if (Pages == null || Pages.Count == 0)
            {
                var p = new Page();
                Pages = new List<Page> { p };
                ActivePageId = p.Id;
                return p;
            }
            var active = Pages.FirstOrDefault(p => p.Id == ActivePageId);
            return active ?? Pages[0];
        }

        public IEnumerable<LayerNode> GetAllLayersFlat()
        {
            var page = GetActivePage();
            var list = new List<LayerNode>();
            CollectLayersRecursive(page.Layers, list);
            return list;
        }

        private void CollectLayersRecursive(List<LayerNode> nodes, List<LayerNode> output)
        {
            if (nodes == null) return;
            foreach (var n in nodes)
            {
                output.Add(n);
                if (n.Children != null && n.Children.Count > 0)
                {
                    CollectLayersRecursive(n.Children, output);
                }
            }
        }
    }
}
