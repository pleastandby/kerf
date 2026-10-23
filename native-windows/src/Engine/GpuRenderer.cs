using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Kerf.Model;
using IOPath = System.IO.Path;

namespace Kerf.Engine
{
    public class GpuRenderer
    {
        private readonly Dictionary<string, BitmapSource> _imageCache;
        private readonly bool _isHardwareTier;

        public bool IsHardwareTier { get { return _isHardwareTier; } }

        public GpuRenderer()
        {
            _imageCache = new Dictionary<string, BitmapSource>();
            int tier = RenderCapability.Tier >> 16;
            _isHardwareTier = tier >= 2;
        }

        public void RenderNodeToVisual(LayerNode node, DrawingVisual visual, double zoomScale)
        {
            if (node == null || visual == null) return;

            // Apply direct GPU pixel shader effects
            ApplyGpuEffects(node, visual);

            // Hardware clip visual and all child layers to the frame's exact shape & corner radius
            if (node.Type == ElementType.Frame && node.ClipContent)
            {
                var clipGeo = BuildGeometry(node);
                if (clipGeo != null)
                {
                    clipGeo.Freeze();
                    visual.Clip = clipGeo;
                }
                else
                {
                    visual.Clip = null;
                }
            }
            else
            {
                visual.Clip = null;
            }

            using (var dc = visual.RenderOpen())
            {
                if (!node.IsVisible || node.Opacity <= 0.0)
                {
                    return;
                }

                // Push node opacity if < 1.0
                bool pushedOpacity = false;
                if (node.Opacity < 1.0)
                {
                    dc.PushOpacity(node.Opacity);
                    pushedOpacity = true;
                }

                // Push Transform (Rotation, Scale)
                bool pushedTransform = false;
                var matrix = node.GetTransformMatrix();
                if (!matrix.IsIdentity)
                {
                    dc.PushTransform(new MatrixTransform(matrix));
                    pushedTransform = true;
                }

                // Push Clip if Frame has ClipContent
                bool pushedClip = false;
                if (node.Type == ElementType.Frame && node.ClipContent)
                {
                    var clipGeo = BuildGeometry(node);
                    if (clipGeo != null)
                    {
                        dc.PushClip(clipGeo);
                        pushedClip = true;
                    }
                }

                // Draw Drop Shadow (CPU fallback pass if shader effect not supported)
                if (!_isHardwareTier)
                {
                    RenderSoftwareShadowPass(node, dc);
                }

                // Draw Shape / Content
                RenderNodeContent(node, dc, zoomScale);

                // Draw Inner Shadow pass
                RenderInnerShadowPass(node, dc);

                // Pop clip / transform / opacity
                if (pushedClip) dc.Pop();
                if (pushedTransform) dc.Pop();
                if (pushedOpacity) dc.Pop();
            }
        }

        private void ApplyGpuEffects(LayerNode node, DrawingVisual visual)
        {
            if (node.Effects == null || node.Effects.Count == 0)
            {
                visual.Effect = null;
                return;
            }

            // Find first active effect
            var dropShadow = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.DropShadow);
            var blur = node.Effects.FirstOrDefault(e => e.Enabled && (e.Type == EffectType.LayerBlur || e.Type == EffectType.Feather));

            if (dropShadow != null && _isHardwareTier)
            {
                double depth = Math.Sqrt(dropShadow.OffsetX * dropShadow.OffsetX + dropShadow.OffsetY * dropShadow.OffsetY);
                double angle = Math.Atan2(dropShadow.OffsetY, dropShadow.OffsetX) * (180.0 / Math.PI);
                Color shadowColor = ParseHexColor(dropShadow.ColorHex, 1.0);

                var dse = new DropShadowEffect
                {
                    BlurRadius = Math.Max(1.0, dropShadow.Blur),
                    ShadowDepth = depth,
                    Direction = angle,
                    Color = shadowColor,
                    Opacity = Math.Max(0.0, Math.Min(1.0, dropShadow.Opacity)),
                    RenderingBias = RenderingBias.Performance
                };
                visual.Effect = dse;
            }
            else if (blur != null && _isHardwareTier)
            {
                var be = new BlurEffect
                {
                    Radius = Math.Max(1.0, blur.Blur),
                    KernelType = KernelType.Gaussian,
                    RenderingBias = RenderingBias.Performance
                };
                visual.Effect = be;
            }
            else
            {
                visual.Effect = null;
            }
        }

