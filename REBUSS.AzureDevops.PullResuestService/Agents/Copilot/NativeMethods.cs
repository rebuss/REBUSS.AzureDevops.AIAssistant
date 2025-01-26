using System.Runtime.InteropServices;
using System.Text;

namespace REBUSS.AzureDevOps.PullRequestAPI.Agents.Copilot
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
    }
}
