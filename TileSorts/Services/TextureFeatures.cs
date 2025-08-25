using OpenCvSharp;

namespace Hikvision.TileSort.Services;

public static class TextureFeatures
{
    public static (double edgeDensity, double dirRatio, double lineDensity) Compute(Mat roiBgr)
    {
        using var gray = new Mat();
        Cv2.CvtColor(roiBgr, gray, ColorConversionCodes.BGR2GRAY);

        // 1) Kenar yoğunluğu (tır­tıklı arka yüzde genellikle daha yüksek)
        using var edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);
        double edgeDensity = Cv2.CountNonZero(edges) / (double)(edges.Rows * edges.Cols);

        // 2) Yönlü gradyan oranı (tek yönde çizgiler -> oran sapar)
        using var gx = new Mat();
        using var gy = new Mat();
        Cv2.Sobel(gray, gx, MatType.CV_32F, 1, 0, ksize: 3);
        Cv2.Sobel(gray, gy, MatType.CV_32F, 0, 1, ksize: 3);
        double meanAbs(Mat m) => Cv2.Mean(Cv2.Abs(m)).Val0;
        var mx = meanAbs(gx);
        var my = meanAbs(gy);
        double dirRatio = (mx > my) ? (mx / (my + 1e-6)) : (my / (mx + 1e-6));

        // 3) Çizgi yoğunluğu (Hough)
        using var linesImg = edges.Clone();
        var lines = Cv2.HoughLinesP(linesImg, 1, Math.PI / 180, threshold: 30, minLineLength: Math.Min(edges.Rows, edges.Cols) * 0.4, maxLineGap: 10);
        double lineDensity = (lines?.Length ?? 0) / (double)(edges.Rows * edges.Cols);

        return (edgeDensity, dirRatio, lineDensity);
    }
}
