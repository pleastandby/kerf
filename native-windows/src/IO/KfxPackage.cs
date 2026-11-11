using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web.Script.Serialization;
using Kerf.Engine.Animation;
using Kerf.Model;

namespace Kerf.IO
{
    public class KfxManifestPayload
    {
        public string FormatVersion { get; set; }
        public string Generator { get; set; }
        public string CreatedAt { get; set; }
        public string RuntimeCompatibility { get; set; }
        public string Title { get; set; }
        public bool HasMetadata { get; set; }
        public Dictionary<string, object> EntryPoints { get; set; }

        public KfxManifestPayload()
        {
            FormatVersion = "1.0.0";
            Generator = "Kerf Studio 4.0.0";
            CreatedAt = DateTime.UtcNow.ToString("o");
            RuntimeCompatibility = ">=1.0.0";
            Title = "Untitled";
            HasMetadata = true;
            EntryPoints = new Dictionary<string, object>();
        }
    }

    public class KfxScenePayload
    {
        public string PageId { get; set; }
        public string Name { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string BackgroundColor { get; set; }
        public List<LayerNode> Layers { get; set; }

        public KfxScenePayload()
        {
            PageId = "page1";
            Name = "Page 1";
            Width = 1920.0;
            Height = 1080.0;
            BackgroundColor = "#1E1E22";
            Layers = new List<LayerNode>();
        }
    }

    public class KfxMetadataPayload
    {
        public string DocumentId { get; set; }
        public string OriginalName { get; set; }
        public string ActivePageId { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
        public string SchemaVersion { get; set; }
        public Dictionary<string, string> AssetLibrary { get; set; }
        public List<Page> Pages { get; set; }

        public KfxMetadataPayload()
        {
            AssetLibrary = new Dictionary<string, string>();
            Pages = new List<Page>();
        }
    }

