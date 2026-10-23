using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Kerf.Model;

namespace Kerf.Engine
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RustPoint2D
    {
        public double X;
        public double Y;

        public RustPoint2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public Point ToPoint()
        {
            return new Point(X, Y);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RustRect2D
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public Rect ToRect()
        {
            return new Rect(X, Y, Math.Max(0, Width), Math.Max(0, Height));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RustBezierNode
    {
        public double AnchorX;
        public double AnchorY;
        public double InX;
        public double InY;
        public double OutX;
        public double OutY;
        public int NodeType;

        public static RustBezierNode FromBezierNode(BezierNode n)
        {
            return new RustBezierNode
            {
                AnchorX = n.Anchor.X,
                AnchorY = n.Anchor.Y,
                InX = n.InHandle.X,
                InY = n.InHandle.Y,
                OutX = n.OutHandle.X,
                OutY = n.OutHandle.Y,
                NodeType = (int)n.Type
            };
        }
    }

    /// <summary>
    /// High-performance Rust Graphics and Vector Geometry acceleration interop.
    /// Safely loads kerf_core.dll and provides fallback if not present.
    /// </summary>
    public static class RustEngineInterop
    {
        private const string DllName = "kerf_core.dll";

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool FreeLibrary(IntPtr hModule);

        #region Native Exports

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_engine_init")]
        private static extern int NativeInit();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_engine_version")]
        private static extern IntPtr NativeVersion();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_engine_simd_desc")]
        private static extern IntPtr NativeSimdDesc();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_engine_free_string")]
        private static extern void NativeFreeString(IntPtr ptr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_path_compute_bounds")]
        private static extern int NativePathComputeBounds(
            [In] RustBezierNode[] nodes,
            int count,
            int isClosed,
            out RustRect2D outRect);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_bezier_adaptive_flatten")]
        private static extern int NativeAdaptiveFlatten(
            double p0x, double p0y,
            double p1x, double p1y,
            double p2x, double p2y,
            double p3x, double p3y,
            double tolerance,
            [Out] RustPoint2D[] outPoints,
            int maxPoints,
            out int outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_path_hit_test")]
        private static extern int NativePathHitTest(
            [In] RustBezierNode[] nodes,
            int count,
            int isClosed,
            double qx,
            double qy,
            double threshold,
            out int outPart,
            out int outIndex,
            out double outDist);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_path_length")]
        private static extern double NativePathLength(
            [In] RustBezierNode[] nodes,
            int count,
            int isClosed);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_simplify_points")]
        private static extern int NativeSimplifyPoints(
            [In] RustPoint2D[] points,
            int count,
            double epsilon,
            [Out] RustPoint2D[] outPoints,
            int maxPoints,
            out int outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_path_signed_area")]
        private static extern double NativePathSignedArea(
            [In] RustBezierNode[] nodes,
            int count,
            double tolerance);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_path_contains_point")]
        private static extern int NativePathContainsPoint(
            [In] RustBezierNode[] nodes,
            int count,
            double qx,
            double qy,
            double tolerance);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "kerf_convex_hull")]
        private static extern int NativeConvexHull(
            [In] RustPoint2D[] points,
            int count,
            [Out] RustPoint2D[] outPoints,
            int maxPoints,
            out int outCount);

        #endregion

        private static bool _isAvailable = false;
        private static string _version = "N/A";
        private static string _simdDesc = "Scalar";
        private static bool _initialized = false;
        private static readonly object _initLock = new object();

        static RustEngineInterop()
        {
            EnsureInitialized();
        }

        public static bool IsAvailable
        {
            get
            {
                EnsureInitialized();
                return _isAvailable;
            }
        }

        public static string Version
        {
            get
            {
                EnsureInitialized();
                return _version;
            }
        }

        public static string SimdDescription
        {
            get
            {
                EnsureInitialized();
                return _simdDesc;
            }
        }

        public static void EnsureInitialized()
        {
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;
                _initialized = true;

                try
                {
                    // Check if DLL exists in app directory or working dir
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string dllPath = Path.Combine(baseDir, DllName);

                    if (!File.Exists(dllPath))
                    {
                        dllPath = Path.Combine(Directory.GetCurrentDirectory(), DllName);
                    }

                    IntPtr hLib = IntPtr.Zero;
                    if (File.Exists(dllPath))
                    {
                        hLib = LoadLibrary(dllPath);
                    }

                    if (hLib != IntPtr.Zero || File.Exists(DllName))
                    {
                        int res = NativeInit();
                        if (res == 1)
                        {
                            _isAvailable = true;
                            IntPtr vPtr = NativeVersion();
                            if (vPtr != IntPtr.Zero)
                            {
                                _version = Marshal.PtrToStringAnsi(vPtr);
                            }

                            IntPtr sPtr = NativeSimdDesc();
                            if (sPtr != IntPtr.Zero)
                            {
                                _simdDesc = Marshal.PtrToStringAnsi(sPtr);
                                NativeFreeString(sPtr);
                            }
                        }
                    }
                }
                catch
                {
                    _isAvailable = false;
                }
            }
        }

        /// <summary>
        /// Exact mathematical cubic Bézier path bounds computed in Rust.
        /// </summary>
        public static bool TryComputePathBounds(VectorGeometry geo, out Rect bounds)
        {
            bounds = Rect.Empty;
            if (!IsAvailable || geo == null || geo.Nodes == null || geo.Nodes.Count == 0)
                return false;

            try
            {
                int count = geo.Nodes.Count;
                var nativeNodes = new RustBezierNode[count];
                for (int i = 0; i < count; i++)
                {
                    nativeNodes[i] = RustBezierNode.FromBezierNode(geo.Nodes[i]);
                }

                RustRect2D rustRect;
                int ok = NativePathComputeBounds(nativeNodes, count, geo.IsClosed ? 1 : 0, out rustRect);
                if (ok == 1)
                {
                    bounds = rustRect.ToRect();
                    return true;
                }
            }
            catch
            {
                // Fallback gracefully on exception
            }

            return false;
        }

        /// <summary>
        /// Exact path arc length in Rust.
        /// </summary>
        public static bool TryComputePathLength(VectorGeometry geo, out double length)
        {
            length = 0.0;
            if (!IsAvailable || geo == null || geo.Nodes == null || geo.Nodes.Count < 2)
                return false;

            try
            {
                int count = geo.Nodes.Count;
                var nativeNodes = new RustBezierNode[count];
                for (int i = 0; i < count; i++)
                {
                    nativeNodes[i] = RustBezierNode.FromBezierNode(geo.Nodes[i]);
                }

                length = NativePathLength(nativeNodes, count, geo.IsClosed ? 1 : 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adaptive curve flattening in Rust.
        /// </summary>
        public static List<Point> AdaptiveFlatten(Point p0, Point p1, Point p2, Point p3, double tolerance)
        {
            var result = new List<Point>();
            if (!IsAvailable)
            {
                // Managed fallback: 16-point subdivision
                for (int i = 0; i <= 16; i++)
                {
                    double t = i / 16.0;
                    double u = 1.0 - t;
                    double x = u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X;
                    double y = u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y;
                    result.Add(new Point(x, y));
                }
                return result;
            }

            try
            {
                var buffer = new RustPoint2D[128];
                int count;
                int ok = NativeAdaptiveFlatten(p0.X, p0.Y, p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, tolerance, buffer, buffer.Length, out count);
                if (ok == 1 && count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        result.Add(buffer[i].ToPoint());
                    }
                    return result;
                }
            }
            catch
            {
                // Fallback
            }

            result.Add(p0);
            result.Add(p3);
            return result;
        }

        /// <summary>
        /// Computes the signed area of a closed vector path using native Rust.
        /// Positive indicates CCW winding, negative indicates CW.
        /// </summary>
        public static bool TryComputeSignedArea(VectorGeometry geo, out double area)
        {
            area = 0.0;
            if (!IsAvailable || geo == null || geo.Nodes == null || geo.Nodes.Count < 3)
                return false;

            try
            {
                int count = geo.Nodes.Count;
                var nativeNodes = new RustBezierNode[count];
                for (int i = 0; i < count; i++)
                {
                    nativeNodes[i] = RustBezierNode.FromBezierNode(geo.Nodes[i]);
                }

                area = NativePathSignedArea(nativeNodes, count, 0.5);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Tests if a query point is inside a closed vector path using the native Rust winding number rule.
        /// </summary>
        public static bool TryPathContainsPoint(VectorGeometry geo, Point query, out bool contains)
        {
            contains = false;
            if (!IsAvailable || geo == null || geo.Nodes == null || geo.Nodes.Count < 3)
                return false;

            try
            {
                int count = geo.Nodes.Count;
                var nativeNodes = new RustBezierNode[count];
                for (int i = 0; i < count; i++)
                {
                    nativeNodes[i] = RustBezierNode.FromBezierNode(geo.Nodes[i]);
                }

                int inside = NativePathContainsPoint(nativeNodes, count, query.X, query.Y, 0.5);
                contains = (inside == 1);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Computes the minimal 2D Convex Hull of a collection of points using Andrew's Monotone Chain in Rust.
        /// </summary>
        public static List<Point> ComputeConvexHull(IEnumerable<Point> points)
        {
            var result = new List<Point>();
            if (points == null) return result;

            var inList = new List<Point>(points);
            if (inList.Count <= 2)
            {
                result.AddRange(inList);
                return result;
            }

            if (!IsAvailable)
            {
                // Fallback: return bounding box corners
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                for (int i = 0; i < inList.Count; i++)
                {
                    Point pt = inList[i];
                    if (pt.X < minX) minX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y > maxY) maxY = pt.Y;
                }
                result.Add(new Point(minX, minY));
                result.Add(new Point(maxX, minY));
                result.Add(new Point(maxX, maxY));
                result.Add(new Point(minX, maxY));
                return result;
            }

            try
            {
                int inCount = inList.Count;
                var inPts = new RustPoint2D[inCount];
                for (int i = 0; i < inCount; i++)
                {
                    inPts[i] = new RustPoint2D(inList[i].X, inList[i].Y);
                }

                var outBuffer = new RustPoint2D[inCount + 4];
                int outCount;
                int ok = NativeConvexHull(inPts, inCount, outBuffer, outBuffer.Length, out outCount);
                if (ok == 1 && outCount > 0)
                {
                    for (int i = 0; i < outCount; i++)
                    {
                        result.Add(outBuffer[i].ToPoint());
                    }
                    return result;
                }
            }
            catch
            {
                // Fallback on exception
            }

            result.AddRange(inList);
            return result;
        }
    }
}
