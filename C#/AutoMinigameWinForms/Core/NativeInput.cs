using System.Runtime.InteropServices;

namespace AutoMinigameWinForms.Core;

public static class NativeInput
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventfKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    public static bool KeyDown(string key, nint? targetHwnd = null)
    {
        if (!AppConstants.VkMap.TryGetValue(key, out var vkByte))
        {
            return false;
        }

        if (targetHwnd.HasValue && targetHwnd.Value != nint.Zero && IsWindow(targetHwnd.Value))
        {
            _ = SetForegroundWindow(targetHwnd.Value);
        }

        var vk = (ushort)vkByte;
        var inputs = new[]
        {
            new Input
            {
                Type = InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        Vk = vk,
                        Scan = 0,
                        Flags = 0,
                        Time = 0,
                        ExtraInfo = 0,
                    }
                }
            }
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent == (uint)inputs.Length)
        {
            return true;
        }

        // Fallback for environments where SendInput gets filtered.
        keybd_event(vkByte, 0, 0, 0);
        return true;
    }

    public static bool KeyUp(string key, nint? targetHwnd = null)
    {
        if (!AppConstants.VkMap.TryGetValue(key, out var vkByte))
        {
            return false;
        }

        if (targetHwnd.HasValue && targetHwnd.Value != nint.Zero && IsWindow(targetHwnd.Value))
        {
            _ = SetForegroundWindow(targetHwnd.Value);
        }

        var vk = (ushort)vkByte;
        var inputs = new[]
        {
            new Input
            {
                Type = InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        Vk = vk,
                        Scan = 0,
                        Flags = KeyEventfKeyUp,
                        Time = 0,
                        ExtraInfo = 0,
                    }
                }
            }
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent == (uint)inputs.Length)
        {
            return true;
        }

        keybd_event(vkByte, 0, KeyEventfKeyUp, 0);
        return true;
    }

    public static bool PressKey(string key, nint? targetHwnd = null)
    {
        var downSent = KeyDown(key, targetHwnd);
        var upSent = KeyUp(key, targetHwnd);
        return downSent && upSent;
    }
}
