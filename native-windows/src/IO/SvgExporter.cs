using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Windows.Media;
using Kerf.Engine;
using Kerf.Model;
using Kerf.Tools;

namespace Kerf.IO
{
    public static class SvgExporter
    {
        public static string ExportPage(Page page)
        {
            if (page == null) return "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";

            // Compute bounding box containing all layers
            double minX = 0, minY = 0, maxX = 1200, maxY = 800;
            if (page.Layers != null && page.Layers.Count > 0)
            {
                minX = page.Layers.Min(l => l.X);
                minY = page.Layers.Min(l => l.Y);
                maxX = page.Layers.Max(l => l.X + Math.Max(1, l.Width));
                maxY = page.Layers.Max(l => l.Y + Math.Max(1, l.Height));

                // Add small padding
                minX = Math.Max(0, minX - 40);
                minY = Math.Max(0, minY - 40);
                maxX += 40;
                maxY += 40;
            }

            double width = maxX - minX;
            double height = maxY - minY;

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                "viewBox=\"{0:F2} {1:F2} {2:F2} {3:F2}\" width=\"{2:F2}\" height=\"{3:F2}\">\n",
                minX, minY, width, height);

            // Defs for gradients and filters
            sb.AppendLine("  <defs>");
            var defIds = new HashSet<string>();
            if (page.Layers != null)
            {
                foreach (var layer in page.Layers)
                {
                    WriteLayerDefs(sb, layer, defIds);
                }
            }
            sb.AppendLine("  </defs>");

