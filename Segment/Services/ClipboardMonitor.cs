using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Segment.App.Services
{
    public class ClipboardMonitor : IDisposable
    {
        public event EventHandler? ClipboardChanged;

        private HwndSource? _hwndSource;
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        public void Start(System.Windows.Window windowSource)
        {
            var helper = new WindowInteropHelper(windowSource);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource.AddHook(HwndHandler);
            AddClipboardFormatListener(_hwndSource.Handle);
        }

        public void Stop()
        {
            if (_hwndSource != null)
            {
                RemoveClipboardFormatListener(_hwndSource.Handle);
                _hwndSource.RemoveHook(HwndHandler);
                _hwndSource.Dispose();
                _hwndSource = null;
            }
        }

        private IntPtr HwndHandler(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                ClipboardChanged?.Invoke(this, EventArgs.Empty);
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}