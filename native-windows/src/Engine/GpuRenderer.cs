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
        private readonly Dictionary<string, BitmapSource> _adjustedImageCache = new Dictionary<string, BitmapSource>();
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

            // Hardware clip visual and all child layers (Frame ClipContent and Rive-style Clipping Targets)
            Geometry combinedClip = null;
            if (node.Type == ElementType.Frame && node.ClipContent)
            {
                combinedClip = BuildGeometry(node);
            }

            if (node.ClippingTargets != null && node.ClippingTargets.Count > 0 && ActivePage != null)
            {
                for (int c = 0; c < node.ClippingTargets.Count; c++)
                {
                    var clipTarget = node.ClippingTargets[c];
                    if (clipTarget == null || !clipTarget.Enabled || string.IsNullOrEmpty(clipTarget.TargetLayerId)) continue;

                    var targetNode = ActivePage.FindLayerById(clipTarget.TargetLayerId);
                    if (targetNode == null) continue;

                    var targetGeo = BuildClipSourceGeometry(targetNode);
                    if (targetGeo != null)
                    {
                        Geometry effectiveTargetGeo = targetGeo;
                        if (clipTarget.IsInverse)
                        {
                            var bigBounds = new RectangleGeometry(new Rect(-50000, -50000, 100000, 100000));
                            var inv = new CombinedGeometry(GeometryCombineMode.Exclude, bigBounds, targetGeo);
                            inv.Freeze();
                            effectiveTargetGeo = inv;
                        }

                        // The clip is attached to this visual, so its own and ancestor
                        // animation transforms will be applied to the clip as well. Convert
                        // the mask from document space back into the owner's local space first.
                        effectiveTargetGeo = TransformClipToOwnerSpace(effectiveTargetGeo, node);

                        if (combinedClip == null)
                        {
                            combinedClip = effectiveTargetGeo;
                        }
                        else
                        {
                            var intersected = new CombinedGeometry(GeometryCombineMode.Intersect, combinedClip, effectiveTargetGeo);
                            intersected.Freeze();
                            combinedClip = intersected;
                        }
                    }
                }
            }

            if (combinedClip != null)
            {
                combinedClip.Freeze();
                visual.Clip = combinedClip;
            }
            else
            {
                visual.Clip = null;
            }

            // Set hardware transform if RenderTransform is active (transforms this visual and its child visual tree)
            if (node.RenderTransform != null && !node.RenderTransform.IsIdentity)
            {
                var rtMat = node.GetRenderTransformMatrix();
                visual.Transform = new MatrixTransform(rtMat);
            }
            else
            {
                visual.Transform = null;
            }

            visual.Opacity = Math.Max(0.0, Math.Min(1.0, node.Opacity));

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

                // Push Transform (Rotation, Scale) - note: RenderTransform is handled on visual.Transform
                bool pushedTransform = false;
                var matrix = node.GetStaticTransformMatrix();
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

                // Draw Drop Shadow in software when the visual effect is unavailable, and also
                // before layer blur so a layer can show both effects in the same pass.
                bool hasLayerBlur = node.Effects != null && node.Effects.Any(e => e != null && e.Enabled &&
                    (e.Type == EffectType.LayerBlur || e.Type == EffectType.Feather));
                if (!_isHardwareTier || hasLayerBlur)
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

            // Blur the rendered layer in either hardware or software WPF tiers. If a drop shadow
            // is also enabled, RenderSoftwareShadowPass draws it into the layer before blurring.
            var dropShadow = node.Effects.FirstOrDefault(e => e.Enabled && e.Type == EffectType.DropShadow);
            var blur = node.Effects.FirstOrDefault(e => e.Enabled && (e.Type == EffectType.LayerBlur || e.Type == EffectType.Feather));

            if (blur != null)
            {
                visual.Effect = new BlurEffect
                {
                    Radius = Math.Max(1.0, blur.Blur),
                    KernelType = KernelType.Gaussian,
                    RenderingBias = RenderingBias.Performance
                };
                return;
            }

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

        private const float GlassBevel = 1.0f;
        private const float GlassProfileExp = 3.5f;
        private const float GlassMaxBend = 0.999f;
        private const float GlassRimBend = 0.2f;
        private const float GlassDispersionSpread = 0.16f;
        private const int GlassDispersionSteps = 8;
        private const float GlassHlLo = 0.35f;
        private const float GlassHlSecondary = 0.6f;
        private const float GlassBroad = 0.05f;
        private const float GlassOneSampleSplitPx = 1.0f / 256.0f;

        private static float GlassLin(float a, float b, float x)
        {
            float span = Math.Abs(b - a);
            if (span < 1e-5f) span = 1e-5f;
            float t = (x - a) / span;
            return Math.Max(0.0f, Math.Min(1.0f, t));
        }

        private static float GlassSmoothstep(float a, float b, float x)
        {
            float span = Math.Abs(b - a);
            if (span < 1e-5f) span = 1e-5f;
            float t = Math.Max(0.0f, Math.Min(1.0f, (x - a) / span));
            return t * t * (3.0f - 2.0f * t);
        }

        private static float GlassRimLightAt(float x, float bevel)
        {
            return GlassSmoothstep(-bevel - 0.001f, -0.2f * bevel, -x);
        }

        private static float GlassCatches(float facing)
        {
            return GlassSmoothstep(GlassHlLo, 1.0f, facing) + GlassHlSecondary * GlassSmoothstep(GlassHlLo, 1.0f, -facing);
        }

        private static float GlassSheenAt(float x, float depth, float bevel)
        {
            float l = GlassLin(-depth, -bevel + 1e-4f, -x);
            return l * l;
        }

        private static float GlassBendAt(float x, float depth, float bevel, float refraction)
        {
            if (x >= Math.Max(1e-4f, depth) + bevel) return 0.0f;
            float sd = -x;
            float t = GlassLin(-depth - bevel, -bevel, sd);
            float most = GlassMaxBend * depth;
            float core = Math.Max(-most, Math.Min(most, (float)Math.Pow(t, GlassProfileExp) * depth * refraction));
            float rim = refraction * GlassRimBend * most;
            float u = GlassLin(-bevel - 1e-4f, -0.3f * bevel, sd);
            return core + (rim - core) * u;
        }

        private static float[] GlassSampleRgba(byte[] buf, int w, int h, int stride, float x, float y)
        {
            if (w <= 0 || h <= 0 || buf == null || buf.Length == 0) return new float[] { 0, 0, 0, 0 };
            x = x - 0.5f;
            y = y - 0.5f;
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            float tx = x - x0;
            float ty = y - y0;

            Func<int, int, float[]> px = (xi, yi) =>
            {
                if (xi < 0 || yi < 0 || xi >= w || yi >= h) return new float[] { 0, 0, 0, 0 };
                int idx = yi * stride + xi * 4;
                if (idx + 3 < buf.Length)
                {
                    // Bgra
                    return new float[] { buf[idx + 2], buf[idx + 1], buf[idx], buf[idx + 3] };
                }
                return new float[] { 0, 0, 0, 0 };
            };

            float[] a = px(x0, y0);
            float[] b = px(x0 + 1, y0);
            float[] c = px(x0, y0 + 1);
            float[] d = px(x0 + 1, y0 + 1);

            float[] outVal = new float[4];
            for (int k = 0; k < 4; k++)
            {
                float top = a[k] + (b[k] - a[k]) * tx;
                float bot = c[k] + (d[k] - c[k]) * tx;
                outVal[k] = top + (bot - top) * ty;
            }
            return outVal;
        }

        private static float[] GlassDispersed(byte[] buf, int w, int h, int stride, float x, float y, float dx, float dy, float k)
        {
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (k < 0.001f || len * k < GlassOneSampleSplitPx)
            {
                float[] s = GlassSampleRgba(buf, w, h, stride, x + dx, y + dy);
                return new float[] { Math.Min(s[0], s[3]), Math.Min(s[1], s[3]), Math.Min(s[2], s[3]), s[3] };
            }
            float last = GlassDispersionSteps - 1.0f;
            float[] acc = new float[4];
            float sum = 0.0f;
            for (int i = 1; i < GlassDispersionSteps - 1; i++)
            {
                float t = i / last - 0.5f;
                float weight = 1.0f - Math.Abs(2.0f * t);
                float[] r = GlassSampleRgba(buf, w, h, stride, x + dx * (1.0f + (t + 0.5f) * k), y + dy * (1.0f + (t + 0.5f) * k));
                float[] g = GlassSampleRgba(buf, w, h, stride, x + dx * (1.0f + t * k), y + dy * (1.0f + t * k));
                float[] b = GlassSampleRgba(buf, w, h, stride, x + dx * (1.0f + (t - 0.5f) * k), y + dy * (1.0f + (t - 0.5f) * k));
                acc[0] += r[0] * weight;
                acc[1] += g[1] * weight;
                acc[2] += b[2] * weight;
                acc[3] += Math.Max(r[3], Math.Max(g[3], b[3])) * weight;
                sum += weight;
            }
            float alpha = acc[3] / Math.Max(1e-5f, sum);
            return new float[] { Math.Min(acc[0] / sum, alpha), Math.Min(acc[1] / sum, alpha), Math.Min(acc[2] / sum, alpha), alpha };
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
            float depth = bezel > 0.0f ? bezel : 20.0f;
            float refraction = bezelHeight > 0.0f && bezelHeight <= 1.0f ? bezelHeight : (bezelHeight > 1.0f ? Math.Min(1.0f, bezelHeight / 100.0f) : 0.8f);
            float splay = Math.Max(0.0f, Math.Min(1.0f, thickness));
            float disp = dispersion > 0.0f ? Math.Min(1.0f, dispersion) : 0.5f;
            float intensity = specularIntensity > 0.0f ? Math.Min(1.0f, specularIntensity) : 0.8f;
            float kDisp = disp * GlassDispersionSpread;

            float lightLen = (float)Math.Sqrt(lightX * lightX + lightY * lightY);
            if (lightLen < 1e-5f) lightLen = 1.0f;
            float lx = lightX / lightLen;
            float ly = lightY / lightLen;

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

                    float distIn = -d;

                    // Outward 2D normal from SDF gradient
                    float e = 0.5f;
                    float gx = SdfSquircle(fx + e, fy, cx, cy, hw, hh, radius) - SdfSquircle(fx - e, fy, cx, cy, hw, hh, radius);
                    float gy = SdfSquircle(fx, fy + e, cx, cy, hw, hh, radius) - SdfSquircle(fx, fy - e, cx, cy, hw, hh, radius);
                    float gl = (float)Math.Sqrt(gx * gx + gy * gy);
                    if (gl < 1e-6f) gl = 1e-6f;
                    float n2x = gx / gl;
                    float n2y = gy / gl;
                    float inwardX = -n2x;
                    float inwardY = -n2y;

                    // Center-relative direction for splay
                    float px = fx - cx;
                    float py = fy - cy;
                    float plen = (float)Math.Sqrt(px * px + py * py);
                    float awayX = plen > 1e-6f ? px / plen : 0.0f;
                    float awayY = plen > 1e-6f ? py / plen : 0.0f;

                    // Bend direction
                    float outDirX = -inwardX * (1.0f - splay) + awayX * splay;
                    float outDirY = -inwardY * (1.0f - splay) + awayY * splay;

                    // FigCraft bend displacement
                    float bend = GlassBendAt(distIn, depth, GlassBevel, refraction);
                    float moveX = -outDirX * bend;
                    float moveY = -outDirY * bend;
                    float k = bend == 0.0f ? 0.0f : kDisp;

                    float[] baseRgba = GlassDispersed(bg, bgW, bgH, bgStride, bgFx, bgFy, moveX, moveY, k);

                    // FigCraft Rim light and Sheen
                    float facing = -(inwardX * lx + inwardY * ly);
                    float rim = GlassRimLightAt(distIn, GlassBevel) * GlassCatches(facing);
                    float sheen = -(outDirX * lx + outDirY * ly) * GlassSheenAt(distIn, depth, GlassBevel) * GlassBroad;
                    float add = 255.0f * intensity * (rim + sheen);

                    // Tint composite
                    if (tintAlpha > 0.0f)
                    {
                        baseRgba[0] = baseRgba[0] * (1.0f - tintAlpha) + tintR * tintAlpha;
                        baseRgba[1] = baseRgba[1] * (1.0f - tintAlpha) + tintG * tintAlpha;
                        baseRgba[2] = baseRgba[2] * (1.0f - tintAlpha) + tintB * tintAlpha;
                    }

                    // Direct background sample for anti-aliasing
                    float[] directBg = GlassSampleRgba(bg, bgW, bgH, bgStride, bgFx, bgFy);
                    float finalR = (directBg[0] * (1.0f - coverage) + Math.Max(0.0f, Math.Min(255.0f, baseRgba[0] + add)) * coverage);
                    float finalG = (directBg[1] * (1.0f - coverage) + Math.Max(0.0f, Math.Min(255.0f, baseRgba[1] + add)) * coverage);
                    float finalB = (directBg[2] * (1.0f - coverage) + Math.Max(0.0f, Math.Min(255.0f, baseRgba[2] + add)) * coverage);

                    // Output in BGRA order
                    output[outIdx] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(finalB)));
                    output[outIdx + 1] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(finalG)));
                    output[outIdx + 2] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(finalR)));
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
            if (glass == null || !glass.Enabled) return;
            if (geo == null || bounds.Width <= 0 || bounds.Height <= 0) return;

            glass.EnsureGlassDefaults();

            float intensity = (float)Math.Max(0.0, Math.Min(1.0, glass.LightIntensity));
            float refraction = (float)Math.Max(0.0, Math.Min(1.0, glass.Refraction));
            float depth = (float)Math.Max(1.0, Math.Min(1000.0, glass.Depth));
            float dispersion = (float)Math.Max(0.0, Math.Min(1.0, glass.Dispersion));
            float frost = (float)Math.Max(0.0, Math.Min(100.0, glass.Frost));
            float splay = (float)Math.Max(0.0, Math.Min(1.0, glass.Splay));

            double lightAngleDeg = glass.LightAngle; // -180 to 180 degrees clockwise from top
            double rad = lightAngleDeg * (Math.PI / 180.0);
            float lightX = (float)Math.Sin(rad);
            float lightY = -(float)Math.Cos(rad);

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
            double shadowOffsetDist = Math.Max(2.0, Math.Min(16.0, depth * 0.25));
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

            int texW = (int)Math.Max(16, Math.Round(bounds.Width));
            int texH = (int)Math.Max(16, Math.Round(bounds.Height));
            int margin = (int)Math.Max(20, Math.Min(120, depth + 10.0f));

            int bgW, bgH, bgStride;
            byte[] bg = CaptureBackdrop(node, bounds, margin, out bgW, out bgH, out bgStride);

            var glassBmp = RenderLiquidGlassBackdropTexture(
                bg, bgW, bgH, bgStride, margin, margin,
                texW, texH, radius, depth, refraction, splay, frost, dispersion,
                lightX, lightY, tintColor, (float)bodyAlpha, intensity);

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

                case ElementType.Group:
                    // Groups are pure organizational containers and do not render background geometry
                    break;

                case ElementType.Frame:
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
                case ElementType.ComponentInstance:
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
                    return ApplyCornerRadius(BuildStarGeometry(x, y, w, h, 5, 0.45), node.CornerRadius);

                case ElementType.Polygon:
                    return ApplyCornerRadius(BuildPolygonGeometry(x, y, w, h, 6), node.CornerRadius);

                case ElementType.Line:
                    return new LineGeometry(new Point(x, y), new Point(x + w, y + h));

                case ElementType.Arrow:
                    return ApplyCornerRadius(BuildArrowGeometry(x, y, w, h), node.CornerRadius);

                case ElementType.Path:
                    return ApplyCornerRadius(BuildBezierPathGeometry(node), node.CornerRadius);

                case ElementType.Text:
                    return new RectangleGeometry(new Rect(x, y, w, h));

                default:
                    return new RectangleGeometry(new Rect(x, y, w, h));
            }
        }

        public Geometry BuildClipSourceGeometry(LayerNode node, bool includeAncestorStaticTransforms = false)
        {
            if (node == null) return null;
            if (node.Type != ElementType.Group)
            {
                Geometry shape = BuildGeometry(node);
                if (shape == null) return null;
                Matrix transform = GetLayerWorldTransformMatrix(node, ActivePage, includeAncestorStaticTransforms);
                if (transform.IsIdentity) return shape;
                return ApplyGeometryTransform(shape, transform);
            }

            var combined = new GeometryGroup { FillRule = FillRule.Nonzero };
            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    LayerNode child = node.Children[i];
                    if (child == null || !child.IsVisible || child.Type == ElementType.Frame || child.Type == ElementType.Slice) continue;
                    Geometry childGeo = BuildClipSourceGeometry(child, includeAncestorStaticTransforms);
                    if (childGeo != null) combined.Children.Add(childGeo);
                }
            }

            // Descendant source geometries already include this group's transform chain.
            return combined;
        }

        public static Matrix GetLayerWorldRenderTransformMatrix(LayerNode layer, Page page)
        {
            if (layer == null) return Matrix.Identity;
            Matrix transform = layer.GetRenderTransformMatrix();
            var ancestors = GetLayerAncestorChain(page, layer);
            for (int i = ancestors.Count - 1; i >= 0; i--)
                transform.Append(ancestors[i].GetRenderTransformMatrix());
            return transform;
        }

        public static Matrix GetLayerWorldTransformMatrix(LayerNode layer, Page page, bool includeAncestorStaticTransforms)
        {
            if (layer == null) return Matrix.Identity;
            Matrix transform = layer.GetTransformMatrix();
            var ancestors = GetLayerAncestorChain(page, layer);
            for (int i = ancestors.Count - 1; i >= 0; i--)
                transform.Append(includeAncestorStaticTransforms
                    ? ancestors[i].GetTransformMatrix()
                    : ancestors[i].GetRenderTransformMatrix());
            return transform;
        }

        private static List<LayerNode> GetLayerAncestorChain(Page page, LayerNode target)
        {
            var path = new List<LayerNode>();
            if (page == null || target == null) return path;
            var visited = new HashSet<LayerNode>();
            if (!TryFindLayerPath(page.Layers, target, path, visited)) return new List<LayerNode>();
            path.RemoveAt(path.Count - 1);
            return path;
        }

        private static bool TryFindLayerPath(IEnumerable<LayerNode> layers, LayerNode target, List<LayerNode> path, HashSet<LayerNode> visited)
        {
            if (layers == null) return false;
            foreach (var layer in layers)
            {
                if (layer == null || !visited.Add(layer)) continue;
                path.Add(layer);
                if (ReferenceEquals(layer, target)) return true;
                if (TryFindLayerPath(layer.Children, target, path, visited)) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        private static Geometry TransformClipToOwnerSpace(Geometry geometry, LayerNode owner)
        {
            if (geometry == null || owner == null) return geometry;
            Matrix ownerTransform = GetLayerWorldRenderTransformMatrix(owner, ActivePage);
            if (ownerTransform.IsIdentity || !ownerTransform.HasInverse) return geometry;
            ownerTransform.Invert();
            return ApplyGeometryTransform(geometry, ownerTransform);
        }

        public static Geometry ApplyGeometryTransform(Geometry geometry, Matrix transform)
        {
            if (geometry == null || transform.IsIdentity) return geometry;
            Geometry result = geometry.Clone();
            Matrix combined = result.Transform != null ? result.Transform.Value : Matrix.Identity;
            if (combined.IsIdentity) combined = transform;
            else combined.Append(transform);
            result.Transform = new MatrixTransform(combined);
            result.Freeze();
            return result;
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

        private Geometry ApplyCornerRadius(Geometry geometry, double radius)
        {
            if (geometry == null || radius <= 0.01) return geometry;

            PathGeometry flattened;
            try
            {
                flattened = geometry.GetFlattenedPathGeometry(0.25, ToleranceType.Absolute);
            }
            catch
            {
                return geometry;
            }

            if (flattened == null || flattened.Figures == null || flattened.Figures.Count == 0)
                return geometry;

            var roundedGeometry = new StreamGeometry { FillRule = flattened.FillRule };
            bool changed = false;

            using (var ctx = roundedGeometry.Open())
            {
                foreach (PathFigure figure in flattened.Figures)
                {
                    var points = GetFlattenedPoints(figure);
                    if (figure.IsClosed && points.Count > 2 && DistanceSquared(points[0], points[points.Count - 1]) < 0.0001)
                        points.RemoveAt(points.Count - 1);

                    int minPoints = figure.IsClosed ? 3 : 2;
                    if (points.Count < minPoints)
                    {
                        if (points.Count > 0)
                        {
                            ctx.BeginFigure(points[0], figure.IsFilled, figure.IsClosed);
                            for (int i = 1; i < points.Count; i++) ctx.LineTo(points[i], true, false);
                        }
                        continue;
                    }

                    var incoming = new Point[points.Count];
                    var outgoing = new Point[points.Count];
                    var isRounded = new bool[points.Count];
                    for (int i = 0; i < points.Count; i++)
                    {
                        Point inPoint;
                        Point outPoint;
                        if (TryGetRoundedCorner(points, i, figure.IsClosed, radius, out inPoint, out outPoint))
                        {
                            incoming[i] = inPoint;
                            outgoing[i] = outPoint;
                            isRounded[i] = true;
                            changed = true;
                        }
                        else
                        {
                            incoming[i] = points[i];
                            outgoing[i] = points[i];
                        }
                    }

                    Point start = figure.IsClosed ? outgoing[0] : points[0];
                    ctx.BeginFigure(start, figure.IsFilled, figure.IsClosed);

                    int firstInterior = figure.IsClosed ? 1 : 1;
                    int lastInterior = figure.IsClosed ? points.Count - 1 : points.Count - 2;
                    for (int i = firstInterior; i <= lastInterior; i++)
                    {
                        if (isRounded[i])
                        {
                            ctx.LineTo(incoming[i], true, false);
                            AddQuadraticCorner(ctx, incoming[i], points[i], outgoing[i]);
                        }
                        else
                        {
                            ctx.LineTo(points[i], true, false);
                        }
                    }

                    if (figure.IsClosed)
                    {
                        if (isRounded[0])
                        {
                            ctx.LineTo(incoming[0], true, false);
                            AddQuadraticCorner(ctx, incoming[0], points[0], outgoing[0]);
                        }
                        else
                        {
                            ctx.LineTo(points[0], true, false);
                        }
                    }
                    else
                    {
                        ctx.LineTo(points[points.Count - 1], true, false);
                    }
                }
            }

            if (!changed) return geometry;
            roundedGeometry.Freeze();
            return roundedGeometry;
        }

        private static List<Point> GetFlattenedPoints(PathFigure figure)
        {
            var points = new List<Point> { figure.StartPoint };
            foreach (PathSegment segment in figure.Segments)
            {
                var line = segment as LineSegment;
                if (line != null)
                {
                    points.Add(line.Point);
                    continue;
                }

                var polyLine = segment as PolyLineSegment;
                if (polyLine != null)
                {
                    foreach (Point point in polyLine.Points) points.Add(point);
                }
            }
            return points;
        }

        private static bool TryGetRoundedCorner(List<Point> points, int index, bool isClosed, double radius, out Point incoming, out Point outgoing)
        {
            incoming = points[index];
            outgoing = points[index];
            if (!isClosed && (index == 0 || index == points.Count - 1)) return false;

            int previousIndex = (index == 0) ? points.Count - 1 : index - 1;
            int nextIndex = (index + 1) % points.Count;
            Point corner = points[index];
            double previousX = points[previousIndex].X - corner.X;
            double previousY = points[previousIndex].Y - corner.Y;
            double nextX = points[nextIndex].X - corner.X;
            double nextY = points[nextIndex].Y - corner.Y;
            double previousLength = Math.Sqrt(previousX * previousX + previousY * previousY);
            double nextLength = Math.Sqrt(nextX * nextX + nextY * nextY);
            if (previousLength < 0.001 || nextLength < 0.001) return false;

            double previousUnitX = previousX / previousLength;
            double previousUnitY = previousY / previousLength;
            double nextUnitX = nextX / nextLength;
            double nextUnitY = nextY / nextLength;
            double dot = Math.Max(-1.0, Math.Min(1.0, previousUnitX * nextUnitX + previousUnitY * nextUnitY));
            double interiorAngle = Math.Acos(dot);
            double turnAngle = Math.PI - interiorAngle;

            // Flattened Bezier curves contain many small joins. Only round distinct
            // corners so smooth curves remain visually smooth.
            if (turnAngle < 0.35 || turnAngle > Math.PI - 0.001) return false;

            double tangentDistance = radius * Math.Tan(turnAngle * 0.5);
            tangentDistance = Math.Min(tangentDistance, Math.Min(previousLength, nextLength) * 0.45);
            if (tangentDistance < 0.001) return false;

            incoming = new Point(corner.X + previousUnitX * tangentDistance, corner.Y + previousUnitY * tangentDistance);
            outgoing = new Point(corner.X + nextUnitX * tangentDistance, corner.Y + nextUnitY * tangentDistance);
            return true;
        }

        private static void AddQuadraticCorner(StreamGeometryContext ctx, Point start, Point control, Point end)
        {
            // Convert a quadratic Bezier through the original vertex into a cubic.
            var control1 = new Point(start.X + (control.X - start.X) * (2.0 / 3.0), start.Y + (control.Y - start.Y) * (2.0 / 3.0));
            var control2 = new Point(end.X + (control.X - end.X) * (2.0 / 3.0), end.Y + (control.Y - end.Y) * (2.0 / 3.0));
            ctx.BezierTo(control1, control2, end, true, false);
        }

        private static double DistanceSquared(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy;
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

                        bool prevHasHandle = prev.Type != BezierNodeType.Corner &&
                                             (prev.OutHandle.X != prev.Anchor.X || prev.OutHandle.Y != prev.Anchor.Y);
                        bool curHasHandle = cur.Type != BezierNodeType.Corner &&
                                            (cur.InHandle.X != cur.Anchor.X || cur.InHandle.Y != cur.Anchor.Y);

                        if (prevHasHandle || curHasHandle)
                        {
                            ctx.BezierTo(prevHasHandle ? prev.OutHandle : prev.Anchor,
                                curHasHandle ? cur.InHandle : cur.Anchor, cur.Anchor, true, false);
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
                        bool lastHasHandle = last.Type != BezierNodeType.Corner &&
                                             (last.OutHandle.X != last.Anchor.X || last.OutHandle.Y != last.Anchor.Y);
                        bool firstHasHandle = first.Type != BezierNodeType.Corner &&
                                              (first.InHandle.X != first.Anchor.X || first.InHandle.Y != first.Anchor.Y);

                        if (lastHasHandle || firstHasHandle)
                        {
                            ctx.BezierTo(lastHasHandle ? last.OutHandle : last.Anchor,
                                firstHasHandle ? first.InHandle : first.Anchor, first.Anchor, true, false);
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

            // 1. Multiple Fills (drawn from bottom to top so Fills[0] is rendered on top)
            if (node.Fills != null)
            {
                for (int i = node.Fills.Count - 1; i >= 0; i--)
                {
                    var fill = node.Fills[i];
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

            // 2. Multiple Strokes (drawn from bottom to top so Strokes[0] is rendered on top)
            if (node.Strokes != null)
            {
                for (int i = node.Strokes.Count - 1; i >= 0; i--)
                {
                    var stroke = node.Strokes[i];
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

                    StrokeEffectsEngine.RenderStroke(dc, geo, stroke, strokeBrush, strokePen);
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
                        for (int i = node.Fills.Count - 1; i >= 0; i--)
                        {
                            var fill = node.Fills[i];
                            if (fill != null && fill.Enabled)
                            {
                                var fillBrush = CreateBrushFromPaint(fill, new Rect(node.X, node.Y, node.Width, node.Height));
                                dc.DrawGeometry(fillBrush, null, glyphGeo);
                            }
                        }
                    }

                    if (node.Strokes != null)
                    {
                        for (int i = node.Strokes.Count - 1; i >= 0; i--)
                        {
                            var stroke = node.Strokes[i];
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

        public BitmapSource GetAdjustedBitmap(Paint paint)
        {
            if (paint == null || string.IsNullOrEmpty(paint.ImageRef)) return null;

            BitmapSource bmp;
            if (!_imageCache.TryGetValue(paint.ImageRef, out bmp))
            {
                if (File.Exists(paint.ImageRef))
                {
                    try
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(paint.ImageRef, UriKind.Absolute);
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        bmp = bi;
                        _imageCache[paint.ImageRef] = bmp;
                    }
                    catch
                    {
                        bmp = null;
                    }
                }
            }

            if (bmp == null) return null;

            bool hasCrop = paint.CropRect.HasValue &&
                (paint.CropRect.Value.X > 0.001 || paint.CropRect.Value.Y > 0.001 ||
                 paint.CropRect.Value.Width < 0.999 || paint.CropRect.Value.Height < 0.999);

            bool hasAdjust = Math.Abs(paint.Exposure) > 0.1 || Math.Abs(paint.Contrast) > 0.1 ||
                Math.Abs(paint.Saturation) > 0.1 || Math.Abs(paint.Temperature) > 0.1 ||
                Math.Abs(paint.Tint) > 0.1 || Math.Abs(paint.Highlights) > 0.1 || Math.Abs(paint.Shadows) > 0.1;

            if (!hasCrop && !hasAdjust)
            {
                return bmp;
            }

            double cx = (paint.CropRect.HasValue) ? paint.CropRect.Value.X : 0.0;
            double cy = (paint.CropRect.HasValue) ? paint.CropRect.Value.Y : 0.0;
            double cw = (paint.CropRect.HasValue) ? paint.CropRect.Value.Width : 1.0;
            double ch = (paint.CropRect.HasValue) ? paint.CropRect.Value.Height : 1.0;

            string key = string.Format(CultureInfo.InvariantCulture, "{0}_C{1:F3}_{2:F3}_{3:F3}_{4:F3}_E{5:F1}_C{6:F1}_S{7:F1}_T{8:F1}_Ti{9:F1}_H{10:F1}_Sh{11:F1}",
                paint.ImageRef, cx, cy, cw, ch,
                paint.Exposure, paint.Contrast, paint.Saturation, paint.Temperature, paint.Tint, paint.Highlights, paint.Shadows);

            BitmapSource cached;
            if (_adjustedImageCache.TryGetValue(key, out cached))
            {
                return cached;
            }

            BitmapSource current = bmp;
            if (hasCrop)
            {
                int px = (int)Math.Max(0, Math.Min(bmp.PixelWidth - 1, cx * bmp.PixelWidth));
                int py = (int)Math.Max(0, Math.Min(bmp.PixelHeight - 1, cy * bmp.PixelHeight));
                int pw = (int)Math.Max(1, Math.Min(bmp.PixelWidth - px, cw * bmp.PixelWidth));
                int ph = (int)Math.Max(1, Math.Min(bmp.PixelHeight - py, ch * bmp.PixelHeight));
                try
                {
                    var cb = new CroppedBitmap(bmp, new Int32Rect(px, py, pw, ph));
                    cb.Freeze();
                    current = cb;
                }
                catch { }
            }

            if (hasAdjust)
            {
                try
                {
                    var conv = new FormatConvertedBitmap(current, PixelFormats.Bgra32, null, 0);
                    int width = conv.PixelWidth;
                    int height = conv.PixelHeight;
                    int stride = width * 4;
                    byte[] pixels = new byte[stride * height];
                    conv.CopyPixels(pixels, stride, 0);

                    double exp = paint.Exposure / 100.0;
                    double cont = (paint.Contrast + 100.0) / 100.0;
                    double sat = (paint.Saturation + 100.0) / 100.0;
                    double temp = paint.Temperature / 100.0;
                    double tint = paint.Tint / 100.0;
                    double hi = paint.Highlights / 100.0;
                    double sh = paint.Shadows / 100.0;

                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        byte b = pixels[i];
                        byte g = pixels[i + 1];
                        byte r = pixels[i + 2];
                        byte a = pixels[i + 3];
                        if (a == 0) continue;

                        double rd = r * (1.0 + exp);
                        double gd = g * (1.0 + exp);
                        double bd = b * (1.0 + exp);

                        double lum = 0.299 * rd + 0.587 * gd + 0.114 * bd;
                        if (hi != 0 && lum > 128.0)
                        {
                            double factor = (lum - 128.0) / 128.0;
                            rd += hi * 35.0 * factor;
                            gd += hi * 35.0 * factor;
                            bd += hi * 35.0 * factor;
                        }
                        if (sh != 0 && lum < 128.0)
                        {
                            double factor = (128.0 - lum) / 128.0;
                            rd += sh * 35.0 * factor;
                            gd += sh * 35.0 * factor;
                            bd += sh * 35.0 * factor;
                        }

                        rd = ((rd - 128.0) * cont) + 128.0;
                        gd = ((gd - 128.0) * cont) + 128.0;
                        bd = ((bd - 128.0) * cont) + 128.0;

                        lum = 0.299 * rd + 0.587 * gd + 0.114 * bd;
                        rd = lum + (rd - lum) * sat;
                        gd = lum + (gd - lum) * sat;
                        bd = lum + (bd - lum) * sat;

                        rd += temp * 25.0 + tint * 15.0;
                        gd -= tint * 20.0;
                        bd -= temp * 25.0 - tint * 15.0;

                        pixels[i] = (byte)Math.Max(0, Math.Min(255, (int)bd));
                        pixels[i + 1] = (byte)Math.Max(0, Math.Min(255, (int)gd));
                        pixels[i + 2] = (byte)Math.Max(0, Math.Min(255, (int)rd));
                    }

                    var result = BitmapSource.Create(width, height, conv.DpiX, conv.DpiY, PixelFormats.Bgra32, null, pixels, stride);
                    result.Freeze();
                    current = result;
                }
                catch { }
            }

            _adjustedImageCache[key] = current;
            return current;
        }

        private void RenderImage(LayerNode node, DrawingContext dc)
        {
            var bounds = new Rect(node.X, node.Y, Math.Max(1, node.Width), Math.Max(1, node.Height));
            var paint = node.GetPrimaryImagePaint();

            if (paint != null && !string.IsNullOrEmpty(paint.ImageRef))
            {
                BitmapSource bmp = GetAdjustedBitmap(paint);

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
                    return CreateRadialGradientBrush(paint, bounds);

                case PaintType.Image:
                    var adj = GetAdjustedBitmap(paint);
                    if (adj != null)
                    {
                        var ib = new ImageBrush(adj);
                        ib.Opacity = paint.Opacity;
                        if (paint.ImageFillMode == "Fit") ib.Stretch = Stretch.Uniform;
                        else if (paint.ImageFillMode == "Crop") ib.Stretch = Stretch.UniformToFill;
                        else if (paint.ImageFillMode == "Tile") { ib.Stretch = Stretch.None; ib.TileMode = TileMode.Tile; }
                        else ib.Stretch = Stretch.Fill;
                        ib.Freeze();
                        return ib;
                    }
                    var fallbackImg = new SolidColorBrush(ParseHexColor(paint.ColorHex, paint.Opacity));
                    fallbackImg.Freeze();
                    return fallbackImg;

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
            return CreateRadialGradientBrush(paint, new Rect(0, 0, 100, 100));
        }

        private RadialGradientBrush CreateRadialGradientBrush(Paint paint, Rect bounds)
        {
            double w = Math.Max(1.0, bounds.Width);
            double h = Math.Max(1.0, bounds.Height);
            double minDim = Math.Min(w, h);

            double rx = (paint.GradientRadius * minDim) / w;
            double ry = (paint.GradientRadius * minDim) / h;

            var rgb = new RadialGradientBrush
            {
                Center = paint.GradientCenter,
                GradientOrigin = paint.GradientCenter,
                RadiusX = rx,
                RadiusY = ry,
                MappingMode = BrushMappingMode.RelativeToBoundingBox
            };

            if (Math.Abs(paint.GradientAngle) > 0.01)
            {
                rgb.RelativeTransform = new RotateTransform(paint.GradientAngle, paint.GradientCenter.X, paint.GradientCenter.Y);
            }

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
