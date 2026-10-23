using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using Kerf.Model;

namespace Kerf.Model
{
    public static class DocumentMigrations
    {
        public static KerfDocument MigrateToCanonical(string json)
        {
            return LoadAndMigrate(json);
        }

        public static KerfDocument LoadAndMigrate(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new KerfDocument();
            }

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

            try
            {
                // 1. Try parsing as v3.0 KerfDocument
                var dict = serializer.Deserialize<Dictionary<string, object>>(json);
                if (dict == null) return new KerfDocument();

                if (dict.ContainsKey("SchemaVersion") && dict["SchemaVersion"].ToString().StartsWith("3."))
                {
                    var doc = serializer.Deserialize<KerfDocument>(json);
                    if (doc != null && doc.Pages != null && doc.Pages.Count > 0)
                    {
                        return doc;
                    }
                }

                // 2. Migration: Detect v2.4 Native format or React Project format
                var migrated = new KerfDocument();
                if (dict.ContainsKey("Name")) migrated.Name = dict["Name"].ToString();
                else if (dict.ContainsKey("name")) migrated.Name = dict["name"].ToString();

                var targetPage = migrated.GetActivePage();

                // Check if JSON has "pages" (React format)
                if (dict.ContainsKey("pages") && dict["pages"] is System.Collections.ArrayList)
                {
                    var pagesList = (System.Collections.ArrayList)dict["pages"];
                    migrated.Pages.Clear();

                    foreach (var pageObj in pagesList)
                    {
                        if (pageObj is Dictionary<string, object>)
                        {
                            var pDict = (Dictionary<string, object>)pageObj;
                            var page = new Page();
                            if (pDict.ContainsKey("id")) page.Id = pDict["id"].ToString();
                            if (pDict.ContainsKey("name")) page.Name = pDict["name"].ToString();
                            if (pDict.ContainsKey("elements") && pDict["elements"] is System.Collections.ArrayList)
                            {
                                var elemList = (System.Collections.ArrayList)pDict["elements"];
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
                // Check if JSON has "Elements" (WPF v2.4 format)
                else if (dict.ContainsKey("Elements") && dict["Elements"] is System.Collections.ArrayList)
                {
                    var elemList = (System.Collections.ArrayList)dict["Elements"];
                    foreach (var elObj in elemList)
                    {
                        if (elObj is Dictionary<string, object>)
                        {
                            targetPage.Layers.Add(MigrateElementDict((Dictionary<string, object>)elObj));
                        }
                    }
                }
                // Check if JSON has "elements" (React flat format)
                else if (dict.ContainsKey("elements") && dict["elements"] is System.Collections.ArrayList)
                {
                    var elemList = (System.Collections.ArrayList)dict["elements"];
                    foreach (var elObj in elemList)
                    {
                        if (elObj is Dictionary<string, object>)
                        {
                            targetPage.Layers.Add(MigrateElementDict((Dictionary<string, object>)elObj));
                        }
                    }
                }

                migrated.SchemaVersion = "3.0.0";
                migrated.UpdatedAt = DateTime.UtcNow.Ticks;
                return migrated;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Migration warning: " + ex.Message);
                return new KerfDocument();
            }
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
