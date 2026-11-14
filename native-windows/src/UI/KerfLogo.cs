using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Kerf.UI
{
    /// <summary>
    /// Kerf official vector logo and icon visual factory.
    /// Implements the brand mark geometry from Kerf-Logo.svg and kerf-icon.svg:
    /// - Logo Badge: 205x205 rounded squircle (rx=43) filled with #46EFB1 (Electric Mint)
    ///   containing the stylized geometric cutouts filled with #1E082E (Obsidian Plum).
    /// - Standalone Icon: 108x134 geometric mark rendered with customizable fill (#46EFB1 default).
    /// </summary>
    public static class KerfLogo
    {
        private static readonly Color BrandMint = Color.FromRgb(0x46, 0xEF, 0xB1);
        private static readonly Color BrandObsidian = Color.FromRgb(0x1E, 0x08, 0x2E);
        private static readonly SolidColorBrush MintBrush = FreezeBrush(new SolidColorBrush(BrandMint));
        private static readonly SolidColorBrush ObsidianBrush = FreezeBrush(new SolidColorBrush(BrandObsidian));

        private static SolidColorBrush FreezeBrush(SolidColorBrush b)
        {
            if (b != null && b.CanFreeze) b.Freeze();
            return b;
        }

        private static DrawingGroup _cachedLogoDrawingGroup;
        private static ImageSource _cachedLogoImage;
        private static DrawingGroup _cachedIconDrawingGroup;
        private static ImageSource _cachedIconImage;
        private static ImageSource _cachedAssetIconImage;

        // Path geometries from official vector SVG definitions
        public const string PathCutout1 = "M54.4821 56.685C51.283 53.6375 51.1601 48.5737 54.2075 45.3746L71.2518 27.4821C74.2992 24.283 79.3631 24.1601 82.5622 27.2075L100.455 44.2518C103.654 47.2992 103.777 52.3631 100.729 55.5622L83.685 73.4546C80.6375 76.6537 75.5737 76.7767 72.3746 73.7292L54.4821 56.685Z";
        public const string PathCutout2 = "M114.372 51.4542L57.8722 107.516C57.1154 108.267 56.6896 109.289 56.6896 110.356V154.138C56.6896 157.687 60.9671 159.478 63.4959 156.989L117.907 103.422C120.007 101.354 121.19 98.53 121.19 95.5829V54.2936C121.19 50.7375 116.897 48.9494 114.372 51.4542Z";
        public const string PathCutout3 = "M119.21 108.561L103.48 124.291C101.321 126.45 101.127 129.885 103.03 132.273L122.279 156.429C123.418 157.857 125.145 158.69 126.972 158.69H153.627C158.583 158.69 161.402 153.022 158.412 149.07L128.237 109.184C126.04 106.28 121.784 105.986 119.21 108.561Z";

        // Standalone Icon paths from kerf-icon.svg (viewBox 0 0 108 134)
        public const string IconPath1 = "M2.48212 31.685C-0.716979 28.6375 -0.839915 23.5737 2.20753 20.3746L19.2518 2.48212C22.2992 -0.71698 27.3631 -0.839915 30.5622 2.20753L48.4546 19.2518C51.6537 22.2992 51.7767 27.3631 48.7292 30.5622L31.685 48.4546C28.6375 51.6537 23.5737 51.7767 20.3746 48.7292L2.48212 31.685Z";
        public const string IconPath2 = "M62.3722 26.4542L5.87224 82.5162C5.11535 83.2672 4.68964 84.2894 4.68964 85.3556V129.138C4.68964 132.687 8.96714 134.478 11.4959 131.989L65.9068 78.4216C68.007 76.354 69.1896 73.53 69.1896 70.5829V29.2936C69.1896 25.7375 64.8966 23.9494 62.3722 26.4542Z";
        public const string IconPath3 = "M67.2096 83.5609L51.4797 99.2909C49.321 101.45 49.1274 104.885 51.03 107.273L70.2794 131.429C71.4178 132.857 73.145 133.69 74.9717 133.69H101.627C106.583 133.69 109.402 128.022 106.412 124.07L76.2373 84.1836C74.0404 81.2798 69.7844 80.9862 67.2096 83.5609Z";

        /// <summary>
        /// Returns the official Kerf Logo badge DrawingGroup (Electric Mint rounded badge with Obsidian Plum geometry).
        /// </summary>
        public static DrawingGroup GetLogoDrawingGroup()
        {
            if (_cachedLogoDrawingGroup != null) return _cachedLogoDrawingGroup;

            var group = new DrawingGroup();
            group.ClipGeometry = new RectangleGeometry(new Rect(0, 0, 205, 205));

            // Background squircle (rx=43, ry=43)
            var badgeGeo = new RectangleGeometry(new Rect(0, 0, 205, 205), 43, 43);
            badgeGeo.Freeze();
            group.Children.Add(new GeometryDrawing(MintBrush, null, badgeGeo));

            // Obsidian plum cuts
            var darkBrush = ObsidianBrush;

            var g1 = Geometry.Parse(PathCutout1);
            g1.Freeze();
            group.Children.Add(new GeometryDrawing(darkBrush, null, g1));

            var g2 = Geometry.Parse(PathCutout2);
            g2.Freeze();
            group.Children.Add(new GeometryDrawing(darkBrush, null, g2));

            var g3 = Geometry.Parse(PathCutout3);
            g3.Freeze();
            group.Children.Add(new GeometryDrawing(darkBrush, null, g3));

            group.Freeze();
            _cachedLogoDrawingGroup = group;
            return _cachedLogoDrawingGroup;
        }

        public static ImageSource GetLogoImageSource()
        {
            if (_cachedLogoImage != null) return _cachedLogoImage;
            _cachedLogoImage = GetAssetIconImage() ?? new DrawingImage(GetLogoDrawingGroup());
            _cachedLogoImage.Freeze();
            return _cachedLogoImage;
        }

        /// <summary>
        /// Returns the official standalone Kerf Icon mark DrawingGroup (viewBox 0 0 108 134).
        /// </summary>
        public static DrawingGroup GetIconDrawingGroup(Brush fillBrush = null)
        {
            var brush = fillBrush ?? MintBrush;
            if (brush == MintBrush && _cachedIconDrawingGroup != null)
            {
                return _cachedIconDrawingGroup;
            }

            var group = new DrawingGroup();
            group.ClipGeometry = new RectangleGeometry(new Rect(0, 0, 108, 134));

            var g1 = Geometry.Parse(IconPath1);
            g1.Freeze();
            group.Children.Add(new GeometryDrawing(brush, null, g1));

            var g2 = Geometry.Parse(IconPath2);
            g2.Freeze();
            group.Children.Add(new GeometryDrawing(brush, null, g2));

            var g3 = Geometry.Parse(IconPath3);
            g3.Freeze();
            group.Children.Add(new GeometryDrawing(brush, null, g3));

            group.Freeze();

            if (brush == MintBrush)
            {
                _cachedIconDrawingGroup = group;
            }
            return group;
        }

        public static ImageSource GetIconImageSource(Brush fillBrush = null)
        {
            ImageSource assetIcon = GetAssetIconImage();
            if (assetIcon != null) return assetIcon;

            var brush = fillBrush ?? MintBrush;
            if (brush == MintBrush && _cachedIconImage != null)
            {
                return _cachedIconImage;
            }

            var img = new DrawingImage(GetIconDrawingGroup(brush));
            img.Freeze();

            if (brush == MintBrush)
            {
                _cachedIconImage = img;
            }
            return img;
        }

        private static ImageSource GetAssetIconImage()
        {
            if (_cachedAssetIconImage != null) return _cachedAssetIconImage;
            try
            {
                using (var stream = typeof(KerfLogo).Assembly.GetManifestResourceStream("Kerf.Icon"))
                {
                    if (stream == null) return null;
                    var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    var frame = decoder.Frames.OrderByDescending(item => item.PixelWidth * item.PixelHeight).FirstOrDefault();
                    if (frame == null) return null;
                    frame.Freeze();
                    _cachedAssetIconImage = frame;
                    return _cachedAssetIconImage;
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Creates a high-fidelity vector element representing the Kerf logo badge.
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
                    Color = BrandMint,
                    BlurRadius = Math.Max(8, size * 0.35),
                    ShadowDepth = 0,
                    Opacity = 0.65
                };
            }

            return img;
        }

        /// <summary>
        /// Creates a high-fidelity vector element representing the standalone Kerf Icon mark.
        /// </summary>
        public static FrameworkElement CreateIconElement(double width = 28, double height = 34, Brush fillBrush = null, bool withGlow = false)
        {
            var img = new Image
            {
                Source = GetIconImageSource(fillBrush),
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (withGlow)
            {
                img.Effect = new DropShadowEffect
                {
                    Color = BrandMint,
                    BlurRadius = Math.Max(10, width * 0.4),
                    ShadowDepth = 0,
                    Opacity = 0.75
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
