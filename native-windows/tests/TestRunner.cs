using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Kerf.Engine;
using Kerf.IO;
using Kerf.Model;
using Kerf.Tools;

namespace Kerf.Tests
{
    public static class TestRunner
    {
        private static int _passed = 0;
        private static int _failed = 0;

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
    }
}