        private void RenderSoftwareShadowPass(LayerNode node, DrawingContext dc)
        {
            if (node.Effects == null) return;
            var dropShadow = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.DropShadow);
            if (dropShadow == null || dropShadow.Opacity <= 0.0) return;

            var geo = BuildGeometry(node);
            if (geo == null) return;

            // Draw multi-layered soft outline for software shadow
            Color baseColor = ParseHexColor(dropShadow.ColorHex, dropShadow.Opacity * 0.4);
            int passes = Math.Min(4, (int)Math.Max(1, dropShadow.Blur / 4.0));
            for (int i = passes; i >= 1; i--)
            {
                double spread = (i * dropShadow.Blur) / passes;
                double alpha = (dropShadow.Opacity * 0.3) / passes;
                var shadowBrush = new SolidColorBrush(Color.FromArgb(
                    (byte)(alpha * 255),
                    baseColor.R,
                    baseColor.G,
                    baseColor.B));
                shadowBrush.Freeze();

                var trans = new TranslateTransform(dropShadow.OffsetX, dropShadow.OffsetY);
                dc.PushTransform(trans);
                var shadowPen = new Pen(shadowBrush, spread * 2);
                shadowPen.Freeze();
                dc.DrawGeometry(shadowBrush, shadowPen, geo);
                dc.Pop();
            }
        }

        private void RenderInnerShadowPass(LayerNode node, DrawingContext dc)
        {
            if (node.Effects == null) return;
            var innerShadow = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.InnerShadow);
            if (innerShadow == null || innerShadow.Opacity <= 0.0) return;

            var geo = BuildGeometry(node);
            if (geo == null) return;

            // Clip inner shadow to the shape itself
            dc.PushClip(geo);

            Color color = ParseHexColor(innerShadow.ColorHex, innerShadow.Opacity);
            var shadowBrush = new SolidColorBrush(color);
            shadowBrush.Freeze();

            var pen = new Pen(shadowBrush, Math.Max(2.0, innerShadow.Blur));
            pen.Freeze();

            var trans = new TranslateTransform(innerShadow.OffsetX, innerShadow.OffsetY);
            dc.PushTransform(trans);
            dc.DrawGeometry(null, pen, geo);
            dc.Pop();

            dc.Pop(); // pop clip
        }

        private void RenderNodeContent(LayerNode node, DrawingContext dc, double zoomScale)
        {
            switch (node.Type)
            {
                case ElementType.Text:
                    RenderText(node, dc);
                    break;

                case ElementType.Image:
                    RenderImage(node, dc);
                    break;

                case ElementType.Frame:
                case ElementType.Group:
                case ElementType.Rectangle:
                case ElementType.Ellipse:
                case ElementType.Star:
                case ElementType.Polygon:
                case ElementType.Line:
                case ElementType.Arrow:
                case ElementType.Path:
                default:
                    RenderVectorGeometry(node, dc);
                    break;
            }
        }

        public Geometry BuildGeometry(LayerNode node)
        {
            if (node == null) return null;

            double x = node.X;
            double y = node.Y;
            double w = Math.Max(1.0, node.Width);
            double h = Math.Max(1.0, node.Height);

            switch (node.Type)
            {
                case ElementType.Rectangle:
                case ElementType.Frame:
                case ElementType.Group:
                case ElementType.Image:
                    if (node.CornerRadius > 0.0)
                    {
                        return new RectangleGeometry(new Rect(x, y, w, h), node.CornerRadius, node.CornerRadius);
                    }
                    if (node.CornerRadiusTopLeft > 0.0 || node.CornerRadiusTopRight > 0.0 ||
                        node.CornerRadiusBottomRight > 0.0 || node.CornerRadiusBottomLeft > 0.0)
                    {
                        return BuildRoundedRectGeometry(x, y, w, h,
                            node.CornerRadiusTopLeft, node.CornerRadiusTopRight,
                            node.CornerRadiusBottomRight, node.CornerRadiusBottomLeft);
                    }
                    return new RectangleGeometry(new Rect(x, y, w, h));

                case ElementType.Ellipse:
                    return new EllipseGeometry(new Rect(x, y, w, h));

                case ElementType.Star:
                    return BuildStarGeometry(x, y, w, h, 5, 0.45);

                case ElementType.Polygon:
                    return BuildPolygonGeometry(x, y, w, h, 6);

                case ElementType.Line:
                    return new LineGeometry(new Point(x, y), new Point(x + w, y + h));

                case ElementType.Arrow:
                    return BuildArrowGeometry(x, y, w, h);

                case ElementType.Path:
                    return BuildBezierPathGeometry(node);

                case ElementType.Text:
                    return new RectangleGeometry(new Rect(x, y, w, h));

                default:
                    return new RectangleGeometry(new Rect(x, y, w, h));
            }
        }

        private Geometry BuildRoundedRectGeometry(double x, double y, double w, double h,
            double rtl, double rtr, double rbr, double rbl)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(x + rtl, y), true, true);
                ctx.LineTo(new Point(x + w - rtr, y), true, false);
                if (rtr > 0) ctx.ArcTo(new Point(x + w, y + rtr), new Size(rtr, rtr), 0, false, SweepDirection.Clockwise, true, false);

                ctx.LineTo(new Point(x + w, y + h - rbr), true, false);
                if (rbr > 0) ctx.ArcTo(new Point(x + w - rbr, y + h), new Size(rbr, rbr), 0, false, SweepDirection.Clockwise, true, false);

                ctx.LineTo(new Point(x + rbl, y + h), true, false);
                if (rbl > 0) ctx.ArcTo(new Point(x, y + h - rbl), new Size(rbl, rbl), 0, false, SweepDirection.Clockwise, true, false);

                ctx.LineTo(new Point(x, y + rtl), true, false);
                if (rtl > 0) ctx.ArcTo(new Point(x + rtl, y), new Size(rtl, rtl), 0, false, SweepDirection.Clockwise, true, false);
            }
            geo.Freeze();
            return geo;
        }

        private Geometry BuildStarGeometry(double x, double y, double w, double h, int points, double innerRatio)
        {
            var geo = new StreamGeometry();
            double cx = x + w / 2.0;
            double cy = y + h / 2.0;
            double rx = w / 2.0;
            double ry = h / 2.0;
            double innerRx = rx * innerRatio;
            double innerRy = ry * innerRatio;
            double step = Math.PI / points;

            using (var ctx = geo.Open())
            {
                for (int i = 0; i < points * 2; i++)
                {
                    double angle = i * step - Math.PI / 2.0;
                    double rCurX = (i % 2 == 0) ? rx : innerRx;
                    double rCurY = (i % 2 == 0) ? ry : innerRy;
                    var pt = new Point(cx + rCurX * Math.Cos(angle), cy + rCurY * Math.Sin(angle));
                    if (i == 0)
                    {
                        ctx.BeginFigure(pt, true, true);
                    }
                    else
                    {
                        ctx.LineTo(pt, true, false);
                    }
                }
            }
            geo.Freeze();
            return geo;
        }

        private Geometry BuildPolygonGeometry(double x, double y, double w, double h, int sides)
        {
            var geo = new StreamGeometry();
            double cx = x + w / 2.0;
            double cy = y + h / 2.0;
            double rx = w / 2.0;
            double ry = h / 2.0;
            double step = (2.0 * Math.PI) / sides;

            using (var ctx = geo.Open())
            {
                for (int i = 0; i < sides; i++)
                {
                    double angle = i * step - Math.PI / 2.0;
                    var pt = new Point(cx + rx * Math.Cos(angle), cy + ry * Math.Sin(angle));
                    if (i == 0)
                    {
                        ctx.BeginFigure(pt, true, true);
                    }
                    else
                    {
                        ctx.LineTo(pt, true, false);
                    }
                }
            }
            geo.Freeze();
            return geo;
        }

        private Geometry BuildArrowGeometry(double x, double y, double w, double h)
        {
            var geo = new StreamGeometry();
            var start = new Point(x, y);
            var end = new Point(x + w, y + h);

            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            double arrowSize = Math.Min(18.0, Math.Max(8.0, Math.Sqrt(w * w + h * h) * 0.2));

            var p1 = new Point(end.X - arrowSize * Math.Cos(angle - Math.PI / 6.0),
                               end.Y - arrowSize * Math.Sin(angle - Math.PI / 6.0));
            var p2 = new Point(end.X - arrowSize * Math.Cos(angle + Math.PI / 6.0),
                               end.Y - arrowSize * Math.Sin(angle + Math.PI / 6.0));

            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.LineTo(end, true, false);
                ctx.BeginFigure(p1, true, true);
                ctx.LineTo(end, true, false);
                ctx.LineTo(p2, true, false);
            }
            geo.Freeze();
            return geo;
        }

        private Geometry BuildBezierPathGeometry(LayerNode node)
        {
            if (node.Geometry != null && node.Geometry.Nodes != null && node.Geometry.Nodes.Count > 0)
            {
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    var nodes = node.Geometry.Nodes;
                    ctx.BeginFigure(nodes[0].Anchor, node.Geometry.IsClosed, node.Geometry.IsClosed);

                    for (int i = 1; i < nodes.Count; i++)
                    {
                        var prev = nodes[i - 1];
                        var cur = nodes[i];

                        bool prevHasHandle = (prev.OutHandle.X != prev.Anchor.X || prev.OutHandle.Y != prev.Anchor.Y);
                        bool curHasHandle = (cur.InHandle.X != cur.Anchor.X || cur.InHandle.Y != cur.Anchor.Y);

                        if (prevHasHandle || curHasHandle)
                        {
                            ctx.BezierTo(prev.OutHandle, cur.InHandle, cur.Anchor, true, false);
                        }
                        else
                        {
                            ctx.LineTo(cur.Anchor, true, false);
                        }
                    }

                    if (node.Geometry.IsClosed && nodes.Count > 2)
                    {
                        var last = nodes[nodes.Count - 1];
                        var first = nodes[0];
                        bool lastHasHandle = (last.OutHandle.X != last.Anchor.X || last.OutHandle.Y != last.Anchor.Y);
                        bool firstHasHandle = (first.InHandle.X != first.Anchor.X || first.InHandle.Y != first.Anchor.Y);

                        if (lastHasHandle || firstHasHandle)
                        {
                            ctx.BezierTo(last.OutHandle, first.InHandle, first.Anchor, true, false);
                        }
                    }
                }
                geo.Freeze();
                return geo;
            }

            if (node.Geometry != null && !string.IsNullOrEmpty(node.Geometry.SvgPathData))
            {
                try
                {
                    var parsed = Geometry.Parse(node.Geometry.SvgPathData);
                    parsed.Freeze();
                    return parsed;
                }
                catch
                {
                    // Fall back to rectangle
                }
            }

            return new RectangleGeometry(new Rect(node.X, node.Y, node.Width, node.Height));
        }

        private void RenderVectorGeometry(LayerNode node, DrawingContext dc)
        {
            var geo = BuildGeometry(node);
            if (geo == null) return;

            // 1. Multiple Fills (drawn in order)
            if (node.Fills != null)
            {
                foreach (var fill in node.Fills)
                {
                    if (fill != null && fill.Enabled)
                    {
                        var fillBrush = CreateBrushFromPaint(fill, new Rect(node.X, node.Y, node.Width, node.Height));
                        if (fillBrush != null)
                        {
                            dc.DrawGeometry(fillBrush, null, geo);
                        }
                    }
                }
            }

            // 2. Multiple Strokes (drawn in order with alignment)
            if (node.Strokes != null)
            {
                foreach (var stroke in node.Strokes)
                {
                    if (stroke == null || !stroke.Enabled || stroke.Width <= 0.0 || stroke.Paint == null) continue;

                    var strokeBrush = CreateBrushFromPaint(stroke.Paint, new Rect(node.X, node.Y, node.Width, node.Height));
                    if (strokeBrush == null) continue;

                    var strokePen = new Pen(strokeBrush, stroke.Width)
                    {
                        StartLineCap = ConvertCap(stroke.Cap),
                        EndLineCap = ConvertCap(stroke.Cap),
                        LineJoin = ConvertJoin(stroke.Join)
                    };
                    if (stroke.DashPattern != null && stroke.DashPattern.Count > 0)
                    {
                        strokePen.DashStyle = new DashStyle(stroke.DashPattern, 0);
                    }
                    strokePen.Freeze();

                    if (stroke.Alignment == StrokeAlignment.Inside)
                    {
                        dc.PushClip(geo);
                        var insidePen = strokePen.Clone();
                        insidePen.Thickness = stroke.Width * 2.0;
                        insidePen.Freeze();
                        dc.DrawGeometry(null, insidePen, geo);
                        dc.Pop();
                    }
                    else if (stroke.Alignment == StrokeAlignment.Outside)
                    {
                        var outsidePen = strokePen.Clone();
                        outsidePen.Thickness = stroke.Width * 2.0;
                        outsidePen.Freeze();
                        dc.DrawGeometry(null, outsidePen, geo);
                    }
                    else
                    {
                        dc.DrawGeometry(null, strokePen, geo);
                    }
                }
            }
        }

        private void RenderText(LayerNode node, DrawingContext dc)
        {
            var typo = node.Typography ?? new TypographyData();
            string text = typo.Text ?? "";
            if (string.IsNullOrEmpty(text)) return;

            switch (typo.Case)
            {
                case TextCase.Upper:
                    text = text.ToUpperInvariant();
                    break;
                case TextCase.Lower:
                    text = text.ToLowerInvariant();
                    break;
                case TextCase.Title:
                    text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
                    break;
            }

            Brush textBrush = Brushes.White;
            if (node.Fills != null)
            {
                var activeFill = node.Fills.FirstOrDefault(f => f.Enabled);
                if (activeFill != null)
                {
                    textBrush = CreateBrushFromPaint(activeFill, new Rect(node.X, node.Y, node.Width, node.Height));
                }
            }

            var typeFace = new Typeface(
                new FontFamily(string.IsNullOrEmpty(typo.FontFamily) ? "Segoe UI, Arial" : typo.FontFamily),
                typo.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                FontWeight.FromOpenTypeWeight(Math.Max(100, Math.Min(900, typo.FontWeight))),
                FontStretches.Normal);

            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeFace,
                Math.Max(8.0, typo.FontSize),
                textBrush);

            // Text Decorations (Underline, Strikethrough)
            if (typo.Decoration == TextDecorationMode.Underline)
            {
                ft.SetTextDecorations(TextDecorations.Underline);
            }
            else if (typo.Decoration == TextDecorationMode.Strikethrough)
            {
                ft.SetTextDecorations(TextDecorations.Strikethrough);
            }

            // Line Height
            if (typo.LineHeight > 0.0)
            {
                ft.LineHeight = typo.FontSize * typo.LineHeight;
            }

            // Horizontal Alignment
            switch (typo.Align)
            {
                case TextAlign.Center:
                    ft.TextAlignment = TextAlignment.Center;
                    break;
                case TextAlign.Right:
                    ft.TextAlignment = TextAlignment.Right;
                    break;
                case TextAlign.Justify:
                    ft.TextAlignment = TextAlignment.Justify;
                    break;
                default:
                    ft.TextAlignment = TextAlignment.Left;
                    break;
            }

            // Dynamic Sizing & Overflow/Underflow (Figma Text Dynamics)
            switch (typo.AutoResize)
            {
                case TextAutoResize.AutoWidth:
                    node.Width = Math.Max(12.0, ft.WidthIncludingTrailingWhitespace);
                    node.Height = Math.Max(12.0, ft.Height);
                    break;

                case TextAutoResize.AutoHeight:
                    ft.MaxTextWidth = Math.Max(1.0, node.Width);
                    node.Height = Math.Max(12.0, ft.Height);
                    break;

                case TextAutoResize.Fixed:
                default:
                    ft.MaxTextWidth = Math.Max(1.0, node.Width);
                    ft.MaxTextHeight = Math.Max(1.0, node.Height);
                    break;
            }

            // Vertical Alignment calculation
            double yOffset = 0.0;
            if (node.Height > ft.Height)
            {
                if (typo.VerticalAlign == VerticalAlign.Middle)
                {
                    yOffset = (node.Height - ft.Height) / 2.0;
                }
                else if (typo.VerticalAlign == VerticalAlign.Bottom)
                {
                    yOffset = node.Height - ft.Height;
                }
            }

            var origin = new Point(node.X, node.Y + yOffset);

            // Vector glyph rendering for multiple fills or strokes
            bool hasMultipleFills = (node.Fills != null && node.Fills.Count(f => f.Enabled) > 1);
            bool hasStrokes = (node.Strokes != null && node.Strokes.Any(s => s.Enabled && s.Width > 0.0));

            if (hasMultipleFills || hasStrokes)
            {
                var glyphGeo = ft.BuildGeometry(origin);
                if (glyphGeo != null)
                {
                    if (node.Fills != null)
                    {
                        foreach (var fill in node.Fills)
                        {
                            if (fill != null && fill.Enabled)
                            {
                                var fillBrush = CreateBrushFromPaint(fill, new Rect(node.X, node.Y, node.Width, node.Height));
                                dc.DrawGeometry(fillBrush, null, glyphGeo);
                            }
                        }
                    }

                    if (node.Strokes != null)
                    {
                        foreach (var stroke in node.Strokes)
                        {
                            if (stroke != null && stroke.Enabled && stroke.Width > 0.0 && stroke.Paint != null)
                            {
                                var sBrush = CreateBrushFromPaint(stroke.Paint, new Rect(node.X, node.Y, node.Width, node.Height));
                                var pen = new Pen(sBrush, stroke.Width)
                                {
                                    StartLineCap = ConvertCap(stroke.Cap),
                                    EndLineCap = ConvertCap(stroke.Cap),
                                    LineJoin = ConvertJoin(stroke.Join)
                                };
                                pen.Freeze();
                                dc.DrawGeometry(null, pen, glyphGeo);
                            }
                        }
                    }
                    return;
                }
            }

            // Direct crisp subpixel text rendering
            dc.DrawText(ft, origin);
        }

        private void RenderImage(LayerNode node, DrawingContext dc)
        {
            var bounds = new Rect(node.X, node.Y, Math.Max(1, node.Width), Math.Max(1, node.Height));
            var paint = node.Fills != null ? node.Fills.FirstOrDefault(f => f.Type == PaintType.Image && f.Enabled) : null;

            if (paint != null && !string.IsNullOrEmpty(paint.ImageRef))
            {
                BitmapSource bmp;
                if (!_imageCache.TryGetValue(paint.ImageRef, out bmp))
                {
                    if (File.Exists(paint.ImageRef))
                    {
                        try
                        {
                            bmp = new BitmapImage(new Uri(paint.ImageRef, UriKind.Absolute));
                            _imageCache[paint.ImageRef] = bmp;
                        }
                        catch
                        {
                            bmp = null;
                        }
                    }
                }

                if (bmp != null)
                {
                    if (node.CornerRadius > 0)
                    {
                        dc.PushClip(new RectangleGeometry(bounds, node.CornerRadius, node.CornerRadius));
                        dc.DrawImage(bmp, bounds);
                        dc.Pop();
                    }
                    else
                    {
                        dc.DrawImage(bmp, bounds);
                    }
                    return;
                }
            }

            // Fallback placeholder rectangle
            var placeholder = new SolidColorBrush(Color.FromRgb(45, 52, 64));
            placeholder.Freeze();
            dc.DrawRectangle(placeholder, null, bounds);
        }

        public Brush CreateBrushFromPaint(Paint paint, Rect bounds)
        {
            if (paint == null) return Brushes.Transparent;

            switch (paint.Type)
            {
                case PaintType.Solid:
                    var solid = new SolidColorBrush(ParseHexColor(paint.ColorHex, paint.Opacity));
                    solid.Freeze();
                    return solid;

                case PaintType.LinearGradient:
                    return CreateLinearGradientBrush(paint);

                case PaintType.RadialGradient:
                    return CreateRadialGradientBrush(paint);

                default:
                    var fallback = new SolidColorBrush(ParseHexColor(paint.ColorHex, paint.Opacity));
                    fallback.Freeze();
                    return fallback;
            }
        }

        private LinearGradientBrush CreateLinearGradientBrush(Paint paint)
        {
            double rad = (paint.GradientAngle * Math.PI) / 180.0;
            // Angle to start/end points on unit square
            double x1 = 0.5 - 0.5 * Math.Cos(rad);
            double y1 = 0.5 - 0.5 * Math.Sin(rad);
            double x2 = 0.5 + 0.5 * Math.Cos(rad);
            double y2 = 0.5 + 0.5 * Math.Sin(rad);

            var lgb = new LinearGradientBrush
            {
                StartPoint = new Point(x1, y1),
                EndPoint = new Point(x2, y2),
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };

            if (paint.GradientStops != null && paint.GradientStops.Count > 0)
            {
                foreach (var stop in paint.GradientStops)
                {
                    lgb.GradientStops.Add(new GradientStop(
                        ParseHexColor(stop.ColorHex, stop.Opacity * paint.Opacity),
                        stop.Position));
                }
            }
            else
            {
                lgb.GradientStops.Add(new GradientStop(Color.FromRgb(13, 153, 255), 0.0));
                lgb.GradientStops.Add(new GradientStop(Color.FromRgb(162, 89, 255), 1.0));
            }

            lgb.Freeze();
            return lgb;
        }

        private RadialGradientBrush CreateRadialGradientBrush(Paint paint)
        {
            var rgb = new RadialGradientBrush
            {
                Center = paint.GradientCenter,
                GradientOrigin = paint.GradientCenter,
                RadiusX = paint.GradientRadius,
                RadiusY = paint.GradientRadius,
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };

            if (paint.GradientStops != null && paint.GradientStops.Count > 0)
            {
                foreach (var stop in paint.GradientStops)
                {
                    rgb.GradientStops.Add(new GradientStop(
                        ParseHexColor(stop.ColorHex, stop.Opacity * paint.Opacity),
                        stop.Position));
                }
            }
            else
            {
                rgb.GradientStops.Add(new GradientStop(Color.FromRgb(13, 153, 255), 0.0));
                rgb.GradientStops.Add(new GradientStop(Color.FromRgb(162, 89, 255), 1.0));
            }

            rgb.Freeze();
            return rgb;
        }

        private PenLineCap ConvertCap(StrokeCap cap)
        {
            switch (cap)
            {
                case StrokeCap.Round: return PenLineCap.Round;
                case StrokeCap.Square: return PenLineCap.Square;
                default: return PenLineCap.Flat;
            }
        }

        private PenLineJoin ConvertJoin(StrokeJoin join)
        {
            switch (join)
            {
                case StrokeJoin.Round: return PenLineJoin.Round;
                case StrokeJoin.Bevel: return PenLineJoin.Bevel;
                default: return PenLineJoin.Miter;
            }
        }

        public static Color ParseHexColor(string hex, double opacity = 1.0)
        {
            if (string.IsNullOrEmpty(hex)) return Color.FromArgb((byte)(opacity * 255), 0, 0, 0);

            hex = hex.Trim().TrimStart('#');
            byte a = (byte)(Math.Max(0.0, Math.Min(1.0, opacity)) * 255);
            byte r = 0, g = 0, b = 0;

            try
            {
                if (hex.Length == 3)
                {
                    r = Convert.ToByte(new string(hex[0], 2), 16);
                    g = Convert.ToByte(new string(hex[1], 2), 16);
                    b = Convert.ToByte(new string(hex[2], 2), 16);
                }
                else if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }
                else if (hex.Length == 8)
                {
                    byte hexA = Convert.ToByte(hex.Substring(0, 2), 16);
                    a = (byte)((hexA / 255.0) * opacity * 255);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
            }
            catch
            {
                r = 13; g = 153; b = 255;
            }

            return Color.FromArgb(a, r, g, b);
        }
    }
}
