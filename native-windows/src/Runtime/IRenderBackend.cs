using System;

namespace Kerf.Runtime
{
    public interface IRenderBackend
    {
        void Initialize(int width, int height);
        void BeginFrame();
        void RenderPose(RenderPose pose);
        void EndFrame();
        void Resize(int width, int height);
        void Destroy();
    }
}
