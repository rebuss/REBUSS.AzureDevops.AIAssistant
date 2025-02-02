using System.Runtime.InteropServices;
using System.Text;

namespace REBUSS.GitDaif.Service.API.Agents.Helpers
{
    public class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint FindWindow(string IpClassName, string IpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern nint FindWindowEx(nint hwndParent, nint hwndChildAfter, string IpszClass, string IpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetForegroundWindow(nint hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SendMessage(nint hWnd, int Msg, nint wParam, string IParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(nint hWndParent, EnumChildProc IpEnumFunc, nint IParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetClassName(nint hWnd, StringBuilder IpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll")]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;

        public delegate bool EnumChildProc(nint hWnd, nint IParam);
        public const byte VK_RETURN = 0x0D; // Enter
        public const uint KEYEVENTF_KEYDOWN = 0x00000; // Key pressed
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const int WM_SETTEXT = 0x000C;

        public static nint FindControlByClass(nint hWndParent, string className)
        {
            nint result = nint.Zero;
            EnumChildWindows(hWndParent, (hWnd, IParam) =>
            {
                StringBuilder classBuilder = new StringBuilder(256);
                GetClassName(hWnd, classBuilder, classBuilder.Capacity);
                if (classBuilder.ToString() == className)
                {
                    result = hWnd;
                    return false;
                }
                return true;
            }, nint.Zero);

            return result;
        }

        public static string GetClipboardText()
        {
            if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
            {
                Console.WriteLine("No text available in the clipboard.");
                return string.Empty;
            }

            if (!OpenClipboard(IntPtr.Zero))
            {
                Console.WriteLine("Failed to open the clipboard.");
                return string.Empty;
            }

            IntPtr handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
            {
                Console.WriteLine("Failed to retrieve data from the clipboard.");
                CloseClipboard();
                return string.Empty;
            }

            IntPtr pointer = GlobalLock(handle);
            string clipboardText = Marshal.PtrToStringUni(pointer);
            GlobalUnlock(handle);
            CloseClipboard();

            return clipboardText;
        }

        public static void SetClipboardText(string text)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                throw new InvalidOperationException("Failed to open the clipboard.");
            }

            try
            {
                EmptyClipboard();
                IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)((text.Length + 1) * 2));
                if (hGlobal == IntPtr.Zero)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for clipboard text.");
                }

                IntPtr target = GlobalLock(hGlobal);
                if (target == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to lock global memory.");
                }

                try
                {
                    Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                    Marshal.WriteInt16(target, text.Length * 2, 0);
                }
                finally
                {
                    GlobalUnlock(hGlobal);
                }

                if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to set clipboard data.");
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        public static void ClearClipboard()
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                throw new InvalidOperationException("Failed to open the clipboard.");
            }

            try
            {
                EmptyClipboard();
            }
            finally
            {
                CloseClipboard();
            }
        }
    }
}