using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace AutoMinigameWinForms.Models;

public sealed class DetectionResult : IDisposable
{
    public required Point Center { get; init; }
    public required Mat RedMask { get; init; }
    public required Mat BlueMask { get; init; }
    public Point? RedPoint { get; init; }
    public double? RedAngle { get; init; }
    public required List<double> BlueAngles { get; init; }
    public required List<Point> BluePoints { get; init; }
    public double? BestDiff { get; init; }
    public bool Overlap { get; init; }

    public void Dispose()
    {
        RedMask.Dispose();
        BlueMask.Dispose();
    }
}

public readonly record struct OcrResult(string? Key, double Score, string Debug, Rect Box);


