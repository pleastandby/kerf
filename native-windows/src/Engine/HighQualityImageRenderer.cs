using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kerf.IO;
using Kerf.Model;
using Kerf.Tools;

namespace Kerf.Engine
{
    /// <summary>
    /// High-Quality, Production-Grade Image Rendering Engine for Kerf Studio.
    /// Processes complete layer hierarchies, Auto Layout, text metrics, multiple fills/strokes,
    /// and renders Photoshop-grade 32-bit ARGB PNGs and Studio JPEGs with subpixel antialiasing.
    /// </summary>
    public static class HighQualityImageRenderer
    {
        private static readonly Dictionary<string, BitmapSource> _imageCache = new Dictionary<string, BitmapSource>();

        #region Public Export API

        public static void ExportToFile(
            Page page,
            LayerNode singleNode,
            string outputPath,
            RasterFormat format,
            double scale = 2.0)
        {
            var targets = (singleNode != null) ? new List<LayerNode> { singleNode } : null;
            ExportToFile(page, targets, outputPath, format, scale);
        }

        public static void ExportToFile(
            Page page,
            IEnumerable<LayerNode> targets,
            string outputPath,
            RasterFormat format,
            double scale = 2.0)
        {
            if (page == null) throw new ArgumentNullException("page");
            if (string.IsNullOrEmpty(outputPath)) throw new ArgumentNullException("outputPath");

            bool isJpeg = (format == RasterFormat.Jpeg);
            var bmp = Render(page, targets, scale, isJpeg);

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            BitmapEncoder encoder;
            if (isJpeg)
            {
                var jpg = new JpegBitmapEncoder();
                jpg.QualityLevel = 100; // Studio maximum quality (Photoshop 12)
                encoder = jpg;
            }
            else
            {
                encoder = new PngBitmapEncoder();
            }

            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(fs);
            }
        }

        public static BitmapSource Render(
            Page page,
            LayerNode singleNode,
            double scale = 2.0,
            bool isJpeg = false)
        {
            var targets = (singleNode != null) ? new List<LayerNode> { singleNode } : null;
            return Render(page, targets, scale, isJpeg);
        }

