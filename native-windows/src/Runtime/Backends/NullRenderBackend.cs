using System;

namespace Kerf.Runtime.Backends
{
    public class NullRenderBackend : IRenderBackend
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
    }
}
