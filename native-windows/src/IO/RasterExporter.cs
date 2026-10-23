using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kerf.Engine;
using Kerf.Model;

namespace Kerf.IO
{
    public enum RasterFormat
    {
        Png,
        Jpeg
    }

    public static class RasterExporter
    {
        public static void ExportToFile(
            Page page,
            LayerNode singleNode,
            string outputPath,
            RasterFormat format,
            double scale = 2.0)
        {
            if (page == null) throw new ArgumentNullException("page");
            if (string.IsNullOrEmpty(outputPath)) throw new ArgumentNullException("outputPath");

            Rect exportBounds;
            if (singleNode != null)
            {
                exportBounds = singleNode.GetBounds();
            }
            else
            {
                if (page.Layers == null || page.Layers.Count == 0)
                {
                    exportBounds = new Rect(0, 0, 800, 600);
                }
                else
                {
                    double minX = page.Layers.Min(l => l.X);
                    double minY = page.Layers.Min(l => l.Y);
                    double maxX = page.Layers.Max(l => l.X + Math.Max(1, l.Width));
                    double maxY = page.Layers.Max(l => l.Y + Math.Max(1, l.Height));
                    exportBounds = new Rect(minX - 20, minY - 20, (maxX - minX) + 40, (maxY - minY) + 40);
                }
            }

            int pixelWidth = (int)Math.Ceiling(exportBounds.Width * scale);
            int pixelHeight = (int)Math.Ceiling(exportBounds.Height * scale);

            if (pixelWidth < 1) pixelWidth = 1;
            if (pixelHeight < 1) pixelHeight = 1;

            var rtb = new RenderTargetBitmap(
                pixelWidth,
                pixelHeight,
                96.0 * scale,
                96.0 * scale,
                PixelFormats.Pbgra32);

            var visual = new DrawingVisual();
            var renderer = new GpuRenderer();

            using (var dc = visual.RenderOpen())
            {
                // Background
                if (singleNode == null && !string.IsNullOrEmpty(page.BackgroundColor))
                {
                    var bgBrush = new SolidColorBrush(GpuRenderer.ParseHexColor(page.BackgroundColor, 1.0));
                    bgBrush.Freeze();
                    dc.DrawRectangle(bgBrush, null, new Rect(0, 0, exportBounds.Width, exportBounds.Height));
                }

                // Translate canvas to export origin
                dc.PushTransform(new TranslateTransform(-exportBounds.X, -exportBounds.Y));

                if (singleNode != null)
                {
                    var nodeVisual = new DrawingVisual();
                    renderer.RenderNodeToVisual(singleNode, nodeVisual, 1.0);
                    // Draw visual
                    dc.DrawRectangle(new VisualBrush(nodeVisual), null, singleNode.GetBounds());
                }
                else if (page.Layers != null)
                {
                    foreach (var layer in page.Layers)
                    {
                        var layerVisual = new DrawingVisual();
                        renderer.RenderNodeToVisual(layer, layerVisual, 1.0);
                        dc.DrawRectangle(new VisualBrush(layerVisual), null, layer.GetBounds());
                    }
                }

                dc.Pop(); // pop translation
            }

            rtb.Render(visual);

            string dir = System.IO.Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            BitmapEncoder encoder;
            if (format == RasterFormat.Jpeg)
            {
                var jpg = new JpegBitmapEncoder();
                jpg.QualityLevel = 95;
                encoder = jpg;
            }
            else
            {
                encoder = new PngBitmapEncoder();
            }

            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(fs);
            }
        }
    }
}
