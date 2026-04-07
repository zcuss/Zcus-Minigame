using System.Runtime.InteropServices;
using System.Text;

namespace AutoMinigameWinForms.Core;

public sealed record WindowInfo(nint Handle, string Title, Rectangle Bounds)
{
    public int Area => Math.Max(0, Bounds.Width) * Math.Max(0, Bounds.Height);
}

public static class WindowFinder
{
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectNative
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out RectNative lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    public static List<WindowInfo> GetWindowsWithTitle(string titleContains)
    {
        var output = new List<WindowInfo>();
        var needle = titleContains ?? string.Empty;
        var currentPid = Environment.ProcessId;

        EnumWindows((hWnd, _lParam) =>
        {
            if (!IsWindowVisible(hWnd) || IsIconic(hWnd))
            {
                return true;
            }

            _ = GetWindowThreadProcessId(hWnd, out var pid);
            if ((int)pid == currentPid)
            {
                return true;
            }

            var length = GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return true;
            }

            var sb = new StringBuilder(length + 1);
            _ = GetWindowText(hWnd, sb, sb.Capacity);
            var text = sb.ToString();
            if (text.Length == 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(needle) && !text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!GetWindowRect(hWnd, out var rect))
            {
                return true;
            }

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
            {
                return true;
            }

            output.Add(new WindowInfo(
                hWnd,
                text,
                new Rectangle(rect.Left, rect.Top, width, height)
            ));
            return true;
        }, 0);

        return output;
    }

    public static bool TryGetForegroundWindowInfo(out WindowInfo? info)
    {
        info = null;
        var hWnd = GetForegroundWindow();
        if (hWnd == nint.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(hWnd, out var pid);
        if ((int)pid == Environment.ProcessId)
        {
            return false;
        }

        if (!IsWindowVisible(hWnd) || IsIconic(hWnd))
        {
            return false;
        }

        var length = GetWindowTextLength(hWnd);
        if (length <= 0)
        {
            return false;
        }

        var sb = new StringBuilder(length + 1);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        var text = sb.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!GetWindowRect(hWnd, out var rect))
        {
            return false;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        info = new WindowInfo(hWnd, text, new Rectangle(rect.Left, rect.Top, width, height));
        return true;
    }
}
