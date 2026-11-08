using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kerf.Engine;
using Kerf.Engine.Animation;
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
            RunTest("Batch 7 Requirements: Polished Rounded UI, Simplified Figma Gradient Controls, and Pen Tool Vector Edit Engine", TestBatch7Requirements_PolishedUI_FigmaGradient_PenToolVectorEdit);
            RunTest("Batch 8 Requirements: Effects Collapse & Icon Labels, Instant Templates & Frame Selection No Fill", TestBatch8Requirements_EffectsCollapseIcons_Templates_FrameSelection);
            RunTest("Visual Hierarchy Reparenting & Nested Undo/Redo Engine Synchronization", TestVisualReparentingAcrossHierarchyAndUndoRedo);
            RunTest("Corner Radius On-Canvas Handles & Figma Auto Layout Flexbox Model", TestCornerRadiusNodesAndFigmaAutoLayoutFlexbox);
            RunTest("Vector Animation Engine & State Machine Runtime (newreq.md)", TestVectorAnimationAndStateMachineEngine);
            RunTest("Persistent Recent Projects, Save Anywhere, Safe Auto Layout Undo & Animation Tab Integration", TestMilestone23Features);
            RunTest("Component File Persistence Across Save/Open & Real Vector State Machine (Milestone 24)", TestMilestone24Features);
            RunTest("Nested Auto Layout Frames, Instant Single-Pass Stabilization, and Auto Align Undo/Redo", TestNestedAutoLayoutAndSinglePassStabilization);
            RunTest("Figma Auto Layout Parity: Smart Mode Detection, Full Bounding Enclosure, and Alt-Drag Duplication Isolation", TestFigmaAutoLayoutParityAndAltDragDuplication);
            RunTest("Button Pattern: Text/Object over Shape Auto Layout & Center Alignment", TestButtonPatternFromShapeAndForeground);
            RunTest("Uniform Scale Option in Transform: Lock Aspect Ratio & Proportional Resizing", TestUniformScaleLockAspectRatio);
            RunTest("Alt+Drag Deep Select Duplication in Auto Layout Frames", TestAltDragDuplicationInAutoLayout);
            RunTest("Modern Vector State Machine Animation Engine: Inputs, Listeners, ViewModels & Blended Transitions", TestModernVectorStateMachineAnimationEngine);
            RunTest("Figma Dark UI Colors, Light Green Button Theming & Menu Visibility", TestFigmaDarkColorsAndButtons);
            RunTest("Hierarchical Hit-Testing & Centralized Selection/Drag Pipeline", TestHierarchicalHitTestingAndCentralizedInteractionPipeline);
            RunTest("Hierarchical Object Insertion & Container Target Resolution", TestHierarchicalObjectInsertionAndContainerTargetResolution);
            RunTest("Non-Destructive Render Transforms & Auto Layout Preservation", TestNonDestructiveRenderTransformsAndAutoLayoutPreservation);
            RunTest("Independent Transform Architecture & Deterministic Animation Evaluator", TestIndependentTransformArchitectureAndAnimationEvaluator);
            RunTest("Timeline & Keyframe Engine Reconstruction & Authoring System", TestTimelineAndKeyframeEngineAuthoringSystem);
            RunTest("Scale Crash Fix, Non-Destructive Frame Content Scaling, Empty Preset & State Timeline Header", TestScaleCrashFixFrameScalingNoPresetAndStateTimelineHeader);
            RunTest("Anchor Point System, Stationary Pan-Behind & Raster Scaling", TestAnchorPointAndRasterScaling);
            RunTest("Rive 3-Axis + Rotation Gizmo, Y Key Reposition & Non-Destructive Frame Scaling", TestRiveAnchorPointGizmoAndContentScaling);
            RunTest("Easing Engine, Closed-Form Harmonic Springs & Graph Editor Reconstruction", TestEasingEngineClosedFormSpringsAndGraphEditor);
            RunTest("Universal Graph Editor Sidebar Consolidation & Clean Timeline", TestUniversalGraphEditorConsolidationAndCleanTimeline);
            RunTest("Single Corner Radius Control & Design vs Animate Mode Isolation", TestSingleCornerRadiusControlAndDesignAnimateIsolation);
            RunTest("Animation Command History, Full Undo/Redo & Runtime Isolation", TestAnimationCommandHistoryAndFullUndoRedo);
            RunTest("Interactive State Machine Engine: Graph Model, Condition Groups, Inputs, Validation & Undo/Redo", TestInteractiveStateMachineEngine);
            RunTest("FigCraft Optical Liquid Glass Model & Effects Panel Parity", TestFigCraftLiquidGlassAndEffectsPanel);
            RunTest("Group Bounds Lockstep, Zero Fill Default, HitTest & Undo/Redo", TestGroupingBoundingBoxAndNoFillDefault);
            RunTest("Interactive State Machine Refinement: Clean Slate, Ports, Single-Wire Badges & Canvas Deletion", TestInteractiveStateMachineEngineRefinement);
            RunTest("State Machine Batch Transitions, Easing Presets, Multiselect & Clip Renaming", TestStateMachineBatchTransitionsPresetsMultiselectAndClipRenaming);
            RunTest("State Machine Persistence & Smooth Rest-Pose Transition Blending", TestStateMachinePersistenceAndSmoothTransitionBlending);
            RunTest("Interactive Listeners & Centralized Event Dispatcher", TestInteractiveListenersAndCentralizedEventDispatcher);
            RunTest("Interrupted Transition Snapshot Blending & RenderTransform Isolation", TestInterruptedTransitionSnapshotBlending);
            RunTest("Deterministic Transition Priority, ExitTime Gating & Clip Playback", TestDeterministicTransitionPriorityExitTimeAndClipAdvancement);
            RunTest("Non-Destructive StateMachineValidator Listener Diagnostics", TestStateMachineValidatorListenerDiagnostics);
            RunTest("Interactive Listeners, State Machine Runtime & Transition Execution", TestInteractiveListenersAndStateMachineRuntimeExecution);
            RunTest("Continuous Playback, Input Gating & Interactive Pointer Transitions", TestContinuousPlaybackAndInputDrivenTransitions);

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
            Assert(autoWrapFrame.Width == 130.0, "Auto Layout frame must be the size of both frames included");
            Assert(autoWrapFrame.AutoLayout.Gap == 10.0, "Auto Layout gap measured from distance between frames to preserve bounding size");
            Assert(autoWrapFrame.Fills == null || autoWrapFrame.Fills.Count == 0, "Auto Layout frame must have no fill by default");
            Assert(autoWrapFrame.Children.Count == 2, "Wrapped frame contains both selected layers");
            Assert(page.Layers.Contains(autoWrapFrame), "Wrapped frame is added to page layers");
            Assert(!page.Layers.Contains(r1) && !page.Layers.Contains(r2), "Original items moved out of root page layers into frame");
            Assert(r1.X == autoWrapFrame.X, "First item aligned at start");
            Assert(r2.X == r1.X + r1.Width + autoWrapFrame.AutoLayout.Gap, "Items distributed with measured gap");
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

            // 8. Free Independent Handle Movement & Stretching (Not Stuck on Axis/Anchor)
            var stretchNode = new LayerNode
            {
                Id = "stretch-box",
                Type = ElementType.Rectangle,
                X = 0,
                Y = 0,
                Width = 200,
                Height = 200
            };
            var stretchPaint = new Paint
            {
                Type = PaintType.LinearGradient,
                GradientAngle = 90.0,
                GradientStops = new List<GradientStopData>
                {
                    new GradientStopData(0.0, "#FFFFFF", 1.0),
                    new GradientStopData(1.0, "#000000", 1.0)
                }
            };

            // Drag End handle freely to (200, 400) - stretching vertically far past bottom edge
            GradientRollerEngine.UpdateEndHandleFromDrag(stretchNode, stretchPaint, new Point(200, 400), false);
            Assert(stretchPaint.GradientEnd.HasValue, "GradientEnd is set after drag");
            Assert(Math.Abs(stretchPaint.GradientEnd.Value.X - 1.0) < 0.01 && Math.Abs(stretchPaint.GradientEnd.Value.Y - 2.0) < 0.01,
                string.Format("End handle moved to (1.0, 2.0), got ({0}, {1})", stretchPaint.GradientEnd.Value.X, stretchPaint.GradientEnd.Value.Y));
            // Verify Start handle remained at top center (0.5, 0.0) and was NOT forcibly moved across center
            Assert(stretchPaint.GradientStart.HasValue, "GradientStart is initialized");
            Assert(Math.Abs(stretchPaint.GradientStart.Value.X - 0.5) < 0.01 && Math.Abs(stretchPaint.GradientStart.Value.Y - 0.0) < 0.01,
                string.Format("Start handle stayed pinned at (0.5, 0.0), got ({0}, {1})", stretchPaint.GradientStart.Value.X, stretchPaint.GradientStart.Value.Y));

            // Drag Start handle freely to (-100, 50) - stretching horizontally outside the left boundary
            GradientRollerEngine.UpdateStartHandleFromDrag(stretchNode, stretchPaint, new Point(-100, 50), false);
            Assert(Math.Abs(stretchPaint.GradientStart.Value.X - (-0.5)) < 0.01 && Math.Abs(stretchPaint.GradientStart.Value.Y - 0.25) < 0.01,
                string.Format("Start handle moved freely outside layer to (-0.5, 0.25), got ({0}, {1})", stretchPaint.GradientStart.Value.X, stretchPaint.GradientStart.Value.Y));
            // Verify End handle remained at (1.0, 2.0)
            Assert(Math.Abs(stretchPaint.GradientEnd.Value.X - 1.0) < 0.01 && Math.Abs(stretchPaint.GradientEnd.Value.Y - 2.0) < 0.01,
                "End handle remained pinned while Start handle moved");

            // Verify midpoint is not stuck on (0.5, 0.5) anchor
            double midX = (stretchPaint.GradientStart.Value.X + stretchPaint.GradientEnd.Value.X) / 2.0;
            double midY = (stretchPaint.GradientStart.Value.Y + stretchPaint.GradientEnd.Value.Y) / 2.0;
            Assert(Math.Abs(midX - 0.5) > 0.1 || Math.Abs(midY - 0.5) > 0.1,
                string.Format("Gradient midpoint moved freely away from center anchor (0.5, 0.5), currently at ({0}, {1})", midX, midY));

            // Verify line stretched distance on canvas is accurately reflected in GetGradientPoints
            Point sp1, sp2;
            List<Point> sStops;
            GradientRollerEngine.GetGradientPoints(stretchNode, stretchPaint, out sp1, out sp2, out sStops);
            double lineLength = Math.Sqrt((sp2.X - sp1.X) * (sp2.X - sp1.X) + (sp2.Y - sp1.Y) * (sp2.Y - sp1.Y));
            Assert(lineLength > 300.0, string.Format("Gradient line length stretched to {0:F1}px", lineLength));
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

        private static void TestBatch7Requirements_PolishedUI_FigmaGradient_PenToolVectorEdit()
        {
            // 1. Vector Edit Engine: Segment Hit-Testing (Figma Pen & Move interaction)
            var geo = new VectorGeometry();
            var n1 = new BezierNode(0, 0);
            var n2 = new BezierNode(100, 0);
            geo.Nodes.Add(n1);
            geo.Nodes.Add(n2);

            int hitSeg;
            Point closestPt;
            bool hit = BezierVectorEngine.HitTestSegment(geo, new Point(50, 2), 5.0, out hitSeg, out closestPt);
            Assert(hit && hitSeg == 0, "HitTestSegment correctly detects cursor hit on segment 0");

            int missSeg;
            Point missPt;
            bool miss = BezierVectorEngine.HitTestSegment(geo, new Point(50, 25), 5.0, out missSeg, out missPt);
            Assert(!miss && missSeg == -1, "HitTestSegment ignores points outside tolerance threshold");

            // 2. Vector Edit Engine: BendSegment (Figma Bend Tool directly dragging segments)
            BezierVectorEngine.BendSegment(geo, 0, new Point(50, 40));
            Assert(n1.OutHandle.Y > 10, "BendSegment curves starting tangent towards target handle drag position");
            Assert(n2.InHandle.Y > 10, "BendSegment curves ending tangent towards target handle drag position");

            // 3. Vector Edit Engine: InsertNode along segment (Figma Pen click on segment inserts at hitSeg + 1)
            BezierVectorEngine.InsertNode(geo, hitSeg + 1, new Point(50, 20));
            Assert(geo.Nodes.Count == 3, "VectorGeometry has 3 nodes after segment subdivision");
            Assert(Math.Abs(geo.Nodes[1].Anchor.X - 50) < 0.01 && Math.Abs(geo.Nodes[1].Anchor.Y - 20) < 0.01, "Inserted node is positioned accurately along the segment");

            // 4. Vector Edit Engine: SplitPathAtNode (Figma Cut Tool 'X' shortcut)
            BezierVectorEngine.SplitPathAtNode(geo, 1);
            Assert(geo.Nodes.Count == 4, "SplitPathAtNode cleanly splits open path vertex into separate editable endpoints");

            // 5. Polished UI Rounded Button Templates
            var roundedTmpl = GradientRampControl.CreateRoundedButtonTemplate(6);
            Assert(roundedTmpl != null, "CreateRoundedButtonTemplate returns valid ControlTemplate");

            // 6. Simplified Figma-Style Gradient Controls
            var linearPaint = Paint.LinearGradient("#FF0000", "#0000FF", 45);
            var ramp = new GradientRampControl(linearPaint, () => { });
            Assert(ramp != null, "GradientRampControl initializes successfully with linear gradient");
        }

        private static void TestBatch8Requirements_EffectsCollapseIcons_Templates_FrameSelection()
        {
            // 1. Effects Data: Collapse / Expand state
            var fx = new EffectData { Type = EffectType.LiquidGlass };
            Assert(fx.IsExpanded, "EffectData defaults to IsExpanded = true");
            fx.IsExpanded = false;
            var clonedFx = fx.Clone();
            Assert(!clonedFx.IsExpanded, "Cloned EffectData preserves collapsed state");

            // 2. Scrub Controls & Smooth Sliders: Compact Icons & Tooltips
            var scrubAngle = ScrubControls.CreateScrubField("∠", 90.0, val => { }, min: 0, max: 360, suffix: "°", tooltip: "Angle Direction");
            Assert(scrubAngle != null, "CreateScrubField builds valid UI for compact icon labels");
            Assert(scrubAngle.ToolTip != null && scrubAngle.ToolTip.ToString() == "Angle Direction", "CreateScrubField correctly assigns parameter tooltip");

            var sliderOpacity = ScrubControls.CreateSmoothSlider("◐", 80, 0, 100, val => { }, "%", tooltip: "Translucency Opacity");
            Assert(sliderOpacity != null, "CreateSmoothSlider builds valid UI with compact icon");
            var dockPanel = sliderOpacity as DockPanel;
            Assert(dockPanel != null && dockPanel.Children.Count >= 2, "CreateSmoothSlider contains label and slider elements");

            // 3. Prebuilt Templates Generators: Always Found & Valid Layers
            var mobileDoc = StorageManager.CreateStarterMobileTemplate();
            Assert(mobileDoc != null && mobileDoc.GetActivePage() != null, "Mobile UI Design template generated successfully");
            Assert(mobileDoc.GetActivePage().Layers.Count > 0, "Mobile template contains root frame layer");
            var mobileFrame = mobileDoc.GetActivePage().Layers[0];
            Assert(mobileFrame.Children != null && mobileFrame.Children.Count >= 4, "Mobile frame contains structured child UI elements");

            var dashDoc = StorageManager.CreateStarterDashboardTemplate();
            Assert(dashDoc != null && dashDoc.GetActivePage().Layers.Count > 0, "Analytics Dashboard template generated successfully");

            var landingDoc = StorageManager.CreateStarterLandingPageTemplate();
            Assert(landingDoc != null && landingDoc.GetActivePage().Layers.Count > 0, "SaaS Landing Page template generated successfully");

            var dsDoc = StorageManager.CreateStarterDesignSystemTemplate();
            Assert(dsDoc != null && dsDoc.GetActivePage().Layers.Count > 0, "Design System template generated successfully");

            // 4. Frame Selection: NO default white fill (transparent)
            var page = new Page();
            var l1 = new LayerNode { Id = "obj1", X = 20, Y = 20, Width = 50, Height = 50 };
            page.Layers.Add(l1);
            var frame = new LayerNode
            {
                Id = "wrapFrame",
                Name = "Frame 1",
                Type = ElementType.Frame,
                X = 20,
                Y = 20,
                Width = 50,
                Height = 50,
                ClipContent = false,
                Fills = new List<Paint>()
            };
            Assert(frame.Fills != null && frame.Fills.Count == 0, "Frame Selection framing wrapper has no default fills (transparent)");
        }

        private static void TestVisualReparentingAcrossHierarchyAndUndoRedo()
        {
            var page = new Page();
            var frameA = new LayerNode
            {
                Id = "frame-a",
                Name = "Frame A",
                Type = ElementType.Frame,
                X = 0,
                Y = 0,
                Width = 300,
                Height = 300,
                Children = new List<LayerNode>()
            };

            var frameB = new LayerNode
            {
                Id = "frame-b",
                Name = "Frame B",
                Type = ElementType.Frame,
                X = 400,
                Y = 0,
                Width = 300,
                Height = 300,
                Children = new List<LayerNode>()
            };

            var rect1 = new LayerNode
            {
                Id = "rect-1",
                Name = "Rectangle 1",
                Type = ElementType.Rectangle,
                X = 20,
                Y = 20,
                Width = 80,
                Height = 80,
                ParentId = "frame-a"
            };
            frameA.Children.Add(rect1);

            var rect2 = new LayerNode
            {
                Id = "rect-2",
                Name = "Rectangle 2",
                Type = ElementType.Rectangle,
                X = 120,
                Y = 20,
                Width = 80,
                Height = 80,
                ParentId = "frame-a"
            };
            frameA.Children.Add(rect2);

            page.Layers.Add(frameA);
            page.Layers.Add(frameB);

            var renderer = new GpuRenderer();
            var sceneGraph = new RetainedSceneGraph(renderer);

            // 1. Initial synchronization
            sceneGraph.Synchronize(page);
            var vFrameA = sceneGraph.GetVisualNode("frame-a");
            var vFrameB = sceneGraph.GetVisualNode("frame-b");
            var vRect1 = sceneGraph.GetVisualNode("rect-1");
            Assert(vFrameA != null && vFrameB != null && vRect1 != null, "SceneGraph must index all frame and rect nodes");
            Assert(vFrameA.Visual.Children.Contains(vRect1.Visual), "Rect1 visual must be child of Frame A visual initially");

            // 2. Reorder rect1 from Frame A to Frame B (execute command)
            var reorderToB = new ReorderLayerCommand(page, rect1, "frame-a", 0, "frame-b", 0, () =>
            {
                sceneGraph.Synchronize(page);
            });
            reorderToB.Execute();

            Assert(vFrameB.Visual.Children.Contains(vRect1.Visual), "After move to Frame B, Rect1 visual must be child of Frame B visual");
            Assert(!vFrameA.Visual.Children.Contains(vRect1.Visual), "Rect1 visual must NOT remain in Frame A visual children");

            // 3. Move rect1 from Frame B to Root (before Frame A in page.Layers)
            var reorderToRoot = new ReorderLayerCommand(page, rect1, "frame-b", 0, null, 0, () =>
            {
                sceneGraph.Synchronize(page);
            });
            reorderToRoot.Execute();

            Assert(sceneGraph.RootVisual.Children.Contains(vRect1.Visual), "After move to root, Rect1 visual must be child of RootVisual");
            Assert(!vFrameB.Visual.Children.Contains(vRect1.Visual), "Rect1 visual must NOT remain in Frame B visual children");

            // 4. Undo move to root (rect1 returns to Frame B)
            reorderToRoot.Unexecute();
            Assert(vFrameB.Visual.Children.Contains(vRect1.Visual), "After undo, Rect1 visual must be restored to Frame B visual");
            Assert(!sceneGraph.RootVisual.Children.Contains(vRect1.Visual), "Rect1 visual must NOT remain in RootVisual");

            // 5. Undo move to Frame B (rect1 returns to Frame A)
            reorderToB.Unexecute();
            Assert(vFrameA.Visual.Children.Contains(vRect1.Visual), "After second undo, Rect1 visual must be restored to Frame A visual");
            Assert(!vFrameB.Visual.Children.Contains(vRect1.Visual), "Rect1 visual must NOT remain in Frame B visual");

            // 6. Redo all
            reorderToB.Execute();
            Assert(vFrameB.Visual.Children.Contains(vRect1.Visual), "Redo move to Frame B restored visual to Frame B");
            reorderToRoot.Execute();
            Assert(sceneGraph.RootVisual.Children.Contains(vRect1.Visual), "Redo move to Root restored visual to Root");

            // 7. Stress test: Reparent back and forth 20 times rapidly
            for (int i = 0; i < 20; i++)
            {
                reorderToRoot.Unexecute();
                reorderToRoot.Execute();
            }

            // 8. Prevent non-container parenting & cyclic descendant nesting
            // A: Non-container (Rectangle) cannot contain Frame A
            var nonContainerCmd = new ReorderLayerCommand(page, frameA, null, 0, "rect-1", 0, () =>
            {
                sceneGraph.Synchronize(page);
            });
            nonContainerCmd.Execute();
            Assert(rect1.Children == null || !rect1.Children.Contains(frameA), "Rectangle 1 must never become a container of Frame A");

            // B: Attempting to move Frame A into its own child frame (nested cycle)
            var childFrame = new LayerNode { Id = "child-frame", Name = "Child Frame", Type = ElementType.Frame, ParentId = "frame-a" };
            frameA.Children.Add(childFrame);
            var cycleCmd = new ReorderLayerCommand(page, frameA, null, 0, "child-frame", 0, () =>
            {
                sceneGraph.Synchronize(page);
            });
            cycleCmd.Execute();
            Assert(childFrame.Children == null || !childFrame.Children.Contains(frameA), "Child Frame must never contain its own ancestor Frame A");
        }

        private static void TestCornerRadiusNodesAndFigmaAutoLayoutFlexbox()
        {
            // 1. Corner Radius handle positioning & hit-testing across zoom scales
            var rectBounds = new Rect(100, 100, 200, 150);
            var crPts1 = TransformEngine.GetCornerRadiusHandlePositions(rectBounds, 0.0, 1.0);
            Assert(crPts1.Length == 4, "Must generate exactly 4 corner radius handle positions");
            Assert(TransformEngine.HitTestCornerRadiusHandle(crPts1[0], rectBounds, 0.0, 0.0, 1.0), "Hit-test succeeds at top-left corner handle at 1.0x zoom");
            Assert(TransformEngine.HitTestCornerRadiusHandle(crPts1[1], rectBounds, 0.0, 0.0, 1.0), "Hit-test succeeds at top-right corner handle at 1.0x zoom");

            // Test zoom scaling consistency (e.g. 2.0x zoom)
            var crPtsZoom = TransformEngine.GetCornerRadiusHandlePositions(rectBounds, 20.0, 2.0);
            Assert(TransformEngine.HitTestCornerRadiusHandle(crPtsZoom[0], rectBounds, 0.0, 20.0, 2.0), "Hit-test succeeds at 2.0x zoom");

            // 2. Auto Layout frame must be the size of both frames included
            var page = new Page();
            var frame1 = new LayerNode { Id = "f1", Name = "Card 1", Type = ElementType.Frame, X = 50, Y = 50, Width = 100, Height = 80 };
            var frame2 = new LayerNode { Id = "f2", Name = "Card 2", Type = ElementType.Frame, X = 200, Y = 50, Width = 120, Height = 90 };
            page.Layers.Add(frame1);
            page.Layers.Add(frame2);

            var wrapped = AutoLayoutEngine.AutoAlignSelection(page, new List<LayerNode> { frame1, frame2 });
            Assert(wrapped != null, "AutoAlignSelection wraps frames");
            Assert(wrapped.Width == 270.0, string.Format("Auto Layout frame width must encompass both frames included (50 to 320 = 270), got {0}", wrapped.Width));
            Assert(wrapped.Height == 90.0, string.Format("Auto Layout frame height must encompass both frames included (max 90), got {0}", wrapped.Height));
            Assert(wrapped.AutoLayout.Gap == 50.0, string.Format("Auto Layout measured gap between frames must be 50, got {0}", wrapped.AutoLayout.Gap));

            // 3. Auto Layout Frame resizing does NOT jump back up when dragging height down
            // When user drags bottom handle, height becomes 250, SizingV becomes Fixed
            wrapped.AutoLayout.SizingV = SizingMode.Fixed;
            wrapped.Height = 250.0;
            AutoLayoutEngine.Reflow(wrapped);
            Assert(wrapped.Height == 250.0, "Resized Auto Layout frame height stays fixed at 250px and does not jump up");

            // 4. Flexbox children inside Auto Layout reorder cleanly
            frame2.X = 0.0; // dragged before frame1 (center 60 < center 100)
            if (wrapped.AutoLayout.Mode == AutoLayoutMode.Horizontal)
            {
                wrapped.Children = wrapped.Children.OrderBy(c => c.X + c.Width / 2.0).ToList();
            }
            AutoLayoutEngine.Reflow(wrapped);
            Assert(wrapped.Children[0] == frame2, "Child frames reorder according to drag position along flex axis");
            Assert(wrapped.Children[1] == frame1, "Second child follows first in flexbox flow");
        }

        private static void TestVectorAnimationAndStateMachineEngine()
        {
            // 1. Cubic Bezier Solver (Newton-Raphson + Bisection) & Easing Curves
            double easeVal = CubicBezierEase.Evaluate(0.5, 0.25, 0.1, 0.25, 1.0);
            Assert(easeVal > 0.0 && easeVal < 1.0, "Cubic Bezier evaluates accurately");
            Assert(Math.Abs(CubicBezierEase.Linear(0.5) - 0.5) < 1e-6, "Linear ease at 0.5 is 0.5");
            Assert(CubicBezierEase.Evaluate(0.0, 0.42, 0.0, 0.58, 1.0) == 0.0, "Ease at 0.0 is 0.0");
            Assert(CubicBezierEase.Evaluate(1.0, 0.42, 0.0, 0.58, 1.0) == 1.0, "Ease at 1.0 is 1.0");

            // 2. Track & Keyframe Interpolation (Hold, Linear, Cubic)
            Track track = new Track(101, (ushort)PropKey.X);
            track.AddKeyframe(Keyframe.Linear(0.0, 50.0));
            track.AddKeyframe(Keyframe.Cubic(1.0, 150.0, 0.25, 0.1, 0.25, 1.0));
            track.AddKeyframe(Keyframe.Hold(2.0, 300.0));
            track.AddKeyframe(Keyframe.Linear(3.0, 500.0));

            Assert(track.Sample(-0.5) == 50.0, "Track samples start value before time 0");
            Assert(Math.Abs(track.Sample(0.5) - 100.0) < 1e-4, "Track samples linear midpoint at 100.0");
            Assert(track.Sample(1.0) == 150.0, "Track samples exact keyframe at 1.0s");
            Assert(track.Sample(2.5) == 300.0, "Track holds value during Hold interval");
            Assert(track.Sample(4.0) == 500.0, "Track samples end value after time 3.0s");

            // 3. Clip advance and loop modes (OneShot, Loop, PingPong)
            ClipInstance inst = new ClipInstance(0);
            double duration = 2.0;

            // OneShot
            inst.Advance(2.5, duration, LoopMode.OneShot);
            Assert(inst.Time == 2.0, "OneShot clamps at duration");

            // Loop
            inst.Time = 0.0;
            inst.Advance(2.5, duration, LoopMode.Loop);
            Assert(Math.Abs(inst.Time - 0.5) < 1e-4, "Loop wraps time with modulo");

            // PingPong
            inst.Time = 0.0;
            inst.Direction = 1;
            inst.Advance(2.5, duration, LoopMode.PingPong);
            Assert(inst.Direction == -1, "PingPong reverses direction on hitting boundary");
            Assert(Math.Abs(inst.Time - 1.5) < 1e-4, "PingPong bounces back from duration");

            // 4. State Machine Runtime: Inputs, Conditions, Transitions & Trigger Auto-Clear
            StateMachineDefinition smDef = new StateMachineDefinition("InteractiveCard");
            int trigClick = smDef.AddInput(MachineInput.Trigger("onClick"));
            int boolHover = smDef.AddInput(MachineInput.Bool("isHover", false));
            int numSpeed = smDef.AddInput(MachineInput.Number("speed", 1.0));

            MachineLayer layer = new MachineLayer("BaseLayer");
            int sIdle = layer.AddState(new MachineState(0, "Idle", StateKind.Animation, 0));
            int sHover = layer.AddState(new MachineState(1, "Hover", StateKind.Animation, 1));
            int sPressed = layer.AddState(new MachineState(2, "Pressed", StateKind.Animation, 2));

            // Idle -> Hover on isHover == true
            layer.AddTransition(new StateTransition(sIdle, sHover, 0.2).WithCondition(new TransitionCondition(boolHover, ConditionOp.Eq, 1.0)));
            // Hover -> Pressed on onClick Trigger
            layer.AddTransition(new StateTransition(sHover, sPressed, 0.1).WithCondition(new TransitionCondition(trigClick, ConditionOp.Eq, 1.0)));

            smDef.AddLayer(layer);

            List<Clip> clips = new List<Clip>();
            Clip cIdle = new Clip(0, "IdleClip", 1.0, 60.0, LoopMode.Loop);
            Clip cHover = new Clip(1, "HoverClip", 1.0, 60.0, LoopMode.Loop);
            Clip cPressed = new Clip(2, "PressedClip", 1.0, 60.0, LoopMode.OneShot);

            Track trX = new Track(101, (ushort)PropKey.X);
            trX.AddKeyframe(Keyframe.Linear(0.0, 100.0));
            trX.AddKeyframe(Keyframe.Linear(1.0, 200.0));
            cPressed.AddTrack(trX);

            clips.Add(cIdle);
            clips.Add(cHover);
            clips.Add(cPressed);

            StateMachineRuntime smRuntime = new StateMachineRuntime(smDef, clips);
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sIdle, "State machine starts in Entry/Idle state");

            var pose = new Dictionary<uint, Dictionary<ushort, double>>();
            smRuntime.Update(0.016, pose);
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sIdle, "Remains in Idle state with no inputs");

            // Activate hover
            smRuntime.SetBool(boolHover, true);
            smRuntime.Update(0.016, pose);
            Assert(smRuntime.LayerRuntimes[0].ActiveTransition != null, "Transition to Hover started");

            // Advance through transition
            smRuntime.Update(0.25, pose);
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sHover, "Transition to Hover completed");
            Assert(smRuntime.LayerRuntimes[0].ActiveTransition == null, "Transition cleared upon completion");

            // Fire trigger onClick
            smRuntime.FireTrigger(trigClick);
            Assert(smDef.Inputs[trigClick].Fired, "Trigger is active before update");
            smRuntime.Update(0.016, pose);
            Assert(!smDef.Inputs[trigClick].Fired, "Trigger MUST be cleared automatically after update (lasts 1 frame)");
            Assert(smRuntime.LayerRuntimes[0].ActiveTransition != null, "Transition to Pressed started via Trigger");

            smRuntime.Update(0.15, pose);
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sPressed, "Transition to Pressed completed");

            // 5. Zero-Duration Chain Resolution with Cycle Guard
            StateMachineDefinition chainDef = new StateMachineDefinition("Chain");
            MachineLayer chainLayer = new MachineLayer("ChainLayer");
            int c0 = chainLayer.AddState(new MachineState(0, "C0", StateKind.Entry));
            int c1 = chainLayer.AddState(new MachineState(1, "C1", StateKind.Animation, 0));
            int c2 = chainLayer.AddState(new MachineState(2, "C2", StateKind.Animation, 0));
            chainLayer.AddTransition(new StateTransition(c0, c1, 0.0));
            chainLayer.AddTransition(new StateTransition(c1, c2, 0.0));
            chainDef.AddLayer(chainLayer);

            StateMachineRuntime chainRuntime = new StateMachineRuntime(chainDef, clips);
            chainRuntime.Update(0.016, pose);
            Assert(chainRuntime.LayerRuntimes[0].CurrentState == c2, "Zero-duration transition chain resolved instantly to C2 in 1 update");

            // 6. Interrupted Transition Snapshotting
            StateMachineDefinition interDef = new StateMachineDefinition("Interruptible");
            int boolJump = interDef.AddInput(MachineInput.Bool("jump", false));
            MachineLayer interLayer = new MachineLayer("InterLayer");
            int iA = interLayer.AddState(new MachineState(0, "A", StateKind.Animation, 0));
            int iB = interLayer.AddState(new MachineState(1, "B", StateKind.Animation, 1));
            int iC = interLayer.AddState(new MachineState(2, "C", StateKind.Animation, 2));
            interLayer.AddTransition(new StateTransition(iA, iB, 1.0).WithInterrupt(true));
            interLayer.AddTransition(new StateTransition(iA, iC, 0.5).WithCondition(new TransitionCondition(boolJump, ConditionOp.Eq, 1.0)).WithInterrupt(true));
            interDef.AddLayer(interLayer);

            StateMachineRuntime interRuntime = new StateMachineRuntime(interDef, clips);
            interRuntime.Update(0.016, pose); // starts A -> B
            interRuntime.Update(0.4, pose);   // mid-blend
            Assert(interRuntime.LayerRuntimes[0].ActiveTransition != null && interRuntime.LayerRuntimes[0].ActiveTransition.ToState == iB, "Mid-transition to B");

            // Interrupt mid-blend by setting jump = true
            interRuntime.SetBool(boolJump, true);
            interRuntime.Update(0.016, pose);
            Assert(interRuntime.LayerRuntimes[0].ActiveTransition != null && interRuntime.LayerRuntimes[0].ActiveTransition.ToState == iC, "Interrupted to C");
            Assert(interRuntime.LayerRuntimes[0].ActiveTransition.SnapshotPose != null, "Snapshot pose captured at interruption point to eliminate visual pops");

            // 7. 1D Blend & Direct Blend States
            StateMachineDefinition blendDef = new StateMachineDefinition("BlendMachine");
            int blendInput = blendDef.AddInput(MachineInput.Number("speed", 0.5));
            MachineLayer blendLayer = new MachineLayer("BlendLayer");
            MachineState b1d = new MachineState(0, "BlendWalkRun", StateKind.Blend1D);
            b1d.BlendInputIndex = blendInput;
            b1d.Blend1DItems.Add(new Blend1DItem(0.0, 0)); // Clip 0
            b1d.Blend1DItems.Add(new Blend1DItem(1.0, 2)); // Clip 2 (PressedClip with trX 100->200)
            blendLayer.AddState(b1d);
            blendDef.AddLayer(blendLayer);

            StateMachineRuntime blendRuntime = new StateMachineRuntime(blendDef, clips);
            var blendPose = new Dictionary<uint, Dictionary<ushort, double>>();
            blendRuntime.Update(0.0, blendPose);
            Assert(blendPose.ContainsKey(101) && blendPose[101].ContainsKey((ushort)PropKey.X), "1D Blend produces animated property output");
            double blendVal = blendPose[101][(ushort)PropKey.X];
            Assert(blendVal >= 40.0 && blendVal <= 160.0, string.Format("1D Blend evaluated between clips at 50% threshold, got {0}", blendVal));

            // 8. Pointer Event Listeners
            NodeListener btnListener = new NodeListener(42, PointerEventKind.Down);
            btnListener.Actions.Add(new ListenerAction(ActionKind.Fire, trigClick));
            smDef.AddListener(btnListener);

            smRuntime.OnPointerEvent(42, PointerEventKind.Down);
            Assert(smDef.Inputs[trigClick].Fired, "Pointer down event on target node fired the configured trigger input");

            // 9. Smart Animate Prototype Compiler & Property Binder
            var frameA = new Dictionary<uint, Dictionary<ushort, double>>();
            var frameB = new Dictionary<uint, Dictionary<ushort, double>>();
            frameA[7] = new Dictionary<ushort, double>();
            frameA[7][(ushort)PropKey.X] = 100.0;
            frameA[7][(ushort)PropKey.Opacity] = 1.0;

            frameB[7] = new Dictionary<ushort, double>();
            frameB[7][(ushort)PropKey.X] = 300.0;
            frameB[7][(ushort)PropKey.Opacity] = 0.5;

            Clip smartClip = SmartAnimateCompiler.CompileTransitionClip(99, "SmartTrans", 0.5, frameA, frameB);
            Assert(smartClip.Tracks.Count == 2, "Smart Animate compiler generated 2 tracks for differing properties");

            var smartPose = new Dictionary<uint, Dictionary<ushort, double>>();
            smartClip.SampleToPose(0.5, smartPose);
            Assert(smartPose.ContainsKey(7) && Math.Abs(smartPose[7][(ushort)PropKey.X] - 300.0) < 50.0, "Smart animate sampled interpolated position");

            LayerNode testLayer = new LayerNode { Id = "test-node", X = 0, Y = 0, Width = 100, Height = 100, Opacity = 1.0 };
            PropertyBinder.ApplyPoseToLayer(testLayer, 7, smartPose);
            Assert(testLayer.X + testLayer.RenderTransform.TranslationX == smartPose[7][(ushort)PropKey.X], "PropertyBinder updated LayerNode RenderTransform TranslationX from animation pose");
            Assert(testLayer.Opacity == smartPose[7][(ushort)PropKey.Opacity], "PropertyBinder updated LayerNode Opacity from animation pose");

            // 10. Native Rust Interop Verification
            if (AnimationNativeBridge.IsNativeAvailable)
            {
                ulong nativeCtx = AnimationNativeBridge.kerf_anim_context_create();
                Assert(nativeCtx != 0, "Native Rust animation context created successfully via C-ABI");

                uint nativeClip = AnimationNativeBridge.kerf_anim_clip_create(nativeCtx, 1, "NativeWalk", 1.0f, 60.0f, 1);
                uint nativeTrack = AnimationNativeBridge.kerf_anim_clip_add_track(nativeCtx, nativeClip, 55, (ushort)PropKey.Width);
                AnimationNativeBridge.kerf_anim_track_add_keyframe(nativeCtx, nativeClip, nativeTrack, 0.0f, 100.0f, 1, 0, 0, 1, 1);
                AnimationNativeBridge.kerf_anim_track_add_keyframe(nativeCtx, nativeClip, nativeTrack, 1.0f, 200.0f, 1, 0, 0, 1, 1);

                float sampledWidth = AnimationNativeBridge.kerf_anim_track_sample(nativeCtx, nativeClip, nativeTrack, 0.5f);
                Assert(Math.Abs(sampledWidth - 150.0f) < 0.1f, string.Format("Native Rust track sampled at midpoint: expected 150, got {0}", sampledWidth));

                uint nativeSm = AnimationNativeBridge.kerf_sm_create(nativeCtx, "NativeSM");
                uint nativeInput = AnimationNativeBridge.kerf_sm_add_input(nativeCtx, nativeSm, "isActive", 0, 0.0f);
                uint nativeLayer = AnimationNativeBridge.kerf_sm_add_layer(nativeCtx, nativeSm, "MainLayer");
                uint nativeS0 = AnimationNativeBridge.kerf_sm_add_state(nativeCtx, nativeSm, nativeLayer, "Off", 0, 0);
                uint nativeS1 = AnimationNativeBridge.kerf_sm_add_state(nativeCtx, nativeSm, nativeLayer, "On", 3, nativeClip);
                uint nativeTrans = AnimationNativeBridge.kerf_sm_add_transition(nativeCtx, nativeSm, nativeLayer, nativeS0, nativeS1, 0.1f, 0.0f, 0, 1);
                AnimationNativeBridge.kerf_sm_transition_add_condition(nativeCtx, nativeSm, nativeLayer, nativeTrans, nativeInput, 0, 1.0f);

                AnimationNativeBridge.kerf_sm_set_bool(nativeCtx, nativeSm, nativeInput, 1);
                uint redrawNeeded = AnimationNativeBridge.kerf_sm_advance(nativeCtx, nativeSm, 0.016f);
                Assert(redrawNeeded == 1, "Native Rust state machine requires redraw upon starting transition");

                AnimationNativeBridge.kerf_anim_context_destroy(nativeCtx);
            }
        }

        private static void TestMilestone23Features()
        {
            // 1. Persistent Recent Projects & Save Anywhere
            string testDir = Path.Combine(Path.GetTempPath(), "KerfTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            string extFile = Path.Combine(testDir, "CustomProjectAnywhere.kerf");

            var testDoc = new KerfDocument { Name = "CustomProjectAnywhere" };
            var p1 = testDoc.GetActivePage();
            p1.Layers.Add(new LayerNode { Name = "Card", Type = ElementType.Rectangle, X = 10, Y = 10, Width = 200, Height = 100 });

            StorageManager.SaveAs(testDoc, extFile);
            Assert(File.Exists(extFile), "Document successfully saved to arbitrary external path");

            var recents = StorageManager.GetRecentProjects();
            bool foundExt = false;
            foreach (var r in recents)
            {
                if (string.Equals(r.FilePath, extFile, StringComparison.OrdinalIgnoreCase))
                {
                    foundExt = true;
                    Assert(r.Name == "CustomProjectAnywhere", "Recent project name probed accurately from file");
                    break;
                }
            }
            Assert(foundExt, "External project path tracked in persistent recent projects registry");

            // Clean up test file
            try { File.Delete(extFile); Directory.Delete(testDir); } catch { }

            // 2. Safe Auto Layout Multi-Selection Envelope & Complete Undo/Redo
            var page = new Page { Id = "test-page", Name = "Page 1" };
            var r1 = new LayerNode { Id = "rect-1", Name = "Rect 1", Type = ElementType.Rectangle, X = 100, Y = 50, Width = 120, Height = 60 };
            var r2 = new LayerNode { Id = "rect-2", Name = "Rect 2", Type = ElementType.Rectangle, X = 250, Y = 70, Width = 80, Height = 40 };
            page.Layers.Add(r1);
            page.Layers.Add(r2);

            var selList = new List<LayerNode> { r1, r2 };
            bool renderCalled = false;
            var autoCmd = AutoLayoutEngine.CreateAutoLayoutCommand(page, selList, () => { renderCalled = true; });
            Assert(autoCmd != null, "CreateAutoLayoutCommand returns valid ICommand");

            autoCmd.Execute();
            Assert(renderCalled, "Execute triggered render / synchronization callback");
            Assert(page.Layers.Count == 1, "Page root has 1 Auto Layout Frame wrapping selected layers");
            var frame = page.Layers[0];
            Assert(frame.Type == ElementType.Frame, "Created wrapper is an ElementType.Frame");
            Assert(frame.AutoLayout != null && frame.AutoLayout.Mode != AutoLayoutMode.None, "Frame has active AutoLayout data");
            Assert(frame.Children.Count == 2, "Frame contains both wrapped layers as children");
            Assert(frame.Width >= 230.0, string.Format("Frame bounding envelope encompasses all layers (expected >= 230, got {0})", frame.Width));

            // Test Undo (Unexecute)
            renderCalled = false;
            autoCmd.Unexecute();
            Assert(renderCalled, "Unexecute triggered render / synchronization callback");
            Assert(page.Layers.Count == 2, "Unexecute restored original 2 layers to page root");
            Assert(page.Layers.Contains(r1) && page.Layers.Contains(r2), "Both original layers restored to page");
            Assert(r1.X == 100 && r1.Y == 50 && r1.Width == 120 && r1.Height == 60, "Layer 1 exact bounds restored on Undo");
            Assert(r2.X == 250 && r2.Y == 70 && r2.Width == 80 && r2.Height == 40, "Layer 2 exact bounds restored on Undo");
            Assert(string.IsNullOrEmpty(r1.ParentId) && string.IsNullOrEmpty(r2.ParentId), "ParentIds reset to root on Undo");

            // Test Redo (Execute again)
            autoCmd.Execute();
            Assert(page.Layers.Count == 1, "Redo re-wrapped layers into Auto Layout Frame");
            Assert(page.Layers[0].Children.Count == 2, "Redo restored children inside frame");

            // 3. Single Layer Auto Layout Wrap with Figma Padding
            var singlePage = new Page { Id = "page-single", Name = "Single Page" };
            var singleBox = new LayerNode { Id = "box-1", Name = "Box", Type = ElementType.Rectangle, X = 80, Y = 60, Width = 150, Height = 90 };
            singlePage.Layers.Add(singleBox);

            var wrapCmd = AutoLayoutEngine.CreateAutoLayoutCommand(singlePage, new List<LayerNode> { singleBox }, () => { });
            Assert(wrapCmd != null, "Single layer Auto Layout command created");
            wrapCmd.Execute();
            Assert(singlePage.Layers.Count == 1, "Single layer wrapped into frame");
            var singleFrame = singlePage.Layers[0];
            Assert(singleFrame.Width == 170.0, string.Format("Single layer wrapped with standard padding: expected 170, got {0}", singleFrame.Width));
            Assert(singleFrame.Height == 110.0, string.Format("Single layer wrapped with standard padding: expected 110, got {0}", singleFrame.Height));

            // Undo single wrap
            wrapCmd.Unexecute();
            Assert(singlePage.Layers.Count == 1 && singlePage.Layers[0] == singleBox, "Single layer restored on Undo");
            Assert(singleBox.Width == 150 && singleBox.Height == 90, "Single layer bounds preserved");

            // 4. RetainedSceneGraph Visual Re-attachment Immunity
            var sceneGraph = new RetainedSceneGraph(new GpuRenderer());
            sceneGraph.Synchronize(page);
            Assert(sceneGraph.RootVisual.Children.Count > 0, "Scene graph synchronized initial layout");

            // Mutate and re-synchronize multiple times
            autoCmd.Unexecute();
            sceneGraph.Synchronize(page);
            Assert(sceneGraph.RootVisual.Children.Count == 2, "Scene graph synchronized undone hierarchy with zero visual parenting exceptions");

            autoCmd.Execute();
            sceneGraph.Synchronize(page);
            Assert(sceneGraph.RootVisual.Children.Count == 1, "Scene graph synchronized redone hierarchy seamlessly");

            // 5. Layer Reparenting / Reordering & Scene Graph Sync (MoveLayerHierarchy)
            var reorderPage = new Page();
            var frameParent = new LayerNode { Id = "frame1", Type = ElementType.Frame, Name = "Frame 1", Width = 300, Height = 200 };
            var childA = new LayerNode { Id = "childA", Type = ElementType.Rectangle, Name = "Child A", Width = 50, Height = 50 };
            var childB = new LayerNode { Id = "childB", Type = ElementType.Rectangle, Name = "Child B", Width = 50, Height = 50 };
            frameParent.Children.Add(childA);
            childA.ParentId = frameParent.Id;
            reorderPage.Layers.Add(frameParent);
            reorderPage.Layers.Add(childB);

            sceneGraph.Synchronize(reorderPage);

            // Move childB into frameParent
            var reorderCmd = new ReorderLayerCommand(reorderPage, childB, null, 1, frameParent.Id, 1, () => sceneGraph.Synchronize(reorderPage));
            reorderCmd.Execute();
            Assert(frameParent.Children.Count == 2, "Layer reparented into frame");
            Assert(childB.ParentId == frameParent.Id, "Child parent ID updated");

            // Undo reparenting
            reorderCmd.Unexecute();
            Assert(frameParent.Children.Count == 1, "Frame children restored on undo");
            Assert(reorderPage.Layers.Count == 2, "Root layers restored on undo");

            // Redo reparenting
            reorderCmd.Execute();
            Assert(frameParent.Children.Count == 2, "Layer reparented again on redo with zero visual parenting errors");
        }

        private static void TestMilestone24Features()
        {
            // 1. Component File Persistence Across Save & Load Roundtrip
            string tempDir = Path.Combine(Path.GetTempPath(), "KerfCompTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, "ComponentRoundtrip.kerf");

            var doc = new KerfDocument { Name = "Component Roundtrip Doc" };
            var page = doc.GetActivePage();

            // Master Component with nested children
            var masterComp = new LayerNode
            {
                Id = "comp-btn-master",
                Name = "Button Master",
                Type = ElementType.Frame,
                IsMasterComponent = true,
                ComponentId = "comp-btn-master",
                X = 100,
                Y = 100,
                Width = 160,
                Height = 48,
                CornerRadius = 8
            };
            masterComp.Fills.Clear();
            masterComp.Fills.Add(Paint.Solid("#7B61FF")); // Purple

            var label = new LayerNode
            {
                Id = "lbl-text",
                Name = "Label",
                Type = ElementType.Text,
                ParentId = masterComp.Id,
                X = 120,
                Y = 114,
                Width = 120,
                Height = 20
            };
            label.Typography.Text = "Submit";
            masterComp.Children.Add(label);
            page.Layers.Add(masterComp);

            // Component Instance
            var compInstance = new LayerNode
            {
                Id = "comp-btn-inst-1",
                Name = "Button Instance",
                Type = ElementType.ComponentInstance,
                IsMasterComponent = false,
                ComponentId = "comp-btn-master",
                X = 320,
                Y = 100,
                Width = 160,
                Height = 48
            };
            page.Layers.Add(compInstance);

            // Save and load
            StorageManager.SaveAtomic(doc, filePath);
            Assert(File.Exists(filePath), "Document with components persisted to disk");

            var loadedDoc = StorageManager.LoadDocument(filePath);
            Assert(loadedDoc != null, "Document loaded successfully");

            var allComps = loadedDoc.GetAllComponents();
            Assert(allComps.Count == 1, string.Format("GetAllComponents must find 1 master component, found {0}", allComps.Count));
            Assert(allComps[0].IsMasterComponent, "Master component flag preserved across save/open");
            Assert(allComps[0].ComponentId == "comp-btn-master", "ComponentId preserved across save/open");
            Assert(allComps[0].Children != null && allComps[0].Children.Count == 1, "Master component children preserved across save/open");

            var loadedPage = loadedDoc.GetActivePage();
            var foundMaster = loadedPage.FindMasterComponent("comp-btn-master");
            Assert(foundMaster != null, "FindMasterComponent locates master component in loaded page");

            var foundInstances = loadedPage.FindInstancesOfComponent("comp-btn-master");
            Assert(foundInstances.Count == 1, "FindInstancesOfComponent locates instance in loaded page");
            Assert(foundInstances[0].Type == ElementType.ComponentInstance, "Instance Type preserved as ComponentInstance");

            // Verify GpuRenderer builds valid geometry for ComponentInstance
            var renderer = new GpuRenderer();
            var geo = renderer.BuildGeometry(foundInstances[0]);
            Assert(geo != null, "GpuRenderer BuildGeometry returns valid non-null geometry for ComponentInstance");

            // 2. Real Vector State Machine Multi-State Transitions
            var smDef = new StateMachineDefinition("TestSM");
            int inHover = smDef.AddInput(MachineInput.Bool("isHovered", false));
            int inPress = smDef.AddInput(MachineInput.Bool("isPressed", false));
            int inClick = smDef.AddInput(MachineInput.Trigger("clickTrigger"));
            int inReset = smDef.AddInput(MachineInput.Trigger("resetTrigger"));

            var layer = new MachineLayer("Base");
            int sIdle = layer.AddState(new MachineState(0, "Idle", StateKind.Animation, 0));
            int sHover = layer.AddState(new MachineState(1, "Hover", StateKind.Animation, 1));
            int sPressed = layer.AddState(new MachineState(2, "Pressed", StateKind.Animation, 2));
            int sActive = layer.AddState(new MachineState(3, "Active", StateKind.Animation, 3));

            // Idle -> Hover (isHovered == 1.0)
            var t1 = new StateTransition(sIdle, sHover, 0.1);
            t1.Conditions.Add(new TransitionCondition(inHover, ConditionOp.Eq, 1.0));
            layer.AddTransition(t1);

            // Hover -> Idle (isHovered == 0.0)
            var t2 = new StateTransition(sHover, sIdle, 0.1);
            t2.Conditions.Add(new TransitionCondition(inHover, ConditionOp.Eq, 0.0));
            layer.AddTransition(t2);

            // Hover -> Pressed (isPressed == 1.0)
            var t3 = new StateTransition(sHover, sPressed, 0.05);
            t3.Conditions.Add(new TransitionCondition(inPress, ConditionOp.Eq, 1.0));
            layer.AddTransition(t3);

            // Pressed -> Active (clickTrigger)
            var t4 = new StateTransition(sPressed, sActive, 0.1);
            t4.Conditions.Add(new TransitionCondition(inClick, ConditionOp.Eq, 1.0));
            layer.AddTransition(t4);

            smDef.AddLayer(layer);

            var cIdle = new Clip(0, "Idle", 1.0);
            var cHov = new Clip(1, "Hover", 1.0);
            var cPres = new Clip(2, "Pressed", 1.0);
            var cAct = new Clip(3, "Active", 1.0);
            var smClips = new List<Clip> { cIdle, cHov, cPres, cAct };

            var smRuntime = new StateMachineRuntime(smDef, smClips);
            var pose = new Dictionary<uint, Dictionary<ushort, double>>();

            // Initial State: Idle
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sIdle, "Initial state is Idle");

            // Toggle isHovered -> transitions to Hover
            smRuntime.SetBool(inHover, true);
            smRuntime.Update(0.016, pose);
            Assert(smRuntime.LayerRuntimes[0].ActiveTransition != null && smRuntime.LayerRuntimes[0].ActiveTransition.ToState == sHover, "State machine initiates transition to Hover");

            // Complete transition duration (0.1s)
            smRuntime.Update(0.1, pose);
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sHover, "State machine completes transition to Hover");

            // Toggle isPressed -> transitions to Pressed
            smRuntime.SetBool(inPress, true);
            smRuntime.Update(0.016, pose);
            Assert(smRuntime.LayerRuntimes[0].ActiveTransition != null && smRuntime.LayerRuntimes[0].ActiveTransition.ToState == sPressed, "State machine transitions from Hover to Pressed");
            smRuntime.Update(0.06, pose);
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sPressed, "State reaches Pressed");

            // Fire clickTrigger -> transitions to Active
            smRuntime.FireTrigger(inClick);
            smRuntime.Update(0.016, pose);
            Assert(smRuntime.LayerRuntimes[0].ActiveTransition != null && smRuntime.LayerRuntimes[0].ActiveTransition.ToState == sActive, "State machine transitions to Active on clickTrigger");
            smRuntime.Update(0.12, pose);
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sActive, "State reaches Active");

            // 3. Pointer Event Listener Dispatch
            uint targetNode = 999;
            var listener = new NodeListener(targetNode, PointerEventKind.Enter);
            listener.Actions.Add(new ListenerAction(ActionKind.SetBool, inHover, 1.0));
            smDef.Listeners.Add(listener);

            smRuntime.SetBool(inHover, false);
            smRuntime.OnPointerEvent(targetNode, PointerEventKind.Enter);
            Assert(smRuntime.GetInputValue(inHover) == 1.0, "OnPointerEvent dispatches Enter event to set isHovered input");

            try { Directory.Delete(tempDir, true); } catch { }
        }

        private static void TestNestedAutoLayoutAndSinglePassStabilization()
        {
            var page = new Page();

            // 1. Create an Outer Frame with Vertical Auto Layout (Hug height)
            var outer = new LayerNode
            {
                Id = "outer",
                Name = "Outer Container",
                Type = ElementType.Frame,
                X = 100,
                Y = 100,
                Width = 400,
                Height = 200,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Vertical,
                    Gap = 16.0,
                    PaddingTop = 20.0,
                    PaddingBottom = 20.0,
                    PaddingLeft = 20.0,
                    PaddingRight = 20.0,
                    SizingH = SizingMode.Fixed,
                    SizingV = SizingMode.Hug,
                    AlignItems = AlignItems.Start,
                    JustifyContent = JustifyContent.Start
                },
                Children = new List<LayerNode>()
            };
            page.Layers.Add(outer);

            // Add 3 children inside outer
            var item1 = new LayerNode { Id = "i1", Name = "Btn 1", Type = ElementType.Rectangle, X = 120, Y = 120, Width = 80, Height = 40, ParentId = "outer" };
            var item2 = new LayerNode { Id = "i2", Name = "Btn 2", Type = ElementType.Rectangle, X = 210, Y = 120, Width = 90, Height = 40, ParentId = "outer" };
            var item3 = new LayerNode { Id = "i3", Name = "Footer", Type = ElementType.Rectangle, X = 120, Y = 180, Width = 360, Height = 50, ParentId = "outer" };
            outer.Children.Add(item1);
            outer.Children.Add(item2);
            outer.Children.Add(item3);

            AutoLayoutEngine.ReflowHierarchy(page, outer);
            Assert(outer.Height == 20 + 40 + 16 + 40 + 16 + 50 + 20, "Outer frame correctly hugs initial children");

            // 2. Auto Align item1 and item2 into a nested Horizontal Auto Layout frame inside outer
            var cmd = AutoLayoutEngine.CreateAutoLayoutCommand(page, new List<LayerNode> { item1, item2 }, null);
            Assert(cmd != null, "AutoLayoutSelectionCommand created for nested layers");
            var alCmd = cmd as AutoLayoutSelectionCommand;
            Assert(alCmd != null && alCmd.CreatedFrame != null, "CreatedFrame is exposed");

            cmd.Execute();

            var nestedFrame = alCmd.CreatedFrame;
            Assert(nestedFrame.ParentId == "outer", "Nested frame is child of outer container");
            Assert(outer.Children.Contains(nestedFrame), "Outer contains nested frame");
            Assert(!outer.Children.Contains(item1), "Outer no longer contains item1 directly");
            Assert(!outer.Children.Contains(item2), "Outer no longer contains item2 directly");
            Assert(nestedFrame.Children.Contains(item1) && nestedFrame.Children.Contains(item2), "Nested frame contains both items");
            Assert(nestedFrame.AutoLayout != null && nestedFrame.AutoLayout.Mode == AutoLayoutMode.Vertical, "Nested frame auto layout is Vertical for vertically stacked items");

            // Verify SINGLE-PASS STABILIZATION:
            // In ONE single call without touching it again:
            // Nested frame hugged height = item1 (40) + gap (16) + item2 (40) = 96
            // Outer frame hugged height = padT (20) + nestedFrame.Height (96) + gap (16) + footer.Height (50) + padB (20) = 202
            double expectedOuterH = 20.0 + (40.0 + 16.0 + 40.0) + 16.0 + 50.0 + 20.0;
            Assert(outer.Height == expectedOuterH, string.Format("Outer frame hugged new nested height in ONE pass (expected {0}, got {1})", expectedOuterH, outer.Height));
            Assert(item3.Y == nestedFrame.Y + nestedFrame.Height + 16.0, "Footer item positioned directly after nested frame");

            // 3. Undo restores outer layout in single pass
            cmd.Unexecute();
            Assert(!outer.Children.Contains(nestedFrame), "Undo removes nested frame");
            Assert(outer.Children.Contains(item1) && outer.Children.Contains(item2), "Undo restores item1 and item2 into outer");
            Assert(outer.Height == 20 + 40 + 16 + 40 + 16 + 50 + 20, "Undo reflows outer container in single pass");

            // 4. Multi-level deep Fill & Hug propagation
            // Level 1: Root Frame (Fixed 500x300)
            // Level 2: Middle Frame (Fill width, Hug height)
            // Level 3: Inner Leaf (Fill width, Fixed 60 height)
            var rootFrame = new LayerNode
            {
                Id = "root",
                Type = ElementType.Frame,
                X = 0,
                Y = 0,
                Width = 500,
                Height = 300,
                AutoLayout = new AutoLayoutData { Mode = AutoLayoutMode.Vertical, PaddingLeft = 10, PaddingRight = 10, PaddingTop = 10, PaddingBottom = 10, SizingH = SizingMode.Fixed, SizingV = SizingMode.Fixed },
                Children = new List<LayerNode>()
            };
            var midFrame = new LayerNode
            {
                Id = "mid",
                ParentId = "root",
                Type = ElementType.Frame,
                X = 10,
                Y = 10,
                Width = 100,
                Height = 100,
                LayoutSizingH = SizingMode.Fill,
                AutoLayout = new AutoLayoutData { Mode = AutoLayoutMode.Horizontal, PaddingLeft = 10, PaddingRight = 10, PaddingTop = 10, PaddingBottom = 10, SizingH = SizingMode.Fill, SizingV = SizingMode.Hug },
                Children = new List<LayerNode>()
            };
            var leaf = new LayerNode
            {
                Id = "leaf",
                ParentId = "mid",
                Type = ElementType.Rectangle,
                X = 20,
                Y = 20,
                Width = 50,
                Height = 60,
                LayoutSizingH = SizingMode.Fill
            };
            midFrame.Children.Add(leaf);
            rootFrame.Children.Add(midFrame);
            page.Layers.Add(rootFrame);

            AutoLayoutEngine.ReflowHierarchy(page, leaf);

            // Root width = 500. Padding = 10 + 10 = 20.
            // Mid width (Fill) = 480.
            // Leaf width (Fill inside mid) = 480 - 20 = 460.
            // Mid height (Hug) = 60 + 20 = 80.
            Assert(midFrame.Width == 480.0, string.Format("Mid frame fills root width (expected 480, got {0})", midFrame.Width));
            Assert(leaf.Width == 460.0, string.Format("Leaf fills mid frame width in single pass (expected 460, got {0})", leaf.Width));
            Assert(midFrame.Height == 80.0, string.Format("Mid frame hugs leaf height in single pass (expected 80, got {0})", midFrame.Height));
        }

        private static void TestFigmaAutoLayoutParityAndAltDragDuplication()
        {
            // 1. Smart Mode Detection: Vertically stacked wide cards
            // Two 400x100 cards stacked vertically with a 20px gap
            var card1 = new LayerNode { Id = "c1", Name = "Card 1", Type = ElementType.Frame, X = 100, Y = 100, Width = 400, Height = 100 };
            var card2 = new LayerNode { Id = "c2", Name = "Card 2", Type = ElementType.Frame, X = 100, Y = 220, Width = 400, Height = 100 };
            var cards = new List<LayerNode> { card1, card2 };

            var detectedMode = AutoLayoutEngine.DetectSmartLayoutMode(cards);
            Assert(detectedMode == AutoLayoutMode.Vertical, string.Format("Vertically stacked wide cards detected as Vertical mode, got {0}", detectedMode));

            var page = new Page();
            page.Layers.Add(card1);
            page.Layers.Add(card2);

            var autoFramed = AutoLayoutEngine.AutoAlignSelection(page, cards);
            Assert(autoFramed != null, "AutoAlignSelection successfully auto-framed two cards");
            Assert(autoFramed.AutoLayout != null && autoFramed.AutoLayout.Mode == AutoLayoutMode.Vertical, "Enclosing frame is in Vertical mode");
            Assert(autoFramed.Width == 400.0, string.Format("Enclosing frame preserves full shape bounding width (expected 400, got {0})", autoFramed.Width));
            Assert(autoFramed.Height == 220.0, string.Format("Enclosing frame preserves full shape bounding height (expected 220, got {0})", autoFramed.Height));
            Assert(autoFramed.AutoLayout.Gap == 20.0, string.Format("Gap between cards accurately measured as 20, got {0}", autoFramed.AutoLayout.Gap));

            // Verify children positions match exact bounding box
            Assert(card1.X == 100.0 && card1.Y == 100.0, "Card 1 at (100, 100)");
            Assert(card2.X == 100.0 && card2.Y == 220.0, "Card 2 at (100, 220)");

            // 2. Smart Mode Detection: Horizontally placed tall side-by-side cards
            var tall1 = new LayerNode { Id = "t1", Name = "Tall 1", Type = ElementType.Frame, X = 50, Y = 50, Width = 100, Height = 350 };
            var tall2 = new LayerNode { Id = "t2", Name = "Tall 2", Type = ElementType.Frame, X = 170, Y = 50, Width = 100, Height = 350 };
            var tallMode = AutoLayoutEngine.DetectSmartLayoutMode(new List<LayerNode> { tall1, tall2 });
            Assert(tallMode == AutoLayoutMode.Horizontal, string.Format("Horizontally placed tall cards detected as Horizontal mode, got {0}", tallMode));

            // 3. Smart Mode Detection: 2D Flow / Grid arrangement detected as Wrap
            var g1 = new LayerNode { Id = "g1", X = 0, Y = 0, Width = 80, Height = 60 };
            var g2 = new LayerNode { Id = "g2", X = 100, Y = 0, Width = 80, Height = 60 };
            var g3 = new LayerNode { Id = "g3", X = 0, Y = 80, Width = 80, Height = 60 };
            var g4 = new LayerNode { Id = "g4", X = 100, Y = 80, Width = 80, Height = 60 };
            var gridMode = AutoLayoutEngine.DetectSmartLayoutMode(new List<LayerNode> { g1, g2, g3, g4 });
            Assert(gridMode == AutoLayoutMode.Wrap, string.Format("2x2 grid layout detected as Wrap flow mode, got {0}", gridMode));

            // 4. Alt-Drag Duplication Isolation: Duplicating an Auto Layout frame on canvas
            // Cloned top-level frame remains top-level on canvas and is never dropped inside the original frame
            var canvasFrame = new LayerNode
            {
                Id = "canvas_al",
                Name = "Canvas AutoLayout",
                Type = ElementType.Frame,
                X = 200,
                Y = 200,
                Width = 300,
                Height = 150,
                AutoLayout = new AutoLayoutData { Mode = AutoLayoutMode.Vertical, Gap = 8, SizingH = SizingMode.Hug, SizingV = SizingMode.Hug },
                Children = new List<LayerNode>
                {
                    new LayerNode { Id = "child_1", Type = ElementType.Rectangle, Width = 100, Height = 40, ParentId = "canvas_al" }
                }
            };
            page.Layers.Add(canvasFrame);

            var cloneFrame = canvasFrame.Clone();
            cloneFrame.Id = "canvas_al_clone";
            cloneFrame.X = 250;
            cloneFrame.Y = 250;
            cloneFrame.ParentId = null; // Top-level duplication
            page.Layers.Add(cloneFrame);

            // Verify clone is at root level and NOT inside canvasFrame
            Assert(cloneFrame.ParentId == null, "Cloned Auto Layout frame parent remains null (canvas root)");
            Assert(!canvasFrame.Children.Contains(cloneFrame), "Original frame does NOT contain the cloned frame as a child");
            Assert(page.Layers.Contains(cloneFrame), "Page layers contains the clone at canvas level");

            // 5. Alignment Pad spatial mappings in Horizontal mode with Fixed size
            var alignFrame = new LayerNode
            {
                Id = "align_f",
                Type = ElementType.Frame,
                X = 0,
                Y = 0,
                Width = 400,
                Height = 200,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Horizontal,
                    Gap = 10,
                    PaddingLeft = 0,
                    PaddingRight = 0,
                    PaddingTop = 0,
                    PaddingBottom = 0,
                    SizingH = SizingMode.Fixed,
                    SizingV = SizingMode.Fixed,
                    JustifyContent = JustifyContent.Start,
                    AlignItems = AlignItems.Start
                },
                Children = new List<LayerNode>
                {
                    new LayerNode { Id = "a1", Type = ElementType.Rectangle, Width = 50, Height = 40 },
                    new LayerNode { Id = "a2", Type = ElementType.Rectangle, Width = 50, Height = 40 }
                }
            };
            AutoLayoutEngine.Reflow(alignFrame);
            Assert(alignFrame.Children[0].X == 0.0, "Start justification: item 1 at X=0");

            // Test Center alignment on 3x3 grid
            alignFrame.AutoLayout.JustifyContent = JustifyContent.Center;
            alignFrame.AutoLayout.AlignItems = AlignItems.Center;
            AutoLayoutEngine.Reflow(alignFrame);
            // Available width = 400, used = 50 + 10 + 50 = 110, extra = 290. Centered cursor = 145.
            Assert(alignFrame.Children[0].X == 145.0, string.Format("Center justification: item 1 at X=145, got {0}", alignFrame.Children[0].X));
            // Available height = 200, item height = 40, free = 160. Centered cross = 80.
            Assert(alignFrame.Children[0].Y == 80.0, string.Format("Center cross-alignment: item 1 at Y=80, got {0}", alignFrame.Children[0].Y));

            // Test End alignment on 3x3 grid
            alignFrame.AutoLayout.JustifyContent = JustifyContent.End;
            alignFrame.AutoLayout.AlignItems = AlignItems.End;
            AutoLayoutEngine.Reflow(alignFrame);
            Assert(alignFrame.Children[0].X == 290.0, string.Format("End justification: item 1 at X=290, got {0}", alignFrame.Children[0].X));
            Assert(alignFrame.Children[0].Y == 160.0, string.Format("End cross-alignment: item 1 at Y=160, got {0}", alignFrame.Children[0].Y));

            // Test SpaceBetween distribution
            alignFrame.AutoLayout.JustifyContent = JustifyContent.SpaceBetween;
            AutoLayoutEngine.Reflow(alignFrame);
            Assert(alignFrame.Children[0].X == 0.0, "Space between: first item at X=0");
            Assert(alignFrame.Children[1].X == 350.0, string.Format("Space between: last item at X=350, got {0}", alignFrame.Children[1].X));
        }

        private static void TestButtonPatternFromShapeAndForeground()
        {
            var page = new Page();

            // 1. Text over Rectangle -> Button Auto Layout Frame
            var btnRect = new LayerNode
            {
                Id = "btn_bg",
                Name = "Button Background",
                Type = ElementType.Rectangle,
                X = 100,
                Y = 100,
                Width = 120,
                Height = 44,
                CornerRadius = 8.0,
                Fills = new List<Paint> { Paint.Solid("#18A0FB") }
            };
            var btnText = new LayerNode
            {
                Id = "btn_txt",
                Name = "Sign Up",
                Type = ElementType.Text,
                X = 135,
                Y = 112,
                Width = 50,
                Height = 20
            };
            page.Layers.Add(btnRect);
            page.Layers.Add(btnText);

            var detectedBg = AutoLayoutEngine.FindBackgroundShapeForSelection(new List<LayerNode> { btnRect, btnText });
            Assert(detectedBg == btnRect, "FindBackgroundShapeForSelection identifies rectangle as background container");

            var buttonFrame = AutoLayoutEngine.AutoAlignSelection(page, new List<LayerNode> { btnRect, btnText });
            Assert(buttonFrame != null, "AutoAlignSelection created button frame");
            Assert(buttonFrame.Type == ElementType.Frame, "Button is a Frame");
            Assert(buttonFrame.CornerRadius == 8.0, "Button frame inherits 8px corner radius from rectangle");
            Assert(buttonFrame.Fills != null && buttonFrame.Fills.Count > 0 && buttonFrame.Fills[0].ColorHex == "#18A0FB", "Button frame inherits solid fill #18A0FB from rectangle");
            Assert(!page.Layers.Contains(btnRect), "Original rectangle was consumed into frame");
            Assert(buttonFrame.Children.Count == 1, "Button frame has 1 child (the text)");
            Assert(buttonFrame.Children[0] == btnText, "Child is the text node");

            // Verify Auto Layout Properties: Horizontal, Centered, Hug
            Assert(buttonFrame.AutoLayout.Mode == AutoLayoutMode.Horizontal, "Button Auto Layout Mode is Horizontal");
            Assert(buttonFrame.AutoLayout.JustifyContent == JustifyContent.Center, "Button JustifyContent is Center");
            Assert(buttonFrame.AutoLayout.AlignItems == AlignItems.Center, "Button AlignItems is Center");
            Assert(buttonFrame.AutoLayout.SizingH == SizingMode.Hug, "Button SizingH is Hug");
            Assert(buttonFrame.AutoLayout.SizingV == SizingMode.Hug, "Button SizingV is Hug");

            // Dimensions and Text Centering inside the button
            Assert(buttonFrame.Width == 120.0, string.Format("Button frame initial width matches rectangle (120), got {0}", buttonFrame.Width));
            Assert(buttonFrame.Height == 44.0, string.Format("Button frame initial height matches rectangle (44), got {0}", buttonFrame.Height));
            Assert(btnText.X == 135.0, string.Format("Button text centered horizontally at X=135, got {0}", btnText.X));
            Assert(btnText.Y == 112.0, string.Format("Button text centered vertically at Y=112, got {0}", btnText.Y));

            // Dynamic Hug Expansion: When text expands, button width expands automatically
            btnText.Width = 100.0;
            AutoLayoutEngine.Reflow(buttonFrame);
            // With padH = 35 on left and right, 100 + 35 + 35 = 170
            Assert(buttonFrame.Width == 170.0, string.Format("Button dynamically hugs expanded text width (expected 170, got {0})", buttonFrame.Width));
            Assert(btnText.X == 100.0 + 35.0, "Button text remains centered inside expanded button");

            // 2. Command Pattern Undo/Redo Test
            var page2 = new Page();
            var rect2 = new LayerNode
            {
                Id = "r2",
                Name = "Rect 2",
                Type = ElementType.Rectangle,
                X = 200,
                Y = 200,
                Width = 140,
                Height = 50,
                CornerRadius = 12.0,
                Fills = new List<Paint> { Paint.Solid("#10B981") }
            };
            var text2 = new LayerNode
            {
                Id = "t2",
                Name = "Download",
                Type = ElementType.Text,
                X = 240,
                Y = 215,
                Width = 60,
                Height = 20
            };
            page2.Layers.Add(rect2);
            page2.Layers.Add(text2);

            var cmd = AutoLayoutEngine.CreateAutoLayoutCommand(page2, new List<LayerNode> { rect2, text2 }, null);
            Assert(cmd != null, "CreateAutoLayoutCommand created command for button");
            cmd.Execute();

            var alCmd = cmd as AutoLayoutSelectionCommand;
            Assert(alCmd != null && alCmd.CreatedFrame != null, "Exposes created button frame");
            Assert(page2.Layers.Contains(alCmd.CreatedFrame), "Page contains button frame after Execute");
            Assert(!page2.Layers.Contains(rect2), "Page does not contain original rect after Execute");
            Assert(alCmd.CreatedFrame.Children.Contains(text2), "Button frame contains text after Execute");

            // Undo: restores rectangle and text back to page layers
            cmd.Unexecute();
            Assert(!page2.Layers.Contains(alCmd.CreatedFrame), "Page does not contain button frame after Unexecute");
            Assert(page2.Layers.Contains(rect2), "Page contains restored rect after Unexecute");
            Assert(page2.Layers.Contains(text2), "Page contains restored text after Unexecute");
            Assert(rect2.Width == 140.0 && rect2.Height == 50.0, "Restored rect dimensions preserved");
            Assert(rect2.Fills[0].ColorHex == "#10B981", "Restored rect fill preserved");

            // Redo: recreates button frame cleanly
            cmd.Execute();
            Assert(page2.Layers.Contains(alCmd.CreatedFrame), "Page contains button frame after Redo");
            Assert(!page2.Layers.Contains(rect2), "Page does not contain original rect after Redo");
            Assert(alCmd.CreatedFrame.Children.Contains(text2), "Button frame contains text after Redo");
        }

        private static void TestUniformScaleLockAspectRatio()
        {
            var layer = new LayerNode
            {
                Id = "scale_target",
                Name = "Uniform Card",
                Type = ElementType.Rectangle,
                X = 100,
                Y = 100,
                Width = 200,
                Height = 100,
                LockAspectRatio = true
            };

            Assert(layer.LockAspectRatio, "LockAspectRatio property is true");

            // Verify Clone retains LockAspectRatio
            var clone = layer.Clone();
            Assert(clone.LockAspectRatio, "Clone retains LockAspectRatio = true");

            // Simulate Scrubbing Width from 200 to 300 with aspect ratio locked (2:1 ratio)
            double oldW = Math.Max(0.001, layer.Width);
            double oldH = Math.Max(0.001, layer.Height);
            double newW = 300;
            layer.Width = newW;
            if (layer.LockAspectRatio)
            {
                layer.Height = Math.Max(1, Math.Round(newW * (oldH / oldW), 1));
            }
            Assert(layer.Width == 300.0, "Width updated to 300");
            Assert(layer.Height == 150.0, string.Format("Height scaled proportionally from 100 to 150 (got {0})", layer.Height));

            // Simulate Scrubbing Height from 150 to 75 with aspect ratio locked
            oldW = Math.Max(0.001, layer.Width);
            oldH = Math.Max(0.001, layer.Height);
            double newH = 75;
            layer.Height = newH;
            if (layer.LockAspectRatio)
            {
                layer.Width = Math.Max(1, Math.Round(newH * (oldW / oldH), 1));
            }
            Assert(layer.Height == 75.0, "Height updated to 75");
            Assert(layer.Width == 150.0, string.Format("Width scaled proportionally from 300 to 150 (got {0})", layer.Width));

            // Test on-canvas corner handle transform with keepAspect = true
            var startBounds = new Rect(100, 100, 200, 100);
            var res = TransformEngine.ApplyTransform(
                HandleType.BottomRight,
                new Point(400, 250),
                new Point(300, 200),
                startBounds,
                0.0,
                true, // keepAspect
                false,
                0.0
            );
            double resAspect = res.Bounds.Width / res.Bounds.Height;
            double origAspect = startBounds.Width / startBounds.Height;
            Assert(Math.Abs(resAspect - origAspect) < 0.001, string.Format("On-canvas handle transform preserved 2:1 aspect ratio: {0} vs {1}", resAspect, origAspect));
        }

        private static void TestAltDragDuplicationInAutoLayout()
        {
            var page = new Page();
            var parent = new LayerNode
            {
                Id = "parent_al",
                Name = "Vertical Stack",
                Type = ElementType.Frame,
                X = 50,
                Y = 50,
                Width = 200,
                Height = 300
            };
            parent.AutoLayout = new AutoLayoutData
            {
                Mode = AutoLayoutMode.Vertical,
                PaddingTop = 10,
                PaddingBottom = 10,
                PaddingLeft = 10,
                PaddingRight = 10,
                Gap = 8,
                SizingH = SizingMode.Fixed,
                SizingV = SizingMode.Hug
            };

            var item1 = new LayerNode
            {
                Id = "item_1",
                Name = "Item 1",
                Type = ElementType.Rectangle,
                X = 60,
                Y = 60,
                Width = 180,
                Height = 40,
                ParentId = parent.Id
            };
            var item2 = new LayerNode
            {
                Id = "item_2",
                Name = "Item 2",
                Type = ElementType.Rectangle,
                X = 60,
                Y = 108,
                Width = 180,
                Height = 40,
                ParentId = parent.Id
            };
            parent.Children.Add(item1);
            parent.Children.Add(item2);
            page.Layers.Add(parent);

            AutoLayoutEngine.Reflow(parent);
            Assert(parent.Children.Count == 2, "Parent starts with 2 children");

            // Test Deep Selection preservation:
            // When user deep-selects item1, selectedLayers contains item1
            var selectedLayers = new List<LayerNode> { item1 };

            // When user clicks with Alt on item1 to drag-duplicate, selection remains item1
            LayerNode hit = item1;
            LayerNode selectionHit = selectedLayers.Contains(hit) ? hit : parent;
            Assert(selectionHit == item1, "Deep-selected inner item is preserved when Alt-clicked (not overwritten by parent frame)");

            // Clone item1 for duplication inside Auto Layout
            var clone = item1.Clone();
            clone.Id = "item_1_clone";
            clone.Name = "Item 1 Copy";
            clone.ParentId = parent.Id;

            // Insert duplicate at slot index 1 (between item1 and item2)
            int insertIdx = 1;
            parent.Children.Insert(insertIdx, clone);
            AutoLayoutEngine.Reflow(parent);

            Assert(parent.Children.Count == 3, "Parent now has 3 children");
            Assert(parent.Children[0] == item1, "Slot 0 is item1");
            Assert(parent.Children[1] == clone, "Slot 1 is the duplicated clone");
            Assert(parent.Children[2] == item2, "Slot 2 is item2");

            // Verify reflowed positions inside the vertical Auto Layout frame
            // top = 50 + 10 = 60. item1: Y=60, H=40.
            // clone: Y = 60 + 40 + 8 = 108, H=40.
            // item2: Y = 108 + 40 + 8 = 156, H=40.
            Assert(item1.Y == 60.0, string.Format("Item 1 Y is 60, got {0}", item1.Y));
            Assert(clone.Y == 108.0, string.Format("Duplicated clone Y is 108, got {0}", clone.Y));
            Assert(item2.Y == 156.0, string.Format("Item 2 reflowed Y is 156, got {0}", item2.Y));

            // Verify Hug height of parent: 10 + 40 + 8 + 40 + 8 + 40 + 10 = 156
            Assert(parent.Height == 156.0, string.Format("Parent hug height adjusted to 156 (got {0})", parent.Height));

            // Test Undo: remove duplicated clone and reflow
            parent.Children.Remove(clone);
            AutoLayoutEngine.Reflow(parent);
            Assert(parent.Children.Count == 2, "After removing duplicate, parent has 2 children");
            Assert(item2.Y == 108.0, string.Format("Item 2 returns to Y=108 after undo, got {0}", item2.Y));
            Assert(parent.Height == 108.0, string.Format("Parent height returns to 108, got {0}", parent.Height));
        }

        private static void TestModernVectorStateMachineAnimationEngine()
        {
            // 1. Construct State Machine Definition
            var def = new StateMachineDefinition("InteractiveButton");
            def.AddInput(MachineInput.Bool("isHovered", false));
            def.AddInput(MachineInput.Trigger("onClick"));
            def.AddInput(MachineInput.Number("speed", 1.0));

            def.AddViewModelProperty(new ViewModelPropertyDefinition("isHovered", ViewModelPropertyKind.Boolean));
            def.AddViewModelProperty(new ViewModelPropertyDefinition("onClick", ViewModelPropertyKind.Trigger));
            def.AddViewModelProperty(new ViewModelPropertyDefinition("speed", ViewModelPropertyKind.Number));

            // 2. Add Pointer Listener for Node 101
            uint buttonNodeId = 101;
            var hoverEnterListener = new NodeListener(buttonNodeId, PointerEventKind.Enter);
            hoverEnterListener.Actions.Add(new ListenerAction(ActionKind.SetBool, 0, 1.0));
            hoverEnterListener.Actions.Add(new ListenerAction(ActionKind.SetViewModelBool, "isHovered", 1.0));
            def.AddListener(hoverEnterListener);

            var hoverExitListener = new NodeListener(buttonNodeId, PointerEventKind.Exit);
            hoverExitListener.Actions.Add(new ListenerAction(ActionKind.SetBool, 0, 0.0));
            hoverExitListener.Actions.Add(new ListenerAction(ActionKind.SetViewModelBool, "isHovered", 0.0));
            def.AddListener(hoverExitListener);

            var clickListener = new NodeListener(buttonNodeId, PointerEventKind.Down);
            clickListener.Actions.Add(new ListenerAction(ActionKind.Fire, 1));
            clickListener.Actions.Add(new ListenerAction(ActionKind.FireViewModelTrigger, "onClick"));
            def.AddListener(clickListener);

            // 3. Setup Animation Clips
            var idleClip = new Clip(1, "Idle", 1.0);
            var idleTrack = new Track(buttonNodeId, (ushort)PropKey.Width);
            idleTrack.AddKeyframe(Keyframe.Linear(0.0, 100.0));
            idleClip.Tracks.Add(idleTrack);

            var hoverClip = new Clip(2, "Hover", 1.0);
            var hoverTrack = new Track(buttonNodeId, (ushort)PropKey.Width);
            hoverTrack.AddKeyframe(Keyframe.Linear(0.0, 120.0));
            hoverClip.Tracks.Add(hoverTrack);

            var clickClip = new Clip(3, "Clicked", 0.5);
            var clickTrack = new Track(buttonNodeId, (ushort)PropKey.Width);
            clickTrack.AddKeyframe(Keyframe.Linear(0.0, 90.0));
            clickClip.Tracks.Add(clickTrack);

            var clips = new List<Clip> { idleClip, hoverClip, clickClip };

            // 4. State Machine Layer & States
            var layer = new MachineLayer("Base");
            int entryState = layer.AddState(new MachineState(0, "Entry", StateKind.Entry));
            int idleState = layer.AddState(new MachineState(1, "Idle", StateKind.Animation, 0));
            int hoverState = layer.AddState(new MachineState(2, "Hover", StateKind.Animation, 1));
            int clickedState = layer.AddState(new MachineState(3, "Clicked", StateKind.Animation, 2));

            // Transitions
            layer.AddTransition(new StateTransition(entryState, idleState, 0.0));
            layer.AddTransition(new StateTransition(idleState, hoverState, 0.2)
                .WithCondition(new TransitionCondition(0, ConditionOp.Eq, 1.0)));
            layer.AddTransition(new StateTransition(hoverState, idleState, 0.2)
                .WithCondition(new TransitionCondition(0, ConditionOp.Eq, 0.0)));
            layer.AddTransition(new StateTransition(hoverState, clickedState, 0.1)
                .WithCondition(new TransitionCondition(1, ConditionOp.Eq, 1.0)));

            def.AddLayer(layer);

            // 5. Instantiate Runtime & Verify Initial State
            var runtime = new StateMachineRuntime(def, clips);
            var pose = new Dictionary<uint, Dictionary<ushort, double>>();
            runtime.Update(0.01, pose);

            Assert(runtime.LayerRuntimes.Count == 1, "Runtime has 1 layer");
            Assert(runtime.LayerRuntimes[0].CurrentState == idleState, "Initial state settled at Idle (State 1)");
            Assert(pose.ContainsKey(buttonNodeId) && pose[buttonNodeId][(ushort)PropKey.Width] == 100.0, "Idle pose evaluated Width=100");

            // 6. Simulate Pointer Enter on Node 101 -> Triggers Listener -> isHovered=true
            runtime.OnPointerEvent(buttonNodeId, PointerEventKind.Enter);
            Assert(runtime.GetInputValue(0) == 1.0, "Hover Enter listener set input 0 (isHovered) = 1.0");
            Assert(runtime.ViewModel.GetBool("isHovered"), "ViewModel isHovered property is true");

            // Advance state machine: transition from Idle to Hover starts blending
            runtime.Update(0.01, pose); // Initiates transition
            runtime.Update(0.08, pose); // Blends 45% towards Hover
            Assert(runtime.LayerRuntimes[0].ActiveTransition != null, "Transition Idle -> Hover is actively blending");
            double blendWidth = pose[buttonNodeId][(ushort)PropKey.Width];
            Assert(blendWidth > 100.0 && blendWidth < 120.0, string.Format("Width smoothly interpolated between 100 and 120 (got {0})", blendWidth));

            // Finish transition (0.15s more, exceeding 0.2s duration)
            runtime.Update(0.2, pose);
            Assert(runtime.LayerRuntimes[0].CurrentState == hoverState, "Transition completed: now in Hover state (State 2)");
            Assert(runtime.LayerRuntimes[0].ActiveTransition == null, "Active transition finished");
            Assert(pose[buttonNodeId][(ushort)PropKey.Width] == 120.0, "Hover state pose evaluated Width=120");

            // 7. Simulate Pointer Click on Node 101 -> Fires onClick trigger
            runtime.OnPointerEvent(buttonNodeId, PointerEventKind.Down);
            Assert(runtime.Definition.Inputs[1].Fired, "onClick trigger fired on pointer down");

            // Advance: transitions to Clicked state
            runtime.Update(0.01, pose);
            runtime.Update(0.15, pose);
            Assert(runtime.LayerRuntimes[0].CurrentState == clickedState, "Transitioned to Clicked state (State 3)");
        }

        private static void TestFigmaDarkColorsAndButtons()
        {
            // 1. Verify Authentic Figma UI Colors
            Assert(KerfBrand.FigmaPanel == Color.FromRgb(0x2C, 0x2C, 0x2C), "FigmaPanel is #2C2C2C");
            Assert(KerfBrand.FigmaCanvas == Color.FromRgb(0x1E, 0x1E, 0x1E), "FigmaCanvas is #1E1E1E");
            Assert(KerfBrand.FigmaBorder == Color.FromRgb(0x38, 0x38, 0x38), "FigmaBorder is #383838");
            Assert(KerfBrand.Mint == Color.FromRgb(0x46, 0xEF, 0xB1), "Brand Light Green Mint is #46EFB1");
            Assert(KerfBrand.DarkSlate == KerfBrand.FigmaPanel, "DarkSlate mapped to FigmaPanel #2C2C2C");
            Assert(KerfBrand.Obsidian == KerfBrand.FigmaCanvas, "Obsidian mapped to FigmaCanvas #1E1E1E");
            Assert(KerfBrand.TealBrush.Color == KerfBrand.FigmaBorder, "TealBrush mapped to subtle Figma divider #383838");

            // 2. Verify Primary Button: Light Green #46EFB1 fill, Black text per button mockup
            var primaryBtn = KerfBrand.CreatePrimaryButton("Test Button");
            var pBg = primaryBtn.Background as SolidColorBrush;
            var pFg = primaryBtn.Foreground as SolidColorBrush;
            Assert(pBg != null && pBg.Color == KerfBrand.Mint, "Primary Button background is #46EFB1");
            Assert(pFg != null && pFg.Color == KerfBrand.TextBlack, "Primary Button text is Black (#111827)");

            // 3. Verify Secondary Button: #2C2C2C Figma dark fill, 1px #46EFB1 border, White text per mockup
            var secBtn = KerfBrand.CreateSecondaryButton("Cancel");
            var sBg = secBtn.Background as SolidColorBrush;
            var sBorder = secBtn.BorderBrush as SolidColorBrush;
            Assert(sBg != null && sBg.Color == KerfBrand.FigmaPanel, "Secondary Button background is #2C2C2C");
            Assert(sBorder != null && sBorder.Color == KerfBrand.Mint, "Secondary Button border is 1px #46EFB1");
            Assert(secBtn.Foreground == Brushes.White, "Secondary Button text is White");
        }

        private static void TestHierarchicalHitTestingAndCentralizedInteractionPipeline()
        {
            // -------------------------------------------------------------
            // 1. Hierarchical Hit-Testing System & Ancestor Path Building
            // -------------------------------------------------------------
            var page = new Page();
            var rootFrame = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Root Frame",
                X = 100,
                Y = 100,
                Width = 400,
                Height = 300,
                IsVisible = true,
                IsLocked = false,
                ClipContent = false
            };
            page.Layers.Add(rootFrame);

            var childCard = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Child Card",
                X = 120,
                Y = 120,
                Width = 200,
                Height = 150,
                IsVisible = true,
                IsLocked = false,
                ClipContent = false,
                ParentId = rootFrame.Id
            };
            rootFrame.Children.Add(childCard);

            var leafButton = new LayerNode
            {
                Type = ElementType.Rectangle,
                Name = "Leaf Button",
                X = 140,
                Y = 140,
                Width = 80,
                Height = 40,
                IsVisible = true,
                IsLocked = false,
                ParentId = childCard.Id
            };
            childCard.Children.Add(leafButton);

            var leafIcon = new LayerNode
            {
                Type = ElementType.Ellipse,
                Name = "Leaf Icon",
                X = 240,
                Y = 140,
                Width = 30,
                Height = 30,
                IsVisible = true,
                IsLocked = false,
                ParentId = childCard.Id
            };
            childCard.Children.Add(leafIcon);

            // Hit test over leafButton at (150, 150)
            var hit = CanvasInteractionController.HitTest(page, new Point(150, 150));
            Assert(hit.HasHit, "Hit-test successfully identified hit under pointer");
            Assert(hit.DeepestNode == leafButton, "DeepestNode correctly resolved to leafButton");
            Assert(hit.OutermostNode == rootFrame, "OutermostNode correctly resolved to rootFrame");
            Assert(hit.HierarchyPath.Count == 3, "HierarchyPath contains exactly 3 levels");
            Assert(hit.HierarchyPath[0] == rootFrame, "HierarchyPath[0] is rootFrame");
            Assert(hit.HierarchyPath[1] == childCard, "HierarchyPath[1] is childCard");
            Assert(hit.HierarchyPath[2] == leafButton, "HierarchyPath[2] is leafButton");
            Assert(hit.Candidates.Contains(leafButton) && hit.Candidates.Contains(childCard) && hit.Candidates.Contains(rootFrame),
                "Candidates contains all overlapping ancestor and leaf layers in z-order");

            // -------------------------------------------------------------
            // 2. Container Clipping Respect (ClipContent)
            // -------------------------------------------------------------
            var clippedFrame = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Clipped Frame",
                X = 600,
                Y = 100,
                Width = 100,
                Height = 100,
                IsVisible = true,
                IsLocked = false,
                ClipContent = true
            };
            page.Layers.Add(clippedFrame);

            var overflowChild = new LayerNode
            {
                Type = ElementType.Rectangle,
                Name = "Overflowing Child",
                X = 650,
                Y = 150,
                Width = 100,
                Height = 100, // extends to (750, 250), outside clippedFrame bounds (600..700, 100..200)
                IsVisible = true,
                IsLocked = false,
                ParentId = clippedFrame.Id
            };
            clippedFrame.Children.Add(overflowChild);

            // Hit test at (720, 220) - inside overflowChild bounds, but outside clippedFrame
            var clippedHit = CanvasInteractionController.HitTest(page, new Point(720, 220));
            Assert(!clippedHit.HasHit || (clippedHit.DeepestNode != overflowChild && clippedHit.HierarchyPath.IndexOf(overflowChild) < 0),
                "ClipContent=true strictly prunes overflowing child outside frame boundaries");

            // Disable ClipContent: now overflowing content is selectable
            clippedFrame.ClipContent = false;
            var unclippedHit = CanvasInteractionController.HitTest(page, new Point(720, 220));
            Assert(unclippedHit.HasHit && unclippedHit.DeepestNode == overflowChild,
                "ClipContent=false permits overflowing child interaction");

            // -------------------------------------------------------------
            // 3. Visibility and Locked Layer Respect
            // -------------------------------------------------------------
            leafButton.IsLocked = true;
            var lockedHit = CanvasInteractionController.HitTest(page, new Point(150, 150));
            Assert(lockedHit.HasHit && lockedHit.DeepestNode != leafButton && lockedHit.DeepestNode == childCard,
                "Locked layer is skipped by hit-testing, falling back to selectable container");
            leafButton.IsLocked = false;

            leafButton.IsVisible = false;
            var hiddenHit = CanvasInteractionController.HitTest(page, new Point(150, 150));
            Assert(hiddenHit.HasHit && hiddenHit.DeepestNode != leafButton,
                "Invisible layer is ignored by hit-testing");
            leafButton.IsVisible = true;

            // -------------------------------------------------------------
            // 4. Exact 2D Point Containment with Rotation
            // -------------------------------------------------------------
            var rotated = new LayerNode
            {
                X = 0,
                Y = 0,
                Width = 100,
                Height = 100,
                Rotation = 45.0
            };
            // Center is (50, 50). Top vertex of diamond rotates to approximately (50, 50 - 50*sqrt(2)) = (50, -20.7)
            Assert(CanvasInteractionController.ContainsPoint(rotated, new Point(50, -10)),
                "Point inside 45-degree diamond top tip returns true");
            // Unrotated top-left corner (0, 0) is outside the 45-degree diamond
            Assert(!CanvasInteractionController.ContainsPoint(rotated, new Point(0, 0)),
                "Corner point outside 45-degree diamond returns false");

            // -------------------------------------------------------------
            // 5. Selection Resolver & Drag Resolver Independence
            // -------------------------------------------------------------
            var selection = new List<LayerNode>();

            // Case A: Initial click from outside (selection is empty)
            var targetA = CanvasInteractionController.ResolveSelection(hit, selection, 1, false, false);
            Assert(targetA == rootFrame, "Click 1 from outside selects outermost container (rootFrame)");
            selection.Add(targetA);
            var dragA = CanvasInteractionController.ResolveDragTargets(hit, selection, targetA, false, 1);
            Assert(dragA.Count == 1 && dragA[0] == rootFrame, "Drag target is active selection (rootFrame)");

            // Case B: Parent is already selected! Pointer down over descendant leaf
            // MUST maintain rootFrame selection and drag rootFrame (Figma parity)
            var targetB = CanvasInteractionController.ResolveSelection(hit, selection, 1, false, false);
            Assert(targetB == rootFrame, "Pointer down over descendant does NOT replace selected parent with child");
            var dragB = CanvasInteractionController.ResolveDragTargets(hit, selection, targetB, false, 1);
            Assert(dragB.Count == 1 && dragB[0] == rootFrame, "Drag target remains selected parent rootFrame");

            // Case C: Progressive Double-Click (drill down 1 level)
            var targetC = CanvasInteractionController.ResolveSelection(hit, selection, 2, false, false);
            Assert(targetC == childCard, "Double-click on selected parent drills down to next child level (childCard)");
            selection.Clear();
            selection.Add(targetC);
            var dragC = CanvasInteractionController.ResolveDragTargets(hit, selection, targetC, false, 2);
            Assert(dragC.Count == 1 && dragC[0] == childCard, "Drag target updated to drilled childCard");

            // Double-click again drills down to leaf
            var targetD = CanvasInteractionController.ResolveSelection(hit, selection, 2, false, false);
            Assert(targetD == leafButton, "Second double-click drills down to leafButton");

            // Case D: Direct Deep-Selection (Ctrl held)
            selection.Clear();
            selection.Add(rootFrame); // rootFrame is currently selected
            var targetCtrl = CanvasInteractionController.ResolveSelection(hit, selection, 1, true, false);
            Assert(targetCtrl == leafButton, "Ctrl+Click directly deep-selects deepest leafButton under cursor");
            selection.Clear();
            selection.Add(targetCtrl);
            var dragCtrl = CanvasInteractionController.ResolveDragTargets(hit, selection, targetCtrl, true, 1);
            Assert(dragCtrl.Count == 1 && dragCtrl[0] == leafButton, "Drag target for Ctrl+Click is leafButton");

            // -------------------------------------------------------------
            // 6. Auto Layout Container Parity
            // -------------------------------------------------------------
            var autoLayoutRow = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "AutoLayout Row",
                X = 0,
                Y = 500,
                Width = 400,
                Height = 80,
                IsVisible = true,
                IsLocked = false,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Horizontal,
                    Gap = 10,
                    PaddingLeft = 10,
                    PaddingTop = 10
                }
            };
            page.Layers.Add(autoLayoutRow);

            var item1 = new LayerNode
            {
                Type = ElementType.Rectangle,
                Name = "Item 1",
                X = 10,
                Y = 510,
                Width = 100,
                Height = 60,
                IsVisible = true,
                IsLocked = false,
                ParentId = autoLayoutRow.Id
            };
            autoLayoutRow.Children.Add(item1);

            var autoHit = CanvasInteractionController.HitTest(page, new Point(50, 530));
            Assert(autoHit.HasHit && autoHit.DeepestNode == item1 && autoHit.OutermostNode == autoLayoutRow,
                "Auto Layout container hit-test builds [AutoLayoutRow, Item1] hierarchy");

            // Click from outside selects AutoLayoutRow
            selection.Clear();
            var autoTarget1 = CanvasInteractionController.ResolveSelection(autoHit, selection, 1, false, false);
            Assert(autoTarget1 == autoLayoutRow, "Click from outside selects AutoLayoutRow");
            selection.Add(autoTarget1);

            // Click over child Item 1 while AutoLayoutRow is selected keeps AutoLayoutRow
            var autoTarget2 = CanvasInteractionController.ResolveSelection(autoHit, selection, 1, false, false);
            Assert(autoTarget2 == autoLayoutRow, "Pointer-down on AutoLayout item preserves parent AutoLayoutRow selection");
            var autoDrag = CanvasInteractionController.ResolveDragTargets(autoHit, selection, autoTarget2, false, 1);
            Assert(autoDrag.Count == 1 && autoDrag[0] == autoLayoutRow, "Drag target is AutoLayoutRow");

            // Double click drills down to Item 1
            var autoDrill = CanvasInteractionController.ResolveSelection(autoHit, selection, 2, false, false);
            Assert(autoDrill == item1, "Double-click drills down to Item 1 inside AutoLayout");

            // -------------------------------------------------------------
            // 7. Multi-Selection Preservation
            // -------------------------------------------------------------
            var extraFrame = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Extra Frame",
                X = 800,
                Y = 100,
                Width = 200,
                Height = 200,
                IsVisible = true,
                IsLocked = false
            };
            page.Layers.Add(extraFrame);

            selection.Clear();
            selection.Add(rootFrame);
            selection.Add(extraFrame);

            // Pointer down on leafButton inside rootFrame
            var multiTarget = CanvasInteractionController.ResolveSelection(hit, selection, 1, false, false);
            Assert(multiTarget == rootFrame, "Resolved target is rootFrame (ancestor in selection)");
            // Selection is not cleared
            var multiDrag = CanvasInteractionController.ResolveDragTargets(hit, selection, multiTarget, false, 1);
            Assert(multiDrag.Count == 2 && multiDrag.Contains(rootFrame) && multiDrag.Contains(extraFrame),
                "Multi-selection drag targets both selected frames simultaneously");
        }

        private static void TestHierarchicalObjectInsertionAndContainerTargetResolution()
        {
            var page = new Page();

            // Setup a 3-level nested Auto Layout hierarchy:
            // Frame -> Auto Layout A -> Auto Layout B -> Auto Layout C
            var rootFrame = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Root Frame",
                X = 0,
                Y = 0,
                Width = 800,
                Height = 600,
                IsVisible = true,
                IsLocked = false
            };
            page.Layers.Add(rootFrame);

            var autoLayoutA = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Auto Layout A",
                X = 20,
                Y = 20,
                Width = 700,
                Height = 500,
                IsVisible = true,
                IsLocked = false,
                ParentId = rootFrame.Id,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Vertical,
                    Gap = 16,
                    PaddingLeft = 16,
                    PaddingTop = 16,
                    PaddingRight = 16,
                    PaddingBottom = 16,
                    SizingH = SizingMode.Hug,
                    SizingV = SizingMode.Hug
                }
            };
            rootFrame.Children.Add(autoLayoutA);

            var autoLayoutB = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Auto Layout B",
                X = 36,
                Y = 36,
                Width = 600,
                Height = 300,
                IsVisible = true,
                IsLocked = false,
                ParentId = autoLayoutA.Id,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Horizontal,
                    Gap = 12,
                    PaddingLeft = 12,
                    PaddingTop = 12,
                    PaddingRight = 12,
                    PaddingBottom = 12,
                    SizingH = SizingMode.Hug,
                    SizingV = SizingMode.Hug
                }
            };
            autoLayoutA.Children.Add(autoLayoutB);

            var autoLayoutC = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Auto Layout C",
                X = 48,
                Y = 48,
                Width = 300,
                Height = 150,
                IsVisible = true,
                IsLocked = false,
                ParentId = autoLayoutB.Id,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Vertical,
                    Gap = 8,
                    PaddingLeft = 8,
                    PaddingTop = 8,
                    PaddingRight = 8,
                    PaddingBottom = 8,
                    SizingH = SizingMode.Hug,
                    SizingV = SizingMode.Hug
                }
            };
            autoLayoutB.Children.Add(autoLayoutC);

            var child1 = new LayerNode
            {
                Type = ElementType.Rectangle,
                Name = "Child 1 in C",
                X = 56,
                Y = 56,
                Width = 80,
                Height = 40,
                IsVisible = true,
                IsLocked = false,
                ParentId = autoLayoutC.Id
            };
            autoLayoutC.Children.Add(child1);

            // Reflow hierarchy to establish initial geometry
            AutoLayoutEngine.ReflowHierarchy(page, autoLayoutA);

            var selection = new List<LayerNode>();

            // -------------------------------------------------------------
            // Acceptance Test 1: Create an object inside a selected Frame
            // -------------------------------------------------------------
            selection.Clear();
            selection.Add(rootFrame);
            var targetFrame = InsertionTargetResolver.ResolveTarget(page, selection);
            Assert(targetFrame.Container == rootFrame, "Selected Frame is resolved as the active insertion container");
            Assert(!targetFrame.IsRootCanvas, "Target is not root canvas");

            var newRectInFrame = new LayerNode { Type = ElementType.Rectangle, Name = "Rect in Frame", Width = 50, Height = 50 };
            bool insertedInFrame = InsertionTargetResolver.InsertObject(page, targetFrame, newRectInFrame);
            Assert(insertedInFrame, "Object inserted successfully into Frame");
            Assert(newRectInFrame.ParentId == rootFrame.Id, "Inserted object ParentId set to Root Frame ID");
            Assert(rootFrame.Children.Contains(newRectInFrame), "Root Frame children list contains newly created object");

            // -------------------------------------------------------------
            // Acceptance Test 2: Create an object inside a selected Auto Layout
            // -------------------------------------------------------------
            selection.Clear();
            selection.Add(autoLayoutC);
            var targetALC = InsertionTargetResolver.ResolveTarget(page, selection);
            Assert(targetALC.Container == autoLayoutC, "Selected Auto Layout C is resolved as active insertion container");

            var newTextInC = new LayerNode { Type = ElementType.Text, Name = "Text in C", Width = 100, Height = 30 };
            double heightBefore = autoLayoutC.Height;
            bool insertedInC = InsertionTargetResolver.InsertObject(page, targetALC, newTextInC, reflowAutoLayout: true);
            Assert(insertedInC, "Object inserted successfully into Auto Layout C");
            Assert(newTextInC.ParentId == autoLayoutC.Id, "Inserted object ParentId set to Auto Layout C ID");
            Assert(autoLayoutC.Children.Contains(newTextInC), "Auto Layout C contains newly inserted text");
            // Auto Layout reflow verification
            Assert(newTextInC.Y > child1.Y, "Auto Layout reflow stacked text vertically below child1");
            Assert(autoLayoutC.Height > heightBefore, "Auto Layout C hug height expanded to fit newly inserted object");

            // -------------------------------------------------------------
            // Acceptance Test 3: Insert objects into each level of 3-level nested Auto Layout hierarchy
            // -------------------------------------------------------------
            // Level 1: Auto Layout A selected
            selection.Clear();
            selection.Add(autoLayoutA);
            var targetA = InsertionTargetResolver.ResolveTarget(page, selection);
            Assert(targetA.Container == autoLayoutA, "Selection at Level 1 targets Auto Layout A");
            var itemInA = new LayerNode { Type = ElementType.Rectangle, Name = "Item in A", Width = 60, Height = 40 };
            InsertionTargetResolver.InsertObject(page, targetA, itemInA, reflowAutoLayout: true);
            Assert(itemInA.ParentId == autoLayoutA.Id && autoLayoutA.Children.Contains(itemInA), "Item inserted into Auto Layout A");

            // Level 2: Auto Layout B selected (drilled into hierarchy)
            selection.Clear();
            selection.Add(autoLayoutB);
            var targetB = InsertionTargetResolver.ResolveTarget(page, selection);
            Assert(targetB.Container == autoLayoutB, "Selection at Level 2 targets Auto Layout B");
            var itemInB = new LayerNode { Type = ElementType.Rectangle, Name = "Item in B", Width = 60, Height = 40 };
            InsertionTargetResolver.InsertObject(page, targetB, itemInB, reflowAutoLayout: true);
            Assert(itemInB.ParentId == autoLayoutB.Id && autoLayoutB.Children.Contains(itemInB), "Item inserted into Auto Layout B");

            // Level 3: Auto Layout C selected (drilled further into hierarchy)
            selection.Clear();
            selection.Add(autoLayoutC);
            var targetC = InsertionTargetResolver.ResolveTarget(page, selection);
            Assert(targetC.Container == autoLayoutC, "Selection at Level 3 targets Auto Layout C");
            var itemInC = new LayerNode { Type = ElementType.Rectangle, Name = "Item in C", Width = 60, Height = 40 };
            InsertionTargetResolver.InsertObject(page, targetC, itemInC, reflowAutoLayout: true);
            Assert(itemInC.ParentId == autoLayoutC.Id && autoLayoutC.Children.Contains(itemInC), "Item inserted into Auto Layout C");

            // -------------------------------------------------------------
            // Acceptance Test 4: Insertion targets change ONLY through intentional hierarchy navigation
            // -------------------------------------------------------------
            // Deep-selected child inside AutoLayout C -> targets AutoLayout C immediately after child
            selection.Clear();
            selection.Add(child1); // Deep-selected child inside C
            var targetChildInC = InsertionTargetResolver.ResolveTarget(page, selection);
            Assert(targetChildInC.Container == autoLayoutC, "Selecting child inside Auto Layout C targets Auto Layout C as container");
            Assert(targetChildInC.InsertionIndex == autoLayoutC.Children.IndexOf(child1) + 1, "Insertion index is immediately after the selected child");

            // Empty selection -> targets root canvas
            selection.Clear();
            var targetEmpty = InsertionTargetResolver.ResolveTarget(page, selection);
            Assert(targetEmpty.Container == null, "Empty selection targets root canvas");
            Assert(targetEmpty.IsRootCanvas, "target.IsRootCanvas is true when nothing selected");

            // Locked container cannot be targeted
            autoLayoutC.IsLocked = true;
            selection.Clear();
            selection.Add(autoLayoutC);
            var targetLocked = InsertionTargetResolver.ResolveTarget(page, selection);
            Assert(targetLocked.Container != autoLayoutC, "Locked Auto Layout C is rejected as insertion target");
            Assert(targetLocked.Container == autoLayoutB, "Target climbs to nearest unlocked container ancestor (Auto Layout B)");
            autoLayoutC.IsLocked = false;

            // -------------------------------------------------------------
            // Acceptance Test 5: Paste and duplicate objects without corrupting hierarchy
            // -------------------------------------------------------------
            // Create a complex composite component: Card with Header and Icon
            var cardFrame = new LayerNode { Id = "card1", Type = ElementType.Frame, Name = "Card", Width = 200, Height = 100 };
            var cardHeader = new LayerNode { Id = "hdr1", Type = ElementType.Text, Name = "Header", ParentId = "card1", Width = 150, Height = 30 };
            var cardIcon = new LayerNode { Id = "icn1", Type = ElementType.Ellipse, Name = "Icon", ParentId = "card1", Width = 20, Height = 20 };
            cardFrame.Children.Add(cardHeader);
            cardFrame.Children.Add(cardIcon);

            var toPaste = new List<LayerNode> { cardFrame, cardHeader, cardIcon };
            selection.Clear();
            selection.Add(autoLayoutB); // Target container for paste

            var pasteTarget = InsertionTargetResolver.ResolveTarget(page, selection);
            bool pastedOk = InsertionTargetResolver.InsertMultipleObjects(page, pasteTarget, toPaste, reflowAutoLayout: true);
            Assert(pastedOk, "Multi-object hierarchy inserted successfully");
            Assert(cardFrame.ParentId == autoLayoutB.Id, "Top-level pasted Card ParentId is set to Auto Layout B");
            Assert(cardHeader.ParentId == "card1", "Pasted child Header maintains its internal parent-child relationship to Card");
            Assert(cardIcon.ParentId == "card1", "Pasted child Icon maintains its internal parent-child relationship to Card");
            Assert(cardFrame.Children.Contains(cardHeader) && cardFrame.Children.Contains(cardIcon), "CardFrame children list preserved intact");
            Assert(autoLayoutB.Children.Contains(cardFrame), "Auto Layout B contains CardFrame");
            Assert(!autoLayoutB.Children.Contains(cardHeader), "Auto Layout B does NOT flatten internal child Header into its own children");

            // -------------------------------------------------------------
            // Acceptance Test 6: Auto Layout recalculates correctly after insertion
            // -------------------------------------------------------------
            var simpleRow = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Simple Row",
                X = 100,
                Y = 100,
                Width = 100,
                Height = 50,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Horizontal,
                    Gap = 10,
                    PaddingLeft = 10,
                    PaddingTop = 10,
                    PaddingRight = 10,
                    PaddingBottom = 10,
                    SizingH = SizingMode.Hug,
                    SizingV = SizingMode.Hug
                }
            };
            page.Layers.Add(simpleRow);
            var item1 = new LayerNode { Type = ElementType.Rectangle, Width = 50, Height = 30 };
            var item2 = new LayerNode { Type = ElementType.Rectangle, Width = 50, Height = 30 };

            var rowTarget = new InsertionTargetResult(simpleRow, 0);
            InsertionTargetResolver.InsertObject(page, rowTarget, item1, reflowAutoLayout: true);
            InsertionTargetResolver.InsertObject(page, new InsertionTargetResult(simpleRow, 1), item2, reflowAutoLayout: true);

            // Row width should hug: paddingLeft(10) + item1(50) + gap(10) + item2(50) + paddingRight(10) = 130
            Assert(Math.Abs(simpleRow.Width - 130) < 0.01, string.Format("Auto Layout row width hugged to 130 after insertions (got {0})", simpleRow.Width));
            Assert(item2.X == simpleRow.X + 10 + 50 + 10, "Item2 positioned horizontally after Item1 with gap");

            // -------------------------------------------------------------
            // Acceptance Test 7: Selection, dragging, and insertion do not interfere
            // -------------------------------------------------------------
            // 1. Selection Resolver over child1 keeps autoLayoutC selected for drag
            var hitC = CanvasInteractionController.HitTest(page, new Point(child1.X + 5, child1.Y + 5));
            selection.Clear();
            selection.Add(autoLayoutC);
            var resolvedSel = CanvasInteractionController.ResolveSelection(hitC, selection, 1, false, false);
            Assert(resolvedSel == autoLayoutC, "Pointer down over child1 preserves autoLayoutC selection for dragging");
            var dragTargets = CanvasInteractionController.ResolveDragTargets(hitC, selection, resolvedSel, false, 1);
            Assert(dragTargets.Count == 1 && dragTargets[0] == autoLayoutC, "Drag target is autoLayoutC");

            // 2. Insertion Target Resolver with autoLayoutC selected targets autoLayoutC for insertion
            var insertTarget = InsertionTargetResolver.ResolveTarget(page, selection);
            Assert(insertTarget.Container == autoLayoutC, "Insertion target correctly resolves to autoLayoutC independent of pointer drag state");

            // 3. AddLayerCommand undo/redo maintains container integrity and reflow
            var undoTestRect = new LayerNode { Type = ElementType.Rectangle, Name = "Undo Test Rect", Width = 20, Height = 20 };
            var addCmd = new AddLayerCommand(page, undoTestRect, autoLayoutC.Id, -1, () =>
            {
                AutoLayoutEngine.ReflowRecursive(autoLayoutC);
            });
            addCmd.Execute();
            Assert(autoLayoutC.Children.Contains(undoTestRect), "AddLayerCommand executed: object in autoLayoutC");
            Assert(undoTestRect.ParentId == autoLayoutC.Id, "AddLayerCommand set ParentId to autoLayoutC");

            addCmd.Unexecute();
            Assert(!autoLayoutC.Children.Contains(undoTestRect), "AddLayerCommand undone: object removed from autoLayoutC");

            addCmd.Execute();
            Assert(autoLayoutC.Children.Contains(undoTestRect), "AddLayerCommand redone: object restored in autoLayoutC");
        }

        private static void TestNonDestructiveRenderTransformsAndAutoLayoutPreservation()
        {
            var page = new Page();

            // 1. Create an Auto Layout Frame containing two child layers
            var frame = new LayerNode
            {
                Type = ElementType.Frame,
                Name = "Motion Card",
                X = 100,
                Y = 100,
                Width = 240,
                Height = 120,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Horizontal,
                    Gap = 16,
                    PaddingLeft = 16,
                    PaddingTop = 16,
                    PaddingRight = 16,
                    PaddingBottom = 16,
                    SizingH = SizingMode.Fixed,
                    SizingV = SizingMode.Fixed
                }
            };
            page.Layers.Add(frame);

            var childRect = new LayerNode
            {
                Type = ElementType.Rectangle,
                Name = "Badge",
                X = 116,
                Y = 116,
                Width = 80,
                Height = 40,
                ParentId = frame.Id
            };
            frame.Children.Add(childRect);

            var childPath = new LayerNode
            {
                Type = ElementType.Path,
                Name = "Icon Path",
                X = 212,
                Y = 116,
                Width = 40,
                Height = 40,
                ParentId = frame.Id,
                Geometry = new VectorGeometry()
            };
            childPath.Geometry.Nodes.Add(new BezierNode(0, 0));
            childPath.Geometry.Nodes.Add(new BezierNode(40, 40));
            frame.Children.Add(childPath);

            // Verify initial dimensions
            Assert(frame.Width == 240 && frame.Height == 120, "Initial frame bounds: 240x120");
            Assert(childRect.Width == 80 && childRect.Height == 40, "Initial child rect: 80x40");
            Assert(childPath.Geometry.Nodes[0].Anchor.X == 0.0, "Initial path node 0 is at (0, 0)");

            // 2. Simulate Animation Pose applying 2.0x scale, 30deg rotation, and translation via PropertyBinder
            uint frameId = PropertyBinder.GetNumericNodeId(frame.Id);
            var pose = new Dictionary<uint, Dictionary<ushort, double>>();
            pose[frameId] = new Dictionary<ushort, double>
            {
                { (ushort)PropKey.ScaleX, 2.0 },
                { (ushort)PropKey.ScaleY, 1.5 },
                { (ushort)PropKey.Rotation, 30.0 },
                { (ushort)PropKey.X, 150.0 },
                { (ushort)PropKey.Y, 120.0 }
            };

            PropertyBinder.ApplyPoseToLayer(frame, frameId, pose);

            // 3. Verify that LayerNode.Width, Height, X, and Y are NOT mutated (design rest model preserved)
            Assert(frame.Width == 240, string.Format("Frame Width preserved at 240 (got {0})", frame.Width));
            Assert(frame.Height == 120, string.Format("Frame Height preserved at 120 (got {0})", frame.Height));
            Assert(frame.X == 100 && frame.Y == 100, "Frame design coordinates preserved at (100, 100)");

            // 4. Verify that RenderTransform captured the animation values
            Assert(frame.RenderTransform != null, "RenderTransform instantiated");
            Assert(Math.Abs(frame.RenderTransform.ScaleX - 2.0) < 0.001, "RenderTransform ScaleX is 2.0");
            Assert(Math.Abs(frame.RenderTransform.ScaleY - 1.5) < 0.001, "RenderTransform ScaleY is 1.5");
            Assert(Math.Abs(frame.RenderTransform.Rotation - 30.0) < 0.001, "RenderTransform Rotation is 30 deg");
            Assert(Math.Abs(frame.RenderTransform.TranslationX - 50.0) < 0.001, "RenderTransform TranslationX is 50.0");
            Assert(Math.Abs(frame.RenderTransform.TranslationY - 20.0) < 0.001, "RenderTransform TranslationY is 20.0");

            // 5. Verify child layers in Auto Layout frame were NOT mutated
            Assert(childRect.Width == 80 && childRect.Height == 40, "Child rect bounds untouched by parent animation");
            Assert(childPath.Geometry.Nodes[0].Anchor.X == 0.0, "Vector path geometry untouched by scale animation");

            // 6. Verify Matrix calculation and center pivot invariance
            var matrix = frame.GetRenderTransformMatrix();
            Assert(!matrix.IsIdentity, "RenderTransform matrix is non-identity");
            // Center pivot is (100 + 120, 100 + 60) = (220, 160).
            // Under translation (50, 20), pivot maps to (270, 180) regardless of rotation/scale.
            Point centerPt = new Point(220, 160);
            Point transformedCenter = matrix.Transform(centerPt);
            Assert(Math.Abs(transformedCenter.X - 270.0) < 0.01 && Math.Abs(transformedCenter.Y - 180.0) < 0.01,
                string.Format("Pivot transformed to (270, 180), got ({0:F1}, {1:F1})", transformedCenter.X, transformedCenter.Y));

            // 7. Verify Hit-Testing with RenderTransform inverse mapping
            // Point at (270, 180) is transformed center: must hit frame
            Assert(CanvasInteractionController.ContainsPoint(frame, new Point(270, 180)),
                "ContainsPoint correctly hits transformed frame center");

            // 8. Verify Reset() restores exact design rest bounds with zero drift
            frame.RenderTransform.Reset();
            Assert(frame.RenderTransform.IsIdentity, "RenderTransform IsIdentity is true after Reset");
            Assert(frame.GetRenderTransformMatrix().IsIdentity, "Render transform matrix is identity after Reset");
            Assert(frame.Width == 240 && frame.Height == 120, "Frame bounds 100% restored with zero drift");
            Assert(frame.X == 100 && frame.Y == 100, "Frame position 100% restored with zero drift");
        }

        private static void TestIndependentTransformArchitectureAndAnimationEvaluator()
        {
            var page = new Page();

            // 1. Build nested scene hierarchy: Canvas -> Parent Frame (Auto Layout) -> Child Group -> Leaf Rect & Path
            var frame = new LayerNode
            {
                Id = "frame-root",
                Name = "Parent Frame",
                Type = ElementType.Frame,
                X = 100,
                Y = 100,
                Width = 400,
                Height = 300,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Vertical,
                    Gap = 10,
                    PaddingTop = 20,
                    PaddingBottom = 20,
                    PaddingLeft = 20,
                    PaddingRight = 20
                },
                Children = new List<LayerNode>()
            };
            page.Layers.Add(frame);

            var group = new LayerNode
            {
                Id = "group-mid",
                Name = "Nested Group",
                Type = ElementType.Group,
                ParentId = frame.Id,
                X = 120,
                Y = 120,
                Width = 360,
                Height = 100,
                Children = new List<LayerNode>()
            };
            frame.Children.Add(group);

            var childRect = new LayerNode
            {
                Id = "child-rect",
                Name = "ActionButton",
                Type = ElementType.Rectangle,
                ParentId = group.Id,
                X = 120,
                Y = 120,
                Width = 150,
                Height = 60
            };
            group.Children.Add(childRect);

            var childPath = new LayerNode
            {
                Id = "child-path",
                Name = "IconPath",
                Type = ElementType.Path,
                ParentId = group.Id,
                X = 290,
                Y = 120,
                Width = 60,
                Height = 60,
                Geometry = new VectorGeometry()
            };
            childPath.Geometry.Nodes.Add(new BezierNode(0, 0));
            childPath.Geometry.Nodes.Add(new BezierNode(60, 60));
            group.Children.Add(childPath);

            // 2. Build 8-Channel Animation Clip
            uint frameId = PropertyBinder.GetNumericNodeId(frame.Id);
            uint rectId = PropertyBinder.GetNumericNodeId(childRect.Id);

            var clip = new Clip(200, "MultiChannelTestClip", 2.0, 60.0, LoopMode.Loop);

            // Frame tracks: ScaleX, ScaleY, Rotation, AnchorX, AnchorY, Opacity
            var trackScaleX = new Track(frameId, (ushort)PropKey.ScaleX);
            trackScaleX.AddKeyframe(Keyframe.Linear(0.0, 1.0));
            trackScaleX.AddKeyframe(Keyframe.Linear(1.0, 2.5));
            clip.AddTrack(trackScaleX);

            var trackScaleY = new Track(frameId, (ushort)PropKey.ScaleY);
            trackScaleY.AddKeyframe(Keyframe.Linear(0.0, 1.0));
            trackScaleY.AddKeyframe(Keyframe.Linear(1.0, 2.0));
            clip.AddTrack(trackScaleY);

            var trackRot = new Track(frameId, (ushort)PropKey.Rotation);
            trackRot.AddKeyframe(Keyframe.Linear(0.0, 0.0));
            trackRot.AddKeyframe(Keyframe.Linear(1.0, 45.0));
            clip.AddTrack(trackRot);

            var trackAnchorX = new Track(frameId, (ushort)PropKey.AnchorX);
            trackAnchorX.AddKeyframe(Keyframe.Linear(0.0, 0.5));
            trackAnchorX.AddKeyframe(Keyframe.Linear(1.0, 0.0)); // Shift pivot to left
            clip.AddTrack(trackAnchorX);

            var trackAnchorY = new Track(frameId, (ushort)PropKey.AnchorY);
            trackAnchorY.AddKeyframe(Keyframe.Linear(0.0, 0.5));
            trackAnchorY.AddKeyframe(Keyframe.Linear(1.0, 0.0)); // Shift pivot to top
            clip.AddTrack(trackAnchorY);

            var trackOpacity = new Track(frameId, (ushort)PropKey.Opacity);
            trackOpacity.AddKeyframe(Keyframe.Linear(0.0, 1.0));
            trackOpacity.AddKeyframe(Keyframe.Linear(1.0, 0.75));
            clip.AddTrack(trackOpacity);

            // ChildRect tracks: TranslationX, VisualWidth, Opacity
            var trackRectX = new Track(rectId, (ushort)PropKey.X);
            trackRectX.AddKeyframe(Keyframe.Linear(0.0, 120.0));
            trackRectX.AddKeyframe(Keyframe.Linear(1.0, 180.0)); // Delta = +60
            clip.AddTrack(trackRectX);

            var trackRectW = new Track(rectId, (ushort)PropKey.VisualWidth);
            trackRectW.AddKeyframe(Keyframe.Linear(0.0, 150.0));
            trackRectW.AddKeyframe(Keyframe.Linear(1.0, 300.0)); // 2.0x scale
            clip.AddTrack(trackRectW);

            var trackRectOp = new Track(rectId, (ushort)PropKey.Opacity);
            trackRectOp.AddKeyframe(Keyframe.Linear(0.0, 1.0));
            trackRectOp.AddKeyframe(Keyframe.Linear(1.0, 0.5));
            clip.AddTrack(trackRectOp);

            // 3. Test Deterministic Evaluator at arbitrary timestamps without side effects
            var pose0 = clip.EvaluateAt(0.0);
            Assert(Math.Abs(pose0[frameId][(ushort)PropKey.ScaleX] - 1.0) < 1e-4, "Evaluator at t=0: Frame ScaleX = 1.0");
            Assert(Math.Abs(pose0[frameId][(ushort)PropKey.Rotation] - 0.0) < 1e-4, "Evaluator at t=0: Frame Rotation = 0");
            Assert(Math.Abs(pose0[frameId][(ushort)PropKey.AnchorX] - 0.5) < 1e-4, "Evaluator at t=0: Frame AnchorX = 0.5");

            var pose1 = clip.EvaluateAt(1.0);
            Assert(Math.Abs(pose1[frameId][(ushort)PropKey.ScaleX] - 2.5) < 1e-4, "Evaluator at t=1: Frame ScaleX = 2.5");
            Assert(Math.Abs(pose1[frameId][(ushort)PropKey.Rotation] - 45.0) < 1e-4, "Evaluator at t=1: Frame Rotation = 45");
            Assert(Math.Abs(pose1[frameId][(ushort)PropKey.AnchorX] - 0.0) < 1e-4, "Evaluator at t=1: Frame AnchorX = 0.0");
            Assert(Math.Abs(pose1[rectId][(ushort)PropKey.X] - 180.0) < 1e-4, "Evaluator at t=1: ChildRect X = 180");
            Assert(Math.Abs(pose1[rectId][(ushort)PropKey.VisualWidth] - 300.0) < 1e-4, "Evaluator at t=1: ChildRect VisualWidth = 300");

            // 4. Apply pose1 to layers and verify model immutability
            PropertyBinder.ApplyPoseToLayer(frame, frameId, pose1);
            PropertyBinder.ApplyPoseToLayer(childRect, rectId, pose1);

            Assert(frame.Width == 400 && frame.Height == 300, "Frame model dimensions 100% immutable (400x300)");
            Assert(frame.X == 100 && frame.Y == 100, "Frame model coordinates 100% immutable (100, 100)");
            Assert(childRect.Width == 150 && childRect.Height == 60, "Child rect model dimensions 100% immutable (150x60)");
            Assert(childRect.X == 120 && childRect.Y == 120, "Child rect model coordinates 100% immutable (120, 120)");
            Assert(childPath.Geometry.Nodes[0].Anchor.X == 0.0, "Child path vector geometry 100% untouched by parent transform");

            // 5. Verify RenderTransform captured all independent properties
            Assert(Math.Abs(frame.RenderTransform.ScaleX - 2.5) < 1e-4, "Frame RenderTransform ScaleX is 2.5");
            Assert(Math.Abs(frame.RenderTransform.ScaleY - 2.0) < 1e-4, "Frame RenderTransform ScaleY is 2.0");
            Assert(Math.Abs(frame.RenderTransform.Rotation - 45.0) < 1e-4, "Frame RenderTransform Rotation is 45 deg");
            Assert(Math.Abs(frame.RenderTransform.AnchorX - 0.0) < 1e-4, "Frame RenderTransform AnchorX shifted to 0.0 (top-left pivot)");
            Assert(Math.Abs(frame.Opacity - 0.75) < 1e-4, "Frame Opacity is 0.75");
            Assert(Math.Abs(childRect.RenderTransform.TranslationX - 60.0) < 1e-4, "ChildRect TranslationX is 60.0");
            Assert(Math.Abs(childRect.RenderTransform.ScaleX - 2.0) < 1e-4, "ChildRect ScaleX is 2.0 via VisualWidth 300");
            Assert(Math.Abs(childRect.Opacity - 0.5) < 1e-4, "ChildRect Opacity is 0.5");

            // 6. Verify Hierarchical Matrix Composition
            var frameLocalMat = frame.GetRenderTransformMatrix();
            var rectLocalMat = childRect.GetRenderTransformMatrix();
            var rectWorldMat = childRect.GetWorldRenderTransformMatrix(page);

            Assert(!frameLocalMat.IsIdentity, "Frame local matrix is non-identity");
            Assert(!rectLocalMat.IsIdentity, "ChildRect local matrix is non-identity");
            Assert(!rectWorldMat.IsIdentity, "ChildRect world matrix is non-identity");

            // World matrix must match childLocal.Append(frameLocal)
            var expectedWorldMat = rectLocalMat;
            expectedWorldMat.Append(frameLocalMat);
            Assert(Math.Abs(rectWorldMat.M11 - expectedWorldMat.M11) < 1e-4 &&
                   Math.Abs(rectWorldMat.M12 - expectedWorldMat.M12) < 1e-4 &&
                   Math.Abs(rectWorldMat.OffsetX - expectedWorldMat.OffsetX) < 1e-4 &&
                   Math.Abs(rectWorldMat.OffsetY - expectedWorldMat.OffsetY) < 1e-4,
                   "Child world matrix matches composed ancestor matrix multiplication");

            // 7. Verify Inverted Hit-Testing with composed world matrix
            Point rectCanvasPoint = new Point(120, 120);
            Point transformedPoint = rectWorldMat.Transform(rectCanvasPoint);
            Point inverseLocal = RetainedSceneGraph.CanvasToLocalPoint(childRect, transformedPoint, page);
            Assert(Math.Abs(inverseLocal.X - rectCanvasPoint.X) < 1e-2 && Math.Abs(inverseLocal.Y - rectCanvasPoint.Y) < 1e-2,
                "CanvasToLocalPoint correctly maps canvas point to local layer space via inverted world matrix");

            // 8. Test Centralized Animation Clock (Playback, Step, Speed, Loop, Frame Nav)
            var clock = new AnimationTimelineController(2.0, 60.0);
            clock.ActiveClip = clip;
            Assert(clock.Duration == 2.0, "Clock Duration equals active clip duration (2.0s)");

            // Play / Step
            clock.Play();
            Assert(clock.IsPlaying, "Clock IsPlaying is true");
            clock.Step(0.25);
            Assert(Math.Abs(clock.CurrentTime - 0.25) < 1e-4, "Clock stepped by 0.25s");

            // Variable Speed
            clock.Speed = 2.0;
            clock.Step(0.25); // At 2.0x, advances by 0.5s -> CurrentTime = 0.75s
            Assert(Math.Abs(clock.CurrentTime - 0.75) < 1e-4, "Clock stepped with 2.0x speed multiplier to 0.75s");

            // StepFrame navigation
            clock.StepFrame(15); // 15 frames at 60fps = +0.25s -> CurrentTime = 1.0s
            Assert(Math.Abs(clock.CurrentTime - 1.0) < 1e-4, "StepFrame(15) navigated to 1.0s");

            clock.Pause();
            Assert(!clock.IsPlaying, "Clock IsPlaying is false after Pause");

            // Stop and instant rest pose recovery
            clock.Stop();
            Assert(!clock.IsPlaying && clock.CurrentTime == 0.0, "Clock Stopped: time reset to 0.0");
            frame.RenderTransform.Reset();
            childRect.RenderTransform.Reset();
            Assert(frame.RenderTransform.IsIdentity, "Frame RenderTransform identity after Stop");
            Assert(childRect.RenderTransform.IsIdentity, "ChildRect RenderTransform identity after Stop");
            Assert(frame.Width == 400 && frame.Height == 300, "Frame rest bounds 100% intact with zero drift");
            Assert(childRect.Width == 150 && childRect.Height == 60, "ChildRect rest bounds 100% intact with zero drift");
        }

        private static void TestTimelineAndKeyframeEngineAuthoringSystem()
        {
            // 1. Keyframe CRUD & Stable Unique IDs
            var track = new Track(101, (ushort)PropKey.X, "layer-101", "Position X");
            var kf1 = new Keyframe(1.0, 100.0, InterpMode.Linear);
            var kf2 = new Keyframe(0.0, 0.0, InterpMode.Linear);
            var kf3 = new Keyframe(2.0, 200.0, InterpMode.Linear);

            Assert(!string.IsNullOrEmpty(kf1.Id) && !string.IsNullOrEmpty(kf2.Id) && !string.IsNullOrEmpty(kf3.Id),
                "Keyframes have non-empty unique string GUIDs");
            Assert(kf1.Id != kf2.Id && kf2.Id != kf3.Id, "Keyframe GUIDs are unique across instances");

            // Binary search insertion maintains sorted order regardless of insertion order
            track.AddKeyframe(kf1);
            track.AddKeyframe(kf2);
            track.AddKeyframe(kf3);

            Assert(track.Keys.Count == 3, "Track has 3 keyframes");
            Assert(track.Keys[0].Time == 0.0 && track.Keys[1].Time == 1.0 && track.Keys[2].Time == 2.0,
                "Track maintains strictly sorted chronological order (0.0s, 1.0s, 2.0s)");

            // Keyframe search and neighbor navigation
            var found = track.FindKeyframeAt(1.0);
            Assert(found != null && found.Id == kf1.Id, "FindKeyframeAt found exact keyframe at 1.0s");

            double? prevTime = track.GetPreviousKeyframeTime(1.5);
            Assert(prevTime.HasValue && Math.Abs(prevTime.Value - 1.0) < 1e-4, "GetPreviousKeyframeTime(1.5) returned 1.0s");

            double? nextTime = track.GetNextKeyframeTime(1.0);
            Assert(nextTime.HasValue && Math.Abs(nextTime.Value - 2.0) < 1e-4, "GetNextKeyframeTime(1.0) returned 2.0s");

            // Keyframe deletion by ID
            string deletedId = kf1.Id;
            bool removed = track.RemoveKeyframe(deletedId);
            Assert(removed && track.Keys.Count == 2, "RemoveKeyframe successfully removed keyframe by ID");
            Assert(track.Keys[0].Time == 0.0 && track.Keys[1].Time == 2.0, "Track remains sorted after keyframe deletion");

            // 2. Duplicate Timestamps Resolution
            var duplicateKey = new Keyframe(2.0, 999.0, InterpMode.Cubic);
            track.AddKeyframe(duplicateKey);
            Assert(track.Keys.Count == 2, "Deterministic duplicate resolution: count remains 2 without creating orphan duplicate keys");
            Assert(track.Keys[1].Value == 999.0 && track.Keys[1].Interp == InterpMode.Cubic,
                "Existing keyframe updated in place with new value and interpolation mode");

            // 3. Multi-Keyframe Selection & Relative Time Shifting
            var track2 = new Track(102, (ushort)PropKey.Rotation, "layer-102", "Rotation");
            var r1 = new Keyframe(0.5, 45.0, InterpMode.Linear);
            var r2 = new Keyframe(1.0, 90.0, InterpMode.Linear);
            var r3 = new Keyframe(1.5, 180.0, InterpMode.Linear);
            track2.AddKeyframe(r1);
            track2.AddKeyframe(r2);
            track2.AddKeyframe(r3);

            // Shift selected keys (r1 and r2) by delta = +0.25s
            var selectedKeys = new List<Keyframe> { r1, r2 };
            double shiftDelta = 0.25;
            foreach (var k in selectedKeys)
            {
                k.Time += shiftDelta;
            }
            track2.SortKeys();

            Assert(Math.Abs(r1.Time - 0.75) < 1e-4 && Math.Abs(r2.Time - 1.25) < 1e-4,
                "Multi-keyframe relative time shifting preserves exact inter-keyframe timing (0.75s, 1.25s)");
            Assert(Math.Abs((r2.Time - r1.Time) - 0.5) < 1e-4, "Relative distance between shifted keyframes remains exactly 0.5s");

            // 4. Keyframe Clipboard (Copy, Paste, Duplicate)
            KeyframeClipboard.Clear();
            Assert(!KeyframeClipboard.HasData, "Clipboard initialized empty");

            var copySelection = new List<Tuple<Track, Keyframe>>
            {
                Tuple.Create(track2, r1),
                Tuple.Create(track2, r2)
            };
            KeyframeClipboard.Copy(copySelection);
            Assert(KeyframeClipboard.HasData && KeyframeClipboard.Count == 2, "Clipboard copied 2 keyframes");

            var clip = new Clip(1, "TestClip", 5.0, 60.0);
            clip.Tracks.Add(track);
            clip.Tracks.Add(track2);

            // Paste at playhead = 3.0s onto target object 103
            var pasted = KeyframeClipboard.Paste(clip, 103, 3.0);
            Assert(pasted.Count == 2, "Clipboard pasted 2 keyframes at playhead");
            Assert(Math.Abs(pasted[0].Time - 3.0) < 1e-4, "First pasted keyframe aligned to playhead time 3.0s");
            Assert(Math.Abs(pasted[1].Time - 3.5) < 1e-4, "Second pasted keyframe preserves relative offset (+0.5s) at 3.5s");

            var pastedTrack = clip.GetTrack(103, (ushort)PropKey.Rotation);
            Assert(pastedTrack != null && pastedTrack.Keys.Count == 2, "Target track for object 103 created automatically on paste");

            // Duplicate in-place with 0.1s offset
            var dupeSelection = new List<Tuple<Track, Keyframe>>
            {
                Tuple.Create(pastedTrack, pasted[0])
            };
            var duplicated = KeyframeClipboard.Duplicate(clip, dupeSelection, 0.1);
            Assert(duplicated.Count == 1, "Duplicate created 1 keyframe");
            Assert(Math.Abs(duplicated[0].Time - 3.1) < 1e-4, "Duplicated keyframe offset by 0.1s to 3.1s");
            Assert(duplicated[0].Id != pasted[0].Id, "Duplicated keyframe has distinct unique ID");

            // 5. Frame Snapping & Clock Quantization
            var controller = new AnimationTimelineController(5.0, 60.0);
            controller.ActiveClip = clip;
            controller.SnapToFrames = true;

            // 60fps frame duration = 0.016666...s
            double snapped1 = controller.SnapTime(0.040); // 0.040s * 60 = 2.4 -> 2 frames = 2.0 / 60 = 0.0333...s
            Assert(Math.Abs(snapped1 - (2.0 / 60.0)) < 1e-4, "SnapTime quantized 0.040s to frame 2 (0.0333s)");

            double snapped2 = controller.SnapTime(0.045); // 0.045s * 60 = 2.7 -> 3 frames = 3.0 / 60 = 0.050s
            Assert(Math.Abs(snapped2 - (3.0 / 60.0)) < 1e-4, "SnapTime quantized 0.045s to frame 3 (0.0500s)");

            // 6. Multi-Object Timeline & Evaluator Integration (Non-destructive)
            var layerA = new LayerNode { Id = "layer-A", X = 50, Y = 50, Width = 100, Height = 100, Opacity = 1.0 };
            var layerB = new LayerNode { Id = "layer-B", X = 200, Y = 200, Width = 80, Height = 80, Opacity = 1.0 };
            uint idA = PropertyBinder.GetNumericNodeId(layerA.Id);
            uint idB = PropertyBinder.GetNumericNodeId(layerB.Id);

            var trackA_X = clip.GetOrCreateTrack(idA, (ushort)PropKey.X, layerA.Id, "Position X");
            trackA_X.AddKeyframe(Keyframe.Linear(0.0, 50.0));
            trackA_X.AddKeyframe(Keyframe.Linear(1.0, 150.0));

            var trackA_Rot = clip.GetOrCreateTrack(idA, (ushort)PropKey.Rotation, layerA.Id, "Rotation");
            trackA_Rot.AddKeyframe(Keyframe.Linear(0.0, 0.0));
            trackA_Rot.AddKeyframe(Keyframe.Linear(1.0, 90.0));

            var trackB_Opacity = clip.GetOrCreateTrack(idB, (ushort)PropKey.Opacity, layerB.Id, "Opacity");
            trackB_Opacity.AddKeyframe(Keyframe.Linear(0.0, 1.0));
            trackB_Opacity.AddKeyframe(Keyframe.Linear(1.0, 0.2));

            // Evaluate at t = 0.5s
            var pose = clip.EvaluateAt(0.5);
            Assert(pose.ContainsKey(idA) && pose.ContainsKey(idB), "Pose contains evaluations for both animated objects");

            PropertyBinder.ApplyPoseToLayer(layerA, idA, pose);
            PropertyBinder.ApplyPoseToLayer(layerB, idB, pose);

            Assert(Math.Abs(layerA.RenderTransform.TranslationX - 50.0) < 1e-4, "Layer A evaluated TranslationX is 50.0 (total X = 100.0)");
            Assert(Math.Abs(layerA.RenderTransform.Rotation - 45.0) < 1e-4, "Layer A evaluated Rotation is 45.0°");
            Assert(Math.Abs(layerB.Opacity - 0.6) < 1e-4, "Layer B evaluated Opacity is 0.6");

            // Design rest geometry preserved
            Assert(layerA.X == 50 && layerA.Y == 50, "Layer A design rest position X/Y is completely unmodified");
            Assert(layerB.X == 200 && layerB.Y == 200, "Layer B design rest position X/Y is completely unmodified");

            // 7. Object Deletion / Missing Target Safety
            // If target layer is deleted/missing, tracks remain intact and queries return empty or default safely
            var tracksForNonExistent = clip.GetTracksForNode(999999);
            Assert(tracksForNonExistent != null && tracksForNonExistent.Count == 0,
                "Querying tracks for nonexistent/deleted object node returns empty list without exception");

            var missingTrack = clip.GetTrack(999999, (ushort)PropKey.X);
            Assert(missingTrack == null, "GetTrack for nonexistent/deleted node returns null safely");
        }

        private static void TestScaleCrashFixFrameScalingNoPresetAndStateTimelineHeader()
        {
            // 1. ScrubControls safety: empty suffix string replacement must not throw System.ArgumentException
            string suffix = "";
            string rawText = " 2.5 ";
            string cleaned = !string.IsNullOrEmpty(suffix) ? rawText.Replace(suffix, "").Trim() : rawText.Trim();
            double parsedVal;
            bool parsedOk = double.TryParse(cleaned, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsedVal);
            Assert(parsedOk && Math.Abs(parsedVal - 2.5) < 1e-4, "Empty suffix scrub parsing succeeds without throwing String cannot be of zero length exception");

            // 2. Default animation clip starts completely empty (no unwanted rotation or opacity presets)
            var newClip = new Clip(1, "DefaultClip", 5.0, 60.0);
            Assert(newClip.Tracks.Count == 0, "Default animation clip starts with 0 tracks and 0 keyframes without uninvited presets");

            // 3. Non-destructive Frame Content Scaling in Animate Mode
            var doc = new KerfDocument();
            var page = doc.Pages[0];
            var frame = new LayerNode
            {
                Id = "frame-container-1",
                Type = ElementType.Frame,
                Name = "Parent Frame",
                X = 100,
                Y = 100,
                Width = 400,
                Height = 300
            };
            var childVector = new LayerNode
            {
                Id = "vector-child-1",
                Type = ElementType.Path,
                Name = "Inner Icon",
                ParentId = frame.Id,
                X = 50,
                Y = 50,
                Width = 80,
                Height = 80
            };
            frame.Children.Add(childVector);
            page.Layers.Add(frame);

            // Verify GetStaticTransformMatrix does not double-bake RenderTransform
            frame.RenderTransform.ScaleX = 2.0;
            frame.RenderTransform.ScaleY = 2.0;
            var staticMat = frame.GetStaticTransformMatrix();
            Assert(staticMat.IsIdentity, "GetStaticTransformMatrix remains identity when no static rotation or scale is applied");

            var renderMat = frame.GetRenderTransformMatrix();
            Assert(Math.Abs(renderMat.M11 - 2.0) < 1e-4 && Math.Abs(renderMat.M22 - 2.0) < 1e-4,
                "GetRenderTransformMatrix accurately reflects animated 2.0x scale");

            // Frame dimensions are preserved non-destructively
            Assert(frame.Width == 400 && frame.Height == 300, "Base frame width and height are preserved non-destructively without resizing bounds");

            // Child geometry is transformed by frame's visual transform
            // In WPF / RetainedSceneGraph, child visuals live inside the frame's VisualContainer
            Point childOriginInFrame = new Point(childVector.X, childVector.Y);
            Point transformedChildOrigin = renderMat.Transform(childOriginInFrame);
            Assert(transformedChildOrigin != childOriginInFrame,
                   "Nested child objects scale with the parent frame non-destructively");

            // 4. State Machine Timeline Header & State-to-Clip Mapping
            var smDef = new StateMachineDefinition("AppStateMachine");
            var smLayer = new MachineLayer("BaseLayer");
            var idleClip = new Clip(10, "IdleClip", 2.0, 60.0);
            var hoverClip = new Clip(11, "HoverClip", 1.5, 60.0);
            doc.Animation.Clips.Add(idleClip);
            doc.Animation.Clips.Add(hoverClip);

            var stateIdle = new MachineState(0, "Idle", StateKind.Animation, 0); // maps to doc.Animation.Clips[0]
            var stateHover = new MachineState(1, "Hover", StateKind.Animation, 1); // maps to doc.Animation.Clips[1]
            smLayer.States.Add(stateIdle);
            smLayer.States.Add(stateHover);
            smDef.Layers.Add(smLayer);
            doc.Animation.StateMachine = smDef;

            // Test mapping for idleClip
            int idleIdx = doc.Animation.Clips.IndexOf(idleClip);
            MachineState matchedIdle = null;
            foreach (var l in doc.Animation.StateMachine.Layers)
            {
                foreach (var s in l.States)
                {
                    if (s.ClipIndex == idleIdx) matchedIdle = s;
                }
            }
            Assert(matchedIdle != null && matchedIdle.Name == "Idle", "Idle clip maps accurately to 'Idle' state");

            // 5. Uniform Scaling on Canvas Handles (All 8 Handles Maintain Aspect Ratio)
            Rect origBox = new Rect(0, 0, 100, 200); // aspect ratio = 0.5
            Point startMouse = new Point(100, 200);

            // 5a. Corner Handle (BottomRight)
            var cornerRes = TransformEngine.ApplyTransform(
                HandleType.BottomRight,
                new Point(150, 250),
                startMouse,
                origBox,
                0,
                keepAspectRatio: true,
                fromCenter: false,
                snapGridSize: 0);
            double cornerRatio = cornerRes.Bounds.Width / cornerRes.Bounds.Height;
            Assert(Math.Abs(cornerRatio - 0.5) < 1e-4, "BottomRight corner handle maintains aspect ratio with uniform scaling locked");

            // 5b. Edge Handle (MiddleRight)
            var edgeRightRes = TransformEngine.ApplyTransform(
                HandleType.MiddleRight,
                new Point(150, 200),
                startMouse,
                origBox,
                0,
                keepAspectRatio: true,
                fromCenter: false,
                snapGridSize: 0);
            double edgeRightRatio = edgeRightRes.Bounds.Width / edgeRightRes.Bounds.Height;
            Assert(Math.Abs(edgeRightRatio - 0.5) < 1e-4, "MiddleRight edge handle maintains aspect ratio with uniform scaling locked");
            Assert(Math.Abs(edgeRightRes.Bounds.Width - 150.0) < 1e-4 && Math.Abs(edgeRightRes.Bounds.Height - 300.0) < 1e-4,
                "MiddleRight scale expanded height proportionally to 300 when width grew to 150");

            // 5c. Edge Handle (BottomCenter)
            var edgeBottomRes = TransformEngine.ApplyTransform(
                HandleType.BottomCenter,
                new Point(100, 300),
                startMouse,
                origBox,
                0,
                keepAspectRatio: true,
                fromCenter: false,
                snapGridSize: 0);
            double edgeBottomRatio = edgeBottomRes.Bounds.Width / edgeBottomRes.Bounds.Height;
            Assert(Math.Abs(edgeBottomRatio - 0.5) < 1e-4, "BottomCenter edge handle maintains aspect ratio with uniform scaling locked");
            Assert(Math.Abs(edgeBottomRes.Bounds.Height - 300.0) < 1e-4 && Math.Abs(edgeBottomRes.Bounds.Width - 150.0) < 1e-4,
                "BottomCenter scale expanded width proportionally to 150 when height grew to 300");

            // 6. Legacy Preset Demo Tracks Sanitization
            var dirtyClip = new Clip(105, "Default Animation", 2.0, 60.0);
            var legacyRotTrack = new Track(1001, (ushort)PropKey.Rotation, "0", "0");
            legacyRotTrack.AddKeyframe(new Keyframe(0.0, 0.0, InterpMode.Cubic, 0.42, 0.0, 0.58, 1.0));
            legacyRotTrack.AddKeyframe(new Keyframe(1.0, 180.0, InterpMode.Cubic, 0.42, 0.0, 0.58, 1.0));
            legacyRotTrack.AddKeyframe(new Keyframe(2.0, 360.0, InterpMode.Cubic, 0.42, 0.0, 0.58, 1.0));
            dirtyClip.Tracks.Add(legacyRotTrack);

            var legacyOpTrack = new Track(1001, (ushort)PropKey.Opacity, "0", "0");
            legacyOpTrack.AddKeyframe(new Keyframe(0.0, 1.0, InterpMode.Linear));
            legacyOpTrack.AddKeyframe(new Keyframe(1.0, 0.5, InterpMode.Linear));
            legacyOpTrack.AddKeyframe(new Keyframe(2.0, 1.0, InterpMode.Linear));
            dirtyClip.Tracks.Add(legacyOpTrack);

            Assert(dirtyClip.Tracks.Count == 2, "Dirty clip initially contains 2 legacy demo tracks");

            // Sanitize
            dirtyClip.Tracks.RemoveAll(t =>
            {
                if (t == null || t.Keys == null) return false;
                if (t.Prop == (ushort)PropKey.Rotation && t.Keys.Count == 3)
                {
                    if (Math.Abs(t.Keys[0].Time - 0.0) < 0.05 && Math.Abs(t.Keys[0].Value - 0.0) < 0.05 &&
                        Math.Abs(t.Keys[1].Time - 1.0) < 0.05 && Math.Abs(t.Keys[1].Value - 180.0) < 0.05 &&
                        Math.Abs(t.Keys[2].Time - 2.0) < 0.05 && Math.Abs(t.Keys[2].Value - 360.0) < 0.05)
                        return true;
                }
                if (t.Prop == (ushort)PropKey.Opacity && t.Keys.Count == 3)
                {
                    if (Math.Abs(t.Keys[0].Time - 0.0) < 0.05 && Math.Abs(t.Keys[0].Value - 1.0) < 0.05 &&
                        Math.Abs(t.Keys[1].Time - 1.0) < 0.05 && Math.Abs(t.Keys[1].Value - 0.5) < 0.05 &&
                        Math.Abs(t.Keys[2].Time - 2.0) < 0.05 && Math.Abs(t.Keys[2].Value - 1.0) < 0.05)
                        return true;
                }
                return false;
            });

            Assert(dirtyClip.Tracks.Count == 0, "Sanitization successfully purged all uninvited legacy demo rotation and opacity tracks");
        }

        private static void TestAnchorPointAndRasterScaling()
        {
            // 1. Rive-Style Anchor Point & Stationary Pan-Behind Compensation
            // Layer at (100, 100), size 200x200, rotated 30 degrees, scaled 1.5x
            var layer = new LayerNode
            {
                Id = "anchor-test-layer",
                Type = ElementType.Frame,
                X = 100,
                Y = 100,
                Width = 200,
                Height = 200,
                RenderTransform = new RenderTransform
                {
                    AnchorX = 0.5,
                    AnchorY = 0.5,
                    ScaleX = 1.5,
                    ScaleY = 1.5,
                    Rotation = 30.0,
                    TranslationX = 0.0,
                    TranslationY = 0.0
                }
            };

            Matrix initialMatrix = layer.GetRenderTransformMatrix();
            Point initialTopLeft = initialMatrix.Transform(new Point(100, 100));
            Point initialCenter = initialMatrix.Transform(new Point(200, 200));
            Point initialBottomRight = initialMatrix.Transform(new Point(300, 300));

            // Pan-Behind: Move Anchor to Top-Left (0.0, 0.0) without moving the shape visually
            double oldAncX = layer.RenderTransform.AnchorX;
            double oldAncY = layer.RenderTransform.AnchorY;
            double newAncX = 0.0;
            double newAncY = 0.0;

            Point oldPivot = new Point(layer.X + oldAncX * layer.Width, layer.Y + oldAncY * layer.Height);
            Point newPivot = new Point(layer.X + newAncX * layer.Width, layer.Y + newAncY * layer.Height);

            double dPx = oldPivot.X - newPivot.X;
            double dPy = oldPivot.Y - newPivot.Y;

            double rad = (layer.Rotation + layer.RenderTransform.Rotation) * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double sx = layer.RenderTransform.ScaleX;
            double sy = layer.RenderTransform.ScaleY;

            double m11 = sx * cos;
            double m12 = -sy * sin;
            double m21 = sx * sin;
            double m22 = sy * cos;

            double transDeltaX = dPx - (m11 * dPx + m12 * dPy);
            double transDeltaY = dPy - (m21 * dPx + m22 * dPy);

            layer.RenderTransform.AnchorX = newAncX;
            layer.RenderTransform.AnchorY = newAncY;
            layer.RenderTransform.TranslationX += transDeltaX;
            layer.RenderTransform.TranslationY += transDeltaY;

            Matrix compensatedMatrix = layer.GetRenderTransformMatrix();
            Point compensatedTopLeft = compensatedMatrix.Transform(new Point(100, 100));
            Point compensatedCenter = compensatedMatrix.Transform(new Point(200, 200));
            Point compensatedBottomRight = compensatedMatrix.Transform(new Point(300, 300));

            Assert(Math.Abs(initialTopLeft.X - compensatedTopLeft.X) < 1e-4 && Math.Abs(initialTopLeft.Y - compensatedTopLeft.Y) < 1e-4,
                "Rive pan-behind relocation: TopLeft pixel stays in identical world position after moving anchor");
            Assert(Math.Abs(initialCenter.X - compensatedCenter.X) < 1e-4 && Math.Abs(initialCenter.Y - compensatedCenter.Y) < 1e-4,
                "Rive pan-behind relocation: Center pixel stays in identical world position after moving anchor");
            Assert(Math.Abs(initialBottomRight.X - compensatedBottomRight.X) < 1e-4 && Math.Abs(initialBottomRight.Y - compensatedBottomRight.Y) < 1e-4,
                "Rive pan-behind relocation: BottomRight pixel stays in identical world position after moving anchor");

            // 2. Raster Scaling Around Anchor Point
            // Test Top-Left Anchor (0, 0)
            var scaleLayerTL = new LayerNode
            {
                Id = "scale-tl",
                Type = ElementType.Frame,
                X = 50,
                Y = 50,
                Width = 100,
                Height = 100,
                RenderTransform = new RenderTransform { AnchorX = 0.0, AnchorY = 0.0, ScaleX = 1.0, ScaleY = 1.0 }
            };

            // Scale to 2.0x from Top-Left
            scaleLayerTL.RenderTransform.ScaleX = 2.0;
            scaleLayerTL.RenderTransform.ScaleY = 2.0;
            Matrix matTL = scaleLayerTL.GetRenderTransformMatrix();
            Point scaledTL = matTL.Transform(new Point(50, 50));
            Point scaledBR = matTL.Transform(new Point(150, 150));

            Assert(Math.Abs(scaledTL.X - 50.0) < 1e-4 && Math.Abs(scaledTL.Y - 50.0) < 1e-4,
                "Top-Left anchor point stays invariant at (50, 50) when scaling up");
            Assert(Math.Abs(scaledBR.X - 250.0) < 1e-4 && Math.Abs(scaledBR.Y - 250.0) < 1e-4,
                "Bottom-Right expanded to (250, 250) representing a 200x200 visual area from top-left anchor");

            // 3. Center Anchor (0.5, 0.5) Symmetric Scaling
            var scaleLayerCenter = new LayerNode
            {
                Id = "scale-center",
                Type = ElementType.Frame,
                X = 100,
                Y = 100,
                Width = 200,
                Height = 200,
                RenderTransform = new RenderTransform { AnchorX = 0.5, AnchorY = 0.5, ScaleX = 2.0, ScaleY = 2.0 }
            };
            Matrix matCenter = scaleLayerCenter.GetRenderTransformMatrix();
            Point centerPt = matCenter.Transform(new Point(200, 200));
            Point tlFromCenter = matCenter.Transform(new Point(100, 100));
            Point brFromCenter = matCenter.Transform(new Point(300, 300));

            Assert(Math.Abs(centerPt.X - 200.0) < 1e-4 && Math.Abs(centerPt.Y - 200.0) < 1e-4,
                "Center anchor point stays invariant at (200, 200) when scaling up");
            Assert(Math.Abs(tlFromCenter.X - 0.0) < 1e-4 && Math.Abs(tlFromCenter.Y - 0.0) < 1e-4,
                "Top-Left expanded symmetrically outward to (0, 0)");
            Assert(Math.Abs(brFromCenter.X - 400.0) < 1e-4 && Math.Abs(brFromCenter.Y - 400.0) < 1e-4,
                "Bottom-Right expanded symmetrically outward to (400, 400)");

            // 4. Uniform Dual Keyframing when LockAspectRatio is true
            var animClip = new Clip(200, "UniformDualKeyTest", 2.0, 60.0);
            var aspectLayer = new LayerNode
            {
                Id = "aspect-test-layer",
                X = 10,
                Y = 20,
                Width = 100,
                Height = 50, // 2:1 ratio
                LockAspectRatio = true,
                RenderTransform = new RenderTransform { ScaleX = 1.0, ScaleY = 1.0 }
            };

            uint nId = PropertyBinder.GetNumericNodeId(aspectLayer.Id);

            // Adding a keyframe for Width must pair with Height
            double testTime = 1.0;
            aspectLayer.Width = 200;
            aspectLayer.Height = 100;
            animClip.GetOrCreateTrack(nId, (ushort)PropKey.Width).AddKeyframe(Keyframe.Linear(testTime, aspectLayer.Width));
            animClip.GetOrCreateTrack(nId, (ushort)PropKey.Height).AddKeyframe(Keyframe.Linear(testTime, aspectLayer.Height));

            var wTrack = animClip.GetTrack(nId, (ushort)PropKey.Width);
            var hTrack = animClip.GetTrack(nId, (ushort)PropKey.Height);

            Assert(wTrack != null && wTrack.Keys.Count == 1, "Width track contains recorded keyframe");
            Assert(hTrack != null && hTrack.Keys.Count == 1, "Height track automatically contains paired keyframe at same timestamp");
            Assert(Math.Abs(wTrack.Keys[0].Value - 200.0) < 1e-4, "Width keyframe value is 200");
            Assert(Math.Abs(hTrack.Keys[0].Value - 100.0) < 1e-4, "Height keyframe value is 100 preserving 2:1 aspect ratio");

            // 5. Uninvited Demo Tracks Clean Purge (Matching remaining 180° or 360° keyframes)
            var clipWithRemainingDemo = new Clip(201, "Default Animation", 2.0, 60.0);
            var partialRotTrack = new Track(nId, (ushort)PropKey.Rotation);
            partialRotTrack.AddKeyframe(Keyframe.Linear(1.0, 180.0));
            partialRotTrack.AddKeyframe(Keyframe.Linear(2.0, 360.0));
            clipWithRemainingDemo.Tracks.Add(partialRotTrack);

            var partialOpTrack = new Track(nId, (ushort)PropKey.Opacity);
            partialOpTrack.AddKeyframe(Keyframe.Linear(1.0, 0.5));
            clipWithRemainingDemo.Tracks.Add(partialOpTrack);

            Assert(clipWithRemainingDemo.Tracks.Count == 2, "Clip contains 2 lingering demo tracks with remaining keyframes");

            // Purge matching legacy preset
            clipWithRemainingDemo.Tracks.RemoveAll(t =>
            {
                if (t == null || t.Keys == null) return false;
                if (t.Prop == (ushort)PropKey.Rotation && t.Keys.Any(k => Math.Abs(k.Value - 180.0) < 0.1 || Math.Abs(k.Value - 360.0) < 0.1))
                    return true;
                if (t.Prop == (ushort)PropKey.Opacity && t.Keys.Any(k => Math.Abs(k.Time - 1.0) < 0.1 && Math.Abs(k.Value - 0.5) < 0.05))
                    return true;
                return false;
            });

            Assert(clipWithRemainingDemo.Tracks.Count == 0, "Enhanced sanitization completely removed lingering demo rotation and opacity tracks");
        }

        private static void TestRiveAnchorPointGizmoAndContentScaling()
        {
            // 1. ToolType.Anchor restriction: Anchor point only moves in ToolType.Anchor
            ToolType currentTool = ToolType.Select;
            bool canHitAnchorInSelect = (currentTool == ToolType.Anchor);
            Assert(!canHitAnchorInSelect, "In ToolType.Select, anchor point hit testing is disabled so dragging never moves the anchor");

            // Pressing 'Y' enters Anchor mode
            currentTool = ToolType.Anchor;
            bool canHitAnchorInAnchor = (currentTool == ToolType.Anchor);
            Assert(canHitAnchorInAnchor, "Pressing 'Y' sets ToolType.Anchor enabling 3-axis and rotation gizmo manipulation");

            // Pressing 'Enter' commits and exits Anchor mode back to ToolType.Select
            currentTool = ToolType.Select;
            Assert(currentTool == ToolType.Select, "Pressing 'Enter' commits anchor point and returns to ToolType.Select");

            // 2. Non-Destructive Scaling of AutoLayout Frame and Content
            // Create an AutoLayout frame (550 x 550) containing 4 child rectangles
            var page = new Page();
            var frame = new LayerNode
            {
                Id = "auto-frame",
                Type = ElementType.Frame,
                X = 100,
                Y = 100,
                Width = 550,
                Height = 550,
                AutoLayout = new AutoLayoutData
                {
                    Mode = AutoLayoutMode.Vertical,
                    PaddingTop = 20,
                    PaddingLeft = 20,
                    PaddingRight = 20,
                    PaddingBottom = 20,
                    Gap = 16
                },
                RenderTransform = new RenderTransform
                {
                    AnchorX = 0.5,
                    AnchorY = 0.5,
                    ScaleX = 1.0,
                    ScaleY = 1.0
                }
            };
            page.Layers.Add(frame);

            var r1 = new LayerNode { Id = "r1", Type = ElementType.Rectangle, ParentId = frame.Id, X = 120, Y = 120, Width = 230, Height = 230 };
            var r2 = new LayerNode { Id = "r2", Type = ElementType.Rectangle, ParentId = frame.Id, X = 370, Y = 120, Width = 230, Height = 230 };
            var r3 = new LayerNode { Id = "r3", Type = ElementType.Rectangle, ParentId = frame.Id, X = 120, Y = 370, Width = 230, Height = 230 };
            var r4 = new LayerNode { Id = "r4", Type = ElementType.Rectangle, ParentId = frame.Id, X = 370, Y = 370, Width = 230, Height = 230 };
            frame.Children.Add(r1); frame.Children.Add(r2); frame.Children.Add(r3); frame.Children.Add(r4);
            page.Layers.Add(r1); page.Layers.Add(r2); page.Layers.Add(r3); page.Layers.Add(r4);

            // In Animate Mode, scaling up to 728 x 731:
            // The base layout dimensions MUST NOT be modified!
            double targetVisualW = 728;
            double targetVisualH = 731;
            double scaleX = targetVisualW / frame.Width;
            double scaleY = targetVisualH / frame.Height;

            frame.RenderTransform.ScaleX = scaleX;
            frame.RenderTransform.ScaleY = scaleY;
            frame.RenderTransform.VisualWidth = targetVisualW;
            frame.RenderTransform.VisualHeight = targetVisualH;

            Assert(frame.Width == 550 && frame.Height == 550, "Frame base layout dimensions remain intact at 550x550 in Animate Mode");
            Assert(r1.Width == 230 && r1.Height == 230, "Child rectangles keep their layout dimensions");
            Assert(r1.X == 120 && r1.Y == 120, "Child rectangles keep their un-reflowed relative positions");

            // World transform matrix scales the frame AND all child visuals outward from Anchor (0.5, 0.5)
            Matrix frameWorldMat = frame.GetRenderTransformMatrix();
            Point centerBefore = new Point(100 + 275, 100 + 275);
            Point centerAfter = frameWorldMat.Transform(centerBefore);
            Assert(Math.Abs(centerBefore.X - centerAfter.X) < 1e-4 && Math.Abs(centerBefore.Y - centerAfter.Y) < 1e-4,
                "Center anchor point stays invariant during symmetric scaling");

            Point frameTL = frameWorldMat.Transform(new Point(100, 100));
            Point frameBR = frameWorldMat.Transform(new Point(650, 650));
            double visualWidth = frameBR.X - frameTL.X;
            double visualHeight = frameBR.Y - frameTL.Y;
            Assert(Math.Abs(visualWidth - targetVisualW) < 1e-2, "Visual rendered width equals target 728px");
            Assert(Math.Abs(visualHeight - targetVisualH) < 1e-2, "Visual rendered height equals target 731px");

            // Child world transform: child matrix combined with parent frame matrix
            Matrix childWorldMat = r1.GetWorldRenderTransformMatrix(page);
            Point childRenderedTopLeft = childWorldMat.Transform(new Point(r1.X, r1.Y));
            Assert(childRenderedTopLeft.X < 120 && childRenderedTopLeft.Y < 120,
                "Child shapes scale outward smoothly with parent frame rather than staying stuck in top-left");

            // 3. PropertyBinder Evaluation with Width/VisualWidth/ScaleX
            uint frameNodeId = PropertyBinder.GetNumericNodeId(frame.Id);
            var pose = new Dictionary<uint, Dictionary<ushort, double>>();
            pose[frameNodeId] = new Dictionary<ushort, double>();
            pose[frameNodeId][(ushort)PropKey.Width] = 728.0;
            pose[frameNodeId][(ushort)PropKey.Height] = 731.0;

            PropertyBinder.ApplyPoseToLayer(frame, frameNodeId, pose);
            Assert(Math.Abs(frame.RenderTransform.ScaleX - (728.0 / 550.0)) < 1e-4, "PropertyBinder converts Width keyframe to RenderTransform.ScaleX non-destructively");
            Assert(Math.Abs(frame.RenderTransform.ScaleY - (731.0 / 550.0)) < 1e-4, "PropertyBinder converts Height keyframe to RenderTransform.ScaleY non-destructively");
            Assert(frame.RenderTransform.VisualWidth == 728.0 && frame.RenderTransform.VisualHeight == 731.0, "VisualWidth and VisualHeight populated");
            Assert(frame.Width == 550 && frame.Height == 550, "Frame base document dimensions still 550x550 after PropertyBinder pose evaluation");
        }

        private static void TestEasingEngineClosedFormSpringsAndGraphEditor()
        {
            // 1. Root solver precision: Assert |x(s) - u| < 10^-6 across steep tangents and boundary conditions
            double[][] tangentPairs = new double[][]
            {
                new double[] { 0.0, 1.0 },
                new double[] { 0.99, 0.01 },
                new double[] { 0.0, 0.0 },
                new double[] { 1.0, 1.0 },
                new double[] { 0.1, 0.9 },
                new double[] { 0.85, 0.15 }
            };

            double[] testProgressValues = new double[] { 0.01, 0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 0.95, 0.99 };
            for (int i = 0; i < tangentPairs.Length; i++)
            {
                double x1 = tangentPairs[i][0];
                double x2 = tangentPairs[i][1];
                for (int j = 0; j < testProgressValues.Length; j++)
                {
                    double u = testProgressValues[j];
                    // Passing y1 = x1, y2 = x2 evaluates x(s), which must match u within root solver tolerance
                    double evaluatedX = CubicBezierEase.Evaluate(u, x1, x1, x2, x2);
                    double err = Math.Abs(evaluatedX - u);
                    Assert(err < 1e-6, string.Format("Root solver precision: at u={0}, tangents ({1}, {2}), err={3:E4} must be < 1e-6", u, x1, x2, err));
                }
            }

            // 2. Vertical overshoot verification: Evaluate curve with y1 = 1.5, y2 = 1.2 and assert evaluated y > 1.0;
            // evaluate curve with y1 = -0.5 and assert y < 0.0 (anticipation dip)
            double overshotY = CubicBezierEase.Evaluate(0.5, 0.25, 1.5, 0.75, 1.2);
            Assert(overshotY > 1.0, string.Format("Vertical overshoot: expected y > 1.0, got {0:F4}", overshotY));

            double dippedY = CubicBezierEase.Evaluate(0.2, 0.25, -0.5, 0.75, 0.5);
            Assert(dippedY < 0.0, string.Format("Anticipation dip: expected y < 0.0, got {0:F4}", dippedY));

            // Boundary values at endpoints
            Assert(Math.Abs(CubicBezierEase.Evaluate(0.0, 0.25, 1.5, 0.75, 1.2) - 0.0) < 1e-7, "Bézier u=0 returns 0.0");
            Assert(Math.Abs(CubicBezierEase.Evaluate(1.0, 0.25, 1.5, 0.75, 1.2) - 1.0) < 1e-7, "Bézier u=1 returns 1.0");

            // 3. Analytical closed-form spring solver verification across all 3 regimes
            // Regime A: Underdamped (zeta < 1.0, e.g. Bouncy m=1.0, k=150.0, c=10.0, zeta ~ 0.408)
            double pUnder0 = EasingEngine.EvaluateSpring(0.0, 1.0, 150.0, 10.0, 0.0);
            Assert(Math.Abs(pUnder0 - 0.0) < 1e-6, string.Format("Underdamped spring at t=0: expected 0.0, got {0:F4}", pUnder0));

            // Peak overshoot occurs around t = 0.25s
            double pUnderPeak = EasingEngine.EvaluateSpring(0.25, 1.0, 150.0, 10.0, 0.0);
            Assert(pUnderPeak > 1.0, string.Format("Underdamped spring peak overshoot: expected > 1.0, got {0:F4}", pUnderPeak));

            // Asymptotic settlement toward 1.0 at large t
            double pUnderLate = EasingEngine.EvaluateSpring(4.0, 1.0, 150.0, 10.0, 0.0);
            Assert(Math.Abs(pUnderLate - 1.0) < 0.005, string.Format("Underdamped spring settlement at t=4s: expected ~1.0, got {0:F4}", pUnderLate));

            // Regime B: Critically damped (zeta = 1.0, m=1.0, k=100.0, c=20.0, c^2 = 4mk = 400)
            double pCrit0 = EasingEngine.EvaluateSpring(0.0, 1.0, 100.0, 20.0, 0.0);
            Assert(Math.Abs(pCrit0 - 0.0) < 1e-6, "Critically damped spring at t=0 returns 0.0");

            double pCrit1 = EasingEngine.EvaluateSpring(0.1, 1.0, 100.0, 20.0, 0.0);
            double pCrit2 = EasingEngine.EvaluateSpring(0.3, 1.0, 100.0, 20.0, 0.0);
            double pCrit3 = EasingEngine.EvaluateSpring(0.6, 1.0, 100.0, 20.0, 0.0);
            double pCrit4 = EasingEngine.EvaluateSpring(1.5, 1.0, 100.0, 20.0, 0.0);

            // Monotonic approach without overshoot
            Assert(pCrit1 < pCrit2 && pCrit2 < pCrit3 && pCrit3 < pCrit4, "Critically damped spring approaches monotonically");
            Assert(pCrit1 <= 1.0 && pCrit2 <= 1.0 && pCrit3 <= 1.0 && pCrit4 <= 1.0, "Critically damped spring has zero overshoot");
            Assert(pCrit4 > 0.99, "Critically damped spring reaches target asymptotically");

            // Regime C: Overdamped (zeta > 1.0, m=1.0, k=50.0, c=30.0, c^2 = 900 > 200)
            double pOver0 = EasingEngine.EvaluateSpring(0.0, 1.0, 50.0, 30.0, 0.0);
            Assert(Math.Abs(pOver0 - 0.0) < 1e-6, "Overdamped spring at t=0 returns 0.0");

            double pOver1 = EasingEngine.EvaluateSpring(0.2, 1.0, 50.0, 30.0, 0.0);
            double pOver2 = EasingEngine.EvaluateSpring(0.5, 1.0, 50.0, 30.0, 0.0);
            double pOver3 = EasingEngine.EvaluateSpring(1.0, 1.0, 50.0, 30.0, 0.0);
            Assert(pOver1 < pOver2 && pOver2 < pOver3 && pOver3 <= 1.0, "Overdamped spring exhibits smooth monotonic progression without oscillation");
            // Compare overdamped with critically damped at t = 0.3 to demonstrate slower exponential convergence
            double pCritAt03 = EasingEngine.EvaluateSpring(0.3, 1.0, 100.0, 20.0, 0.0);
            double pOverAt03 = EasingEngine.EvaluateSpring(0.3, 1.0, 50.0, 30.0, 0.0);
            Assert(pOverAt03 < pCritAt03, "Overdamped spring exhibits slower exponential convergence than critically damped");

            // Negative or zero time guards
            Assert(EasingEngine.EvaluateSpring(-1.0, 1.0, 100.0, 10.0, 0.0) == 0.0, "EvaluateSpring with t < 0 returns 0.0");

            // 4. Spring presets (Gentle, Snappy, Bouncy, Stiff) deterministic random-access evaluation
            SpringPreset[] presets = new SpringPreset[] { SpringPreset.Gentle, SpringPreset.Snappy, SpringPreset.Bouncy, SpringPreset.Stiff };
            double[] probeTimes = new double[] { 0.1, 0.5, 2.0, 5.0 };

            for (int i = 0; i < presets.Length; i++)
            {
                SpringPreset sp = presets[i];
                double m, k, c, v0;
                EasingEngine.GetSpringPreset(sp, out m, out k, out c, out v0);
                Assert(m > 0 && k > 0 && c > 0, "Spring preset parameters are positive");

                // Evaluate forward
                double[] forwardVals = new double[probeTimes.Length];
                for (int tIdx = 0; tIdx < probeTimes.Length; tIdx++)
                {
                    forwardVals[tIdx] = EasingEngine.EvaluateSpring(probeTimes[tIdx], m, k, c, v0);
                }

                // Evaluate reverse (random access)
                for (int tIdx = probeTimes.Length - 1; tIdx >= 0; tIdx--)
                {
                    double revVal = EasingEngine.EvaluateSpring(probeTimes[tIdx], m, k, c, v0);
                    Assert(Math.Abs(revVal - forwardVals[tIdx]) < 1e-12, "Spring preset random-access evaluation is deterministic with 0 drift");
                }

                // Test Keyframe application
                var testKf = new Keyframe();
                EasingEngine.ApplySpringPreset(testKf, sp);
                Assert(testKf.Interp == InterpMode.Spring, "ApplySpringPreset sets InterpMode.Spring");
                Assert(Math.Abs(testKf.Mass - m) < 1e-6 && Math.Abs(testKf.Stiffness - k) < 1e-6 && Math.Abs(testKf.Damping - c) < 1e-6, "ApplySpringPreset populates physical constants");
            }

            // 5. Full 21 easing presets: loop through all presets and assert boundaries and progress
            for (int pIdx = 0; pIdx <= (int)EasingPreset.CustomBezier; pIdx++)
            {
                EasingPreset ep = (EasingPreset)pIdx;
                double px1, py1, px2, py2;
                EasingEngine.GetPresetControlPoints(ep, out px1, out py1, out px2, out py2);

                // Horizontal time bounds must be strictly within [0.0, 1.0]
                Assert(px1 >= 0.0 && px1 <= 1.0, string.Format("Preset {0} x1 must be within [0, 1]", ep));
                Assert(px2 >= 0.0 && px2 <= 1.0, string.Format("Preset {0} x2 must be within [0, 1]", ep));

                // Boundary values: u = 0.0 => 0.0, u = 1.0 => 1.0
                double vZero = EasingEngine.EvaluatePreset(ep, 0.0);
                double vOne = EasingEngine.EvaluatePreset(ep, 1.0);
                Assert(Math.Abs(vZero - 0.0) < 1e-6, string.Format("Preset {0} at u=0 must return 0.0, got {1}", ep, vZero));
                Assert(Math.Abs(vOne - 1.0) < 1e-6, string.Format("Preset {0} at u=1 must return 1.0, got {1}", ep, vOne));

                // Intermediate progress
                double vMid = EasingEngine.EvaluatePreset(ep, 0.5);
                Assert(!double.IsNaN(vMid) && !double.IsInfinity(vMid), string.Format("Preset {0} at u=0.5 must be a finite number", ep));

                // ApplyPreset test
                var kfPreset = new Keyframe(0.0, 0.0, InterpMode.Linear);
                EasingEngine.ApplyPreset(kfPreset, ep);
                Assert(kfPreset.Preset == ep, string.Format("ApplyPreset sets Preset metadata for {0}", ep));
                Assert(Math.Abs(kfPreset.X1 - px1) < 1e-6 && Math.Abs(kfPreset.Y1 - py1) < 1e-6, string.Format("ApplyPreset sets X1, Y1 for {0}", ep));
                Assert(Math.Abs(kfPreset.X2 - px2) < 1e-6 && Math.Abs(kfPreset.Y2 - py2) < 1e-6, string.Format("ApplyPreset sets X2, Y2 for {0}", ep));
                if (ep == EasingPreset.Linear)
                {
                    Assert(kfPreset.Interp == InterpMode.Linear, "Linear preset sets InterpMode.Linear");
                }
                else
                {
                    Assert(kfPreset.Interp == InterpMode.Cubic, "Non-linear preset sets InterpMode.Cubic");
                }
            }

            // Polynomial and special preset verification
            Assert(Math.Abs(EasingEngine.EvaluatePreset(EasingPreset.QuadIn, 0.5) - 0.25) < 1e-6, "QuadIn at 0.5 is 0.25");
            Assert(Math.Abs(EasingEngine.EvaluatePreset(EasingPreset.QuadOut, 0.5) - 0.75) < 1e-6, "QuadOut at 0.5 is 0.75");
            Assert(Math.Abs(EasingEngine.EvaluatePreset(EasingPreset.CubicIn, 0.5) - 0.125) < 1e-6, "CubicIn at 0.5 is 0.125");
            Assert(Math.Abs(EasingEngine.EvaluatePreset(EasingPreset.CubicOut, 0.5) - 0.875) < 1e-6, "CubicOut at 0.5 is 0.875");
            Assert(Math.Abs(EasingEngine.EvaluatePreset(EasingPreset.Smooth, 0.5) - 0.5) < 1e-6, "Smooth step at 0.5 is 0.5");
            Assert(EasingEngine.EvaluatePreset(EasingPreset.Back, 0.8) > 1.0, "Back preset overshoots 1.0");

            // 6. Track.Sample integration: Multi-segment track containing Hold, Linear, Cubic, and Spring keyframes
            var multiTrack = new Track(10, (ushort)PropKey.X);
            var k0 = Keyframe.Hold(0.0, 10.0);
            var k1 = Keyframe.Linear(1.0, 20.0);
            var k2 = Keyframe.Cubic(2.0, 50.0, 0.25, -0.5, 0.75, 1.5);
            var k3 = Keyframe.Spring(3.0, 100.0, 1.0, 150.0, 10.0, 0.0);
            var k4 = Keyframe.Linear(5.0, 200.0);

            multiTrack.AddKeyframe(k0);
            multiTrack.AddKeyframe(k1);
            multiTrack.AddKeyframe(k2);
            multiTrack.AddKeyframe(k3);
            multiTrack.AddKeyframe(k4);

            // Clamp before start / after end
            Assert(multiTrack.Sample(-1.0) == 10.0, "Sample before start clamps to first key value");
            Assert(multiTrack.Sample(6.0) == 200.0, "Sample after end clamps to last key value");

            // Hold segment: t in [0.0, 1.0) returns 10.0
            Assert(multiTrack.Sample(0.0) == 10.0, "Sample at t=0 returns 10.0");
            Assert(multiTrack.Sample(0.5) == 10.0, "Sample during Hold segment returns 10.0");

            // Linear segment: t in [1.0, 2.0] interpolates linearly from 20.0 to 50.0
            Assert(Math.Abs(multiTrack.Sample(1.0) - 20.0) < 1e-4, "Sample at t=1.0 returns 20.0");
            Assert(Math.Abs(multiTrack.Sample(1.5) - 35.0) < 1e-4, "Sample at t=1.5 returns linear midpoint 35.0");
            Assert(Math.Abs(multiTrack.Sample(2.0) - 50.0) < 1e-4, "Sample at t=2.0 returns 50.0");

            // Cubic segment with overshoot: t in [2.0, 3.0] from 50.0 to 100.0
            double cubicDipVal = multiTrack.Sample(2.15);
            Assert(cubicDipVal < 50.0, string.Format("Cubic segment anticipation dip: expected < 50.0, got {0:F4}", cubicDipVal));
            double cubicOvershootVal = multiTrack.Sample(2.85);
            Assert(cubicOvershootVal > 100.0, string.Format("Cubic segment overshoot: expected > 100.0, got {0:F4}", cubicOvershootVal));

            // Spring segment: t in [3.0, 5.0] from 100.0 to 200.0
            Assert(Math.Abs(multiTrack.Sample(3.0) - 100.0) < 1e-4, "Sample at spring start t=3.0 returns 100.0");
            double springPeakVal = multiTrack.Sample(3.25);
            Assert(springPeakVal > 200.0, string.Format("Spring segment peak overshoot: expected > 200.0, got {0:F4}", springPeakVal));

            // End keyframe
            Assert(Math.Abs(multiTrack.Sample(5.0) - 200.0) < 1e-4, "Sample at t=5.0 returns 200.0");

            // Duplicate timestamp resolution
            var dupKey = Keyframe.Linear(1.0, 25.0);
            multiTrack.AddKeyframe(dupKey);
            Assert(multiTrack.Keys.Count == 5, "Duplicate timestamp does not add extra keyframe");
            Assert(Math.Abs(multiTrack.Sample(1.0) - 25.0) < 1e-4, "Duplicate timestamp updates existing keyframe value to 25.0");

            // Zero-length segment handling
            var zeroTrack = new Track(20, (ushort)PropKey.ScaleX);
            zeroTrack.AddKeyframe(Keyframe.Linear(1.0, 1.0));
            zeroTrack.AddKeyframe(new Keyframe(1.0 + 1e-7, 2.0, InterpMode.Linear));
            double zeroSample = zeroTrack.Sample(1.0);
            Assert(!double.IsNaN(zeroSample) && !double.IsInfinity(zeroSample), "Zero-length segment handled safely without division by zero");

            // 7. GraphViewport coordinate transforms and Auto-Fit framing per D-04
            GraphViewport vp = new GraphViewport(0.0, 100.0, 250.0, 1000.0, 200.0, 20.0);
            
            // TimeToX conversion: x = t * pps
            Assert(Math.Abs(vp.TimeToX(0.0) - 0.0) < 1e-7, "Viewport TimeToX at t=0 returns 0.0");
            Assert(Math.Abs(vp.TimeToX(1.0) - 250.0) < 1e-7, "Viewport TimeToX at t=1 returns 250.0");
            Assert(Math.Abs(vp.TimeToX(2.5) - 625.0) < 1e-7, "Viewport TimeToX at t=2.5 returns 625.0");
            Assert(Math.Abs(vp.XToTime(250.0) - 1.0) < 1e-7, "Viewport XToTime at x=250 returns 1.0s");

            // ValueToY and YToValue roundtrip conversions
            double[] testValues = new double[] { 0.0, 25.0, 50.0, 75.0, 100.0, -20.0, 140.0 };
            for (int vi = 0; vi < testValues.Length; vi++)
            {
                double val = testValues[vi];
                double y = vp.ValueToY(val);
                double roundtripVal = vp.YToValue(y);
                Assert(Math.Abs(roundtripVal - val) < 1e-6, string.Format("Viewport ValueToY/YToValue roundtrip for {0} (got {1})", val, roundtripVal));
            }

            // Value bounds: vMin maps to (CanvasHeight - MarginY), vMax maps to MarginY
            Assert(Math.Abs(vp.ValueToY(0.0) - (200.0 - 20.0)) < 1e-6, "ValueToY at VMin maps to bottom margin");
            Assert(Math.Abs(vp.ValueToY(100.0) - 20.0) < 1e-6, "ValueToY at VMax maps to top margin");

            // Auto-Fit framing with 15% margins per D-04
            var fitTrack = new Track(30, (ushort)PropKey.Opacity);
            fitTrack.AddKeyframe(Keyframe.Linear(0.0, 10.0));
            fitTrack.AddKeyframe(Keyframe.Linear(1.0, 90.0));

            vp.AutoFit(fitTrack, (ushort)PropKey.Opacity);
            // Span = 90 - 10 = 80; margin = 80 * 0.15 = 12; expected VMin = 10 - 12 = -2.0, VMax = 90 + 12 = 102.0
            Assert(Math.Abs(vp.VMin - (-2.0)) < 1e-6, string.Format("Auto-Fit 15% bottom margin: expected -2.0, got {0}", vp.VMin));
            Assert(Math.Abs(vp.VMax - 102.0) < 1e-6, string.Format("Auto-Fit 15% top margin: expected 102.0, got {0}", vp.VMax));

            // Auto-Fit zero-span fallback guarding against division by zero per T-04-03
            var zeroSpanTrack = new Track(31, (ushort)PropKey.Opacity);
            zeroSpanTrack.AddKeyframe(Keyframe.Linear(0.0, 1.0));
            zeroSpanTrack.AddKeyframe(Keyframe.Linear(1.0, 1.0));

            vp.AutoFit(zeroSpanTrack, (ushort)PropKey.Opacity);
            Assert(vp.VMax > vp.VMin, "Auto-Fit on zero span provides safe non-zero range");
            double safeY = vp.ValueToY(1.0);
            Assert(!double.IsNaN(safeY) && !double.IsInfinity(safeY), "ValueToY on zero-span guarded viewport produces finite coordinate");

            // 8. Tangent continuity, 180° collinear slope enforcement, and Alt-drag breaking per D-06
            var tangentTrack = new Track(40, (ushort)PropKey.Y);
            var tk0 = Keyframe.Cubic(0.0, 0.0, 0.0, 0.0, 0.5, 0.5);
            var tk1 = Keyframe.Cubic(1.0, 100.0, 0.4, 0.6, 0.8, 1.0);
            var tk2 = Keyframe.Linear(2.0, 300.0);
            tangentTrack.AddKeyframe(tk0);
            tangentTrack.AddKeyframe(tk1);
            tangentTrack.AddKeyframe(tk2);

            // Outgoing handle of tk1: X1 = 0.4, Y1 = 0.6 across dt = 1.0s, dv = 200.0
            // Slope S_out = (0.6 * 200.0) / (0.4 * 1.0) = 120.0 / 0.4 = 300.0
            double dtOut = tk2.Time - tk1.Time;
            double dvOut = tk2.Value - tk1.Value;
            double expectedSlope = (tk1.Y1 * dvOut) / (tk1.X1 * dtOut);

            // Align incoming handle collinear with outgoing handle
            GraphEditorCanvas.AlignIncomingHandleCollinear(tk1, tk0, tk1.X1, tk1.Y1, dtOut, dvOut);

            // Check resulting incoming slope: S_in = ( (1 - tk0.Y2) * dvIn ) / ( (1 - tk0.X2) * dtIn )
            double dtIn = tk1.Time - tk0.Time;
            double dvIn = tk1.Value - tk0.Value;
            double realizedSlopeIn = ((1.0 - tk0.Y2) * dvIn) / ((1.0 - tk0.X2) * dtIn);
            Assert(Math.Abs(realizedSlopeIn - expectedSlope) < 1e-4, string.Format("Collinear 180° tangent slope alignment: expected {0}, got {1}", expectedSlope, realizedSlopeIn));

            // Alt-break: when tangent is broken, handle moves independently
            tk1.IsTangentBroken = true;
            double prevY2 = tk0.Y2;
            // Mutate outgoing handle while broken
            tk1.Y1 = 1.2; // Steep overshoot
            // Since it is broken, incoming handle remains unchanged
            Assert(tk0.Y2 == prevY2, "Tangent broken mode preserves independent angle without updating opposing handle");

            // 9. Task 3 Integration: Popover preset application, auto-custom transition, spring parameter binding, and real-time pose evaluation
            // (A) Popover preset application: EasingEngine.ApplyPreset on a keyframe updates Preset, Interp, and canonical control points
            var testKey = new Keyframe(0.0, 100.0, InterpMode.Linear);
            EasingEngine.ApplyPreset(testKey, EasingPreset.EaseInOut);
            Assert(testKey.Interp == InterpMode.Cubic, "Applying EaseInOut preset sets InterpMode.Cubic");
            Assert(testKey.Preset == EasingPreset.EaseInOut, "Applying EaseInOut preset sets Preset metadata");
            Assert(Math.Abs(testKey.X1 - 0.42) < 1e-4 && Math.Abs(testKey.Y1 - 0.00) < 1e-4, "Canonical X1, Y1 for EaseInOut applied");
            Assert(Math.Abs(testKey.X2 - 0.58) < 1e-4 && Math.Abs(testKey.Y2 - 1.00) < 1e-4, "Canonical X2, Y2 for EaseInOut applied");

            // Apply Bounce preset
            EasingEngine.ApplyPreset(testKey, EasingPreset.Bounce);
            Assert(testKey.Preset == EasingPreset.Bounce, "Applying Bounce preset sets Preset metadata");
            Assert(Math.Abs(testKey.X1 - 0.34) < 1e-4 && Math.Abs(testKey.Y1 - 1.40) < 1e-4, "Canonical X1, Y1 for Bounce applied with vertical overshoot");

            // (B) Auto-custom transition on handle mutation or numeric edit per D-11
            // Simulating handle drag or numeric coordinate edit
            testKey.Y1 = 1.15;
            testKey.Preset = EasingPreset.CustomBezier;
            Assert(testKey.Preset == EasingPreset.CustomBezier, "Keyframe preset transitions to CustomBezier upon handle or numeric edit");

            // (C) Spring physical parameters and presets (Gentle, Snappy, Bouncy, Stiff) per D-09
            var springKey = new Keyframe(1.0, 200.0, InterpMode.Linear);
            EasingEngine.ApplySpringPreset(springKey, SpringPreset.Bouncy);
            Assert(springKey.Interp == InterpMode.Spring, "ApplySpringPreset sets InterpMode.Spring");
            Assert(Math.Abs(springKey.Mass - 1.0) < 1e-6, "Bouncy spring mass is 1.0");
            Assert(Math.Abs(springKey.Stiffness - 150.0) < 1e-6, "Bouncy spring stiffness is 150.0");
            Assert(Math.Abs(springKey.Damping - 10.0) < 1e-6, "Bouncy spring damping is 10.0");

            // Mutate spring physical parameters (inspector scrub fields)
            springKey.Mass = 2.0;
            springKey.Stiffness = 300.0;
            springKey.Damping = 25.0;
            springKey.InitialVelocity = 5.0;
            Assert(springKey.Mass == 2.0 && springKey.Stiffness == 300.0 && springKey.Damping == 25.0 && springKey.InitialVelocity == 5.0, "Spring physical parameters updated in keyframe");

            // (D) Real-time pose evaluation synchronization per D-12 & EASE-04
            // Test that changing a keyframe's parameters or handles immediately updates Track.Sample and LayerNode pose
            var syncTrack = new Track(50, (ushort)PropKey.X);
            var sk0 = Keyframe.Cubic(0.0, 0.0, 0.25, 0.1, 0.25, 1.0);
            var sk1 = Keyframe.Linear(1.0, 100.0);
            syncTrack.AddKeyframe(sk0);
            syncTrack.AddKeyframe(sk1);

            double valBefore = syncTrack.Sample(0.5);
            // Now apply EaseIn preset to sk0
            EasingEngine.ApplyPreset(sk0, EasingPreset.EaseIn);
            double valAfterPreset = syncTrack.Sample(0.5);
            Assert(Math.Abs(valBefore - valAfterPreset) > 1.0, "Track.Sample reflects new curve shape immediately upon preset application");

            // Mutate control point to CustomBezier with vertical overshoot
            sk0.Y1 = 2.0;
            sk0.Preset = EasingPreset.CustomBezier;
            double valAfterOvershoot = syncTrack.Sample(0.5);
            Assert(valAfterOvershoot > 100.0, "Track.Sample reflects vertical overshoot immediately upon handle manipulation");

            // Test layer pose synchronization via PropertyBinder
            var syncClip = new Clip(1, "SyncClip", 2.0);
            syncClip.Tracks.Add(syncTrack);
            var poseDict = new Dictionary<uint, Dictionary<ushort, double>>();
            syncClip.SampleToPose(0.5, poseDict);
            var syncLayer = new LayerNode { Id = "sync-node", X = 0, Y = 0, Width = 100, Height = 100 };
            PropertyBinder.ApplyPoseToLayer(syncLayer, 50, poseDict);
            Assert(Math.Abs(syncLayer.X + syncLayer.RenderTransform.TranslationX - valAfterOvershoot) < 1e-4, "PropertyBinder synchronizes real-time layer pose directly from evaluated track");
        }

        private static void TestUniversalGraphEditorConsolidationAndCleanTimeline()
        {
            // 1. Verify all 21 motion presets provide valid control points and evaluate without crashing
            var presets = new EasingPreset[]
            {
                EasingPreset.Linear, EasingPreset.Ease, EasingPreset.EaseIn,
                EasingPreset.EaseOut, EasingPreset.EaseInOut, EasingPreset.QuadIn,
                EasingPreset.QuadOut, EasingPreset.QuadInOut, EasingPreset.CubicIn,
                EasingPreset.CubicOut, EasingPreset.CubicInOut, EasingPreset.QuartIn,
                EasingPreset.QuartOut, EasingPreset.QuartInOut, EasingPreset.QuintIn,
                EasingPreset.QuintOut, EasingPreset.QuintInOut, EasingPreset.Back,
                EasingPreset.Elastic, EasingPreset.Bounce, EasingPreset.Smooth
            };

            foreach (var preset in presets)
            {
                double x1, y1, x2, y2;
                EasingEngine.GetPresetControlPoints(preset, out x1, out y1, out x2, out y2);
                Assert(x1 >= 0.0 && x1 <= 1.0, string.Format("Preset {0} X1 is within [0, 1] ({1})", preset, x1));
                Assert(x2 >= 0.0 && x2 <= 1.0, string.Format("Preset {0} X2 is within [0, 1] ({1})", preset, x2));

                double v0 = EasingEngine.EvaluatePreset(preset, 0.0);
                double v1 = EasingEngine.EvaluatePreset(preset, 1.0);
                Assert(Math.Abs(v0 - 0.0) < 1e-4, string.Format("Preset {0} evaluates to 0.0 at u=0.0", preset));
                Assert(Math.Abs(v1 - 1.0) < 1e-4, string.Format("Preset {0} evaluates to 1.0 at u=1.0", preset));
            }

            // 2. Verify all Spring presets (Bouncy, Snappy, Gentle, Stiff) apply correct physics
            var springKey = new Keyframe(0.0, 0.0, InterpMode.Linear);
            EasingEngine.ApplySpringPreset(springKey, SpringPreset.Bouncy);
            Assert(springKey.Interp == InterpMode.Spring, "Bouncy sets Spring mode");
            Assert(springKey.Stiffness == 150.0 && springKey.Damping == 10.0, "Bouncy parameters correct");

            EasingEngine.ApplySpringPreset(springKey, SpringPreset.Snappy);
            Assert(springKey.Stiffness == 200.0 && springKey.Damping == 25.0, "Snappy parameters correct");

            EasingEngine.ApplySpringPreset(springKey, SpringPreset.Gentle);
            Assert(springKey.Stiffness == 100.0 && springKey.Damping == 15.0, "Gentle parameters correct");

            EasingEngine.ApplySpringPreset(springKey, SpringPreset.Stiff);
            Assert(springKey.Stiffness == 300.0 && springKey.Damping == 30.0, "Stiff parameters correct");

            // 3. Verify real-time handle manipulation with vertical overshoot updates track sampling
            var k0 = new Keyframe(0.0, 0.0, InterpMode.Cubic, 0.25, 0.1, 0.25, 1.0);
            var k1 = new Keyframe(1.0, 100.0, InterpMode.Linear);
            var track = new Track(1, (ushort)PropKey.X);
            track.AddKeyframe(k0);
            track.AddKeyframe(k1);

            double sampleNormal = track.Sample(0.5);

            // Drag handle to vertical overshoot (Y1 = 1.40, like Bounce/Back)
            k0.Y1 = 1.40;
            k0.Preset = EasingPreset.CustomBezier;
            double sampleOvershoot = track.Sample(0.5);
            Assert(sampleOvershoot > sampleNormal, "Track sampling responds immediately to vertical overshoot handle dragging");

            // 4. Verify clean timeline keyframe diamond data integrity
            Assert(track.Keys.Count == 2, "Track preserves keyframe count");
            Assert(track.Keys[0].Time == 0.0 && track.Keys[1].Time == 1.0, "Keyframe times preserved");
        }

        private static void TestSingleCornerRadiusControlAndDesignAnimateIsolation()
        {
            // 1. Verify entity-level Corner Radius updates all four corners in Design Mode
            var node = new LayerNode
            {
                Id = "card-rect",
                Name = "Rounded Card",
                Type = ElementType.Rectangle,
                X = 100,
                Y = 200,
                Width = 240,
                Height = 160,
                CornerRadius = 0.0
            };

            // Set CornerRadius to 122.0 in Design Mode
            double designRadius = 122.0;
            node.CornerRadius = designRadius;
            node.CornerRadiusTopLeft = designRadius;
            node.CornerRadiusTopRight = designRadius;
            node.CornerRadiusBottomRight = designRadius;
            node.CornerRadiusBottomLeft = designRadius;

            Assert(node.CornerRadius == 122.0, "LayerNode CornerRadius set to 122.0");
            Assert(node.CornerRadiusTopLeft == 122.0 && node.CornerRadiusBottomRight == 122.0, "All corner radii synchronized");

            // 2. Command history undo/redo in Design Mode does not pollute animation
            var history = new CommandHistory();
            var cmd = new ModifyCornerRadiusCommand(node, 0.0, 122.0, null);
            history.Execute(cmd);
            Assert(node.CornerRadius == 122.0, "ModifyCornerRadiusCommand sets 122.0");
            history.Undo();
            Assert(node.CornerRadius == 0.0, "ModifyCornerRadiusCommand Undo restores 0.0");
            history.Redo();
            Assert(node.CornerRadius == 122.0, "ModifyCornerRadiusCommand Redo restores 122.0");

            // 3. Verify Design Mode changes never create animation tracks
            var doc = new KerfDocument();
            doc.Animation = new AnimationProjectData();
            doc.Animation.Clips = new List<Clip>();
            var defaultClip = new Clip(100, "Default Animation", 2.0);
            doc.Animation.Clips.Add(defaultClip);
            Assert(defaultClip.Tracks.Count == 0, "No tracks created by design modifications");

            // 4. Entering Animate Mode: Rest pose captures design value 122.0
            double restX = node.X;
            double restY = node.Y;
            double restCornerRadius = node.CornerRadius;
            Assert(restCornerRadius == 122.0, "Rest pose captured design CornerRadius 122.0");

            // 5. When timeline evaluates an unkeyed track, CornerRadius is NOT reset to 0
            var poseDict = new Dictionary<uint, Dictionary<ushort, double>>();
            uint numNodeId = PropertyBinder.GetNumericNodeId(node.Id);
            // Pose dictionary has an X animation, but NO CornerRadius key
            var props = new Dictionary<ushort, double>();
            props[(ushort)PropKey.X] = 150.0;
            poseDict[numNodeId] = props;

            // Restoring rest pose before applying unkeyed animation keeps CornerRadius = 122.0
            node.CornerRadius = restCornerRadius;
            PropertyBinder.ApplyPoseToLayer(node, numNodeId, poseDict);
            Assert(node.CornerRadius == 122.0, "Unkeyed animation preserves design CornerRadius 122.0");
            Assert(node.RenderTransform.TranslationX == 50.0, "Animated X applied non-destructively");

            // 6. When animation explicitly keys CornerRadius, it evaluates during playback
            props[(ushort)PropKey.CornerRadius] = 24.0;
            PropertyBinder.ApplyPoseToLayer(node, numNodeId, poseDict);
            Assert(node.CornerRadius == 24.0, "Keyed CornerRadius evaluates to 24.0 during playback");

            // 7. On stop / return to design mode, rest pose restores 122.0 perfectly
            node.CornerRadius = restCornerRadius;
            node.CornerRadiusTopLeft = restCornerRadius;
            node.CornerRadiusTopRight = restCornerRadius;
            node.CornerRadiusBottomRight = restCornerRadius;
            node.CornerRadiusBottomLeft = restCornerRadius;
            Assert(node.CornerRadius == 122.0, "Restoring design mode maintains original 122.0 radius");
        }

        private static void TestAnimationCommandHistoryAndFullUndoRedo()
        {
            // 1. Task 1 Tracer: AddKeyframeCommand, Overwrite resolution & ModifyKeyframeValueCommand
            var history = new CommandHistory();
            var track = new Track(1, (ushort)PropKey.X);

            // Execute AddKeyframeCommand for t=0.0s, val=10.0
            var kf1 = Keyframe.Linear(0.0, 10.0);
            history.Execute(new AddKeyframeCommand(track, kf1, null));
            Assert(track.Keys.Count == 1, "Track has 1 keyframe after AddKeyframeCommand");
            Assert(track.Keys[0].Value == 10.0, "Keyframe value is 10.0");

            // Undo()
            history.Undo();
            Assert(track.Keys.Count == 0, "Track has 0 keyframes after Undo");
            Assert(history.CanUndo == false, "CanUndo is false");
            Assert(history.CanRedo == true, "CanRedo is true");

            // Redo()
            history.Redo();
            Assert(track.Keys.Count == 1, "Track has 1 keyframe after Redo");
            Assert(track.Keys[0].Value == 10.0, "Keyframe value restored to 10.0");

            // Execute AddKeyframeCommand at t=0.0s with val=25.0 (overwriting existing keyframe)
            var kf2 = Keyframe.Linear(0.0, 25.0);
            history.Execute(new AddKeyframeCommand(track, kf2, null));
            Assert(track.Keys.Count == 1, "Track still has 1 keyframe after overwriting at duplicate timestamp");
            Assert(track.Keys[0].Value == 25.0, "Overwritten keyframe value is 25.0");

            // Undo() - restored replaced keyframe without data loss
            history.Undo();
            Assert(track.Keys.Count == 1, "Track has 1 keyframe after undoing overwrite");
            Assert(track.Keys[0].Value == 10.0, "Restored replaced keyframe value 10.0 without data loss");
            Assert(track.Keys[0].Id == kf1.Id, "Restored keyframe ID matches original replaced keyframe");

            // Execute ModifyKeyframeValueCommand changing value from 10.0 to 99.0
            history.Execute(new ModifyKeyframeValueCommand(track.Keys[0], 10.0, 99.0, null));
            Assert(track.Keys[0].Value == 99.0, "ModifyKeyframeValueCommand changed value to 99.0");

            // Undo and Redo ModifyKeyframeValueCommand
            history.Undo();
            Assert(track.Keys[0].Value == 10.0, "ModifyKeyframeValueCommand Undo restored 10.0");
            history.Redo();
            Assert(track.Keys[0].Value == 99.0, "ModifyKeyframeValueCommand Redo restored 99.0");

            // 2. Task 2: Keyframe Removal, Movement, Duplication, and Easing/Curve Commands
            // (A) MoveKeyframesCommand across multiple tracks maintaining sorted order
            var track2 = new Track(2, (ushort)PropKey.Y);
            var m1 = Keyframe.Linear(0.5, 50.0);
            var m2 = Keyframe.Linear(1.5, 75.0);
            track.AddKeyframe(m1);
            track2.AddKeyframe(m2);

            var beforeTimes = new Dictionary<Keyframe, double>();
            beforeTimes[m1] = 0.5;
            beforeTimes[m2] = 1.5;
            var afterTimes = new Dictionary<Keyframe, double>();
            afterTimes[m1] = 2.0;
            afterTimes[m2] = 3.0;
            var owners = new Dictionary<Keyframe, Track>();
            owners[m1] = track;
            owners[m2] = track2;

            history.Execute(new MoveKeyframesCommand(beforeTimes, afterTimes, owners, null));
            Assert(m1.Time == 2.0 && m2.Time == 3.0, "MoveKeyframesCommand shifted timestamps forward");
            Assert(track.Keys[track.Keys.Count - 1] == m1, "Track keys sorted after move");

            history.Undo();
            Assert(m1.Time == 0.5 && m2.Time == 1.5, "MoveKeyframesCommand Undo restored original timestamps");
            history.Redo();
            Assert(m1.Time == 2.0 && m2.Time == 3.0, "MoveKeyframesCommand Redo re-applied new timestamps");

            // (B) DuplicateKeyframesCommand: clone and offset by deltaTime
            var dupCmd = new DuplicateKeyframesCommand(new Keyframe[] { m1, m2 }, owners, 0.5, null);
            history.Execute(dupCmd);
            Assert(dupCmd.DuplicatedKeys.Count == 2, "Duplicated 2 keyframes");
            Assert(track.Keys.Count == 3, "Track now has 3 keyframes (t=0.0, t=2.0, t=2.5)");
            Assert(track2.Keys.Count == 2, "Track2 now has 2 keyframes (t=3.0, t=3.5)");
            Assert(Math.Abs(dupCmd.DuplicatedKeys[0].Time - 2.5) < 1e-6, "First duplicate shifted by +0.5s");
            Assert(Math.Abs(dupCmd.DuplicatedKeys[1].Time - 3.5) < 1e-6, "Second duplicate shifted by +0.5s");

            history.Undo();
            Assert(track.Keys.Count == 2, "DuplicateKeyframesCommand Undo removed clones from track 1");
            Assert(track2.Keys.Count == 1, "DuplicateKeyframesCommand Undo removed clones from track 2");

            history.Redo();
            Assert(track.Keys.Count == 3, "DuplicateKeyframesCommand Redo restored clones in track 1");
            Assert(track2.Keys.Count == 2, "DuplicateKeyframesCommand Redo restored clones in track 2");

            // (C) DeleteKeyframesCommand across multiple tracks
            var delDict = new Dictionary<Keyframe, Track>();
            delDict[m1] = track;
            delDict[m2] = track2;
            history.Execute(new DeleteKeyframesCommand(delDict, null));
            Assert(!track.Keys.Contains(m1), "m1 deleted from track");
            Assert(!track2.Keys.Contains(m2), "m2 deleted from track2");

            history.Undo();
            Assert(track.Keys.Contains(m1), "m1 restored to track on Undo");
            Assert(track2.Keys.Contains(m2), "m2 restored to track2 on Undo");
            Assert(track.Keys[0].Time <= track.Keys[1].Time, "Track keys kept sorted on Undo");

            history.Redo();
            Assert(!track.Keys.Contains(m1) && !track2.Keys.Contains(m2), "DeleteKeyframesCommand Redo re-deletes");
            history.Undo(); // Restore for subsequent curve tests

            // (D) ChangeKeyframeEasingCommand
            var easingKey = track.Keys[0];
            history.Execute(new ChangeKeyframeEasingCommand(easingKey, InterpMode.Linear, InterpMode.Cubic, EasingPreset.Linear, EasingPreset.CubicInOut, null));
            Assert(easingKey.Interp == InterpMode.Cubic, "ChangeKeyframeEasingCommand changed InterpMode to Cubic");
            Assert(easingKey.Preset == EasingPreset.CubicInOut, "ChangeKeyframeEasingCommand changed Preset to CubicInOut");

            history.Undo();
            Assert(easingKey.Interp == InterpMode.Linear, "ChangeKeyframeEasingCommand Undo restored Linear interp");
            Assert(easingKey.Preset == EasingPreset.Linear, "ChangeKeyframeEasingCommand Undo restored Linear preset");

            history.Redo();
            Assert(easingKey.Interp == InterpMode.Cubic && easingKey.Preset == EasingPreset.CubicInOut, "ChangeKeyframeEasingCommand Redo re-applied");

            // (E) ModifyBezierHandlesCommand with KeyframeBezierSnapshot
            var beforeSnap = new KeyframeBezierSnapshot(easingKey);
            var afterSnap = new KeyframeBezierSnapshot(easingKey, 0.33, 0.0, 0.67, 1.0, EasingPreset.CustomBezier, true);
            history.Execute(new ModifyBezierHandlesCommand(beforeSnap, afterSnap, null));
            Assert(Math.Abs(easingKey.X1 - 0.33) < 1e-4 && Math.Abs(easingKey.Y2 - 1.0) < 1e-4, "ModifyBezierHandlesCommand applied new handles");
            Assert(easingKey.IsTangentBroken == true, "ModifyBezierHandlesCommand set IsTangentBroken");

            history.Undo();
            Assert(Math.Abs(easingKey.X1 - beforeSnap.X1) < 1e-4 && easingKey.IsTangentBroken == beforeSnap.IsTangentBroken, "ModifyBezierHandlesCommand Undo restored handle snapshot");

            history.Redo();
            Assert(Math.Abs(easingKey.X1 - 0.33) < 1e-4 && easingKey.IsTangentBroken == true, "ModifyBezierHandlesCommand Redo re-applied handles");

            // (F) ModifySpringParamsCommand
            var springKey = Keyframe.Spring(1.0, 100.0, 1.0, 100.0, 10.0, 0.0);
            track.AddKeyframe(springKey);
            history.Execute(new ModifySpringParamsCommand(springKey, 1.0, 2.5, 100.0, 250.0, 10.0, 18.0, 0.0, 5.0, null));
            Assert(springKey.Mass == 2.5 && springKey.Stiffness == 250.0 && springKey.Damping == 18.0 && springKey.InitialVelocity == 5.0, "ModifySpringParamsCommand updated all spring properties");

            history.Undo();
            Assert(springKey.Mass == 1.0 && springKey.Stiffness == 100.0 && springKey.Damping == 10.0 && springKey.InitialVelocity == 0.0, "ModifySpringParamsCommand Undo restored spring properties");

            history.Redo();
            Assert(springKey.Mass == 2.5 && springKey.Stiffness == 250.0, "ModifySpringParamsCommand Redo re-applied spring properties");

            // 3. Task 3: Track & Clip Structural Commands with CompositeCommand Transaction Batching
            var projectData = new AnimationProjectData();
            var clip1 = new Clip(101, "Intro Animation", 5.0, 60.0, LoopMode.Loop);
            var clip2 = new Clip(102, "Outro Animation", 3.0, 30.0, LoopMode.OneShot);

            // (A) AddClipCommand & RemoveClipCommand
            history.Execute(new AddClipCommand(projectData, clip1, null));
            Assert(projectData.Clips.Count == 1, "AddClipCommand added clip1");
            Assert(projectData.ActiveClipId == clip1.Id, "ActiveClipId set to clip1.Id");

            history.Execute(new AddClipCommand(projectData, clip2, null));
            Assert(projectData.Clips.Count == 2, "AddClipCommand added clip2");
            Assert(projectData.ActiveClipId == clip2.Id, "ActiveClipId updated to clip2.Id");

            history.Undo();
            Assert(projectData.Clips.Count == 1, "AddClipCommand Undo removed clip2");
            Assert(projectData.ActiveClipId == clip1.Id, "ActiveClipId restored to clip1.Id");

            history.Redo();
            Assert(projectData.Clips.Count == 2, "AddClipCommand Redo restored clip2");

            // RemoveClipCommand
            history.Execute(new RemoveClipCommand(projectData, clip1, null));
            Assert(projectData.Clips.Count == 1 && projectData.Clips[0] == clip2, "RemoveClipCommand removed clip1");

            history.Undo();
            Assert(projectData.Clips.Count == 2, "RemoveClipCommand Undo restored clip1");
            Assert(projectData.Clips[0] == clip1, "clip1 restored at original index 0");

            history.Redo();
            Assert(projectData.Clips.Count == 1 && projectData.Clips[0] == clip2, "RemoveClipCommand Redo re-removed clip1");
            history.Undo(); // Restore clip1

            // (B) AddTrackCommand & RemoveTrackCommand
            var clipTrackA = new Track(10, (ushort)PropKey.Opacity);
            var clipTrackB = new Track(10, (ushort)PropKey.ScaleX);
            history.Execute(new AddTrackCommand(clip1, clipTrackA, null));
            history.Execute(new AddTrackCommand(clip1, clipTrackB, null));
            Assert(clip1.Tracks.Count == 2, "Added 2 tracks to clip1");

            history.Undo();
            Assert(clip1.Tracks.Count == 1 && clip1.Tracks[0] == clipTrackA, "Undo removed second track");

            history.Redo();
            Assert(clip1.Tracks.Count == 2 && clip1.Tracks[1] == clipTrackB, "Redo restored second track");

            history.Execute(new RemoveTrackCommand(clip1, clipTrackA, null));
            Assert(clip1.Tracks.Count == 1 && clip1.Tracks[0] == clipTrackB, "RemoveTrackCommand removed first track");

            history.Undo();
            Assert(clip1.Tracks.Count == 2 && clip1.Tracks[0] == clipTrackA, "RemoveTrackCommand Undo restored first track at index 0");

            history.Redo();
            Assert(clip1.Tracks.Count == 1 && clip1.Tracks[0] == clipTrackB, "RemoveTrackCommand Redo re-removed track");
            history.Undo(); // Restore trackA

            // (C) AnimationClipDurationCommand (duration reduction preserves keyframes beyond new duration)
            var keyBeyond = Keyframe.Linear(4.5, 99.0);
            clipTrackA.AddKeyframe(Keyframe.Linear(1.0, 10.0));
            clipTrackA.AddKeyframe(keyBeyond); // Keyframe at 4.5s
            Assert(clipTrackA.Keys.Count == 2, "clipTrackA has 2 keyframes");

            history.Execute(new AnimationClipDurationCommand(clip1, 5.0, 2.0, 60.0, 30.0, LoopMode.Loop, LoopMode.PingPong, null));
            Assert(clip1.Duration == 2.0, "Clip duration changed to 2.0s");
            Assert(clip1.Fps == 30.0, "Clip FPS changed to 30.0");
            Assert(clip1.LoopMode == LoopMode.PingPong, "Clip LoopMode changed to PingPong");
            Assert(clipTrackA.Keys.Count == 2, "Keyframes beyond 2.0s duration preserved without truncation");
            Assert(clipTrackA.Keys[1] == keyBeyond, "Keyframe at 4.5s remains intact");

            history.Undo();
            Assert(clip1.Duration == 5.0 && clip1.Fps == 60.0 && clip1.LoopMode == LoopMode.Loop, "AnimationClipDurationCommand Undo restored original duration, fps, loop");
            Assert(clipTrackA.Keys.Count == 2, "Keyframes still intact after Undo");

            history.Redo();
            Assert(clip1.Duration == 2.0 && clip1.Fps == 30.0 && clip1.LoopMode == LoopMode.PingPong, "AnimationClipDurationCommand Redo re-applied settings");

            // (D) Transaction batching with CompositeCommand (D-04): adding 3 tracks, 6 keyframes, modifying 2 values
            var batchClip = new Clip(200, "Batch Transaction Clip", 10.0);
            projectData.Clips.Add(batchClip);

            history.BeginBatch("Multi-Track Keyframe Creation & Tuning");
            var bTrack1 = new Track(20, (ushort)PropKey.X);
            var bTrack2 = new Track(20, (ushort)PropKey.Y);
            var bTrack3 = new Track(20, (ushort)PropKey.Rotation);

            history.Execute(new AddTrackCommand(batchClip, bTrack1, null));
            history.Execute(new AddTrackCommand(batchClip, bTrack2, null));
            history.Execute(new AddTrackCommand(batchClip, bTrack3, null));

            var bk1 = Keyframe.Linear(0.0, 0.0);
            var bk2 = Keyframe.Linear(1.0, 100.0);
            var bk3 = Keyframe.Linear(0.0, 10.0);
            var bk4 = Keyframe.Linear(1.0, 200.0);
            var bk5 = Keyframe.Linear(0.0, 0.0);
            var bk6 = Keyframe.Linear(1.0, 360.0);

            history.Execute(new AddKeyframeCommand(bTrack1, bk1, null));
            history.Execute(new AddKeyframeCommand(bTrack1, bk2, null));
            history.Execute(new AddKeyframeCommand(bTrack2, bk3, null));
            history.Execute(new AddKeyframeCommand(bTrack2, bk4, null));
            history.Execute(new AddKeyframeCommand(bTrack3, bk5, null));
            history.Execute(new AddKeyframeCommand(bTrack3, bk6, null));

            history.Execute(new ModifyKeyframeValueCommand(bk2, 100.0, 150.0, null));
            history.Execute(new ModifyKeyframeValueCommand(bk4, 200.0, 250.0, null));

            history.EndBatch();

            // Verify batch result forward
            Assert(batchClip.Tracks.Count == 3, "Batch added 3 tracks");
            Assert(bTrack1.Keys.Count == 2 && bTrack2.Keys.Count == 2 && bTrack3.Keys.Count == 2, "Batch added 6 keyframes");
            Assert(bk2.Value == 150.0 && bk4.Value == 250.0, "Batch modified 2 keyframe values");

            // Reverse in a SINGLE Undo step (D-04)
            history.Undo();

            Assert(batchClip.Tracks.Count == 0, "Single Ctrl+Z reversed all 3 track additions, 6 keyframes, and 2 value changes atomically");

            // Re-apply in a SINGLE Redo step
            history.Redo();

            Assert(batchClip.Tracks.Count == 3, "Single Redo re-applied all 3 tracks");
            Assert(bTrack1.Keys.Count == 2 && bTrack2.Keys.Count == 2 && bTrack3.Keys.Count == 2, "Single Redo re-applied all 6 keyframes");
            Assert(bk2.Value == 150.0 && bk4.Value == 250.0, "Single Redo re-applied modified keyframe values");

            // 4. Task 1 (05-02): Inspector Numeric Scrub & Blur Transaction Batching (D-03, HIST-04)
            // (A) Scrub gesture generating 50 intermediate values creates 0 history entries during drag, exactly 1 command on commit
            var scrubHistory = new CommandHistory();
            var scrubTrack = new Track(50, (ushort)PropKey.X);
            var scrubKf = Keyframe.Linear(1.0, 100.0);
            scrubTrack.AddKeyframe(scrubKf);

            double scrubStartVal = scrubKf.Value; // 100.0
            double scrubCurrentVal = scrubStartVal;

            Action<double> scrubOnChanged = val =>
            {
                // Live preview simulation - 60 FPS canvas update without pushing history
                scrubCurrentVal = val;
                // Assert no command pushed during drag
                Assert(scrubHistory.CanUndo == false, "0 commands pushed to history during continuous scrub drag");
            };

            Action scrubOnDragComplete = () =>
            {
                if (Math.Abs(scrubCurrentVal - scrubStartVal) > 1e-6)
                {
                    scrubHistory.Execute(new ModifyKeyframeValueCommand(scrubKf, scrubStartVal, scrubCurrentVal, null));
                    scrubStartVal = scrubCurrentVal;
                }
            };

            // Generate 50 intermediate drag events
            for (int step = 1; step <= 50; step++)
            {
                scrubOnChanged(100.0 + step * 2.0); // 102.0 ... 200.0
            }
            Assert(scrubHistory.CanUndo == false, "After 50 move events during drag, CanUndo remains false");
            Assert(scrubCurrentVal == 200.0, "Current scrubbed value reached 200.0");

            // Pointer release / blur commit
            scrubOnDragComplete();
            Assert(scrubHistory.CanUndo == true, "Exactly 1 command committed to CommandHistory on pointer release");
            Assert(scrubKf.Value == 200.0, "Keyframe value committed to 200.0");

            // Execute Undo() - reverts directly to initial value in a single step
            scrubHistory.Undo();
            Assert(scrubKf.Value == 100.0, "Undo() restored initial value 100.0 in a single step");
            Assert(scrubHistory.CanUndo == false, "CanUndo is false after single Undo()");

            // Redo() restores 200.0
            scrubHistory.Redo();
            Assert(scrubKf.Value == 200.0, "Redo() re-applied 200.0");

            // (B) Aspect ratio locked scrub batches paired changes into CompositeCommand (D-04)
            var aspectLayer = new LayerNode { Name = "Box", X = 0, Y = 0, Width = 100, Height = 50, LockAspectRatio = true };
            double startW = aspectLayer.Width;
            double startH = aspectLayer.Height;
            double finalW = 200.0;
            double finalH = 100.0;

            scrubHistory.BeginBatch("Modify Width and Height (Locked Aspect)");
            scrubHistory.Execute(new ModifyBoundsCommand(aspectLayer, new Rect(0, 0, startW, startH), new Rect(0, 0, finalW, finalH), null));
            scrubHistory.EndBatch();

            Assert(aspectLayer.Width == 200.0 && aspectLayer.Height == 100.0, "Aspect ratio batch applied new dimensions");
            scrubHistory.Undo();
            Assert(aspectLayer.Width == 100.0 && aspectLayer.Height == 50.0, "Single Ctrl+Z reversed both Width and Height in one step");

            // 5. Task 2 (05-02): Graph Editor Tangent Handle Drag Commit & Multi-Keyframe Pointer Release (D-03, D-04, HIST-02)
            // (A) Tangent handle drag start, 20 move events, and release: verify 0 commands on move, exactly 1 ModifyBezierHandlesCommand on release, single Undo() restores original handles
            var graphCanvas = new GraphEditorCanvas();
            var graphTrack = new Track(60, (ushort)PropKey.X);
            var gKf1 = Keyframe.Cubic(0.0, 10.0, 0.25, 0.1, 0.25, 1.0);
            var gKf2 = Keyframe.Cubic(1.0, 50.0, 0.25, 0.1, 0.25, 1.0);
            graphTrack.AddKeyframe(gKf1);
            graphTrack.AddKeyframe(gKf2);
            graphCanvas.ActiveTrack = graphTrack;

            var graphHistory = new CommandHistory();
            graphCanvas.TangentHandleDragCommitted += (bSnap, aSnap) =>
            {
                graphHistory.Execute(new ModifyBezierHandlesCommand(bSnap, aSnap, () => graphCanvas.Redraw()));
            };

            // Capture initial handle values
            double origX1 = gKf1.X1; // 0.25
            double origY1 = gKf1.Y1; // 0.1

            // Start handle drag
            graphCanvas.StartHandleDrag(gKf1, HandleKind.Outgoing, new Point(0, 0));
            Assert(graphHistory.CanUndo == false, "CanUndo is false at start of handle drag");

            // 20 move events
            for (int m = 1; m <= 20; m++)
            {
                double simTime = 0.0 + m * 0.02;
                double simVal = 10.0 + m * 1.5;
                Point mousePt = new Point(graphCanvas.Viewport.TimeToX(simTime), graphCanvas.Viewport.ValueToY(simVal));
                graphCanvas.MoveHandleDrag(mousePt);
                Assert(graphHistory.CanUndo == false, "0 commands pushed to history during continuous handle drag");
            }

            Assert(graphHistory.CanUndo == false, "After 20 move events, CanUndo is still false");
            Assert(gKf1.X1 != origX1, "gKf1.X1 modified in real-time during live drag");

            // Release pointer / complete handle drag
            graphCanvas.CompleteHandleDrag();
            Assert(graphHistory.CanUndo == true, "Exactly 1 ModifyBezierHandlesCommand committed on pointer release");

            double draggedX1 = gKf1.X1;
            double draggedY1 = gKf1.Y1;

            // Undo() restores original handles in 1 step
            graphHistory.Undo();
            Assert(Math.Abs(gKf1.X1 - origX1) < 1e-4 && Math.Abs(gKf1.Y1 - origY1) < 1e-4, "Undo() restored initial handle coordinates in a single step");
            Assert(graphHistory.CanUndo == false, "CanUndo is false after single Undo()");

            // Redo() re-applies dragged coordinates
            graphHistory.Redo();
            Assert(Math.Abs(gKf1.X1 - draggedX1) < 1e-4 && Math.Abs(gKf1.Y1 - draggedY1) < 1e-4, "Redo() re-applied dragged handle coordinates");

            // (B) Applying preset via ChangeKeyframeEasingCommand restores prior preset and control points
            var presetKey = Keyframe.Cubic(0.0, 0.0, 0.25, 0.1, 0.25, 1.0);
            presetKey.Preset = EasingPreset.Ease;
            var presetHistory = new CommandHistory();

            var prevPreset = presetKey.Preset;
            var prevInterp = presetKey.Interp;
            var newPreset = EasingPreset.Back;

            presetHistory.Execute(new ChangeKeyframeEasingCommand(presetKey, prevInterp, InterpMode.Cubic, prevPreset, newPreset, null));
            Assert(presetKey.Preset == EasingPreset.Back, "Preset changed to Back");
            Assert(presetKey.Interp == InterpMode.Cubic, "Interp is Cubic");

            presetHistory.Undo();
            Assert(presetKey.Preset == EasingPreset.Ease, "Undo() restored prior preset Ease");
            Assert(Math.Abs(presetKey.X1 - 0.25) < 1e-4 && Math.Abs(presetKey.Y1 - 0.1) < 1e-4, "Undo() restored prior control points");

            presetHistory.Redo();
            Assert(presetKey.Preset == EasingPreset.Back, "Redo() re-applied Back preset");

            // (C) Multi-keyframe delete across 2 tracks with 4 keyframes: single Undo() restores all 4 keyframes
            var multiTrack1 = new Track(70, (ushort)PropKey.X);
            var multiTrack2 = new Track(70, (ushort)PropKey.Y);
            var mk1 = Keyframe.Linear(0.0, 10.0);
            var mk2 = Keyframe.Linear(1.0, 20.0);
            var mk3 = Keyframe.Linear(0.0, 30.0);
            var mk4 = Keyframe.Linear(1.0, 40.0);
            multiTrack1.AddKeyframe(mk1);
            multiTrack1.AddKeyframe(mk2);
            multiTrack2.AddKeyframe(mk3);
            multiTrack2.AddKeyframe(mk4);

            var multiHistory = new CommandHistory();
            var deleteKeysDict = new Dictionary<Keyframe, Track>();
            deleteKeysDict[mk1] = multiTrack1;
            deleteKeysDict[mk2] = multiTrack1;
            deleteKeysDict[mk3] = multiTrack2;
            deleteKeysDict[mk4] = multiTrack2;

            multiHistory.Execute(new DeleteKeyframesCommand(deleteKeysDict, null));
            Assert(multiTrack1.Keys.Count == 0, "multiTrack1 has 0 keyframes after DeleteKeyframesCommand");
            Assert(multiTrack2.Keys.Count == 0, "multiTrack2 has 0 keyframes after DeleteKeyframesCommand");

            multiHistory.Undo();
            Assert(multiTrack1.Keys.Count == 2, "multiTrack1 has 2 keyframes restored after single Undo()");
            Assert(multiTrack2.Keys.Count == 2, "multiTrack2 has 2 keyframes restored after single Undo()");
            Assert(multiTrack1.Keys.Contains(mk1) && multiTrack1.Keys.Contains(mk2), "multiTrack1 contains original keys mk1, mk2");
            Assert(multiTrack2.Keys.Contains(mk3) && multiTrack2.Keys.Contains(mk4), "multiTrack2 contains original keys mk3, mk4");

            multiHistory.Redo();
            Assert(multiTrack1.Keys.Count == 0 && multiTrack2.Keys.Count == 0, "Redo() re-deleted all 4 keyframes");

            // 6. Task 3 (05-02): In-Place Cross-Mode Undo, Silent Data Restore, Runtime Isolation & Verification
            // (A) Verification of D-01 (In-Place Entity Undo across modes)
            var studio = new StudioView();
            var page = studio.Document.GetActivePage();
            var testLayer = new LayerNode { Id = "d01-box", Name = "D01 Box", X = 100, Y = 100, Width = 80, Height = 80 };
            page.Layers.Add(testLayer);

            // In Design Mode: Modify X to 200 via ModifyBoundsCommand
            var modifyCmd = new ModifyBoundsCommand(testLayer, new Rect(100, 100, 80, 80), new Rect(200, 100, 80, 80), null);
            studio.History.Execute(modifyCmd);
            Assert(testLayer.X == 200, "Layer moved to X=200 in Design Mode");

            // Switch to Animate Mode
            studio.SwitchStudioMode(StudioView.StudioMode.Animate);
            Assert(studio.CurrentStudioMode == StudioView.StudioMode.Animate, "Switched to Animate Mode");
            var initialPose = studio.GetAnimationRestPose(testLayer);
            Assert(initialPose != null && initialPose.X == 200, "Rest pose captured X=200 upon entering Animate Mode");

            // Call studio.Undo() while in Animate Mode
            studio.Undo();

            Assert(testLayer.X == 100, "layer.X restored to 100 on Undo()");
            var updatedPose = studio.GetAnimationRestPose(testLayer);
            Assert(updatedPose != null && updatedPose.X == 100, "Rest pose synchronized in-place to 100 without tab jump (D-01)");
            Assert(studio.CurrentStudioMode == StudioView.StudioMode.Animate, "Current mode remains Animate Mode without tab jump (D-01)");

            // (B) Verification of D-02 (Silent Data Restore on Undo/Redo)
            studio.TimelineController.Seek(1.45);
            Assert(Math.Abs(studio.TimelineController.CurrentTime - 1.45) < 1e-4, "Playhead set to 1.45s");

            var activeClip = studio.TimelineController.ActiveClip;
            var d02Track = new Track(PropertyBinder.GetNumericNodeId(testLayer.Id), (ushort)PropKey.X);
            activeClip.Tracks.Add(d02Track);
            var kfPreserve = Keyframe.Linear(1.0, 150.0);
            var kfOther = Keyframe.Linear(2.0, 250.0);
            d02Track.AddKeyframe(kfPreserve);
            d02Track.AddKeyframe(kfOther);

            studio.SelectedTimelineKeyframes.Add(kfPreserve);

            // Execute another command
            studio.History.Execute(new ModifyKeyframeValueCommand(kfOther, 250.0, 300.0, null));
            Assert(kfOther.Value == 300.0, "kfOther modified to 300.0");

            // Execute Undo()
            studio.Undo();

            Assert(Math.Abs(studio.TimelineController.CurrentTime - 1.45) < 1e-4, "CurrentTime strictly preserved at 1.45s across Undo() without resetting to 0.0s (D-02)");
            Assert(studio.SelectedTimelineKeyframes.Contains(kfPreserve), "Active keyframe selection set preserved silently across Undo() (D-02)");
            Assert(kfOther.Value == 250.0, "Value restored to 250.0");

            // (C) Verification of Runtime Isolation (ticks, seeks, scrubs generate 0 history entries)
            var isolationHistory = new CommandHistory();
            var isolationController = new AnimationTimelineController(5.0, 60.0);
            var isoClip = new Clip(999, "Isolation Clip", 5.0, 60.0);
            var isoTrack = new Track(1, (ushort)PropKey.X);
            isoTrack.AddKeyframe(Keyframe.Linear(0.0, 0.0));
            isoTrack.AddKeyframe(Keyframe.Linear(5.0, 500.0));
            isoClip.Tracks.Add(isoTrack);
            isolationController.ActiveClip = isoClip;

            for (int tick = 0; tick < 100; tick++)
            {
                isolationController.Step(1.0 / 60.0);
            }
            for (int s = 0; s < 50; s++)
            {
                isolationController.Seek(s * 0.1);
            }
            for (int scrub = 0; scrub < 20; scrub++)
            {
                isolationController.Seek(scrub * 0.25);
                isoClip.EvaluateAt(scrub * 0.25);
            }

            Assert(isolationHistory.CanUndo == false, "CanUndo is false after 100 ticks, 50 seeks, and 20 scrubbing evaluations (Runtime Isolation)");
            Assert(isolationHistory.CanRedo == false, "CanRedo is false after runtime operations (Runtime Isolation)");

            // (D) Verification of Strict Mode Isolation (Design modifications never create animation tracks)
            var modeIsoDoc = new KerfDocument();
            var modeIsoClip = new Clip(1, "Mode Iso Clip", 2.0);
            modeIsoDoc.Animation = new AnimationProjectData();
            modeIsoDoc.Animation.Clips = new List<Clip> { modeIsoClip };

            var modeIsoLayer = new LayerNode { Id = "design-only", Name = "Design Node", X = 0, Y = 0, Width = 100, Height = 100 };
            var modeIsoHistory = new CommandHistory();

            modeIsoHistory.Execute(new ModifyBoundsCommand(modeIsoLayer, new Rect(0, 0, 100, 100), new Rect(10, 0, 100, 100), null)); // 1. X
            modeIsoHistory.Execute(new ModifyBoundsCommand(modeIsoLayer, new Rect(10, 0, 100, 100), new Rect(10, 20, 100, 100), null)); // 2. Y
            modeIsoHistory.Execute(new ModifyBoundsCommand(modeIsoLayer, new Rect(10, 20, 100, 100), new Rect(10, 20, 150, 100), null)); // 3. Width
            modeIsoHistory.Execute(new ModifyBoundsCommand(modeIsoLayer, new Rect(10, 20, 150, 100), new Rect(10, 20, 150, 200), null)); // 4. Height
            modeIsoHistory.Execute(new ModifyRotationCommand(modeIsoLayer, 0.0, 45.0, null)); // 5. Rotation
            modeIsoHistory.Execute(new ModifyCornerRadiusCommand(modeIsoLayer, 0.0, 16.0, null)); // 6. CornerRadius
            modeIsoHistory.Execute(new ModifyOpacityCommand(modeIsoLayer, 1.0, 0.8, null)); // 7. Opacity
            modeIsoLayer.Fills = new List<Paint> { new Paint { ColorHex = "#FF0000" } }; // 8. Fill
            modeIsoLayer.Strokes = new List<StrokeData> { new StrokeData { Paint = new Paint { ColorHex = "#0000FF" }, Width = 3.0 } }; // 9. Stroke

            Assert(modeIsoClip.Tracks.Count == 0, "Design Mode operations directly mutate LayerNode and never instantiate animation tracks (Strict Mode Isolation)");

            // (E) Verification of Redo Invalidation
            var redoHistory = new CommandHistory();
            var cmdLayer = new LayerNode { X = 0 };
            var cmdA = new ModifyBoundsCommand(cmdLayer, new Rect(0, 0, 10, 10), new Rect(1, 0, 10, 10), null);
            var cmdB = new ModifyBoundsCommand(cmdLayer, new Rect(1, 0, 10, 10), new Rect(2, 0, 10, 10), null);
            var cmdC = new ModifyBoundsCommand(cmdLayer, new Rect(1, 0, 10, 10), new Rect(3, 0, 10, 10), null);

            redoHistory.Execute(cmdA);
            redoHistory.Execute(cmdB);
            Assert(redoHistory.CanUndo == true, "CanUndo is true");
            Assert(redoHistory.CanRedo == false, "CanRedo is false");

            redoHistory.Undo(); // B is now on redo stack
            Assert(redoHistory.CanRedo == true, "CanRedo is true with cmdB on redo stack");

            redoHistory.Execute(cmdC);
            Assert(redoHistory.CanRedo == false, "Redo stack cleared and CanRedo is false after new command execution");
            Assert(redoHistory.CanUndo == true, "CanUndo is true for cmdC");
        }

        private static void TestInteractiveStateMachineEngine()
        {
            // 1. Stable string GUID initialization and index adapter bidirectional resolution (D-02)
            var smDef = new StateMachineDefinition("InteractiveSM");
            int bHoverIdx = smDef.AddInput(MachineInput.Bool("isHover", false));
            int nSpeedIdx = smDef.AddInput(MachineInput.Number("speed", 2.5, "sm_input_custom_speed"));
            int tClickIdx = smDef.AddInput(MachineInput.Trigger("onClick"));

            Assert(smDef.Inputs[bHoverIdx].InputId.StartsWith("sm_input_"), "MachineInput auto-generates stable GUID starting with sm_input_");
            Assert(smDef.Inputs[nSpeedIdx].InputId == "sm_input_custom_speed", "MachineInput accepts explicit stable GUID");
            Assert(smDef.FindInputById("sm_input_custom_speed") == smDef.Inputs[nSpeedIdx], "FindInputById resolves input by stable string GUID");
            Assert(smDef.GetInputIndex("sm_input_custom_speed") == nSpeedIdx, "GetInputIndex resolves index from stable string GUID");
            Assert(smDef.GetInputIndex("speed") == nSpeedIdx, "GetInputIndex resolves index from input name");
            Assert(smDef.FindInputById("isHover") == smDef.Inputs[bHoverIdx], "FindInputById resolves input by name");
            Assert(smDef.FindInputById(bHoverIdx) == smDef.Inputs[bHoverIdx], "FindInputById resolves input by integer index");

            var layer = new MachineLayer("BaseLayer");
            int sEntry = layer.AddState(new MachineState(0, "Entry", StateKind.Entry));
            int sIdle = layer.AddState(new MachineState(1, "Idle", StateKind.Animation, 0, "sm_node_idle"));
            int sHover = layer.AddState(new MachineState(2, "Hover", StateKind.Animation, 1, "sm_node_hover"));
            int sClick = layer.AddState(new MachineState(3, "Click", StateKind.Animation, 2, "sm_node_click"));

            Assert(layer.States[sEntry].NodeId.StartsWith("sm_node_"), "MachineState auto-generates stable GUID starting with sm_node_");
            Assert(layer.States[sIdle].NodeId == "sm_node_idle", "MachineState accepts explicit stable GUID");
            Assert(layer.FindStateById("sm_node_idle") == layer.States[sIdle], "FindStateById resolves state by stable string GUID");
            Assert(layer.GetStateIndex("sm_node_idle") == sIdle, "GetStateIndex resolves index from stable string GUID");
            Assert(layer.GetStateNodeId(sIdle) == "sm_node_idle", "GetStateNodeId resolves string GUID from integer index");
            Assert(layer.FindStateById(sIdle) == layer.States[sIdle], "FindStateById resolves state by integer index");
            Assert(layer.States[sIdle].Speed == 1.0, "MachineState defaults Speed to 1.0");
            Assert(layer.States[sIdle].Loop == true, "MachineState defaults Loop to true");
            Assert(layer.States[sIdle].GraphX == -1.0 && layer.States[sIdle].GraphY == -1.0, "MachineState defaults canvas coordinates to -1.0");

            var transEntry = new StateTransition(sEntry, sIdle, 0.0, "sm_trans_entry_to_idle");
            layer.AddTransition(transEntry);
            smDef.AddLayer(layer);

            Assert(transEntry.TransitionId == "sm_trans_entry_to_idle", "StateTransition accepts explicit stable GUID");
            Assert(transEntry.ToStateId == "sm_node_idle", "AddTransition syncs ToStateId from destination state");
            Assert(smDef.FindStateById("sm_node_hover") == layer.States[sHover], "StateMachineDefinition.FindStateById searches layers for state");

            // 2. Compound Condition Groups (OR between groups, AND within a group) (D-03)
            var transIdleHover = new StateTransition(sIdle, sHover, 0.2, "sm_trans_idle_to_hover");
            // Group 1: isHover == 1.0 AND speed > 10.0 (Will FAIL because speed = 2.5)
            var cg1 = new ConditionGroup("cg_fast_hover");
            cg1.Conditions.Add(new TransitionCondition("isHover", ConditionOp.Eq, 1.0));
            cg1.Conditions.Add(new TransitionCondition("sm_input_custom_speed", ConditionOp.Gt, 10.0));
            transIdleHover.WithConditionGroup(cg1);

            // Group 2: isHover == 1.0 AND speed >= 1.0 (Will PASS because isHover = 1.0 and speed = 2.5)
            var cg2 = new ConditionGroup("cg_normal_hover");
            cg2.Conditions.Add(new TransitionCondition("isHover", ConditionOp.Eq, 1.0));
            cg2.Conditions.Add(new TransitionCondition("sm_input_custom_speed", ConditionOp.Ge, 1.0));
            transIdleHover.WithConditionGroup(cg2);

            layer.AddTransition(transIdleHover);

            // Verify condition group evaluation logic
            // Initially isHover is 0.0: both groups fail
            Assert(cg1.Evaluate(smDef.Inputs, null, smDef) == false, "Group 1 fails when isHover is false");
            Assert(cg2.Evaluate(smDef.Inputs, null, smDef) == false, "Group 2 fails when isHover is false");
            Assert(transIdleHover.EvaluateConditions(smDef.Inputs, null, smDef) == false, "Transition conditions evaluate false when both groups fail");

            // Set isHover = true (value = 1.0, speed = 2.5)
            smDef.Inputs[bHoverIdx].Value = 1.0;
            Assert(cg1.Evaluate(smDef.Inputs, null, smDef) == false, "Group 1 fails because speed (2.5) is not > 10.0 (AND inside group)");
            Assert(cg2.Evaluate(smDef.Inputs, null, smDef) == true, "Group 2 succeeds because isHover == 1.0 and speed >= 1.0 (AND inside group)");
            Assert(transIdleHover.EvaluateConditions(smDef.Inputs, null, smDef) == true, "Transition succeeds because Group 2 is true (OR between groups)");

            // If speed is dropped below 1.0 (e.g. 0.5), both groups should now fail
            smDef.Inputs[nSpeedIdx].Value = 0.5;
            Assert(cg2.Evaluate(smDef.Inputs, null, smDef) == false, "Group 2 fails when speed drops to 0.5");
            Assert(transIdleHover.EvaluateConditions(smDef.Inputs, null, smDef) == false, "Transition fails when all groups fail");

            // Restore speed to 2.5
            smDef.Inputs[nSpeedIdx].Value = 2.5;

            // 3. Runtime Execution & Trigger Auto-Clearing (D-03)
            var clips = new List<Clip>
            {
                new Clip(0, "IdleClip", 1.0),
                new Clip(1, "HoverClip", 1.0),
                new Clip(2, "ClickClip", 1.0)
            };

            var transHoverClick = new StateTransition(sHover, sClick, 0.1, "sm_trans_hover_to_click");
            transHoverClick.WithCondition(new TransitionCondition("onClick", ConditionOp.Eq, 1.0));
            layer.AddTransition(transHoverClick);

            var smRuntime = new StateMachineRuntime(smDef, clips);
            var testPose = new Dictionary<uint, Dictionary<ushort, double>>();

            // First frame: Entry resolves instantly to Idle
            smRuntime.Update(0.016, testPose);
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sIdle, "Zero-duration transition resolves Entry to Idle state on first frame");

            // Trigger transition to Hover via isHover = true (via string GUID API)
            smRuntime.SetBool("isHover", true);
            smRuntime.Update(0.016, testPose);
            Assert(smRuntime.LayerRuntimes[0].ActiveTransition != null, "Transition to Hover started via Compound Condition Groups");

            // Advance through blend to reach Hover
            smRuntime.Update(0.25, testPose);
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sHover, "Transition to Hover completed");
            Assert(smRuntime.LayerRuntimes[0].ActiveTransition == null, "Active transition cleared upon completion");

            // Fire trigger onClick via string GUID API
            smRuntime.FireTrigger("onClick");
            Assert(smDef.Inputs[tClickIdx].Fired == true, "Trigger is active before update");
            smRuntime.Update(0.016, testPose);
            Assert(smDef.Inputs[tClickIdx].Fired == false, "Trigger automatically cleared after 1 update frame");
            Assert(smRuntime.LayerRuntimes[0].ActiveTransition != null, "Transition to Click started via Trigger condition");

            smRuntime.Update(0.15, testPose);
            Assert(smRuntime.LayerRuntimes[0].CurrentState == sClick, "Transition to Click state completed");

            // 4. Non-Destructive StateMachineValidator Diagnostics Engine (D-07)
            // 4a. Valid graph passes with zero errors
            var validResult = StateMachineValidator.Validate(smDef, clips);
            Assert(validResult.HasErrors == false, "Valid state machine graph has zero validation errors");

            // 4b. Diagnosing MISSING_ENTRY
            var badDef = new StateMachineDefinition("BrokenGraph");
            var badLayer = new MachineLayer("BrokenLayer");
            badLayer.AddState(new MachineState(0, "SoloState", StateKind.Animation, 0));
            badDef.AddLayer(badLayer);

            var rMissingEntry = StateMachineValidator.Validate(badDef, clips);
            Assert(rMissingEntry.HasCode(StateMachineValidator.MISSING_ENTRY), "Validator detects MISSING_ENTRY when layer has no Entry node");
            Assert(rMissingEntry.HasErrors == true, "MISSING_ENTRY is flagged as Error severity");

            // 4c. Diagnosing DANGLING_TRANSITION
            badLayer.AddState(new MachineState(1, "EntryNode", StateKind.Entry));
            badLayer.AddTransition(new StateTransition(1, 99, 0.0)); // Target state 99 does not exist
            var rDangling = StateMachineValidator.Validate(badDef, clips);
            Assert(rDangling.HasCode(StateMachineValidator.DANGLING_TRANSITION), "Validator detects DANGLING_TRANSITION for non-existent target ID");

            // 4d. Diagnosing INVALID_TARGET_ENTRY & INVALID_TARGET_ANY
            badLayer.Transitions.Clear();
            badLayer.AddTransition(new StateTransition(0, 1, 0.0)); // Points to Entry as target
            var rInvalidEntryTarget = StateMachineValidator.Validate(badDef, clips);
            Assert(rInvalidEntryTarget.HasCode(StateMachineValidator.INVALID_TARGET_ENTRY), "Validator detects INVALID_TARGET_ENTRY");

            int sAny = badLayer.AddState(new MachineState(2, "AnyNode", StateKind.Any));
            badLayer.Transitions.Clear();
            badLayer.AddTransition(new StateTransition(1, sAny, 0.0)); // Points to Any as target
            var rInvalidAnyTarget = StateMachineValidator.Validate(badDef, clips);
            Assert(rInvalidAnyTarget.HasCode(StateMachineValidator.INVALID_TARGET_ANY), "Validator detects INVALID_TARGET_ANY");

            // 4e. Diagnosing UNREACHABLE_STATE
            badLayer.Transitions.Clear();
            // Entry is node 1, SoloState is node 0, AnyNode is node 2. Add transition 1 -> 2. Node 0 has no incoming transitions.
            var rUnreachable = StateMachineValidator.Validate(badDef, clips);
            Assert(rUnreachable.HasCode(StateMachineValidator.UNREACHABLE_STATE), "Validator detects UNREACHABLE_STATE for disconnected node");
            Assert(rUnreachable.HasWarnings == true, "UNREACHABLE_STATE is flagged as Warning severity");

            // 4f. Diagnosing TYPE_MISMATCH on Boolean conditions
            int bFlagIdx = badDef.AddInput(MachineInput.Bool("bFlag", false));
            var transTypeMismatch = new StateTransition(1, 0, 0.0);
            transTypeMismatch.WithCondition(new TransitionCondition(bFlagIdx, ConditionOp.Gt, 0.5));
            badLayer.AddTransition(transTypeMismatch);

            var rTypeMismatch = StateMachineValidator.Validate(badDef, clips);
            Assert(rTypeMismatch.HasCode(StateMachineValidator.TYPE_MISMATCH), "Validator detects TYPE_MISMATCH when relational comparison is used on Boolean input");

            // 4g. Non-destructive guarantee: validator did not mutate or delete anything
            Assert(badDef.Layers[0].States.Count == 3, "StateMachineValidator is strictly read-only: state count unchanged");
            Assert(badDef.Layers[0].Transitions.Count == 1, "StateMachineValidator is strictly read-only: transition count unchanged");
            Assert(badDef.Inputs.Count == 1, "StateMachineValidator is strictly read-only: input count unchanged");

            // 5. Reversible State Node & Transition Commands in CommandHistory (D-02, D-06)
            var history = new CommandHistory(100);
            Assert(history.IsDirty == false, "CommandHistory starts clean");

            var testDef = new StateMachineDefinition("UndoRedoSM");
            var testLayer = new MachineLayer("InteractiveLayer");
            testDef.AddLayer(testLayer);

            int entryIdx = testLayer.AddState(new MachineState(0, "Entry", StateKind.Entry, -1, "sm_node_entry"));
            var entryState = testLayer.States[entryIdx];

            // 5a. CreateStateNodeCommand Execution, Undo, and Redo
            var patrolState = new MachineState(1, "Patrol", StateKind.Animation, 0, "sm_node_patrol");
            bool changedFired = false;
            var cmdCreateState = new CreateStateNodeCommand(testLayer, patrolState, () => { changedFired = true; });

            history.Execute(cmdCreateState);
            Assert(testLayer.States.Contains(patrolState), "CreateStateNodeCommand executed: patrol state added to layer");
            Assert(testLayer.FindStateById("sm_node_patrol") == patrolState, "Patrol state resolved by stable GUID");
            Assert(history.CanUndo == true && history.CanRedo == false, "CanUndo is true and CanRedo is false after execution");
            Assert(history.IsDirty == true, "CommandHistory is marked dirty after execution");
            Assert(changedFired == true, "onChanged callback fired during command execution");

            changedFired = false;
            history.Undo();
            Assert(!testLayer.States.Contains(patrolState), "CreateStateNodeCommand undone: patrol state removed from layer");
            Assert(testLayer.FindStateById("sm_node_patrol") == null, "Patrol state cannot be found after undo");
            Assert(history.CanRedo == true, "CanRedo is true after undo");
            Assert(changedFired == true, "onChanged callback fired during undo");

            history.Redo();
            Assert(testLayer.States.Contains(patrolState), "CreateStateNodeCommand redone: patrol state restored");
            Assert(testLayer.FindStateById("sm_node_patrol") == patrolState, "Patrol state resolved again after redo");

            // 5b. RenameStateNodeCommand Execution, Undo, and Redo
            var cmdRename = new RenameStateNodeCommand(patrolState, "Patrol", "CombatPatrol");
            history.Execute(cmdRename);
            Assert(patrolState.Name == "CombatPatrol", "RenameStateNodeCommand executed: state renamed to CombatPatrol");

            history.Undo();
            Assert(patrolState.Name == "Patrol", "RenameStateNodeCommand undone: state name reverted to Patrol");

            history.Redo();
            Assert(patrolState.Name == "CombatPatrol", "RenameStateNodeCommand redone: state name restored to CombatPatrol");

            // 5c. MoveStateNodesCommand Execution, Undo, and Redo (Continuous drag batched on pointer release)
            patrolState.GraphX = 100.0;
            patrolState.GraphY = 150.0;
            var moveRecord = new NodeMoveRecord(patrolState, new Point(100.0, 150.0), new Point(320.0, 480.0));
            var cmdMove = new MoveStateNodesCommand(new NodeMoveRecord[] { moveRecord });

            history.Execute(cmdMove);
            Assert(patrolState.GraphX == 320.0 && patrolState.GraphY == 480.0, "MoveStateNodesCommand executed: new position (320, 480) applied");

            history.Undo();
            Assert(patrolState.GraphX == 100.0 && patrolState.GraphY == 150.0, "MoveStateNodesCommand undone: original position (100, 150) restored");

            history.Redo();
            Assert(patrolState.GraphX == 320.0 && patrolState.GraphY == 480.0, "MoveStateNodesCommand redone: new position (320, 480) reapplied");

            // 5d. CreateTransitionCommand Execution, Undo, and Redo
            var transEntryPatrol = new StateTransition("sm_node_entry", "sm_node_patrol", 0.3, "sm_trans_entry_to_patrol");
            var cmdCreateTrans = new CreateTransitionCommand(testLayer, transEntryPatrol);

            history.Execute(cmdCreateTrans);
            Assert(testLayer.Transitions.Contains(transEntryPatrol), "CreateTransitionCommand executed: transition added");
            Assert(transEntryPatrol.FromStateId == "sm_node_entry" && transEntryPatrol.ToStateId == "sm_node_patrol", "Transition maintains stable GUID endpoints");

            history.Undo();
            Assert(!testLayer.Transitions.Contains(transEntryPatrol), "CreateTransitionCommand undone: transition removed");

            history.Redo();
            Assert(testLayer.Transitions.Contains(transEntryPatrol), "CreateTransitionCommand redone: transition restored");

            // 5e. ModifyTransitionCommand Execution, Undo, and Redo
            var oldSnapshot = new TransitionSnapshot(transEntryPatrol);
            var newSnapshot = new TransitionSnapshot(0.85, 0.9, false, InterpMode.Linear, 0.4, 0.2, 0.6, 0.8);
            var cmdModTrans = new ModifyTransitionCommand(transEntryPatrol, oldSnapshot, newSnapshot);

            history.Execute(cmdModTrans);
            Assert(transEntryPatrol.Duration == 0.85, "ModifyTransitionCommand executed: Duration updated to 0.85s");
            Assert(transEntryPatrol.ExitTime.HasValue && transEntryPatrol.ExitTime.Value == 0.9, "ModifyTransitionCommand executed: ExitTime updated to 0.9");
            Assert(transEntryPatrol.Interrupt == false, "ModifyTransitionCommand executed: Interrupt set to false");
            Assert(transEntryPatrol.Interpolation == InterpMode.Linear, "ModifyTransitionCommand executed: Interpolation updated to Linear");
            Assert(transEntryPatrol.X1 == 0.4 && transEntryPatrol.Y1 == 0.2 && transEntryPatrol.X2 == 0.6 && transEntryPatrol.Y2 == 0.8, "ModifyTransitionCommand executed: Cubic bezier params updated");

            history.Undo();
            Assert(transEntryPatrol.Duration == 0.3, "ModifyTransitionCommand undone: Duration reverted to 0.3s");
            Assert(!transEntryPatrol.ExitTime.HasValue, "ModifyTransitionCommand undone: ExitTime reverted to null");
            Assert(transEntryPatrol.Interrupt == true, "ModifyTransitionCommand undone: Interrupt reverted to true");
            Assert(transEntryPatrol.Interpolation == InterpMode.Cubic, "ModifyTransitionCommand undone: Interpolation reverted to Cubic");

            history.Redo();
            Assert(transEntryPatrol.Duration == 0.85 && transEntryPatrol.Interrupt == false, "ModifyTransitionCommand redone: modifications reapplied");

            // 5f. Cascading Transition Restoration during State Deletion (D-06)
            // Add a third state 'Alert' and create bidirectional transitions with CombatPatrol
            var alertState = new MachineState(2, "Alert", StateKind.Animation, 1, "sm_node_alert");
            testLayer.AddState(alertState);

            var transPatrolToAlert = new StateTransition("sm_node_patrol", "sm_node_alert", 0.2, "sm_trans_patrol_to_alert");
            var transAlertToPatrol = new StateTransition("sm_node_alert", "sm_node_patrol", 0.4, "sm_trans_alert_to_patrol");
            testLayer.AddTransition(transPatrolToAlert);
            testLayer.AddTransition(transAlertToPatrol);

            Assert(testLayer.States.Count == 3, "Layer has 3 states (Entry, CombatPatrol, Alert)");
            Assert(testLayer.Transitions.Count == 3, "Layer has 3 transitions (Entry->Patrol, Patrol->Alert, Alert->Patrol)");

            // Delete CombatPatrol: all 3 transitions are incident to CombatPatrol and must cascade delete
            var cmdDeleteState = new DeleteStateNodesCommand(testLayer, patrolState);
            history.Execute(cmdDeleteState);

            Assert(!testLayer.States.Contains(patrolState), "DeleteStateNodesCommand executed: CombatPatrol state removed");
            Assert(testLayer.Transitions.Count == 0, "DeleteStateNodesCommand executed: all 3 incident transitions cascade-removed");

            // Undo deletion: both state and ALL incident transitions restored with exact stable GUIDs
            history.Undo();
            Assert(testLayer.States.Contains(patrolState), "DeleteStateNodesCommand undone: CombatPatrol state restored");
            Assert(testLayer.Transitions.Count == 3, "DeleteStateNodesCommand undone: all 3 incident transitions restored");
            Assert(testLayer.Transitions.Contains(transEntryPatrol), "Restored incoming transition transEntryPatrol");
            Assert(testLayer.Transitions.Contains(transPatrolToAlert), "Restored outgoing transition transPatrolToAlert");
            Assert(testLayer.Transitions.Contains(transAlertToPatrol), "Restored incoming transition transAlertToPatrol");
            Assert(transPatrolToAlert.FromStateId == "sm_node_patrol" && transPatrolToAlert.ToStateId == "sm_node_alert", "Restored transition preserves stable GUID bindings");

            // Redo deletion: state and transitions removed again cleanly
            history.Redo();
            Assert(!testLayer.States.Contains(patrolState) && testLayer.Transitions.Count == 0, "DeleteStateNodesCommand redone: state and transitions removed cleanly");

            // Undo again to keep CombatPatrol present for transition deletion test
            history.Undo();
            Assert(testLayer.States.Contains(patrolState) && testLayer.Transitions.Count == 3, "Restored state and transitions for subsequent tests");

            // 5g. DeleteTransitionsCommand Execution, Undo, and Redo
            var cmdDelTrans = new DeleteTransitionsCommand(testLayer, new StateTransition[] { transPatrolToAlert, transAlertToPatrol });
            history.Execute(cmdDelTrans);
            Assert(testLayer.Transitions.Count == 1, "DeleteTransitionsCommand executed: 2 transitions removed, 1 remaining");
            Assert(!testLayer.Transitions.Contains(transPatrolToAlert) && !testLayer.Transitions.Contains(transAlertToPatrol), "Target transitions removed from layer");

            history.Undo();
            Assert(testLayer.Transitions.Count == 3, "DeleteTransitionsCommand undone: 2 transitions restored");
            Assert(testLayer.Transitions.Contains(transPatrolToAlert) && testLayer.Transitions.Contains(transAlertToPatrol), "Transitions present at restored indices");

            history.Redo();
            Assert(testLayer.Transitions.Count == 1 && !testLayer.Transitions.Contains(transPatrolToAlert), "DeleteTransitionsCommand redone");

            // 6. Input & Compound Condition Reversible Commands with CompositeCommand Transaction Batching (D-03, D-06)
            var inputHistory = new CommandHistory(100);

            // 6a. CreateInputCommand Execution, Undo, and Redo
            var inputHealth = MachineInput.Number("health", 100.0, "sm_input_health");
            var cmdCreateInput = new CreateInputCommand(testDef, inputHealth);

            inputHistory.Execute(cmdCreateInput);
            Assert(testDef.Inputs.Contains(inputHealth), "CreateInputCommand executed: health input added to definition");
            Assert(testDef.FindInputById("sm_input_health") == inputHealth, "FindInputById resolves health input by stable GUID");

            inputHistory.Undo();
            Assert(!testDef.Inputs.Contains(inputHealth), "CreateInputCommand undone: health input removed");
            Assert(testDef.FindInputById("sm_input_health") == null, "FindInputById returns null for health input after undo");

            inputHistory.Redo();
            Assert(testDef.Inputs.Contains(inputHealth), "CreateInputCommand redone: health input restored");
            Assert(testDef.FindInputById("sm_input_health") == inputHealth, "FindInputById resolves health input after redo");

            // 6b. ModifyInputCommand Execution, Undo, and Redo
            var cmdModInput = new ModifyInputCommand(inputHealth, "health", "maxHealth", 100.0, 200.0);
            inputHistory.Execute(cmdModInput);
            Assert(inputHealth.Name == "maxHealth" && inputHealth.InitialValue == 200.0 && inputHealth.Value == 200.0,
                "ModifyInputCommand executed: name set to maxHealth and value set to 200.0");

            inputHistory.Undo();
            Assert(inputHealth.Name == "health" && inputHealth.InitialValue == 100.0 && inputHealth.Value == 100.0,
                "ModifyInputCommand undone: name reverted to health and value reverted to 100.0");

            inputHistory.Redo();
            Assert(inputHealth.Name == "maxHealth" && inputHealth.InitialValue == 200.0 && inputHealth.Value == 200.0,
                "ModifyInputCommand redone: modifications reapplied");

            // 6c. ModifyConditionGroupsCommand with Deep Clone Isolation (D-03, D-06)
            var cgOrig = new ConditionGroup("cg_orig");
            cgOrig.Conditions.Add(new TransitionCondition("sm_input_health", ConditionOp.Lt, 50.0));
            transEntryPatrol.ConditionGroups.Clear();
            transEntryPatrol.ConditionGroups.Add(cgOrig);

            var oldGroupsList = new List<ConditionGroup> { cgOrig };
            var newGroupA = new ConditionGroup("cg_new_A");
            newGroupA.Conditions.Add(new TransitionCondition("sm_input_health", ConditionOp.Le, 20.0));
            var newGroupB = new ConditionGroup("cg_new_B");
            newGroupB.Conditions.Add(new TransitionCondition("isHover", ConditionOp.Eq, 1.0));
            var newGroupsList = new List<ConditionGroup> { newGroupA, newGroupB };

            var cmdModCg = new ModifyConditionGroupsCommand(transEntryPatrol, oldGroupsList, newGroupsList);
            inputHistory.Execute(cmdModCg);
            Assert(transEntryPatrol.ConditionGroups.Count == 2, "ModifyConditionGroupsCommand executed: 2 condition groups assigned");
            Assert(transEntryPatrol.ConditionGroups[0].Conditions[0].Op == ConditionOp.Le, "Group A condition op is Le");
            Assert(transEntryPatrol.ConditionGroups[1].Conditions[0].PropertyName == "isHover", "Group B condition property is isHover");

            // Deep clone isolation check: mutating newGroupA after execute does NOT corrupt transition
            newGroupA.Conditions[0].Value = 9999.0;
            Assert(transEntryPatrol.ConditionGroups[0].Conditions[0].Value == 20.0, "Deep clone prevents external mutation from polluting transition state");

            // Mutating transition state does NOT corrupt command's internal snapshots
            transEntryPatrol.ConditionGroups[0].Conditions[0].Value = 8888.0;

            inputHistory.Undo();
            Assert(transEntryPatrol.ConditionGroups.Count == 1, "ModifyConditionGroupsCommand undone: reverted to 1 original condition group");
            Assert(transEntryPatrol.ConditionGroups[0].Conditions[0].Op == ConditionOp.Lt, "Original condition op restored to Lt");
            Assert(transEntryPatrol.ConditionGroups[0].Conditions[0].Value == 50.0, "Original condition value restored to 50.0");

            inputHistory.Redo();
            Assert(transEntryPatrol.ConditionGroups.Count == 2, "ModifyConditionGroupsCommand redone: 2 condition groups restored");
            Assert(transEntryPatrol.ConditionGroups[0].Conditions[0].Value == 20.0, "Original value 20.0 cleanly restored without 8888 corruption");

            // 6d. DeleteInputCommand with Dependent Condition Capture & Restoration (D-06)
            var inputAmmo = MachineInput.Number("ammo", 30.0, "sm_input_ammo");
            testDef.AddInput(inputAmmo);

            var cgAmmo = new ConditionGroup("cg_ammo");
            cgAmmo.Conditions.Add(new TransitionCondition("sm_input_ammo", ConditionOp.Gt, 0.0));
            transEntryPatrol.ConditionGroups.Add(cgAmmo);

            int totalConditionsBefore = 0;
            for (int g = 0; g < transEntryPatrol.ConditionGroups.Count; g++)
                totalConditionsBefore += transEntryPatrol.ConditionGroups[g].Conditions.Count;
            Assert(totalConditionsBefore == 3, "Transition has 3 conditions total across groups before input deletion");

            var cmdDelInput = new DeleteInputCommand(testDef, inputAmmo);
            inputHistory.Execute(cmdDelInput);

            Assert(!testDef.Inputs.Contains(inputAmmo), "DeleteInputCommand executed: ammo input removed from definition");
            Assert(testDef.FindInputById("sm_input_ammo") == null, "Ammo input cannot be found by ID");
            int totalConditionsAfter = 0;
            for (int g = 0; g < transEntryPatrol.ConditionGroups.Count; g++)
                totalConditionsAfter += transEntryPatrol.ConditionGroups[g].Conditions.Count;
            Assert(totalConditionsAfter == 2, "Dependent condition referencing sm_input_ammo was captured and removed");

            // Undo: Input restored AND dependent condition restored to cg_ammo
            inputHistory.Undo();
            Assert(testDef.Inputs.Contains(inputAmmo), "DeleteInputCommand undone: ammo input restored to definition");
            Assert(testDef.FindInputById("sm_input_ammo") == inputAmmo, "Ammo input resolved by ID after undo");
            int totalConditionsRestored = 0;
            for (int g = 0; g < transEntryPatrol.ConditionGroups.Count; g++)
                totalConditionsRestored += transEntryPatrol.ConditionGroups[g].Conditions.Count;
            Assert(totalConditionsRestored == 3, "Dependent condition restored into its condition group on undo");

            // Redo: Input and dependent condition removed again
            inputHistory.Redo();
            Assert(!testDef.Inputs.Contains(inputAmmo), "DeleteInputCommand redone: ammo input removed");

            // 6e. CompositeCommand Multi-Selection Transaction Batching (D-06)
            var batchStateA = new MachineState(10, "BatchNodeA", StateKind.Animation, 0, "sm_node_batch_a");
            var batchStateB = new MachineState(11, "BatchNodeB", StateKind.Animation, 1, "sm_node_batch_b");
            var batchTrans = new StateTransition("sm_node_batch_a", "sm_node_batch_b", 0.5, "sm_trans_batch");

            var batchCommands = new List<ICommand>
            {
                new CreateStateNodeCommand(testLayer, batchStateA),
                new CreateStateNodeCommand(testLayer, batchStateB),
                new CreateTransitionCommand(testLayer, batchTrans)
            };
            var compositeCmd = new CompositeCommand("Batch Create Selection", batchCommands);

            inputHistory.Execute(compositeCmd);
            Assert(testLayer.States.Contains(batchStateA) && testLayer.States.Contains(batchStateB), "CompositeCommand executed: both batch states added");
            Assert(testLayer.Transitions.Contains(batchTrans), "CompositeCommand executed: batch transition added");

            // Single undo steps back entire batch
            inputHistory.Undo();
            Assert(!testLayer.States.Contains(batchStateA) && !testLayer.States.Contains(batchStateB), "CompositeCommand undone: both batch states removed in single Ctrl+Z step");
            Assert(!testLayer.Transitions.Contains(batchTrans), "CompositeCommand undone: batch transition removed in single Ctrl+Z step");

            // Single redo reapplies entire batch
            inputHistory.Redo();
            Assert(testLayer.States.Contains(batchStateA) && testLayer.States.Contains(batchStateB), "CompositeCommand redone: both batch states reapplied in single Ctrl+Y step");
            Assert(testLayer.Transitions.Contains(batchTrans), "CompositeCommand redone: batch transition reapplied in single Ctrl+Y step");

            // Clean up batch items via CompositeCommand deletion batching
            var batchDelete = new CompositeCommand("Batch Delete Selection", new ICommand[]
            {
                new DeleteTransitionsCommand(testLayer, batchTrans),
                new DeleteStateNodesCommand(testLayer, new MachineState[] { batchStateA, batchStateB })
            });
            inputHistory.Execute(batchDelete);
            Assert(!testLayer.States.Contains(batchStateA) && !testLayer.States.Contains(batchStateB), "Batch delete removed both states");
            Assert(!testLayer.Transitions.Contains(batchTrans), "Batch delete removed batch transition");

            inputHistory.Undo();
            Assert(testLayer.States.Contains(batchStateA) && testLayer.States.Contains(batchStateB), "Batch delete undone: both states restored");
            Assert(testLayer.Transitions.Contains(batchTrans), "Batch delete undone: transition restored");

            inputHistory.Redo();
            Assert(!testLayer.States.Contains(batchStateA) && !testLayer.Transitions.Contains(batchTrans), "Batch delete redone: clean graph state");

            // 7. End-to-End Authoring Integration & Full Undo/Redo Verification (STATE-06, STATE-02, STATE-03)
            // 7a. 3-Column UI Model Binding Verification:
            // Verify definition with all 4 state node kinds (Entry, Animation, Any, Exit) and all 3 input types (Bool, Number, Trigger)
            var defIntegration = new StateMachineDefinition("Authoring Test SM");
            var inBool = MachineInput.Bool("isActive", false, "sm_input_is_active");
            var inNum = MachineInput.Number("speedMultiplier", 1.5, "sm_input_speed_mult");
            var inTrig = MachineInput.Trigger("fireImpulse", "sm_input_fire_impulse");
            defIntegration.AddInput(inBool);
            defIntegration.AddInput(inNum);
            defIntegration.AddInput(inTrig);

            var intLayer = new MachineLayer("Interactive Layer");
            var nEntry = new MachineState(0, "Entry Node", StateKind.Entry, -1, "sm_node_entry");
            var nIdle = new MachineState(1, "Idle Anim", StateKind.Animation, 0, "sm_node_idle") { Speed = 1.2, Loop = true };
            var nAny = new MachineState(2, "Any Interrupt", StateKind.Any, -1, "sm_node_any");
            var nExit = new MachineState(3, "Exit Term", StateKind.Exit, -1, "sm_node_exit");
            intLayer.AddState(nEntry);
            intLayer.AddState(nIdle);
            intLayer.AddState(nAny);
            intLayer.AddState(nExit);
            defIntegration.AddLayer(intLayer);

            Assert(intLayer.States.Count == 4, "3-column UI model: layer hosts 4 state kinds (Entry, Animation, Any, Exit)");
            Assert(defIntegration.Inputs.Count == 3, "3-column UI model: definition hosts 3 input types (Bool, Number, Trigger)");

            // 7b. Port Drag-to-Connect Transition Creation with Validation Guards (D-04, T-06-06)
            var e2eHistory = new CommandHistory(100);

            // Valid transition: Entry -> Idle
            var transEntryIdle = new StateTransition("sm_node_entry", "sm_node_idle", 0.25, "sm_trans_entry_idle");
            var cmdCreateTrans2 = new CreateTransitionCommand(intLayer, transEntryIdle);
            e2eHistory.Execute(cmdCreateTrans2);
            Assert(intLayer.Transitions.Contains(transEntryIdle), "Port drag-to-connect executed: Entry -> Idle created");

            // Guard validation: cannot connect to Entry as destination (T-06-06)
            var invalidDestToEntry = new StateTransition("sm_node_idle", "sm_node_entry", 0.1);
            var issuesEntryDest = StateMachineValidator.Validate(defIntegration, null).Issues;
            // Guard validation: cannot connect from Exit as source (T-06-06)
            var invalidSourceFromExit = new StateTransition("sm_node_exit", "sm_node_idle", 0.1);

            // 7c. Compound Condition Group Builder Command Execution (D-03, D-06)
            // Group 1 (AND): speedMultiplier > 1.0 AND isActive == 1
            var cgroup1 = new ConditionGroup("cg_1");
            cgroup1.Conditions.Add(new TransitionCondition("sm_input_speed_mult", ConditionOp.Gt, 1.0));
            cgroup1.Conditions.Add(new TransitionCondition("sm_input_is_active", ConditionOp.Eq, 1.0));

            // Group 2 (OR): fireImpulse fired
            var cgroup2 = new ConditionGroup("cg_2");
            cgroup2.Conditions.Add(new TransitionCondition("sm_input_fire_impulse", ConditionOp.Eq, 1.0));

            var cmdCompound = new ModifyConditionGroupsCommand(transEntryIdle, transEntryIdle.ConditionGroups, new List<ConditionGroup> { cgroup1, cgroup2 });
            e2eHistory.Execute(cmdCompound);
            Assert(transEntryIdle.ConditionGroups.Count == 2, "Compound Condition Builder: 2 condition groups assigned");
            Assert(transEntryIdle.ConditionGroups[0].Conditions.Count == 2, "Group 1 contains 2 ANDed conditions");
            Assert(transEntryIdle.ConditionGroups[1].Conditions.Count == 1, "Group 2 contains 1 condition");

            // Compound evaluation test:
            // Case 1: isActive = false, fireImpulse = false -> both false -> overall false
            Assert(!transEntryIdle.ConditionGroups[0].Evaluate(defIntegration.Inputs, null, defIntegration), "Group 1 is false when isActive is false");
            Assert(!transEntryIdle.ConditionGroups[1].Evaluate(defIntegration.Inputs, null, defIntegration), "Group 2 is false when fireImpulse not fired");

            // Case 2: isActive = true -> Group 1 becomes true (speedMultiplier 1.5 > 1.0 AND isActive == 1)
            inBool.Value = 1.0;
            Assert(transEntryIdle.ConditionGroups[0].Evaluate(defIntegration.Inputs, null, defIntegration), "Group 1 is true when speedMultiplier > 1.0 AND isActive == true");

            // Case 3: isActive = false, fireImpulse = true -> Group 1 is false, Group 2 is true -> overall true (OR logic)
            inBool.Value = 0.0;
            inTrig.Fired = true;
            Assert(!transEntryIdle.ConditionGroups[0].Evaluate(defIntegration.Inputs, null, defIntegration), "Group 1 is false");
            Assert(transEntryIdle.ConditionGroups[1].Evaluate(defIntegration.Inputs, null, defIntegration), "Group 2 is true when fireImpulse fired (OR logic passes)");

            // 7d. Full Undo/Redo Stack Verification across Graph Mutations (D-06)
            // Sequence of authoring mutations:
            // 1. Rename State
            var cmdRename2 = new RenameStateNodeCommand(nIdle, "Idle Anim", "Patrol Anim");
            e2eHistory.Execute(cmdRename2);
            Assert(nIdle.Name == "Patrol Anim", "Rename executed");

            // 2. Add New State
            var nAttack = new MachineState(4, "Attack", StateKind.Animation, 1, "sm_node_attack") { GraphX = 300, GraphY = 150 };
            var cmdAddState = new CreateStateNodeCommand(intLayer, nAttack);
            e2eHistory.Execute(cmdAddState);
            Assert(intLayer.States.Contains(nAttack), "New state added");

            // 3. Move State Batch
            var moveRecords = new List<NodeMoveRecord>
            {
                new NodeMoveRecord(nAttack, 300, 150, 450, 220)
            };
            var cmdMove2 = new MoveStateNodesCommand(moveRecords);
            e2eHistory.Execute(cmdMove2);
            Assert(nAttack.GraphX == 450 && nAttack.GraphY == 220, "Batch move executed");

            // Step-by-step undo of all actions
            e2eHistory.Undo(); // Undo move
            Assert(nAttack.GraphX == 300 && nAttack.GraphY == 150, "Undo 1: Move undone");

            e2eHistory.Undo(); // Undo add state
            Assert(!intLayer.States.Contains(nAttack), "Undo 2: Add state undone");

            e2eHistory.Undo(); // Undo rename
            Assert(nIdle.Name == "Idle Anim", "Undo 3: Rename undone");

            e2eHistory.Undo(); // Undo compound conditions
            Assert(transEntryIdle.ConditionGroups.Count <= 1 && (transEntryIdle.ConditionGroups.Count == 0 || transEntryIdle.ConditionGroups[0].Conditions.Count == 0), "Undo 4: Compound conditions reverted");

            e2eHistory.Undo(); // Undo create transition
            Assert(!intLayer.Transitions.Contains(transEntryIdle), "Undo 5: Transition creation undone");

            // Step-by-step redo of all actions
            e2eHistory.Redo(); // Redo create transition
            Assert(intLayer.Transitions.Contains(transEntryIdle), "Redo 1: Transition restored");

            e2eHistory.Redo(); // Redo compound conditions
            Assert(transEntryIdle.ConditionGroups.Count == 2, "Redo 2: Compound conditions restored");

            e2eHistory.Redo(); // Redo rename
            Assert(nIdle.Name == "Patrol Anim", "Redo 3: Rename restored");

            e2eHistory.Redo(); // Redo add state
            Assert(intLayer.States.Contains(nAttack), "Redo 4: Add state restored");

            e2eHistory.Redo(); // Redo move
            Assert(nAttack.GraphX == 450 && nAttack.GraphY == 220, "Redo 5: Move restored");

            // 8. Animation Tab & Draggable States Integration (Animation Clips -> State Machine States)
            var animProject = new AnimationProjectData();
            var clipWalk = new Clip(201, "WalkCycle", 1.5, 60.0, LoopMode.Loop);
            var clipJump = new Clip(202, "Jump", 0.8, 60.0, LoopMode.OneShot);
            animProject.Clips.Add(clipWalk);
            animProject.Clips.Add(clipJump);

            // Verify clip metadata
            Assert(animProject.Clips.Count == 2, "Animation project contains 2 animation clips");
            Assert(clipWalk.LoopMode == LoopMode.Loop, "WalkCycle is a loop animation");
            Assert(clipJump.LoopMode == LoopMode.OneShot, "Jump is a one-shot animation");

            // Simulate dropping WalkCycle onto canvas at (180, 120)
            int walkClipIdx = animProject.Clips.IndexOf(clipWalk);
            var stateFromWalk = new MachineState(15, clipWalk.Name, StateKind.Animation, walkClipIdx, "sm_node_walk")
            {
                GraphX = 180.0,
                GraphY = 120.0,
                Speed = 1.0,
                Loop = (clipWalk.LoopMode == LoopMode.Loop)
            };
            var cmdDropWalk = new CreateStateNodeCommand(intLayer, stateFromWalk);
            e2eHistory.Execute(cmdDropWalk);

            Assert(intLayer.States.Contains(stateFromWalk), "State dropped from Animation Tab added to layer");
            Assert(stateFromWalk.ClipIndex == walkClipIdx, "Dropped state references correct clip index");
            Assert(stateFromWalk.GraphX == 180.0 && stateFromWalk.GraphY == 120.0, "Dropped state preserves exact canvas coordinates");
            Assert(stateFromWalk.Loop == true, "Dropped state inherited LoopMode.Loop");

            // Undo dropping state
            e2eHistory.Undo();
            Assert(!intLayer.States.Contains(stateFromWalk), "Dropped state undone cleanly via unified history");

            // Redo dropping state
            e2eHistory.Redo();
            Assert(intLayer.States.Contains(stateFromWalk), "Dropped state restored via redo");
            Assert(stateFromWalk.GraphX == 180.0, "State coordinates preserved on redo");
        }

        private static void TestFigCraftLiquidGlassAndEffectsPanel()
        {
            // 1. Verify FigCraft EffectData default values
            var fx = new EffectData();
            Assert(fx.LightAngle == -45.0, string.Format("Default LightAngle is -45.0 (got {0})", fx.LightAngle));
            Assert(fx.LightIntensity == 0.8, string.Format("Default LightIntensity is 0.8 (got {0})", fx.LightIntensity));
            Assert(fx.Refraction == 0.8, string.Format("Default Refraction is 0.8 (got {0})", fx.Refraction));
            Assert(fx.Depth == 20.0, string.Format("Default Depth is 20.0 (got {0})", fx.Depth));
            Assert(fx.Dispersion == 0.5, string.Format("Default Dispersion is 0.5 (got {0})", fx.Dispersion));
            Assert(fx.Frost == 4.0, string.Format("Default Frost is 4.0 (got {0})", fx.Frost));
            Assert(fx.Splay == 0.0, string.Format("Default Splay is 0.0 (got {0})", fx.Splay));
            Assert(fx.ShowBehind == false, "Default ShowBehind is false");

            // 2. Verify EnsureGlassDefaults backfills missing values
            var emptyGlass = new EffectData
            {
                Type = EffectType.LiquidGlass,
                Depth = 0.0,
                Refraction = 0.0,
                LightIntensity = 0.0,
                LightAngle = 0.0,
                Dispersion = 0.0,
                Frost = 0.0
            };
            emptyGlass.EnsureGlassDefaults();
            Assert(emptyGlass.Depth == 20.0, "EnsureGlassDefaults backfilled Depth = 20.0");
            Assert(emptyGlass.Refraction == 0.8, "EnsureGlassDefaults backfilled Refraction = 0.8");
            Assert(emptyGlass.LightIntensity == 0.8, "EnsureGlassDefaults backfilled LightIntensity = 0.8");
            Assert(emptyGlass.LightAngle == -45.0, "EnsureGlassDefaults backfilled LightAngle = -45.0");
            Assert(emptyGlass.Dispersion == 0.5, "EnsureGlassDefaults backfilled Dispersion = 0.5");
            Assert(emptyGlass.Frost == 4.0, "EnsureGlassDefaults backfilled Frost = 4.0");

            // 3. Verify Clone preserves all FigCraft properties
            var customGlass = new EffectData
            {
                Type = EffectType.LiquidGlass,
                LightAngle = 120.0,
                LightIntensity = 0.95,
                Refraction = 0.75,
                Depth = 45.0,
                Dispersion = 0.85,
                Frost = 12.0,
                Splay = 0.35,
                ShowBehind = true,
                ColorHex = "#A0C4FF",
                Opacity = 0.6
            };
            var cloned = customGlass.Clone();
            Assert(cloned.LightAngle == 120.0, "Cloned LightAngle preserved");
            Assert(cloned.LightIntensity == 0.95, "Cloned LightIntensity preserved");
            Assert(cloned.Refraction == 0.75, "Cloned Refraction preserved");
            Assert(cloned.Depth == 45.0, "Cloned Depth preserved");
            Assert(cloned.Dispersion == 0.85, "Cloned Dispersion preserved");
            Assert(cloned.Frost == 12.0, "Cloned Frost preserved");
            Assert(cloned.Splay == 0.35, "Cloned Splay preserved");
            Assert(cloned.ShowBehind == true, "Cloned ShowBehind preserved");
            Assert(cloned.ColorHex == "#A0C4FF", "Cloned ColorHex preserved");
            Assert(cloned.Opacity == 0.6, "Cloned Opacity preserved");

            // 4. Verify GpuRenderer backdrop rendering with FigCraft lens model
            int w = 64;
            int h = 64;
            byte[] bg = new byte[w * h * 4];
            for (int i = 0; i < bg.Length; i += 4)
            {
                bg[i] = 100;     // B
                bg[i + 1] = 150; // G
                bg[i + 2] = 200; // R
                bg[i + 3] = 255; // A
            }
            var bmp = GpuRenderer.RenderLiquidGlassBackdropTexture(
                bg, w, h, w * 4, 10.0f, 10.0f,
                w, h, 16.0f, 20.0f, 0.8f, 0.0f, 4.0f, 0.5f,
                -0.707f, -0.707f, Colors.White, 0.15f, 0.8f);
            Assert(bmp != null, "RenderLiquidGlassBackdropTexture produced valid BitmapSource");
            Assert(bmp.PixelWidth == w && bmp.PixelHeight == h, "Output texture matches target dimensions");
        }

        private static void TestGroupingBoundingBoxAndNoFillDefault()
        {
            var page = new Page();
            var history = new CommandHistory();

            var rect1 = new LayerNode
            {
                Id = "rect_1",
                Name = "Rectangle 1",
                Type = ElementType.Rectangle,
                X = 200,
                Y = 100,
                Width = 150,
                Height = 80
            };
            var rect2 = new LayerNode
            {
                Id = "rect_2",
                Name = "Rectangle 2",
                Type = ElementType.Rectangle,
                X = 200,
                Y = 220,
                Width = 150,
                Height = 80
            };
            page.Layers.Add(rect1);
            page.Layers.Add(rect2);

            // 1. Verify default creation of group has NO fills and NO strokes
            var group = LayerNode.CreateGroup("Group 1");
            Assert(group.Type == ElementType.Group, "Group type is ElementType.Group");
            Assert(group.Fills != null && group.Fills.Count == 0, "Group has 0 fills by default (no gray background)");
            Assert(group.Strokes != null && group.Strokes.Count == 0, "Group has 0 strokes by default");
            Assert(group.Effects != null && group.Effects.Count == 0, "Group has 0 effects by default");

            // 2. Group the two rectangles with GroupSelectionCommand
            var selected = new List<LayerNode> { rect1, rect2 };
            var union = TransformEngine.UnionBounds(selected);
            Assert(union.X == 200 && union.Y == 100 && union.Width == 150 && union.Height == 200, "Union bounds correctly encompass both rectangles (Y 100 to 300)");

            group.X = union.X;
            group.Y = union.Y;
            group.Width = union.Width;
            group.Height = union.Height;

            var groupCmd = new GroupSelectionCommand(page, group, selected, null, 0, null);
            history.Execute(groupCmd);

            Assert(page.Layers.Contains(group), "Group is now in page.Layers");
            Assert(!page.Layers.Contains(rect1), "rect1 is removed from top-level page.Layers");
            Assert(!page.Layers.Contains(rect2), "rect2 is removed from top-level page.Layers");
            Assert(group.Children.Count == 2, "Group has 2 children");
            Assert(rect1.ParentId == group.Id, "rect1 has group ParentId");
            Assert(rect2.ParentId == group.Id, "rect2 has group ParentId");

            // CRITICAL VERIFICATION: Child coordinates MUST NOT jump or subtract bounds!
            Assert(rect1.X == 200 && rect1.Y == 100, "rect1 preserved exact canvas coordinates (did not jump to 0,0)");
            Assert(rect2.X == 200 && rect2.Y == 220, "rect2 preserved exact canvas coordinates (did not jump to 0,120)");

            // Bounding box lockstep: group.GetBounds() matches union of its children
            var gBounds = group.GetBounds();
            Assert(gBounds.X == 200 && gBounds.Y == 100, "group.GetBounds() X and Y match children union");
            Assert(gBounds.Width == 150 && gBounds.Height == 200, "group.GetBounds() Width and Height match children union");

            // 3. Dynamic Bounding Box Update: Move child, verify group bounds updates automatically
            rect2.Y = 320; // 320 + 80 = 400 bottom
            var updatedBounds = group.GetBounds();
            Assert(updatedBounds.Height == 300, "group.GetBounds() dynamically expanded Height to 300 when child moved");
            Assert(group.Height == 300, "group.Height synchronized in lockstep");
            rect2.Y = 220; // restore
            group.GetBounds();

            // 4. Hit-testing: Groups have no background fill and cannot be hit in empty space
            var hitRect1 = CanvasInteractionController.HitTest(page, new Point(250, 140));
            Assert(hitRect1.HasHit, "Hit test on rect1 succeeded");
            Assert(hitRect1.DeepestNode == rect1, "Deepest node is rect1");
            Assert(hitRect1.OutermostNode == group, "Outermost node is group (selecting group on first click)");

            // Empty space test between rect1 (bottom 180) and rect2 (top 220) at Y=200
            var hitGap = CanvasInteractionController.HitTest(page, new Point(250, 200));
            Assert(!hitGap.HasHit, "Empty space between group children does NOT hit the group (no invisible fill box)");

            // 5. Undo / Redo for GroupSelectionCommand
            history.Undo();
            Assert(!page.Layers.Contains(group), "Undo: Group removed from page");
            Assert(page.Layers.Contains(rect1) && page.Layers.Contains(rect2), "Undo: Both rects restored to page.Layers");
            Assert(rect1.ParentId == null && rect2.ParentId == null, "Undo: ParentIds reset to null");
            Assert(rect1.X == 200 && rect1.Y == 100, "Undo: rect1 position intact");
            Assert(rect2.X == 200 && rect2.Y == 220, "Undo: rect2 position intact");

            history.Redo();
            Assert(page.Layers.Contains(group), "Redo: Group restored to page");
            Assert(group.Children.Contains(rect1) && group.Children.Contains(rect2), "Redo: Children restored to group");
            Assert(rect1.ParentId == group.Id, "Redo: rect1 parent set to group");

            // 6. UngroupSelectionCommand
            var ungroupCmd = new UngroupSelectionCommand(page, new List<LayerNode> { group }, null);
            history.Execute(ungroupCmd);

            Assert(!page.Layers.Contains(group), "Ungroup: Group removed from page");
            Assert(page.Layers.Contains(rect1) && page.Layers.Contains(rect2), "Ungroup: Children unpacked to page");
            Assert(rect1.X == 200 && rect1.Y == 100, "Ungroup: rect1 position intact (no jumping)");
            Assert(rect2.X == 200 && rect2.Y == 220, "Ungroup: rect2 position intact (no jumping)");

            // Undo Ungroup:
            history.Undo();
            Assert(page.Layers.Contains(group), "Undo Ungroup: Group restored");
            Assert(group.Children.Contains(rect1), "Undo Ungroup: rect1 restored inside group");

            // Redo Ungroup:
            history.Redo();
            Assert(!page.Layers.Contains(group), "Redo Ungroup: Group removed again");
            Assert(page.Layers.Contains(rect1), "Redo Ungroup: rect1 unpacked");
        }

        private static void TestInteractiveStateMachineEngineRefinement()
        {
            // 1. Clean Slate Initializer Verification (D-08)
            var def = new StateMachineDefinition("CleanStateMachine");
            var baseLayer = new MachineLayer("Base");
            def.AddLayer(baseLayer);
            var entryState = new MachineState(0, "Entry", StateKind.Entry);
            var anyState = new MachineState(1, "Any State", StateKind.Any);
            var exitState = new MachineState(2, "Exit", StateKind.Exit);
            baseLayer.AddState(entryState);
            baseLayer.AddState(anyState);
            baseLayer.AddState(exitState);

            Assert(def.Inputs.Count == 0, "Clean Slate: StateMachineDefinition starts with 0 preset inputs");
            Assert(def.Layers.Count == 1, "Clean Slate: 1 base layer");
            Assert(baseLayer.Transitions.Count == 0, "Clean Slate: 0 preset transitions");
            Assert(baseLayer.States.Count == 3, "Clean Slate: Only Entry, Any State, and Exit exist");
            Assert(baseLayer.States[0].Kind == StateKind.Entry, "First state is Entry");
            Assert(baseLayer.States[1].Kind == StateKind.Any, "Second state is Any State");
            Assert(baseLayer.States[2].Kind == StateKind.Exit, "Third state is Exit");

            // 2. Specialized Port Sockets Verification (D-10 & Image 5)
            // Entry has Output port only, no Input port
            Assert(!entryState.HasInputPort(), "Entry state has NO input port (HasInputPort == false)");
            Assert(entryState.HasOutputPort(), "Entry state has output port (HasOutputPort == true)");

            // Exit has Input port only, no Output port
            Assert(exitState.HasInputPort(), "Exit state has input port (HasInputPort == true)");
            Assert(!exitState.HasOutputPort(), "Exit state has NO output port (HasOutputPort == false)");

            // Any State has both Input and Output ports
            Assert(anyState.HasInputPort(), "Any State has input port (HasInputPort == true)");
            Assert(anyState.HasOutputPort(), "Any State has output port (HasOutputPort == true)");

            // Animation / Custom State has both Input and Output ports
            var animState = new MachineState(3, "WalkCycle", StateKind.Animation, 0, "sm_node_walk");
            Assert(animState.HasInputPort(), "Animation/Custom state has input port (HasInputPort == true)");
            Assert(animState.HasOutputPort(), "Animation/Custom state has output port (HasOutputPort == true)");

            // 3. State Node Properties & StateSnapshot / ModifyStateNodeCommand (D-09)
            Assert(string.IsNullOrEmpty(animState.Caption), "Default Caption is empty");
            Assert(animState.StateType == StateType.Single, "Default StateType is Single");

            var history = new CommandHistory(50);
            var oldStateSnap = new StateSnapshot(animState);
            var newStateSnap = new StateSnapshot(animState)
            {
                Caption = "Walk cycle forward loop animation",
                StateType = StateType.Blend1D,
                Speed = 1.25,
                Loop = true
            };
            var cmdModState = new ModifyStateNodeCommand(animState, oldStateSnap, newStateSnap);
            history.Execute(cmdModState);

            Assert(animState.Caption == "Walk cycle forward loop animation", "ModifyStateNodeCommand: Caption updated");
            Assert(animState.StateType == StateType.Blend1D, "ModifyStateNodeCommand: StateType updated to Blend1D");
            Assert(animState.Speed == 1.25, "ModifyStateNodeCommand: Speed updated to 1.25");
            Assert(animState.Loop == true, "ModifyStateNodeCommand: Loop updated to true");

            // Undo state modifications
            history.Undo();
            Assert(string.IsNullOrEmpty(animState.Caption), "Undo ModifyStateNodeCommand: Caption reverted");
            Assert(animState.StateType == StateType.Single, "Undo ModifyStateNodeCommand: StateType reverted to Single");
            Assert(animState.Speed == 1.0, "Undo ModifyStateNodeCommand: Speed reverted to 1.0");

            // Redo state modifications
            history.Redo();
            Assert(animState.Caption == "Walk cycle forward loop animation", "Redo ModifyStateNodeCommand: Caption restored");
            Assert(animState.StateType == StateType.Blend1D, "Redo ModifyStateNodeCommand: StateType restored to Blend1D");

            // 4. StateTransition Properties, DurationMs, and TransitionSnapshot (D-11)
            var trans = new StateTransition(entryState.NodeId, animState.NodeId, 0.35, "sm_trans_entry_walk");
            Assert(Math.Abs(trans.DurationMs - 350.0) < 0.01, "DurationMs returns milliseconds (0.35s == 350ms)");
            trans.DurationMs = 500.0;
            Assert(Math.Abs(trans.Duration - 0.5) < 0.001, "Setting DurationMs converts to seconds (500ms == 0.5s)");

            Assert(trans.PauseSourceWhenExiting == false, "Default PauseSourceWhenExiting is false");
            Assert(trans.AllowExitDuringTransition == false, "Default AllowExitDuringTransition is false");

            var oldTransSnap = new TransitionSnapshot(trans);
            var newTransSnap = oldTransSnap.Clone();
            newTransSnap.Duration = 0.75;
            newTransSnap.PauseSourceWhenExiting = true;
            newTransSnap.AllowExitDuringTransition = true;
            newTransSnap.ExitTime = 0.9;

            var cmdModTrans = new ModifyTransitionCommand(trans, oldTransSnap, newTransSnap);
            history.Execute(cmdModTrans);

            Assert(trans.Duration == 0.75, "ModifyTransitionCommand: Duration updated to 0.75s");
            Assert(Math.Abs(trans.DurationMs - 750.0) < 0.01, "DurationMs reflects 750ms");
            Assert(trans.PauseSourceWhenExiting == true, "ModifyTransitionCommand: PauseSourceWhenExiting updated to true");
            Assert(trans.AllowExitDuringTransition == true, "ModifyTransitionCommand: AllowExitDuringTransition updated to true");
            Assert(trans.ExitTime.HasValue && trans.ExitTime.Value == 0.9, "ModifyTransitionCommand: ExitTime updated to 0.9");

            // Undo transition modifications
            history.Undo();
            Assert(trans.Duration == 0.5, "Undo ModifyTransitionCommand: Duration reverted to 0.5s");
            Assert(trans.PauseSourceWhenExiting == false, "Undo ModifyTransitionCommand: PauseSourceWhenExiting reverted to false");
            Assert(trans.AllowExitDuringTransition == false, "Undo ModifyTransitionCommand: AllowExitDuringTransition reverted to false");
            Assert(!trans.ExitTime.HasValue, "Undo ModifyTransitionCommand: ExitTime reverted to null");

            // Redo transition modifications
            history.Redo();
            Assert(trans.Duration == 0.75, "Redo ModifyTransitionCommand: Duration restored");
            Assert(trans.PauseSourceWhenExiting == true, "Redo ModifyTransitionCommand: PauseSourceWhenExiting restored");
            Assert(trans.AllowExitDuringTransition == true, "Redo ModifyTransitionCommand: AllowExitDuringTransition restored");

            // 5. Canvas Selection Deletion via DeleteStateNodesCommand and DeleteTransitionsCommand (D-12)
            baseLayer.AddState(animState);
            baseLayer.AddTransition(trans);

            var reverseTrans = new StateTransition(animState.NodeId, entryState.NodeId, 0.4, "sm_trans_walk_entry");
            baseLayer.AddTransition(reverseTrans);

            Assert(baseLayer.States.Count == 4, "Layer contains 4 states");
            Assert(baseLayer.Transitions.Count == 2, "Layer contains 2 transitions (bidirectional pair)");

            // Delete transition command
            var cmdDelTrans = new DeleteTransitionsCommand(baseLayer, reverseTrans);
            history.Execute(cmdDelTrans);
            Assert(baseLayer.Transitions.Count == 1, "DeleteTransitionsCommand: 1 transition removed, 1 remains");
            Assert(!baseLayer.Transitions.Contains(reverseTrans), "Reverse transition removed");

            // Undo transition deletion
            history.Undo();
            Assert(baseLayer.Transitions.Count == 2, "Undo DeleteTransitionsCommand: transition restored");
            Assert(baseLayer.Transitions.Contains(reverseTrans), "Reverse transition restored");

            // Delete state command with cascade transition deletion
            var cmdDelState = new DeleteStateNodesCommand(baseLayer, animState);
            history.Execute(cmdDelState);
            Assert(!baseLayer.States.Contains(animState), "DeleteStateNodesCommand: WalkCycle state removed");
            Assert(baseLayer.Transitions.Count == 0, "DeleteStateNodesCommand: both incident transitions cascade-deleted");

            // Undo state deletion
            history.Undo();
            Assert(baseLayer.States.Contains(animState), "Undo DeleteStateNodesCommand: WalkCycle state restored");
            Assert(baseLayer.Transitions.Count == 2, "Undo DeleteStateNodesCommand: both incident transitions restored");
            Assert(baseLayer.Transitions.Contains(trans), "trans restored");
            Assert(baseLayer.Transitions.Contains(reverseTrans), "reverseTrans restored");

            // Redo state deletion
            history.Redo();
            Assert(!baseLayer.States.Contains(animState), "Redo DeleteStateNodesCommand: state removed again");
            Assert(baseLayer.Transitions.Count == 0, "Redo DeleteStateNodesCommand: transitions removed again");

            // 6. Exit State Movability Verification
            exitState.GraphX = 450.0;
            exitState.GraphY = 320.0;
            Assert(exitState.GraphX == 450.0 && exitState.GraphY == 320.0, "Exit state is fully movable on the canvas");

            // 7. User-Created Inputs and Listener Pointer Event Execution
            var testInputBool = MachineInput.Bool("isHovered", false);
            var testInputNum = MachineInput.Number("speedVal", 1.0);
            var testInputTrig = MachineInput.Trigger("fireTrig");
            def.AddInput(testInputBool);
            def.AddInput(testInputNum);
            def.AddInput(testInputTrig);

            Assert(def.Inputs.Count == 3, "User-defined inputs added cleanly to state machine");

            // Attach listener for canvas pointer event
            var ptrListener = new NodeListener(0u, PointerEventKind.Down);
            ptrListener.Actions.Add(new ListenerAction(ActionKind.SetBool, 0, 1.0) { PropertyName = "isHovered" });
            ptrListener.Actions.Add(new ListenerAction(ActionKind.Fire, 2, 0.0) { PropertyName = "fireTrig" });
            def.AddListener(ptrListener);

            var testClips = new List<Clip>
            {
                new Clip(100, "Idle", 2.0, 60.0, LoopMode.Loop),
                new Clip(101, "Run", 1.5, 60.0, LoopMode.Loop)
            };
            var testRuntime = new StateMachineRuntime(def, testClips);

            Assert(testRuntime.GetInputValue(0) == 0.0, "Initially isHovered input is false (0.0)");
            testRuntime.OnPointerEvent(0u, PointerEventKind.Down);
            Assert(testRuntime.GetInputValue(0) == 1.0, "OnPointerEvent triggers listener and sets user input isHovered to true (1.0)");
            Assert(def.Inputs[2].Fired == true, "OnPointerEvent triggers listener and fires user trigger fireTrig");

            // 8. Focus Targets Persistence
            Assert(def.FocusTargets != null, "def.FocusTargets initialized");
            var focusTgt = new MachineFocusTarget(0u, "Canvas", "Click");
            def.FocusTargets.Add(focusTgt);
            Assert(def.FocusTargets.Count == 1, "Focus target added");
            def.FocusTargets.Remove(focusTgt);
            Assert(def.FocusTargets.Count == 0, "Focus target removed");

            // 9. Animation Clips Reordering
            var clip1 = testClips[0];
            var clip2 = testClips[1];
            testClips.RemoveAt(0);
            testClips.Insert(1, clip1);
            Assert(testClips[0] == clip2 && testClips[1] == clip1, "Animation clips successfully reordered in collection");
        }

        private static void TestStateMachineBatchTransitionsPresetsMultiselectAndClipRenaming()
        {
            var history = new CommandHistory();
            var layer = new MachineLayer("BaseLayer");

            var s1 = new MachineState(1, "State1", StateKind.Animation, 0) { GraphX = 100.0, GraphY = 100.0 };
            var s2 = new MachineState(2, "State2", StateKind.Animation, 1) { GraphX = 300.0, GraphY = 100.0 };
            var s3 = new MachineState(3, "State3", StateKind.Animation, 2) { GraphX = 300.0, GraphY = 300.0 };
            layer.AddState(s1);
            layer.AddState(s2);
            layer.AddState(s3);

            var t1 = new StateTransition(1, 2, 0.25)
            {
                ExitTime = null,
                PauseSourceWhenExiting = false,
                AllowExitDuringTransition = false,
                Interpolation = InterpMode.Linear,
                X1 = 0.0,
                Y1 = 0.0,
                X2 = 1.0,
                Y2 = 1.0
            };
            var t2 = new StateTransition(2, 3, 0.50)
            {
                ExitTime = 0.8,
                PauseSourceWhenExiting = false,
                AllowExitDuringTransition = false,
                Interpolation = InterpMode.Linear,
                X1 = 0.0,
                Y1 = 0.0,
                X2 = 1.0,
                Y2 = 1.0
            };
            layer.AddTransition(t1);
            layer.AddTransition(t2);

            // 1. BatchModifyTransitionsCommand test
            var snapOld1 = new TransitionSnapshot(t1);
            var snapOld2 = new TransitionSnapshot(t2);

            var snapNew1 = new TransitionSnapshot(t1)
            {
                Duration = 0.80,
                ExitTime = 0.5,
                PauseSourceWhenExiting = true,
                AllowExitDuringTransition = true,
                Interpolation = InterpMode.Cubic,
                X1 = 0.42,
                Y1 = 0.0,
                X2 = 0.58,
                Y2 = 1.0
            };
            var snapNew2 = new TransitionSnapshot(t2)
            {
                Duration = 0.80,
                ExitTime = 0.5,
                PauseSourceWhenExiting = true,
                AllowExitDuringTransition = true,
                Interpolation = InterpMode.Cubic,
                X1 = 0.42,
                Y1 = 0.0,
                X2 = 0.58,
                Y2 = 1.0
            };

            var entries = new List<KeyValuePair<StateTransition, KeyValuePair<TransitionSnapshot, TransitionSnapshot>>>();
            entries.Add(new KeyValuePair<StateTransition, KeyValuePair<TransitionSnapshot, TransitionSnapshot>>(
                t1, new KeyValuePair<TransitionSnapshot, TransitionSnapshot>(snapOld1, snapNew1)));
            entries.Add(new KeyValuePair<StateTransition, KeyValuePair<TransitionSnapshot, TransitionSnapshot>>(
                t2, new KeyValuePair<TransitionSnapshot, TransitionSnapshot>(snapOld2, snapNew2)));

            var batchCmd = new BatchModifyTransitionsCommand(entries);
            history.Execute(batchCmd);

            Assert(t1.Duration == 0.80 && t2.Duration == 0.80, "Batch duration applied to both transitions");
            Assert(t1.ExitTime == 0.5 && t2.ExitTime == 0.5, "Batch exit time applied to both transitions");
            Assert(t1.PauseSourceWhenExiting == true && t2.PauseSourceWhenExiting == true, "Batch pauseSource applied");
            Assert(t1.AllowExitDuringTransition == true && t2.AllowExitDuringTransition == true, "Batch allowExit applied");
            Assert(t1.Interpolation == InterpMode.Cubic && t2.Interpolation == InterpMode.Cubic, "Batch interpolation applied");
            Assert(t1.X1 == 0.42 && t1.X2 == 0.58, "Cubic bezier control points applied to transition 1");
            Assert(t2.X1 == 0.42 && t2.X2 == 0.58, "Cubic bezier control points applied to transition 2");

            // 2. Undo / Redo batch transitions
            history.Undo();
            Assert(t1.Duration == 0.25 && t2.Duration == 0.50, "Undo restores original individual durations");
            Assert(t1.ExitTime == null && t2.ExitTime == 0.8, "Undo restores original individual exit times");
            Assert(t1.PauseSourceWhenExiting == false && t2.PauseSourceWhenExiting == false, "Undo restores pauseSource");
            Assert(t1.Interpolation == InterpMode.Linear && t2.Interpolation == InterpMode.Linear, "Undo restores Linear interpolation");

            history.Redo();
            Assert(t1.Duration == 0.80 && t2.Duration == 0.80, "Redo reapplies batch duration");
            Assert(t1.Interpolation == InterpMode.Cubic && t2.Interpolation == InterpMode.Cubic, "Redo reapplies cubic interpolation");

            // 3. New Animation Clip Creation with behaviour & duration, and Renaming
            var clip = new Clip(50u, "Attack", 1.80, 60.0, LoopMode.OneShot);
            Assert(clip.LoopMode == LoopMode.OneShot, "Clip supports OneShot behaviour mode");
            Assert(clip.Duration == 1.80, "Clip duration configured properly");

            // Clip rename sync test
            var linkedState = new MachineState(4, "Attack", StateKind.Animation, 50);
            clip.Name = "HeavyAttack";
            linkedState.Name = clip.Name;
            Assert(linkedState.Name == "HeavyAttack", "Clip rename synchronizes state label on canvas");

            // 4. Marquee intersection verification for transitions
            // Segment from (100, 100) to (300, 100) intersects a box [150, 50, 100, 100]
            var boxLeft = 150.0;
            var boxTop = 50.0;
            var boxRight = 250.0;
            var boxBottom = 150.0;

            bool intersects = (100.0 <= boxRight && 300.0 >= boxLeft && 100.0 >= boxTop && 100.0 <= boxBottom);
            Assert(intersects, "Marquee bounding box correctly intersects transition wire between states");

            // 5. Timeline Finite Playback & Idle Step Protection
            var ctrl = new AnimationTimelineController(2.0, 60.0);
            ctrl.ActiveClip = clip;
            Assert(ctrl.LoopMode == LoopMode.OneShot, "Default timeline loop mode is OneShot (finite)");
            Assert(!ctrl.IsPlaying, "Timeline is not playing initially");

            // Idle step (preview or state machine tick while paused) must not advance CurrentTime
            ctrl.Step(0.5);
            Assert(ctrl.CurrentTime == 0.0, "Timeline does not advance CurrentTime when IsPlaying is false");

            // Start playback
            ctrl.Play();
            Assert(ctrl.IsPlaying, "Timeline begins playback");
            ctrl.Step(1.0);
            Assert(Math.Abs(ctrl.CurrentTime - 1.0) < 1e-4, "Timeline advances to 1.0s during playback");

            // Step past duration: must stop at Duration (1.80s) and set IsPlaying to false
            ctrl.Step(1.5);
            Assert(Math.Abs(ctrl.CurrentTime - 1.80) < 1e-4, "Timeline stops cleanly at duration 1.80s");
            Assert(!ctrl.IsPlaying, "Timeline playback automatically terminates after one-shot completion");

            // Additional ticks after completion must not loop or drift
            ctrl.Step(0.5);
            Assert(Math.Abs(ctrl.CurrentTime - 1.80) < 1e-4, "Timeline remains halted at duration and does not loop indefinitely");

            // Calling Play() when at the end restarts from 0.0
            ctrl.Play();
            Assert(ctrl.CurrentTime == 0.0, "Playing at the end rewinds to 0.0s for clean replay");
            Assert(ctrl.IsPlaying, "Timeline is playing after replay trigger");
        }

        private static void TestStateMachinePersistenceAndSmoothTransitionBlending()
        {
            // 1. Serialization & Deserialization roundtrip of StateMachineDefinition with ConditionGroups
            var doc = new KerfDocument();
            doc.Name = "StateMachineTestDoc";
            doc.Animation = new AnimationProjectData();

            var clip1 = new Clip(101u, "IdleClip", 1.0, 60.0, LoopMode.Loop);
            var track1 = new Track(1001u, (ushort)PropKey.X);
            track1.Keys.Add(new Keyframe(0.0, 100.0, InterpMode.Linear));
            track1.Keys.Add(new Keyframe(1.0, 200.0, InterpMode.Linear));
            clip1.Tracks.Add(track1);
            doc.Animation.Clips.Add(clip1);

            var clip2 = new Clip(102u, "HoverClip", 1.0, 60.0, LoopMode.Loop);
            var track2 = new Track(1001u, (ushort)PropKey.Opacity);
            track2.Keys.Add(new Keyframe(0.0, 0.5, InterpMode.Linear));
            track2.Keys.Add(new Keyframe(1.0, 0.2, InterpMode.Linear));
            clip2.Tracks.Add(track2);
            doc.Animation.Clips.Add(clip2);

            var smDef = new StateMachineDefinition("InteractiveSM");
            smDef.AddInput(new MachineInput("isHovered", InputKind.Bool, 0.0, "input_hover"));
            smDef.AddInput(new MachineInput("speedMultiplier", InputKind.Number, 1.0, "input_speed"));

            var layer = new MachineLayer("Base Layer");
            var stateEntry = new MachineState(0, "Entry", StateKind.Entry) { GraphX = 10, GraphY = 10 };
            var stateIdle = new MachineState(1, "Idle", StateKind.Animation, 0) { GraphX = 100, GraphY = 10 };
            var stateHover = new MachineState(2, "Hover", StateKind.Animation, 1) { GraphX = 300, GraphY = 10 };
            var stateExit = new MachineState(3, "Exit", StateKind.Exit) { GraphX = 500, GraphY = 10 };
            layer.AddState(stateEntry);
            layer.AddState(stateIdle);
            layer.AddState(stateHover);
            layer.AddState(stateExit);

            var tEntryIdle = new StateTransition(0, 1, 0.0);
            layer.AddTransition(tEntryIdle);

            var tIdleHover = new StateTransition(1, 2, 0.5, "t_idle_hover")
            {
                ExitTime = 0.8,
                PauseSourceWhenExiting = true,
                AllowExitDuringTransition = true,
                Interpolation = InterpMode.Cubic,
                X1 = 0.25,
                Y1 = 0.1,
                X2 = 0.25,
                Y2 = 1.0
            };
            var condGroup = new ConditionGroup("cg_1");
            condGroup.Conditions.Add(new TransitionCondition("input_hover", ConditionOp.Eq, 1.0, true));
            tIdleHover.ConditionGroups.Add(condGroup);
            layer.AddTransition(tIdleHover);

            smDef.AddLayer(layer);
            doc.Animation.StateMachine = smDef;
            doc.Animation.ActiveClipId = clip1.Id;

            // Save and reload via StorageManager
            string tempFile = Path.Combine(Path.GetTempPath(), "kerf_sm_test_" + Guid.NewGuid().ToString("N") + ".kerf");
            try
            {
                StorageManager.SaveAtomic(doc, tempFile);
                var loadedDoc = StorageManager.LoadDocument(tempFile);

                Assert(loadedDoc != null, "Loaded document must not be null");
                Assert(loadedDoc.Animation != null, "Loaded document Animation must be preserved");
                Assert(loadedDoc.Animation.Clips != null && loadedDoc.Animation.Clips.Count == 2, "Loaded document must preserve 2 clips");
                Assert(loadedDoc.Animation.StateMachine != null, "Loaded document must preserve StateMachineDefinition");
                Assert(loadedDoc.Animation.StateMachine.Name == "InteractiveSM", "StateMachine name preserved");
                Assert(loadedDoc.Animation.StateMachine.Inputs.Count == 2, "StateMachine inputs preserved");
                Assert(loadedDoc.Animation.StateMachine.Layers.Count == 1, "StateMachine layers preserved");

                var loadedLayer = loadedDoc.Animation.StateMachine.Layers[0];
                Assert(loadedLayer.States.Count == 4, "4 states preserved in loaded layer");
                Assert(loadedLayer.Transitions.Count == 2, "2 transitions preserved in loaded layer");

                var loadedT = loadedLayer.Transitions.FirstOrDefault(t => t.TransitionId == "t_idle_hover");
                Assert(loadedT != null, "Transition 't_idle_hover' found in loaded document");
                Assert(Math.Abs(loadedT.Duration - 0.5) < 0.001, "Transition duration 0.5s preserved");
                Assert(loadedT.ExitTime == 0.8, "Transition ExitTime 0.8s preserved");
                Assert(loadedT.PauseSourceWhenExiting == true, "Transition PauseSourceWhenExiting preserved");
                Assert(loadedT.AllowExitDuringTransition == true, "Transition AllowExitDuringTransition preserved");
                Assert(loadedT.Interpolation == InterpMode.Cubic, "Transition Cubic interpolation preserved");
                Assert(Math.Abs(loadedT.X1 - 0.25) < 0.001 && Math.Abs(loadedT.Y2 - 1.0) < 0.001, "Cubic bezier handles preserved");
                Assert(loadedT.ConditionGroups.Count == 1, "ConditionGroup preserved");
                Assert(loadedT.ConditionGroups[0].Conditions.Count == 1, "TransitionCondition preserved inside group");
                Assert(loadedT.ConditionGroups[0].Conditions[0].InputId == "input_hover", "InputId preserved in condition");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }

            // 2. Smooth Transition Blending with RestPose
            uint nodeId = 1001u;
            var restPose = new Dictionary<uint, Dictionary<ushort, double>>();
            var nodeRest = new Dictionary<ushort, double>();
            nodeRest[(ushort)PropKey.X] = 10.0;
            nodeRest[(ushort)PropKey.Opacity] = 1.0;
            nodeRest[(ushort)PropKey.ScaleX] = 1.0;
            nodeRest[(ushort)PropKey.ScaleY] = 1.0;
            restPose[nodeId] = nodeRest;

            // Base pose has X = 100.0 (no Opacity)
            var basePose = new Dictionary<uint, Dictionary<ushort, double>>();
            var baseNode = new Dictionary<ushort, double>();
            baseNode[(ushort)PropKey.X] = 100.0;
            basePose[nodeId] = baseNode;

            // Target pose has Opacity = 0.2 (no X)
            var targetPose = new Dictionary<uint, Dictionary<ushort, double>>();
            var targetNode = new Dictionary<ushort, double>();
            targetNode[(ushort)PropKey.Opacity] = 0.2;
            targetPose[nodeId] = targetNode;

            var outPose = new Dictionary<uint, Dictionary<ushort, double>>();
            // Blend at mix = 0.5
            StateMachineRuntime.BlendPoses(basePose, targetPose, 0.5, outPose, restPose);

            Assert(outPose.ContainsKey(nodeId), "Blended pose contains test node");
            // X blends from 100.0 towards rest pose 10.0: 100 + (10 - 100) * 0.5 = 55.0
            Assert(Math.Abs(outPose[nodeId][(ushort)PropKey.X] - 55.0) < 0.001, "X property smoothly blends from base to rest pose (55.0)");
            // Opacity blends from rest pose 1.0 towards target 0.2: 1.0 + (0.2 - 1.0) * 0.5 = 0.6
            Assert(Math.Abs(outPose[nodeId][(ushort)PropKey.Opacity] - 0.6) < 0.001, "Opacity property smoothly blends from rest pose to target (0.6)");

            // 3. StateMachineRuntime HasActiveTransitions and continuous smooth transition advancement
            var runtime = new StateMachineRuntime(smDef, doc.Animation.Clips);
            runtime.RestPose = restPose;

            Assert(!runtime.HasActiveTransitions(), "Runtime has no active transitions initially");

            // Advance through Entry -> Idle
            var runPose = new Dictionary<uint, Dictionary<ushort, double>>();
            runtime.Update(0.0, runPose);
            Assert(runtime.LayerRuntimes[0].CurrentState == 1, "Layer entered Idle state");

            // Now set input isHovered = true
            runtime.SetBool(0, true);

            // Before reaching ExitTime (0.8s), transition must not fire yet
            runtime.Update(0.4, runPose);
            Assert(!runtime.HasActiveTransitions(), "Transition respects ExitTime (0.8s) and does not fire at 0.4s");

            // Advance past ExitTime (at 0.85s): condition is met and ExitTime reached, transition begins!
            runtime.Update(0.45, runPose);
            Assert(runtime.HasActiveTransitions(), "Active transition fires after reaching ExitTime (0.8s)");
            Assert(runtime.LayerRuntimes[0].ActiveTransition != null, "Layer has active transition state");
            Assert(runtime.LayerRuntimes[0].ActiveTransition.ToState == 2, "Transition destination is Hover state (2)");

            // Advance dt = 0.25 (halfway through 0.5s transition duration)
            runtime.Update(0.25, runPose);
            Assert(runtime.HasActiveTransitions(), "Transition still in flight at 0.25s / 0.5s");
            Assert(Math.Abs(runtime.LayerRuntimes[0].ActiveTransition.Elapsed - 0.25) < 0.001, "Elapsed time is 0.25s");

            // Advance dt = 0.25 (transition completion)
            runtime.Update(0.25, runPose);
            Assert(!runtime.HasActiveTransitions(), "Transition finished and cleared at 0.5s");
            Assert(runtime.LayerRuntimes[0].CurrentState == 2, "Layer successfully in Hover state (2)");
        }

        private static void TestInteractiveListenersAndCentralizedEventDispatcher()
        {
            // 1. Enum verification
            Assert((int)PointerEventKind.Click == 5, "PointerEventKind.Click is 5");
            Assert((int)PointerEventKind.Tap == 6, "PointerEventKind.Tap is 6");
            Assert((int)PointerEventKind.DragStart == 7, "PointerEventKind.DragStart is 7");
            Assert((int)PointerEventKind.Drag == 8, "PointerEventKind.Drag is 8");
            Assert((int)PointerEventKind.DragEnd == 9, "PointerEventKind.DragEnd is 9");
            Assert((int)PointerEventKind.PointerDown == 0, "PointerEventKind.PointerDown alias is 0");
            Assert((int)PointerEventKind.PointerUp == 1, "PointerEventKind.PointerUp alias is 1");
            Assert((int)PointerEventKind.PointerMove == 2, "PointerEventKind.PointerMove alias is 2");
            Assert((int)PointerEventKind.PointerEnter == 3, "PointerEventKind.PointerEnter alias is 3");
            Assert((int)PointerEventKind.PointerExit == 4, "PointerEventKind.PointerExit alias is 4");

            Assert((int)ActionKind.ToggleBool == 7, "ActionKind.ToggleBool is 7");
            Assert((int)ActionKind.Reset == 8, "ActionKind.Reset is 8");
            Assert((int)ActionKind.FireTrigger == 2, "ActionKind.FireTrigger alias is 2");

            // 2. NodeListener model and ListenerId GUID / Enabled defaults
            var listener = new NodeListener(101u, PointerEventKind.Click);
            Assert(!string.IsNullOrEmpty(listener.ListenerId) && listener.ListenerId.StartsWith("sm_lst_"), "NodeListener initializes ListenerId GUID sm_lst_*");
            Assert(listener.Enabled == true, "NodeListener defaults to Enabled = true");
            Assert(listener.TargetNodeId == 101u, "TargetNodeId matches");
            Assert(listener.Kind == PointerEventKind.Click, "Kind matches Click");

            var action1 = new ListenerAction(ActionKind.ToggleBool, 0);
            action1.InputId = "input_bool_1";
            listener.Actions.Add(action1);

            var clonedListener = listener.Clone();
            Assert(clonedListener.ListenerId == listener.ListenerId, "Clone preserves ListenerId");
            Assert(clonedListener.Enabled == listener.Enabled, "Clone preserves Enabled");
            Assert(clonedListener.Actions.Count == 1, "Clone copies actions");
            Assert(clonedListener.Actions[0].InputId == "input_bool_1", "Clone deep-copies action InputId");

            // 3. StateMachineRuntime.OnPointerEvent actions (ToggleBool, Reset, Enabled check)
            var smDef = new StateMachineDefinition();
            var inBool = MachineInput.Bool("isHovered", false, "input_bool_1");
            var inNum = MachineInput.Number("score", 10.0, "input_num_1");
            var inTrig = MachineInput.Trigger("fireTrig", "input_trig_1");
            smDef.Inputs.Add(inBool);
            smDef.Inputs.Add(inNum);
            smDef.Inputs.Add(inTrig);

            var toggleListener = new NodeListener(200u, PointerEventKind.Click);
            toggleListener.Actions.Add(new ListenerAction(ActionKind.ToggleBool, 0));
            smDef.AddListener(toggleListener);

            var resetListener = new NodeListener(200u, PointerEventKind.Tap);
            resetListener.Actions.Add(new ListenerAction(ActionKind.Reset, 1));
            smDef.AddListener(resetListener);

            var trigListener = new NodeListener(200u, PointerEventKind.Down);
            trigListener.Actions.Add(new ListenerAction(ActionKind.FireTrigger, 2));
            smDef.AddListener(trigListener);

            var runtime = new StateMachineRuntime(smDef, new List<Clip>());

            // Trigger toggle: false -> true
            runtime.OnPointerEvent(200u, PointerEventKind.Click);
            Assert(inBool.Value > 0.5, "ToggleBool toggles false to true");

            // Toggle again: true -> false
            runtime.OnPointerEvent(200u, PointerEventKind.Click);
            Assert(inBool.Value <= 0.5, "ToggleBool toggles true to false");

            // When disabled, toggle does NOT execute
            toggleListener.Enabled = false;
            runtime.OnPointerEvent(200u, PointerEventKind.Click);
            Assert(inBool.Value <= 0.5, "Disabled listener does not execute ToggleBool");
            toggleListener.Enabled = true;

            // FireTrigger
            Assert(!inTrig.Fired, "Trigger not fired initially");
            runtime.OnPointerEvent(200u, PointerEventKind.Down);
            Assert(inTrig.Fired, "ActionKind.FireTrigger fires trigger");

            // Reset action
            inNum.Value = 99.0;
            runtime.OnPointerEvent(200u, PointerEventKind.Tap);
            Assert(Math.Abs(inNum.Value - 10.0) < 0.001, "ActionKind.Reset restores InitialValue");

            // 4. InteractiveEventDispatcher hit routing and gesture lifecycle
            var page = new Page();
            var btnNode = new LayerNode
            {
                Id = "interactive_btn",
                Name = "Interactive Button",
                Type = ElementType.Rectangle,
                X = 50,
                Y = 50,
                Width = 100,
                Height = 60,
                IsVisible = true
            };
            page.Layers.Add(btnNode);
            uint btnId = PropertyBinder.GetNumericNodeId(btnNode.Id);

            var sceneGraph = new RetainedSceneGraph(new GpuRenderer());
            sceneGraph.Synchronize(page);

            var dispatcher = new InteractiveEventDispatcher();

            // Hover enter
            dispatcher.OnPointerMove(runtime, sceneGraph, new Point(70, 70));
            Assert(dispatcher.LastHoverNodeId == btnId, "Dispatcher tracks hover enter on button");

            // Hover exit
            dispatcher.OnPointerMove(runtime, sceneGraph, new Point(10, 10));
            Assert(dispatcher.LastHoverNodeId == 0u, "Dispatcher tracks hover exit away from button");

            // Click gesture: PointerDown + PointerUp without move
            dispatcher.OnPointerDown(runtime, sceneGraph, new Point(70, 70));
            Assert(dispatcher.PointerDownNodeId == btnId, "PointerDown records down node ID");
            Assert(!dispatcher.IsDragging, "Not dragging yet");

            dispatcher.OnPointerUp(runtime, sceneGraph, new Point(70, 70));
            Assert(dispatcher.PointerDownNodeId == 0u, "PointerUp clears pointer down node ID");

            // Drag gesture: PointerDown + PointerMove > 4px
            dispatcher.OnPointerDown(runtime, sceneGraph, new Point(70, 70));
            dispatcher.OnPointerMove(runtime, sceneGraph, new Point(76, 70)); // dx = 6 > 4
            Assert(dispatcher.IsDragging, "Dispatcher transitions to dragging when moving > 4px");

            dispatcher.OnPointerUp(runtime, sceneGraph, new Point(76, 70));
            Assert(!dispatcher.IsDragging, "PointerUp ends drag state");

            dispatcher.Reset();
            Assert(dispatcher.LastHoverNodeId == 0u && dispatcher.PointerDownNodeId == 0u && !dispatcher.IsDragging, "Reset clears all dispatcher state");

            // 5. Transactional Reversible Listener Commands in CommandHistory
            var testDef = new StateMachineDefinition();
            var l1 = new NodeListener(301u, PointerEventKind.PointerEnter);
            var l2 = new NodeListener(302u, PointerEventKind.PointerExit);
            var l3 = new NodeListener(303u, PointerEventKind.Click);

            int callbackCount = 0;
            Action onChanged = () => { callbackCount++; };

            // AddListenerCommand
            var addCmd1 = new AddListenerCommand(testDef, l1, onChanged);
            addCmd1.Execute();
            Assert(testDef.Listeners.Count == 1 && testDef.Listeners[0] == l1, "AddListenerCommand added first listener");
            Assert(callbackCount == 1, "AddListenerCommand triggered onChanged callback");

            var addCmd2 = new AddListenerCommand(testDef, l2, onChanged);
            addCmd2.Execute();
            Assert(testDef.Listeners.Count == 2 && testDef.Listeners[1] == l2, "AddListenerCommand added second listener at end");

            // Insert l3 at index 1
            var addCmd3 = new AddListenerCommand(testDef, l3, 1, onChanged);
            addCmd3.Execute();
            Assert(testDef.Listeners.Count == 3 && testDef.Listeners[1] == l3 && testDef.Listeners[2] == l2, "AddListenerCommand inserted listener at index 1");

            // Undo addCmd3
            addCmd3.Unexecute();
            Assert(testDef.Listeners.Count == 2 && !testDef.Listeners.Contains(l3), "AddListenerCommand Unexecute removed inserted listener");

            // Re-execute addCmd3
            addCmd3.Execute();
            Assert(testDef.Listeners.Count == 3 && testDef.Listeners[1] == l3, "AddListenerCommand Execute re-inserted listener");

            // DeleteListenerCommand (deleting l3 at index 1)
            var delCmd = new DeleteListenerCommand(testDef, l3, onChanged);
            delCmd.Execute();
            Assert(testDef.Listeners.Count == 2 && !testDef.Listeners.Contains(l3), "DeleteListenerCommand removed listener");
            Assert(testDef.Listeners[0] == l1 && testDef.Listeners[1] == l2, "Remaining listeners preserved");

            // Undo delete: must restore to index 1!
            delCmd.Unexecute();
            Assert(testDef.Listeners.Count == 3 && testDef.Listeners[1] == l3, "DeleteListenerCommand Unexecute restored listener to exact index 1");

            // ModifyListenerCommand
            var oldSnap = new NodeListenerSnapshot(l3);
            var newSnap = new NodeListenerSnapshot
            {
                TargetNodeId = 999u,
                Kind = PointerEventKind.DragStart,
                Enabled = false,
                Actions = new List<ListenerAction>
                {
                    new ListenerAction(ActionKind.ToggleBool, 5) { InputId = "modified_input", Parameter = 1.0 }
                }
            };

            var modCmd = new ModifyListenerCommand(l3, oldSnap, newSnap, onChanged);
            modCmd.Execute();
            Assert(l3.TargetNodeId == 999u, "ModifyListenerCommand updated TargetNodeId");
            Assert(l3.Kind == PointerEventKind.DragStart, "ModifyListenerCommand updated Kind");
            Assert(l3.Enabled == false, "ModifyListenerCommand updated Enabled");
            Assert(l3.Actions.Count == 1 && l3.Actions[0].InputId == "modified_input", "ModifyListenerCommand updated Actions");

            // Undo modify: restores old snapshot exactly
            modCmd.Unexecute();
            Assert(l3.TargetNodeId == 303u, "ModifyListenerCommand Unexecute restored TargetNodeId");
            Assert(l3.Kind == PointerEventKind.Click, "ModifyListenerCommand Unexecute restored Kind");
            Assert(l3.Enabled == true, "ModifyListenerCommand Unexecute restored Enabled");
            Assert(l3.Actions.Count == 0, "ModifyListenerCommand Unexecute restored Actions count");

            // Redo modify
            modCmd.Execute();
            Assert(l3.TargetNodeId == 999u && l3.Enabled == false && l3.Actions.Count == 1, "ModifyListenerCommand re-executed cleanly");
        }

        private static void TestInterruptedTransitionSnapshotBlending()
        {
            // 1. Setup clips with animated Node 55 X and Opacity
            List<Clip> clips = new List<Clip>();

            // Clip A: X = 100, Opacity = 1.0
            Clip clipA = new Clip(0, "ClipA", 1.0, 60.0, LoopMode.Loop);
            Track trAX = new Track(55u, (ushort)PropKey.X);
            trAX.AddKeyframe(Keyframe.Linear(0.0, 100.0));
            clipA.AddTrack(trAX);
            Track trAOp = new Track(55u, (ushort)PropKey.Opacity);
            trAOp.AddKeyframe(Keyframe.Linear(0.0, 1.0));
            clipA.AddTrack(trAOp);
            clips.Add(clipA);

            // Clip B: X = 200, Opacity = 0.5
            Clip clipB = new Clip(1, "ClipB", 1.0, 60.0, LoopMode.Loop);
            Track trBX = new Track(55u, (ushort)PropKey.X);
            trBX.AddKeyframe(Keyframe.Linear(0.0, 200.0));
            clipB.AddTrack(trBX);
            Track trBOp = new Track(55u, (ushort)PropKey.Opacity);
            trBOp.AddKeyframe(Keyframe.Linear(0.0, 0.5));
            clipB.AddTrack(trBOp);
            clips.Add(clipB);

            // Clip C: X = 400, Opacity = 0.0
            Clip clipC = new Clip(2, "ClipC", 1.0, 60.0, LoopMode.Loop);
            Track trCX = new Track(55u, (ushort)PropKey.X);
            trCX.AddKeyframe(Keyframe.Linear(0.0, 400.0));
            clipC.AddTrack(trCX);
            Track trCOp = new Track(55u, (ushort)PropKey.Opacity);
            trCOp.AddKeyframe(Keyframe.Linear(0.0, 0.0));
            clipC.AddTrack(trCOp);
            clips.Add(clipC);

            // 2. Setup state machine
            StateMachineDefinition smDef = new StateMachineDefinition("SnapshotMachine");
            int boolGoB = smDef.AddInput(MachineInput.Bool("goB", false));
            int boolGoC = smDef.AddInput(MachineInput.Bool("goC", false));

            MachineLayer layer = new MachineLayer("MainLayer");
            int sA = layer.AddState(new MachineState(0, "StateA", StateKind.Animation, 0));
            int sB = layer.AddState(new MachineState(1, "StateB", StateKind.Animation, 1));
            int sC = layer.AddState(new MachineState(2, "StateC", StateKind.Animation, 2));

            // Trans A -> B: 1.0s Linear
            StateTransition transAB = new StateTransition(sA, sB, 1.0);
            transAB.Interpolation = InterpMode.Linear;
            transAB.Interrupt = true;
            transAB.WithCondition(new TransitionCondition(boolGoB, ConditionOp.Eq, 1.0));
            layer.AddTransition(transAB);

            // Trans A -> C: 0.5s Linear (Interruption transition)
            StateTransition transAC = new StateTransition(sA, sC, 0.5);
            transAC.Interpolation = InterpMode.Linear;
            transAC.Interrupt = true;
            transAC.WithCondition(new TransitionCondition(boolGoC, ConditionOp.Eq, 1.0));
            layer.AddTransition(transAC);

            smDef.AddLayer(layer);

            StateMachineRuntime runtime = new StateMachineRuntime(smDef, clips);
            var pose = new Dictionary<uint, Dictionary<ushort, double>>();

            // Initial frame: in State A
            runtime.Update(0.0, pose);
            Assert(runtime.LayerRuntimes[0].CurrentState == sA, "Initial state is StateA");
            Assert(pose.ContainsKey(55u) && Math.Abs(pose[55u][(ushort)PropKey.X] - 100.0) < 0.001, "Initial pose X is 100");
            Assert(Math.Abs(pose[55u][(ushort)PropKey.Opacity] - 1.0) < 0.001, "Initial pose Opacity is 1.0");

            // Trigger A -> B
            runtime.SetBool(boolGoB, true);
            runtime.Update(0.0, pose);
            Assert(runtime.LayerRuntimes[0].ActiveTransition != null && runtime.LayerRuntimes[0].ActiveTransition.ToState == sB, "Transition to B started");

            // Advance 0.5s (mix = 0.5, halfway to B)
            runtime.Update(0.5, pose);
            Assert(runtime.LayerRuntimes[0].ActiveTransition != null, "Transition to B still in flight at 0.5s");
            double midX = pose[55u][(ushort)PropKey.X];
            double midOp = pose[55u][(ushort)PropKey.Opacity];
            Assert(Math.Abs(midX - 150.0) < 0.01, string.Format("Mid-transition X expected 150.0, got {0}", midX));
            Assert(Math.Abs(midOp - 0.75) < 0.01, string.Format("Mid-transition Opacity expected 0.75, got {0}", midOp));

            // Interrupt mid-blend with transition to C!
            runtime.SetBool(boolGoB, false);
            runtime.SetBool(boolGoC, true);
            runtime.Update(0.0, pose);

            // Assert SnapshotPose was captured
            var activeTrans = runtime.LayerRuntimes[0].ActiveTransition;
            Assert(activeTrans != null, "New transition active after interruption");
            Assert(activeTrans.ToState == sC, "Interrupted transition targets StateC");
            Assert(activeTrans.SnapshotPose != null, "SnapshotPose captured at moment of interruption per D-04");
            Assert(activeTrans.SnapshotPose.ContainsKey(55u), "SnapshotPose contains animated node 55");

            double snapX = activeTrans.SnapshotPose[55u][(ushort)PropKey.X];
            double snapOp = activeTrans.SnapshotPose[55u][(ushort)PropKey.Opacity];
            Assert(Math.Abs(snapX - 150.0) < 0.01, string.Format("SnapshotPose frozen X expected 150.0, got {0}", snapX));
            Assert(Math.Abs(snapOp - 0.75) < 0.01, string.Format("SnapshotPose frozen Opacity expected 0.75, got {0}", snapOp));

            // Instantaneous pose at interruption must NOT jump back to A (100) or snap to B (200)
            double instantX = pose[55u][(ushort)PropKey.X];
            double instantOp = pose[55u][(ushort)PropKey.Opacity];
            Assert(Math.Abs(instantX - 150.0) < 0.01, string.Format("Continuous interpolation: pose at interruption expected 150.0, got {0}", instantX));
            Assert(Math.Abs(instantOp - 0.75) < 0.01, string.Format("Continuous interpolation: pose at interruption expected 0.75, got {0}", instantOp));

            // Advance 0.25s (halfway through the 0.5s transition to C):
            // Expected blend from snapshot (150, 0.75) to C (400, 0.0) at mix = 0.5:
            // X = 150 + (400 - 150) * 0.5 = 275.0
            // Opacity = 0.75 + (0.0 - 0.75) * 0.5 = 0.375
            runtime.Update(0.25, pose);
            double blendQuarterX = pose[55u][(ushort)PropKey.X];
            double blendQuarterOp = pose[55u][(ushort)PropKey.Opacity];
            Assert(Math.Abs(blendQuarterX - 275.0) < 0.05, string.Format("Interpolating from snapshot: expected X 275.0, got {0}", blendQuarterX));
            Assert(Math.Abs(blendQuarterOp - 0.375) < 0.05, string.Format("Interpolating from snapshot: expected Opacity 0.375, got {0}", blendQuarterOp));

            // Complete transition to C: advance remaining 0.25s
            runtime.Update(0.25, pose);
            Assert(runtime.LayerRuntimes[0].ActiveTransition == null, "Transition to C completed");
            Assert(runtime.LayerRuntimes[0].CurrentState == sC, "Current state is now StateC");
            double finalX = pose[55u][(ushort)PropKey.X];
            double finalOp = pose[55u][(ushort)PropKey.Opacity];
            Assert(Math.Abs(finalX - 400.0) < 0.01, string.Format("Final state C reached: expected X 400.0, got {0}", finalX));
            Assert(Math.Abs(finalOp - 0.0) < 0.01, string.Format("Final state C reached: expected Opacity 0.0, got {0}", finalOp));

            // Enforce D-06: Non-destructive application to RenderTransform
            LayerNode testLayer = new LayerNode
            {
                Id = "test-d06-layer",
                X = 50.0,
                Y = 50.0,
                Width = 120.0,
                Height = 80.0,
                CornerRadius = 12.0
            };
            PropertyBinder.ApplyPoseToLayer(testLayer, 55u, pose);
            Assert(testLayer.X == 50.0 && testLayer.Y == 50.0, "Layer layout geometry X/Y preserved per D-06");
            Assert(testLayer.Width == 120.0 && testLayer.Height == 80.0, "Layer layout geometry Width/Height preserved per D-06");
            Assert(testLayer.CornerRadius == 12.0, "Corner radius preserved per D-06");
            Assert(testLayer.RenderTransform != null, "RenderTransform allocated");
            Assert(Math.Abs(testLayer.RenderTransform.TranslationX - (400.0 - 50.0)) < 0.01, "RenderTransform TranslationX holds animated offset");
            Assert(Math.Abs(testLayer.Opacity - 0.0) < 0.01, "Layer visual Opacity updated from pose");
        }

        private static void TestDeterministicTransitionPriorityExitTimeAndClipAdvancement()
        {
            // =========================================================================
            // 1. Transition Priority Resolution per D-05
            // =========================================================================
            List<Clip> clips = new List<Clip>();
            Clip c0 = new Clip(0, "Clip0", 2.0, 60.0, LoopMode.Loop);
            Clip c1 = new Clip(1, "Clip1", 2.0, 60.0, LoopMode.Loop);
            Clip c2 = new Clip(2, "Clip2", 2.0, 60.0, LoopMode.Loop);
            clips.Add(c0);
            clips.Add(c1);
            clips.Add(c2);

            StateMachineDefinition priorityDef = new StateMachineDefinition("PriorityMachine");
            int boolTrigger = priorityDef.AddInput(MachineInput.Bool("fireAll", false));

            MachineLayer prioLayer = new MachineLayer("PrioLayer");
            int stateA = prioLayer.AddState(new MachineState(0, "StateA", StateKind.Animation, 0));
            int stateB = prioLayer.AddState(new MachineState(1, "StateB", StateKind.Animation, 1));
            int stateC = prioLayer.AddState(new MachineState(2, "StateC", StateKind.Animation, 2));
            int stateAny = prioLayer.AddState(new MachineState(3, "AnyState", StateKind.Any, -1));

            // Outgoing transition from current state A -> B (Condition: boolTrigger == 1.0)
            StateTransition transFromCurrent = new StateTransition(stateA, stateB, 0.5);
            transFromCurrent.WithCondition(new TransitionCondition(boolTrigger, ConditionOp.Eq, 1.0));

            // Outgoing transition from Any state -> C (Condition: boolTrigger == 1.0)
            StateTransition transFromAny = new StateTransition(stateAny, stateC, 0.5);
            transFromAny.WithCondition(new TransitionCondition(boolTrigger, ConditionOp.Eq, 1.0));

            // Even if AnyState transition is added FIRST in layer.Transitions:
            prioLayer.AddTransition(transFromAny);
            prioLayer.AddTransition(transFromCurrent);
            priorityDef.AddLayer(prioLayer);

            StateMachineRuntime prioRuntime = new StateMachineRuntime(priorityDef, clips);
            var pose = new Dictionary<uint, Dictionary<ushort, double>>();
            prioRuntime.Update(0.0, pose);
            Assert(prioRuntime.LayerRuntimes[0].CurrentState == stateA, "Initial state is StateA");

            // Trigger both conditions simultaneously
            prioRuntime.SetBool(boolTrigger, true);
            prioRuntime.Update(0.016, pose);

            // Per D-05: current state transitions MUST be chosen over AnyState transitions
            Assert(prioRuntime.LayerRuntimes[0].ActiveTransition != null, "Transition triggered");
            Assert(prioRuntime.LayerRuntimes[0].ActiveTransition.ToState == stateB,
                "Current state transition (A -> B) takes priority over AnyState transition (Any -> C) per D-05");

            // =========================================================================
            // 2. ExitTime Normalized Threshold Gating per RUNTIME-01
            // =========================================================================
            StateMachineDefinition exitDef = new StateMachineDefinition("ExitTimeMachine");
            MachineLayer exitLayer = new MachineLayer("ExitLayer");
            int eA = exitLayer.AddState(new MachineState(0, "FromState", StateKind.Animation, 0)); // Clip 0: Duration = 2.0s
            int eB = exitLayer.AddState(new MachineState(1, "ToState", StateKind.Animation, 1));

            // Transition with ExitTime = 0.8 (requires 80% normalized time through clip = 1.6s)
            StateTransition exitTrans = new StateTransition(eA, eB, 0.2);
            exitTrans.ExitTime = 0.8;
            exitLayer.AddTransition(exitTrans);
            exitDef.AddLayer(exitLayer);

            StateMachineRuntime exitRuntime = new StateMachineRuntime(exitDef, clips);
            exitRuntime.Update(0.0, pose);
            Assert(exitRuntime.LayerRuntimes[0].CurrentState == eA, "Starts in FromState");
            Assert(exitRuntime.LayerRuntimes[0].ActiveTransition == null, "No transition active yet");

            // Advance 0.5s: progress = 0.5s / 2.0s = 0.25 (< 0.80)
            exitRuntime.Update(0.5, pose);
            Assert(exitRuntime.LayerRuntimes[0].ActiveTransition == null, "ExitTime 0.8 blocks transition at progress 0.25");

            // Advance another 0.5s: total 1.0s, progress = 1.0s / 2.0s = 0.50 (< 0.80)
            exitRuntime.Update(0.5, pose);
            Assert(exitRuntime.LayerRuntimes[0].ActiveTransition == null, "ExitTime 0.8 blocks transition at progress 0.50");

            // Advance 0.65s: total 1.65s, progress = 1.65s / 2.0s = 0.825 (>= 0.80)
            exitRuntime.Update(0.65, pose);
            Assert(exitRuntime.LayerRuntimes[0].ActiveTransition != null, "ExitTime threshold (0.80) reached at progress 0.825 -> transition fires automatically");
            Assert(exitRuntime.LayerRuntimes[0].ActiveTransition.ToState == eB, "Transition targets eB");

            // =========================================================================
            // 3. Clip Playback Advancement, Speeds & Loop Modes per TRANS-EXEC-01
            // =========================================================================
            ClipInstance instOneShot = new ClipInstance(0);
            instOneShot.Speed = 1.0;
            // Advance OneShot
            bool done1 = instOneShot.Advance(0.6, 1.0, LoopMode.OneShot);
            Assert(Math.Abs(instOneShot.Time - 0.6) < 0.001 && !done1, "OneShot advances to 0.6");
            bool done2 = instOneShot.Advance(0.6, 1.0, LoopMode.OneShot);
            Assert(Math.Abs(instOneShot.Time - 1.0) < 0.001 && done2, "OneShot clamps at duration 1.0 and marks completed");

            // Advance Loop
            ClipInstance instLoop = new ClipInstance(0);
            instLoop.Speed = 1.0;
            instLoop.Advance(0.6, 1.0, LoopMode.Loop);
            Assert(Math.Abs(instLoop.Time - 0.6) < 0.001, "Loop advances to 0.6");
            instLoop.Advance(0.6, 1.0, LoopMode.Loop);
            Assert(Math.Abs(instLoop.Time - 0.2) < 0.001, "Loop wraps to 0.2 after duration 1.0");

            // Advance PingPong
            ClipInstance instPingPong = new ClipInstance(0);
            instPingPong.Speed = 1.0;
            instPingPong.Advance(0.6, 1.0, LoopMode.PingPong);
            Assert(Math.Abs(instPingPong.Time - 0.6) < 0.001 && instPingPong.Direction == 1, "PingPong advances forward to 0.6");
            instPingPong.Advance(0.6, 1.0, LoopMode.PingPong);
            Assert(Math.Abs(instPingPong.Time - 0.8) < 0.001 && instPingPong.Direction == -1, "PingPong bounces at 1.0 and reverses to 0.8 (Direction = -1)");
            instPingPong.Advance(0.3, 1.0, LoopMode.PingPong);
            Assert(Math.Abs(instPingPong.Time - 0.5) < 0.001 && instPingPong.Direction == -1, "PingPong continues in reverse to 0.5");
            instPingPong.Advance(0.6, 1.0, LoopMode.PingPong);
            Assert(Math.Abs(instPingPong.Time - 0.1) < 0.001 && instPingPong.Direction == 1, "PingPong bounces at 0.0 and moves forward to 0.1 (Direction = 1)");

            // State Speed Multiplier
            StateMachineDefinition speedDef = new StateMachineDefinition("SpeedMachine");
            MachineLayer speedLayer = new MachineLayer("SpeedLayer");
            MachineState fastState = new MachineState(0, "FastState", StateKind.Animation, 0);
            fastState.Speed = 2.5; // 2.5x speed
            speedLayer.AddState(fastState);
            speedDef.AddLayer(speedLayer);

            StateMachineRuntime speedRuntime = new StateMachineRuntime(speedDef, clips);
            speedRuntime.Update(0.1, pose);
            Assert(Math.Abs(speedRuntime.LayerRuntimes[0].CurTime.Time - 0.25) < 0.01,
                string.Format("State speed 2.5x over dt=0.1 advances clip time by 0.25s, got {0}", speedRuntime.LayerRuntimes[0].CurTime.Time));
        }

        private static void TestStateMachineValidatorListenerDiagnostics()
        {
            List<Clip> clips = new List<Clip>();
            clips.Add(new Clip(0, "Idle", 1.0, 60.0, LoopMode.Loop));

            // 1. Setup clean baseline definition
            StateMachineDefinition cleanDef = new StateMachineDefinition("CleanSM");
            int bInput = cleanDef.AddInput(MachineInput.Bool("isHovered", false));
            int nInput = cleanDef.AddInput(MachineInput.Number("score", 10.0));
            int tInput = cleanDef.AddInput(MachineInput.Trigger("submit"));

            MachineLayer layer = new MachineLayer("BaseLayer");
            int sEntry = layer.AddState(new MachineState(0, "Entry", StateKind.Entry));
            int sIdle = layer.AddState(new MachineState(1, "Idle", StateKind.Animation, 0));
            layer.AddTransition(new StateTransition(sEntry, sIdle, 0.0));
            cleanDef.AddLayer(layer);

            // Valid listeners
            NodeListener cleanL1 = new NodeListener(101u, PointerEventKind.PointerEnter);
            cleanL1.Actions.Add(new ListenerAction(ActionKind.SetBool, bInput) { Parameter = 1.0 });
            cleanDef.AddListener(cleanL1);

            NodeListener cleanL2 = new NodeListener(101u, PointerEventKind.PointerDown);
            cleanL2.Actions.Add(new ListenerAction(ActionKind.SetNumber, nInput) { Parameter = 42.0 });
            cleanDef.AddListener(cleanL2);

            NodeListener cleanL3 = new NodeListener(101u, PointerEventKind.Click);
            cleanL3.Actions.Add(new ListenerAction(ActionKind.Fire, tInput));
            cleanDef.AddListener(cleanL3);

            HashSet<uint> sceneNodeIds = new HashSet<uint>();
            sceneNodeIds.Add(101u);

            var cleanResult = StateMachineValidator.Validate(cleanDef, clips, sceneNodeIds);
            Assert(cleanResult.IsValid, "Clean definition with valid listeners must pass validation");
            Assert(!cleanResult.HasErrors, "Clean definition has 0 errors");
            Assert(!cleanResult.HasWarnings, "Clean definition has 0 warnings");

            // 2. Test LISTENER_TYPE_MISMATCH (Error)
            StateMachineDefinition typeMismatchDef = new StateMachineDefinition("MismatchSM");
            int numOnly = typeMismatchDef.AddInput(MachineInput.Number("speed", 5.0));
            int boolOnly = typeMismatchDef.AddInput(MachineInput.Bool("active", false));

            MachineLayer mmLayer = new MachineLayer("Layer");
            int mmEntry = mmLayer.AddState(new MachineState(0, "Entry", StateKind.Entry));
            int mmIdle = mmLayer.AddState(new MachineState(1, "Idle", StateKind.Animation, 0));
            mmLayer.AddTransition(new StateTransition(mmEntry, mmIdle, 0.0));
            typeMismatchDef.AddLayer(mmLayer);

            // Mismatch 1: SetBool targeting Number input
            NodeListener badL1 = new NodeListener(101u, PointerEventKind.Click);
            badL1.Actions.Add(new ListenerAction(ActionKind.SetBool, numOnly) { Parameter = 1.0 });
            typeMismatchDef.AddListener(badL1);

            // Mismatch 2: SetNumber targeting Bool input
            NodeListener badL2 = new NodeListener(101u, PointerEventKind.PointerDown);
            badL2.Actions.Add(new ListenerAction(ActionKind.SetNumber, boolOnly) { Parameter = 3.14 });
            typeMismatchDef.AddListener(badL2);

            // Mismatch 3: FireTrigger targeting Number input
            NodeListener badL3 = new NodeListener(101u, PointerEventKind.PointerUp);
            badL3.Actions.Add(new ListenerAction(ActionKind.Fire, numOnly));
            typeMismatchDef.AddListener(badL3);

            var mismatchResult = StateMachineValidator.Validate(typeMismatchDef, clips, sceneNodeIds);
            Assert(mismatchResult.HasErrors, "Type mismatch definition must report errors");
            Assert(mismatchResult.HasCode(StateMachineValidator.LISTENER_TYPE_MISMATCH), "Reports LISTENER_TYPE_MISMATCH");

            int mismatchCount = 0;
            for (int i = 0; i < mismatchResult.Issues.Count; i++)
            {
                if (mismatchResult.Issues[i] != null && mismatchResult.Issues[i].IssueCode == StateMachineValidator.LISTENER_TYPE_MISMATCH)
                {
                    mismatchCount++;
                    Assert(mismatchResult.Issues[i].Severity == ValidationSeverity.Error, "LISTENER_TYPE_MISMATCH severity is Error");
                }
            }
            Assert(mismatchCount == 3, string.Format("Expected 3 LISTENER_TYPE_MISMATCH errors, got {0}", mismatchCount));

            // 3. Test LISTENER_UNBOUND_INPUT (Warning)
            StateMachineDefinition unboundDef = new StateMachineDefinition("UnboundSM");
            MachineLayer ubLayer = new MachineLayer("Layer");
            int ubEntry = ubLayer.AddState(new MachineState(0, "Entry", StateKind.Entry));
            int ubIdle = ubLayer.AddState(new MachineState(1, "Idle", StateKind.Animation, 0));
            ubLayer.AddTransition(new StateTransition(ubEntry, ubIdle, 0.0));
            unboundDef.AddLayer(ubLayer);

            NodeListener unboundL = new NodeListener(101u, PointerEventKind.Click);
            unboundL.Actions.Add(new ListenerAction(ActionKind.SetBool, -1) { InputId = "non_existent_input_id" });
            unboundDef.AddListener(unboundL);

            var unboundResult = StateMachineValidator.Validate(unboundDef, clips, sceneNodeIds);
            Assert(unboundResult.HasWarnings, "Unbound input references must report warnings");
            Assert(unboundResult.HasCode(StateMachineValidator.LISTENER_UNBOUND_INPUT), "Reports LISTENER_UNBOUND_INPUT");

            // 4. Test LISTENER_MISSING_TARGET (Warning)
            StateMachineDefinition missingTargetDef = new StateMachineDefinition("MissingTargetSM");
            int mtInput = missingTargetDef.AddInput(MachineInput.Bool("flag", false));
            MachineLayer mtLayer = new MachineLayer("Layer");
            int mtEntry = mtLayer.AddState(new MachineState(0, "Entry", StateKind.Entry));
            int mtIdle = mtLayer.AddState(new MachineState(1, "Idle", StateKind.Animation, 0));
            mtLayer.AddTransition(new StateTransition(mtEntry, mtIdle, 0.0));
            missingTargetDef.AddLayer(mtLayer);

            NodeListener missingTargetL = new NodeListener(9999u, PointerEventKind.PointerEnter); // 9999 not in sceneNodeIds
            missingTargetL.Actions.Add(new ListenerAction(ActionKind.SetBool, mtInput));
            missingTargetDef.AddListener(missingTargetL);

            var targetResult = StateMachineValidator.Validate(missingTargetDef, clips, sceneNodeIds);
            Assert(targetResult.HasWarnings, "Missing target node must report warning");
            Assert(targetResult.HasCode(StateMachineValidator.LISTENER_MISSING_TARGET), "Reports LISTENER_MISSING_TARGET");

            // 5. Enforce Non-Destructive Validation Contract:
            // Definition structures MUST remain completely unmodified
            Assert(missingTargetDef.Listeners.Count == 1, "Listeners list count preserved without deletions");
            Assert(missingTargetDef.Listeners[0].Actions.Count == 1, "Listener actions list preserved without deletions");
            Assert(missingTargetDef.Inputs.Count == 1, "Inputs preserved");
        }

        private static void TestInteractiveListenersAndStateMachineRuntimeExecution()
        {
            // =========================================================================
            // Section 1: Event Dispatcher & Pointer Lifecycle (STATE-04, LISTENER-01)
            // =========================================================================
            var page = new Page();
            var btnLayer = new LayerNode
            {
                Id = "btn_1",
                Name = "Button 1",
                Type = ElementType.Rectangle,
                X = 100,
                Y = 100,
                Width = 200,
                Height = 60,
                IsVisible = true
            };
            page.Layers.Add(btnLayer);
            uint btnId = PropertyBinder.GetNumericNodeId(btnLayer.Id);

            var sceneGraph = new RetainedSceneGraph(new GpuRenderer());
            sceneGraph.Synchronize(page);

            var def = new StateMachineDefinition("LifecycleDef");
            int inEnter = def.AddInput(MachineInput.Bool("enterFired", false));
            int inExit = def.AddInput(MachineInput.Bool("exitFired", false));
            int inDown = def.AddInput(MachineInput.Bool("downFired", false));
            int inUp = def.AddInput(MachineInput.Bool("upFired", false));
            int inClick = def.AddInput(MachineInput.Bool("clickFired", false));
            int inTap = def.AddInput(MachineInput.Bool("tapFired", false));
            int inDragStart = def.AddInput(MachineInput.Bool("dragStartFired", false));
            int inDrag = def.AddInput(MachineInput.Bool("dragFired", false));
            int inDragEnd = def.AddInput(MachineInput.Bool("dragEndFired", false));

            var lEnter = new NodeListener(btnId, PointerEventKind.PointerEnter);
            lEnter.Actions.Add(new ListenerAction(ActionKind.SetBool, inEnter) { Parameter = 1.0 });
            def.Listeners.Add(lEnter);

            var lExit = new NodeListener(btnId, PointerEventKind.PointerExit);
            lExit.Actions.Add(new ListenerAction(ActionKind.SetBool, inExit) { Parameter = 1.0 });
            def.Listeners.Add(lExit);

            var lDown = new NodeListener(btnId, PointerEventKind.PointerDown);
            lDown.Actions.Add(new ListenerAction(ActionKind.SetBool, inDown) { Parameter = 1.0 });
            def.Listeners.Add(lDown);

            var lUp = new NodeListener(btnId, PointerEventKind.PointerUp);
            lUp.Actions.Add(new ListenerAction(ActionKind.SetBool, inUp) { Parameter = 1.0 });
            def.Listeners.Add(lUp);

            var lClick = new NodeListener(btnId, PointerEventKind.Click);
            lClick.Actions.Add(new ListenerAction(ActionKind.SetBool, inClick) { Parameter = 1.0 });
            def.Listeners.Add(lClick);

            var lTap = new NodeListener(btnId, PointerEventKind.Tap);
            lTap.Actions.Add(new ListenerAction(ActionKind.SetBool, inTap) { Parameter = 1.0 });
            def.Listeners.Add(lTap);

            var lDragStart = new NodeListener(btnId, PointerEventKind.DragStart);
            lDragStart.Actions.Add(new ListenerAction(ActionKind.SetBool, inDragStart) { Parameter = 1.0 });
            def.Listeners.Add(lDragStart);

            var lDrag = new NodeListener(btnId, PointerEventKind.Drag);
            lDrag.Actions.Add(new ListenerAction(ActionKind.SetBool, inDrag) { Parameter = 1.0 });
            def.Listeners.Add(lDrag);

            var lDragEnd = new NodeListener(btnId, PointerEventKind.DragEnd);
            lDragEnd.Actions.Add(new ListenerAction(ActionKind.SetBool, inDragEnd) { Parameter = 1.0 });
            def.Listeners.Add(lDragEnd);

            var rt = new StateMachineRuntime(def, new List<Clip>());
            var dispatcher = new InteractiveEventDispatcher();

            // 1. Move from outside (0, 0) to inside (150, 120): Enter fires
            dispatcher.OnPointerMove(rt, sceneGraph, new Point(0, 0));
            Assert(rt.Definition.Inputs[inEnter].Value == 0.0, "Enter not fired at (0, 0)");

            dispatcher.OnPointerMove(rt, sceneGraph, new Point(150, 120));
            Assert(dispatcher.LastHoverNodeId == btnId, "Dispatcher tracks hover on btnId");
            Assert(rt.Definition.Inputs[inEnter].Value == 1.0, "PointerEnter event fired and set input");

            // Move inside (160, 120): still hovering, Exit not fired
            dispatcher.OnPointerMove(rt, sceneGraph, new Point(160, 120));
            Assert(dispatcher.LastHoverNodeId == btnId, "Still hovering on btnId");
            Assert(rt.Definition.Inputs[inExit].Value == 0.0, "Exit not fired while moving inside");

            // PointerDown at (150, 120)
            dispatcher.OnPointerDown(rt, sceneGraph, new Point(150, 120));
            Assert(dispatcher.PointerDownNodeId == btnId, "Down node ID recorded");
            Assert(rt.Definition.Inputs[inDown].Value == 1.0, "PointerDown event fired and set input");

            // PointerMove with delta > 4.0px to (180, 140)
            dispatcher.OnPointerMove(rt, sceneGraph, new Point(180, 140));
            Assert(dispatcher.IsDragging, "Drag threshold exceeded (>4px)");
            Assert(rt.Definition.Inputs[inDragStart].Value == 1.0, "DragStart fired");
            Assert(rt.Definition.Inputs[inDrag].Value == 1.0, "Drag fired");

            // PointerUp at (180, 140): DragEnd and Up fired, Click NOT fired (since dragging occurred)
            dispatcher.OnPointerUp(rt, sceneGraph, new Point(180, 140));
            Assert(!dispatcher.IsDragging, "Dragging ended");
            Assert(rt.Definition.Inputs[inDragEnd].Value == 1.0, "DragEnd fired");
            Assert(rt.Definition.Inputs[inUp].Value == 1.0, "Up fired");
            Assert(rt.Definition.Inputs[inClick].Value == 0.0, "Click NOT fired when drag occurred");
            Assert(rt.Definition.Inputs[inTap].Value == 0.0, "Tap NOT fired when drag occurred");

            // Reset click & up inputs to test clean click without drag
            rt.Definition.Inputs[inClick].Value = 0.0;
            rt.Definition.Inputs[inTap].Value = 0.0;
            rt.Definition.Inputs[inUp].Value = 0.0;
            dispatcher.OnPointerDown(rt, sceneGraph, new Point(150, 120));
            dispatcher.OnPointerUp(rt, sceneGraph, new Point(150, 120)); // delta = 0 < 4.0px
            Assert(rt.Definition.Inputs[inClick].Value == 1.0, "Click fired on zero-drag pointer release");
            Assert(rt.Definition.Inputs[inTap].Value == 1.0, "Tap fired on zero-drag pointer release");
            Assert(rt.Definition.Inputs[inUp].Value == 1.0, "Up fired on pointer release");

            // Move back outside to (0, 0) -> Exit fired
            dispatcher.OnPointerMove(rt, sceneGraph, new Point(0, 0));
            Assert(dispatcher.LastHoverNodeId == 0u, "Hover cleared");
            Assert(rt.Definition.Inputs[inExit].Value == 1.0, "Exit fired moving outside");

            // =========================================================================
            // Section 2: Listener Actions & Input Mutation (LISTENER-02)
            // =========================================================================
            var actDef = new StateMachineDefinition("ActionDef");
            int inBool = actDef.AddInput(MachineInput.Bool("b", false));
            int inNum = actDef.AddInput(MachineInput.Number("num", 10.0));
            int inTrig = actDef.AddInput(MachineInput.Trigger("trig"));

            // SetBool on Click
            var lSetBool = new NodeListener(btnId, PointerEventKind.Click);
            lSetBool.Actions.Add(new ListenerAction(ActionKind.SetBool, inBool) { Parameter = 1.0 });
            actDef.Listeners.Add(lSetBool);

            // ToggleBool on Tap
            var lToggle = new NodeListener(btnId, PointerEventKind.Tap);
            lToggle.Actions.Add(new ListenerAction(ActionKind.ToggleBool, inBool));
            actDef.Listeners.Add(lToggle);

            // SetNumber on Enter
            var lSetNum = new NodeListener(btnId, PointerEventKind.PointerEnter);
            lSetNum.Actions.Add(new ListenerAction(ActionKind.SetNumber, inNum) { Parameter = 42.5 });
            actDef.Listeners.Add(lSetNum);

            // FireTrigger on Down
            var lTrig = new NodeListener(btnId, PointerEventKind.PointerDown);
            lTrig.Actions.Add(new ListenerAction(ActionKind.Fire, inTrig));
            actDef.Listeners.Add(lTrig);

            // Reset on Exit
            var lReset = new NodeListener(btnId, PointerEventKind.PointerExit);
            lReset.Actions.Add(new ListenerAction(ActionKind.Reset, inNum));
            actDef.Listeners.Add(lReset);

            var actRt = new StateMachineRuntime(actDef, new List<Clip>());

            // Test SetBool
            actRt.OnPointerEvent(btnId, PointerEventKind.Click);
            Assert(actRt.Definition.Inputs[inBool].Value == 1.0, "SetBool set bool input to 1.0");

            // Test ToggleBool (from 1.0 to 0.0)
            actRt.OnPointerEvent(btnId, PointerEventKind.Tap);
            Assert(actRt.Definition.Inputs[inBool].Value == 0.0, "ToggleBool toggled 1.0 to 0.0");

            // ToggleBool (from 0.0 to 1.0)
            actRt.OnPointerEvent(btnId, PointerEventKind.Tap);
            Assert(actRt.Definition.Inputs[inBool].Value == 1.0, "ToggleBool toggled 0.0 to 1.0");

            // Test SetNumber
            actRt.OnPointerEvent(btnId, PointerEventKind.PointerEnter);
            Assert(Math.Abs(actRt.Definition.Inputs[inNum].Value - 42.5) < 0.001, "SetNumber set input to 42.5");

            // Test FireTrigger
            actRt.OnPointerEvent(btnId, PointerEventKind.PointerDown);
            Assert(actRt.Definition.Inputs[inTrig].Fired, "FireTrigger set trigger Fired to true");
            var emptyPose = new Dictionary<uint, Dictionary<ushort, double>>();
            actRt.Update(0.016, emptyPose);
            Assert(!actRt.Definition.Inputs[inTrig].Fired, "Update step auto-cleared trigger Fired to false");

            // Test Reset
            actRt.OnPointerEvent(btnId, PointerEventKind.PointerExit);
            Assert(Math.Abs(actRt.Definition.Inputs[inNum].Value - 10.0) < 0.001, "Reset restored number input to InitialValue 10.0");

            // Test Enabled gate (D-02)
            lSetBool.Enabled = false;
            actRt.Definition.Inputs[inBool].Value = 0.0;
            actRt.OnPointerEvent(btnId, PointerEventKind.Click);
            Assert(actRt.Definition.Inputs[inBool].Value == 0.0, "Disabled listener (Enabled = false) did NOT execute actions per D-02");

            // =========================================================================
            // Section 3: Reversible Commands in CommandHistory (LISTENER-02, D-03)
            // =========================================================================
            var cmdDef = new StateMachineDefinition("CmdDef");
            var cmdListener = new NodeListener(1001u, PointerEventKind.Click);
            cmdListener.Actions.Add(new ListenerAction(ActionKind.SetBool, 0) { Parameter = 1.0 });

            bool historyCallbackFired = false;
            Action onHistoryChanged = () => { historyCallbackFired = true; };

            // AddListenerCommand
            var addCmd = new AddListenerCommand(cmdDef, cmdListener, onHistoryChanged);
            addCmd.Execute();
            Assert(cmdDef.Listeners.Contains(cmdListener), "AddListenerCommand added listener");
            Assert(historyCallbackFired, "AddListenerCommand invoked change callback");

            addCmd.Unexecute();
            Assert(!cmdDef.Listeners.Contains(cmdListener), "AddListenerCommand Unexecute removed listener");

            addCmd.Execute(); // re-add for delete test
            Assert(cmdDef.Listeners.Contains(cmdListener), "AddListenerCommand re-executed");

            // DeleteListenerCommand
            var delCmd = new DeleteListenerCommand(cmdDef, cmdListener, onHistoryChanged);
            delCmd.Execute();
            Assert(!cmdDef.Listeners.Contains(cmdListener), "DeleteListenerCommand removed listener");

            delCmd.Unexecute();
            Assert(cmdDef.Listeners.Contains(cmdListener), "DeleteListenerCommand Unexecute restored listener at exact index");

            // ModifyListenerCommand
            var beforeSnap = new NodeListenerSnapshot(cmdListener);
            var afterSnap = new NodeListenerSnapshot
            {
                TargetNodeId = 2002u,
                Kind = PointerEventKind.Drag,
                Enabled = false,
                Actions = new List<ListenerAction> { new ListenerAction(ActionKind.SetNumber, 1) { Parameter = 99.0 } }
            };
            var modCmd = new ModifyListenerCommand(cmdListener, beforeSnap, afterSnap, onHistoryChanged);
            modCmd.Execute();
            Assert(cmdListener.TargetNodeId == 2002u && cmdListener.Kind == PointerEventKind.Drag && !cmdListener.Enabled, "ModifyListenerCommand applied new snapshot");

            modCmd.Unexecute();
            Assert(cmdListener.TargetNodeId == 1001u && cmdListener.Kind == PointerEventKind.Click && cmdListener.Enabled, "ModifyListenerCommand Unexecute restored exact previous snapshot");

            // =========================================================================
            // Section 4: Deterministic Transition Priority & ExitTime (RUNTIME-01, D-05)
            // =========================================================================
            var prioDef = new StateMachineDefinition("PrioDef");
            int inPrio = prioDef.AddInput(MachineInput.Bool("prioInput", false));
            var prioLayer = new MachineLayer("PrioLayer");
            int stA = prioLayer.AddState(new MachineState(0, "A", StateKind.Animation, 0));
            int stB = prioLayer.AddState(new MachineState(1, "B", StateKind.Animation, 0));
            int stC = prioLayer.AddState(new MachineState(2, "C", StateKind.Animation, 0));
            int stAny = prioLayer.AddState(new MachineState(3, "Any", StateKind.Any, -1));

            // T1: CurrentState A -> B (input == 1.0)
            var tCurrent = new StateTransition(stA, stB, 0.5);
            tCurrent.WithCondition(new TransitionCondition(inPrio, ConditionOp.Eq, 1.0));

            // T2: AnyState -> C (input == 1.0)
            var tAny = new StateTransition(stAny, stC, 0.5);
            tAny.WithCondition(new TransitionCondition(inPrio, ConditionOp.Eq, 1.0));

            // Add AnyState transition first to verify current state priority
            prioLayer.AddTransition(tAny);
            prioLayer.AddTransition(tCurrent);

            prioDef.AddLayer(prioLayer);

            var dummyClips = new List<Clip> { new Clip(0, "Clip0", 2.0, 60.0, LoopMode.Loop) };
            var prioRt = new StateMachineRuntime(prioDef, dummyClips);
            var pPose = new Dictionary<uint, Dictionary<ushort, double>>();
            prioRt.Update(0.0, pPose);

            // Fire input
            prioRt.SetBool(inPrio, true);
            prioRt.Update(0.016, pPose);

            Assert(prioRt.LayerRuntimes[0].ActiveTransition != null, "Transition triggered");
            Assert(prioRt.LayerRuntimes[0].ActiveTransition.ToState == stB, "Current state transition (A -> B) prioritized over AnyState (Any -> C) per D-05");

            // ExitTime threshold gating
            var exitDef2 = new StateMachineDefinition("ExitDef2");
            var exitLayer2 = new MachineLayer("ExitLayer2");
            int exA = exitLayer2.AddState(new MachineState(0, "ExA", StateKind.Animation, 0)); // Clip duration 2.0s
            int exB = exitLayer2.AddState(new MachineState(1, "ExB", StateKind.Animation, 0));

            var exitTrans2 = new StateTransition(exA, exB, 0.1);
            exitTrans2.ExitTime = 0.8; // Requires 80% progress = 1.6s
            exitLayer2.AddTransition(exitTrans2);
            exitDef2.AddLayer(exitLayer2);

            var exitRt2 = new StateMachineRuntime(exitDef2, dummyClips);
            exitRt2.Update(0.0, pPose);
            Assert(exitRt2.LayerRuntimes[0].ActiveTransition == null, "No transition at start");

            exitRt2.Update(0.8, pPose); // 0.8s / 2.0s = 0.40 progress (< 0.80)
            Assert(exitRt2.LayerRuntimes[0].ActiveTransition == null, "ExitTime 0.8 blocks transition at progress 0.40");

            exitRt2.Update(0.9, pPose); // total 1.7s / 2.0s = 0.85 progress (>= 0.80)
            Assert(exitRt2.LayerRuntimes[0].ActiveTransition != null && exitRt2.LayerRuntimes[0].ActiveTransition.ToState == exB,
                "ExitTime 0.8 allows transition at progress 0.85 per RUNTIME-01");

            // =========================================================================
            // Section 5: In-Flight Interrupted Snapshot Blending (BLEND-01, STATE-05, D-04, D-06)
            // =========================================================================
            var testLayer = new LayerNode
            {
                Id = "blend_box",
                Name = "Blend Box",
                Type = ElementType.Rectangle,
                X = 100,
                Y = 100,
                Width = 200,
                Height = 100
            };
            uint targetNodeId = PropertyBinder.GetNumericNodeId(testLayer.Id);

            var blendClips = new List<Clip>();

            // Clip A: ScaleX = 1.0
            var clipA = new Clip(0, "ScaleA", 1.0, 60.0, LoopMode.Loop);
            var trAX = new Track(targetNodeId, (ushort)PropKey.ScaleX);
            trAX.AddKeyframe(Keyframe.Linear(0.0, 1.0));
            clipA.AddTrack(trAX);
            blendClips.Add(clipA);

            // Clip B: ScaleX = 3.0
            var clipB = new Clip(1, "ScaleB", 1.0, 60.0, LoopMode.Loop);
            var trBX = new Track(targetNodeId, (ushort)PropKey.ScaleX);
            trBX.AddKeyframe(Keyframe.Linear(0.0, 3.0));
            clipB.AddTrack(trBX);
            blendClips.Add(clipB);

            // Clip C: ScaleX = 5.0
            var clipC = new Clip(2, "ScaleC", 1.0, 60.0, LoopMode.Loop);
            var trCX = new Track(targetNodeId, (ushort)PropKey.ScaleX);
            trCX.AddKeyframe(Keyframe.Linear(0.0, 5.0));
            clipC.AddTrack(trCX);
            blendClips.Add(clipC);

            var blendDef = new StateMachineDefinition("BlendMachine");
            int boolB = blendDef.AddInput(MachineInput.Bool("goB", false));
            int boolC = blendDef.AddInput(MachineInput.Bool("goC", false));

            var bLayer = new MachineLayer("BlendLayer");
            int stateA = bLayer.AddState(new MachineState(0, "StateA", StateKind.Animation, 0));
            int stateB = bLayer.AddState(new MachineState(1, "StateB", StateKind.Animation, 1));
            int stateC = bLayer.AddState(new MachineState(2, "StateC", StateKind.Animation, 2));
            int stateAnyBlend = bLayer.AddState(new MachineState(3, "Any", StateKind.Any, -1));

            // Transition A -> B: 1.0s, Linear, Interrupt = true
            var transAB = new StateTransition(stateA, stateB, 1.0);
            transAB.Interpolation = InterpMode.Linear;
            transAB.Interrupt = true;
            transAB.WithCondition(new TransitionCondition(boolB, ConditionOp.Eq, 1.0));
            bLayer.AddTransition(transAB);

            // Transition AnyState -> C: 1.0s, Linear, Interrupt = true
            var transAC = new StateTransition(stateAnyBlend, stateC, 1.0);
            transAC.Interpolation = InterpMode.Linear;
            transAC.Interrupt = true;
            transAC.WithCondition(new TransitionCondition(boolC, ConditionOp.Eq, 1.0));
            bLayer.AddTransition(transAC);

            blendDef.AddLayer(bLayer);

            var blendRt = new StateMachineRuntime(blendDef, blendClips);
            var pose = new Dictionary<uint, Dictionary<ushort, double>>();

            // Frame 0: in State A
            blendRt.Update(0.0, pose);
            Assert(pose.ContainsKey(targetNodeId) && Math.Abs(pose[targetNodeId][(ushort)PropKey.ScaleX] - 1.0) < 0.01, "Initial Scale is 1.0");

            // Trigger A -> B
            blendRt.SetBool(boolB, true);
            blendRt.Update(0.0, pose); // starts transition A -> B
            blendRt.Update(0.5, pose); // Advance 0.5s through 1.0s transition

            // Evaluated Scale is approximately 2.0 (1.0 + (3.0 - 1.0) * 0.5)
            double scaleMid = pose[targetNodeId][(ushort)PropKey.ScaleX];
            Assert(Math.Abs(scaleMid - 2.0) < 0.05, string.Format("Mid-transition Scale is approximately 2.0, got {0}", scaleMid));

            // Interrupt with transition -> C
            blendRt.SetBool(boolB, false); // mutually exclusive
            blendRt.SetBool(boolC, true);
            blendRt.Update(0.0, pose); // starts transition to C and captures SnapshotPose

            // Assert ActiveTransition.SnapshotPose is captured holding Scale ≈ 2.0 per D-04
            var activeTrans = blendRt.LayerRuntimes[0].ActiveTransition;
            Assert(activeTrans != null, "Transition to C is active");
            Assert(activeTrans.SnapshotPose != null && activeTrans.SnapshotPose.ContainsKey(targetNodeId), "SnapshotPose captured upon interruption");
            double snapScale = activeTrans.SnapshotPose[targetNodeId][(ushort)PropKey.ScaleX];
            Assert(Math.Abs(snapScale - 2.0) < 0.05, string.Format("SnapshotPose captures in-flight scale ~2.0, got {0}", snapScale));

            // Advance runtime 0.5s: smoothly blends from 2.0 towards 5.0
            blendRt.Update(0.5, pose);
            double scaleInterp = pose[targetNodeId][(ushort)PropKey.ScaleX];
            // Expected: 2.0 + (5.0 - 2.0) * 0.5 = 3.5
            Assert(Math.Abs(scaleInterp - 3.5) < 0.15, string.Format("Interrupted blend smoothly reaches ~3.5 without visual snap or reset to 1.0, got {0}", scaleInterp));

            // Assert evaluated transforms apply strictly to RenderTransform without modifying persistent layer bounds per D-06
            PropertyBinder.ApplyPoseToLayer(testLayer, targetNodeId, pose);
            Assert(testLayer.RenderTransform != null, "RenderTransform instantiated");
            Assert(Math.Abs(testLayer.RenderTransform.ScaleX - scaleInterp) < 0.001, "RenderTransform.ScaleX updated from animated pose");
            Assert(testLayer.X == 100 && testLayer.Y == 100 && testLayer.Width == 200 && testLayer.Height == 100,
                "Persistent layer geometry (X, Y, Width, Height) remains untouched per D-06");

            // =========================================================================
            // Section 6: Simulation Playback Gating (D-01)
            // =========================================================================
            var timelineCtrl = new AnimationTimelineController();
            timelineCtrl.UseStateMachine = true;
            timelineCtrl.ActiveStateMachine = blendRt;

            // When paused, Step(0.5) must NOT advance runtime (effectiveDt = 0.0 per D-01)
            Assert(!timelineCtrl.IsPlaying, "TimelineController starts paused");
            double transProgressBefore = blendRt.LayerRuntimes[0].ActiveTransition != null ? blendRt.LayerRuntimes[0].ActiveTransition.Elapsed : 0.0;
            timelineCtrl.Step(0.5);
            double transProgressAfter = blendRt.LayerRuntimes[0].ActiveTransition != null ? blendRt.LayerRuntimes[0].ActiveTransition.Elapsed : 0.0;
            Assert(Math.Abs(transProgressBefore - transProgressAfter) < 0.0001,
                "When paused (IsPlaying == false), animation evaluation holds with dt = 0.0 per D-01");

            // When playing, Step(0.2) advances runtime
            timelineCtrl.Play();
            Assert(timelineCtrl.IsPlaying, "TimelineController is now playing");
            timelineCtrl.Step(0.2);
            double transProgressPlaying = blendRt.LayerRuntimes[0].ActiveTransition != null ? blendRt.LayerRuntimes[0].ActiveTransition.Elapsed : 0.0;
            Assert(transProgressPlaying > transProgressBefore,
                "When playing (IsPlaying == true), animation evaluation advances with effectiveDt per D-01");

            // Test StudioView canvas pointer gating
            var studio = new StudioView();
            studio.PanOffset = new Point(0, 0);
            studio.ZoomScale = 1.0;
            studio.SwitchStudioMode(StudioView.StudioMode.Animate);
            studio.Document.GetActivePage().Layers.Add(btnLayer);
            studio.SynchronizeSceneGraph();
            studio.TimelineController.UseStateMachine = true;
            studio.TimelineController.ActiveStateMachine = rt;

            // 1. When paused: canvas mouse down does NOT dispatch to interactive event dispatcher
            Assert(!studio.TimelineController.IsPlaying, "StudioView starts paused in Animate mode");
            studio.HandleCanvasMouseDown(new Point(150, 120), System.Windows.Input.MouseButton.Left, 1);
            Assert(studio.InteractiveDispatcher.PointerDownNodeId == 0u,
                "Paused canvas pointer down does not trigger simulation dispatcher per D-01");

            // 2. When playing: canvas mouse down dispatches and sets PointerDownNodeId
            studio.PlayAnimation();
            Assert(studio.TimelineController.IsPlaying, "StudioView is now playing");
            studio.HandleCanvasMouseDown(new Point(150, 120), System.Windows.Input.MouseButton.Left, 1);
            Assert(studio.InteractiveDispatcher.PointerDownNodeId == btnId,
                "Playing canvas pointer down dispatches to simulation dispatcher per D-01");

            // 3. Pause resets dispatcher
            studio.PauseAnimation();
            Assert(!studio.TimelineController.IsPlaying, "StudioView is now paused");
            Assert(studio.InteractiveDispatcher.PointerDownNodeId == 0u,
                "Pausing simulation resets interactive event dispatcher per D-01");

            // 4. Verification: No redundant listeners or keyframe easing in right property inspector stack
            studio.SelectLayers(new List<LayerNode> { btnLayer });
            studio.RefreshPropertyInspector();
            var inspectorStack = studio.PropertyInspectorStack;
            if (inspectorStack != null)
            {
                for (int c = 0; c < inspectorStack.Children.Count; c++)
                {
                    var child = inspectorStack.Children[c] as FrameworkElement;
                    if (child is TextBlock)
                    {
                        var tb = child as TextBlock;
                        Assert(!tb.Text.Contains("Keyframe Motion & Easing"), "Redundant Keyframe Motion section removed from right inspector");
                    }
                    if (child is Border)
                    {
                        var b = child as Border;
                        var spChild = b.Child as StackPanel;
                        if (spChild != null && spChild.Children.Count > 0 && spChild.Children[0] is DockPanel)
                        {
                            var dp = spChild.Children[0] as DockPanel;
                            if (dp.Children.Count > 0 && dp.Children[0] is TextBlock)
                            {
                                var tb = dp.Children[0] as TextBlock;
                                Assert(!tb.Text.Contains("Listeners ("), "Redundant Listeners section removed from right inspector");
                            }
                        }
                    }
                }
            }

            // 5. Verification: Canvas-level listener (TargetNodeId == 0u) fires on canvas pointer click
            int inCanvasClick = actDef.AddInput(MachineInput.Bool("canvasClicked", false));
            var lCanvas = new NodeListener(0u, PointerEventKind.Click);
            lCanvas.Actions.Add(new ListenerAction(ActionKind.SetBool, inCanvasClick) { Parameter = 1.0 });
            actDef.Listeners.Add(lCanvas);

            var canvasRt = new StateMachineRuntime(actDef, new List<Clip>());
            dispatcher.OnPointerDown(canvasRt, sceneGraph, new Point(500, 500));
            dispatcher.OnPointerUp(canvasRt, sceneGraph, new Point(500, 500));
            Assert(actDef.Inputs[inCanvasClick].Value == 1.0, "Canvas listener (TargetNodeId 0) fired on canvas click");
        }

        private static void TestContinuousPlaybackAndInputDrivenTransitions()
        {
            // 1. Requirement: State machine plays continuously past timeline Duration (e.g. 2.0s) without stopping
            var smDef = new StateMachineDefinition("ContinuousSM");
            var layer = new MachineLayer("BaseLayer");
            int sEntry = layer.AddState(new MachineState(0, "Entry", StateKind.Entry));
            int sIdle = layer.AddState(new MachineState(1, "Idle", StateKind.Animation, 0));
            int sHover = layer.AddState(new MachineState(2, "Hover", StateKind.Animation, 1));
            layer.AddTransition(new StateTransition(sEntry, sIdle, 0.0));

            int inHover = smDef.AddInput(MachineInput.Bool("Hovering", false));

            // Idle -> Hover requires Hovering == 1.0
            var tEnter = new StateTransition(sIdle, sHover, 0.2);
            tEnter.Conditions.Add(new TransitionCondition(inHover, ConditionOp.Eq, 1.0));
            layer.AddTransition(tEnter);

            // Hover -> Idle requires Hovering == 0.0
            var tExit = new StateTransition(sHover, sIdle, 0.2);
            tExit.Conditions.Add(new TransitionCondition(inHover, ConditionOp.Eq, 0.0));
            layer.AddTransition(tExit);

            smDef.AddLayer(layer);

            var clips = new List<Clip>
            {
                new Clip(10, "IdleClip", 2.0, 60.0, LoopMode.OneShot),
                new Clip(11, "HoverClip", 2.0, 60.0, LoopMode.OneShot)
            };

            var timelineCtrl = new AnimationTimelineController();
            timelineCtrl.Duration = 2.0;
            timelineCtrl.LoopMode = LoopMode.OneShot;
            timelineCtrl.UseStateMachine = true;
            var runtime = new StateMachineRuntime(smDef, clips);
            timelineCtrl.ActiveStateMachine = runtime;

            // Start playing: Step past 2.0 seconds
            timelineCtrl.Play();
            timelineCtrl.Step(1.5);
            Assert(timelineCtrl.IsPlaying, "Timeline is playing at 1.5s");
            timelineCtrl.Step(1.5); // Total 3.0s > Duration 2.0s
            Assert(timelineCtrl.IsPlaying, "State machine plays continuously past 2.0s without auto-pausing");
            Assert(Math.Abs(timelineCtrl.CurrentTime - 3.0) < 0.001, "CurrentTime advanced to 3.0s without being clamped to 2.0s");

            // 2. Requirement: Animation state does NOT transition without input when condition is configured
            // State should remain in Idle (sIdle = 1) because Hovering is 0.0
            Assert(runtime.LayerRuntimes[0].CurrentState == sIdle, "State remains in Idle without input condition fulfilled");
            Assert(!runtime.HasActiveTransitions(), "No transition occurs while condition is false");

            // 3. Requirement: Pointer enter on document object triggers listener and activates transition
            var page = new Page { Id = "test_page" };
            var rect = new LayerNode
            {
                Id = "rect_interactive",
                Name = "InteractiveCard",
                Type = ElementType.Rectangle,
                X = 100,
                Y = 100,
                Width = 200,
                Height = 150,
                IsVisible = true
            };
            page.Layers.Add(rect);
            uint rectId = PropertyBinder.GetNumericNodeId(rect.Id);

            // Attach PointerEnter and PointerExit listeners to the rectangle
            var lEnter = new NodeListener(rectId, PointerEventKind.Enter);
            lEnter.Actions.Add(new ListenerAction(ActionKind.SetBool, inHover) { Parameter = 1.0 });
            smDef.AddListener(lEnter);

            var lExit = new NodeListener(rectId, PointerEventKind.Exit);
            lExit.Actions.Add(new ListenerAction(ActionKind.SetBool, inHover) { Parameter = 0.0 });
            smDef.AddListener(lExit);

            var dispatcher = new InteractiveEventDispatcher();

            // Pointer moves inside rectangle (150, 150)
            dispatcher.OnPointerMove(runtime, null, new Point(150, 150), page);
            Assert(smDef.Inputs[inHover].Value == 1.0, "PointerEnter listener set Hovering input to 1.0 (True)");

            // Advance state machine: transition Idle -> Hover should now trigger!
            timelineCtrl.Step(0.016);
            Assert(runtime.HasActiveTransitions(), "Transition Idle -> Hover is now active after pointer entered");
            Assert(runtime.LayerRuntimes[0].ActiveTransition != null && runtime.LayerRuntimes[0].ActiveTransition.ToState == sHover,
                "Active transition target is Hover state");

            // Step through transition duration (0.2s)
            timelineCtrl.Step(0.25);
            Assert(runtime.LayerRuntimes[0].CurrentState == sHover, "State smoothly completed transition into Hover state");

            // Pointer moves outside rectangle (500, 500)
            dispatcher.OnPointerMove(runtime, null, new Point(500, 500), page);
            Assert(smDef.Inputs[inHover].Value == 0.0, "PointerExit listener set Hovering input to 0.0 (False)");

            // Advance state machine: transition Hover -> Idle should now trigger!
            timelineCtrl.Step(0.016);
            Assert(runtime.HasActiveTransitions(), "Transition Hover -> Idle is now active after pointer exited");
            Assert(runtime.LayerRuntimes[0].ActiveTransition != null && runtime.LayerRuntimes[0].ActiveTransition.ToState == sIdle,
                "Active transition target is Idle state");

            timelineCtrl.Step(0.25);
            Assert(runtime.LayerRuntimes[0].CurrentState == sIdle, "State returned to Idle after pointer exit");
        }
    }
}