    public static class KfxPackage
    {
        public static void ExportPackage(KerfDocument doc, string targetFilePath, bool includeEditorMetadata = true)
        {
            if (doc == null)
            {
                throw new ArgumentNullException("doc");
            }
            if (string.IsNullOrEmpty(targetFilePath))
            {
                throw new ArgumentException("targetFilePath cannot be null or empty", "targetFilePath");
            }

            string fullTargetPath = Path.GetFullPath(targetFilePath);
            string directory = Path.GetDirectoryName(fullTargetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempZipPath = Path.Combine(
                !string.IsNullOrEmpty(directory) ? directory : ".",
                Path.GetFileName(fullTargetPath) + ".tmp." + Guid.NewGuid().ToString("N"));

            try
            {
                using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, false))
                {
                    var serializer = new JavaScriptSerializer { MaxJsonLength = 100 * 1024 * 1024 };

                    // 1. manifest.json
                    var manifest = new KfxManifestPayload
                    {
                        FormatVersion = "1.0.0",
                        Generator = "Kerf Studio 4.0.0",
                        CreatedAt = DateTime.UtcNow.ToString("o"),
                        RuntimeCompatibility = ">=1.0.0",
                        Title = doc.Name != null ? doc.Name : "Untitled",
                        HasMetadata = includeEditorMetadata
                    };
                    manifest.EntryPoints["defaultClipId"] = (doc.Animation != null) ? doc.Animation.ActiveClipId : 0;
                    manifest.EntryPoints["defaultStateMachine"] = (doc.Animation != null && doc.Animation.StateMachine != null && !string.IsNullOrEmpty(doc.Animation.StateMachine.Name))
                        ? doc.Animation.StateMachine.Name
                        : "StateMachine";

                    WriteZipEntry(archive, "manifest.json", serializer.Serialize(manifest));

                    // 2. scene.json
                    var activePage = doc.GetActivePage();
                    var scene = new KfxScenePayload
                    {
                        PageId = activePage != null ? activePage.Id : "page1",
                        Name = activePage != null ? activePage.Name : "Page 1",
                        BackgroundColor = (activePage != null && !string.IsNullOrEmpty(activePage.BackgroundColor))
                            ? activePage.BackgroundColor
                            : "#1E1E22",
                        Width = 1920.0,
                        Height = 1080.0,
                        Layers = (activePage != null && activePage.Layers != null) ? activePage.Layers : new List<LayerNode>()
                    };

                    if (activePage != null && activePage.Layers != null)
                    {
                        for (int i = 0; i < activePage.Layers.Count; i++)
                        {
                            var layer = activePage.Layers[i];
                            if (layer != null && layer.Type == ElementType.Frame && layer.Width > 0 && layer.Height > 0)
                            {
                                scene.Width = layer.Width;
                                scene.Height = layer.Height;
                                break;
                            }
                        }
                    }

                    WriteZipEntry(archive, "scene.json", serializer.Serialize(scene));

                    // 3. animations.json
                    var clips = (doc.Animation != null && doc.Animation.Clips != null)
                        ? doc.Animation.Clips
                        : new List<Clip>();
                    WriteZipEntry(archive, "animations.json", serializer.Serialize(clips));

                    // 4. state-machines.json
                    var stateMachine = (doc.Animation != null && doc.Animation.StateMachine != null)
                        ? doc.Animation.StateMachine
                        : new StateMachineDefinition();
                    WriteZipEntry(archive, "state-machines.json", serializer.Serialize(stateMachine));

                    // 5. metadata.json (optional)
                    if (includeEditorMetadata)
                    {
                        var metadata = new KfxMetadataPayload
                        {
                            DocumentId = doc.Id,
                            OriginalName = doc.Name,
                            ActivePageId = doc.ActivePageId,
                            CreatedAt = doc.CreatedAt,
                            UpdatedAt = doc.UpdatedAt,
                            SchemaVersion = doc.SchemaVersion,
                            AssetLibrary = doc.AssetLibrary != null ? doc.AssetLibrary : new Dictionary<string, string>(),
                            Pages = doc.Pages != null ? doc.Pages : new List<Page>()
                        };
                        WriteZipEntry(archive, "metadata.json", serializer.Serialize(metadata));
                    }
                }

                // Atomic commit of temporary file to target
                if (File.Exists(fullTargetPath))
                {
                    string backupFile = fullTargetPath + ".bak";
                    try { File.Copy(fullTargetPath, backupFile, true); }
                    catch { }
                    File.Replace(tempZipPath, fullTargetPath, null, true);
                }
                else
                {
                    File.Move(tempZipPath, fullTargetPath);
                }
            }
            finally
            {
                if (File.Exists(tempZipPath))
                {
                    try { File.Delete(tempZipPath); }
                    catch { }
                }
            }
        }

        public static KerfDocument ImportPackage(string packageFilePath)
        {
            if (string.IsNullOrEmpty(packageFilePath))
            {
                throw new ArgumentNullException("packageFilePath");
            }
            if (!File.Exists(packageFilePath))
            {
                throw new FileNotFoundException("Package file not found: " + packageFilePath);
            }

            // Security check for directory traversal and zip bomb attacks
            KfxSecurityValidator.ValidatePackageSecurity(packageFilePath);

            using (var archive = ZipFile.OpenRead(packageFilePath))
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = 100 * 1024 * 1024 };

                string manifestJson = ReadZipEntryText(archive, "manifest.json");
                string sceneJson = ReadZipEntryText(archive, "scene.json");
                string animationsJson = ReadZipEntryText(archive, "animations.json");
                string stateMachinesJson = ReadZipEntryText(archive, "state-machines.json");
                string metadataJson = ReadZipEntryText(archive, "metadata.json");

                var manifest = !string.IsNullOrEmpty(manifestJson)
                    ? serializer.Deserialize<KfxManifestPayload>(manifestJson)
                    : null;

