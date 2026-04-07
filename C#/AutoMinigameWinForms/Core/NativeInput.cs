using System.Runtime.InteropServices;

namespace AutoMinigameWinForms.Core;

public static class NativeInput
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    private const uint KeyEventfKeyUp = 0x0002;

    public static void PressKey(string key)
    {
        if (!AppConstants.VkMap.TryGetValue(key, out var vk))
        {
            return;
        }

        keybd_event(vk, 0, 0, 0);
        keybd_event(vk, 0, KeyEventfKeyUp, 0);
    }
}
