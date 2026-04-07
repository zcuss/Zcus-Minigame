namespace AutoMinigameWinForms.Models;

public sealed class SimpleConfig
{
    public string WindowTitle { get; set; } = AppConstants.WindowTitle;
    public bool UseFullWindow { get; set; }
    public int CaptureSize { get; set; } = 180;
    public int CaptureWidth { get; set; }
    public int CaptureHeight { get; set; }
    public int CaptureOffsetX { get; set; }
    public int CaptureOffsetY { get; set; }
    public int ScanCenterOffsetX { get; set; }
    public int ScanCenterOffsetY { get; set; }
    public double AngleToleranceDeg { get; set; } = 10.0;
    public int BlueSteps { get; set; } = 20;
    public double BlueStepDeg { get; set; } = 18.0;
    public double BlueRMinRatio { get; set; } = 0.20;
    public double BlueRMaxRatio { get; set; } = 0.78;
    public int BlueRSamples { get; set; } = 30;
    public int BlueNeighbor { get; set; } = 1;
    public int[] Red1Lower { get; set; } = [0, 80, 70];
    public int[] Red1Upper { get; set; } = [8, 255, 255];
    public int[] Red2Lower { get; set; } = [172, 140, 120];
    public int[] Red2Upper { get; set; } = [180, 255, 255];
    public int RedDomMarginRg { get; set; } = 50;
    public int RedDomMarginRb { get; set; } = 35;
    public int RedMinChannel { get; set; } = 120;
    public int[] BlueLower { get; set; } = [92, 110, 80];
    public int[] BlueUpper { get; set; } = [128, 255, 255];
    public int BlueDomMarginBg { get; set; } = 38;
    public int BlueDomMarginBr { get; set; } = 48;
    public int BlueMinChannel { get; set; } = 115;
    public int MorphKernel { get; set; } = 3;
    public int MaskMinArea { get; set; } = 8;
    public int MaskMaxArea { get; set; } = 2200;
}
