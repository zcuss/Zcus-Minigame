namespace AutoMinigameWinForms.Models;

public sealed class AppConfig
{
    public string WindowTitle { get; set; } = AppConstants.WindowTitle;
    public int CaptureOffsetX { get; set; }
    public int CaptureOffsetY { get; set; }
    public int CaptureWidth { get; set; } = 180;
    public int CaptureHeight { get; set; } = 180;
    public int ScanCenterOffsetX { get; set; }
    public int ScanCenterOffsetY { get; set; } = 10;
    public int OcrOffsetX { get; set; } = AppConstants.OcrOffsetX;
    public int OcrOffsetY { get; set; } = AppConstants.OcrOffsetY;
    public int OcrBoxW { get; set; } = AppConstants.OcrBoxW;
    public int OcrBoxH { get; set; } = AppConstants.OcrBoxH;
    public int OcrMinScoreX100 { get; set; } = (int)Math.Round(AppConstants.OcrMinScore * 100.0);
    public int OcrMinMarginX100 { get; set; } = (int)Math.Round(AppConstants.OcrMinMargin * 100.0);
    public int AngleToleranceX10 { get; set; } = (int)(14.5 * 10.0);
    public string OcrMode { get; set; } = AppConstants.OcrModeDefault;
    public int MiniX { get; set; } = AppConstants.MiniGuiX;
    public int MiniY { get; set; } = AppConstants.MiniGuiY;
    public int MiniW { get; set; } = AppConstants.MiniGuiW;
    public int MiniH { get; set; } = AppConstants.MiniGuiH;
    public bool CaptureEnabled { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = false;
    public bool DebugAllLogs { get; set; } = true;
    public string StartHotkey { get; set; } = AppConstants.DefaultStartHotkey;
    public string StartHotkeyModifier { get; set; } = AppConstants.DefaultStartHotkeyModifier;

    public string LicenseKey { get; set; } = string.Empty;
    public string Software { get; set; } = AppConstants.SoftwareName;
    public string MachineId { get; set; } = string.Empty;
    public string LicenseOwner { get; set; } = string.Empty;
    public string LicenseExpiresAt { get; set; } = string.Empty;
    public string LicenseLastVerifiedAt { get; set; } = string.Empty;
    public string LicenseLastMessage { get; set; } = string.Empty;
}
