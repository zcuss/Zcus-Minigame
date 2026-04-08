using AutoMinigameWinForms.Models;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace AutoMinigameWinForms.Core;

public static class MinigameDetector
{
    public static double AngleFromCenter(Point point, Point center)
    {
        var dx = point.X - center.X;
        var dy = center.Y - point.Y;
        var deg = Math.Atan2(dy, dx) * (180.0 / Math.PI);
        return (deg + 360.0) % 360.0;
    }

    public static double CircularDelta(double a, double b)
    {
        var d = Math.Abs(a - b);
        return Math.Min(d, 360.0 - d);
    }

    public static DetectionResult ProcessFrame(Mat frameBgr, SimpleConfig cfg)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(frameBgr, hsv, ColorConversionCodes.BGR2HSV);

        using var red1 = new Mat();
        using var red2 = new Mat();
        var red1Lower = new Scalar(cfg.Red1Lower[0], cfg.Red1Lower[1], cfg.Red1Lower[2]);
        var red1Upper = new Scalar(cfg.Red1Upper[0], cfg.Red1Upper[1], cfg.Red1Upper[2]);
        var red2Lower = new Scalar(cfg.Red2Lower[0], cfg.Red2Lower[1], cfg.Red2Lower[2]);
        var red2Upper = new Scalar(cfg.Red2Upper[0], cfg.Red2Upper[1], cfg.Red2Upper[2]);
        Cv2.InRange(hsv, red1Lower, red1Upper, red1);
        Cv2.InRange(hsv, red2Lower, red2Upper, red2);

        var redMask = new Mat();
        Cv2.BitwiseOr(red1, red2, redMask);

        using var bCh = new Mat();
        using var gCh = new Mat();
        using var rCh = new Mat();
        Cv2.ExtractChannel(frameBgr, bCh, 0);
        Cv2.ExtractChannel(frameBgr, gCh, 1);
        Cv2.ExtractChannel(frameBgr, rCh, 2);

        using var redDom = BuildDominanceMask(
            dominant: rCh,
            otherA: gCh,
            otherB: bCh,
            minChannel: cfg.RedMinChannel,
            marginA: cfg.RedDomMarginRg,
            marginB: cfg.RedDomMarginRb
        );
        Cv2.BitwiseAnd(redMask, redDom, redMask);

        var blueMask = new Mat();
        var blueLower = new Scalar(cfg.BlueLower[0], cfg.BlueLower[1], cfg.BlueLower[2]);
        var blueUpper = new Scalar(cfg.BlueUpper[0], cfg.BlueUpper[1], cfg.BlueUpper[2]);
        Cv2.InRange(hsv, blueLower, blueUpper, blueMask);

