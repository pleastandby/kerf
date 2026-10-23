using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Kerf.UI
{
    /// <summary>
    /// Kerf vector logo generator and visual factory.
    /// Implements the official iridescent folded-ribbon brand mark (peach, coral, indigo, sky blue, mint & teal gradients).
    /// </summary>
    public static class KerfLogo
    {
        private static DrawingGroup _cachedDrawingGroup;
        private static DrawingImage _cachedDrawingImage;

        public static DrawingGroup GetLogoDrawingGroup()
        {
            if (_cachedDrawingGroup != null) return _cachedDrawingGroup;

            var group = new DrawingGroup();
            group.ClipGeometry = new RectangleGeometry(new Rect(0, 0, 512, 512));

            // 1. peachCoral gradient & path
            var peachCoral = new LinearGradientBrush
            {
                StartPoint = new Point(116, 72),
                EndPoint = new Point(232, 286),
                MappingMode = BrushMappingMode.Absolute
            };
            peachCoral.GradientStops.Add(new GradientStop(Color.FromRgb(255, 232, 199), 0.00));
            peachCoral.GradientStops.Add(new GradientStop(Color.FromRgb(255, 199, 168), 0.38));
            peachCoral.GradientStops.Add(new GradientStop(Color.FromRgb(255, 146, 127), 0.72));
            peachCoral.GradientStops.Add(new GradientStop(Color.FromRgb(245, 109, 136), 1.00));
            peachCoral.Freeze();

            var geo1 = Geometry.Parse("M113 169C113 115 151 73 205 73H242V211C242 235 229 254 208 268L113 326V169Z");
            geo1.Freeze();
            group.Children.Add(new GeometryDrawing(peachCoral, null, geo1));

            // 2. indigoBlue gradient & path
            var indigoBlue = new LinearGradientBrush
            {
                StartPoint = new Point(122, 282),
                EndPoint = new Point(237, 420),
                MappingMode = BrushMappingMode.Absolute
            };
            indigoBlue.GradientStops.Add(new GradientStop(Color.FromRgb(108, 84, 182), 0.00));
            indigoBlue.GradientStops.Add(new GradientStop(Color.FromRgb(53, 79, 142), 0.48));
            indigoBlue.GradientStops.Add(new GradientStop(Color.FromRgb(23, 69, 104), 1.00));
            indigoBlue.Freeze();

            var geo2 = Geometry.Parse("M113 326L208 268C226 257 242 268 242 289V420H170C138 420 113 395 113 363V326Z");
            geo2.Freeze();
            group.Children.Add(new GeometryDrawing(indigoBlue, null, geo2));

            // 3. skyBlue gradient & upper wing
            var skyBlue = new LinearGradientBrush
            {
                StartPoint = new Point(265, 100),
                EndPoint = new Point(380, 228),
                MappingMode = BrushMappingMode.Absolute
            };
            skyBlue.GradientStops.Add(new GradientStop(Color.FromRgb(184, 228, 255), 0.00));
            skyBlue.GradientStops.Add(new GradientStop(Color.FromRgb(119, 185, 245), 0.55));
            skyBlue.GradientStops.Add(new GradientStop(Color.FromRgb(52, 118, 216), 1.00));
            skyBlue.Freeze();

            var geo3 = Geometry.Parse("M242 179L303 118C326 95 346 92 369 92H410V143C410 164 401 181 385 196L315 255L242 210V179Z");
            geo3.Freeze();
            group.Children.Add(new GeometryDrawing(skyBlue, null, geo3));

            // 4. mintTeal gradient & lower wing
            var mintTeal = new LinearGradientBrush
            {
                StartPoint = new Point(270, 264),
                EndPoint = new Point(390, 405),
                MappingMode = BrushMappingMode.Absolute
            };
            mintTeal.GradientStops.Add(new GradientStop(Color.FromRgb(216, 242, 189), 0.00));
            mintTeal.GradientStops.Add(new GradientStop(Color.FromRgb(118, 214, 183), 0.38));
            mintTeal.GradientStops.Add(new GradientStop(Color.FromRgb(39, 184, 169), 0.75));
            mintTeal.GradientStops.Add(new GradientStop(Color.FromRgb(0, 156, 152), 1.00));
            mintTeal.Freeze();

            var geo4 = Geometry.Parse("M242 235L315 255L382 324C400 342 410 358 410 379C410 402 392 420 369 420C350 420 337 412 322 397L242 315V235Z");
            geo4.Freeze();
            group.Children.Add(new GeometryDrawing(mintTeal, null, geo4));

            // 5. warmFold (opacity 0.96)
            var warmFold = new LinearGradientBrush
            {
                StartPoint = new Point(160, 180),
                EndPoint = new Point(254, 285),
                MappingMode = BrushMappingMode.Absolute,
                Opacity = 0.96
            };
            warmFold.GradientStops.Add(new GradientStop(Color.FromRgb(255, 178, 157), 0.00));
            warmFold.GradientStops.Add(new GradientStop(Color.FromRgb(246, 185, 199), 0.50));
            warmFold.GradientStops.Add(new GradientStop(Color.FromRgb(213, 182, 240), 1.00));
            warmFold.Freeze();

            var geo5 = Geometry.Parse("M113 326L208 268C226 257 242 241 242 211V289C242 305 232 317 216 326L168 354C145 367 124 355 113 340V326Z");
            geo5.Freeze();
            group.Children.Add(new GeometryDrawing(warmFold, null, geo5));

            // 6. centerFold (opacity 0.98)
            var centerFold = new LinearGradientBrush
            {
                StartPoint = new Point(232, 210),
                EndPoint = new Point(314, 280),
                MappingMode = BrushMappingMode.Absolute,
                Opacity = 0.98
            };
            centerFold.GradientStops.Add(new GradientStop(Color.FromRgb(178, 196, 241), 0.00));
            centerFold.GradientStops.Add(new GradientStop(Color.FromRgb(138, 207, 203), 0.50));
            centerFold.GradientStops.Add(new GradientStop(Color.FromRgb(212, 232, 182), 1.00));
            centerFold.Freeze();

            var geo6 = Geometry.Parse("M242 211L315 255L270 285L242 315V211Z");
            geo6.Freeze();
            group.Children.Add(new GeometryDrawing(centerFold, null, geo6));

            // 7. centerHighlight (solid with opacity 0.32)
            var centerHighlight = new SolidColorBrush(Color.FromArgb(82, 185, 216, 216));
            centerHighlight.Freeze();
            group.Children.Add(new GeometryDrawing(centerHighlight, null, geo6));

            // 8. blueFold (opacity 0.82)
            var blueFold = new LinearGradientBrush
            {
                StartPoint = new Point(160, 280),
                EndPoint = new Point(244, 355),
                MappingMode = BrushMappingMode.Absolute,
                Opacity = 0.82
            };
            blueFold.GradientStops.Add(new GradientStop(Color.FromRgb(115, 89, 181), 0.00));
            blueFold.GradientStops.Add(new GradientStop(Color.FromRgb(38, 78, 135), 1.00));
            blueFold.Freeze();

            var geo8 = Geometry.Parse("M113 326C128 302 151 294 174 300L216 311C231 315 242 329 242 347V420H170C138 420 113 395 113 363V326Z");
            geo8.Freeze();
            group.Children.Add(new GeometryDrawing(blueFold, null, geo8));

            // 9. tealAccent (opacity 0.24)
            var tealAccent = new SolidColorBrush(Color.FromArgb(61, 53, 190, 178));
            tealAccent.Freeze();
            var geo9 = Geometry.Parse("M242 315L270 285L322 334L282 365L242 326V315Z");
            geo9.Freeze();
            group.Children.Add(new GeometryDrawing(tealAccent, null, geo9));

            group.Freeze();
            _cachedDrawingGroup = group;
            return _cachedDrawingGroup;
        }

        public static ImageSource GetLogoImageSource()
        {
            if (_cachedDrawingImage != null) return _cachedDrawingImage;
            _cachedDrawingImage = new DrawingImage(GetLogoDrawingGroup());
            _cachedDrawingImage.Freeze();
            return _cachedDrawingImage;
        }

        /// <summary>
        /// Creates a high-fidelity vector element representing the Kerf logo at any specified width & height.
        /// </summary>
        public static FrameworkElement CreateLogoElement(double size = 32, bool withGlow = false)
        {
            var img = new Image
            {
                Source = GetLogoImageSource(),
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (withGlow)
            {
                img.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(56, 189, 248),
                    BlurRadius = Math.Max(8, size * 0.4),
                    ShadowDepth = 0,
                    Opacity = 0.55
                };
            }

            return img;
        }

        /// <summary>
        /// Renders the vector logo into a 32-bit ARGB RenderTargetBitmap.
        /// </summary>
        public static RenderTargetBitmap RenderBitmap(int pixelWidth = 256, int pixelHeight = 256)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawDrawing(GetLogoDrawingGroup());
            }

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }
    }
}
