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
        public static Page ActivePage { get; set; }
        public Page CurrentPage { get { return ActivePage; } set { ActivePage = value; } }

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

                // Draw Liquid Glass pass
                RenderLiquidGlassPass(node, dc);

                // Draw Grain pass
                RenderGrainPass(node, dc);

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
                // Synchronize polar / offsets if needed
                if (dropShadow.Distance > 0.0 && dropShadow.OffsetX == 0.0 && dropShadow.OffsetY == 0.0)
                {
                    dropShadow.UpdateOffsetsFromPolar();
                }
                else if (dropShadow.Distance == 0.0 && (dropShadow.OffsetX != 0.0 || dropShadow.OffsetY != 0.0))
                {
                    dropShadow.UpdatePolarFromOffsets();
                }

                double depth = Math.Sqrt(dropShadow.OffsetX * dropShadow.OffsetX + dropShadow.OffsetY * dropShadow.OffsetY);
                // WPF DropShadowEffect.Direction: 0 = East (+X), 90 = North (-Y), 180 = West (-X), 270 = South (+Y).
                double wpfAngle = Math.Atan2(-dropShadow.OffsetY, dropShadow.OffsetX) * (180.0 / Math.PI);
                if (wpfAngle < 0) wpfAngle += 360.0;
                Color shadowColor = ParseHexColor(dropShadow.ColorHex, 1.0);

                var dse = new DropShadowEffect
                {
                    BlurRadius = Math.Max(1.0, dropShadow.Blur),
                    ShadowDepth = depth,
                    Direction = wpfAngle,
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

        private static BitmapSource _proceduralNoise;
        private static readonly Dictionary<string, BitmapSource> _liquidGlassCache = new Dictionary<string, BitmapSource>();

        private static float SdfSquircle(float px, float py, float cx, float cy, float hw, float hh, float r)
        {
            r = Math.Max(0.0f, Math.Min(r, Math.Min(hw, hh)));
            float qx = Math.Abs(px - cx) - (hw - r);
            float qy = Math.Abs(py - cy) - (hh - r);
            float ox = Math.Max(0.0f, qx);
            float oy = Math.Max(0.0f, qy);
            float outside = (float)Math.Sqrt(ox * ox + oy * oy);
            return outside + Math.Min(0.0f, Math.Max(qx, qy)) - r;
        }

        private static float SquircleHeight(float x)
        {
            float clamped = Math.Max(0.0f, Math.Min(1.0f, x));
            float inv = 1.0f - clamped;
            float inv4 = inv * inv * inv * inv;
            return (float)Math.Pow(Math.Max(0.0, 1.0 - inv4), 0.25);
        }

        private static float SquircleHeightSlope(float x)
        {
            float e = 0.001f;
            return (SquircleHeight(x + e) - SquircleHeight(x - e)) / (2.0f * e);
        }

        private static void SnellRefract(float ix, float iy, float iz, float nx, float ny, float nz, float eta, out float rx, out float ry, out float rz)
        {
            float d = ix * nx + iy * ny + iz * nz;
            float k = 1.0f - eta * eta * (1.0f - d * d);
            if (k < 0.0f)
            {
                rx = 0.0f; ry = 0.0f; rz = -1.0f;
                return;
            }
            float s = eta * d + (float)Math.Sqrt(k);
            rx = eta * ix - s * nx;
            ry = eta * iy - s * ny;
            rz = eta * iz - s * nz;
        }

        private static float SampleBilinearChannel(byte[] buf, int w, int h, int stride, float x, float y, int channel)
        {
            x = Math.Max(0.0f, Math.Min(w - 1.0f, x));
            y = Math.Max(0.0f, Math.Min(h - 1.0f, y));

            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(w - 1, x0 + 1);
            int y1 = Math.Min(h - 1, y0 + 1);

            float fx = x - x0;
            float fy = y - y0;

            float a = buf[y0 * stride + x0 * 4 + channel];
            float b = buf[y0 * stride + x1 * 4 + channel];
            float c = buf[y1 * stride + x0 * 4 + channel];
            float d = buf[y1 * stride + x1 * 4 + channel];

            float top = a * (1.0f - fx) + b * fx;
            float bot = c * (1.0f - fx) + d * fx;
            return top * (1.0f - fy) + bot * fy;
        }

        private static readonly GpuRenderer _defaultInstance = new GpuRenderer();

        private static void RenderLiquidGlassManagedV2(
            byte[] bg,
            int bgW,
            int bgH,
            int bgStride,
            byte[] output,
            int outW,
            int outH,
            int outStride,
            float marginX,
            float marginY,
            float radius,
            float bezel,
            float bezelHeight,
            float thickness,
            float frostSigma,
            float dispersion,
            float lightX,
            float lightY,
            float tintR,
            float tintG,
            float tintB,
            float tintAlpha,
            float specularIntensity)
        {
            float dIor = dispersion <= 0.0f ? 0.08f : Math.Max(0.01f, Math.Min(0.25f, dispersion));
            // In Bgra format: channel 0 = Blue, channel 1 = Green, channel 2 = Red
            // Red bends less (lower IOR), Blue bends more (higher IOR)
            float[] ior = new float[] { 1.50f + dIor, 1.50f, 1.50f - dIor };

            float lightLen = (float)Math.Sqrt(lightX * lightX + lightY * lightY);
            if (lightLen < 1e-5f) lightLen = 1.0f;
            float lx = lightX / lightLen;
            float ly = lightY / lightLen;

            float bezelVal = Math.Max(2.0f, bezel);
            float bezelH = Math.Max(1.0f, bezelHeight);
            float thick = Math.Max(5.0f, thickness);

            float hw = outW * 0.5f;
            float hh = outH * 0.5f;
            float cx = hw;
            float cy = hh;

            for (int y = 0; y < outH; y++)
            {
                int outRow = y * outStride;
                float fy = y + 0.5f;
                float bgFy = fy + marginY;

                for (int x = 0; x < outW; x++)
                {
                    int outIdx = outRow + x * 4;
                    float fx = x + 0.5f;
                    float bgFx = fx + marginX;

                    float d = SdfSquircle(fx, fy, cx, cy, hw, hh, radius);
                    float coverage = Math.Max(0.0f, Math.Min(1.0f, 0.5f - d));
                    if (coverage <= 0.0f)
                    {
                        output[outIdx] = 0;
                        output[outIdx + 1] = 0;
                        output[outIdx + 2] = 0;
                        output[outIdx + 3] = 0;
                        continue;
                    }

                    // Outward 2D normal from SDF gradient
                    float e = 0.5f;
                    float gx = SdfSquircle(fx + e, fy, cx, cy, hw, hh, radius) - SdfSquircle(fx - e, fy, cx, cy, hw, hh, radius);
                    float gy = SdfSquircle(fx, fy + e, cx, cy, hw, hh, radius) - SdfSquircle(fx, fy - e, cx, cy, hw, hh, radius);
                    float gl = (float)Math.Sqrt(gx * gx + gy * gy);
                    if (gl < 1e-6f) gl = 1e-6f;
                    float n2x = gx / gl;
                    float n2y = gy / gl;

                    float depth = -d;
                    float t = Math.Max(0.0f, Math.Min(1.0f, depth / bezelVal));
                    float slope = SquircleHeightSlope(t) * (bezelH / bezelVal);
                    bool inBezel = depth < bezelVal;

                    // 3D surface normal
                    float nx = inBezel ? n2x * slope : 0.0f;
                    float ny = inBezel ? n2y * slope : 0.0f;
                    float nl = (float)Math.Sqrt(nx * nx + ny * ny + 1.0f);
                    float normX = nx / nl;
                    float normY = ny / nl;
                    float normZ = 1.0f / nl;

                    // Refract per channel (chromatic dispersion with Snell's law)
                    float[] refractedBgr = new float[3];
                    for (int c = 0; c < 3; c++)
                    {
                        float rx, ry, rz;
                        SnellRefract(0.0f, 0.0f, -1.0f, normX, normY, normZ, 1.0f / ior[c], out rx, out ry, out rz);
                        float travel = thick / Math.Max(0.05f, -rz);
                        float sx = bgFx + rx * travel;
                        float sy = bgFy + ry * travel;

                        refractedBgr[c] = SampleBilinearChannel(bg, bgW, bgH, bgStride, sx, sy, c);
                    }

                    // Specular highlight on bezel facing light
                    float spec = 0.0f;
                    if (inBezel)
                    {
                        float facing = Math.Abs(n2x * lx + n2y * ly);
                        spec = (float)Math.Pow(facing, 1.5) * (1.0f - t) * (1.0f - t) * 0.9f * specularIntensity;
                    }

                    // Composite tint and specular highlight
                    float finalB = Math.Min(255.0f, refractedBgr[0] * (1.0f - tintAlpha) + tintB * tintAlpha + 255.0f * spec);
                    float finalG = Math.Min(255.0f, refractedBgr[1] * (1.0f - tintAlpha) + tintG * tintAlpha + 255.0f * spec);
                    float finalR = Math.Min(255.0f, refractedBgr[2] * (1.0f - tintAlpha) + tintR * tintAlpha + 255.0f * spec);

                    float baseB = SampleBilinearChannel(bg, bgW, bgH, bgStride, bgFx, bgFy, 0);
                    float baseG = SampleBilinearChannel(bg, bgW, bgH, bgStride, bgFx, bgFy, 1);
                    float baseR = SampleBilinearChannel(bg, bgW, bgH, bgStride, bgFx, bgFy, 2);

                    output[outIdx] = (byte)(baseB * (1.0f - coverage) + finalB * coverage);
                    output[outIdx + 1] = (byte)(baseG * (1.0f - coverage) + finalG * coverage);
                    output[outIdx + 2] = (byte)(baseR * (1.0f - coverage) + finalR * coverage);
                    output[outIdx + 3] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(coverage * 255.0f)));
                }
            }
        }

        private static void RenderLiquidGlassManaged(
            byte[] bg,
            int w,
            int h,
            int stride,
            byte[] output,
            float cx,
            float cy,
            float hw,
            float hh,
            float radius,
            float bezel,
            float bezelHeight,
            float thickness,
            float frostSigma,
            float lightX,
            float lightY,
            float tintR,
            float tintG,
            float tintB,
            float tintAlpha,
            float specularIntensity)
        {
            RenderLiquidGlassManagedV2(
                bg, w, h, stride,
                output, w, h, stride,
                0.0f, 0.0f, radius,
                bezel, bezelHeight, thickness, frostSigma, 0.08f,
                lightX, lightY,
                tintR, tintG, tintB, tintAlpha,
                specularIntensity);
        }

        public static BitmapSource RenderLiquidGlassBackdropTexture(
            byte[] bg,
            int bgW,
            int bgH,
            int bgStride,
            float marginX,
            float marginY,
            int outW,
            int outH,
            float radius,
            float bezel,
            float bezelHeight,
            float thickness,
            float frostSigma,
            float dispersion,
            float lightX,
            float lightY,
            Color tintColor,
            float tintAlpha,
            float specularIntensity)
        {
            outW = Math.Max(16, Math.Min(2560, outW));
            outH = Math.Max(16, Math.Min(2560, outH));
            int outStride = outW * 4;
            byte[] output = new byte[outH * outStride];

            bool rendered = false;
            if (RustEngineInterop.IsAvailable)
            {
                rendered = RustEngineInterop.RenderLiquidGlassV2(
                    bg, bgW, bgH, bgStride,
                    output, outW, outH, outStride,
                    marginX, marginY, radius,
                    bezel, bezelHeight, thickness, frostSigma, dispersion,
                    lightX, lightY,
                    tintColor.R, tintColor.G, tintColor.B, tintAlpha,
                    specularIntensity,
                    true);
            }

            if (!rendered)
            {
                RenderLiquidGlassManagedV2(
                    bg, bgW, bgH, bgStride,
                    output, outW, outH, outStride,
                    marginX, marginY, radius,
                    bezel, bezelHeight, thickness, frostSigma, dispersion,
                    lightX, lightY,
                    tintColor.R, tintColor.G, tintColor.B, tintAlpha,
                    specularIntensity);
            }

            var bmp = BitmapSource.Create(outW, outH, 96, 96, PixelFormats.Bgra32, null, output, outStride);
            bmp.Freeze();
            return bmp;
        }

        public static BitmapSource GetOrCreateLiquidGlassTexture(
            int w,
            int h,
            float radius,
            float bezel,
            float bezelHeight,
            float thickness,
            float frostSigma,
            float lightX,
            float lightY,
            Color tintColor,
            float tintAlpha,
            float specularIntensity)
        {
            w = Math.Max(16, Math.Min(1200, w));
            h = Math.Max(16, Math.Min(1200, h));

            string cacheKey = string.Format("{0}x{1}_r{2:F0}_b{3:F0}_bh{4:F0}_t{5:F0}_f{6:F1}_l{7:F2},{8:F2}_c{9}_{10:F2}_s{11:F2}",
                w, h, radius, bezel, bezelHeight, thickness, frostSigma, lightX, lightY, tintColor.ToString(), tintAlpha, specularIntensity);

            lock (_liquidGlassCache)
            {
                BitmapSource cached;
                if (_liquidGlassCache.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }
            }

            int stride = w * 4;
            byte[] bg = new byte[h * stride];
            byte[] output = new byte[h * stride];

            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float hw = w * 0.5f;
            float hh = h * 0.5f;

            // Generate ambient backdrop for the test/legacy caller
            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                float fy = (float)y / Math.Max(1, h);
                for (int x = 0; x < w; x++)
                {
                    float fx = (float)x / Math.Max(1, w);
                    float diag = (fx + fy) * 0.5f;
                    float softbox = (float)Math.Exp(-Math.Pow((fx - 0.35f) * 2.5f, 2.0) - Math.Pow((fy - 0.35f) * 2.5f, 2.0));

                    byte baseR = (byte)Math.Max(0, Math.Min(255, (int)(22.0f + 25.0f * diag + 85.0f * softbox)));
                    byte baseG = (byte)Math.Max(0, Math.Min(255, (int)(28.0f + 32.0f * diag + 105.0f * softbox)));
                    byte baseB = (byte)Math.Max(0, Math.Min(255, (int)(44.0f + 50.0f * diag + 140.0f * softbox)));

                    int idx = row + x * 4;
                    bg[idx] = baseB;     // B
                    bg[idx + 1] = baseG; // G
                    bg[idx + 2] = baseR; // R
                    bg[idx + 3] = 255;   // A
                }
            }

            bool rendered = false;
            if (RustEngineInterop.IsAvailable)
            {
                rendered = RustEngineInterop.RenderLiquidGlass(
                    bg, w, h, stride, output,
                    cx, cy, hw, hh, radius,
                    bezel, bezelHeight, thickness, frostSigma,
                    lightX, lightY,
                    tintColor.R, tintColor.G, tintColor.B, tintAlpha,
                    specularIntensity,
                    true);
            }

            if (!rendered)
            {
                RenderLiquidGlassManaged(
                    bg, w, h, stride, output,
                    cx, cy, hw, hh, radius,
                    bezel, bezelHeight, thickness, frostSigma,
                    lightX, lightY,
                    tintColor.R, tintColor.G, tintColor.B, tintAlpha,
                    specularIntensity);
            }

            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, output, stride);
            bmp.Freeze();

            lock (_liquidGlassCache)
            {
                if (_liquidGlassCache.Count > 60)
                {
                    _liquidGlassCache.Clear();
                }
                _liquidGlassCache[cacheKey] = bmp;
            }

            return bmp;
        }

        private static BitmapSource GetNoiseTexture()
        {
            if (_proceduralNoise != null) return _proceduralNoise;
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
            _proceduralNoise = bmp;
            return _proceduralNoise;
        }

        private static void CollectPriorLayers(IList<LayerNode> layers, LayerNode target, List<LayerNode> collected, ref bool found)
        {
            if (layers == null || found) return;
            foreach (var l in layers)
            {
                if (l == target)
                {
                    found = true;
                    return;
                }
                collected.Add(l);
                if (l.Children != null && l.Children.Count > 0)
                {
                    CollectPriorLayers(l.Children, target, collected, ref found);
                    if (found) return;
                }
            }
        }

        private static void RenderNodeWithTransform(LayerNode node, DrawingContext dc)
        {
            if (node == null || !node.IsVisible || node.Opacity <= 0.0) return;

            bool pushedOpacity = false;
            if (node.Opacity < 1.0)
            {
                dc.PushOpacity(node.Opacity);
                pushedOpacity = true;
            }

            bool pushedTransform = false;
            var matrix = node.GetTransformMatrix();
            if (!matrix.IsIdentity)
            {
                dc.PushTransform(new MatrixTransform(matrix));
                pushedTransform = true;
            }

            _defaultInstance.RenderNodeContent(node, dc, 1.0);

            if (pushedTransform) dc.Pop();
            if (pushedOpacity) dc.Pop();
        }

        public static byte[] CaptureBackdrop(
            LayerNode node,
            Rect bounds,
            int margin,
            out int bgW,
            out int bgH,
            out int bgStride)
        {
            bgW = (int)Math.Max(32, Math.Round(bounds.Width + margin * 2));
            bgH = (int)Math.Max(32, Math.Round(bounds.Height + margin * 2));
            bgStride = bgW * 4;
            byte[] bgPixels = new byte[bgH * bgStride];

            double bgWorldX = bounds.X - margin;
            double bgWorldY = bounds.Y - margin;
            Rect bgWorldRect = new Rect(bgWorldX, bgWorldY, bgW, bgH);

            var page = ActivePage;
            string bgHex = (page != null && !string.IsNullOrEmpty(page.BackgroundColor)) ? page.BackgroundColor : "#141722";
            Color bgColor = ParseHexColor(bgHex, 1.0);

            // Collect prior layers situated beneath this layer that intersect the backdrop bounds
            var priorLayers = new List<LayerNode>();
            if (page != null && page.Layers != null)
            {
                bool found = false;
                var allPrior = new List<LayerNode>();
                CollectPriorLayers(page.Layers, node, allPrior, ref found);
                foreach (var pl in allPrior)
                {
                    if (pl.IsVisible && pl.Opacity > 0.0)
                    {
                        var plBounds = pl.GetBounds();
                        if (plBounds.IntersectsWith(bgWorldRect))
                        {
                            priorLayers.Add(pl);
                        }
                    }
                }
            }

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                // 1. Canvas background
                var bgBrush = new SolidColorBrush(bgColor);
                bgBrush.Freeze();
                dc.DrawRectangle(bgBrush, null, new Rect(0, 0, bgW, bgH));

                // 2. Canvas grid dots
                if (page != null && page.ShowGrid)
                {
                    double step = 32.0;
                    double startX = (step - (bgWorldX % step)) % step;
                    double startY = (step - (bgWorldY % step)) % step;
                    var dotBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));
                    dotBrush.Freeze();

                    for (double gx = startX; gx < bgW; gx += step)
                    {
                        for (double gy = startY; gy < bgH; gy += step)
                        {
                            dc.DrawRectangle(dotBrush, null, new Rect(gx, gy, 2.0, 2.0));
                        }
                    }
                }

                // 3. Render prior layers situated below the glass
                if (priorLayers.Count > 0)
                {
                    dc.PushTransform(new TranslateTransform(-bgWorldX, -bgWorldY));
                    foreach (var pl in priorLayers)
                    {
                        RenderNodeWithTransform(pl, dc);
                    }
                    dc.Pop();
                }
            }

            try
            {
                var rtb = new RenderTargetBitmap(bgW, bgH, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                rtb.CopyPixels(bgPixels, bgStride, 0);
            }
            catch
            {
                // Fallback fill with canvas background color
                for (int y = 0; y < bgH; y++)
                {
                    int row = y * bgStride;
                    for (int x = 0; x < bgW; x++)
                    {
                        int idx = row + x * 4;
                        bgPixels[idx] = bgColor.B;
                        bgPixels[idx + 1] = bgColor.G;
                        bgPixels[idx + 2] = bgColor.R;
                        bgPixels[idx + 3] = 255;
                    }
                }
            }

            return bgPixels;
        }

        public static void RenderAppleLiquidGlass(LayerNode node, DrawingContext dc, Geometry geo, Rect bounds, EffectData glass)
        {
            if (glass == null || !glass.Enabled || glass.Intensity <= 0.0) return;
            if (geo == null || bounds.Width <= 0 || bounds.Height <= 0) return;

            double intensity = Math.Max(0.05, Math.Min(1.0, glass.Intensity));
            double lightAngleDeg = glass.Direction > 0 ? glass.Direction : 315.0; // Default 315° = top-left studio light
            double rad = lightAngleDeg * (Math.PI / 180.0);
            double lightCos = Math.Cos(rad);
            double lightSin = Math.Sin(rad);

            // Dynamic normalized light vector
            Point lightStart = new Point(
                Math.Max(0.0, Math.Min(1.0, 0.5 - lightCos * 0.5)),
                Math.Max(0.0, Math.Min(1.0, 0.5 - lightSin * 0.5)));
            Point lightEnd = new Point(
                Math.Max(0.0, Math.Min(1.0, 0.5 + lightCos * 0.5)),
                Math.Max(0.0, Math.Min(1.0, 0.5 + lightSin * 0.5)));

            // 1. Apple Physical Contact & Occlusion Depth Shadow (elevates glass slab above canvas)
            double shadowOffsetDist = Math.Max(2.0, Math.Min(16.0, (glass.Spread > 0 ? glass.Spread : 2.5) * 1.5));
            double shadowX = lightCos * shadowOffsetDist;
            double shadowY = lightSin * shadowOffsetDist;

            var shadowTrans = new TranslateTransform(shadowX, shadowY);
            var shadowGeo = geo.Clone();
            shadowGeo.Transform = shadowTrans;
            var shadowBrush = new SolidColorBrush(Color.FromArgb((byte)(38 * intensity), 0, 0, 0));
            shadowBrush.Freeze();
            dc.DrawGeometry(shadowBrush, null, shadowGeo);

            // 2. Push Layer Geometry Clip for Internal Volumetric Optical Passes
            dc.PushClip(geo);

            Color tintColor = Colors.White;
            if (!string.IsNullOrEmpty(glass.ColorHex))
            {
                try { tintColor = ParseHexColor(glass.ColorHex, 1.0); } catch { }
            }
            double bodyAlpha = Math.Max(0.0, Math.Min(1.0, glass.Opacity));

            // 2a. Real Optical Liquid Glass Refraction & Chromatic Dispersion Texture
            float radius = (float)Math.Min(bounds.Width * 0.5, Math.Min(bounds.Height * 0.5, node.CornerRadius > 0 ? node.CornerRadius : bounds.Height * 0.5));
            float bezel = (float)Math.Min(bounds.Width * 0.5, Math.Min(bounds.Height * 0.5, glass.Spread > 0 ? glass.Spread * 15.0 : 40.0));
            float bezelHeight = (float)(32.0 * (glass.Scale > 0 ? glass.Scale : 1.0));
            float thickness = 60.0f;
            float frostSigma = (float)(glass.Blur > 0 ? glass.Blur * 0.4 : 6.0);
            float lightX = -(float)lightCos;
            float lightY = -(float)lightSin;

            int texW = (int)Math.Max(16, Math.Round(bounds.Width));
            int texH = (int)Math.Max(16, Math.Round(bounds.Height));
            int margin = (int)Math.Max(20, Math.Min(60, bezel));

            int bgW, bgH, bgStride;
            byte[] bg = CaptureBackdrop(node, bounds, margin, out bgW, out bgH, out bgStride);

            // Chromatic dispersion: glass.Distance represents chromatic dispersion % (0 to 100, default 60%)
            // 60% -> 0.08, giving vivid 6-10px spectral rainbow caustics
            float dispersionVal = (float)(glass.Distance > 0.0 ? glass.Distance * 0.0015 : 0.08);

            var glassBmp = RenderLiquidGlassBackdropTexture(
                bg, bgW, bgH, bgStride, margin, margin,
                texW, texH, radius, bezel, bezelHeight, thickness, frostSigma, dispersionVal,
                lightX, lightY, tintColor, (float)bodyAlpha, (float)intensity);

            if (glassBmp != null)
            {
                dc.DrawImage(glassBmp, bounds);
            }

            // 2b. Internal Ambient Radial Luminous Bloom (Center-Light Diffusion)
            double bloomAlpha = 28 * intensity * Math.Max(0.05, bodyAlpha);
            var bloomGrad = new RadialGradientBrush
            {
                Center = lightStart,
                GradientOrigin = lightStart,
                RadiusX = 0.85,
                RadiusY = 0.85
            };
            bloomGrad.GradientStops.Add(new GradientStop(Color.FromArgb((byte)bloomAlpha, 255, 255, 255), 0.0));
            bloomGrad.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
            bloomGrad.Freeze();
            dc.DrawRectangle(bloomGrad, null, bounds);

            // 2c. Signature Apple Curved Lens Specular Sheen (Convex Parabolic Highlight Arc)
            double lensHeightRatio = Math.Max(0.25, Math.Min(0.65, 0.42 * (glass.Scale > 0 ? glass.Scale : 1.0)));
            double lensH = Math.Max(2.0, bounds.Height * lensHeightRatio);
            double arcDepth = Math.Min(lensH * 0.35, bounds.Height * 0.15);

            var lensStream = new StreamGeometry();
            using (var sgc = lensStream.Open())
            {
                sgc.BeginFigure(new Point(bounds.Left, bounds.Top), true, true);
                sgc.LineTo(new Point(bounds.Right, bounds.Top), true, false);
                sgc.LineTo(new Point(bounds.Right, bounds.Top + lensH - arcDepth), true, false);
                sgc.QuadraticBezierTo(
                    new Point(bounds.Left + bounds.Width * 0.5, bounds.Top + lensH + arcDepth),
                    new Point(bounds.Left, bounds.Top + lensH - arcDepth),
                    true, false);
            }
            lensStream.Freeze();

            var lensSpecularBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            lensSpecularBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(130 * intensity), 255, 255, 255), 0.0));
            lensSpecularBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(35 * intensity), 255, 255, 255), 0.45));
            lensSpecularBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
            lensSpecularBrush.Freeze();
            dc.DrawGeometry(lensSpecularBrush, null, lensStream);

            // 2d. Studio Softbox Apex Glint Line
            var glintPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(180 * intensity), 255, 255, 255)), 1.2);
            glintPen.Freeze();
            dc.DrawLine(glintPen, new Point(bounds.Left + bounds.Width * 0.15, bounds.Top + 1.2), new Point(bounds.Right - bounds.Width * 0.15, bounds.Top + 1.2));

            // 2e. Caustic Ground Bounce Light
            double bounceH = Math.Max(1.0, bounds.Height * 0.20);
            var bounceGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            bounceGrad.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.0));
            bounceGrad.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(60 * intensity), 255, 255, 255), 1.0));
            bounceGrad.Freeze();
            dc.DrawRectangle(bounceGrad, null, new Rect(bounds.Left, bounds.Bottom - bounceH, bounds.Width, bounceH));

            // Pop internal clip
            dc.Pop();

            // 3. Apple Multi-Stop Luminous Caustic Fresnel Rim (Luminous Outer Edge)
            double rimThickness = Math.Max(1.2, Math.Min(12.0, glass.Spread > 0 ? glass.Spread : 2.2));
            var rimGrad = new LinearGradientBrush
            {
                StartPoint = lightStart,
                EndPoint = lightEnd
            };
            rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(220 * intensity), 255, 255, 255), 0.0));
            rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(60 * intensity), 255, 255, 255), 0.25));
            rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(15 * intensity), 255, 255, 255), 0.55));
            rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(95 * intensity), 255, 255, 255), 0.85));
            rimGrad.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(65 * intensity), 255, 255, 255), 1.0));
            rimGrad.Freeze();

            var rimPen = new Pen(rimGrad, rimThickness);
            rimPen.Freeze();
            dc.DrawGeometry(null, rimPen, geo);

            // 4. Razor Subpixel Micro-Facet
            var microPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(160 * intensity), 255, 255, 255)), 0.8);
            microPen.Freeze();
            dc.DrawGeometry(null, microPen, geo);
        }

        private void RenderLiquidGlassPass(LayerNode node, DrawingContext dc)
        {
            if (node.Effects == null || node.Effects.Count == 0) return;
            var glass = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.LiquidGlass);
            if (glass == null || glass.Intensity <= 0.0) return;

            var geo = BuildGeometry(node);
            if (geo == null) return;

            var bounds = new Rect(node.X, node.Y, Math.Max(1, node.Width), Math.Max(1, node.Height));
            RenderAppleLiquidGlass(node, dc, geo, bounds, glass);
        }

        private void RenderGrainPass(LayerNode node, DrawingContext dc)
        {
            if (node.Effects == null || node.Effects.Count == 0) return;
            var grain = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.Grain);
            if (grain == null || grain.Intensity <= 0.0) return;

            var geo = BuildGeometry(node);
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

            bool hasLiquidGlass = node.Effects != null && node.Effects.Any(e => e.Enabled && e.Type == EffectType.LiquidGlass);

            // 1. Multiple Fills (drawn in order)
            if (node.Fills != null)
            {
                foreach (var fill in node.Fills)
                {
                    if (fill != null && fill.Enabled)
                    {
                        if (hasLiquidGlass && fill.Type == PaintType.Solid)
                        {
                            // Skip solid opaque background fill so the glass panel is fully translucent / transparent
                            continue;
                        }
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

        public static Color InterpolateColor(Color c1, Color c2, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            byte a = (byte)(c1.A + (c2.A - c1.A) * t);
            byte r = (byte)(c1.R + (c2.R - c1.R) * t);
            byte g = (byte)(c1.G + (c2.G - c1.G) * t);
            byte b = (byte)(c1.B + (c2.B - c1.B) * t);
            return Color.FromArgb(a, r, g, b);
        }

        public static Color SampleGradientColor(IList<GradientStopData> stops, double pos)
        {
            if (stops == null || stops.Count == 0) return Colors.White;
            var sorted = new List<GradientStopData>(stops);
            sorted.Sort((a, b) => a.Position.CompareTo(b.Position));
            if (pos <= sorted[0].Position) return ParseHexColor(sorted[0].ColorHex, sorted[0].Opacity);
            if (pos >= sorted[sorted.Count - 1].Position) return ParseHexColor(sorted[sorted.Count - 1].ColorHex, sorted[sorted.Count - 1].Opacity);

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var s1 = sorted[i];
                var s2 = sorted[i + 1];
                if (pos >= s1.Position && pos <= s2.Position)
                {
                    double range = s2.Position - s1.Position;
                    if (range <= 0.0001) return ParseHexColor(s1.ColorHex, s1.Opacity);
                    double relT = (pos - s1.Position) / range;

                    double mid = (s1.Midpoint > 0.01 && s1.Midpoint < 0.99) ? s1.Midpoint : 0.5;
                    double adjustedT = relT;
                    if (Math.Abs(mid - 0.5) > 0.01)
                    {
                        if (relT < mid)
                            adjustedT = 0.5 * (relT / mid);
                        else
                            adjustedT = 0.5 + 0.5 * ((relT - mid) / (1.0 - mid));
                    }

                    Color c1 = ParseHexColor(s1.ColorHex, s1.Opacity);
                    Color c2 = ParseHexColor(s2.ColorHex, s2.Opacity);
                    return InterpolateColor(c1, c2, adjustedT);
                }
            }
            return ParseHexColor(sorted[sorted.Count - 1].ColorHex, sorted[sorted.Count - 1].Opacity);
        }

        public static void PopulateGradientStops(GradientStopCollection targetStops, Paint paint)
        {
            if (targetStops == null || paint == null) return;
            if (paint.GradientStops == null || paint.GradientStops.Count == 0)
            {
                targetStops.Add(new GradientStop(Color.FromRgb(13, 153, 255), 0.0));
                targetStops.Add(new GradientStop(Color.FromRgb(162, 89, 255), 1.0));
                return;
            }

            var sorted = new List<GradientStopData>(paint.GradientStops);
            sorted.Sort((a, b) => a.Position.CompareTo(b.Position));

            for (int i = 0; i < sorted.Count; i++)
            {
                var curr = sorted[i];
                targetStops.Add(new GradientStop(
                    ParseHexColor(curr.ColorHex, curr.Opacity * paint.Opacity),
                    curr.Position));

                // Adobe Illustrator midpoint support: insert blend point when shifted from 50%
                if (i < sorted.Count - 1 && Math.Abs(curr.Midpoint - 0.5) > 0.01 && curr.Midpoint > 0.05 && curr.Midpoint < 0.95)
                {
                    var next = sorted[i + 1];
                    double midPos = curr.Position + curr.Midpoint * (next.Position - curr.Position);
                    Color c1 = ParseHexColor(curr.ColorHex, curr.Opacity * paint.Opacity);
                    Color c2 = ParseHexColor(next.ColorHex, next.Opacity * paint.Opacity);
                    Color midColor = InterpolateColor(c1, c2, 0.5);
                    targetStops.Add(new GradientStop(midColor, midPos));
                }
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

            Point ptStart = paint.GradientStart.HasValue ? paint.GradientStart.Value : new Point(x1, y1);
            Point ptEnd = paint.GradientEnd.HasValue ? paint.GradientEnd.Value : new Point(x2, y2);

            var lgb = new LinearGradientBrush
            {
                StartPoint = ptStart,
                EndPoint = ptEnd,
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };

            PopulateGradientStops(lgb.GradientStops, paint);

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

            PopulateGradientStops(rgb.GradientStops, paint);

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
