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
            HighQualityImageRenderer.ExportToFile(page, singleNode, outputPath, format, scale);
        }

        public static void ExportToFile(
            Page page,
            System.Collections.Generic.IEnumerable<LayerNode> targets,
            string outputPath,
            RasterFormat format,
            double scale = 2.0)
        {
            HighQualityImageRenderer.ExportToFile(page, targets, outputPath, format, scale);
        }
    }
}
