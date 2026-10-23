using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using Kerf.Model;
using IOPath = System.IO.Path;

namespace Kerf.IO
{
    public class ProjectMetadata
    {
        public string FilePath { get; set; }
        public string Name { get; set; }
        public DateTime LastModified { get; set; }
        public long FileSizeBytes { get; set; }
        public int LayerCount { get; set; }
        public string SchemaVersion { get; set; }
    }

    public static class StorageManager
    {
        private static readonly string AppDataFolder = IOPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KerfStudio");

        public static string ProjectsFolder
        {
            get
            {
                var p = IOPath.Combine(AppDataFolder, "Projects");
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                return p;
            }
        }

        public static string AutosaveFolder
        {
            get
            {
                var p = IOPath.Combine(AppDataFolder, "Autosave");
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                return p;
            }
        }

        public static string TemplatesFolder
        {
            get
            {
                var p = IOPath.Combine(AppDataFolder, "Templates");
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                return p;
            }
        }

        public static void SaveAtomic(KerfDocument doc, string targetFilePath)
        {
            if (doc == null) throw new ArgumentNullException("doc");
            if (string.IsNullOrEmpty(targetFilePath)) throw new ArgumentNullException("targetFilePath");

            doc.UpdatedAt = DateTime.UtcNow.Ticks;
            var serializer = new JavaScriptSerializer { MaxJsonLength = 100 * 1024 * 1024 };
            string json = serializer.Serialize(doc);

            string directory = IOPath.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to temporary file first
            string tempFile = targetFilePath + ".tmp." + Guid.NewGuid().ToString("N");
            File.WriteAllText(tempFile, json, Encoding.UTF8);

            // Create backup if target exists
            if (File.Exists(targetFilePath))
            {
                string bakFile = targetFilePath + ".bak";
                try
                {
                    File.Copy(targetFilePath, bakFile, true);
                }
                catch
                {
                    // Non-fatal if backup copy fails
                }

                // Atomic replace
                try
                {
                    File.Delete(targetFilePath);
                    File.Move(tempFile, targetFilePath);
                }
                catch
                {
                    // Fallback to copy and delete
                    File.Copy(tempFile, targetFilePath, true);
                    File.Delete(tempFile);
                }
            }
            else
            {
                File.Move(tempFile, targetFilePath);
            }
        }

        public static KerfDocument LoadDocument(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Document file not found: " + filePath);
            }

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            var doc = DocumentMigrations.MigrateToCanonical(json);
            return doc;
        }

        public static void SaveAutosave(KerfDocument doc, string activeDocumentPath)
        {
            if (doc == null) return;
            try
            {
                string docKey = string.IsNullOrEmpty(activeDocumentPath)
                    ? "unsaved_" + doc.Id
                    : IOPath.GetFileNameWithoutExtension(activeDocumentPath);

                string autosavePath = IOPath.Combine(AutosaveFolder, docKey + ".autosave.kerf");
                SaveAtomic(doc, autosavePath);
            }
            catch
            {
                // Background autosave failure must not crash application
            }
        }

        public static string CheckForAutosaveRecovery(string originalFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(originalFilePath) || !File.Exists(originalFilePath))
                {
                    // Check for any unsaved autosaves
                    var unsavedFiles = Directory.GetFiles(AutosaveFolder, "unsaved_*.autosave.kerf");
                    if (unsavedFiles.Length > 0)
                    {
                        var newest = unsavedFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                        return newest;
                    }
                    return null;
                }

                string docKey = IOPath.GetFileNameWithoutExtension(originalFilePath);
                string autosavePath = IOPath.Combine(AutosaveFolder, docKey + ".autosave.kerf");

