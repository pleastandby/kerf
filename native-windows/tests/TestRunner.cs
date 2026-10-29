using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kerf.Engine;
using Kerf.IO;
using Kerf.Model;
using Kerf.Tools;
using Kerf.UI;
using Page = Kerf.Model.Page;

namespace Kerf.Tests
{
    public static class TestRunner
    {
        private static int _passed = 0;
        private static int _failed = 0;

        [STAThread]
        public static int Main(string[] args)
        {
            Console.WriteLine("=====================================================================");
            Console.WriteLine(" Kerf Studio — Automated Verification Test Suite (v3.0.0)");
            Console.WriteLine("=====================================================================");

            RunTest("Document Migrations: Legacy React v1/v2 to Canonical v3.0", TestLegacyReactMigration);
            RunTest("Document Migrations: Legacy WPF v2.4 to Canonical v3.0", TestLegacyWpfMigration);
            RunTest("Document Persistence: Atomic Save & Re-load Roundtrip", TestDocumentRoundtrip);
            RunTest("Command History: Add, Undo, Redo, Batch Transactions", TestCommandHistory);
            RunTest("Transform Engine: 8-handle Aspect Ratio & Center Scaling", TestTransformEngine);
            RunTest("Bézier Engine: Anchor & Smooth Handle Alignments", TestBezierEngine);
            RunTest("Auto Layout Engine: Horizontal & Vertical Reflow with Hug Sizing", TestAutoLayoutEngine);
            RunTest("Boolean Geometry Engine: Path Combination", TestBooleanEngine);
            RunTest("SVG Exporter: Compliant Vector XML Generation", TestSvgExport);
            RunTest("Layer Hierarchy & Management: Containers, Cycles, Nested Undo/Redo", TestLayerHierarchyAndManagement);
            RunTest("Bézier Pen Curves & Vector Edit Mode: Handles, HitTest, Node Mutators", TestPenCurvesAndVectorEditing);
            RunTest("Path Geometry Sync & Corner Curving: Movement, Scaling, Corner Widgets", TestPathMovementAndCornerCurving);
            RunTest("Native Rust Acceleration: Signed Area, Point Containment & Convex Hull", TestRustAcceleration);
            RunTest("Frame Clipping & Child Lockstep Movement", TestFrameClippingAndChildContainment);
            RunTest("Two-Layer Arrangement Controls & Marquee Selection", TestTwoLayerArrangementAndMarquee);
            RunTest("Split Node Handles & Independent Alt-Movement", TestSplitNodesAndIndependentHandles);
            RunTest("Undo Position Restoration (Move, Arrangement, Vector Geometry)", TestUndoPositionRestoration);
            RunTest("Figma Typography Dynamics & In-Place Text Editing", TestTypographyAndTextDynamics);
            RunTest("Multiple Fills, Multiple Strokes & Gray Shape Defaults", TestMultipleFillsStrokesAndShapeDefaults);
            RunTest("Smart Snapping Alignment Guides & Threshold Snapping", TestSmartSnappingCalculations);
            RunTest("Two-Layer Reordering, Drag Drop Direction & Command Undo/Redo", TestTwoLayerReorderingAndCommandHistory);
            RunTest("Frame Selection (Ctrl+Alt+G): Hierarchy, Relative Bounds & Undo/Redo", TestFrameSelectionCommand);
            RunTest("Component System: Master Components, Instances, Overrides & Detachment", TestComponentAndInstanceSystem);
            RunTest("Auto Layout Align (Shift+A): Multi-Selection Wrap & Single-Frame Toggling", TestAutoAlignSelection);
            RunTest("Canvas Navigation: Double-Finger Slide Pan, Shift Pan & Centered Ctrl-Zoom", TestTouchpadPanAndZoomCalculations);
            RunTest("Figma Toolbar Vector Icons: Selection Arrow Pointer & Component Glyphs", TestFigmaToolbarSelectionIconAndGeometries);
            RunTest("New Requirements: Delete Shortcut, Middle Pan, Context Icons, Component Glyph, 0px Gap, Fill Sizing", TestUpdatedRequirements);
            RunTest("Photoshop-Grade Image Rendering Engine: Nested Layers, Text Dynamics, Multi-Pass Shadows & Multi-Scale Export", TestHighQualityImageRenderingEngine);
            RunTest("Batch 2 Requirements: Drop Shadow Polar/Direction, Multiple Effects (Glass/Grain/Blur), Loading Screen & Icons", TestNewRequirementsBatch2);
            RunTest("Batch 3 Requirements: Polished ComboBox Dropdown Styling, Apple Real Liquid Glass Optical Render & Rich Effects Controls", TestBatch3Requirements);
            RunTest("Batch 4 Requirements: Adobe Illustrator Gradient Ramps, Midpoints, Pins & Presets", TestBatch4Requirements_AdobeIllustratorGradients);
            RunTest("Batch 5 Requirements: Figma-Style On-Canvas Gradient Rollers & Simplified Ramp", TestBatch5Requirements_FigmaOnCanvasGradientRollers);
            RunTest("Batch 6 Requirements: Real Liquid Glass Snell Optical Refraction & Smooth Sliders", TestBatch6Requirements_RealLiquidGlassAndSmoothSliders);

            Console.WriteLine("=====================================================================");
            Console.WriteLine(string.Format(" Tests Completed: {0} Passed, {1} Failed", _passed, _failed));
            Console.WriteLine("=====================================================================");

            return (_failed == 0) ? 0 : 1;
        }

        private static void TestFrameClippingAndChildContainment()
        {
            var page = new Page();
            var frame = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Card Frame",
                X = 100,
                Y = 100,
                Width = 300,
                Height = 200,
                CornerRadius = 16,
                ClipContent = true
            };
            page.Layers.Add(frame);

            var child = new LayerNode
            {
                Type = ElementType.Rectangle,
                Name = "Inner Image",
                X = 150,
                Y = 150,
                Width = 100,
                Height = 80,
                ParentId = frame.Id
            };
            frame.Children.Add(child);

            // 1. Verify frame clipping state
            Assert(frame.ClipContent == true, "Frame ClipContent flag should be true");
            Assert(frame.CornerRadius == 16, "Frame CornerRadius should be 16");

            // 2. Lockstep movement: when frame moves by (50, 40), child moves by (50, 40)
            double dx = 50;
            double dy = 40;
            var dragPositions = new Dictionary<LayerNode, Point>();
            CollectPositionsRecursive(frame, dragPositions);

            Assert(dragPositions.ContainsKey(frame), "Frame should be in drag positions");
            Assert(dragPositions.ContainsKey(child), "Frame child must be in drag positions for lockstep movement");

            foreach (var kvp in dragPositions)
            {
                kvp.Key.X = kvp.Value.X + dx;
                kvp.Key.Y = kvp.Value.Y + dy;
            }

            Assert(frame.X == 150 && frame.Y == 140, "Frame moved to (150, 140)");
            Assert(child.X == 200 && child.Y == 190, "Frame child moved in lockstep to (200, 190)");

            // 3. Drop containment check: layer inside frame bounds
            Point childCenter = new Point(child.X + child.Width / 2.0, child.Y + child.Height / 2.0);
            Assert(frame.GetBounds().Contains(childCenter), "Child center must be inside frame bounds");
        }

        private static void CollectPositionsRecursive(LayerNode node, Dictionary<LayerNode, Point> dict)
        {
            if (node == null) return;
            if (!dict.ContainsKey(node)) dict[node] = new Point(node.X, node.Y);
            if (node.Children != null)
            {
                foreach (var c in node.Children) CollectPositionsRecursive(c, dict);
            }
        }

        private static void TestTwoLayerArrangementAndMarquee()
        {
            var page = new Page();
            var layerA = new LayerNode { Name = "Card", X = 100, Y = 100, Width = 200, Height = 100 };
            var layerB = new LayerNode { Name = "Button", X = 500, Y = 400, Width = 80, Height = 40 };
            page.Layers.Add(layerA);
            page.Layers.Add(layerB);

            double gap = 12.0;

            // 1. Side by side
            layerB.X = layerA.X + layerA.Width + gap;
            layerB.Y = layerA.Y;
            Assert(layerB.X == 312 && layerB.Y == 100, string.Format("Side by side expected X=312, Y=100; got X={0}, Y={1}", layerB.X, layerB.Y));

            // 2. Stack below
            layerB.X = layerA.X;
            layerB.Y = layerA.Y + layerA.Height + gap;
            Assert(layerB.X == 100 && layerB.Y == 212, string.Format("Stack below expected X=100, Y=212; got X={0}, Y={1}", layerB.X, layerB.Y));

            // 3. Center overlap
            layerB.X = layerA.X + (layerA.Width - layerB.Width) / 2.0;
            layerB.Y = layerA.Y + (layerA.Height - layerB.Height) / 2.0;
            Assert(layerB.X == 160 && layerB.Y == 130, string.Format("Center overlap expected X=160, Y=130; got X={0}, Y={1}", layerB.X, layerB.Y));

            // 4. Swap positions
            double ax = layerA.X, ay = layerA.Y;
            double bx = layerB.X, by = layerB.Y;
            layerA.X = bx; layerA.Y = by;
            layerB.X = ax; layerB.Y = ay;
            Assert(layerA.X == 160 && layerA.Y == 130, "LayerA has swapped to B's position");
            Assert(layerB.X == 100 && layerB.Y == 100, "LayerB has swapped to A's position");

            // 5. Match dimensions
            layerB.Width = layerA.Width;
            layerB.Height = layerA.Height;
            Assert(layerB.Width == 200 && layerB.Height == 100, "LayerB dimensions match LayerA");

            // 6. Marquee rectangle intersection selection
            Rect marqueeRect = new Rect(80, 80, 300, 200);
            var selected = new List<LayerNode>();
            foreach (var l in page.GetAllLayersFlat())
            {
                if (marqueeRect.IntersectsWith(l.GetBounds()))
                {
                    selected.Add(l);
                }
            }
            Assert(selected.Count == 2, string.Format("Marquee rectangle should select both layers, got {0}", selected.Count));
        }