        public static BitmapSource Render(
            Page page,
            IEnumerable<LayerNode> targets,
            double scale = 2.0,
            bool isJpeg = false)
        {
            if (page == null) throw new ArgumentNullException("page");

            // 1. Pre-process document: solve Auto Layout and dynamic text measurements
            PreProcessForExport(page, targets);

            // 2. Compute accurate visual bounds enclosing all target shapes & descendants
            Rect exportBounds = ComputeVisualBounds(page, targets);

            if (exportBounds.Width < 1.0) exportBounds.Width = 1.0;
            if (exportBounds.Height < 1.0) exportBounds.Height = 1.0;

            scale = Math.Max(0.5, Math.Min(8.0, scale));
            int pixelWidth = (int)Math.Ceiling(exportBounds.Width * scale);
            int pixelHeight = (int)Math.Ceiling(exportBounds.Height * scale);

            // Safety clamp: max 16384 x 16384 px
            pixelWidth = Math.Max(1, Math.Min(16384, pixelWidth));
            pixelHeight = Math.Max(1, Math.Min(16384, pixelHeight));

            var rtb = new RenderTargetBitmap(
                pixelWidth,
                pixelHeight,
                96.0 * scale,
                96.0 * scale,
                PixelFormats.Pbgra32);

            var visual = new DrawingVisual();

            // Photoshop-grade rendering qualities
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            RenderOptions.SetClearTypeHint(visual, ClearTypeHint.Enabled);
            RenderOptions.SetEdgeMode(visual, EdgeMode.Unspecified);
            TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(visual, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(visual, TextHintingMode.Auto);

            var gpu = new GpuRenderer();

            using (var dc = visual.RenderOpen())
            {
                // Background handling
                bool isFullPage = (targets == null || !targets.Any());
                if (isFullPage)
                {
                    if (!string.IsNullOrEmpty(page.BackgroundColor))
                    {
                        var bgBrush = new SolidColorBrush(GpuRenderer.ParseHexColor(page.BackgroundColor, 1.0));
                        bgBrush.Freeze();
                        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, exportBounds.Width, exportBounds.Height));
                    }
                    else if (isJpeg)
                    {
                        // JPEG cannot have transparent pixels; composite against pure white
                        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, exportBounds.Width, exportBounds.Height));
                    }
                }
                else if (isJpeg)
                {
                    // If exporting a transparent selection as JPEG, composite against page background or white
                    string bgHex = !string.IsNullOrEmpty(page.BackgroundColor) ? page.BackgroundColor : "#FFFFFF";
                    var bgBrush = new SolidColorBrush(GpuRenderer.ParseHexColor(bgHex, 1.0));
                    bgBrush.Freeze();
                    dc.DrawRectangle(bgBrush, null, new Rect(0, 0, exportBounds.Width, exportBounds.Height));
                }

                // Translate canvas space so exportBounds.TopLeft maps to (0, 0)
                dc.PushTransform(new TranslateTransform(-exportBounds.X, -exportBounds.Y));

                if (targets != null && targets.Any())
                {
                    foreach (var target in targets)
                    {
                        RenderNodeHierarchy(target, dc, scale, gpu);
                    }
                }
                else if (page.Layers != null)
                {
                    foreach (var root in page.Layers)
                    {
                        RenderNodeHierarchy(root, dc, scale, gpu);
                    }
                }

                dc.Pop(); // pop translation
            }

            rtb.Render(visual);
            return rtb;
        }

        #endregion

        #region Pre-Processing & Bounds Calculation

        public static void PreProcessForExport(Page page, IEnumerable<LayerNode> targets)
        {
            if (page == null) return;

            // 1. Solve all Auto Layout frames recursively
            AutoLayoutEngine.ReflowAll(page);

            // 2. Measure and synchronize all text layers
            var allLayers = page.GetAllLayersFlat();
            foreach (var node in allLayers)
            {
                if (node.Type == ElementType.Text)
                {
                    SynchronizeTextBounds(node);
                }
            }
        }

        private static void SynchronizeTextBounds(LayerNode node)
        {
            if (node == null || node.Type != ElementType.Text) return;
            var typo = node.Typography ?? new TypographyData();
            string text = typo.Text ?? node.Name ?? "";
            if (string.IsNullOrEmpty(text)) return;

            switch (typo.Case)
            {
                case TextCase.Upper: text = text.ToUpperInvariant(); break;
                case TextCase.Lower: text = text.ToLowerInvariant(); break;
                case TextCase.Title: text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text); break;
            }

            var typeFace = new Typeface(
                new FontFamily(string.IsNullOrEmpty(typo.FontFamily) ? "Segoe UI, Arial" : typo.FontFamily),
                typo.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                FontWeight.FromOpenTypeWeight(Math.Max(100, Math.Min(900, typo.FontWeight > 0 ? typo.FontWeight : 400))),
                FontStretches.Normal);

            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeFace,
                Math.Max(6.0, typo.FontSize > 0.0 ? typo.FontSize : 14.0),
                Brushes.White);

            if (typo.LineHeight > 0.0)
            {
                ft.LineHeight = typo.FontSize * typo.LineHeight;
            }

            if (typo.AutoResize == TextAutoResize.AutoWidth)
            {
                node.Width = Math.Max(12.0, ft.WidthIncludingTrailingWhitespace);
                node.Height = Math.Max(12.0, ft.Height);
            }
            else if (typo.AutoResize == TextAutoResize.AutoHeight)
            {
                ft.MaxTextWidth = Math.Max(1.0, node.Width);
                node.Height = Math.Max(12.0, ft.Height);
            }
        }

        public static Rect ComputeVisualBounds(Page page, IEnumerable<LayerNode> targets)
        {
            if (targets != null && targets.Any())
            {
                var list = targets.ToList();
                // If single Frame with ClipContent, use the Frame's exact artboard bounds
                if (list.Count == 1 && list[0].Type == ElementType.Frame && list[0].ClipContent)
                {
                    return new Rect(list[0].X, list[0].Y, Math.Max(1, list[0].Width), Math.Max(1, list[0].Height));
                }

                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;

                foreach (var target in list)
                {
                    ExpandBoundsRecursive(target, ref minX, ref minY, ref maxX, ref maxY);
                }

                if (minX <= maxX && minY <= maxY)
                {
                    return new Rect(minX, minY, (maxX - minX), (maxY - minY));
                }
            }

            // Exporting full page
            if (page.Layers != null && page.Layers.Count > 0)
            {
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;

                foreach (var root in page.Layers)
                {
                    ExpandBoundsRecursive(root, ref minX, ref minY, ref maxX, ref maxY);
                }

                if (minX <= maxX && minY <= maxY)
                {
                    // Add clean margin for full canvas
                    double pad = 24.0;
                    return new Rect(minX - pad, minY - pad, (maxX - minX) + pad * 2, (maxY - minY) + pad * 2);
                }
            }

            return new Rect(0, 0, 1200, 800);
        }

        private static void ExpandBoundsRecursive(LayerNode node, ref double minX, ref double minY, ref double maxX, ref double maxY)
        {
            if (node == null || !node.IsVisible) return;

            double x = node.X;
            double y = node.Y;
            double w = Math.Max(1, node.Width);
            double h = Math.Max(1, node.Height);

            // Account for drop shadow padding
            double shadowPadLeft = 0, shadowPadTop = 0, shadowPadRight = 0, shadowPadBottom = 0;
            if (node.Effects != null)
            {
                var drop = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.DropShadow);
                if (drop != null)
                {
                    double blur = drop.Blur * 1.5;
                    shadowPadLeft = Math.Max(0, -drop.OffsetX + blur);
                    shadowPadTop = Math.Max(0, -drop.OffsetY + blur);
                    shadowPadRight = Math.Max(0, drop.OffsetX + blur);
                    shadowPadBottom = Math.Max(0, drop.OffsetY + blur);
                }
            }

            // Account for stroke thickness
            double strokePad = 0;
            if (node.Strokes != null)
            {
                var maxStroke = node.Strokes.Where(s => s.Enabled).Select(s => s.Width).DefaultIfEmpty(0).Max();
                strokePad = maxStroke / 2.0;
            }

            minX = Math.Min(minX, x - shadowPadLeft - strokePad);
            minY = Math.Min(minY, y - shadowPadTop - strokePad);
            maxX = Math.Max(maxX, x + w + shadowPadRight + strokePad);
            maxY = Math.Max(maxY, y + h + shadowPadBottom + strokePad);

            // If frame clips content, children bounds do not escape frame bounds
            if (node.Type == ElementType.Frame && node.ClipContent)
            {
                return;
            }

            if (node.Children != null && node.Children.Count > 0)
            {
                foreach (var child in node.Children)
                {
                    ExpandBoundsRecursive(child, ref minX, ref minY, ref maxX, ref maxY);
                }
            }
        }

        #endregion

        #region Hierarchical Node Rendering

        private static void RenderNodeHierarchy(LayerNode node, DrawingContext dc, double scale, GpuRenderer gpu)
        {
            if (node == null || !node.IsVisible || node.Opacity <= 0.0) return;

            // 1. Push Opacity
            bool pushedOpacity = false;
            if (node.Opacity < 0.999)
            {
                dc.PushOpacity(node.Opacity);
                pushedOpacity = true;
            }

            // 2. Push Transform (Rotation / Matrix)
            bool pushedTransform = false;
            var matrix = node.GetTransformMatrix();
            if (!matrix.IsIdentity)
            {
                dc.PushTransform(new MatrixTransform(matrix));
                pushedTransform = true;
            }

            // 3. Push Clip if Frame has ClipContent
            bool pushedClip = false;
            if (node.Type == ElementType.Frame && node.ClipContent)
            {
                var clipGeo = gpu.BuildGeometry(node);
                if (clipGeo != null)
                {
                    dc.PushClip(clipGeo);
                    pushedClip = true;
                }
            }

            // 4. Render Drop Shadow
            RenderDropShadow(node, dc, gpu);

            // 5. Render Node's Own Visual Content
            RenderLayerContent(node, dc, scale, gpu);

            // 6. Render Inner Shadow
            RenderInnerShadow(node, dc, gpu);

            // 7. Render Liquid Glass
            RenderLiquidGlass(node, dc, gpu);

            // 8. Render Grain
            RenderGrain(node, dc, gpu);

            // 9. RECURSIVELY RENDER ALL CHILDREN (In Back-to-Front Painter's Order)
            if (node.Children != null && node.Children.Count > 0)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    RenderNodeHierarchy(node.Children[i], dc, scale, gpu);
                }
            }

            // Pop pushed states in reverse order
            if (pushedClip) dc.Pop();
            if (pushedTransform) dc.Pop();
            if (pushedOpacity) dc.Pop();
        }

        private static void RenderLayerContent(LayerNode node, DrawingContext dc, double scale, GpuRenderer gpu)
        {
            switch (node.Type)
            {
                case ElementType.Text:
                    RenderTextLayer(node, dc, gpu);
                    break;

                case ElementType.Image:
                    RenderImageLayer(node, dc);
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
                case ElementType.ComponentInstance:
                default:
                    RenderVectorShape(node, dc, gpu);
                    break;
            }
        }

        #endregion

        #region Vector Shape, Text & Image Detail Renderers

        private static void RenderTextLayer(LayerNode node, DrawingContext dc, GpuRenderer gpu)
        {
            var typo = node.Typography ?? new TypographyData();
            string text = typo.Text ?? node.Name ?? "";
            if (string.IsNullOrEmpty(text)) return;

            switch (typo.Case)
            {
                case TextCase.Upper: text = text.ToUpperInvariant(); break;
                case TextCase.Lower: text = text.ToLowerInvariant(); break;
                case TextCase.Title: text = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text); break;
            }

            Brush textBrush = Brushes.White;
            if (node.Fills != null && node.Fills.Count > 0)
            {
                var activeFill = node.Fills.FirstOrDefault(f => f.Enabled);
                if (activeFill != null)
                {
                    textBrush = gpu.CreateBrushFromPaint(activeFill, new Rect(node.X, node.Y, Math.Max(1, node.Width), Math.Max(1, node.Height)));
                }
            }

            var typeFace = new Typeface(
                new FontFamily(string.IsNullOrEmpty(typo.FontFamily) ? "Segoe UI, Inter, Arial" : typo.FontFamily),
                typo.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                FontWeight.FromOpenTypeWeight(Math.Max(100, Math.Min(900, typo.FontWeight > 0 ? typo.FontWeight : 400))),
                FontStretches.Normal);

            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeFace,
                Math.Max(6.0, typo.FontSize > 0.0 ? typo.FontSize : 14.0),
                textBrush);

            if (typo.Decoration == TextDecorationMode.Underline)
                ft.SetTextDecorations(TextDecorations.Underline);
            else if (typo.Decoration == TextDecorationMode.Strikethrough)
                ft.SetTextDecorations(TextDecorations.Strikethrough);

            if (typo.LineHeight > 0.0)
                ft.LineHeight = typo.FontSize * typo.LineHeight;

            switch (typo.Align)
            {
                case TextAlign.Center: ft.TextAlignment = TextAlignment.Center; break;
                case TextAlign.Right: ft.TextAlignment = TextAlignment.Right; break;
                case TextAlign.Justify: ft.TextAlignment = TextAlignment.Justify; break;
                default: ft.TextAlignment = TextAlignment.Left; break;
            }

            switch (typo.AutoResize)
            {
                case TextAutoResize.AutoWidth:
                    break;
                case TextAutoResize.AutoHeight:
                    ft.MaxTextWidth = Math.Max(1.0, node.Width);
                    break;
                case TextAutoResize.Fixed:
                default:
                    ft.MaxTextWidth = Math.Max(1.0, node.Width);
                    ft.MaxTextHeight = Math.Max(1.0, node.Height);
                    break;
            }

            double yOffset = 0.0;
            if (node.Height > ft.Height)
            {
                if (typo.VerticalAlign == VerticalAlign.Middle)
                    yOffset = (node.Height - ft.Height) / 2.0;
                else if (typo.VerticalAlign == VerticalAlign.Bottom)
                    yOffset = node.Height - ft.Height;
            }

            var origin = new Point(node.X, node.Y + yOffset);

            // Support multiple fills & strokes on text glyphs
            bool hasMultipleFills = (node.Fills != null && node.Fills.Count(f => f.Enabled) > 1);
            bool hasStrokes = (node.Strokes != null && node.Strokes.Any(s => s.Enabled && s.Width > 0.0));

            if (hasMultipleFills || hasStrokes)
            {
                var textGeo = ft.BuildGeometry(origin);
                if (textGeo != null)
                {
                    if (node.Fills != null)
                    {
                        foreach (var fill in node.Fills)
                        {
                            if (fill != null && fill.Enabled)
                            {
                                var b = gpu.CreateBrushFromPaint(fill, new Rect(node.X, node.Y, node.Width, node.Height));
                                if (b != null) dc.DrawGeometry(b, null, textGeo);
                            }
                        }
                    }
                    if (node.Strokes != null)
                    {
                        foreach (var st in node.Strokes)
                        {
                            if (st != null && st.Enabled && st.Width > 0.0 && st.Paint != null)
                            {
                                var sb = gpu.CreateBrushFromPaint(st.Paint, new Rect(node.X, node.Y, node.Width, node.Height));
                                if (sb != null)
                                {
                                    var pen = new Pen(sb, st.Width);
                                    pen.Freeze();
                                    dc.DrawGeometry(null, pen, textGeo);
                                }
                            }
                        }
                    }
                    return;
                }
            }

            dc.DrawText(ft, origin);
        }

        private static void RenderVectorShape(LayerNode node, DrawingContext dc, GpuRenderer gpu)
        {
            var geo = gpu.BuildGeometry(node);
            if (geo == null) return;

            // 1. Multiple Fills (drawn in order)
            if (node.Fills != null)
            {
                foreach (var fill in node.Fills)
                {
                    if (fill != null && fill.Enabled)
                    {
                        var fillBrush = gpu.CreateBrushFromPaint(fill, new Rect(node.X, node.Y, node.Width, node.Height));
                        if (fillBrush != null)
                        {
                            dc.DrawGeometry(fillBrush, null, geo);
                        }
                    }
                }
            }

            // 2. Multiple Strokes (drawn in order with inside/outside/center alignment)
            if (node.Strokes != null)
            {
                foreach (var stroke in node.Strokes)
                {
                    if (stroke == null || !stroke.Enabled || stroke.Width <= 0.0 || stroke.Paint == null) continue;

                    var strokeBrush = gpu.CreateBrushFromPaint(stroke.Paint, new Rect(node.X, node.Y, node.Width, node.Height));
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

        private static void RenderImageLayer(LayerNode node, DrawingContext dc)
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
                        catch { bmp = null; }
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
                }
            }
        }

        private static void RenderDropShadow(LayerNode node, DrawingContext dc, GpuRenderer gpu)
        {
            if (node.Effects == null || node.Effects.Count == 0) return;
            var dropShadow = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.DropShadow);
            if (dropShadow == null || dropShadow.Opacity <= 0.0) return;

            var geo = gpu.BuildGeometry(node);
            if (geo == null) return;

            Color baseColor = GpuRenderer.ParseHexColor(dropShadow.ColorHex, dropShadow.Opacity);
            double blur = Math.Max(1.0, dropShadow.Blur);
            int passes = Math.Min(8, Math.Max(3, (int)(blur / 2.0)));

            for (int i = passes; i >= 1; i--)
            {
                double t = (double)i / passes;
                double spread = t * blur;
                double weight = Math.Exp(-2.0 * t * t);
                double alpha = (dropShadow.Opacity * 0.45 * weight) / passes;

                var shadowBrush = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Max(1, Math.Min(255, (int)(alpha * 255))),
                    baseColor.R, baseColor.G, baseColor.B));
                shadowBrush.Freeze();

                var trans = new TranslateTransform(dropShadow.OffsetX, dropShadow.OffsetY);
                dc.PushTransform(trans);
                var shadowPen = new Pen(shadowBrush, spread * 1.8);
                shadowPen.Freeze();
                dc.DrawGeometry(shadowBrush, shadowPen, geo);
                dc.Pop();
            }
        }

        private static void RenderInnerShadow(LayerNode node, DrawingContext dc, GpuRenderer gpu)
        {
            if (node.Effects == null || node.Effects.Count == 0) return;
            var innerShadow = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.InnerShadow);
            if (innerShadow == null || innerShadow.Opacity <= 0.0) return;

            var geo = gpu.BuildGeometry(node);
            if (geo == null) return;

            dc.PushClip(geo);

            Color color = GpuRenderer.ParseHexColor(innerShadow.ColorHex, innerShadow.Opacity);
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

        private static BitmapSource _exportNoiseTexture;
        private static BitmapSource GetNoiseTexture()
        {
            if (_exportNoiseTexture != null) return _exportNoiseTexture;
            int size = 64;
            byte[] pixels = new byte[size * size * 4];
            var rng = new Random(1337);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte val = (byte)rng.Next(20, 240);
                pixels[i] = val;     // B
                pixels[i + 1] = val; // G
                pixels[i + 2] = val; // R
                pixels[i + 3] = (byte)rng.Next(180, 255); // A
            }
            var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
            bmp.Freeze();
            _exportNoiseTexture = bmp;
            return _exportNoiseTexture;
        }

        private static void RenderLiquidGlass(LayerNode node, DrawingContext dc, GpuRenderer gpu)
        {
            if (node.Effects == null || node.Effects.Count == 0) return;
            var glass = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.LiquidGlass);
            if (glass == null || glass.Intensity <= 0.0) return;

            var geo = gpu.BuildGeometry(node);
            if (geo == null) return;

            var bounds = new Rect(node.X, node.Y, Math.Max(1, node.Width), Math.Max(1, node.Height));
            GpuRenderer.RenderAppleLiquidGlass(node, dc, geo, bounds, glass);
        }

        private static void RenderGrain(LayerNode node, DrawingContext dc, GpuRenderer gpu)
        {
            if (node.Effects == null || node.Effects.Count == 0) return;
            var grain = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.Grain);
            if (grain == null || grain.Intensity <= 0.0) return;

            var geo = gpu.BuildGeometry(node);
            if (geo == null) return;

            var bounds = new Rect(node.X, node.Y, Math.Max(1, node.Width), Math.Max(1, node.Height));
            double scale = Math.Max(0.2, Math.Min(5.0, grain.Scale > 0.0 ? grain.Scale : 1.0));
            double tilePx = 64.0 * scale;

            var brush = new ImageBrush(GetNoiseTexture())
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, tilePx, tilePx),
                ViewportUnits = BrushMappingMode.Absolute,
                Opacity = Math.Max(0.01, Math.Min(1.0, grain.Intensity))
            };
            brush.Freeze();

            dc.PushClip(geo);
            dc.DrawRectangle(brush, null, bounds);
            dc.Pop();
        }

        private static PenLineCap ConvertCap(StrokeCap cap)
        {
            switch (cap)
            {
                case StrokeCap.Round: return PenLineCap.Round;
                case StrokeCap.Square: return PenLineCap.Square;
                default: return PenLineCap.Flat;
            }
        }

        private static PenLineJoin ConvertJoin(StrokeJoin join)
        {
            switch (join)
            {
                case StrokeJoin.Round: return PenLineJoin.Round;
                case StrokeJoin.Bevel: return PenLineJoin.Bevel;
                default: return PenLineJoin.Miter;
            }
        }

        #endregion
    }
}
