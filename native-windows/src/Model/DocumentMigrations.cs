using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using Kerf.Model;
using Kerf.Engine.Animation;

namespace Kerf.Model
{
    public static class DocumentMigrations
    {
        public const string CurrentSchemaVersion = "4.0.0";

        internal static string NormalizeJsonForTypedDeserialization(string json, JavaScriptSerializer serializer)
        {
            if (string.IsNullOrEmpty(json)) return json;
            if (serializer == null) throw new ArgumentNullException("serializer");
            object payload = serializer.DeserializeObject(json);
            return serializer.Serialize(NormalizeOptionalJsonValues(payload, null));
        }

        private static object NormalizeOptionalJsonValues(object value, string propertyName)
        {
            var dictionary = value as IDictionary<string, object>;
            if (dictionary != null)
            {
                // JavaScriptSerializer crashes when mapping nested explicit null values or
                // object-shaped nullable WPF Point/Rect values to typed document properties.
                // Omitted nulls preserve model defaults; WPF converters accept coordinate strings.
                if (string.Equals(propertyName, "GradientStart", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyName, "GradientEnd", StringComparison.OrdinalIgnoreCase))
                {
                    object x;
                    object y;
                    if (TryGetCaseInsensitive(dictionary, "X", out x) && TryGetCaseInsensitive(dictionary, "Y", out y))
                        return FormatGeometryComponent(x) + "," + FormatGeometryComponent(y);
                }
                else if (string.Equals(propertyName, "CropRect", StringComparison.OrdinalIgnoreCase))
                {
                    object x;
                    object y;
                    object width;
                    object height;
                    if (TryGetCaseInsensitive(dictionary, "X", out x) &&
                        TryGetCaseInsensitive(dictionary, "Y", out y) &&
                        TryGetCaseInsensitive(dictionary, "Width", out width) &&
                        TryGetCaseInsensitive(dictionary, "Height", out height))
                    {
                        return FormatGeometryComponent(x) + "," + FormatGeometryComponent(y) + "," +
                            FormatGeometryComponent(width) + "," + FormatGeometryComponent(height);
                    }
                }

                var normalized = new Dictionary<string, object>();
                foreach (var property in dictionary)
                    if (property.Value != null)
                        normalized[property.Key] = NormalizeOptionalJsonValues(property.Value, property.Key);
                return normalized;
            }

            var list = value as IList;
            if (list != null)
            {
                var normalized = new object[list.Count];
                for (int i = 0; i < list.Count; i++)
                    normalized[i] = NormalizeOptionalJsonValues(list[i], null);
                return normalized;
            }

            return value;
        }

        private static bool TryGetCaseInsensitive(IDictionary<string, object> dictionary, string key, out object value)
        {
            if (dictionary.TryGetValue(key, out value)) return true;
            foreach (var property in dictionary)
            {
                if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }

        private static string FormatGeometryComponent(object value)
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture);
        }

        public static KerfDocument MigrateToCanonical(string json)
        {
            return LoadAndMigrate(json);
        }

        public static KerfDocument LoadAndMigrate(string json)
        {
            MigrationReport unusedReport;
            return LoadAndMigrate(json, out unusedReport);
        }