            // Canvas background
            if (!string.IsNullOrEmpty(page.BackgroundColor))
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "  <rect x=\"{0:F2}\" y=\"{1:F2}\" width=\"{2:F2}\" height=\"{3:F2}\" fill=\"{4}\" />\n",
                    minX, minY, width, height, SecurityElement.Escape(page.BackgroundColor));
            }

            // Layer elements
            if (page.Layers != null)
            {
                foreach (var layer in page.Layers)
                {
                    WriteLayerElement(sb, layer, "  ");
                }
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static void WriteLayerDefs(StringBuilder sb, LayerNode node, HashSet<string> defIds)
        {
            if (node == null) return;

            // Gradient fills
            if (node.Fills != null)
            {
                for (int i = 0; i < node.Fills.Count; i++)
                {
                    var fill = node.Fills[i];
                    if (fill.Type == PaintType.LinearGradient)
                    {
                        string id = string.Format("grad_lin_{0}_{1}", node.Id, i);
                        if (defIds.Add(id))
                        {
                            double rad = (fill.GradientAngle * Math.PI) / 180.0;
                            double x1 = fill.GradientStart.HasValue ? fill.GradientStart.Value.X : (0.5 - 0.5 * Math.Cos(rad));
                            double y1 = fill.GradientStart.HasValue ? fill.GradientStart.Value.Y : (0.5 - 0.5 * Math.Sin(rad));
                            double x2 = fill.GradientEnd.HasValue ? fill.GradientEnd.Value.X : (0.5 + 0.5 * Math.Cos(rad));
                            double y2 = fill.GradientEnd.HasValue ? fill.GradientEnd.Value.Y : (0.5 + 0.5 * Math.Sin(rad));

                            sb.AppendFormat(CultureInfo.InvariantCulture,
                                "    <linearGradient id=\"{0}\" x1=\"{1:F2}\" y1=\"{2:F2}\" x2=\"{3:F2}\" y2=\"{4:F2}\">\n",
                                id, x1, y1, x2, y2);
                            if (fill.GradientStops != null && fill.GradientStops.Count > 0)
                            {
                                var sorted = new List<GradientStopData>(fill.GradientStops);
                                sorted.Sort((a, b) => a.Position.CompareTo(b.Position));
                                for (int sIdx = 0; sIdx < sorted.Count; sIdx++)
                                {
                                    var stop = sorted[sIdx];
                                    sb.AppendFormat(CultureInfo.InvariantCulture,
                                        "      <stop offset=\"{0:0.##}%\" stop-color=\"{1}\" stop-opacity=\"{2:F2}\" />\n",
                                        stop.Position * 100.0, stop.ColorHex, stop.Opacity * fill.Opacity);
                                    if (sIdx < sorted.Count - 1 && Math.Abs(stop.Midpoint - 0.5) > 0.01 && stop.Midpoint > 0.05 && stop.Midpoint < 0.95)
                                    {
                                        var nextStop = sorted[sIdx + 1];
                                        double midPos = stop.Position + stop.Midpoint * (nextStop.Position - stop.Position);
                                        Color c1 = GpuRenderer.ParseHexColor(stop.ColorHex, stop.Opacity * fill.Opacity);
                                        Color c2 = GpuRenderer.ParseHexColor(nextStop.ColorHex, nextStop.Opacity * fill.Opacity);
                                        Color midColor = GpuRenderer.InterpolateColor(c1, c2, 0.5);
                                        string midHex = string.Format("#{0:X2}{1:X2}{2:X2}", midColor.R, midColor.G, midColor.B);
                                        double midOp = midColor.A / 255.0;
                                        sb.AppendFormat(CultureInfo.InvariantCulture,
                                            "      <stop offset=\"{0:0.##}%\" stop-color=\"{1}\" stop-opacity=\"{2:F2}\" />\n",
                                            midPos * 100.0, midHex, midOp);
                                    }
                                }
                            }
                            sb.AppendLine("    </linearGradient>");
                        }
                    }
                    else if (fill.Type == PaintType.RadialGradient)
                    {
                        string id = string.Format("grad_rad_{0}_{1}", node.Id, i);
                        if (defIds.Add(id))
                        {
                            sb.AppendFormat(CultureInfo.InvariantCulture,
                                "    <radialGradient id=\"{0}\" cx=\"{1:F2}\" cy=\"{2:F2}\" r=\"{3:F2}\">\n",
                                id, fill.GradientCenter.X, fill.GradientCenter.Y, fill.GradientRadius);
                            if (fill.GradientStops != null && fill.GradientStops.Count > 0)
                            {
                                var sorted = new List<GradientStopData>(fill.GradientStops);
                                sorted.Sort((a, b) => a.Position.CompareTo(b.Position));
                                for (int sIdx = 0; sIdx < sorted.Count; sIdx++)
                                {
                                    var stop = sorted[sIdx];
                                    sb.AppendFormat(CultureInfo.InvariantCulture,
                                        "      <stop offset=\"{0:0.##}%\" stop-color=\"{1}\" stop-opacity=\"{2:F2}\" />\n",
                                        stop.Position * 100.0, stop.ColorHex, stop.Opacity * fill.Opacity);
                                    if (sIdx < sorted.Count - 1 && Math.Abs(stop.Midpoint - 0.5) > 0.01 && stop.Midpoint > 0.05 && stop.Midpoint < 0.95)
                                    {
                                        var nextStop = sorted[sIdx + 1];
                                        double midPos = stop.Position + stop.Midpoint * (nextStop.Position - stop.Position);
                                        Color c1 = GpuRenderer.ParseHexColor(stop.ColorHex, stop.Opacity * fill.Opacity);
                                        Color c2 = GpuRenderer.ParseHexColor(nextStop.ColorHex, nextStop.Opacity * fill.Opacity);
                                        Color midColor = GpuRenderer.InterpolateColor(c1, c2, 0.5);
                                        string midHex = string.Format("#{0:X2}{1:X2}{2:X2}", midColor.R, midColor.G, midColor.B);
                                        double midOp = midColor.A / 255.0;
                                        sb.AppendFormat(CultureInfo.InvariantCulture,
                                            "      <stop offset=\"{0:0.##}%\" stop-color=\"{1}\" stop-opacity=\"{2:F2}\" />\n",
                                            midPos * 100.0, midHex, midOp);
                                    }
                                }
                            }
                            sb.AppendLine("    </radialGradient>");
                        }
                    }
                }
            }

            // Drop shadow filter
            if (node.Effects != null)
            {
                var drop = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.DropShadow);
                if (drop != null)
                {
                    string fid = string.Format("filter_shadow_{0}", node.Id);
                    if (defIds.Add(fid))
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "    <filter id=\"{0}\" x=\"-20%\" y=\"-20%\" width=\"140%\" height=\"140%\">\n" +
                            "      <feDropShadow dx=\"{1:F1}\" dy=\"{2:F1}\" stdDeviation=\"{3:F1}\" flood-color=\"{4}\" flood-opacity=\"{5:F2}\" />\n" +
                            "    </filter>\n",
                            fid, drop.OffsetX, drop.OffsetY, drop.Blur / 2.0, drop.ColorHex, drop.Opacity);
                    }
                }
            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    WriteLayerDefs(sb, child, defIds);
                }
            }
        }

        private static void WriteLayerElement(StringBuilder sb, LayerNode node, string indent)
        {
            if (node == null || !node.IsVisible) return;

            string fillAttr = "none";
            if (node.Fills != null)
            {
                for (int i = 0; i < node.Fills.Count; i++)
                {
                    var f = node.Fills[i];
                    if (!f.Enabled) continue;
                    if (f.Type == PaintType.Solid)
                    {
                        fillAttr = f.ColorHex;
                    }
                    else if (f.Type == PaintType.LinearGradient)
                    {
                        fillAttr = string.Format("url(#grad_lin_{0}_{1})", node.Id, i);
                    }
                    else if (f.Type == PaintType.RadialGradient)
                    {
                        fillAttr = string.Format("url(#grad_rad_{0}_{1})", node.Id, i);
                    }
                    break;
                }
            }

            string strokeAttr = "";
            if (node.Strokes != null)
            {
                var s = node.Strokes.FirstOrDefault(st => st.Enabled && st.Width > 0);
                if (s != null && s.Paint != null)
                {
                    strokeAttr = string.Format(CultureInfo.InvariantCulture, " stroke=\"{0}\" stroke-width=\"{1:F1}\"",
                        s.Paint.ColorHex, s.Width);
                }
            }

            string filterAttr = "";
            if (node.Effects != null && node.Effects.Any(e => e.Enabled && e.Type == EffectType.DropShadow))
            {
                filterAttr = string.Format(" filter=\"url(#filter_shadow_{0})\"", node.Id);
            }

            string transformAttr = "";
            if (Math.Abs(node.Rotation) > 0.001)
            {
                transformAttr = string.Format(CultureInfo.InvariantCulture, " transform=\"rotate({0:F1} {1:F1} {2:F1})\"",
                    node.Rotation, node.X + node.Width / 2.0, node.Y + node.Height / 2.0);
            }

            string opacityAttr = "";
            if (node.Opacity < 0.999)
            {
                opacityAttr = string.Format(CultureInfo.InvariantCulture, " opacity=\"{0:F2}\"", node.Opacity);
            }

            switch (node.Type)
            {
                case ElementType.Rectangle:
                case ElementType.Frame:
                    string rxAttr = node.CornerRadius > 0
                        ? string.Format(CultureInfo.InvariantCulture, " rx=\"{0:F1}\"", node.CornerRadius)
                        : "";
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "{0}<rect id=\"{1}\" x=\"{2:F2}\" y=\"{3:F2}\" width=\"{4:F2}\" height=\"{5:F2}\"{6} fill=\"{7}\"{8}{9}{10}{11} />\n",
                        indent, SecurityElement.Escape(node.Id), node.X, node.Y, node.Width, node.Height,
                        rxAttr, fillAttr, strokeAttr, filterAttr, transformAttr, opacityAttr);
                    break;

                case ElementType.Ellipse:
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "{0}<ellipse id=\"{1}\" cx=\"{2:F2}\" cy=\"{3:F2}\" rx=\"{4:F2}\" ry=\"{5:F2}\" fill=\"{6}\"{7}{8}{9}{10} />\n",
                        indent, SecurityElement.Escape(node.Id),
                        node.X + node.Width / 2.0, node.Y + node.Height / 2.0,
                        node.Width / 2.0, node.Height / 2.0,
                        fillAttr, strokeAttr, filterAttr, transformAttr, opacityAttr);
                    break;

                case ElementType.Text:
                    var typo = node.Typography ?? new TypographyData();
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "{0}<text id=\"{1}\" x=\"{2:F2}\" y=\"{3:F2}\" font-family=\"{4}\" font-size=\"{5:F1}\" font-weight=\"{6}\" fill=\"{7}\"{8}{9}>{10}</text>\n",
                        indent, SecurityElement.Escape(node.Id), node.X, node.Y + typo.FontSize,
                        SecurityElement.Escape(typo.FontFamily ?? "Segoe UI"),
                        typo.FontSize, typo.FontWeight, fillAttr, transformAttr, opacityAttr,
                        SecurityElement.Escape(typo.Text ?? ""));
                    break;

                case ElementType.Path:
                default:
                    string pathData = "";
                    if (node.Geometry != null)
                    {
                        if (node.Geometry.Nodes != null && node.Geometry.Nodes.Count > 0)
                        {
                            pathData = BezierVectorEngine.ToSvgPathData(node.Geometry);
                        }
                        else if (!string.IsNullOrEmpty(node.Geometry.SvgPathData))
                        {
                            pathData = node.Geometry.SvgPathData;
                        }
                    }
                    if (string.IsNullOrEmpty(pathData))
                    {
                        pathData = string.Format(CultureInfo.InvariantCulture, "M {0:F1} {1:F1} h {2:F1} v {3:F1} h -{2:F1} Z",
                            node.X, node.Y, node.Width, node.Height);
                    }

                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "{0}<path id=\"{1}\" d=\"{2}\" fill=\"{3}\"{4}{5}{6}{7} />\n",
                        indent, SecurityElement.Escape(node.Id), pathData, fillAttr, strokeAttr, filterAttr, transformAttr, opacityAttr);
                    break;
            }

            // Render children if frame/group
            if (node.Children != null && node.Children.Count > 0)
            {
                foreach (var child in node.Children)
                {
                    WriteLayerElement(sb, child, indent + "  ");
                }
            }
        }
    }
}
