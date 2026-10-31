using System;
using System.Runtime.InteropServices;

namespace Kerf.Engine.Animation
{
    public static class AnimationNativeBridge
    {
        private const string DllName = "kerf_core.dll";
        private static bool _nativeChecked = false;
        private static bool _isNativeAvailable = false;

        public static bool IsNativeAvailable
        {
            get
            {
                if (!_nativeChecked)
                {
                    try
                    {
                        ulong testCtx = kerf_anim_context_create();
                        if (testCtx != 0)
                        {
                            kerf_anim_context_destroy(testCtx);
                            _isNativeAvailable = true;
                        }
                    }
                    catch
                    {
                        _isNativeAvailable = false;
                    }
                    _nativeChecked = true;
                }
                return _isNativeAvailable;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong kerf_anim_context_create();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kerf_anim_context_destroy(ulong handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint kerf_anim_clip_create(
            ulong handle,
            uint id,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            float duration,
            float fps,
            int loopMode);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint kerf_anim_clip_add_track(
            ulong handle,
            uint clipIdx,
            uint node,
            ushort prop);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kerf_anim_track_add_keyframe(
            ulong handle,
            uint clipIdx,
            uint trackIdx,
            float time,
            float val,
            int interpType,
            float x1, float y1, float x2, float y2);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float kerf_anim_track_sample(
            ulong handle,
            uint clipIdx,
            uint trackIdx,
            float time);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint kerf_sm_create(
            ulong handle,
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint kerf_sm_add_input(
            ulong handle,
            uint smIdx,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            int kind,
            float initialVal);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kerf_sm_set_bool(
            ulong handle,
            uint smIdx,
            uint inputIdx,
            int val);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kerf_sm_set_number(
            ulong handle,
            uint smIdx,
            uint inputIdx,
            float val);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void kerf_sm_fire_trigger(
            ulong handle,
            uint smIdx,
            uint inputIdx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint kerf_sm_add_layer(
            ulong handle,
            uint smIdx,
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint kerf_sm_add_state(
            ulong handle,
            uint smIdx,
            uint layerIdx,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            int kind,
            uint clipIdx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint kerf_sm_add_transition(
            ulong handle,
            uint smIdx,
            uint layerIdx,
            uint fromState,
            uint toState,
            float duration,
            float exitTime,
            int hasExitTime,
            int interrupt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int kerf_sm_transition_add_condition(
            ulong handle,
            uint smIdx,
            uint layerIdx,
            uint transIdx,
            uint inputIdx,
            int op,
            float val);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint kerf_sm_advance(
            ulong handle,
            uint smIdx,
            float dt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float kerf_sm_get_property_value(
            ulong handle,
            uint node,
            ushort prop,
            float fallback);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint kerf_sm_get_current_state(
            ulong handle,
            uint smIdx,
            uint layerIdx);
    }
}
