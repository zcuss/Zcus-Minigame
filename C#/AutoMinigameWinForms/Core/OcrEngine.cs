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
        double minScore,
        double minMargin,
        IReadOnlyCollection<string>? allowedKeys = null)
    {
        var h = frameBgr.Rows;
        var w = frameBgr.Cols;
        var halfW = Math.Max(1, boxW / 2);
        var halfH = Math.Max(1, boxH / 2);
        const int cropPad = 4;
        var cx = center.X + ocrOffsetX;
        var cy = center.Y + ocrOffsetY;

        var x1 = Math.Max(0, cx - halfW - cropPad);
        var x2 = Math.Min(w, cx + halfW + cropPad);
        var y1 = Math.Max(0, cy - halfH - cropPad);
        var y2 = Math.Min(h, cy + halfH + cropPad);

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

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(2, 2));
        Cv2.MorphologyEx(white, white, MorphTypes.Close, kernel, iterations: 1);
        Cv2.Dilate(white, white, kernel, iterations: 1);
        Cv2.MorphologyEx(white, white, MorphTypes.Open, kernel, iterations: 1);

        Cv2.FindContours(white, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (contours.Length == 0)
        {
            return new OcrResult(null, 0.0, "w:0.00 a:0.00", new Rect(x1, y1, x2 - x1, y2 - y1));
        }

        var cropCx = white.Cols / 2.0;
        var cropCy = white.Rows / 2.0;
        var maxArea = Math.Max(20.0, (white.Cols * white.Rows) * 0.72);
        var candidates = contours
            .Select(c =>
            {
                var rect = Cv2.BoundingRect(c);
                var area = Cv2.ContourArea(c);
                var rcx = rect.X + (rect.Width / 2.0);
                var rcy = rect.Y + (rect.Height / 2.0);
                var dist = Math.Sqrt(((rcx - cropCx) * (rcx - cropCx)) + ((rcy - cropCy) * (rcy - cropCy)));
                return (rect, area, rcx, rcy, dist);
            })
            .Where(x => x.area >= 10.0 && x.area <= maxArea)
            .OrderBy(x => x.dist)
            .ThenByDescending(x => x.area)
            .ToList();

        if (candidates.Count == 0)
        {
            return new OcrResult(null, 0.0, "w:0.00 a:0.00", new Rect(x1, y1, x2 - x1, y2 - y1));
        }

        var seed = candidates[0];
        var seedRadius = Math.Max(8.0, Math.Max(seed.rect.Width, seed.rect.Height) * 0.75);
        var mergePad = 3;
        var selected = candidates
            .Where(x =>
            {
                var closeCenter = Math.Sqrt(((x.rcx - seed.rcx) * (x.rcx - seed.rcx)) + ((x.rcy - seed.rcy) * (x.rcy - seed.rcy))) <= seedRadius;
                var closeRect =
                    x.rect.X <= (seed.rect.Right + mergePad) &&
                    (x.rect.Right + mergePad) >= seed.rect.X &&
                    x.rect.Y <= (seed.rect.Bottom + mergePad) &&
                    (x.rect.Bottom + mergePad) >= seed.rect.Y;
                return closeCenter || closeRect;
            })
            .Select(x => x.rect)
            .ToList();

        if (selected.Count == 0)
        {
            selected.Add(seed.rect);
        }

        var bx = int.MaxValue;
        var by = int.MaxValue;
        var ex = int.MinValue;
        var ey = int.MinValue;

        foreach (var rect in selected)
        {
            bx = Math.Min(bx, rect.X);
            by = Math.Min(by, rect.Y);
            ex = Math.Max(ex, rect.Right);
            ey = Math.Max(ey, rect.Bottom);
        }

        const int roiPad = 2;
        bx = Math.Max(0, bx - roiPad);
        by = Math.Max(0, by - roiPad);
        ex = Math.Min(white.Cols, ex + roiPad);
        ey = Math.Min(white.Rows, ey + roiPad);
        var roiRect = new Rect(bx, by, Math.Max(1, ex - bx), Math.Max(1, ey - by));

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
        var normCount = Cv2.CountNonZero(norm);

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
                var interCount = Cv2.CountNonZero(inter);
                var unionCount = Cv2.CountNonZero(uni);
                var tplCount = Cv2.CountNonZero(tpl);
                var iou = unionCount > 0 ? interCount / (double)unionCount : 0.0;
                var diceDen = normCount + tplCount;
                var dice = diceDen > 0 ? (2.0 * interCount) / diceDen : 0.0;
                var sc = (iou * 0.55) + (dice * 0.45);
                if (key.Equals("s", StringComparison.OrdinalIgnoreCase))
                {
                    sc *= 1.04;
                }
                sc = Math.Min(1.0, sc);
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
        var ambiguous = margin < minMargin;
        var dbg = string.Join(' ', ranked.Take(3).Select(x => $"{x.key}:{x.score:0.00}"));

        if (bestScore < minScore)
        {
            return new OcrResult(null, bestScore, dbg, new Rect(x1, y1, x2 - x1, y2 - y1), margin, true);
        }

        return new OcrResult(bestKey, bestScore, dbg, new Rect(x1, y1, x2 - x1, y2 - y1), margin, ambiguous);
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



