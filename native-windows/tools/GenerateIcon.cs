using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

namespace Kerf.Tools
{
    public static class IconBuilder
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Console.Error.WriteLine("Usage: GenerateIcon.exe <logo.svg> <output.ico>");
                return 2;
            }

            try
            {
                var root = LoadSvg(args[0]);
                DrawingGroup drawing = CreateDrawing(root);
                int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
                byte[][] pngFrames = new byte[sizes.Length][];

                for (int i = 0; i < sizes.Length; i++)
                    pngFrames[i] = RenderPng(drawing, sizes[i]);

                WriteIcon(args[1], sizes, pngFrames);
                Console.WriteLine("Icon generated from " + args[0] + " -> " + args[1]);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Icon generation failed: " + ex.Message);
                return 1;
            }
        }

        private static XmlElement LoadSvg(string path)
        {
            var document = new XmlDocument();
            document.XmlResolver = null;
            document.Load(path);
            if (document.DocumentElement == null || document.DocumentElement.LocalName != "svg")
                throw new InvalidDataException("The icon source is not an SVG document.");
            return document.DocumentElement;
        }

        private static DrawingGroup CreateDrawing(XmlElement root)
        {
            double[] viewBox = ParseNumberList(root.GetAttribute("viewBox"));
            if (viewBox.Length != 4 || viewBox[2] <= 0.0 || viewBox[3] <= 0.0)
                throw new InvalidDataException("The icon SVG must define a valid viewBox.");

            double viewX = viewBox[0];
            double viewY = viewBox[1];
            double viewWidth = viewBox[2];
            double viewHeight = viewBox[3];
            var gradients = new Dictionary<string, LinearGradientBrush>();
            foreach (XmlNode node in root.SelectNodes(".//*[local-name()='linearGradient']"))
            {
                var element = node as XmlElement;
                if (element == null) continue;
                string id = element.GetAttribute("id");
                if (string.IsNullOrEmpty(id)) continue;

                var brush = new LinearGradientBrush
                {
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = new Point(ParseNumber(element.GetAttribute("x1")), ParseNumber(element.GetAttribute("y1"))),
                    EndPoint = new Point(ParseNumber(element.GetAttribute("x2")), ParseNumber(element.GetAttribute("y2"))),
                    SpreadMethod = GradientSpreadMethod.Pad
                };
                foreach (XmlNode stopNode in element.SelectNodes("./*[local-name()='stop']"))
                {
                    var stopElement = stopNode as XmlElement;
                    if (stopElement == null) continue;
                    string colorText = stopElement.GetAttribute("stop-color");
                    Color color = (Color)ColorConverter.ConvertFromString(colorText);
                    string stopOpacity = stopElement.GetAttribute("stop-opacity");
                    if (!string.IsNullOrEmpty(stopOpacity))
                        color.A = (byte)Math.Max(0, Math.Min(255, Math.Round(color.A * ParseNumber(stopOpacity))));
                    brush.GradientStops.Add(new GradientStop(color, ParseOffset(stopElement.GetAttribute("offset"))));
                }
                brush.Freeze();
                gradients[id] = brush;
            }

            var drawing = new DrawingGroup();
            drawing.ClipGeometry = new RectangleGeometry(new Rect(viewX, viewY, viewWidth, viewHeight));
            double scale = 256.0 / Math.Max(viewWidth, viewHeight);
            double offsetX = (256.0 - viewWidth * scale) / 2.0;
            double offsetY = (256.0 - viewHeight * scale) / 2.0;
            drawing.Transform = new MatrixTransform(new Matrix(scale, 0, 0, scale, offsetX - viewX * scale, offsetY - viewY * scale));

            foreach (XmlNode node in root.SelectNodes(".//*[local-name()='rect' or local-name()='path']"))
            {
                var element = node as XmlElement;
                if (element == null) continue;
                string fill = element.GetAttribute("fill");
                if (string.IsNullOrEmpty(fill) || fill == "none") continue;

                Brush brush;
                Match gradientMatch = Regex.Match(fill, "^url\\(#([^)]*)\\)$");
                if (gradientMatch.Success)
                {
                    LinearGradientBrush gradient;
                    if (!gradients.TryGetValue(gradientMatch.Groups[1].Value, out gradient))
                        throw new InvalidDataException("Missing SVG gradient: " + gradientMatch.Groups[1].Value);
                    brush = gradient.Clone();
                }
                else
                {
                    brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fill));
                }

                string opacity = element.GetAttribute("opacity");
                if (!string.IsNullOrEmpty(opacity)) brush.Opacity = ParseNumber(opacity);
                brush.Freeze();
                Geometry geometry;
                if (element.LocalName == "rect")
                {
                    double x = ParseNumber(element.GetAttribute("x"));
                    double y = ParseNumber(element.GetAttribute("y"));
                    double width = ParseNumber(element.GetAttribute("width"));
                    double height = ParseNumber(element.GetAttribute("height"));
                    double radiusX = ParseNumber(element.GetAttribute("rx"));
                    double radiusY = ParseNumber(element.GetAttribute("ry"));
                    if (radiusY <= 0.0) radiusY = radiusX;
                    geometry = new RectangleGeometry(new Rect(x, y, width, height), radiusX, radiusY);
                }
                else
                {
                    string data = element.GetAttribute("d");
                    if (string.IsNullOrEmpty(data)) continue;
                    geometry = Geometry.Parse(data);
                }
                geometry.Freeze();
                drawing.Children.Add(new GeometryDrawing(brush, null, geometry));
            }

            drawing.Freeze();
            return drawing;
        }

        private static byte[] RenderPng(DrawingGroup drawing, int size)
        {
            var visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                context.PushTransform(new ScaleTransform(size / 256.0, size / 256.0));
                context.DrawDrawing(drawing);
                context.Pop();
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        private static void WriteIcon(string path, int[] sizes, byte[][] pngFrames)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)sizes.Length);
                int offset = 6 + (16 * sizes.Length);
                for (int i = 0; i < sizes.Length; i++)
                {
                    int size = sizes[i];
                    writer.Write((byte)(size == 256 ? 0 : size));
                    writer.Write((byte)(size == 256 ? 0 : size));
                    writer.Write((byte)0);
                    writer.Write((byte)0);
                    writer.Write((ushort)1);
                    writer.Write((ushort)32);
                    writer.Write((uint)pngFrames[i].Length);
                    writer.Write((uint)offset);
                    offset += pngFrames[i].Length;
                }
                foreach (byte[] frame in pngFrames) writer.Write(frame);
                File.WriteAllBytes(path, stream.ToArray());
            }
        }

        private static double ParseNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0.0;
            return double.Parse(value.Trim().TrimEnd('p', 'x'), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static double[] ParseNumberList(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return new double[0];
            string[] parts = Regex.Split(value.Trim(), "[\\s,]+");
            var values = new List<double>();
            foreach (string part in parts)
                if (!string.IsNullOrWhiteSpace(part)) values.Add(ParseNumber(part));
            return values.ToArray();
        }

        private static double ParseOffset(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0.0;
            value = value.Trim();
            if (value.EndsWith("%", StringComparison.Ordinal))
                return ParseNumber(value.Substring(0, value.Length - 1)) / 100.0;
            return ParseNumber(value);
        }
    }
}
