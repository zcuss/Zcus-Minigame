using AutoMinigameWinForms.Models;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace AutoMinigameWinForms.Core;

public sealed class OcrEngine : IDisposable
{
    private readonly Dictionary<string, List<Mat>> _templates;

    public OcrEngine()
    {
        _templates = DrawLineTemplates(AppConstants.OcrTemplateSize);
    }

    public void Dispose()
    {
        foreach (var (_, list) in _templates)
        {
            foreach (var mat in list)
            {
                mat.Dispose();
            }
        }
    }

    public OcrResult DetectWhiteLetterLineStyle(
        Mat frameBgr,
        Point center,
        int ocrOffsetX,
        int ocrOffsetY,
        int boxW,
        int boxH,
        IReadOnlyCollection<string>? allowedKeys = null)
    {
        var h = frameBgr.Rows;
        var w = frameBgr.Cols;
        var halfW = Math.Max(1, boxW / 2);
        var halfH = Math.Max(1, boxH / 2);
        var cx = center.X + ocrOffsetX;
        var cy = center.Y + ocrOffsetY;

        var x1 = Math.Max(0, cx - halfW);
        var x2 = Math.Min(w, cx + halfW);
        var y1 = Math.Max(0, cy - halfH);
        var y2 = Math.Min(h, cy + halfH);

        if (x2 <= x1 || y2 <= y1)
        {
            return new OcrResult(null, 0.0, "w:0.00 a:0.00", new Rect(x1, y1, 0, 0));
        }

        using var crop = new Mat(frameBgr, new Rect(x1, y1, x2 - x1, y2 - y1));
        if (crop.Empty())
        {
            return new OcrResult(null, 0.0, "w:0.00 a:0.00", new Rect(x1, y1, x2 - x1, y2 - y1));
        }

        using var hsv = new Mat();
        Cv2.CvtColor(crop, hsv, ColorConversionCodes.BGR2HSV);

        using var white = new Mat();
        Cv2.InRange(
            hsv,
            new Scalar(0, 0, AppConstants.OcrWhiteVMin),
            new Scalar(179, AppConstants.OcrWhiteSMax, 255),
            white
        );

        using var gray = new Mat();
        Cv2.CvtColor(crop, gray, ColorConversionCodes.BGR2GRAY);
        using var bright = new Mat();
        Cv2.Threshold(gray, bright, 145, 255, ThresholdTypes.Binary);
        Cv2.BitwiseOr(white, bright, white);
        using var adaptive = new Mat();
        Cv2.AdaptiveThreshold(gray, adaptive, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 11, 2);
        Cv2.BitwiseOr(white, adaptive, white);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(2, 2));
        Cv2.MorphologyEx(white, white, MorphTypes.Open, kernel, iterations: 1);
        Cv2.MorphologyEx(white, white, MorphTypes.Close, kernel, iterations: 1);

        Cv2.FindContours(white, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var valid = contours
            .Where(c => Cv2.ContourArea(c) >= 6.0)
            .OrderByDescending(c => Cv2.ContourArea(c))
            .Take(8)
            .ToArray();

        Rect roiRect;
        var debugPrefix = "cnt";
        if (valid.Length > 0)
        {
            var bx = int.MaxValue;
            var by = int.MaxValue;
            var ex = int.MinValue;
            var ey = int.MinValue;

            foreach (var cnt in valid)
            {
                var rect = Cv2.BoundingRect(cnt);
                bx = Math.Min(bx, rect.X);
                by = Math.Min(by, rect.Y);
                ex = Math.Max(ex, rect.Right);
                ey = Math.Max(ey, rect.Bottom);
            }

            bx = Math.Max(0, bx);
            by = Math.Max(0, by);
            ex = Math.Min(white.Cols, ex);
            ey = Math.Min(white.Rows, ey);
            roiRect = new Rect(bx, by, Math.Max(1, ex - bx), Math.Max(1, ey - by));
        }
        else
        {
            // Fallback: still try OCR over full box when contouring fails.
            roiRect = new Rect(0, 0, Math.Max(1, white.Cols), Math.Max(1, white.Rows));
            debugPrefix = "full";
        }

        using var roi = new Mat(white, roiRect);
        if (roi.Empty())
        {
            return new OcrResult(null, 0.0, "w:0.00 a:0.00", new Rect(x1, y1, x2 - x1, y2 - y1));
        }

        var scale = Math.Min(
            (AppConstants.OcrTemplateSize - 4.0) / Math.Max(1, roiRect.Width),
            (AppConstants.OcrTemplateSize - 4.0) / Math.Max(1, roiRect.Height)
        );
        var nw = Math.Max(1, (int)(roiRect.Width * scale));
        var nh = Math.Max(1, (int)(roiRect.Height * scale));

        using var resized = new Mat();
        Cv2.Resize(roi, resized, new OpenCvSharp.Size(nw, nh), interpolation: InterpolationFlags.Nearest);

        using var norm = Mat.Zeros(AppConstants.OcrTemplateSize, AppConstants.OcrTemplateSize, MatType.CV_8UC1).ToMat();
        var ox = (AppConstants.OcrTemplateSize - nw) / 2;
        var oy = (AppConstants.OcrTemplateSize - nh) / 2;
        using var normRoi = new Mat(norm, new Rect(ox, oy, nw, nh));
        resized.CopyTo(normRoi);

        var keys = allowedKeys ?? _templates.Keys;
        var scores = new List<(string key, double score)>();

        foreach (var key in keys)
        {
            if (!_templates.TryGetValue(key, out var tplList))
            {
                continue;
            }

            var best = 0.0;
            foreach (var tpl in tplList)
            {
                using var inter = new Mat();
                using var uni = new Mat();
                Cv2.BitwiseAnd(norm, tpl, inter);
                Cv2.BitwiseOr(norm, tpl, uni);
                var upx = Cv2.CountNonZero(uni);
                var sc = upx > 0 ? Cv2.CountNonZero(inter) / (double)upx : 0.0;
                if (sc > best)
                {
                    best = sc;
                }
            }

            scores.Add((key, best));
        }

        if (scores.Count == 0)
        {
            return new OcrResult(null, 0.0, "no-templates", new Rect(x1, y1, x2 - x1, y2 - y1));
        }

        var ranked = scores.OrderByDescending(x => x.score).ToList();
        var bestKey = ranked[0].key;
        var bestScore = ranked[0].score;
        var secondScore = ranked.Count > 1 ? ranked[1].score : 0.0;
        var margin = bestScore - secondScore;
        var dbg = $"{debugPrefix} m:{margin:0.00} " + string.Join(' ', ranked.Take(3).Select(x => $"{x.key}:{x.score:0.00}"));

        // Prevent constant false-positive key lock (e.g. always 'W') when candidates are too close.
        if (bestScore < AppConstants.OcrMinScore || margin < AppConstants.OcrMinMargin)
        {
            return new OcrResult(null, bestScore, dbg, new Rect(x1, y1, x2 - x1, y2 - y1));
        }

        return new OcrResult(bestKey, bestScore, dbg, new Rect(x1, y1, x2 - x1, y2 - y1));
    }