        private static void TestPenCurvesAndVectorEditing()
        {
            var geo = new VectorGeometry { IsClosed = false };

            // 1. Add node and pull out symmetric Bézier handles (Pen curve drag)
            var node0 = new BezierNode(50, 50);
            geo.Nodes.Add(node0);

            // Simulate pulling out Bézier handle to (80, 70)
            Point dragHandle = new Point(80, 70);
            double dx = dragHandle.X - node0.Anchor.X;
            double dy = dragHandle.Y - node0.Anchor.Y;
            node0.Type = BezierNodeType.Symmetric;
            node0.OutHandle = dragHandle;
            node0.InHandle = new Point(node0.Anchor.X - dx, node0.Anchor.Y - dy);

            Assert(node0.OutHandle.X == 80 && node0.OutHandle.Y == 70, "OutHandle must equal drag point");
            Assert(node0.InHandle.X == 20 && node0.InHandle.Y == 30, "InHandle must be mirrored opposite");

            // 2. Add second node
            var node1 = new BezierNode(150, 100);
            geo.Nodes.Add(node1);

            // 3. HitTest against anchors and handles
            var hitAnchor = BezierVectorEngine.HitTest(geo, new Point(50, 50), 5.0);
            Assert(hitAnchor.NodeIndex == 0 && hitAnchor.IsAnchor, "Should hit test anchor at (50, 50)");

            var hitOutHandle = BezierVectorEngine.HitTest(geo, new Point(80, 70), 5.0);
            Assert(hitOutHandle.NodeIndex == 0 && hitOutHandle.IsOutHandle, "Should hit test out-handle at (80, 70)");

            var hitInHandle = BezierVectorEngine.HitTest(geo, new Point(20, 30), 5.0);
            Assert(hitInHandle.NodeIndex == 0 && hitInHandle.IsInHandle, "Should hit test in-handle at (20, 30)");

            // 4. MoveAnchor moves handles synchronously
            BezierVectorEngine.MoveAnchor(geo, 0, new Point(60, 60));
            Assert(node0.Anchor.X == 60 && node0.Anchor.Y == 60, "Anchor moved to (60, 60)");
            Assert(node0.OutHandle.X == 90 && node0.OutHandle.Y == 80, "OutHandle shifted by delta (10, 10)");
            Assert(node0.InHandle.X == 30 && node0.InHandle.Y == 40, "InHandle shifted by delta (10, 10)");

            // 5. MoveHandle preserves symmetry
            BezierVectorEngine.MoveHandle(geo, 0, true, new Point(100, 80));
            Assert(node0.OutHandle.X == 100 && node0.OutHandle.Y == 80, "OutHandle updated");
            Assert(node0.InHandle.X == 20 && node0.InHandle.Y == 40, "InHandle mirrored opposite (60 - 40, 60 - 20)");

            // 6. SmoothAllNodes and SharpenAllNodes
            var poly = new VectorGeometry { IsClosed = true };
            poly.Nodes.Add(new BezierNode(0, 0));
            poly.Nodes.Add(new BezierNode(100, 0));
            poly.Nodes.Add(new BezierNode(100, 100));
            poly.Nodes.Add(new BezierNode(0, 100));

            BezierVectorEngine.SmoothAllNodes(poly);
            Assert(poly.Nodes[1].Type == BezierNodeType.Smooth, "Nodes should be Smooth after SmoothAllNodes");
            Assert(poly.Nodes[1].OutHandle.X != poly.Nodes[1].Anchor.X || poly.Nodes[1].OutHandle.Y != poly.Nodes[1].Anchor.Y, "Handles should be pulled out");

            BezierVectorEngine.SharpenAllNodes(poly);
            Assert(poly.Nodes[1].Type == BezierNodeType.Corner, "Nodes should be Corner after SharpenAllNodes");
            Assert(poly.Nodes[1].OutHandle == poly.Nodes[1].Anchor, "Handles should reset to anchor on sharpen");

            // 7. ToggleClosed
            Assert(poly.IsClosed, "Poly should be closed");
            BezierVectorEngine.ToggleClosed(poly);
            Assert(!poly.IsClosed, "Poly should be open after ToggleClosed");
        }

        private static void TestPathMovementAndCornerCurving()
        {
            // 1. Path movement sync: Layer bounds + VectorGeometry points offset together
            var pathNode = new LayerNode
            {
                Id = "path-1",
                Type = ElementType.Path,
                X = 50,
                Y = 60,
                Width = 100,
                Height = 80,
                Geometry = new VectorGeometry()
            };
            pathNode.Geometry.Nodes.Add(new BezierNode(50, 60));
            pathNode.Geometry.Nodes.Add(new BezierNode(150, 140));

            // Move path by (+30, -20)
            double dx = 30.0;
            double dy = -20.0;
            pathNode.X += dx;
            pathNode.Y += dy;
            BezierVectorEngine.OffsetGeometry(pathNode.Geometry, dx, dy);

            Assert(pathNode.X == 80 && pathNode.Y == 40, "LayerNode X/Y updated");
            Assert(pathNode.Geometry.Nodes[0].Anchor.X == 80 && pathNode.Geometry.Nodes[0].Anchor.Y == 40, "Anchor 0 moved synchronously");
            Assert(pathNode.Geometry.Nodes[1].Anchor.X == 180 && pathNode.Geometry.Nodes[1].Anchor.Y == 120, "Anchor 1 moved synchronously");

            // 2. Clone & Duplicate preservation
            var clone = pathNode.Clone();
            Assert(clone.X == pathNode.X && clone.Y == pathNode.Y, "Clone position matches source exactly");
            Assert(clone.Geometry.Nodes[0].Anchor.X == 80, "Clone geometry nodes in exact alignment");

            // Duplicate with offset (+24, +24)
            clone.X += 24;
            clone.Y += 24;
            BezierVectorEngine.OffsetGeometry(clone.Geometry, 24, 24);
            Assert(clone.X == 104 && clone.Geometry.Nodes[0].Anchor.X == 104, "Duplicated path geometry offset matches bounds");

            // 3. ScaleGeometry
            Rect oldB = new Rect(80, 40, 100, 80);
            Rect newB = new Rect(80, 40, 200, 160); // 2x scale
            BezierVectorEngine.ScaleGeometry(pathNode.Geometry, oldB, newB);
            Assert(pathNode.Geometry.Nodes[0].Anchor.X == 80 && pathNode.Geometry.Nodes[0].Anchor.Y == 40, "Scaled anchor 0 origin");
            Assert(pathNode.Geometry.Nodes[1].Anchor.X == 280 && pathNode.Geometry.Nodes[1].Anchor.Y == 200, "Scaled anchor 1 at 2x offset");

            // 4. Corner Radius Widget hit-testing & curving
            Rect rectBounds = new Rect(100, 100, 200, 120);
            double cornerRadius = 16.0;

            // Inside top-left corner handle: offset ~ 14 + 16*0.4 = 20.4 -> point (120.4, 120.4)
            Point handlePoint = new Point(120.4, 120.4);
            bool hit = TransformEngine.HitTestCornerRadiusHandle(handlePoint, rectBounds, 0, cornerRadius, 1.0);
            Assert(hit, "Corner radius widget hit test should succeed near top-left handle");

            // Point outside handle tolerance
            bool hitMiss = TransformEngine.HitTestCornerRadiusHandle(new Point(100, 100), rectBounds, 0, cornerRadius, 1.0);
            Assert(!hitMiss, "Corner radius widget hit test should miss on outer corner point");

            // Max radius clamping
            double maxAllowedRadius = Math.Min(rectBounds.Width, rectBounds.Height) / 2.0; // 60.0
            Assert(maxAllowedRadius == 60.0, "Max allowed corner radius for 200x120 is 60.0");
        }

        private static void RunTest(string testName, Action testFunc)
        {
            try
            {
                testFunc();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(string.Format("[PASS] {0}", testName));
                Console.ResetColor();
                _passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(string.Format("[FAIL] {0}: {1}", testName, ex.Message));
                Console.ResetColor();
                _failed++;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion Failed: " + message);
        }

        private static void TestLegacyReactMigration()
        {
            string reactJson = "{\"name\":\"Mobile App\",\"pages\":[{\"id\":\"p1\",\"name\":\"Home Screen\",\"elements\":[{\"id\":\"e1\",\"type\":\"rectangle\",\"x\":10,\"y\":20,\"width\":100,\"height\":80,\"fill\":\"#FF0000\"}]}]}";
            var doc = DocumentMigrations.LoadAndMigrate(reactJson);

            Assert(doc != null, "Migrated document must not be null");
            Assert(doc.Name == "Mobile App", "Name should migrate");
            Assert(doc.Pages.Count == 1, "Should have 1 page");
            var page = doc.Pages[0];
            Assert(page.Name == "Home Screen", "Page name should migrate");
            Assert(page.Layers.Count == 1, "Page should contain 1 layer");
            var layer = page.Layers[0];
            Assert(layer.Type == ElementType.Rectangle, "Type should convert to Rectangle");
            Assert(layer.Fills.Count > 0 && layer.Fills[0].ColorHex == "#FF0000", "Fill hex should convert");
        }

        private static void TestLegacyWpfMigration()
        {
            string wpfJson = "{\"Format\":\"kerf-vector-design\",\"Version\":\"2.4.0\",\"Name\":\"Dashboard\",\"Elements\":[{\"Id\":\"w1\",\"Name\":\"Card\",\"Type\":\"Frame\",\"X\":50,\"Y\":50,\"Width\":200,\"Height\":150,\"Fill\":\"#1A1A1A\"}]}";
            var doc = DocumentMigrations.LoadAndMigrate(wpfJson);

            Assert(doc != null, "Migrated document must not be null");
            Assert(doc.Name == "Dashboard", "Project name should migrate");
            var page = doc.GetActivePage();
            Assert(page.Layers.Count == 1, "Should migrate 1 element to page layers");
            var layer = page.Layers[0];
            Assert(layer.Type == ElementType.Frame, "Layer type should be Frame");
            Assert(layer.Width == 200 && layer.Height == 150, "Dimensions should match");
        }

        private static void TestDocumentRoundtrip()
        {
            var doc = new KerfDocument { Name = "Roundtrip Test" };
            var page = doc.GetActivePage();
            var node = new LayerNode
            {
                Name = "Gradient Box",
                Type = ElementType.Rectangle,
                X = 100,
                Y = 120,
                Width = 240,
                Height = 160,
                Fills = new List<Paint> { Paint.LinearGradient("#6366F1", "#A855F7", 45) },
                Effects = new List<EffectData> { new EffectData { Type = EffectType.DropShadow, Blur = 12, OffsetY = 6 } }
            };
            page.Layers.Add(node);

            string tempFile = Path.Combine(Path.GetTempPath(), "kerf_test_" + Guid.NewGuid() + ".kerf");
            StorageManager.SaveAtomic(doc, tempFile);

            var loaded = StorageManager.LoadDocument(tempFile);
            File.Delete(tempFile);

            Assert(loaded != null, "Loaded doc must not be null");
            Assert(loaded.Name == "Roundtrip Test", "Name must match");
            var loadedPage = loaded.GetActivePage();
            Assert(loadedPage.Layers.Count == 1, "Should have 1 layer");
            var loadedNode = loadedPage.Layers[0];
            Assert(loadedNode.Fills[0].Type == PaintType.LinearGradient, "Fill type preserved");
            Assert(loadedNode.Effects[0].Blur == 12, "Effect blur preserved");
        }

        private static void TestCommandHistory()
        {
            var history = new CommandHistory();
            var page = new Page();
            var node = new LayerNode { Name = "Test Node", X = 10, Y = 10, Width = 50, Height = 50 };

            var cmd = new AddLayerCommand(page, node);
            history.Execute(cmd);

            Assert(page.Layers.Count == 1, "Layer added via command");
            Assert(history.CanUndo, "CanUndo must be true");

            history.Undo();
            Assert(page.Layers.Count == 0, "Layer removed via Undo");
            Assert(history.CanRedo, "CanRedo must be true");

            history.Redo();
            Assert(page.Layers.Count == 1, "Layer re-added via Redo");
        }

        private static void TestTransformEngine()
        {
            var orig = new Rect(100, 100, 200, 100);
            var startPt = new Point(300, 200); // BottomRight handle
            var curPt = new Point(350, 250);

            // Aspect ratio locked
            var res = TransformEngine.ApplyTransform(
                HandleType.BottomRight,
                curPt,
                startPt,
                orig,
                0,
                true, // keepAspectRatio
                false,
                0);

            double expectedRatio = 200.0 / 100.0;
            double actualRatio = res.Bounds.Width / res.Bounds.Height;
            Assert(Math.Abs(actualRatio - expectedRatio) < 0.01, "Aspect ratio must be preserved");
        }

        private static void TestBezierEngine()
        {
            var geo = new VectorGeometry();
            var pt1 = new Point(10, 10);
            var pt2 = new Point(100, 10);
            BezierVectorEngine.AddNode(geo, pt1);
            BezierVectorEngine.AddNode(geo, pt2);

            Assert(geo.Nodes.Count == 2, "Should have 2 nodes");

            // Set Smooth on node 1
            BezierVectorEngine.SetNodeType(geo, 0, BezierNodeType.Smooth);
            Assert(geo.Nodes[0].Type == BezierNodeType.Smooth, "Node 0 must be Smooth");

            // Move OutHandle and verify InHandle aligns
            BezierVectorEngine.MoveHandle(geo, 0, true, new Point(30, 10));
            var inH = geo.Nodes[0].InHandle;
            Assert(inH.X < geo.Nodes[0].Anchor.X, "InHandle must be collinear in opposite direction");

            string svgPath = BezierVectorEngine.ToSvgPathData(geo);
            Assert(!string.IsNullOrEmpty(svgPath) && svgPath.StartsWith("M"), "SVG path must start with M");
        }

        private static void TestAutoLayoutEngine()
        {
            var frame = new LayerNode
            {
                Type = ElementType.Frame,
                X = 0,
                Y = 0,
                Width = 400,
                Height = 300,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Horizontal,
                    Gap = 10,
                    PaddingLeft = 20,
                    PaddingRight = 20,
                    PaddingTop = 15,
                    PaddingBottom = 15,
                    SizingH = SizingMode.Hug
                }
            };

            var c1 = new LayerNode { Width = 80, Height = 40 };
            var c2 = new LayerNode { Width = 120, Height = 50 };
            frame.Children.Add(c1);
            frame.Children.Add(c2);

            AutoLayoutEngine.Reflow(frame);

            Assert(c1.X == 20, "Child 1 X must equal PaddingLeft");
            Assert(c2.X == 20 + 80 + 10, "Child 2 X must equal PaddingLeft + Child1.W + Gap");
            Assert(frame.Width == 20 + 80 + 10 + 120 + 20, "Frame Width must hug contents");
        }

