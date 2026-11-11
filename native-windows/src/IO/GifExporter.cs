using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using Kerf.Engine;
using Kerf.Engine.Animation;
using Kerf.Model;

namespace Kerf.IO
{
    /// <summary>
    /// Configuration options for animated GIF export.
    /// Strictly adheres to C# 5.0 compatibility.
    /// </summary>
    public class GifExportOptions
    {
        public Clip Clip { get; set; }
        public double Fps { get; set; }
        public double Scale { get; set; }
        public int LoopCount { get; set; } // 0 = infinite loop
        public bool PreserveTransparency { get; set; }
        public int MaxColors { get; set; } // 32, 64, 128, 256
        public bool ApplyDithering { get; set; }
        public double StartTimeSeconds { get; set; }
        public double EndTimeSeconds { get; set; }
        public Action<double> ProgressCallback { get; set; }

        public GifExportOptions()
        {
            Fps = 15.0;
            Scale = 1.0;
            LoopCount = 0;
            PreserveTransparency = true;
            MaxColors = 256;
            ApplyDithering = true;
            StartTimeSeconds = 0.0;
            EndTimeSeconds = 0.0;
        }
    }

    /// <summary>
    /// High-performance, zero-dependency GIF89a animated stream encoder.
    /// Supports multi-frame animations, variable framerate/delays, Netscape 2.0 looping,
    /// alpha transparency, adaptive palette color quantization, and Floyd-Steinberg error diffusion dithering.
    /// Strictly adheres to C# 5.0 compatibility.
    /// </summary>
    public static class GifExporter
    {
        public static void ExportClipToGif(
            KerfDocument doc,
            Clip clip,
            string targetFilePath,
            double fps = 15.0,
            double scale = 1.0,
            int loopCount = 0,
            bool transparent = true,
            int maxColors = 256,
            bool dither = true,
            Action<double> progressCallback = null)
        {
            var options = new GifExportOptions
            {
                Clip = clip,
                Fps = fps,
                Scale = scale,
                LoopCount = loopCount,
                PreserveTransparency = transparent,
                MaxColors = maxColors,
                ApplyDithering = dither,
                StartTimeSeconds = 0.0,
                EndTimeSeconds = (clip != null) ? clip.Duration : 2.0,
                ProgressCallback = progressCallback
            };

            ExportClipToGif(doc, options, targetFilePath);
        }

        public static void ExportClipToGif(
            KerfDocument doc,
            GifExportOptions options,
            string targetFilePath)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (options == null) throw new ArgumentNullException("options");
            if (string.IsNullOrEmpty(targetFilePath)) throw new ArgumentNullException("targetFilePath");

            var page = doc.GetActivePage();
            if (page == null) throw new InvalidOperationException("Active page not found.");

            Clip clip = options.Clip;
            if (clip == null)
            {
                clip = new Clip(100, "Default Animation", 2.0, 30.0, LoopMode.Loop);
            }

            double fps = Math.Max(1.0, Math.Min(60.0, options.Fps));
            double startT = Math.Max(0.0, options.StartTimeSeconds);
            double endT = (options.EndTimeSeconds > startT) ? options.EndTimeSeconds : clip.Duration;
            if (endT <= startT) endT = startT + 1.0;
            double duration = endT - startT;

            int totalFrames = (int)Math.Ceiling(duration * fps);
            if (totalFrames < 1) totalFrames = 1;

            string fullTarget = Path.GetFullPath(targetFilePath);
            string dir = Path.GetDirectoryName(fullTarget);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Capture initial rest poses
            var restPoses = new Dictionary<LayerNode, RenderTransform>();
            if (page.Layers != null)
            {
                foreach (var l in page.Layers)
                {
                    if (l != null && l.RenderTransform != null)
                    {
                        restPoses[l] = l.RenderTransform.Clone();
                    }
                }
            }

            string tempGif = targetFilePath + ".tmp." + Guid.NewGuid().ToString("N") + ".gif";