        using var blueDom = BuildDominanceMask(
            dominant: bCh,
            otherA: gCh,
            otherB: rCh,
            minChannel: cfg.BlueMinChannel,
            marginA: cfg.BlueDomMarginBg,
            marginB: cfg.BlueDomMarginBr
        );
        Cv2.BitwiseAnd(blueMask, blueDom, blueMask);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(cfg.MorphKernel, cfg.MorphKernel));
        Cv2.MorphologyEx(redMask, redMask, MorphTypes.Open, kernel, iterations: 1);
        Cv2.MorphologyEx(redMask, redMask, MorphTypes.Close, kernel, iterations: 1);
        Cv2.MorphologyEx(blueMask, blueMask, MorphTypes.Open, kernel, iterations: 1);
        Cv2.MorphologyEx(blueMask, blueMask, MorphTypes.Close, kernel, iterations: 1);

        var redFiltered = KeepMaskArea(redMask, cfg.MaskMinArea, cfg.MaskMaxArea);
        var blueFiltered = KeepMaskArea(blueMask, cfg.MaskMinArea, cfg.MaskMaxArea);
        redMask.Dispose();
        blueMask.Dispose();

        var center = new Point(
            frameBgr.Width / 2 + cfg.ScanCenterOffsetX,
            frameBgr.Height / 2 + cfg.ScanCenterOffsetY
        );

        var redPoint = FirstRedPixel(redFiltered, center);
        double? redAngle = redPoint.HasValue ? AngleFromCenter(redPoint.Value, center) : null;

        var (blueAngles, bluePoints) = ScanBlueAngles(blueFiltered, center, cfg);

        var overlap = false;
        double? bestDiff = null;
        if (redAngle.HasValue && blueAngles.Count > 0)
        {
            bestDiff = blueAngles.Min(a => CircularDelta(redAngle.Value, a));
            overlap = bestDiff <= cfg.AngleToleranceDeg;
        }

        return new DetectionResult
        {
            Center = center,
            RedMask = redFiltered,
            BlueMask = blueFiltered,
            RedPoint = redPoint,
            RedAngle = redAngle,
            BlueAngles = blueAngles,
            BluePoints = bluePoints,
            BestDiff = bestDiff,
            Overlap = overlap,
        };
    }

    private static Mat BuildDominanceMask(Mat dominant, Mat otherA, Mat otherB, int minChannel, int marginA, int marginB)
    {
        using var minMask = new Mat();
        using var aCmp = new Mat();
        using var bCmp = new Mat();
        using var otherAPlus = new Mat();
        using var otherBPlus = new Mat();

        Cv2.Compare(dominant, new Scalar(minChannel), minMask, CmpTypes.GE);

        Cv2.Add(otherA, new Scalar(marginA), otherAPlus);
        Cv2.Add(otherB, new Scalar(marginB), otherBPlus);

        Cv2.Compare(dominant, otherAPlus, aCmp, CmpTypes.GE);
        Cv2.Compare(dominant, otherBPlus, bCmp, CmpTypes.GE);

        var outMask = new Mat();
        Cv2.BitwiseAnd(minMask, aCmp, outMask);
        Cv2.BitwiseAnd(outMask, bCmp, outMask);
        return outMask;
    }

    private static Mat KeepMaskArea(Mat mask, int minArea, int maxArea)
    {
        var output = Mat.Zeros(mask.Size(), MatType.CV_8UC1).ToMat();
        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area >= minArea && area <= maxArea)
            {
                Cv2.DrawContours(output, new[] { contour }, -1, Scalar.White, -1);
            }
        }

        return output;
    }

    private static Point? FirstRedPixel(Mat mask, Point center)
    {
        using var points = new Mat();
        Cv2.FindNonZero(mask, points);
        if (points.Empty())
        {
            return null;
        }

        // Use the outer-most red pixel from scan center as the needle tip proxy.
        var best = points.Get<Point>(0);
        var bestDist2 = -1.0;
        for (var i = 0; i < points.Rows; i++)
        {
            var p = points.Get<Point>(i);
            var dx = p.X - center.X;
            var dy = p.Y - center.Y;
            var d2 = (dx * dx) + (dy * dy);
            if (d2 > bestDist2)
            {
                bestDist2 = d2;
                best = p;
            }
        }

        return best;
    }

    private static (List<double> angles, List<Point> points) ScanBlueAngles(Mat mask, Point center, SimpleConfig cfg)
    {
        var hitAngles = new List<double>(cfg.BlueSteps);
        var hitPoints = new List<Point>(cfg.BlueSteps);

        var h = mask.Rows;
        var w = mask.Cols;
        var maxDim = Math.Min(h, w);
        var rMin = Math.Max(1.0, maxDim * cfg.BlueRMinRatio);
        var rMax = Math.Max(rMin + 1.0, maxDim * cfg.BlueRMaxRatio);

        for (var i = 0; i < cfg.BlueSteps; i++)
        {
            var angle = i * cfg.BlueStepDeg;
            var rad = angle * (Math.PI / 180.0);
            var ux = Math.Cos(rad);
            var uy = -Math.Sin(rad);

            var hit = false;
            var stepCount = Math.Max(1, cfg.BlueRSamples - 1);
            for (var s = 0; s < cfg.BlueRSamples; s++)
            {
                var ratio = s / (double)stepCount;
                var r = rMin + (ratio * (rMax - rMin));

                var x = (int)Math.Round(center.X + ux * r);
                var y = (int)Math.Round(center.Y + uy * r);
                if (x < 0 || y < 0 || x >= w || y >= h)
                {
                    continue;
                }

                var x1 = Math.Max(0, x - cfg.BlueNeighbor);
                var y1 = Math.Max(0, y - cfg.BlueNeighbor);
                var x2 = Math.Min(w - 1, x + cfg.BlueNeighbor);
                var y2 = Math.Min(h - 1, y + cfg.BlueNeighbor);

                var roiRect = new Rect(x1, y1, x2 - x1 + 1, y2 - y1 + 1);
                using var roi = new Mat(mask, roiRect);
                if (Cv2.CountNonZero(roi) > 0)
                {
                    hitAngles.Add(angle);
                    hitPoints.Add(new Point(x, y));
                    hit = true;
                    break;
                }
            }

            if (hit)
            {
                continue;
            }
        }

        return (hitAngles, hitPoints);
    }
}



