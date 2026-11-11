using System;
using System.Windows.Media;

namespace Kerf.Runtime.Backends
{
    public class Direct2DRenderBackend : IRenderBackend
    {
        public bool IsInitialized { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int FrameCount { get; private set; }
        public RenderPose LastPose { get; private set; }

        public void Initialize(int width, int height)
        {
            Width = width;
            Height = height;
            IsInitialized = true;
            FrameCount = 0;
            LastPose = null;
        }

        public void BeginFrame()
        {
        }

        public void RenderPose(RenderPose pose)
        {
            LastPose = pose;
            FrameCount++;
        }

        public void EndFrame()
        {
        }

        public void Resize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public void Destroy()
        {
            IsInitialized = false;
            LastPose = null;
            FrameCount = 0;
        }

        public static Matrix ToWpfMatrix(Matrix3x2 m)
        {
            return new Matrix(m.M11, m.M12, m.M21, m.M22, m.OffsetX, m.OffsetY);
        }

        public static Matrix3x2 FromWpfMatrix(Matrix m)
        {
            return new Matrix3x2(m.M11, m.M12, m.M21, m.M22, m.OffsetX, m.OffsetY);
        }
    }
}
