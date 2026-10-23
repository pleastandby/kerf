using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kerf.UI;

namespace Kerf.Tools
{
    public static class IconBuilder
    {
        [STAThread]
        public static void Main(string[] args)
        {
            string outPath = args.Length > 0 ? args[0] : "kerf.ico";
            int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // ICONDIR Header
                bw.Write((short)0); // reserved
                bw.Write((short)1); // type (1 = ICO)
                bw.Write((short)sizes.Length); // count

                byte[][] pngBytes = new byte[sizes.Length][];
                for (int i = 0; i < sizes.Length; i++)
                {
                    int sz = sizes[i];
                    var rtb = KerfLogo.RenderBitmap(sz, sz);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));
                    using (var pms = new MemoryStream())
                    {
                        encoder.Save(pms);
                        pngBytes[i] = pms.ToArray();
                    }
                }

                int offset = 6 + (16 * sizes.Length);
                for (int i = 0; i < sizes.Length; i++)
                {
                    int sz = sizes[i];
                    bw.Write((byte)(sz == 256 ? 0 : sz)); // width
                    bw.Write((byte)(sz == 256 ? 0 : sz)); // height
                    bw.Write((byte)0); // color count
                    bw.Write((byte)0); // reserved
                    bw.Write((short)1); // color planes
                    bw.Write((short)32); // bit count
                    bw.Write((int)pngBytes[i].Length); // bytes in resource
                    bw.Write((int)offset); // image offset
                    offset += pngBytes[i].Length;
                }

                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write(pngBytes[i]);
                }

                File.WriteAllBytes(outPath, ms.ToArray());
                Console.WriteLine("Icon written to: " + outPath + " (" + ms.Length + " bytes)");
            }
        }
    }
}