        public static KerfDocument LoadAndMigrate(string json, out MigrationReport report)
        {
            report = new MigrationReport
            {
                SourceVersion = "3.0.0",
                TargetVersion = CurrentSchemaVersion
            };

            if (string.IsNullOrEmpty(json))
            {
                var emptyDoc = new KerfDocument();
                HealAndValidateReferences(emptyDoc, report);
                return emptyDoc;
            }

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

            try
            {
                var dict = serializer.Deserialize<Dictionary<string, object>>(json);
                if (dict == null)
                {
                    var emptyDoc = new KerfDocument();
                    HealAndValidateReferences(emptyDoc, report);
                    return emptyDoc;
                }

                string schema = null;
                if (dict.ContainsKey("SchemaVersion") && dict["SchemaVersion"] != null) schema = dict["SchemaVersion"].ToString();
                else if (dict.ContainsKey("schemaVersion") && dict["schemaVersion"] != null) schema = dict["schemaVersion"].ToString();

                report.SourceVersion = !string.IsNullOrEmpty(schema) ? schema : "2.4.0";

                KerfDocument doc = null;
                if (!string.IsNullOrEmpty(schema) && (schema.StartsWith("4.") || schema.StartsWith("3.")))
                {
                    try
                    {
                        doc = serializer.Deserialize<KerfDocument>(NormalizeJsonForTypedDeserialization(json, serializer));
                    }
                    catch (Exception ex)
                    {
                        report.Warnings.Add("Direct deserialization warning: " + ex.Message);
                        doc = null;
                    }
                }

                if (doc == null || doc.Pages == null || doc.Pages.Count == 0)
                {
                    doc = MigrateFromDictionary(dict, serializer);
                }

                HealAndValidateReferences(doc, report);
                doc.SchemaVersion = CurrentSchemaVersion;
                doc.UpdatedAt = DateTime.UtcNow.Ticks;
                return doc;
            }
            catch (Exception ex)
            {
                report.Warnings.Add("Migration exception: " + ex.Message);
                var fallback = new KerfDocument();
                fallback.SchemaVersion = CurrentSchemaVersion;
                fallback.UpdatedAt = DateTime.UtcNow.Ticks;
                return fallback;
            }
        }

