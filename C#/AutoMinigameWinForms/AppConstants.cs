using AutoMinigameWinForms.Models;

namespace AutoMinigameWinForms;

public static class AppConstants
{
    public const string WindowTitle = "FiveM";
    public const int UpdateMs = 1;
    public const int MaxDetectFps = 0;
    public const int MaxRenderFps = 0;
    public const int MaxIdleRenderFps = 30;
    public const int OcrIntervalMs = 30;
    public const int OcrHoldMs = 220;
    public const int OcrRequireStableReads = 1;
    public const double AttemptIntervalSec = 0.02;
    public const int WindowRefreshMs = 200;

    public const bool AutoPressOnOverlap = true;
    public const bool AutoPressUseOcrKey = true;
    public const double PressDelaySec = 0.02;
    public const int PressRequireStableFrames = 1;
    public const double PressStrictMaxDiffDeg = 9.0;
    public const double PressStrictTolRatio = 0.70;
    public const int PressRearmClearFrames = 1;
    public const int PressTouchWindowMs = 160;
    public const double PressCenterMaxDiffDeg = 6.5;

    public const int OcrBoxW = 40;
    public const int OcrBoxH = 40;
    public const int OcrOffsetX = 0;
    public const int OcrOffsetY = 4;
    public const int OcrWhiteSMax = 120;
    public const int OcrWhiteVMin = 120;
    public const int OcrTemplateSize = 32;
    public const double OcrMinScore = 0.08;
    public const double OcrMinMargin = 0.00;
    public const int OcrCrosshairMaskRadius = 1;
    public const int OcrNeedleMaskRadius = 10;
    public const string OcrModeDefault = "WASD";

    public const bool DrawOverlay = true;

    public const bool MiniGuiEnabled = true;
    public const int MiniGuiX = 10;
    public const int MiniGuiY = 10;
    public const int MiniGuiW = 150;
    public const int MiniGuiH = 150;

    public const string SoftwareName = "ZCUS MINIGAME V1.0.1";
    public const string DefaultStartHotkey = "F6";
    public const string DefaultStartHotkeyModifier = "None";
    public const string LicenseApiUrl = "http://db2.zcus.biz.id:3007/api/license/validate";
    public const string LicenseProductCode = "ZERO_MINIGAME";
    public const int LicenseApiTimeoutSec = 15;

    public static readonly IReadOnlyDictionary<string, byte> VkMap = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
    {
        ["w"] = 0x57,
        ["a"] = 0x41,
        ["s"] = 0x53,
        ["d"] = 0x44,
        ["0"] = 0x30,
        ["1"] = 0x31,
        ["2"] = 0x32,
        ["3"] = 0x33,
        ["4"] = 0x34,
        ["5"] = 0x35,
        ["6"] = 0x36,
        ["7"] = 0x37,
        ["8"] = 0x38,
        ["9"] = 0x39,
    };

    public static readonly string[] WasdTemplateKeys = ["w", "a", "s", "d"];
    public static readonly string[] DigitTemplateKeys = ["1", "2", "3", "4"];
    public static readonly string[] AllTemplateKeys = ["w", "a", "s", "d", "1", "2", "3", "4"];

    public static SimpleConfig CreateDefaultDetectorConfig()
    {
        return new SimpleConfig
        {
            WindowTitle = WindowTitle,
            UseFullWindow = false,
            CaptureSize = 180,
            CaptureWidth = 180,
            CaptureHeight = 180,
            CaptureOffsetX = 0,
            CaptureOffsetY = 0,
            ScanCenterOffsetX = 0,
            ScanCenterOffsetY = 10,
            AngleToleranceDeg = 14.5,
            BlueSteps = 72,
            BlueStepDeg = 5.0,
            BlueRSamples = 40,
            BlueNeighbor = 2,
            Red1Lower = [0, 140, 110],
            Red1Upper = [6, 255, 255],
            Red2Lower = [174, 140, 110],
            Red2Upper = [180, 255, 255],
            RedDomMarginRg = 55,
            RedDomMarginRb = 40,
            RedMinChannel = 130,
            BlueLower = [96, 130, 90],
            BlueUpper = [126, 255, 255],
            BlueDomMarginBg = 45,
            BlueDomMarginBr = 55,
            BlueMinChannel = 120,
            MorphKernel = 3,
            MaskMinArea = 8,
            MaskMaxArea = 2200,
        };
    }
}
