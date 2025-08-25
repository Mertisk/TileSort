using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace Hikvision.TileSort.Services;

public class TileSegmenter
{
    private readonly SegmentationOptions _opt;
    public TileSegmenter(IOptionsSnapshot<SegmentationOptions> opt) => _opt = opt.Value;

    public IEnumerable<Rect> FindTileRois(Mat bgr)
    {
        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 0);

        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 50, 150);

        using var morph = edges.Clone();
        if (_opt.DilateIterations > 0) Cv2.Dilate(morph, morph, null, iterations: _opt.DilateIterations);
        if (_opt.ErodeIterations > 0) Cv2.Erode(morph, morph, null, iterations: _opt.ErodeIterations);

        Cv2.FindContours(morph, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var c in contours)
        {
            var peri = Cv2.ArcLength(c, true);
            var approx = Cv2.ApproxPolyDP(c, 0.03 * peri, true);

            if (approx.Length is < 4 or > 6) continue; // kare/romb yakınında

            var rect = Cv2.BoundingRect(approx);
            var area = rect.Width * rect.Height;
            if (area < _opt.MinArea || area > _opt.MaxArea) continue;

            // "kare benzeri" ölçüt: en-boy oranı ve doluluk
            double ratio = (double)rect.Width / rect.Height;
            ratio = ratio < 1 ? 1 / ratio : ratio; // >=1
            if (ratio > 1 + _opt.SquareTolerance) continue;

            // konturun ROI içini ne kadar doldurduğu (yaklaşık)
            var contourArea = Cv2.ContourArea(c);
            var fill = contourArea / area;
            if (fill < 0.4) continue;

            yield return rect;
        }
    }
}

public class SegmentationOptions
{
    public int MinArea { get; set; } = 800;
    public int MaxArea { get; set; } = 20000;
    public double SquareTolerance { get; set; } = 0.35;
    public int DilateIterations { get; set; } = 1;
    public int ErodeIterations { get; set; } = 1;
}