        public static void HealAndValidateReferences(KerfDocument doc, MigrationReport report)
        {
            if (doc == null || report == null) return;

            var layersById = new Dictionary<string, LayerNode>(StringComparer.OrdinalIgnoreCase);
            var layersByHash = new Dictionary<uint, LayerNode>();

            if (doc.Pages != null)
            {
                foreach (var page in doc.Pages)
                {
                    if (page == null || page.Layers == null) continue;
                    var flatLayers = new List<LayerNode>();
                    CollectLayersRecursive(page.Layers, flatLayers);
                    foreach (var layer in flatLayers)
                    {
                        if (layer == null) continue;
                        if (!string.IsNullOrEmpty(layer.Id))
                        {
                            if (!layersById.ContainsKey(layer.Id))
                            {
                                layersById[layer.Id] = layer;
                            }
                            uint hash = PropertyBinder.GetNumericNodeId(layer.Id);
                            if (!layersByHash.ContainsKey(hash))
                            {
                                layersByHash[hash] = layer;
                            }
                        }
                    }
                }
            }

            report.TotalLayersChecked = layersById.Count;

            if (doc.Animation == null) doc.Animation = new AnimationProjectData();
            if (doc.Animation.Clips == null) doc.Animation.Clips = new List<Clip>();
            if (doc.Animation.StateMachine == null) doc.Animation.StateMachine = new StateMachineDefinition();

            // 1. Clips & Tracks reference resolution
            if (doc.Animation.Clips != null)
            {
                foreach (var clip in doc.Animation.Clips)
                {
                    if (clip == null || clip.Tracks == null) continue;
                    foreach (var track in clip.Tracks)
                    {
                        if (track == null) continue;
                        if (string.IsNullOrEmpty(track.Id))
                        {
                            track.Id = Guid.NewGuid().ToString("N");
                        }

                        LayerNode matchedLayer = null;
                        if (!string.IsNullOrEmpty(track.TargetNodeId) && layersById.TryGetValue(track.TargetNodeId, out matchedLayer))
                        {
                            track.IsActive = true;
                            track.NodeId = PropertyBinder.GetNumericNodeId(matchedLayer.Id);
                            report.ResolvedTracksCount++;
                        }
                        else if (track.NodeId != 0 && layersByHash.TryGetValue(track.NodeId, out matchedLayer))
                        {
                            // Auto-heal legacy 32-bit FNV-1a hash
                            track.TargetNodeId = matchedLayer.Id;
                            track.IsActive = true;
                            report.ResolvedTracksCount++;
                        }
                        else
                        {
                            // Target layer is missing
                            track.IsActive = false;
                            report.UnresolvedTracksCount++;
                            var orphan = new OrphanedReferenceItem();
                            orphan.Id = track.Id;
                            orphan.Kind = OrphanKind.Track;
                            orphan.NumericId = track.NodeId;
                            orphan.TargetLayerId = track.TargetNodeId;
                            orphan.DisplayName = string.Format("Track: {0} ({1})",
                                !string.IsNullOrEmpty(track.PropertyPath) ? track.PropertyPath : ((PropKey)track.Prop).ToString(),
                                !string.IsNullOrEmpty(track.TargetNodeId) ? track.TargetNodeId : ("#" + track.NodeId));
                            orphan.Description = string.Format("Clip '{0}' targets missing layer ID '{1}'",
                                clip.Name,
                                !string.IsNullOrEmpty(track.TargetNodeId) ? track.TargetNodeId : ("#" + track.NodeId));
                            orphan.IsActive = false;
                            orphan.SuggestedAction = "Keep";
                            report.OrphanedItems.Add(orphan);
                        }
                    }
                }
            }

            // 2. State Machine Listeners reference resolution
            if (doc.Animation != null && doc.Animation.StateMachine != null && doc.Animation.StateMachine.Listeners != null)
            {
                foreach (var listener in doc.Animation.StateMachine.Listeners)
                {
                    if (listener == null) continue;
                    if (string.IsNullOrEmpty(listener.ListenerId))
                    {
                        listener.ListenerId = "sm_lst_" + Guid.NewGuid().ToString("N");
                    }

                    if (listener.TargetNodeId == 0 && string.IsNullOrEmpty(listener.TargetLayerId))
                    {
                        // Global canvas listener
                        listener.IsActive = true;
                        report.ResolvedListenersCount++;
                    }
                    else
                    {
                        LayerNode matchedLayer = null;
                        if (!string.IsNullOrEmpty(listener.TargetLayerId) && layersById.TryGetValue(listener.TargetLayerId, out matchedLayer))
                        {
                            listener.IsActive = true;
                            listener.TargetNodeId = PropertyBinder.GetNumericNodeId(matchedLayer.Id);
                            report.ResolvedListenersCount++;
                        }
                        else if (listener.TargetNodeId != 0 && layersByHash.TryGetValue(listener.TargetNodeId, out matchedLayer))
                        {
                            // Auto-heal
                            listener.TargetLayerId = matchedLayer.Id;
                            listener.IsActive = true;
                            report.ResolvedListenersCount++;
                        }
                        else
                        {
                            // Missing target layer
                            listener.IsActive = false;
                            report.UnresolvedListenersCount++;
                            var orphan = new OrphanedReferenceItem();
                            orphan.Id = listener.ListenerId;
                            orphan.Kind = OrphanKind.Listener;
                            orphan.NumericId = listener.TargetNodeId;
                            orphan.TargetLayerId = listener.TargetLayerId;
                            orphan.DisplayName = string.Format("Listener: {0} on {1}",
                                listener.Kind,
                                !string.IsNullOrEmpty(listener.TargetLayerId) ? listener.TargetLayerId : ("#" + listener.TargetNodeId));
                            orphan.Description = string.Format("State machine listener targets missing layer ID '{0}'",
                                !string.IsNullOrEmpty(listener.TargetLayerId) ? listener.TargetLayerId : ("#" + listener.TargetNodeId));
                            orphan.IsActive = false;
                            orphan.SuggestedAction = "Keep";
                            report.OrphanedItems.Add(orphan);
                        }
                    }
                }
            }
        }