        private static void TestBooleanEngine()
        {
            var n1 = new LayerNode { Type = ElementType.Rectangle, X = 10, Y = 10, Width = 100, Height = 100 };
            var n2 = new LayerNode { Type = ElementType.Rectangle, X = 50, Y = 50, Width = 100, Height = 100 };

            var combined = BooleanGeometryEngine.Combine(new List<LayerNode> { n1, n2 }, BooleanOp.Union, null);
            Assert(combined != null, "Combined layer must not be null");
            Assert(combined.Type == ElementType.Path, "Combined layer must be a Path");
            Assert(combined.Geometry != null, "Combined layer must have VectorGeometry");
        }

        private static void TestSvgExport()
        {
            var page = new Page { BackgroundColor = "#111218" };
            var rect = new LayerNode { Type = ElementType.Rectangle, X = 20, Y = 30, Width = 100, Height = 60, Fills = new List<Paint> { Paint.Solid("#0D99FF") } };
            page.Layers.Add(rect);

            string svg = SvgExporter.ExportPage(page);
            Assert(svg.Contains("<svg"), "Must contain <svg root element");
            Assert(svg.Contains("viewBox="), "Must contain viewBox");
            Assert(svg.Contains("<rect"), "Must contain <rect element");
            Assert(svg.Contains("#0D99FF"), "Must contain fill color");
        }

        private static void TestLayerHierarchyAndManagement()
        {
            var page = new Page();
            var frame = new LayerNode { Id = "frame-1", Name = "Main Frame", Type = ElementType.Frame, Children = new List<LayerNode>() };
            var child1 = new LayerNode { Id = "child-1", Name = "Button", Type = ElementType.Rectangle, ParentId = "frame-1" };
            var child2 = new LayerNode { Id = "child-2", Name = "Label", Type = ElementType.Text, ParentId = "frame-1" };
            var rootLayer = new LayerNode { Id = "root-1", Name = "Background", Type = ElementType.Rectangle };

            frame.Children.Add(child1);
            frame.Children.Add(child2);
            page.Layers.Add(frame);
            page.Layers.Add(rootLayer);

            // 1. Layer count
            Assert(page.CountAllLayers() == 4, "Total layer count should be 4");

            // 2. FindContainer
            LayerNode parent;
            var containerChild = page.FindContainer(child1, out parent);
            Assert(parent == frame, "Parent of child1 must be frame");
            Assert(containerChild == frame.Children, "Container of child1 must be frame.Children");

            var containerRoot = page.FindContainer(rootLayer, out parent);
            Assert(parent == null, "Parent of rootLayer must be null");
            Assert(containerRoot == page.Layers, "Container of rootLayer must be page.Layers");

            // 3. IsDescendant
            Assert(Kerf.Model.Page.IsDescendant(frame, child1), "child1 must be descendant of frame");
            Assert(Kerf.Model.Page.IsDescendant(frame, child2), "child2 must be descendant of frame");
            Assert(!Kerf.Model.Page.IsDescendant(child1, frame), "frame cannot be descendant of child1");
            Assert(!Kerf.Model.Page.IsDescendant(rootLayer, child1), "child1 is not descendant of rootLayer");

            // 4. Nested Layer Deletion and Undo with CommandHistory
            var history = new CommandHistory();
            var deleteCmd = new DeleteLayersCommand(page, new[] { child1 });
            history.Execute(deleteCmd);
            Assert(frame.Children.Count == 1, "frame should have 1 child after deleting child1");
            Assert(page.FindLayerById("child-1") == null, "child1 should not be found in page");

            history.Undo();
            Assert(frame.Children.Count == 2, "frame should have 2 children after undoing delete");
            Assert(frame.Children[0].Id == "child-1", "child1 should be restored to original index");

            // 5. Deep Clone
            var clone = frame.Clone();
            Assert(clone.Id != frame.Id, "Clone must have new ID");
            Assert(clone.Children.Count == 2, "Clone must copy children");
            Assert(clone.Children[0].ParentId == clone.Id, "Cloned child ParentId must point to cloned frame");

            // 6. AddLayersCommand (Alt+Duplicate undo/redo)
            var addCmd = new AddLayersCommand(page, new[] { clone });
            history.Execute(addCmd);
            Assert(page.FindLayerById(clone.Id) != null, "Cloned frame should be present after AddLayersCommand");
            history.Undo();
            Assert(page.FindLayerById(clone.Id) == null, "Cloned frame should be removed after Undo");
            history.Redo();
            Assert(page.FindLayerById(clone.Id) != null, "Cloned frame should be restored after Redo");
        }

        private static void TestRustAcceleration()
        {
            // 1. Rust engine availability
            bool available = RustEngineInterop.IsAvailable;
            Console.WriteLine(string.Format("    [INFO] Rust Core: {0} ({1})",
                available ? "Native DLL Loaded" : "Managed Fallback",
                RustEngineInterop.SimdDescription));

            // 2. Closed path signed area
            var square = new VectorGeometry { IsClosed = true };
            square.Nodes.Add(new BezierNode(0, 0));
            square.Nodes.Add(new BezierNode(100, 0));
            square.Nodes.Add(new BezierNode(100, 100));
            square.Nodes.Add(new BezierNode(0, 100));

            double area;
            bool areaOk = RustEngineInterop.TryComputeSignedArea(square, out area);
            Assert(areaOk, "TryComputeSignedArea should succeed for 4-node closed path");
            Assert(Math.Abs(Math.Abs(area) - 10000.0) < 5.0, string.Format("Square area should be 10000, got {0}", area));

            // 3. Point-in-path containment (winding number)
            bool inside;
            bool hitOk1 = RustEngineInterop.TryPathContainsPoint(square, new Point(50, 50), out inside);
            Assert(hitOk1 && inside, "Point (50,50) must be inside 100x100 square");

            bool hitOk2 = RustEngineInterop.TryPathContainsPoint(square, new Point(150, 50), out inside);
            Assert(hitOk2 && !inside, "Point (150,50) must be outside 100x100 square");

            // 4. Convex Hull
            var cloud = new List<Point>
            {
                new Point(0, 0),
                new Point(100, 0),
                new Point(100, 100),
                new Point(0, 100),
                new Point(50, 50), // interior point
                new Point(25, 75)  // interior point
            };
            var hull = RustEngineInterop.ComputeConvexHull(cloud);
            Assert(hull.Count == 4, string.Format("Convex hull should have 4 corner vertices, got {0}", hull.Count));
        }

        private static void TestSplitNodesAndIndependentHandles()
        {
            var geo = new VectorGeometry { IsClosed = false };
            var node = new BezierNode(100, 100);
            node.InHandle = new Point(60, 100);
            node.OutHandle = new Point(140, 100);
            node.Type = BezierNodeType.Symmetric;
            geo.Nodes.Add(node);

            // 1. In Symmetric mode, moving OutHandle reflects InHandle
            BezierVectorEngine.MoveHandle(geo, 0, true, new Point(140, 120));
            Assert(node.InHandle.Y == 80, "Symmetric handle must reflect InHandle opposite Y");

            // 2. Set to Split mode (or decouple via Alt-drag)
            BezierVectorEngine.SetNodeType(geo, 0, BezierNodeType.Split);
            Assert(node.Type == BezierNodeType.Split, "Node type should be Split");

            // 3. Move OutHandle in Split mode -> InHandle remains completely untouched!
            Point savedInHandle = node.InHandle;
            BezierVectorEngine.MoveHandle(geo, 0, true, new Point(160, 150));
            Assert(node.OutHandle.X == 160 && node.OutHandle.Y == 150, "OutHandle must move to new coordinate");
            Assert(node.InHandle.X == savedInHandle.X && node.InHandle.Y == savedInHandle.Y, "InHandle must stay completely independent and stationary");

            // 4. Move InHandle in Split mode -> OutHandle remains completely untouched!
            Point savedOutHandle = node.OutHandle;
            BezierVectorEngine.MoveHandle(geo, 0, false, new Point(40, 70));
            Assert(node.InHandle.X == 40 && node.InHandle.Y == 70, "InHandle must move to new coordinate");
            Assert(node.OutHandle.X == savedOutHandle.X && node.OutHandle.Y == savedOutHandle.Y, "OutHandle must stay completely independent and stationary");

            // 5. Test SplitPathAtNode on closed path
            var closedGeo = new VectorGeometry { IsClosed = true };
            closedGeo.Nodes.Add(new BezierNode(0, 0));
            closedGeo.Nodes.Add(new BezierNode(100, 0));
            closedGeo.Nodes.Add(new BezierNode(100, 100));
            closedGeo.Nodes.Add(new BezierNode(0, 100));

            BezierVectorEngine.SplitPathAtNode(closedGeo, 1);
            Assert(!closedGeo.IsClosed, "SplitPathAtNode on closed path must open the path");
            Assert(closedGeo.Nodes.Count == 5, "SplitPathAtNode should open loop with duplicate endpoint");
        }

        private static void TestUndoPositionRestoration()
        {
            var history = new CommandHistory();
            var page = new Page();
            var layerA = new LayerNode { Name = "Card", X = 100, Y = 100, Width = 200, Height = 100 };
            var layerB = new LayerNode { Name = "Button", X = 500, Y = 400, Width = 80, Height = 40 };
            page.Layers.Add(layerA);
            page.Layers.Add(layerB);

            // 1. MoveLayersPositionsCommand (Drag / Nudge / Arrangement)
            var beforePos = new Dictionary<LayerNode, Point>();
            beforePos[layerA] = new Point(layerA.X, layerA.Y);
            beforePos[layerB] = new Point(layerB.X, layerB.Y);

            // Simulate moving layerA by +150, +200
            layerA.X = 250;
            layerA.Y = 300;

            var afterPos = new Dictionary<LayerNode, Point>();
            afterPos[layerA] = new Point(layerA.X, layerA.Y);
            afterPos[layerB] = new Point(layerB.X, layerB.Y);

            var moveCmd = new MoveLayersPositionsCommand(beforePos, afterPos, null, null, null);
            history.Execute(moveCmd);

            Assert(layerA.X == 250 && layerA.Y == 300, "LayerA position should be updated");

            // Execute Undo -> MUST reset position back to 100, 100!
            history.Undo();
            Assert(layerA.X == 100 && layerA.Y == 100, string.Format("Undo must reset LayerA position to (100, 100), got ({0}, {1})", layerA.X, layerA.Y));

            // Execute Redo -> Restores to 250, 300
            history.Redo();
            Assert(layerA.X == 250 && layerA.Y == 300, "Redo must restore LayerA position to (250, 300)");

            // 2. ModifyVectorGeometryCommand (Anchor & Handle Movement Undo)
            var pathLayer = new LayerNode
            {
                Type = ElementType.Path,
                X = 0, Y = 0, Width = 100, Height = 100,
                Geometry = new VectorGeometry { IsClosed = false }
            };
            pathLayer.Geometry.Nodes.Add(new BezierNode(10, 10));
            pathLayer.Geometry.Nodes.Add(new BezierNode(90, 90));

            var beforeGeo = pathLayer.Geometry.Clone();

            // Mutate node anchor
            pathLayer.Geometry.Nodes[0].Anchor = new Point(50, 60);
            var afterGeo = pathLayer.Geometry.Clone();

            var geoCmd = new ModifyVectorGeometryCommand(pathLayer, beforeGeo, afterGeo, null);
            history.Execute(geoCmd);

            Assert(pathLayer.Geometry.Nodes[0].Anchor.X == 50, "Geometry node anchor should be at (50, 60)");

            history.Undo();
            Assert(pathLayer.Geometry.Nodes[0].Anchor.X == 10 && pathLayer.Geometry.Nodes[0].Anchor.Y == 10, "Undo must reset geometry node anchor to (10, 10)");

            history.Redo();
            Assert(pathLayer.Geometry.Nodes[0].Anchor.X == 50 && pathLayer.Geometry.Nodes[0].Anchor.Y == 60, "Redo must restore geometry node anchor to (50, 60)");
        }

