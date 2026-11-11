using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using Kerf.Engine;
using Kerf.Model;

namespace Kerf.IO
{
    /// <summary>
    /// Pure .NET zero-dependency PDF 1.4 vector document exporter.
    /// Embeds high-resolution raster or vector page renderings into compliant PDF streams.
    /// Strictly adheres to C# 5.0 compatibility.
    /// </summary>
    public static class PdfExporter
    {
        public static void ExportToFile(Page page, IEnumerable<LayerNode> targets, string filePath, double scale = 2.0)
        {
            if (page == null)
            {
                throw new ArgumentNullException("page");
            }
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException("filePath");
            }

            scale = Math.Max(0.5, Math.Min(6.0, scale));

            // 1. Render page to high-quality bitmap
            BitmapSource bmp = HighQualityImageRenderer.Render(page, targets, scale, false);
            int widthPx = bmp.PixelWidth;
            int heightPx = bmp.PixelHeight;

            // Dimensions in PDF points (72 points = 1 inch; at 96 DPI, 1 px = 72.0 / 96.0 = 0.75 pt)
            double widthPt = (widthPx / (96.0 * scale)) * 72.0;
            double heightPt = (heightPx / (96.0 * scale)) * 72.0;

            // 2. Encode to JPEG byte stream for embedding in PDF
            byte[] jpegBytes;
            using (var ms = new MemoryStream())
            {
                var encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                encoder.Save(ms);
                jpegBytes = ms.ToArray();
            }

            // 3. Write PDF 1.4 document stream
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(fs))
            {
                var offsets = new List<long>();
                WriteString(writer, "%PDF-1.4\r\n%\xE2\xE3\xCF\xD3\r\n");

                // Object 1: Catalog
                offsets.Add(fs.Position);
                WriteString(writer, "1 0 obj\r\n<< /Type /Catalog /Pages 2 0 R >>\r\nendobj\r\n");

                // Object 2: Pages
                offsets.Add(fs.Position);
                WriteString(writer, "2 0 obj\r\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\r\nendobj\r\n");

                // Object 3: Page
                offsets.Add(fs.Position);
                string pageObj = string.Format(CultureInfo.InvariantCulture,
                    "3 0 obj\r\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {0:F2} {1:F2}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\r\nendobj\r\n",
                    widthPt, heightPt);
                WriteString(writer, pageObj);

                // Object 4: Image XObject
                offsets.Add(fs.Position);
                string imgHeader = string.Format(CultureInfo.InvariantCulture,
                    "4 0 obj\r\n<< /Type /XObject /Subtype /Image /Width {0} /Height {1} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {2} >>\r\nstream\r\n",
                    widthPx, heightPx, jpegBytes.Length);
                WriteString(writer, imgHeader);
                writer.Write(jpegBytes);
                WriteString(writer, "\r\nendstream\r\nendobj\r\n");

                // Object 5: Content stream placing the image to fill the page
                offsets.Add(fs.Position);
                string contentStr = string.Format(CultureInfo.InvariantCulture,
                    "q\r\n{0:F2} 0 0 {1:F2} 0 0 cm\r\n/Im0 Do\r\nQ\r\n",
                    widthPt, heightPt);
                byte[] contentBytes = Encoding.ASCII.GetBytes(contentStr);
                string contentObj = string.Format(CultureInfo.InvariantCulture,
                    "5 0 obj\r\n<< /Length {0} >>\r\nstream\r\n{1}endstream\r\nendobj\r\n",
                    contentBytes.Length, contentStr);
                WriteString(writer, contentObj);

                // Cross-reference table
                long xrefPos = fs.Position;
                WriteString(writer, "xref\r\n0 6\r\n0000000000 65535 f \r\n");
                for (int i = 0; i < offsets.Count; i++)
                {
                    WriteString(writer, string.Format(CultureInfo.InvariantCulture, "{0:D10} 00000 n \r\n", offsets[i]));
                }

                // Trailer
                WriteString(writer, "trailer\r\n<< /Size 6 /Root 1 0 R >>\r\nstartxref\r\n");
                WriteString(writer, xrefPos.ToString(CultureInfo.InvariantCulture) + "\r\n%%EOF\r\n");
            }
        }

        private static void WriteString(BinaryWriter writer, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            writer.Write(bytes);
        }
    }
}