                var scene = !string.IsNullOrEmpty(sceneJson)
                    ? serializer.Deserialize<KfxScenePayload>(sceneJson)
                    : new KfxScenePayload();

                var clips = !string.IsNullOrEmpty(animationsJson)
                    ? serializer.Deserialize<List<Clip>>(animationsJson)
                    : new List<Clip>();

                var stateMachine = !string.IsNullOrEmpty(stateMachinesJson)
                    ? serializer.Deserialize<StateMachineDefinition>(stateMachinesJson)
                    : new StateMachineDefinition();

                var metadata = !string.IsNullOrEmpty(metadataJson)
                    ? serializer.Deserialize<KfxMetadataPayload>(metadataJson)
                    : null;

                var doc = new KerfDocument();

                if (metadata != null)
                {
                    if (!string.IsNullOrEmpty(metadata.DocumentId)) doc.Id = metadata.DocumentId;
                    if (!string.IsNullOrEmpty(metadata.OriginalName)) doc.Name = metadata.OriginalName;
                    if (!string.IsNullOrEmpty(metadata.SchemaVersion)) doc.SchemaVersion = metadata.SchemaVersion;
                    if (metadata.CreatedAt > 0) doc.CreatedAt = metadata.CreatedAt;
                    if (metadata.UpdatedAt > 0) doc.UpdatedAt = metadata.UpdatedAt;
                    if (metadata.AssetLibrary != null) doc.AssetLibrary = metadata.AssetLibrary;

                    if (metadata.Pages != null && metadata.Pages.Count > 0)
                    {
                        doc.Pages = metadata.Pages;
                        doc.ActivePageId = !string.IsNullOrEmpty(metadata.ActivePageId)
                            ? metadata.ActivePageId
                            : doc.Pages[0].Id;
                    }
                }
                else
                {
                    if (manifest != null && !string.IsNullOrEmpty(manifest.Title))
                    {
                        doc.Name = manifest.Title;
                    }
                }

                // If doc.Pages was not populated by metadata, assemble from scene.json
                if (doc.Pages == null || doc.Pages.Count == 0 || metadata == null)
                {
                    var page = new Page
                    {
                        Id = !string.IsNullOrEmpty(scene.PageId) ? scene.PageId : "page1",
                        Name = !string.IsNullOrEmpty(scene.Name) ? scene.Name : "Page 1",
                        BackgroundColor = !string.IsNullOrEmpty(scene.BackgroundColor) ? scene.BackgroundColor : "#1E1E22",
                        Layers = scene.Layers != null ? scene.Layers : new List<LayerNode>()
                    };
                    doc.Pages = new List<Page> { page };
                    doc.ActivePageId = page.Id;
                }

                if (doc.Animation == null)
                {
                    doc.Animation = new AnimationProjectData();
                }
                doc.Animation.Clips = clips != null ? clips : new List<Clip>();
                doc.Animation.StateMachine = stateMachine != null ? stateMachine : new StateMachineDefinition();

                if (manifest != null && manifest.EntryPoints != null && manifest.EntryPoints.ContainsKey("defaultClipId"))
                {
                    try
                    {
                        doc.Animation.ActiveClipId = Convert.ToUInt32(manifest.EntryPoints["defaultClipId"]);
                    }
                    catch { }
                }

                // Run migration, ID healing, and reference validation
                string docJson = DocumentMigrations.SerializeToCanonicalJson(doc);
                MigrationReport report;
                var validatedDoc = DocumentMigrations.LoadAndMigrate(docJson, out report);
                return validatedDoc != null ? validatedDoc : doc;
            }
        }

        private static void WriteZipEntry(ZipArchive archive, string entryName, string text)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(text);
            }
        }

        private static string ReadZipEntryText(ZipArchive archive, string entryName)
        {
            var entry = archive.GetEntry(entryName);
            if (entry == null) return null;
            using (var stream = entry.Open())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