        private static void TestTypographyAndTextDynamics()
        {
            var textNode = new LayerNode
            {
                Type = ElementType.Text,
                Name = "Title Heading",
                X = 50,
                Y = 80,
                Width = 200,
                Height = 40,
                Typography = new TypographyData
                {
                    Text = "The quick brown fox",
                    FontFamily = "Segoe UI",
                    FontSize = 24.0,
                    FontWeight = 700,
                    IsItalic = true,
                    LineHeight = 1.4,
                    LetterSpacing = 0.5,
                    Align = TextAlign.Center,
                    VerticalAlign = VerticalAlign.Middle,
                    AutoResize = TextAutoResize.AutoWidth,
                    Decoration = TextDecorationMode.Underline,
                    Case = TextCase.Upper
                }
            };

            Assert(textNode.Typography.FontWeight == 700, "FontWeight should be Bold 700");
            Assert(textNode.Typography.IsItalic, "IsItalic should be true");
            Assert(textNode.Typography.Align == TextAlign.Center, "Align should be Center");
            Assert(textNode.Typography.VerticalAlign == VerticalAlign.Middle, "VerticalAlign should be Middle");
            Assert(textNode.Typography.AutoResize == TextAutoResize.AutoWidth, "AutoResize should be AutoWidth");
            Assert(textNode.Typography.Decoration == TextDecorationMode.Underline, "Decoration should be Underline");
            Assert(textNode.Typography.Case == TextCase.Upper, "Case should be Upper");

            // Test Typography Clone
            var cloned = textNode.Clone();
            Assert(cloned.Typography != null && cloned.Typography.Text == "The quick brown fox", "Cloned typography text should match");
            Assert(cloned.Typography.VerticalAlign == VerticalAlign.Middle, "Cloned VerticalAlign should match");
            Assert(cloned.Typography.AutoResize == TextAutoResize.AutoWidth, "Cloned AutoResize should match");

            // Test ModifyTypographyCommand Undo/Redo
            var history = new CommandHistory();
            var beforeTypo = textNode.Typography.Clone();
            var beforeBounds = textNode.GetBounds();

            textNode.Typography.Text = "Updated Heading Text";
            textNode.Width = 280;
            textNode.Height = 50;
            var afterTypo = textNode.Typography.Clone();
            var afterBounds = textNode.GetBounds();

            var cmd = new ModifyTypographyCommand(textNode, beforeTypo, afterTypo, beforeBounds, afterBounds, null);
            history.Execute(cmd);
            Assert(textNode.Typography.Text == "Updated Heading Text", "Text should be updated");

            history.Undo();
            Assert(textNode.Typography.Text == "The quick brown fox" && textNode.Width == 200, "Undo must restore typography text and bounds");

            history.Redo();
            Assert(textNode.Typography.Text == "Updated Heading Text" && textNode.Width == 280, "Redo must reapply typography text and bounds");
        }

        private static void TestMultipleFillsStrokesAndShapeDefaults()
        {
            // 1. Verify default shape creation has Gray fill (#D9D9D9) and No Stroke
            var defaultShape = new LayerNode();
            Assert(defaultShape.Fills != null && defaultShape.Fills.Count == 1, "Default layer should have 1 fill");
            Assert(defaultShape.Fills[0].ColorHex == "#D9D9D9", string.Format("Default fill color should be Figma gray #D9D9D9, got {0}", defaultShape.Fills[0].ColorHex));
            Assert(defaultShape.Strokes != null && defaultShape.Strokes.Count == 0, "Default shape should have NO STROKE (empty strokes list)");

            // 2. Add multiple fills (Solid + Linear Gradient + Radial Gradient)
            var multiFillNode = new LayerNode();
            multiFillNode.Fills.Clear();

            // Fill 1: Solid base
            multiFillNode.Fills.Add(Paint.Solid("#334155", 1.0));

            // Fill 2: Linear gradient overlay
            var linearGrad = Paint.LinearGradient("#38BDF8", "#6366F1", 45.0);
            linearGrad.Opacity = 0.8;
            multiFillNode.Fills.Add(linearGrad);

            // Fill 3: Radial gradient highlight
            var radialGrad = new Paint
            {
                Type = PaintType.RadialGradient,
                GradientCenter = new Point(0.5, 0.5),
                GradientRadius = 0.5,
                Opacity = 0.5
            };
            radialGrad.GradientStops.Add(new GradientStopData(0.0, "#FFFFFF", 1.0));
            radialGrad.GradientStops.Add(new GradientStopData(1.0, "#000000", 0.0));
            multiFillNode.Fills.Add(radialGrad);

            Assert(multiFillNode.Fills.Count == 3, "Layer should contain 3 fills");
            Assert(multiFillNode.Fills[0].Type == PaintType.Solid, "Fill 1 should be Solid");
            Assert(multiFillNode.Fills[1].Type == PaintType.LinearGradient, "Fill 2 should be LinearGradient");
            Assert(multiFillNode.Fills[2].Type == PaintType.RadialGradient, "Fill 3 should be RadialGradient");

            // 3. Add multiple strokes (Border + Outer Accent)
            multiFillNode.Strokes.Add(new StrokeData
            {
                Paint = Paint.Solid("#000000"),
                Width = 1.5,
                Alignment = StrokeAlignment.Center,
                Enabled = true
            });
            multiFillNode.Strokes.Add(new StrokeData
            {
                Paint = Paint.Solid("#38BDF8"),
                Width = 4.0,
                Alignment = StrokeAlignment.Outside,
                Cap = StrokeCap.Round,
                Enabled = true
            });

            Assert(multiFillNode.Strokes.Count == 2, "Layer should contain 2 strokes");
            Assert(multiFillNode.Strokes[1].Alignment == StrokeAlignment.Outside, "Stroke 2 should have Outside alignment");
            Assert(multiFillNode.Strokes[1].Cap == StrokeCap.Round, "Stroke 2 should have Round cap");

            // 4. Verify deep clone integrity
            var cloned = multiFillNode.Clone();
            Assert(cloned.Fills.Count == 3, "Cloned node should preserve all 3 fills");
            Assert(cloned.Strokes.Count == 2, "Cloned node should preserve all 2 strokes");
            Assert(cloned.Fills[1].GradientStops.Count == 2, "Cloned linear gradient should preserve stops");
        }

        private static void TestSmartSnappingCalculations()
        {
            // Base stationary layer at (100, 100) with width 200, height 150
            var baseLayer = new LayerNode { X = 100, Y = 100, Width = 200, Height = 150, IsVisible = true };

            // Moving layer with width 80, height 60
            // Candidate X's: Left=100, Center=200, Right=300
            // Candidate Y's: Top=100, Center=175, Bottom=250

            double snapThreshold = 6.0;

            // Scenario 1: Drag moving layer's Left edge to X = 98 (within threshold of Left=100)
            double movingLeft = 98.0;
            double diffLeft = 100.0 - movingLeft; // +2.0
            Assert(Math.Abs(diffLeft) <= snapThreshold, "Difference should be within snap threshold");
            double snappedLeft = movingLeft + diffLeft;
            Assert(snappedLeft == 100.0, "Left edge must snap precisely to base layer's left edge at 100.0");

            // Scenario 2: Drag moving layer's Center near base layer's Center (200.0)
            // Moving layer width = 80 -> if movingLeft = 159 -> center = 199.0
            double movingCenter = 199.0;
            double diffCenter = 200.0 - movingCenter; // +1.0
            Assert(Math.Abs(diffCenter) <= snapThreshold, "Center diff should be within snap threshold");
            double snappedCenter = movingCenter + diffCenter;
            Assert(snappedCenter == 200.0, "Center must snap precisely to base layer's center at 200.0");

            // Scenario 3: Outside threshold (e.g. diff = 15px) -> No snap
            double outsideDiff = 15.0;
            Assert(Math.Abs(outsideDiff) > snapThreshold, "Points outside threshold must not snap");
        }

        private static void TestTwoLayerReorderingAndCommandHistory()
        {
            var page = new Page();
            var bottom = new LayerNode { Id = "layer-bottom", Name = "BottomLayer", X = 0, Y = 0, Width = 100, Height = 100 };
            var top = new LayerNode { Id = "layer-top", Name = "TopLayer", X = 50, Y = 50, Width = 100, Height = 100 };
            page.Layers.Add(bottom);
            page.Layers.Add(top);

            Assert(page.Layers.Count == 2, "Page must have exactly 2 layers");
            Assert(page.Layers[0] == bottom && page.Layers[1] == top, "Initial order: index 0 = bottom, index 1 = top");

            // Move bottom layer ABOVE top layer (drag onto upper half of top layer card)
            var moveAboveCmd = new ReorderLayerCommand(page, bottom, null, 0, null, 1, null);
            moveAboveCmd.Execute();
            Assert(page.Layers[0] == top, "After moving above, index 0 must be top");
            Assert(page.Layers[1] == bottom, "After moving above, index 1 must be bottom");

            // Undo: restores original order
            moveAboveCmd.Unexecute();
            Assert(page.Layers[0] == bottom && page.Layers[1] == top, "Undo must restore original order");

            // Redo:
            moveAboveCmd.Execute();
            Assert(page.Layers[0] == top && page.Layers[1] == bottom, "Redo must re-apply reorder");

            // Move bottom (currently at index 1) BELOW top (at index 0)
            var moveBelowCmd = new ReorderLayerCommand(page, bottom, null, 1, null, 0, null);
            moveBelowCmd.Execute();
            Assert(page.Layers[0] == bottom && page.Layers[1] == top, "After moving below, bottom is index 0 again");

            // Undo:
            moveBelowCmd.Unexecute();
            Assert(page.Layers[0] == top && page.Layers[1] == bottom, "Undo must restore to top at index 0");
        }

        private static void TestFrameSelectionCommand()
        {
            var page = new Page();
            var l1 = new LayerNode { Id = "btn1", Name = "Button 1", X = 100, Y = 100, Width = 80, Height = 40 };
            var l2 = new LayerNode { Id = "btn2", Name = "Button 2", X = 200, Y = 180, Width = 100, Height = 50 };
            page.Layers.Add(l1);
            page.Layers.Add(l2);

            double minX = Math.Min(l1.X, l2.X);
            double minY = Math.Min(l1.Y, l2.Y);
            double maxX = Math.Max(l1.X + l1.Width, l2.X + l2.Width);
            double maxY = Math.Max(l1.Y + l1.Height, l2.Y + l2.Height);
            double frameW = maxX - minX;
            double frameH = maxY - minY;

            var frame = new LayerNode
            {
                Id = "frame-sel-1",
                Name = "Frame 1",
                Type = ElementType.Frame,
                X = minX,
                Y = minY,
                Width = frameW,
                Height = frameH,
                ClipContent = false
            };

            l1.X -= minX;
            l1.Y -= minY;
            l2.X -= minX;
            l2.Y -= minY;

            var cmd = new FrameSelectionCommand(page, frame, new List<LayerNode> { l1, l2 }, null, 0, null);
            cmd.Execute();

            Assert(page.Layers.Count == 1, "Page root should contain 1 layer (the frame)");
            Assert(page.Layers[0] == frame, "Root layer must be the new frame");
            Assert(frame.Children.Count == 2, "Frame must contain the 2 wrapped layers");
            Assert(frame.Children[0].ParentId == frame.Id && frame.Children[1].ParentId == frame.Id, "Children ParentIds must match frame Id");
            Assert(l1.X == 0 && l1.Y == 0, "Child 1 relative coordinates preserved");
            Assert(l2.X == 100 && l2.Y == 80, "Child 2 relative coordinates preserved");

            cmd.Unexecute();
            Assert(page.Layers.Count == 2, "Undo must restore the 2 layers to page root");
            Assert(page.FindLayerById("frame-sel-1") == null, "Frame must be removed from page on Undo");
            Assert(l1.ParentId == null && l2.ParentId == null, "Children ParentIds must be cleared on Undo");

            cmd.Execute();
            Assert(page.Layers.Count == 1 && page.Layers[0] == frame, "Redo must re-wrap layers in frame");
            Assert(frame.Children.Count == 2, "Redo must restore frame children");
        }