            try
            {
                // Delay in hundredths of a second (centiseconds)
                ushort delayCentisecs = (ushort)Math.Max(1, (int)Math.Round(100.0 / fps));

                using (var fs = new FileStream(tempGif, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    // Render first frame to establish GIF canvas bounds
                    var firstPose = new Dictionary<uint, Dictionary<ushort, double>>();
                    clip.SampleToPose(startT, firstPose);
                    PropertyBinder.ApplyPoseToLayers(page.Layers, firstPose);

                    BitmapSource firstBmp = HighQualityImageRenderer.Render(page, (IEnumerable<LayerNode>)null, options.Scale, options.PreserveTransparency);
                    int width = firstBmp.PixelWidth;
                    int height = firstBmp.PixelHeight;

                    // 1. Header: "GIF89a"
                    bw.Write(Encoding.ASCII.GetBytes("GIF89a"));

                    // 2. Logical Screen Descriptor
                    bw.Write((ushort)width);
                    bw.Write((ushort)height);
                    bw.Write((byte)0x70); // No global color table, 8-bit color resolution
                    bw.Write((byte)0);    // Background color index
                    bw.Write((byte)0);    // Pixel aspect ratio

                    // 3. Netscape 2.0 Application Extension for looping
                    bw.Write((byte)0x21); // Extension Introducer
                    bw.Write((byte)0xFF); // Application Extension
                    bw.Write((byte)11);   // Block Size
                    bw.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                    bw.Write((byte)3);    // Sub-block length
                    bw.Write((byte)1);    // Sub-block ID
                    bw.Write((ushort)options.LoopCount); // 0 = loop forever
                    bw.Write((byte)0);    // Block terminator

                    // 4. Render and write frames
                    for (int f = 0; f < totalFrames; f++)
                    {
                        double t = startT + ((double)f / fps);
                        if (t > endT) t = endT;

                        var pose = new Dictionary<uint, Dictionary<ushort, double>>();
                        clip.SampleToPose(t, pose);
                        PropertyBinder.ApplyPoseToLayers(page.Layers, pose);

                        BitmapSource frameBmp = (f == 0)
                            ? firstBmp
                            : HighQualityImageRenderer.Render(page, (IEnumerable<LayerNode>)null, options.Scale, options.PreserveTransparency);

                        WriteGifFrame(bw, fs, frameBmp, delayCentisecs, options.MaxColors, options.PreserveTransparency, options.ApplyDithering);

                        if (options.ProgressCallback != null)
                        {
                            options.ProgressCallback((double)(f + 1) / totalFrames);
                        }
                    }

                    // 5. Trailer
                    bw.Write((byte)0x3B); // GIF file terminator ';'
                }

                if (File.Exists(targetFilePath)) File.Delete(targetFilePath);
                File.Move(tempGif, targetFilePath);
            }
            finally
            {
                if (File.Exists(tempGif))
                {
                    try { File.Delete(tempGif); } catch { }
                }

                // Restore rest poses
                if (page.Layers != null)
                {
                    foreach (var l in page.Layers)
                    {
                        if (l != null)
                        {
                            RenderTransform rt;
                            if (restPoses.TryGetValue(l, out rt))
                            {
                                l.RenderTransform = rt;
                            }
                            else
                            {
                                l.RenderTransform = new RenderTransform();
                            }
                        }
                    }
                }
            }
        }