        private static void CollectLayersRecursive(List<LayerNode> nodes, List<LayerNode> output)
        {
            if (nodes == null || output == null) return;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                output.Add(n);
                if (n.Children != null && n.Children.Count > 0)
                {
                    CollectLayersRecursive(n.Children, output);
                }
            }
        }

        private static KerfDocument MigrateFromDictionary(Dictionary<string, object> dict, JavaScriptSerializer serializer)
        {
            var migrated = new KerfDocument();
            if (dict.ContainsKey("Name")) migrated.Name = dict["Name"].ToString();
            else if (dict.ContainsKey("name")) migrated.Name = dict["name"].ToString();

            var targetPage = migrated.GetActivePage();

            // Check if JSON has "Pages" or "pages"
            object pagesRaw = null;
            if (dict.ContainsKey("Pages")) pagesRaw = dict["Pages"];
            else if (dict.ContainsKey("pages")) pagesRaw = dict["pages"];

            if (pagesRaw is System.Collections.ArrayList)
            {
                var pagesList = (System.Collections.ArrayList)pagesRaw;
                migrated.Pages.Clear();

                foreach (var pageObj in pagesList)
                {
                    if (pageObj is Dictionary<string, object>)
                    {
                        var pDict = (Dictionary<string, object>)pageObj;
                        var page = new Page();
                        if (pDict.ContainsKey("Id")) page.Id = pDict["Id"].ToString();
                        else if (pDict.ContainsKey("id")) page.Id = pDict["id"].ToString();

                        if (pDict.ContainsKey("Name")) page.Name = pDict["Name"].ToString();
                        else if (pDict.ContainsKey("name")) page.Name = pDict["name"].ToString();

                        object layersRaw = null;
                        if (pDict.ContainsKey("Layers")) layersRaw = pDict["Layers"];
                        else if (pDict.ContainsKey("layers")) layersRaw = pDict["layers"];
                        else if (pDict.ContainsKey("Elements")) layersRaw = pDict["Elements"];
                        else if (pDict.ContainsKey("elements")) layersRaw = pDict["elements"];

                        if (layersRaw is System.Collections.ArrayList)
                        {
                            var elemList = (System.Collections.ArrayList)layersRaw;
                            foreach (var elObj in elemList)
                            {
                                if (elObj is Dictionary<string, object>)
                                {
                                    page.Layers.Add(MigrateElementDict((Dictionary<string, object>)elObj));
                                }
                            }
                        }
                        migrated.Pages.Add(page);
                    }
                }

                if (migrated.Pages.Count > 0)
                {
                    migrated.ActivePageId = migrated.Pages[0].Id;
                }
                else
                {
                    migrated.Pages.Add(targetPage);
                }
            }
            // Check if JSON has root "Layers", "layers", "Elements", or "elements"
            else
            {
                object rootLayersRaw = null;
                if (dict.ContainsKey("Layers")) rootLayersRaw = dict["Layers"];
                else if (dict.ContainsKey("layers")) rootLayersRaw = dict["layers"];
                else if (dict.ContainsKey("Elements")) rootLayersRaw = dict["Elements"];
                else if (dict.ContainsKey("elements")) rootLayersRaw = dict["elements"];

                if (rootLayersRaw is System.Collections.ArrayList)
                {
                    var elemList = (System.Collections.ArrayList)rootLayersRaw;
                    foreach (var elObj in elemList)
                    {
                        if (elObj is Dictionary<string, object>)
                        {
                            targetPage.Layers.Add(MigrateElementDict((Dictionary<string, object>)elObj));
                        }
                    }
                }
            }

            // Preserve Animation and State Machine data if present in dictionary
            object animRaw = null;
            if (dict.ContainsKey("Animation")) animRaw = dict["Animation"];
            else if (dict.ContainsKey("animation")) animRaw = dict["animation"];

            if (animRaw != null)
            {
                try
                {
                    string animJson = serializer.Serialize(animRaw);
                    migrated.Animation = serializer.Deserialize<AnimationProjectData>(animJson);
                }
                catch { }
            }

            migrated.SchemaVersion = CurrentSchemaVersion;
            migrated.UpdatedAt = DateTime.UtcNow.Ticks;
            return migrated;
        }

        private static LayerNode MigrateElementDict(Dictionary<string, object> d)
        {
            var node = new LayerNode();

            // Identifiers
            if (d.ContainsKey("Id")) node.Id = d["Id"].ToString();
            else if (d.ContainsKey("id")) node.Id = d["id"].ToString();

            if (d.ContainsKey("Name")) node.Name = d["Name"].ToString();
            else if (d.ContainsKey("name")) node.Name = d["name"].ToString();

            // Type
            string typeStr = "";
            if (d.ContainsKey("Type")) typeStr = d["Type"].ToString();
            else if (d.ContainsKey("type")) typeStr = d["type"].ToString();
            node.Type = ParseElementType(typeStr);

            // Coordinates & Bounds
            node.X = GetDouble(d, "X", "x", 0.0);
            node.Y = GetDouble(d, "Y", "y", 0.0);
            node.Width = Math.Max(2.0, GetDouble(d, "Width", "width", GetDouble(d, "w", "w", 100.0)));
            node.Height = Math.Max(2.0, GetDouble(d, "Height", "height", GetDouble(d, "h", "h", 80.0)));
            node.Rotation = GetDouble(d, "Rotation", "rotation", 0.0);
            node.CornerRadius = GetDouble(d, "CornerRadius", "cornerRadius", GetDouble(d, "radius", "radius", 0.0));
            node.Opacity = Math.Max(0.0, Math.Min(1.0, GetDouble(d, "Opacity", "opacity", 1.0)));

            // Fill
            string fillHex = GetString(d, "Fill", "fill", "#0D99FF");
            if (!string.IsNullOrEmpty(fillHex))
            {
                node.Fills.Clear();
                node.Fills.Add(Paint.Solid(fillHex, node.Opacity));
            }

            // Stroke
            string strokeHex = GetString(d, "Stroke", "stroke", "#000000");
            double strokeW = GetDouble(d, "StrokeWidth", "strokeWidth", 0.0);
            if (strokeW > 0 && !string.IsNullOrEmpty(strokeHex))
            {
                node.Strokes.Clear();
                var s = new StrokeData
                {
                    Paint = Paint.Solid(strokeHex, 1.0),
                    Width = strokeW,
                    Enabled = true
                };
                node.Strokes.Add(s);
            }

            // Typography
            string text = GetString(d, "Text", "text", null);
            if (!string.IsNullOrEmpty(text) || node.Type == ElementType.Text)
            {
                node.Typography.Text = text ?? "Typography";
                node.Typography.FontSize = GetDouble(d, "FontSize", "fontSize", 16.0);
                node.Typography.FontFamily = GetString(d, "FontFamily", "fontFamily", "Inter, Segoe UI, Arial");
            }

            // Auto Layout
            bool isAL = GetBool(d, "IsAutoLayout", "isAutoLayout", false);
            if (isAL)
            {
                node.AutoLayout.Mode = AutoLayoutMode.Horizontal;
                string lm = GetString(d, "LayoutMode", "layoutMode", "");
                if (lm.IndexOf("Vertical", StringComparison.OrdinalIgnoreCase) >= 0 || lm.Equals("vertical", StringComparison.OrdinalIgnoreCase))
                {
                    node.AutoLayout.Mode = AutoLayoutMode.Vertical;
                }
                node.AutoLayout.Gap = GetDouble(d, "ItemSpacing", "itemSpacing", 12.0);
                double pad = GetDouble(d, "Padding", "padding", 16.0);
                node.AutoLayout.PaddingTop = GetDouble(d, "PaddingTop", "paddingTop", pad);
                node.AutoLayout.PaddingRight = GetDouble(d, "PaddingRight", "paddingRight", pad);
                node.AutoLayout.PaddingBottom = GetDouble(d, "PaddingBottom", "paddingBottom", pad);
                node.AutoLayout.PaddingLeft = GetDouble(d, "PaddingLeft", "paddingLeft", pad);
            }

            // Vector Geometry & Points
            object ptsObj = null;
            if (d.ContainsKey("Points")) ptsObj = d["Points"];
            else if (d.ContainsKey("points")) ptsObj = d["points"];

            if (ptsObj is System.Collections.ArrayList)
            {
                var ptsList = (System.Collections.ArrayList)ptsObj;
                node.Geometry.Nodes.Clear();

                foreach (var pObj in ptsList)
                {
                    if (pObj is Dictionary<string, object>)
                    {
                        var pd = (Dictionary<string, object>)pObj;
                        double px = GetDouble(pd, "X", "x", 0.0);
                        double py = GetDouble(pd, "Y", "y", 0.0);
                        node.Geometry.Nodes.Add(new BezierNode(px, py));
                    }
                }

                node.Geometry.IsClosed = GetBool(d, "IsClosed", "isClosed", false);
            }

            string svgPath = GetString(d, "SvgPathData", "pathData", null);
            if (!string.IsNullOrEmpty(svgPath))
            {
                node.Geometry.SvgPathData = svgPath;
            }

            // Effects
            object fxObj = null;
            if (d.ContainsKey("Effects")) fxObj = d["Effects"];
            else if (d.ContainsKey("effects")) fxObj = d["effects"];

            if (fxObj is System.Collections.ArrayList)
            {
                var fxList = (System.Collections.ArrayList)fxObj;
                node.Effects.Clear();
                foreach (var item in fxList)
                {
                    if (item is Dictionary<string, object>)
                    {
                        var fxd = (Dictionary<string, object>)item;
                        var ef = new EffectData
                        {
                            OffsetX = GetDouble(fxd, "OffsetX", "x", 0.0),
                            OffsetY = GetDouble(fxd, "OffsetY", "y", 4.0),
                            Blur = GetDouble(fxd, "Blur", "blur", 8.0),
                            Spread = GetDouble(fxd, "Spread", "spread", 0.0),
                            ColorHex = GetString(fxd, "ColorHex", "color", "#000000"),
                            Enabled = GetBool(fxd, "Enabled", "enabled", true)
                        };
                        node.Effects.Add(ef);
                    }
                }
            }

            // Component & Instance metadata
            string compId = GetString(d, "ComponentId", "componentId", null);
            if (!string.IsNullOrEmpty(compId)) node.ComponentId = compId;
            node.IsMasterComponent = GetBool(d, "IsMasterComponent", "isMasterComponent", false);

            // Nested Children (Frames, Groups, Components)
            object childrenRaw = null;
            if (d.ContainsKey("Children")) childrenRaw = d["Children"];
            else if (d.ContainsKey("children")) childrenRaw = d["children"];

            if (childrenRaw is System.Collections.ArrayList)
            {
                var chList = (System.Collections.ArrayList)childrenRaw;
                node.Children = new List<LayerNode>();
                foreach (var item in chList)
                {
                    if (item is Dictionary<string, object>)
                    {
                        var childNode = MigrateElementDict((Dictionary<string, object>)item);
                        childNode.ParentId = node.Id;
                        node.Children.Add(childNode);
                    }
                }
            }

            // Flags
            node.IsVisible = GetBool(d, "IsVisible", "visible", true);
            node.IsLocked = GetBool(d, "IsLocked", "locked", false);
            node.ParentId = GetString(d, "ParentId", "parentId", null);

            return node;
        }

        private static ElementType ParseElementType(string t)
        {
            if (string.IsNullOrEmpty(t)) return ElementType.Rectangle;
            t = t.Trim().ToLowerInvariant();

            int numericType;
            if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out numericType) &&
                Enum.IsDefined(typeof(ElementType), numericType))
            {
                return (ElementType)numericType;
            }

            if (t.Contains("componentinstance") || t.Contains("instance")) return ElementType.ComponentInstance;
            if (t.Contains("frame")) return ElementType.Frame;
            if (t.Contains("group")) return ElementType.Group;
            if (t.Contains("ellipse") || t.Contains("circle")) return ElementType.Ellipse;
            if (t.Contains("star")) return ElementType.Star;
            if (t.Contains("polygon") || t.Contains("triangle")) return ElementType.Polygon;
            if (t.Contains("line")) return ElementType.Line;
            if (t.Contains("arrow")) return ElementType.Arrow;
            if (t.Contains("path") || t.Contains("pen") || t.Contains("pencil")) return ElementType.Path;
            if (t.Contains("text") || t.Contains("type")) return ElementType.Text;
            if (t.Contains("image")) return ElementType.Image;
            return ElementType.Rectangle;
        }

        private static double GetDouble(Dictionary<string, object> d, string k1, string k2, double def)
        {
            object v = null;
            if (d.ContainsKey(k1)) v = d[k1];
            else if (d.ContainsKey(k2)) v = d[k2];

            if (v == null) return def;
            double res;
            if (double.TryParse(v.ToString(), out res)) return res;
            return def;
        }

        private static string GetString(Dictionary<string, object> d, string k1, string k2, string def)
        {
            if (d.ContainsKey(k1) && d[k1] != null) return d[k1].ToString();
            if (d.ContainsKey(k2) && d[k2] != null) return d[k2].ToString();
            return def;
        }

        private static bool GetBool(Dictionary<string, object> d, string k1, string k2, bool def)
        {
            object v = null;
            if (d.ContainsKey(k1)) v = d[k1];
            else if (d.ContainsKey(k2)) v = d[k2];

            if (v == null) return def;
            bool b;
            if (bool.TryParse(v.ToString(), out b)) return b;
            return def;
        }

        public static string SerializeToCanonicalJson(KerfDocument doc)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return serializer.Serialize(doc);
        }
    }
}
