using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace praktik.Services
{
    internal static class WindowCornerService
    {
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwcpRound = 2;

        public static void Apply(Window window)
        {
            if (window == null)
            {
                return;
            }

            var helper = new WindowInteropHelper(window);
            if (helper.Handle != IntPtr.Zero)
            {
                ApplyToHandle(helper.Handle);
                return;
            }

            window.SourceInitialized -= Window_SourceInitialized;
            window.SourceInitialized += Window_SourceInitialized;
        }

        private static void Window_SourceInitialized(object sender, EventArgs e)
        {
            if (sender is Window window)
            {
                window.SourceInitialized -= Window_SourceInitialized;
                ApplyToHandle(new WindowInteropHelper(window).Handle);
            }
        }

        private static void ApplyToHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var preference = DwmwcpRound;
                DwmSetWindowAttribute(
                    handle,
                    DwmwaWindowCornerPreference,
                    ref preference,
                    sizeof(int));
            }
            catch
            {
                // Older Windows builds ignore this visual enhancement.
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            ref int pvAttribute,
            int cbAttribute);
    }
}
