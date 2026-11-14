using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kerf.UI
{
    /// <summary>
    /// Temporarily listens for a desktop click and samples the color beneath it.
    /// Low-level hooks let the user sample outside Kerf while preventing that click
    /// from activating or changing the window underneath the cursor.
    /// </summary>
    internal sealed class ScreenColorPickerSession : IDisposable
    {
        private const int WhMouseLl = 14;
        private const int WhKeyboardLl = 13;
        private const int WmLButtonDown = 0x0201;
        private const int WmLButtonUp = 0x0202;
        private const int WmRButtonDown = 0x0204;
        private const int WmRButtonUp = 0x0205;
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;
        private const uint VkEscape = 0x1B;
        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;

        private readonly Window _owner;
        private readonly Action<Color> _onPicked;
        private readonly Action _onCanceled;
        private readonly Action<string> _onFailed;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _timer;
        private readonly LowLevelMouseProc _mouseProc;
        private readonly LowLevelKeyboardProc _keyboardProc;

        private IntPtr _mouseHook;
        private IntPtr _keyboardHook;
        private Window _hintWindow;
        private TextBlock _hintText;
        private bool _isDisposed;
        private bool _isCompleted;
        private bool _suppressNextLeftButtonUp;
        private bool _suppressNextRightButtonUp;
        private Color? _pendingPickedColor;

        private ScreenColorPickerSession(
            Window owner,
            Action<Color> onPicked,
            Action onCanceled,
            Action<string> onFailed)
        {
            _owner = owner;
            _onPicked = onPicked;
            _onCanceled = onCanceled;
            _onFailed = onFailed;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _mouseProc = HookMouse;
            _keyboardProc = HookKeyboard;

            _timer = new DispatcherTimer(DispatcherPriority.Input, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            _timer.Tick += OnTimerTick;
        }

        public static void Start(
            Window owner,
            Action<Color> onPicked,
            Action onCanceled,
            Action<string> onFailed)
        {
            var session = new ScreenColorPickerSession(owner, onPicked, onCanceled, onFailed);
            try
            {
                session.Initialize();
            }
            catch (Exception ex)
            {
                session.Dispose();
                if (onFailed != null)
                    onFailed("Kerf couldn't start screen color picking. " + ex.Message);
            }
        }

        private void Initialize()
        {
            IntPtr module = GetModuleHandle(null);
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, module, 0);
            if (_mouseHook == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, module, 0);
            if (_keyboardHook == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            CreateHintWindow();

            if (_owner != null)
                _owner.Closed += OnOwnerClosed;

            if (Application.Current != null)
                Application.Current.Exit += OnApplicationExit;

            _timer.Start();
            OnTimerTick(this, EventArgs.Empty);
        }

        private void CreateHintWindow()
        {
            _hintText = new TextBlock
            {
                Text = "Click a color to pick it  ·  Esc or right-click to cancel",
                Foreground = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(246, 24, 26, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 239, 177)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(11, 8, 12, 8),
                Child = _hintText
            };

            _hintWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                SizeToContent = SizeToContent.WidthAndHeight,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                Focusable = false,
                Content = badge
            };

            _hintWindow.Show();
            _hintWindow.UpdateLayout();

            var helper = new WindowInteropHelper(_hintWindow);
            IntPtr hwnd = helper.Handle;
            int styles = GetWindowLong(hwnd, GwlExStyle);
            SetWindowLong(hwnd, GwlExStyle, styles | WsExTransparent | WsExToolWindow | WsExNoActivate);
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            if (_isDisposed || _hintWindow == null) return;

            POINT cursor;
            if (GetCursorPos(out cursor))
                PositionHint(cursor);
        }

        private void PositionHint(POINT cursor)
        {
            Point screenPoint = new Point(cursor.X, cursor.Y);
            PresentationSource source = PresentationSource.FromVisual(_hintWindow);
            if (source != null && source.CompositionTarget != null)
                screenPoint = source.CompositionTarget.TransformFromDevice.Transform(screenPoint);

            double left = screenPoint.X + 18;
            double top = screenPoint.Y + 20;
            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            if (left + _hintWindow.ActualWidth > virtualRight)
                left = screenPoint.X - _hintWindow.ActualWidth - 18;
            if (top + _hintWindow.ActualHeight > virtualBottom)
                top = screenPoint.Y - _hintWindow.ActualHeight - 20;

            _hintWindow.Left = Math.Max(virtualLeft, Math.Min(left, virtualRight - _hintWindow.ActualWidth));
            _hintWindow.Top = Math.Max(virtualTop, Math.Min(top, virtualBottom - _hintWindow.ActualHeight));
        }

        private IntPtr HookMouse(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && !_isDisposed)
            {
                try
                {
                    int message = wParam.ToInt32();
                    if (message == WmLButtonDown)
                    {
                        MSLLHOOKSTRUCT data = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                        _suppressNextLeftButtonUp = true;
                        _pendingPickedColor = ReadScreenColor(data.pt);
                        return new IntPtr(1);
                    }

                    if (message == WmLButtonUp && _suppressNextLeftButtonUp)
                    {
                        _suppressNextLeftButtonUp = false;
                        if (_pendingPickedColor.HasValue)
                        {
                            Color selected = _pendingPickedColor.Value;
                            _pendingPickedColor = null;
                            _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate
                            {
                                Complete(selected);
                            }));
                        }
                        else
                        {
                            _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate
                            {
                                ShowPixelReadError();
                            }));
                        }
                        return new IntPtr(1);
                    }

                    if (message == WmRButtonDown)
                    {
                        _suppressNextRightButtonUp = true;
                        return new IntPtr(1);
                    }

                    if (message == WmRButtonUp && _suppressNextRightButtonUp)
                    {
                        _suppressNextRightButtonUp = false;
                        _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate
                        {
                            Cancel();
                        }));
                        return new IntPtr(1);
                    }
                }
                catch
                {
                    // Keep the hook alive if a single pointer event cannot be sampled.
                }
            }

            return CallNextHookEx(_mouseHook, code, wParam, lParam);
        }

        private IntPtr HookKeyboard(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && !_isDisposed)
            {
                try
                {
                    int message = wParam.ToInt32();
                    if (message == WmKeyDown || message == WmSysKeyDown)
                    {
                        KBDLLHOOKSTRUCT data = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                        if (data.vkCode == VkEscape)
                        {
                            _dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate
                            {
                                Cancel();
                            }));
                            return new IntPtr(1);
                        }
                    }
                }
                catch
                {
                    // Continue normal keyboard processing if the event cannot be decoded.
                }
            }

            return CallNextHookEx(_keyboardHook, code, wParam, lParam);
        }

        private Color? ReadScreenColor(POINT point)
        {
            if (_hintWindow != null && _hintWindow.IsVisible)
                _hintWindow.Hide();

            // Let the desktop repaint without the Kerf hint before reading the pixel.
            DwmFlush();

            IntPtr screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero) return null;

            try
            {
                uint pixel = GetPixel(screenDc, point.X, point.Y);
                if (pixel == 0xFFFFFFFF) return null;

                byte red = (byte)(pixel & 0xFF);
                byte green = (byte)((pixel >> 8) & 0xFF);
                byte blue = (byte)((pixel >> 16) & 0xFF);
                return Color.FromRgb(red, green, blue);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private void ShowPixelReadError()
        {
            if (_isDisposed || _hintWindow == null) return;
            _hintText.Text = "Couldn't read that pixel. Try another spot.";
            _hintWindow.Show();
            POINT cursor;
            if (GetCursorPos(out cursor)) PositionHint(cursor);
        }

        private void Complete(Color color)
        {
            if (_isCompleted || _isDisposed) return;
            _isCompleted = true;
            Dispose();
            if (_onPicked != null) _onPicked(color);
        }

        private void Cancel()
        {
            if (_isCompleted || _isDisposed) return;
            _isCompleted = true;
            Dispose();
            if (_onCanceled != null) _onCanceled();
        }

        private void OnOwnerClosed(object sender, EventArgs e)
        {
            _isCompleted = true;
            Dispose();
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            _isCompleted = true;
            Dispose();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _timer.Stop();
            _timer.Tick -= OnTimerTick;

            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            if (_keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }

            if (_owner != null)
                _owner.Closed -= OnOwnerClosed;
            if (Application.Current != null)
                Application.Current.Exit -= OnApplicationExit;

            if (_hintWindow != null)
            {
                _hintWindow.Close();
                _hintWindow = null;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelMouseProc(int code, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr dc, int x, int y);

        [DllImport("dwmapi.dll")]
        private static extern int DwmFlush();

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr window, int index, int value);
    }
}
