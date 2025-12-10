using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EstlCameo
{
    public static class ScreenshotHelper
    {
        public static void CaptureEstlcamWindow(string outputPath)
        {
            IntPtr hwnd = EstlcamInterop.GetEstlcamMainWindow();
            if (hwnd == IntPtr.Zero) return;

            if (!GetWindowRect(hwnd, out RECT rect)) return;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0) return;

            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                // Capture screen pixels where the window currently is
                g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
                bmp.Save(outputPath, ImageFormat.Png);
            }
        }

        // Win32 ----------------------------------------------------

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