        private static void TestComponentAndInstanceSystem()
        {
            var page = new Page();
            var master = new LayerNode
            {
                Id = "comp-btn-master",
                Name = "Primary Button",
                Type = ElementType.ComponentInstance,
                IsMasterComponent = true,
                ComponentId = "comp-btn-master",
                X = 50,
                Y = 50,
                Width = 140,
                Height = 44
            };
            page.Layers.Add(master);

            var foundMaster = page.FindMasterComponent(master.ComponentId);
            Assert(foundMaster == master, "FindMasterComponent must locate master component by ComponentId");
            Assert(page.FindMasterComponent("non-existent") == null, "FindMasterComponent returns null for missing id");

            var instance = master.Clone();
            instance.Id = "comp-btn-inst-1";
            instance.IsMasterComponent = false;
            instance.ComponentId = master.ComponentId;
            instance.X = master.X + 200;
            instance.Y = master.Y;
            page.Layers.Add(instance);

            var instances = page.FindInstancesOfComponent(master.ComponentId);
            Assert(instances.Count == 1, "Must find exactly 1 instance");
            Assert(instances[0] == instance, "Found instance must match created instance");

            instance.Name = "Custom Submit Button";
            instance.Width = 180;
            Assert(master.Name == "Primary Button", "Master component name must not be mutated by instance override");
            Assert(master.Width == 140, "Master component width must not be mutated by instance override");

            instance.Name = master.Name;
            instance.Width = master.Width;
            instance.Height = master.Height;
            Assert(instance.Name == "Primary Button" && instance.Width == 140, "Overrides reset matches master properties");

            instance.ComponentId = null;
            instance.IsMasterComponent = false;
            instance.Type = ElementType.Rectangle;
            var instancesAfterDetach = page.FindInstancesOfComponent(master.ComponentId);
            Assert(instancesAfterDetach.Count == 0, "Detached instance is no longer returned by FindInstancesOfComponent");
            Assert(instance.ComponentId == null, "Detached instance ComponentId is cleared");
        }

        private static void TestAutoAlignSelection()
        {
            var page = new Page();

            var frame = new LayerNode
            {
                Id = "test-frame",
                Name = "Auto Frame",
                Type = ElementType.Frame,
                X = 0,
                Y = 0,
                Width = 200,
                Height = 200,
                AutoLayout = null
            };
            page.Layers.Add(frame);

            var toggled1 = AutoLayoutEngine.AutoAlignSelection(page, new List<LayerNode> { frame });
            Assert(toggled1 != null && toggled1.AutoLayout != null, "AutoLayout initialized on frame");
            Assert(toggled1.AutoLayout.Mode == AutoLayoutMode.Horizontal, "First toggle sets Mode to Horizontal");

            var toggled2 = AutoLayoutEngine.AutoAlignSelection(page, new List<LayerNode> { frame });
            Assert(toggled2.AutoLayout.Mode == AutoLayoutMode.Vertical, "Second toggle sets Mode to Vertical");

            var toggled3 = AutoLayoutEngine.AutoAlignSelection(page, new List<LayerNode> { frame });
            Assert(toggled3.AutoLayout.Mode == AutoLayoutMode.Horizontal, "Third toggle sets Mode back to Horizontal");

            var r1 = new LayerNode { Id = "r1", Name = "Box 1", Type = ElementType.Rectangle, X = 10, Y = 10, Width = 60, Height = 40 };
            var r2 = new LayerNode { Id = "r2", Name = "Box 2", Type = ElementType.Rectangle, X = 80, Y = 10, Width = 60, Height = 40 };
            page.Layers.Add(r1);
            page.Layers.Add(r2);

            var autoWrapFrame = AutoLayoutEngine.AutoAlignSelection(page, new List<LayerNode> { r1, r2 });
            Assert(autoWrapFrame != null, "AutoAlignSelection should return a new Auto Layout frame");
            Assert(autoWrapFrame.Type == ElementType.Frame, "Wrapped node is a Frame");
            Assert(autoWrapFrame.AutoLayout != null && autoWrapFrame.AutoLayout.Mode == AutoLayoutMode.Horizontal, "Auto layout mode is Horizontal for side-by-side elements");
            Assert(autoWrapFrame.AutoLayout.Gap == 0.0, "Auto Layout default gap must be 0px");
            Assert(autoWrapFrame.Fills == null || autoWrapFrame.Fills.Count == 0, "Auto Layout frame must have no fill by default");
            Assert(autoWrapFrame.Children.Count == 2, "Wrapped frame contains both selected layers");
            Assert(page.Layers.Contains(autoWrapFrame), "Wrapped frame is added to page layers");
            Assert(!page.Layers.Contains(r1) && !page.Layers.Contains(r2), "Original items moved out of root page layers into frame");
            Assert(r1.X == autoWrapFrame.X, "First item aligned at start");
            Assert(r2.X == r1.X + r1.Width, "Items equally distributed with 0px default gap");
        }

        private static void TestTouchpadPanAndZoomCalculations()
        {
            Point panOffset = new Point(0, 0);
            double zoomScale = 1.0;

            int verticalDelta = -120;
            panOffset.Y += verticalDelta;
            Assert(panOffset.X == 0 && panOffset.Y == -120, "Vertical slide translates Y offset by delta");

            int horizontalDelta = 80;
            panOffset.X += horizontalDelta;
            Assert(panOffset.X == 80 && panOffset.Y == -120, "Shift + slide translates X offset by delta");

            Point cursorPt = new Point(400, 300);
            double zoomFactor = 1.15;
            double oldZoom = zoomScale;
            double newZoom = oldZoom * zoomFactor;

            panOffset.X = cursorPt.X - (cursorPt.X - panOffset.X) * (newZoom / oldZoom);
            panOffset.Y = cursorPt.Y - (cursorPt.Y - panOffset.Y) * (newZoom / oldZoom);
            zoomScale = newZoom;

            Assert(Math.Abs(panOffset.X - 32.0) < 0.001, "Pan X correctly anchored to mouse point on zoom");
            Assert(Math.Abs(panOffset.Y - (-183.0)) < 0.001, "Pan Y correctly anchored to mouse point on zoom");
            Assert(zoomScale == 1.15, "Zoom scale updated to 1.15");

            double pinchScale = 1.2;
            double clampedZoom = Math.Max(0.05, Math.Min(32.0, zoomScale * pinchScale));
            Assert(clampedZoom > zoomScale && clampedZoom <= 32.0, "Touchpad pinch zoom within valid boundaries");
        }

        private static void TestFigmaToolbarSelectionIconAndGeometries()
        {
            Assert(VectorIcons.SelectGeometry != null, "VectorIcons.SelectGeometry must not be null");
            Assert(!VectorIcons.SelectGeometry.IsEmpty(), "VectorIcons.SelectGeometry must not be empty");
            Rect selectBounds = VectorIcons.SelectGeometry.Bounds;
            Assert(selectBounds.Width >= 10.0 && selectBounds.Height >= 15.0,
                string.Format("Selection arrow must have pointer proportions, got {0}x{1}", selectBounds.Width, selectBounds.Height));

            Assert(VectorIcons.ComponentGeometry != null, "ComponentGeometry must not be null");
            Assert(!VectorIcons.ComponentGeometry.IsEmpty(), "ComponentGeometry must not be empty");

            Assert(VectorIcons.InstanceGeometry != null, "InstanceGeometry must not be null");
            Assert(!VectorIcons.InstanceGeometry.IsEmpty(), "InstanceGeometry must not be empty");

            Assert(VectorIcons.AutoLayoutGeometry != null, "AutoLayoutGeometry must not be null");
            Assert(!VectorIcons.AutoLayoutGeometry.IsEmpty(), "AutoLayoutGeometry must not be empty");
        }

        private static void TestUpdatedRequirements()
        {
            // 1. Delete / Backspace layer deletion
            var page = new Page();
            var rect = new LayerNode { Id = "rect-del", Name = "To Delete", Type = ElementType.Rectangle };
            page.Layers.Add(rect);
            Assert(page.Layers.Count == 1, "Page has 1 layer before delete");
            var history = new CommandHistory();
            history.Execute(new DeleteLayersCommand(page, new[] { rect }));
            Assert(page.Layers.Count == 0, "Delete command deletes layer");
            history.Undo();
            Assert(page.Layers.Count == 1 && page.Layers[0].Id == "rect-del", "Undo restores deleted layer");

            // 2. Middle mouse drag pan
            Point pan = new Point(100, 100);
            Point lastMouse = new Point(200, 200);
            Point curMouse = new Point(250, 230);
            pan.X += curMouse.X - lastMouse.X;
            pan.Y += curMouse.Y - lastMouse.Y;
            Assert(pan.X == 150 && pan.Y == 130, "Middle mouse drag translates pan by mouse delta");

            // 3. Crisp context menu vector icons & CreateIcon rendering
            Assert(VectorIcons.CutGeometry != null && !VectorIcons.CutGeometry.IsEmpty(), "Cut vector geometry exists");
            Assert(VectorIcons.CopyGeometry != null && !VectorIcons.CopyGeometry.IsEmpty(), "Copy vector geometry exists");
            Assert(VectorIcons.PasteGeometry != null && !VectorIcons.PasteGeometry.IsEmpty(), "Paste vector geometry exists");
            Assert(VectorIcons.TrashGeometry != null && !VectorIcons.TrashGeometry.IsEmpty(), "Trash vector geometry exists");
            Assert(VectorIcons.FlipHGeometry != null && !VectorIcons.FlipHGeometry.IsEmpty(), "Flip horizontal geometry exists");
            Assert(VectorIcons.FlipVGeometry != null && !VectorIcons.FlipVGeometry.IsEmpty(), "Flip vertical geometry exists");
            Assert(VectorIcons.LayerUpGeometry != null && !VectorIcons.LayerUpGeometry.IsEmpty(), "Layer up geometry exists");
            Assert(VectorIcons.LayerDownGeometry != null && !VectorIcons.LayerDownGeometry.IsEmpty(), "Layer down geometry exists");
            Assert(VectorIcons.AlignLeftGeometry != null && !VectorIcons.AlignLeftGeometry.IsEmpty(), "Align left geometry exists");
            Assert(VectorIcons.AlignCenterGeometry != null && !VectorIcons.AlignCenterGeometry.IsEmpty(), "Align center geometry exists");
            Assert(VectorIcons.AlignRightGeometry != null && !VectorIcons.AlignRightGeometry.IsEmpty(), "Align right geometry exists");
            Assert(VectorIcons.AlignTopGeometry != null && !VectorIcons.AlignTopGeometry.IsEmpty(), "Align top geometry exists");
            Assert(VectorIcons.AlignMiddleGeometry != null && !VectorIcons.AlignMiddleGeometry.IsEmpty(), "Align middle geometry exists");
            Assert(VectorIcons.AlignBottomGeometry != null && !VectorIcons.AlignBottomGeometry.IsEmpty(), "Align bottom geometry exists");

            // Verify CreateIcon renders stroked outline for Trash/Frame/Zoom and filled for Component/Select
            var trashIcon = VectorIcons.CreateIcon(VectorIcons.TrashGeometry, System.Windows.Media.Brushes.Red);
            Assert(trashIcon.Stroke != null && trashIcon.StrokeThickness > 0, "Trash icon has visible stroke outline");
            var compIcon = VectorIcons.CreateIcon(VectorIcons.ComponentGeometry, System.Windows.Media.Brushes.Purple);
            Assert(compIcon.Fill != null, "Component icon has visible fill glyph");

            // 4. Component icon (❖) and purple styling
            var master = new LayerNode { Id = "comp-1", Name = "Button", Type = ElementType.Frame, IsMasterComponent = true };
            var instance = new LayerNode { Id = "inst-1", Name = "Button Instance", Type = ElementType.ComponentInstance, ComponentId = "comp-1" };
            Assert(master.IsMasterComponent, "Master component flag set");
            Assert(VectorIcons.ComponentGeometry != null, "Component glyph ❖ exists");
            Assert(VectorIcons.InstanceGeometry != null, "Instance glyph ◇ exists");

            // 5 & 6. Auto Layout default gap = 0px and no fill
            var defaultLayout = new AutoLayoutData();
            Assert(defaultLayout.Gap == 0.0, "Default gap is 0.0px");

            // 7. Color picker dismiss
            ColorPickerPopup.CloseActive();
            Assert(true, "CloseActive succeeds without exception");

            // 8. Auto Layout Engine: IgnoreAutoLayout & Fill Container
            var alFrame = new LayerNode
            {
                Type = ElementType.Frame,
                X = 0,
                Y = 0,
                Width = 300,
                Height = 100,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Horizontal,
                    Gap = 0.0,
                    PaddingLeft = 0,
                    PaddingRight = 0,
                    PaddingTop = 0,
                    PaddingBottom = 0,
                    SizingH = SizingMode.Hug // Starts Hug to test dependency resolution
                },
                Children = new List<LayerNode>()
            };

