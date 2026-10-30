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
                        int nIdx = headerStr.IndexOf("\"Name\":", StringComparison.OrdinalIgnoreCase);
                        if (nIdx < 0) nIdx = headerStr.IndexOf("\"name\":", StringComparison.OrdinalIgnoreCase);
                        if (nIdx >= 0)
                        {
                            int colonIdx = headerStr.IndexOf(":", nIdx);
                            if (colonIdx >= 0)
                            {
                                int startQuote = headerStr.IndexOf("\"", colonIdx);
                                if (startQuote >= 0)
                                {
                                    int endQuote = headerStr.IndexOf("\"", startQuote + 1);
                                    if (endQuote > startQuote)
                                    {
                                        string foundName = headerStr.Substring(startQuote + 1, endQuote - startQuote - 1).Trim();
                                        if (!string.IsNullOrEmpty(foundName))
                                        {
                                            meta.Name = foundName;
                                        }
                                    }
                                }
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
                string mobilePath = IOPath.Combine(ProjectsFolder, "Mobile UI Design.kerf");
                if (!File.Exists(mobilePath))
                {
                    var doc = CreateStarterMobileTemplate();
                    SaveAtomic(doc, mobilePath);
                }

                string dashPath = IOPath.Combine(ProjectsFolder, "Analytics Dashboard.kerf");
                if (!File.Exists(dashPath))
                {
                    var doc = CreateStarterDashboardTemplate();
                    SaveAtomic(doc, dashPath);
                }

                string landingPath = IOPath.Combine(ProjectsFolder, "SaaS Landing Page.kerf");
                if (!File.Exists(landingPath))
                {
                    var doc = CreateStarterLandingPageTemplate();
                    SaveAtomic(doc, landingPath);
                }

                string designPath = IOPath.Combine(ProjectsFolder, "Design System & UI Kit.kerf");
                if (!File.Exists(designPath))
                {
                    var doc = CreateStarterDesignSystemTemplate();
                    SaveAtomic(doc, designPath);
                }
            }
            catch
            {
                // Template initialization failure is non-fatal
            }
        }

        public static KerfDocument CreateStarterMobileTemplate()
        {
            var doc = new KerfDocument { Name = "Mobile UI Design" };
            var page = doc.GetActivePage();
            page.BackgroundColor = "#0F1117";

            var frame = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = "iPhone 16 Pro - Home",
                Type = ElementType.Frame,
                X = 120,
                Y = 80,
                Width = 393,
                Height = 852,
                CornerRadius = 48,
                ClipContent = true,
                Fills = new List<Paint> { Paint.Solid("#131722") },
                Children = new List<LayerNode>()
            };

            var statusBar = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Status Time",
                Type = ElementType.Text,
                X = 36,
                Y = 20,
                Width = 60,
                Height = 24,
                Typography = new TypographyData
                {
                    Text = "9:41",
                    FontSize = 15,
                    FontWeight = 600,
                    FontFamily = "Segoe UI"
                },
                Fills = new List<Paint> { Paint.Solid("#FFFFFF") }
            };
            frame.Children.Add(statusBar);

            var heroCard = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Balance Card",
                Type = ElementType.Rectangle,
                X = 24,
                Y = 80,
                Width = 345,
                Height = 200,
                CornerRadius = 24,
                Fills = new List<Paint> { Paint.LinearGradient("#6366F1", "#A855F7", 135) }
            };
            frame.Children.Add(heroCard);

            var balanceLabel = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Balance Label",
                Type = ElementType.Text,
                X = 48,
                Y = 108,
                Width = 150,
                Height = 20,
                Typography = new TypographyData
                {
                    Text = "TOTAL BALANCE",
                    FontSize = 11,
                    FontWeight = 700,
                    FontFamily = "Segoe UI"
                },
                Fills = new List<Paint> { Paint.Solid("#E0E7FF") }
            };
            frame.Children.Add(balanceLabel);

            var balanceVal = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Balance Value",
                Type = ElementType.Text,
                X = 48,
                Y = 136,
                Width = 240,
                Height = 44,
                Typography = new TypographyData
                {
                    Text = "$24,580.40",
                    FontSize = 32,
                    FontWeight = 700,
                    FontFamily = "Segoe UI"
                },
                Fills = new List<Paint> { Paint.Solid("#FFFFFF") }
            };
            frame.Children.Add(balanceVal);

            var actionBtn1 = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Send Button",
                Type = ElementType.Rectangle,
                X = 24,
                Y = 304,
                Width = 72,
                Height = 72,
                CornerRadius = 20,
                Fills = new List<Paint> { Paint.Solid("#1F2538") }
            };
            frame.Children.Add(actionBtn1);

            var actionBtn2 = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Receive Button",
                Type = ElementType.Rectangle,
                X = 115,
                Y = 304,
                Width = 72,
                Height = 72,
                CornerRadius = 20,
                Fills = new List<Paint> { Paint.Solid("#1F2538") }
            };
            frame.Children.Add(actionBtn2);

            var actionBtn3 = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Swap Button",
                Type = ElementType.Rectangle,
                X = 206,
                Y = 304,
                Width = 72,
                Height = 72,
                CornerRadius = 20,
                Fills = new List<Paint> { Paint.Solid("#1F2538") }
            };
            frame.Children.Add(actionBtn3);

            var actionBtn4 = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "More Button",
                Type = ElementType.Rectangle,
                X = 297,
                Y = 304,
                Width = 72,
                Height = 72,
                CornerRadius = 20,
                Fills = new List<Paint> { Paint.Solid("#1F2538") }
            };
            frame.Children.Add(actionBtn4);

            var txCard = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Transactions Container",
                Type = ElementType.Rectangle,
                X = 24,
                Y = 400,
                Width = 345,
                Height = 360,
                CornerRadius = 24,
                Fills = new List<Paint> { Paint.Solid("#1A1F30") }
            };
            frame.Children.Add(txCard);

            page.Layers.Add(frame);
            return doc;
        }

        public static KerfDocument CreateStarterDashboardTemplate()
        {
            var doc = new KerfDocument { Name = "Analytics Dashboard" };
            var page = doc.GetActivePage();
            page.BackgroundColor = "#0B0D14";

            var frame = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = "Desktop Canvas",
                Type = ElementType.Frame,
                X = 80,
                Y = 80,
                Width = 1280,
                Height = 800,
                CornerRadius = 16,
                ClipContent = true,
                Fills = new List<Paint> { Paint.Solid("#0F131E") },
                Children = new List<LayerNode>()
            };

            var sidebar = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Sidebar",
                Type = ElementType.Rectangle,
                X = 0,
                Y = 0,
                Width = 240,
                Height = 800,
                Fills = new List<Paint> { Paint.Solid("#151926") }
            };
            frame.Children.Add(sidebar);

            var logo = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Brand Logo",
                Type = ElementType.Text,
                X = 24,
                Y = 28,
                Width = 180,
                Height = 28,
                Typography = new TypographyData
                {
                    Text = "APEX STUDIO",
                    FontSize = 16,
                    FontWeight = 700,
                    FontFamily = "Segoe UI"
                },
                Fills = new List<Paint> { Paint.Solid("#38BDF8") }
            };
            frame.Children.Add(logo);

            var stat1 = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Revenue Metric Card",
                Type = ElementType.Rectangle,
                X = 270,
                Y = 40,
                Width = 310,
                Height = 140,
                CornerRadius = 16,
                Fills = new List<Paint> { Paint.Solid("#1A2030") }
            };
            frame.Children.Add(stat1);

            var stat2 = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Users Metric Card",
                Type = ElementType.Rectangle,
                X = 605,
                Y = 40,
                Width = 310,
                Height = 140,
                CornerRadius = 16,
                Fills = new List<Paint> { Paint.Solid("#1A2030") }
            };
            frame.Children.Add(stat2);

            var stat3 = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Conversion Metric Card",
                Type = ElementType.Rectangle,
                X = 940,
                Y = 40,
                Width = 300,
                Height = 140,
                CornerRadius = 16,
                Fills = new List<Paint> { Paint.Solid("#1A2030") }
            };
            frame.Children.Add(stat3);

            var mainChart = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Analytics Main Chart",
                Type = ElementType.Rectangle,
                X = 270,
                Y = 210,
                Width = 645,
                Height = 550,
                CornerRadius = 20,
                Fills = new List<Paint> { Paint.Solid("#161C2C") }
            };
            frame.Children.Add(mainChart);

            var activityPanel = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Realtime Activity Feed",
                Type = ElementType.Rectangle,
                X = 940,
                Y = 210,
                Width = 300,
                Height = 550,
                CornerRadius = 20,
                Fills = new List<Paint> { Paint.Solid("#161C2C") }
            };
            frame.Children.Add(activityPanel);

            page.Layers.Add(frame);
            return doc;
        }

        public static KerfDocument CreateStarterLandingPageTemplate()
        {
            var doc = new KerfDocument { Name = "SaaS Landing Page" };
            var page = doc.GetActivePage();
            page.BackgroundColor = "#090D16";

            var frame = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = "Landing Page Screen",
                Type = ElementType.Frame,
                X = 100,
                Y = 80,
                Width = 1440,
                Height = 900,
                CornerRadius = 16,
                ClipContent = true,
                Fills = new List<Paint> { Paint.Solid("#0B101D") },
                Children = new List<LayerNode>()
            };

            var navBar = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Navigation Bar",
                Type = ElementType.Rectangle,
                X = 40,
                Y = 24,
                Width = 1360,
                Height = 64,
                CornerRadius = 16,
                Fills = new List<Paint> { Paint.Solid("#131929") }
            };
            frame.Children.Add(navBar);

            var heroBadge = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Hero Badge Pill",
                Type = ElementType.Rectangle,
                X = 570,
                Y = 140,
                Width = 300,
                Height = 36,
                CornerRadius = 18,
                Fills = new List<Paint> { Paint.Solid("#1E263C") }
            };
            frame.Children.Add(heroBadge);

            var heroTitle = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Headline Text",
                Type = ElementType.Text,
                X = 220,
                Y = 200,
                Width = 1000,
                Height = 70,
                Typography = new TypographyData
                {
                    Text = "Design at the Speed of Light",
                    FontSize = 52,
                    FontWeight = 800,
                    FontFamily = "Segoe UI"
                },
                Fills = new List<Paint> { Paint.Solid("#FFFFFF") }
            };
            frame.Children.Add(heroTitle);

            var heroSub = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Subtitle Text",
                Type = ElementType.Text,
                X = 320,
                Y = 285,
                Width = 800,
                Height = 50,
                Typography = new TypographyData
                {
                    Text = "Next-generation DirectX vector engine delivering sub-millisecond manipulation, native SVG typography, and liquid glass optics.",
                    FontSize = 16,
                    FontWeight = 400,
                    FontFamily = "Segoe UI"
                },
                Fills = new List<Paint> { Paint.Solid("#94A3B8") }
            };
            frame.Children.Add(heroSub);

            var ctaBtn = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Primary CTA Button",
                Type = ElementType.Rectangle,
                X = 540,
                Y = 360,
                Width = 170,
                Height = 50,
                CornerRadius = 12,
                Fills = new List<Paint> { Paint.Solid("#6366F1") }
            };
            frame.Children.Add(ctaBtn);

            var ctaSec = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Secondary CTA Button",
                Type = ElementType.Rectangle,
                X = 730,
                Y = 360,
                Width = 170,
                Height = 50,
                CornerRadius = 12,
                Fills = new List<Paint> { Paint.Solid("#1F273B") }
            };
            frame.Children.Add(ctaSec);

            var previewCanvas = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "App Preview Glass Card",
                Type = ElementType.Rectangle,
                X = 220,
                Y = 450,
                Width = 1000,
                Height = 400,
                CornerRadius = 24,
                Fills = new List<Paint> { Paint.Solid("#151B2E") }
            };
            frame.Children.Add(previewCanvas);

            page.Layers.Add(frame);
            return doc;
        }

        public static KerfDocument CreateStarterDesignSystemTemplate()
        {
            var doc = new KerfDocument { Name = "Design System & UI Kit" };
            var page = doc.GetActivePage();
            page.BackgroundColor = "#0C0F18";

            var frame = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                Name = "Design System Canvas",
                Type = ElementType.Frame,
                X = 80,
                Y = 80,
                Width = 1440,
                Height = 900,
                CornerRadius = 16,
                ClipContent = true,
                Fills = new List<Paint> { Paint.Solid("#0F1422") },
                Children = new List<LayerNode>()
            };

            var dsTitle = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Design System Title",
                Type = ElementType.Text,
                X = 48,
                Y = 40,
                Width = 600,
                Height = 40,
                Typography = new TypographyData
                {
                    Text = "Design System & UI Tokens",
                    FontSize = 28,
                    FontWeight = 700,
                    FontFamily = "Segoe UI"
                },
                Fills = new List<Paint> { Paint.Solid("#FFFFFF") }
            };
            frame.Children.Add(dsTitle);

            // Palette Swatches
            string[] colors = new string[] { "#6366F1", "#0EA5E9", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6" };
            for (int i = 0; i < colors.Length; i++)
            {
                var swatch = new LayerNode
                {
                    Id = Guid.NewGuid().ToString().Substring(0, 8),
                    ParentId = frame.Id,
                    Name = "Token " + colors[i],
                    Type = ElementType.Rectangle,
                    X = 48 + i * 110,
                    Y = 110,
                    Width = 96,
                    Height = 96,
                    CornerRadius = 16,
                    Fills = new List<Paint> { Paint.Solid(colors[i]) }
                };
                frame.Children.Add(swatch);
            }

            // Button Components Card
            var btnSection = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Buttons Showcase",
                Type = ElementType.Rectangle,
                X = 48,
                Y = 240,
                Width = 620,
                Height = 280,
                CornerRadius = 20,
                Fills = new List<Paint> { Paint.Solid("#181E30") }
            };
            frame.Children.Add(btnSection);

            // Cards Showcase
            var cardSection = new LayerNode
            {
                Id = Guid.NewGuid().ToString().Substring(0, 8),
                ParentId = frame.Id,
                Name = "Cards Showcase",
                Type = ElementType.Rectangle,
                X = 700,
                Y = 240,
                Width = 690,
                Height = 280,
                CornerRadius = 20,
                Fills = new List<Paint> { Paint.Solid("#181E30") }
            };
            frame.Children.Add(cardSection);

            page.Layers.Add(frame);
            return doc;
        }
    }
}