                if (File.Exists(autosavePath))
                {
                    var origTime = File.GetLastWriteTime(originalFilePath);
                    var autoTime = File.GetLastWriteTime(autosavePath);

                    if (autoTime > origTime.AddSeconds(5))
                    {
                        return autosavePath;
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public static void ClearAutosave(string activeDocumentPath)
        {
            try
            {
                if (string.IsNullOrEmpty(activeDocumentPath)) return;
                string docKey = IOPath.GetFileNameWithoutExtension(activeDocumentPath);
                string autosavePath = IOPath.Combine(AutosaveFolder, docKey + ".autosave.kerf");
                if (File.Exists(autosavePath))
                {
                    File.Delete(autosavePath);
                }
            }
            catch
            {
                // Ignore cleanup error
            }
        }

        public static List<ProjectMetadata> GetRecentProjects()
        {
            var list = new List<ProjectMetadata>();
            if (!Directory.Exists(ProjectsFolder)) return list;

            var files = Directory.GetFiles(ProjectsFolder, "*.kerf", SearchOption.TopDirectoryOnly);
            var serializer = new JavaScriptSerializer();

            foreach (var file in files)
            {
                try
                {
                    var fi = new FileInfo(file);
                    var meta = new ProjectMetadata
                    {
                        FilePath = file,
                        Name = IOPath.GetFileNameWithoutExtension(file),
                        LastModified = fi.LastWriteTime,
                        FileSizeBytes = fi.Length,
                        LayerCount = 0,
                        SchemaVersion = "3.0.0"
                    };

                    // Fast probe first 1000 bytes
                    using (var reader = new StreamReader(file, Encoding.UTF8))
                    {
                        var headerBuf = new char[1024];
                        int read = reader.Read(headerBuf, 0, 1024);
                        var headerStr = new string(headerBuf, 0, read);
                        if (headerStr.Contains("\"name\":"))
                        {
                            int idx = headerStr.IndexOf("\"name\":") + 7;
                            int end = headerStr.IndexOf("\"", idx + 1);
                            if (idx > 6 && end > idx)
                            {
                                meta.Name = headerStr.Substring(idx + 1, end - idx - 1);
                            }
                        }
                    }

                    list.Add(meta);
                }
                catch
                {
                    // Skip corrupt file in listing
                }
            }

            return list.OrderByDescending(p => p.LastModified).ToList();
        }

        public static void EnsureDefaultTemplates()
        {
            try
            {
                string blankPath = IOPath.Combine(ProjectsFolder, "Mobile UI Design.kerf");
                if (!File.Exists(blankPath))
                {
                    var doc = CreateStarterMobileTemplate();
                    SaveAtomic(doc, blankPath);
                }

                string dashPath = IOPath.Combine(ProjectsFolder, "Analytics Dashboard.kerf");
                if (!File.Exists(dashPath))
                {
                    var doc = CreateStarterDashboardTemplate();
                    SaveAtomic(doc, dashPath);
                }
            }
            catch
            {
                // Template initialization failure is non-fatal
            }
        }

        private static KerfDocument CreateStarterMobileTemplate()
        {
            var doc = new KerfDocument { Name = "Mobile UI Design" };
            var page = doc.GetActivePage();
            page.BackgroundColor = "#0F1117";

            var frame = new LayerNode
            {
                Name = "iPhone 16 Pro - Home",
                Type = ElementType.Frame,
                X = 120,
                Y = 80,
                Width = 393,
                Height = 852,
                CornerRadius = 48,
                ClipContent = true,
                Fills = new List<Paint> { Paint.Solid("#1A1D26") }
            };

            var header = new LayerNode
            {
                Name = "Hero Card",
                Type = ElementType.Rectangle,
                X = 144,
                Y = 160,
                Width = 345,
                Height = 220,
                CornerRadius = 24,
                Fills = new List<Paint> { Paint.LinearGradient("#6366F1", "#A855F7", 135) }
            };

            var title = new LayerNode
            {
                Name = "Title Text",
                Type = ElementType.Text,
                X = 168,
                Y = 200,
                Width = 290,
                Height = 40,
                Typography = new TypographyData
                {
                    Text = "Kerf Studio v3.0",
                    FontSize = 28,
                    FontWeight = 700,
                    FontFamily = "Segoe UI"
                },
                Fills = new List<Paint> { Paint.Solid("#FFFFFF") }
            };

            var btn = new LayerNode
            {
                Name = "Action Button",
                Type = ElementType.Rectangle,
                X = 168,
                Y = 300,
                Width = 140,
                Height = 48,
                CornerRadius = 14,
                Fills = new List<Paint> { Paint.Solid("#FFFFFF") }
            };

            page.Layers.Add(frame);
            page.Layers.Add(header);
            page.Layers.Add(title);
            page.Layers.Add(btn);

            return doc;
        }

        private static KerfDocument CreateStarterDashboardTemplate()
        {
            var doc = new KerfDocument { Name = "Analytics Dashboard" };
            var page = doc.GetActivePage();
            page.BackgroundColor = "#0B0D14";

            var frame = new LayerNode
            {
                Name = "Desktop Canvas",
                Type = ElementType.Frame,
                X = 80,
                Y = 80,
                Width = 1280,
                Height = 800,
                CornerRadius = 16,
                ClipContent = true,
                Fills = new List<Paint> { Paint.Solid("#121622") }
            };

            var sidebar = new LayerNode
            {
                Name = "Sidebar",
                Type = ElementType.Rectangle,
                X = 80,
                Y = 80,
                Width = 240,
                Height = 800,
                Fills = new List<Paint> { Paint.Solid("#171D2D") }
            };

            var metricCard = new LayerNode
            {
                Name = "Revenue Card",
                Type = ElementType.Rectangle,
                X = 360,
                Y = 140,
                Width = 320,
                Height = 180,
                CornerRadius = 16,
                Fills = new List<Paint> { Paint.Solid("#1E2438") }
            };

            page.Layers.Add(frame);
            page.Layers.Add(sidebar);
            page.Layers.Add(metricCard);

            return doc;
        }
    }
}
