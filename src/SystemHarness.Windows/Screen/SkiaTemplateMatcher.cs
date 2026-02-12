using SkiaSharp;

namespace SystemHarness.Windows;

/// <summary>
/// Template matching using Normalized Cross-Correlation (NCC) over grayscale images.
/// Uses integral images for O(1) region sum queries and non-maximum suppression.
/// </summary>
public sealed class SkiaTemplateMatcher : ITemplateMatcher
{
    private const double Epsilon = 1e-10;
    private const int NmsRadius = 10; // Non-maximum suppression radius in pixels

    public Task<IReadOnlyList<TemplateMatchResult>> FindAsync(
        Screenshot screenshot, string templatePath,
        double threshold = 0.8, CancellationToken ct = default)
    {
        if (!File.Exists(templatePath))
            throw new HarnessException($"Template file not found: '{templatePath}'");

        threshold = Math.Clamp(threshold, 0.0, 1.0);

        return Task.Run(() =>
        {
            var templateBytes = File.ReadAllBytes(templatePath);
            using var templateBitmap = SKBitmap.Decode(templateBytes)
                ?? throw new HarnessException($"Failed to decode template image: '{templatePath}'");
            using var sourceBitmap = SKBitmap.Decode(screenshot.Bytes)
                ?? throw new HarnessException("Failed to decode screenshot image.");

            var sourceGray = ToGrayscale(sourceBitmap);
            var templateGray = ToGrayscale(templateBitmap);

            var sw = sourceBitmap.Width;
            var sh = sourceBitmap.Height;
            var tw = templateBitmap.Width;
            var th = templateBitmap.Height;

            if (tw > sw || th > sh)
                return (IReadOnlyList<TemplateMatchResult>)Array.Empty<TemplateMatchResult>();

            // Precompute template statistics
            var templateSum = 0L;
            var templateSumSq = 0L;
            for (var i = 0; i < templateGray.Length; i++)
            {
                templateSum += templateGray[i];
                templateSumSq += (long)templateGray[i] * templateGray[i];
            }

            var templateCount = (long)(tw * th);
            var templateVariance = templateCount * templateSumSq - templateSum * templateSum;
            if (templateVariance < Epsilon)
                return (IReadOnlyList<TemplateMatchResult>)Array.Empty<TemplateMatchResult>();

            var sqrtTemplateVar = Math.Sqrt(templateVariance);

            // Build integral images for source
            var integral = BuildIntegralImage(sourceGray, sw, sh);
            var integralSq = BuildIntegralImageSq(sourceGray, sw, sh);

            // Sliding window NCC
            var searchW = sw - tw;
            var searchH = sh - th;
            var candidates = new List<(int x, int y, double score)>();

            for (var sy = 0; sy <= searchH; sy++)
            {
                ct.ThrowIfCancellationRequested();
                for (var sx = 0; sx <= searchW; sx++)
                {
                    var regionSum = RegionSum(integral, sw, sx, sy, sx + tw - 1, sy + th - 1);
                    var regionSumSq = RegionSumSq(integralSq, sw, sx, sy, sx + tw - 1, sy + th - 1);

                    var imageVariance = templateCount * regionSumSq - regionSum * regionSum;
                    if (imageVariance < Epsilon)
                        continue;

                    // Compute cross-correlation numerator
                    var crossSum = 0L;
                    for (var ty = 0; ty < th; ty++)
                    {
                        var srcRow = (sy + ty) * sw + sx;
                        var tmplRow = ty * tw;
                        for (var tx = 0; tx < tw; tx++)
                        {
                            crossSum += (long)sourceGray[srcRow + tx] * templateGray[tmplRow + tx];
                        }
                    }

                    var numerator = templateCount * crossSum - regionSum * templateSum;
                    var denominator = Math.Sqrt(imageVariance) * sqrtTemplateVar;
                    var score = numerator / denominator;

                    if (score >= threshold)
                        candidates.Add((sx, sy, score));
                }
            }

            // Non-maximum suppression
            var results = NonMaximumSuppression(candidates, tw, th);
            return (IReadOnlyList<TemplateMatchResult>)results;
        }, ct);
    }

    private static byte[] ToGrayscale(SKBitmap bitmap)
    {
        var w = bitmap.Width;
        var h = bitmap.Height;
        var gray = new byte[w * h];
        var pixels = bitmap.Pixels;

        for (var i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            // ITU-R BT.601 luminance
            gray[i] = (byte)((c.Red * 299 + c.Green * 587 + c.Blue * 114) / 1000);
        }

        return gray;
    }

    private static long[] BuildIntegralImage(byte[] gray, int w, int h)
    {
        var ii = new long[(w + 1) * (h + 1)];
        var stride = w + 1;

        for (var y = 0; y < h; y++)
        {
            var rowSum = 0L;
            for (var x = 0; x < w; x++)
            {
                rowSum += gray[y * w + x];
                ii[(y + 1) * stride + (x + 1)] = rowSum + ii[y * stride + (x + 1)];
            }
        }

        return ii;
    }

    private static long[] BuildIntegralImageSq(byte[] gray, int w, int h)
    {
        var ii = new long[(w + 1) * (h + 1)];
        var stride = w + 1;

        for (var y = 0; y < h; y++)
        {
            var rowSum = 0L;
            for (var x = 0; x < w; x++)
            {
                var v = (long)gray[y * w + x];
                rowSum += v * v;
                ii[(y + 1) * stride + (x + 1)] = rowSum + ii[y * stride + (x + 1)];
            }
        }

        return ii;
    }

    private static long RegionSum(long[] integral, int w, int x1, int y1, int x2, int y2)
    {
        var stride = w + 1;
        return integral[(y2 + 1) * stride + (x2 + 1)]
             - integral[y1 * stride + (x2 + 1)]
             - integral[(y2 + 1) * stride + x1]
             + integral[y1 * stride + x1];
    }

    private static long RegionSumSq(long[] integralSq, int w, int x1, int y1, int x2, int y2)
    {
        var stride = w + 1;
        return integralSq[(y2 + 1) * stride + (x2 + 1)]
             - integralSq[y1 * stride + (x2 + 1)]
             - integralSq[(y2 + 1) * stride + x1]
             + integralSq[y1 * stride + x1];
    }

    private static List<TemplateMatchResult> NonMaximumSuppression(
        List<(int x, int y, double score)> candidates, int tw, int th)
    {
        if (candidates.Count == 0)
            return [];

        // Sort by score descending
        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        var results = new List<TemplateMatchResult>();
        var suppressed = new bool[candidates.Count];

        for (var i = 0; i < candidates.Count; i++)
        {
            if (suppressed[i]) continue;

            var (cx, cy, cs) = candidates[i];
            results.Add(new TemplateMatchResult
            {
                X = cx,
                Y = cy,
                Width = tw,
                Height = th,
                Confidence = cs,
            });

            // Suppress nearby lower-scoring candidates
            for (var j = i + 1; j < candidates.Count; j++)
            {
                if (suppressed[j]) continue;
                var (ox, oy, _) = candidates[j];
                if (Math.Abs(cx - ox) < NmsRadius + tw / 2 &&
                    Math.Abs(cy - oy) < NmsRadius + th / 2)
                {
                    suppressed[j] = true;
                }
            }
        }

        return results;
    }
}