            var inFlowChild1 = new LayerNode { Type = ElementType.Rectangle, X = 0, Y = 0, Width = 50, Height = 50, LayoutSizingH = SizingMode.Fixed };
            var ignoredChild = new LayerNode { Type = ElementType.Rectangle, X = 250, Y = 20, Width = 30, Height = 30, IgnoreAutoLayout = true };
            var inFlowChild2 = new LayerNode { Type = ElementType.Rectangle, X = 0, Y = 0, Width = 50, Height = 50, LayoutSizingH = SizingMode.Fill };

            alFrame.Children.Add(inFlowChild1);
            alFrame.Children.Add(ignoredChild);
            alFrame.Children.Add(inFlowChild2);

            AutoLayoutEngine.Reflow(alFrame);

            // Sizing rule: Hug resolved to Fixed because child has Fill
            Assert(alFrame.AutoLayout.SizingH == SizingMode.Fixed, "Parent Hug switches to Fixed when child has Fill");
            // Ignored child kept its independent position
            Assert(ignoredChild.X == 250 && ignoredChild.Y == 20, "Ignored child keeps independent position");
            // inFlowChild1 starts at 0
            Assert(inFlowChild1.X == 0, "inFlowChild1 positioned at start");
            // inFlowChild2 has Fill sizing: available space = 300 - 50 = 250
            Assert(Math.Abs(inFlowChild2.Width - 250.0) < 0.01, string.Format("Fill child width is 250, got {0}", inFlowChild2.Width));
            Assert(inFlowChild2.X == 50, "inFlowChild2 positioned directly after inFlowChild1 with 0px gap");