    private static Dictionary<string, List<Mat>> DrawLineTemplates(int size)
    {
        var templates = new Dictionary<string, List<Mat>>(StringComparer.OrdinalIgnoreCase);

        static Mat NewCanvas(int sz) => Mat.Zeros(sz, sz, MatType.CV_8UC1).ToMat();

        void AddTemplate(string ch, Mat image)
        {
            if (!templates.TryGetValue(ch, out var list))
            {
                list = [];
                templates[ch] = list;
            }

            list.Add(image);
        }

        var w = NewCanvas(size);
        var ptsW = new[]
        {
            new Point(4, 4),
            new Point(10, 28),
            new Point(16, 12),
            new Point(22, 28),
            new Point(28, 4),
        };
        Cv2.Polylines(w, [ptsW], false, Scalar.White, 3, LineTypes.AntiAlias);
        AddTemplate("w", w);

        var a = NewCanvas(size);
        Cv2.Line(a, new Point(6, 28), new Point(16, 4), Scalar.White, 3, LineTypes.AntiAlias);
        Cv2.Line(a, new Point(16, 4), new Point(26, 28), Scalar.White, 3, LineTypes.AntiAlias);
        Cv2.Line(a, new Point(10, 18), new Point(22, 18), Scalar.White, 3, LineTypes.AntiAlias);
        AddTemplate("a", a);

        var s = NewCanvas(size);
        var ptsS = new[]
        {
            new Point(26, 7),
            new Point(20, 4),
            new Point(10, 5),
            new Point(6, 11),
            new Point(10, 16),
            new Point(20, 19),
            new Point(25, 24),
            new Point(22, 28),
            new Point(10, 28),
            new Point(6, 25),
        };
        Cv2.Polylines(s, [ptsS], false, Scalar.White, 3, LineTypes.AntiAlias);
        AddTemplate("s", s);

        var d = NewCanvas(size);
        Cv2.Line(d, new Point(7, 4), new Point(7, 28), Scalar.White, 3, LineTypes.AntiAlias);
        Cv2.Ellipse(d, new Point(13, 16), new OpenCvSharp.Size(11, 12), 0, -90, 90, Scalar.White, 3, LineTypes.AntiAlias);
        AddTemplate("d", d);

        void AddFontTemplates(string ch)
        {
            var scales = new[] { 0.78, 0.88, 0.98, 1.08 };
            var thicknesses = new[] { 2, 3 };
            var fonts = new[] { HersheyFonts.HersheySimplex, HersheyFonts.HersheyDuplex };

            foreach (var font in fonts)
            {
                foreach (var scale in scales)
                {
                    foreach (var thickness in thicknesses)
                    {
                        var img = NewCanvas(size);
                        var text = ch.ToUpperInvariant();
                        var textSize = Cv2.GetTextSize(text, font, scale, thickness, out _);
                        var x = Math.Max(1, (size - textSize.Width) / 2);
                        var y = Math.Min(size - 2, (size + textSize.Height) / 2 - 2);
                        Cv2.PutText(img, text, new Point(x, y), font, scale, Scalar.White, thickness, LineTypes.AntiAlias);
                        Cv2.Threshold(img, img, 1, 255, ThresholdTypes.Binary);
                        AddTemplate(ch, img);
                    }
                }
            }
        }

        foreach (var key in AppConstants.WasdTemplateKeys)
        {
            AddFontTemplates(key);
        }

        foreach (var key in "0123456789")
        {
            var img = NewCanvas(size);
            const HersheyFonts font = HersheyFonts.HersheySimplex;
            const double scale = 0.95;
            const int thickness = 2;
            var text = key.ToString();
            var textSize = Cv2.GetTextSize(text, font, scale, thickness, out _);
            var x = Math.Max(1, (size - textSize.Width) / 2);
            var y = Math.Min(size - 2, (size + textSize.Height) / 2 - 2);
            Cv2.PutText(img, text, new Point(x, y), font, scale, Scalar.White, thickness, LineTypes.AntiAlias);
            Cv2.Threshold(img, img, 1, 255, ThresholdTypes.Binary);
            AddTemplate(text, img);
        }

        return templates;
    }
}



