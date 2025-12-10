using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EstlCameo
{
    public class KeyboardHook : IDisposable
    {
        private IntPtr hookId = IntPtr.Zero;

        public event Action CtrlZPressed;
        public event Action CtrlYPressed;
        public event Action CtrlRPressed;
        public event Action CtrlSPressed;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        public KeyboardHook()
        {
            hookId = SetHook(HookCallback);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule;

            return SetWindowsHookEx(13, proc,
                GetModuleHandle(curModule.ModuleName), 0);
        }


        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // 1. Required by WH_KEYBOARD_LL contract: always call next if nCode < 0
            if (nCode < 0)
                return CallNextHookEx(hookId, nCode, wParam, lParam);

            // 2. Only interested in key-down events
            if (wParam != (IntPtr)WM_KEYDOWN && wParam != (IntPtr)WM_SYSKEYDOWN)
                return CallNextHookEx(hookId, nCode, wParam, lParam);

            // 3. Decode the key and modifier state
            int vkCode = Marshal.ReadInt32(lParam);
            bool ctrlDown = (GetKeyState(VK_CONTROL) & 0x8000) != 0;

            // 4. Fast path: if it's not a Ctrl+hotkey we care about, just pass through
            if (!ctrlDown ||
                (vkCode != (int)Keys.Z &&
                 vkCode != (int)Keys.Y &&
                 vkCode != (int)Keys.R &&
                 vkCode != (int)Keys.S))
            {
                return CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            // 5. Only from here down do we care about which app is foreground
            bool estlcamForeground = EstlcamInterop.IsEstlcamForeground();
            if (!estlcamForeground)
            {
                // Not Estlcam → let Premiere / VS / browser handle Ctrl+Z/Y/R/S
                return CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            // 6. Estlcam has focus and we know it's Ctrl+one-of-ours
            switch ((Keys)vkCode)
            {
                case Keys.Z:
                    CtrlZPressed?.Invoke();
                    return (IntPtr)1; // swallow for Estlcam

                case Keys.Y:
                    CtrlYPressed?.Invoke();
                    return (IntPtr)1;

                case Keys.R:
                    CtrlRPressed?.Invoke();
                    return (IntPtr)1;

                case Keys.S:
                    // Snapshot trigger: notify, but never swallow the keystroke itself.
                    CtrlSPressed?.Invoke();
                    break;
            }

            // 7. Default: let Estlcam also see the key
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }


        public void Dispose() => UnhookWindowsHookEx(hookId);

        // Win32
        private const int VK_CONTROL = 0x11;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int keyCode);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc callback, IntPtr hMod, uint threadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook,
            int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
