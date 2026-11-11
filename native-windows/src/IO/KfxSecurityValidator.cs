using System;
using System.IO;
using System.IO.Compression;

namespace Kerf.IO
{
    public static class KfxSecurityValidator
    {
        public const long MaxUncompressedPackageBytes = 250L * 1024L * 1024L; // 250 MB

        public static void ValidatePackageSecurity(string zipFilePath)
        {
            if (string.IsNullOrEmpty(zipFilePath))
            {
                throw new ArgumentNullException("zipFilePath");
            }
            if (!File.Exists(zipFilePath))
            {
                throw new FileNotFoundException("Zip archive not found: " + zipFilePath);
            }

            using (var archive = ZipFile.OpenRead(zipFilePath))
            {
                long totalBytes = 0;
                foreach (var entry in archive.Entries)
                {
                    string fullName = entry.FullName != null ? entry.FullName : string.Empty;
                    string normalized = fullName.Replace('\\', '/');

                    if (normalized.StartsWith("/") ||
                        normalized.Contains("../") ||
                        normalized.Contains("/..") ||
                        normalized == ".." ||
                        normalized.EndsWith("/.."))
                    {
                        throw new InvalidDataException(
                            "Potentially malicious directory traversal path detected in .kfx archive: " + fullName);
                    }

                    totalBytes += entry.Length;
                    if (totalBytes > MaxUncompressedPackageBytes)
                    {
                        throw new InvalidDataException(
                            "Exceeded maximum uncompressed package size limit (250MB).");
                    }
                }
            }
        }

        public static string ValidateExtractionPath(string targetDir, string entryFullName)
        {
            if (string.IsNullOrEmpty(targetDir))
            {
                throw new ArgumentNullException("targetDir");
            }
            if (string.IsNullOrEmpty(entryFullName))
            {
                throw new ArgumentNullException("entryFullName");
            }

            string normalizedEntry = entryFullName.Replace('\\', '/');
            if (normalizedEntry.StartsWith("/") ||
                normalizedEntry.Contains("../") ||
                normalizedEntry.Contains("/..") ||
                normalizedEntry == ".." ||
                normalizedEntry.EndsWith("/.."))
            {
                throw new InvalidDataException(
                    "Potentially malicious directory traversal path detected in entry name: " + entryFullName);
            }

            string fullTargetDir = Path.GetFullPath(targetDir);
            if (!fullTargetDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                fullTargetDir += Path.DirectorySeparatorChar;
            }

            string fullDestPath = Path.GetFullPath(Path.Combine(fullTargetDir, entryFullName));
            if (!fullDestPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Target extraction path is outside destination directory: " + entryFullName);
            }

            return fullDestPath;
        }
    }
}