        private static void WriteGifFrame(
            BinaryWriter bw,
            Stream outStream,
            BitmapSource bmp,
            ushort delayCentisecs,
            int maxColors,
            bool allowTransparency,
            bool dither)
        {
            int width = bmp.PixelWidth;
            int height = bmp.PixelHeight;
            int stride = width * 4;
            byte[] rawPixels = new byte[height * stride];
            bmp.CopyPixels(rawPixels, stride, 0);

            // 1. Build palette & index pixels
            byte[] palette;
            bool hasTransparentPixel;
            int transparentIndex;
            byte[] indexedPixels = QuantizeAndIndexPixels(
                rawPixels, width, height, maxColors, allowTransparency, dither,
                out palette, out hasTransparentPixel, out transparentIndex);

            // 2. Graphic Control Extension (GCE)
            bw.Write((byte)0x21); // Extension Introducer
            bw.Write((byte)0xF9); // Graphic Control Label
            bw.Write((byte)4);    // Byte count

            // Packed byte:
            // Bits 7-5: Reserved
            // Bits 4-2: Disposal Method (0x02 = Restore to background color / 0x08 in packed bits)
            // Bit 1: User Input Flag (0)
            // Bit 0: Transparent Color Flag
            byte packedGce = 0x08; // Disposal method: restore to background
            if (hasTransparentPixel)
            {
                packedGce |= 0x01; // Transparent color flag
            }
            bw.Write(packedGce);
            bw.Write(delayCentisecs);
            bw.Write((byte)(hasTransparentPixel ? transparentIndex : 0));
            bw.Write((byte)0);    // Block terminator

            // 3. Image Descriptor
            bw.Write((byte)0x2C); // Image Separator ','
            bw.Write((ushort)0);  // Left position
            bw.Write((ushort)0);  // Top position
            bw.Write((ushort)width);
            bw.Write((ushort)height);
            bw.Write((byte)0x87); // Local color table present, 256 colors (2^(7+1))

            // 4. Local Color Table (256 * 3 = 768 bytes)
            bw.Write(palette);

            // 5. LZW Compressed Raster Data
            var lzw = new GifLzwEncoder(indexedPixels, 8, outStream);
            lzw.Encode();
        }

