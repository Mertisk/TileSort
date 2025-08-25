using OpenCvSharp;
using System;

namespace Hikvision.TileSort.Models;

public record TileDetectionResult(
    DateTime Timestamp,
    Rect Roi,
    bool IsRibbed,            // Tırtıklı?
    double Score,             // Bileşik skor
    double EdgeDensity,
    double DirectionalRatio,
    double LineDensity
);
