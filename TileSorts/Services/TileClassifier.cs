using Hikvision.TileSort.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace Hikvision.TileSort.Services;

public class TileClassifier
{
    private readonly ILogger<TileClassifier> _logger;
    private readonly ClassificationOptions _opt;

    // Otomatik kalibrasyon için kayan ortalamalar
    private int _calibCount = 0;
    private double _edgeMeanSmooth = 0, _edgeMeanRibbed = 0;

    public TileClassifier(ILogger<TileClassifier> logger, IOptionsSnapshot<ClassificationOptions> opt)
    {
        _logger = logger;
        _opt = opt.Value;
    }

    public TileDetectionResult Classify(Mat frame, Rect roi)
    {
        using var tile = new Mat(frame, roi);
        var (edgeDensity, dirRatio, lineDensity) = TextureFeatures.Compute(tile);

        // Basit kural tabanlı skor (0-3 arası)
        int votes = 0;
        if (edgeDensity >= _opt.EdgeDensityThreshold) votes++;
        if (dirRatio >= _opt.DirectionalRatioThreshold) votes++;
        if (lineDensity >= _opt.LineDensityThreshold) votes++;

        bool ribbed = votes >= 2; // en az 2 özellik sınırı geçerse tırtıklı
        double score = (edgeDensity / _opt.EdgeDensityThreshold +
                        dirRatio / _opt.DirectionalRatioThreshold +
                        lineDensity / _opt.LineDensityThreshold) / 3.0;

        return new TileDetectionResult(
            Timestamp: DateTime.Now,
            Roi: roi,
            IsRibbed: ribbed,
            Score: score,
            EdgeDensity: edgeDensity,
            DirectionalRatio: dirRatio,
            LineDensity: lineDensity
        );
    }

    public void AutoCalibrate(bool isRibbedGroundTruth, double edgeDensity)
    {
        if (!_opt.UseAutoCalibration || _calibCount >= _opt.AutoCalibSamples) return;

        if (isRibbedGroundTruth)
            _edgeMeanRibbed = UpdateMean(_edgeMeanRibbed, edgeDensity);
        else
            _edgeMeanSmooth = UpdateMean(_edgeMeanSmooth, edgeDensity);

        _calibCount++;

        // Yalın bir örnek: edge yoğunluğu eşiklerini yerinde it
        if (_edgeMeanRibbed > 0 && _edgeMeanSmooth > 0)
        {
            _opt.EdgeDensityThreshold = (_edgeMeanRibbed + _edgeMeanSmooth) / 2 * 0.9; // emniyet payı
        }
    }

    private static double UpdateMean(double current, double value)
        => current == 0 ? value : (current * 0.9 + value * 0.1);
}

public class ClassificationOptions
{
    public double EdgeDensityThreshold { get; set; } = 0.08;
    public double DirectionalRatioThreshold { get; set; } = 1.6;
    public double LineDensityThreshold { get; set; } = 0.002;
    public bool UseAutoCalibration { get; set; } = true;
    public int AutoCalibSamples { get; set; } = 20;
}