        private static byte[] QuantizeAndIndexPixels(
            byte[] bgraPixels,
            int width,
            int height,
            int maxColors,
            bool allowTransparency,
            bool dither,
            out byte[] outPalette,
            out bool outHasTransparent,
            out int outTransparentIndex)
        {
            maxColors = Math.Max(16, Math.Min(256, maxColors));
            int totalPixels = width * height;

            // Palette table: 256 * 3 bytes (RGB)
            outPalette = new byte[768];
            outHasTransparent = false;
            outTransparentIndex = 0;

            // Step 1: Detect transparent pixels & collect color frequencies
            var colorFreq = new Dictionary<int, int>();
            int transparentCount = 0;

            for (int i = 0; i < totalPixels; i++)
            {
                int offset = i * 4;
                byte b = bgraPixels[offset];
                byte g = bgraPixels[offset + 1];
                byte r = bgraPixels[offset + 2];
                byte a = bgraPixels[offset + 3];

                if (allowTransparency && a < 128)
                {
                    transparentCount++;
                }
                else
                {
                    // 15-bit quantized color key for frequency counting
                    int key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
                    int count;
                    colorFreq[key] = colorFreq.TryGetValue(key, out count) ? count + 1 : 1;
                }
            }

            int availableColors = maxColors;
            int paletteStartIndex = 0;

            if (allowTransparency && transparentCount > 0)
            {
                outHasTransparent = true;
                outTransparentIndex = 0;
                paletteStartIndex = 1;
                availableColors--;

                // Set index 0 to neutral black (alpha masked)
                outPalette[0] = 0;
                outPalette[1] = 0;
                outPalette[2] = 0;
            }

            // Step 2: Sort colors by frequency and pick top palette entries
            var sortedList = new List<KeyValuePair<int, int>>(colorFreq);
            sortedList.Sort((a, b) => b.Value.CompareTo(a.Value));

            var paletteRgb = new List<byte[]>();
            int take = Math.Min(availableColors, sortedList.Count);
            for (int i = 0; i < take; i++)
            {
                int key = sortedList[i].Key;
                byte r = (byte)(((key >> 10) & 0x1F) << 3);
                byte g = (byte)(((key >> 5) & 0x1F) << 3);
                byte b = (byte)((key & 0x1F) << 3);
                paletteRgb.Add(new byte[] { r, g, b });
            }

            // If not enough unique colors, fill with standard gradient anchors
            if (paletteRgb.Count == 0)
            {
                paletteRgb.Add(new byte[] { 255, 255, 255 });
                paletteRgb.Add(new byte[] { 0, 0, 0 });
            }

            // Fill palette table
            for (int i = 0; i < paletteRgb.Count && (paletteStartIndex + i) < 256; i++)
            {
                int pIdx = (paletteStartIndex + i) * 3;
                outPalette[pIdx] = paletteRgb[i][0];     // R
                outPalette[pIdx + 1] = paletteRgb[i][1]; // G
                outPalette[pIdx + 2] = paletteRgb[i][2]; // B
            }

            // Step 3: Map pixels to palette indices
            byte[] indexed = new byte[totalPixels];

            if (dither && width > 1 && height > 1)
            {
                // Floyd-Steinberg error diffusion
                int[] errR = new int[totalPixels];
                int[] errG = new int[totalPixels];
                int[] errB = new int[totalPixels];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int p = y * width + x;
                        int offset = p * 4;
                        byte a = bgraPixels[offset + 3];

                        if (allowTransparency && a < 128)
                        {
                            indexed[p] = (byte)outTransparentIndex;
                            continue;
                        }

                        int b = bgraPixels[offset] + (errB[p] >> 4);
                        int g = bgraPixels[offset + 1] + (errG[p] >> 4);
                        int r = bgraPixels[offset + 2] + (errR[p] >> 4);

                        r = Math.Max(0, Math.Min(255, r));
                        g = Math.Max(0, Math.Min(255, g));
                        b = Math.Max(0, Math.Min(255, b));

                        // Find closest color
                        int bestIndex = 0;
                        int bestDist = int.MaxValue;
                        for (int c = 0; c < paletteRgb.Count; c++)
                        {
                            int pr = paletteRgb[c][0];
                            int pg = paletteRgb[c][1];
                            int pb = paletteRgb[c][2];
                            int dist = (r - pr) * (r - pr) * 3 + (g - pg) * (g - pg) * 4 + (b - pb) * (b - pb) * 2;
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestIndex = c;
                            }
                        }

                        indexed[p] = (byte)(paletteStartIndex + bestIndex);

                        // Error diffusion
                        int diffR = r - paletteRgb[bestIndex][0];
                        int diffG = g - paletteRgb[bestIndex][1];
                        int diffB = b - paletteRgb[bestIndex][2];

                        if (x + 1 < width)
                        {
                            errR[p + 1] += diffR * 7;
                            errG[p + 1] += diffG * 7;
                            errB[p + 1] += diffB * 7;
                        }
                        if (y + 1 < height)
                        {
                            if (x > 0)
                            {
                                errR[p + width - 1] += diffR * 3;
                                errG[p + width - 1] += diffG * 3;
                                errB[p + width - 1] += diffB * 3;
                            }
                            errR[p + width] += diffR * 5;
                            errG[p + width] += diffG * 5;
                            errB[p + width] += diffB * 5;
                            if (x + 1 < width)
                            {
                                errR[p + width + 1] += diffR * 1;
                                errG[p + width + 1] += diffG * 1;
                                errB[p + width + 1] += diffB * 1;
                            }
                        }
                    }
                }
            }
            else
            {
                // Nearest neighbor matching
                for (int i = 0; i < totalPixels; i++)
                {
                    int offset = i * 4;
                    byte b = bgraPixels[offset];
                    byte g = bgraPixels[offset + 1];
                    byte r = bgraPixels[offset + 2];
                    byte a = bgraPixels[offset + 3];

                    if (allowTransparency && a < 128)
                    {
                        indexed[i] = (byte)outTransparentIndex;
                        continue;
                    }

                    int bestIndex = 0;
                    int bestDist = int.MaxValue;
                    for (int c = 0; c < paletteRgb.Count; c++)
                    {
                        int pr = paletteRgb[c][0];
                        int pg = paletteRgb[c][1];
                        int pb = paletteRgb[c][2];
                        int dist = (r - pr) * (r - pr) * 3 + (g - pg) * (g - pg) * 4 + (b - pb) * (b - pb) * 2;
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestIndex = c;
                        }
                    }

                    indexed[i] = (byte)(paletteStartIndex + bestIndex);
                }
            }

            return indexed;
        }
    }

    /// <summary>
    /// Fast LZW compressor for standard GIF89a image blocks.
    /// Strictly adheres to C# 5.0 compatibility.
    /// </summary>
    internal class GifLzwEncoder
    {
        private readonly int _initCodeSize;
        private readonly byte[] _pixels;
        private readonly Stream _outStream;
        private int _curCodeSize;
        private int _clearCode;
        private int _eofCode;
        private int _nextCode;
        private int _bitAccum;
        private int _bitCount;
        private readonly byte[] _packet;
        private int _packetLen;

        public GifLzwEncoder(byte[] pixels, int initCodeSize, Stream outStream)
        {
            _pixels = pixels;
            _initCodeSize = initCodeSize;
            _outStream = outStream;
            _packet = new byte[256];
            _packetLen = 0;
        }

        public void Encode()
        {
            _clearCode = 1 << _initCodeSize;
            _eofCode = _clearCode + 1;
            _curCodeSize = _initCodeSize + 1;
            _nextCode = _clearCode + 2;
            _bitAccum = 0;
            _bitCount = 0;
            _packetLen = 0;

            _outStream.WriteByte((byte)_initCodeSize);

            int[] htable = new int[5003];
            int[] codetable = new int[5003];
            for (int i = 0; i < 5003; i++) htable[i] = -1;

            WriteCode(_clearCode);

            if (_pixels == null || _pixels.Length == 0)
            {
                WriteCode(_eofCode);
                FlushPacket();
                _outStream.WriteByte(0);
                return;
            }

            int ent = _pixels[0];
            for (int i = 1; i < _pixels.Length; i++)
            {
                int c = _pixels[i];
                int fcode = (c << 12) + ent;
                int h = (c << 4) ^ ent;
                if (h >= 5003) h -= 5003;

                bool found = false;
                if (htable[h] == fcode)
                {
                    ent = codetable[h];
                    continue;
                }
                else if (htable[h] >= 0)
                {
                    int disp = 5003 - h;
                    if (h == 0) disp = 1;
                    do
                    {
                        h -= disp;
                        if (h < 0) h += 5003;
                        if (htable[h] == fcode)
                        {
                            ent = codetable[h];
                            found = true;
                            break;
                        }
                    } while (htable[h] >= 0);
                }

                if (found) continue;

                WriteCode(ent);
                if (_nextCode < 4096)
                {
                    codetable[h] = _nextCode++;
                    htable[h] = fcode;
                    if (_nextCode > (1 << _curCodeSize) && _curCodeSize < 12)
                    {
                        _curCodeSize++;
                    }
                }
                else
                {
                    for (int j = 0; j < 5003; j++) htable[j] = -1;
                    WriteCode(_clearCode);
                    _curCodeSize = _initCodeSize + 1;
                    _nextCode = _clearCode + 2;
                }
                ent = c;
            }

            WriteCode(ent);
            WriteCode(_eofCode);
            if (_bitCount > 0)
            {
                _packet[_packetLen++] = (byte)(_bitAccum & 0xFF);
            }
            FlushPacket();
            _outStream.WriteByte(0); // Terminating block
        }

        private void WriteCode(int code)
        {
            _bitAccum |= (code << _bitCount);
            _bitCount += _curCodeSize;
            while (_bitCount >= 8)
            {
                _packet[_packetLen++] = (byte)(_bitAccum & 0xFF);
                if (_packetLen >= 254)
                {
                    FlushPacket();
                }
                _bitAccum >>= 8;
                _bitCount -= 8;
            }
        }

        private void FlushPacket()
        {
            if (_packetLen > 0)
            {
                _outStream.WriteByte((byte)_packetLen);
                _outStream.Write(_packet, 0, _packetLen);
                _packetLen = 0;
            }
        }
    }
}