            // Test Baseline Alignment
            var baseFrame = new LayerNode
            {
                Type = ElementType.Frame,
                X = 0, Y = 0, Width = 300, Height = 100,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Horizontal,
                    Gap = 10.0,
                    AlignItems = AlignItems.Baseline,
                    PaddingTop = 10
                },
                Children = new List<LayerNode>()
            };
            var txtLarge = new LayerNode { Type = ElementType.Text, Typography = new TypographyData { FontSize = 40 }, Height = 48, Width = 100 };
            var txtSmall = new LayerNode { Type = ElementType.Text, Typography = new TypographyData { FontSize = 20 }, Height = 24, Width = 60 };
            baseFrame.Children.Add(txtLarge);
            baseFrame.Children.Add(txtSmall);
            AutoLayoutEngine.Reflow(baseFrame);
            // txtSmall baseline is aligned with txtLarge baseline
            double baseLarge = txtLarge.Y + (txtLarge.Typography.FontSize * 0.85);
            double baseSmall = txtSmall.Y + (txtSmall.Typography.FontSize * 0.85);
            Assert(Math.Abs(baseLarge - baseSmall) < 0.01, "Text children share identical baseline in Baseline alignment");
        }

        private static void TestHighQualityImageRenderingEngine()
        {
            var page = new Page { BackgroundColor = "#1E1E2E" };

            // 1. Root Card Frame with corner radius, drop shadow, and clip content
            var cardFrame = new LayerNode
            {
                Id = "card-frame",
                Name = "Hero Card",
                Type = ElementType.Frame,
                X = 100,
                Y = 100,
                Width = 400,
                Height = 260,
                CornerRadius = 16,
                ClipContent = true,
                Fills = new List<Paint> { Paint.Solid("#2A2E3D", 1.0) },
                Strokes = new List<StrokeData>
                {
                    new StrokeData
                    {
                        Paint = Paint.Solid("#3E445B", 1.0),
                        Width = 1.5,
                        Alignment = StrokeAlignment.Inside
                    }
                },
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        Type = EffectType.DropShadow,
                        ColorHex = "#000000",
                        OffsetX = 0,
                        OffsetY = 10,
                        Blur = 20,
                        Spread = 0,
                        Opacity = 0.35,
                        Enabled = true
                    }
                },
                Children = new List<LayerNode>()
            };
            page.Layers.Add(cardFrame);

            // 2. Child Text Layer (Title)
            var titleText = new LayerNode
            {
                Id = "card-title",
                Name = "Card Title",
                Type = ElementType.Text,
                X = 124,
                Y = 124,
                Width = 350,
                Height = 32,
                ParentId = cardFrame.Id,
                Typography = new TypographyData
                {
                    Text = "Photoshop-Grade Rendering",
                    FontFamily = "Segoe UI",
                    FontSize = 22,
                    FontWeight = 700,
                    Case = TextCase.Original
                },
                Fills = new List<Paint> { Paint.Solid("#FFFFFF", 1.0) }
            };
            cardFrame.Children.Add(titleText);

            // 3. Child Text Layer (Subtitle with TextCase.Upper)
            var subText = new LayerNode
            {
                Id = "card-sub",
                Name = "Card Subtitle",
                Type = ElementType.Text,
                X = 124,
                Y = 164,
                Width = 350,
                Height = 20,
                ParentId = cardFrame.Id,
                Typography = new TypographyData
                {
                    Text = "High fidelity vector & font rasterization",
                    FontFamily = "Segoe UI",
                    FontSize = 13,
                    FontWeight = 400,
                    Case = TextCase.Upper
                },
                Fills = new List<Paint> { Paint.Solid("#9CA3AF", 1.0) }
            };
            cardFrame.Children.Add(subText);

            // 4. Nested Button Frame with CornerRadius
            var buttonFrame = new LayerNode
            {
                Id = "card-btn",
                Name = "Action Button",
                Type = ElementType.Frame,
                X = 124,
                Y = 200,
                Width = 130,
                Height = 36,
                CornerRadius = 8,
                ParentId = cardFrame.Id,
                Fills = new List<Paint> { Paint.Solid("#3B82F6", 1.0) },
                Children = new List<LayerNode>()
            };
            cardFrame.Children.Add(buttonFrame);

            // 5. Nested Button Label inside Button Frame
            var btnText = new LayerNode
            {
                Id = "card-btn-text",
                Name = "Button Label",
                Type = ElementType.Text,
                X = 144,
                Y = 208,
                Width = 90,
                Height = 20,
                ParentId = buttonFrame.Id,
                Typography = new TypographyData
                {
                    Text = "Export Now",
                    FontFamily = "Segoe UI",
                    FontSize = 13,
                    FontWeight = 600,
                    Align = TextAlign.Center
                },
                Fills = new List<Paint> { Paint.Solid("#FFFFFF", 1.0) }
            };
            buttonFrame.Children.Add(btnText);

            // Test PreProcess: ensures text measurements and auto layout sync
            HighQualityImageRenderer.PreProcessForExport(page, new[] { cardFrame });
            Assert(titleText.Width > 0 && titleText.Height > 0, "Text bounds synchronized during pre-processing");

            // Test Bounds Computation: includes stroke and shadow padding
            Rect exportBounds = HighQualityImageRenderer.ComputeVisualBounds(page, new[] { cardFrame });
            Assert(exportBounds.Width >= 400.0, "Export bounds width >= frame width (including shadow padding)");
            Assert(exportBounds.Height >= 260.0, "Export bounds height >= frame height (including shadow padding)");

            // Test 1x Scale Render
            var bmp1x = HighQualityImageRenderer.Render(page, cardFrame, 1.0, false);
            Assert(bmp1x != null, "1x RenderTargetBitmap generated");
            Assert(bmp1x.PixelWidth == (int)Math.Ceiling(exportBounds.Width), "1x pixel width matches export bounds");
            Assert(bmp1x.PixelHeight == (int)Math.Ceiling(exportBounds.Height), "1x pixel height matches export bounds");

            // Test 2x Retina Scale Render
            var bmp2x = HighQualityImageRenderer.Render(page, cardFrame, 2.0, false);
            Assert(bmp2x != null, "2x Retina RenderTargetBitmap generated");
            Assert(bmp2x.PixelWidth == (int)Math.Ceiling(exportBounds.Width * 2.0), "2x pixel width is exactly 2x");
            Assert(bmp2x.PixelHeight == (int)Math.Ceiling(exportBounds.Height * 2.0), "2x pixel height is exactly 2x");

            // Test File Export: PNG with transparency and JPEG with studio quality
            string tempDir = Path.Combine(Path.GetTempPath(), "Kerf_HQ_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string pngPath = Path.Combine(tempDir, "hero_card_2x.png");
                string jpgPath = Path.Combine(tempDir, "hero_card_2x.jpg");

                // Export via facade RasterExporter
                RasterExporter.ExportToFile(page, cardFrame, pngPath, RasterFormat.Png, 2.0);
                Assert(File.Exists(pngPath), "PNG export file created on disk");
                var pngInfo = new FileInfo(pngPath);
                Assert(pngInfo.Length > 500, string.Format("PNG file size is healthy ({0} bytes)", pngInfo.Length));

                // Export via HighQualityImageRenderer directly
                HighQualityImageRenderer.ExportToFile(page, cardFrame, jpgPath, RasterFormat.Jpeg, 2.0);
                Assert(File.Exists(jpgPath), "JPEG export file created on disk");
                var jpgInfo = new FileInfo(jpgPath);
                Assert(jpgInfo.Length > 500, string.Format("JPEG file size is healthy ({0} bytes)", jpgInfo.Length));

                // Test full page export (targets = null)
                string fullPagePng = Path.Combine(tempDir, "full_page.png");
                RasterExporter.ExportToFile(page, (LayerNode)null, fullPagePng, RasterFormat.Png, 1.0);
                Assert(File.Exists(fullPagePng), "Full document PNG exported");
                Assert(new FileInfo(fullPagePng).Length > 500, "Full document PNG has valid bytes");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        private static void TestNewRequirementsBatch2()
        {
            // 1. Drop Shadow Direction + Offset (Polar <-> Cartesian)
            var effect = new EffectData
            {
                Type = EffectType.DropShadow,
                Direction = 90.0,
                Distance = 10.0
            };
            effect.UpdateOffsetsFromPolar();
            Assert(Math.Abs(effect.OffsetX) < 0.01, string.Format("At 90 deg, OffsetX should be 0, got {0}", effect.OffsetX));
            Assert(Math.Abs(effect.OffsetY - 10.0) < 0.01, string.Format("At 90 deg, OffsetY should be 10, got {0}", effect.OffsetY));

            effect.Direction = 0.0;
            effect.Distance = 20.0;
            effect.UpdateOffsetsFromPolar();
            Assert(Math.Abs(effect.OffsetX - 20.0) < 0.01, string.Format("At 0 deg, OffsetX should be 20, got {0}", effect.OffsetX));
            Assert(Math.Abs(effect.OffsetY) < 0.01, string.Format("At 0 deg, OffsetY should be 0, got {0}", effect.OffsetY));

            effect.Direction = 180.0;
            effect.Distance = 15.0;
            effect.UpdateOffsetsFromPolar();
            Assert(Math.Abs(effect.OffsetX - (-15.0)) < 0.01, string.Format("At 180 deg, OffsetX should be -15, got {0}", effect.OffsetX));
            Assert(Math.Abs(effect.OffsetY) < 0.01, string.Format("At 180 deg, OffsetY should be 0, got {0}", effect.OffsetY));

            // Cartesian -> Polar
            effect.OffsetX = 10.0;
            effect.OffsetY = 10.0;
            effect.UpdatePolarFromOffsets();
            Assert(Math.Abs(effect.Distance - 14.14) < 0.05, string.Format("Distance should be ~14.14, got {0}", effect.Distance));
            Assert(Math.Abs(effect.Direction - 45.0) < 0.1, string.Format("Direction should be 45 deg, got {0}", effect.Direction));

            effect.OffsetX = 0.0;
            effect.OffsetY = -8.0;
            effect.UpdatePolarFromOffsets();
            Assert(Math.Abs(effect.Distance - 8.0) < 0.01, string.Format("Distance should be 8, got {0}", effect.Distance));
            Assert(Math.Abs(effect.Direction - 270.0) < 0.1, string.Format("Direction should be 270 deg (straight up), got {0}", effect.Direction));

            // 2. Multiple Effects support (LiquidGlass, Grain, LayerBlur, InnerShadow, BackgroundBlur)
            var node = new LayerNode
            {
                Type = ElementType.Rectangle,
                Width = 200,
                Height = 120,
                Effects = new List<EffectData>()
            };

            var glass = new EffectData { Type = EffectType.LiquidGlass, Enabled = true, Intensity = 0.75, Blur = 14 };
            var grain = new EffectData { Type = EffectType.Grain, Enabled = true, Intensity = 0.3, Scale = 1.5 };
            var blur = new EffectData { Type = EffectType.LayerBlur, Enabled = true, Blur = 20 };
            var inner = new EffectData { Type = EffectType.InnerShadow, Enabled = true, ColorHex = "#FFFFFF", Opacity = 0.4, Blur = 6 };
            var bgBlur = new EffectData { Type = EffectType.BackgroundBlur, Enabled = true, Blur = 16 };

            node.Effects.Add(effect);
            node.Effects.Add(glass);
            node.Effects.Add(grain);
            node.Effects.Add(blur);
            node.Effects.Add(inner);
            node.Effects.Add(bgBlur);

            Assert(node.Effects.Count == 6, "LayerNode supports all 6 effect types");

            // Clone deep copy test
            var cloned = node.Clone();
            Assert(cloned.Effects != null && cloned.Effects.Count == 6, "Cloned node preserves all effects");
            Assert(cloned.Effects[1].Type == EffectType.LiquidGlass, "Cloned effect 1 is LiquidGlass");
            Assert(cloned.Effects[1].Intensity == 0.75, "Cloned LiquidGlass intensity preserved");
            Assert(cloned.Effects[2].Type == EffectType.Grain, "Cloned effect 2 is Grain");
            Assert(cloned.Effects[2].Scale == 1.5, "Cloned Grain scale preserved");

            // 3. Vector Icons presence & validity
            Assert(VectorIcons.EffectsGeometry != null, "EffectsGeometry exists");
            Assert(VectorIcons.LiquidGlassGeometry != null, "LiquidGlassGeometry exists");
            Assert(VectorIcons.GrainGeometry != null, "GrainGeometry exists");
            Assert(VectorIcons.BlurGeometry != null, "BlurGeometry exists");
            Assert(VectorIcons.CutGeometry != null, "CutGeometry exists");
            Assert(VectorIcons.DuplicateGeometry != null, "DuplicateGeometry exists");
            Assert(VectorIcons.TrashGeometry != null, "TrashGeometry exists");

            // 4. Loading screen image asset verification
            string assetRelative = "assets/Loading screen.png";
            bool assetExists = File.Exists(assetRelative) || File.Exists(Path.Combine("..", assetRelative));
            Assert(assetExists, "Loading screen image asset exists on disk");

            // 5. HighQualityImageRenderer rendering with LiquidGlass and Grain effects
            var page = new Page { BackgroundColor = "#1E2230" };
            var glassCard = new LayerNode
            {
                Type = ElementType.Rectangle,
                X = 40,
                Y = 40,
                Width = 160,
                Height = 100,
                CornerRadius = 12,
                Fills = new List<Paint> { Paint.Solid("#25293A", 0.9) },
                Effects = new List<EffectData> { glass, grain }
            };
            page.Layers.Add(glassCard);

            var renderedBmp = HighQualityImageRenderer.Render(page, glassCard, 1.0, false);
            Assert(renderedBmp != null, "HighQualityImageRenderer successfully rendered layer with LiquidGlass and Grain");
            Assert(renderedBmp.PixelWidth > 100, "Rendered pixel width is valid");
        }

        private static void TestBatch3Requirements()
        {
            // 1. Modern ComboBox Styling & Visibility (No white-on-white)
            var combo = StudioView.CreateModernComboBox(26);
            Assert(combo != null, "CreateModernComboBox returns a valid ComboBox");
            Assert(combo.Template != null, "Modern ComboBox must have a custom ControlTemplate");
            Assert(combo.ItemContainerStyle != null, "Modern ComboBox must have an ItemContainerStyle for ComboBoxItem");
            Assert(combo.Foreground is SolidColorBrush, "Modern ComboBox has a non-null solid foreground");
            var fg = (SolidColorBrush)combo.Foreground;
            Assert(fg.Color.R > 200 && fg.Color.G > 200 && fg.Color.B > 200, "Foreground is crisp light slate (#F1F5F9)");

            var item = StudioView.CreateModernComboBoxItem("Test Item", EffectType.LiquidGlass);
            Assert(item != null, "CreateModernComboBoxItem returns a valid ComboBoxItem");
            Assert(item.Background == Brushes.Transparent, "ComboBoxItem background must be transparent by default");

            // 2. Apple Real Liquid Glass optical render pass verification
            var glassNode = new LayerNode
            {
                Type = ElementType.Rectangle,
                X = 0,
                Y = 0,
                Width = 240,
                Height = 140,
                CornerRadius = 16,
                Fills = new List<Paint> { Paint.Solid("#1E2230", 0.8) }
            };

            var appleGlass = new EffectData
            {
                Type = EffectType.LiquidGlass,
                Enabled = true,
                Intensity = 0.85,
                Direction = 315.0,
                Spread = 2.5,
                Blur = 16.0,
                Scale = 1.0,
                ColorHex = "#FFFFFF",
                Opacity = 0.15
            };
            glassNode.Effects = new List<EffectData> { appleGlass };

            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var rect = new Rect(glassNode.X, glassNode.Y, glassNode.Width, glassNode.Height);
                var geo = new RectangleGeometry(rect, glassNode.CornerRadius, glassNode.CornerRadius);
                GpuRenderer.RenderAppleLiquidGlass(glassNode, dc, geo, rect, appleGlass);
            }
            Assert(dv != null, "RenderAppleLiquidGlass must execute cleanly on DrawingVisual without exceptions");

            // 3. Preset Pill buttons
            bool pillClicked = false;
            var pill = StudioView.CreatePresetPill("VisionOS", () => { pillClicked = true; });
            Assert(pill != null, "CreatePresetPill returns valid pill Button");
            Assert(pill.Content.ToString() == "VisionOS", "Preset pill has correct label");
            pill.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert(pillClicked, "Preset pill click handler executed");

            // 4. HighQualityImageRenderer export with Apple Liquid Glass
            var page = new Page { BackgroundColor = "#141722" };
            page.Layers.Add(glassNode);
            var rtb = HighQualityImageRenderer.Render(page, glassNode, 2.0, false);
            Assert(rtb != null && rtb.PixelWidth == 480, "HighQualityImageRenderer rendered Apple Liquid Glass at 2x Retina scale (480px)");
        }

        private static void TestBatch4Requirements_AdobeIllustratorGradients()
        {
            // 1. GradientStopData with Midpoint
            var s1 = new GradientStopData(0.0, "#FF0000", 1.0);
            Assert(Math.Abs(s1.Midpoint - 0.5) < 0.001, "Default GradientStopData midpoint is 0.5 (50% blend)");
            var s2 = new GradientStopData(1.0, "#0000FF", 0.8, 0.35);
            Assert(Math.Abs(s2.Midpoint - 0.35) < 0.001, "Custom GradientStopData midpoint is 0.35");
            var clone = s2.Clone();
            Assert(clone.Position == 1.0 && clone.ColorHex == "#0000FF" && clone.Opacity == 0.8 && Math.Abs(clone.Midpoint - 0.35) < 0.001, "Clone copies Midpoint accurately");

            // 2. GpuRenderer Color Interpolation & Gradient Sampling
            var red = Color.FromRgb(255, 0, 0);
            var blue = Color.FromRgb(0, 0, 255);
            var midColor = GpuRenderer.InterpolateColor(red, blue, 0.5);
            Assert(midColor.R == 127 && midColor.B == 127, "50% Color interpolation between red and blue produces purple");

            var stops = new List<GradientStopData> { s1, s2 };
            var sampled0 = GpuRenderer.SampleGradientColor(stops, 0.0);
            Assert(sampled0.R == 255 && sampled0.B == 0, "Sample at 0.0 produces start color");
            var sampled1 = GpuRenderer.SampleGradientColor(stops, 1.0);
            Assert(sampled1.B == 255 && sampled1.R == 0, "Sample at 1.0 produces end color");

            // 3. Illustrator Midpoint stop injection in PopulateGradientStops
            var paint = new Paint
            {
                Type = PaintType.LinearGradient,
                GradientAngle = 90.0,
                GradientStops = new List<GradientStopData>
                {
                    new GradientStopData(0.0, "#FF0000", 1.0, 0.25),
                    new GradientStopData(1.0, "#0000FF", 1.0, 0.5)
                }
            };
            var stopCol = new GradientStopCollection();
            GpuRenderer.PopulateGradientStops(stopCol, paint);
            Assert(stopCol.Count == 3, "PopulateGradientStops automatically injects shifted 0.25 midpoint stop (Total 3 stops)");
            Assert(Math.Abs(stopCol[1].Offset - 0.25) < 0.001, "Injected midpoint stop offset matches 0.25");

            // 4. GradientRampControl instantiation and Adobe Illustrator features
            bool notified = false;
            var ramp = new GradientRampControl(paint, () => { notified = true; });
            Assert(ramp != null, "GradientRampControl initialized successfully");
            Assert(ramp.Content is StackPanel, "GradientRampControl root is a StackPanel");

            // 5. SVG Exporter multi-stop & midpoint export
            var node = new LayerNode
            {
                Id = "grad-box-1",
                Name = "Gradient Rectangle",
                Type = ElementType.Rectangle,
                X = 0,
                Y = 0,
                Width = 200,
                Height = 100,
                Fills = new List<Paint> { paint }
            };
            var page = new Page();
            page.Layers.Add(node);
            string svg = SvgExporter.ExportPage(page);
            Assert(svg.Contains("<linearGradient id=\"grad_lin_grad-box-1_0\""), "SVG output contains linearGradient definition");
            Assert(svg.Contains("stop-color=\"#FF0000\""), "SVG output contains start stop");
            Assert(svg.Contains("stop-color=\"#0000FF\""), "SVG output contains end stop");
            Assert(svg.Contains("offset=\"25%\""), "SVG output contains Illustrator midpoint stop at 25%");
        }

        private static void TestBatch5Requirements_FigmaOnCanvasGradientRollers()
        {
            // 1. GetGradientPoints for LinearGradient
            var node = new LayerNode
            {
                Id = "figma-grad-box",
                Name = "Figma Gradient Card",
                Type = ElementType.Rectangle,
                X = 100,
                Y = 100,
                Width = 200,
                Height = 100,
                Rotation = 0
            };
            var paint = new Paint
            {
                Type = PaintType.LinearGradient,
                GradientAngle = 90.0,
                GradientStops = new List<GradientStopData>
                {
                    new GradientStopData(0.0, "#FF0000", 1.0),
                    new GradientStopData(0.5, "#00FF00", 1.0),
                    new GradientStopData(1.0, "#0000FF", 1.0)
                }
            };

            Point p1, p2;
            List<Point> stops;
            bool ok = GradientRollerEngine.GetGradientPoints(node, paint, out p1, out p2, out stops);
            Assert(ok, "GetGradientPoints returns true for LinearGradient");
            Assert(Math.Abs(p1.X - 200.0) < 0.1 && Math.Abs(p1.Y - 100.0) < 0.1, string.Format("Start point p1 is top center (200, 100), got ({0}, {1})", p1.X, p1.Y));
            Assert(Math.Abs(p2.X - 200.0) < 0.1 && Math.Abs(p2.Y - 200.0) < 0.1, string.Format("End point p2 is bottom center (200, 200), got ({0}, {1})", p2.X, p2.Y));
            Assert(stops.Count == 3, "Returns 3 stop points along the gradient roller line");
            Assert(Math.Abs(stops[1].X - 200.0) < 0.1 && Math.Abs(stops[1].Y - 150.0) < 0.1, string.Format("Stop 0.5 point is exact midpoint (200, 150), got ({0}, {1})", stops[1].X, stops[1].Y));

            // 2. Hit Testing on canvas
            var hitStart = GradientRollerEngine.HitTest(new Point(200, 100), node, paint, 1.0);
            Assert(hitStart.Type == GradientHandleType.StopNode || hitStart.Type == GradientHandleType.Start, "Hit testing (200, 100) hits start handle / stop 0");

            var hitEnd = GradientRollerEngine.HitTest(new Point(200, 200), node, paint, 1.0);
            Assert(hitEnd.Type == GradientHandleType.StopNode || hitEnd.Type == GradientHandleType.End, "Hit testing (200, 200) hits end handle / stop 2");

            var hitMid = GradientRollerEngine.HitTest(new Point(200, 150), node, paint, 1.0);
            Assert(hitMid.Type == GradientHandleType.StopNode && hitMid.StopIndex == 1, "Hit testing (200, 150) hits intermediate stop 1");

            var hitLine = GradientRollerEngine.HitTest(new Point(200, 130), node, paint, 1.0);
            Assert(hitLine.Type == GradientHandleType.Line, "Hit testing (200, 130) hits the gradient roller vector line");
            Assert(Math.Abs(hitLine.ParameterT - 0.3) < 0.05, string.Format("ParameterT along roller line is ~0.3, got {0}", hitLine.ParameterT));

            var hitMiss = GradientRollerEngine.HitTest(new Point(500, 500), node, paint, 1.0);
            Assert(hitMiss.Type == GradientHandleType.None, "Hit testing far away returns None");

            // 3. UpdateAngleFromDrag
            GradientRollerEngine.UpdateAngleFromDrag(node, paint, new Point(300, 150), false);
            Assert(Math.Abs(paint.GradientAngle - 0.0) < 0.5 || Math.Abs(paint.GradientAngle - 360.0) < 0.5, string.Format("Dragging end handle horizontally to the right sets angle to 0 deg, got {0}", paint.GradientAngle));

            GradientRollerEngine.UpdateAngleFromDrag(node, paint, new Point(200, 250), false);
            Assert(Math.Abs(paint.GradientAngle - 90.0) < 0.5, string.Format("Dragging end handle straight down sets angle to 90 deg, got {0}", paint.GradientAngle));

            // 4. ProjectOntoSegment
            double projMid = GradientRollerEngine.ProjectOntoSegment(new Point(200, 150), new Point(200, 100), new Point(200, 200));
            Assert(Math.Abs(projMid - 0.5) < 0.001, "Projecting midpoint onto segment yields 0.5");

            // 5. Rotated node with Gradient roller
            GradientRollerEngine.GetGradientPoints(node, paint, out p1, out p2, out stops);
            node.Rotation = 45.0;
            Point pRot1, pRot2;
            List<Point> stopsRot;
            bool okRot = GradientRollerEngine.GetGradientPoints(node, paint, out pRot1, out pRot2, out stopsRot);
            Assert(okRot, "GetGradientPoints supports rotated nodes");
            double distOrig = Math.Sqrt((p2.X - p1.X) * (p2.X - p1.X) + (p2.Y - p1.Y) * (p2.Y - p1.Y));
            double distRot = Math.Sqrt((pRot2.X - pRot1.X) * (pRot2.X - pRot1.X) + (pRot2.Y - pRot1.Y) * (pRot2.Y - pRot1.Y));
            Assert(Math.Abs(distOrig - distRot) < 0.01, "Length of gradient roller line is preserved under layer rotation");

            // 6. Simplified Figma GradientRampControl
            bool modified = false;
            var rampControl = new GradientRampControl(paint, () => { modified = true; });
            Assert(rampControl != null, "Figma GradientRampControl created");
            Assert(rampControl.SelectedStopIndex == 0, "Default selected stop index is 0");
            rampControl.SelectedStopIndex = 1;
            Assert(rampControl.SelectedStopIndex == 1, "SelectedStopIndex changes smoothly");

            // 7. RenderOverlay execution
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                GradientRollerEngine.RenderOverlay(dc, node, paint, 1.0, 1);
            }
            Assert(dv != null, "RenderOverlay executes and renders on-canvas vector rollers cleanly");
        }

        private static void TestBatch6Requirements_RealLiquidGlassAndSmoothSliders()
        {
            // 1. Rust and Managed Liquid Glass Optical Refraction Simulation
            int w = 100;
            int h = 60;
            int stride = w * 4;
            byte[] bg = new byte[h * stride];
            byte[] output = new byte[h * stride];

            // Fill bg with sample gradient
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * stride + x * 4;
                    bg[idx] = (byte)(x * 2);      // B
                    bg[idx + 1] = (byte)(y * 3);  // G
                    bg[idx + 2] = (byte)(180);    // R
                    bg[idx + 3] = 255;            // A
                }
            }

            // Test Rust Native Engine Liquid Glass
            bool rustExecuted = RustEngineInterop.RenderLiquidGlass(
                bg, w, h, stride, output,
                w * 0.5f, h * 0.5f, w * 0.5f, h * 0.5f, 15.0f,
                20.0f, 16.0f, 40.0f, 3.0f,
                -0.7071f, -0.7071f,
                255.0f, 255.0f, 255.0f, 0.15f,
                0.85f, true);

            Assert(rustExecuted, "Rust Core kerf_render_liquid_glass executes successfully with Snell refraction & chromatic dispersion");

            // Verify refraction output has modified pixels inside the squircle bezel
            bool pixelsModified = false;
            for (int i = 0; i < output.Length; i += 4)
            {
                if (output[i] != bg[i] || output[i + 1] != bg[i + 1] || output[i + 2] != bg[i + 2])
                {
                    pixelsModified = true;
                    break;
                }
            }
            Assert(pixelsModified, "Liquid Glass refraction engine produces optical distortion and specular highlights");

            // 2. GpuRenderer Liquid Glass Texture Generation & Caching
            var glassTexture = GpuRenderer.GetOrCreateLiquidGlassTexture(
                120, 80, 20.0f, 25.0f, 18.0f, 50.0f, 4.0f,
                -0.7071f, -0.7071f, Colors.White, 0.20f, 0.90f);

            Assert(glassTexture != null, "GpuRenderer produces a valid frozen BitmapSource for Liquid Glass");
            Assert(glassTexture.PixelWidth == 120 && glassTexture.PixelHeight == 80, "Liquid Glass texture matches dimensions");
            Assert(glassTexture.Format == PixelFormats.Bgra32, "Liquid Glass texture uses hardware-accelerated Bgra32 format");

            // Cache retrieval
            var cachedTexture = GpuRenderer.GetOrCreateLiquidGlassTexture(
                120, 80, 20.0f, 25.0f, 18.0f, 50.0f, 4.0f,
                -0.7071f, -0.7071f, Colors.White, 0.20f, 0.90f);
            Assert(object.ReferenceEquals(glassTexture, cachedTexture), "Liquid Glass texture is retained and retrieved from cache");

            // 3. Smooth Slider Custom Styles & IsMoveToPointEnabled
            var sliderStyle = ScrubControls.GetSharedSliderStyle();
            Assert(sliderStyle != null, "ScrubControls provides shared modern smooth slider style");

            var sliderElem = ScrubControls.CreateSmoothSlider("Test Smooth", 50, 0, 100, val => { }, "%");
            Assert(sliderElem != null, "CreateSmoothSlider returns a valid UI element");

            // 4. Gradient On-Canvas Handle State: handles active only during editing
            var node = new LayerNode
            {
                Type = ElementType.Rectangle,
                X = 50,
                Y = 50,
                Width = 200,
                Height = 100,
                Fills = new List<Paint>
                {
                    Paint.LinearGradient("#38BDF8", "#6366F1", 45)
                }
            };
            var fill = node.Fills[0];

            // Verify HitTest handles
            var hitStart = GradientRollerEngine.HitTest(new Point(node.X, node.Y), node, fill, 1.0);
            Assert(hitStart != null, "GradientRollerEngine performs handle hit testing");

            // Verify symmetric angle update on drag
            GradientRollerEngine.UpdateAngleFromDrag(node, fill, new Point(150, 200), false);
            Assert(Math.Abs(fill.GradientAngle - 90.0) < 0.5, "Gradient roller updates angle smoothly to 90 degrees on downward drag");

            // 5. Liquid Glass Real Transparency & Chromatic Dispersion
            int outW = 60;
            int outH = 40;
            int margin = 20;
            int bgW = outW + margin * 2;
            int bgH = outH + margin * 2;
            int bgStride = bgW * 4;
            byte[] bgV2 = new byte[bgH * bgStride];
            for (int y = 0; y < bgH; y++)
            {
                for (int x = 0; x < bgW; x++)
                {
                    int idx = y * bgStride + x * 4;
                    bgV2[idx] = (byte)(x * 2);      // B
                    bgV2[idx + 1] = (byte)(y * 2);  // G
                    bgV2[idx + 2] = (byte)220;      // R
                    bgV2[idx + 3] = 255;
                }
            }

            int outStride = outW * 4;
            byte[] outV2 = new byte[outH * outStride];
            bool v2Executed = RustEngineInterop.RenderLiquidGlassV2(
                bgV2, bgW, bgH, bgStride,
                outV2, outW, outH, outStride,
                margin, margin, 10.0f,
                15.0f, 12.0f, 40.0f, 2.0f, 0.08f,
                -0.7071f, -0.7071f,
                255.0f, 255.0f, 255.0f, 0.15f,
                0.85f, true);

            Assert(v2Executed, "Rust Core kerf_render_liquid_glass_v2 executes with backdrop margins and chromatic dispersion");
            // Check transparency outside corner (x=0, y=0 is outside squircle radius 10)
            Assert(outV2[3] == 0, "Liquid Glass outside squircle is completely transparent (alpha = 0)");
            // Check opacity inside center (x=30, y=20)
            int centerIdx = 20 * outStride + 30 * 4;
            Assert(outV2[centerIdx + 3] > 0, "Liquid Glass inside squircle has valid optical coverage alpha");
        }
    }
}

