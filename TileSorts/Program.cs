using Hikvision.TileSort.Models;
using Hikvision.TileSort.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.Configure<CameraOptions>(builder.Configuration.GetSection("Camera"));
builder.Services.Configure<SegmentationOptions>(builder.Configuration.GetSection("Segmentation"));
builder.Services.Configure<ClassificationOptions>(builder.Configuration.GetSection("Classification"));

builder.Services.AddLogging(l => l.AddConsole().SetMinimumLevel(LogLevel.Information));
builder.Services.AddSingleton<IFrameSource, RtspFrameSource>();
builder.Services.AddSingleton<TileSegmenter>();
builder.Services.AddSingleton<TileClassifier>();

using var host = builder.Build();

var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Main");
var source = host.Services.GetRequiredService<IFrameSource>();
var segmenter = host.Services.GetRequiredService<TileSegmenter>();
var classifier = host.Services.GetRequiredService<TileClassifier>();

if (!source.Open()) return;

log.LogInformation("Başlıyor... Q ile çıkış, S ile ekrana ROI çizimi aç/kapat, 1=DUZ (kalibrasyon), 2=TIRTIKLI (kalibrasyon)");

bool show = true;
var win = new Window("TileSort");
while (true)
{
    if (!source.Read(out var frame)) continue;

    var rois = segmenter.FindTileRois(frame).ToList();

    var results = new List<TileDetectionResult>();
    foreach (var roi in rois)
    {
        var res = classifier.Classify(frame, roi);
        results.Add(res);

        if (show)
        {
            var color = res.IsRibbed ? Scalar.Red : Scalar.LimeGreen;
            Cv2.Rectangle(frame, roi, color, 2);
            Cv2.PutText(frame, res.IsRibbed ? "TIRTIKLI" : "DUZ",
                new OpenCvSharp.Point(roi.X, roi.Y - 5),
                HersheyFonts.HersheySimplex, 0.5, color, 1);
        }
    }

    // Basit istatistik/log
    int ribbedCount = results.Count(r => r.IsRibbed);
    int smoothCount = results.Count - ribbedCount;
    Cv2.PutText(frame, $"RIBBED: {ribbedCount}  SMOOTH: {smoothCount}",
        new OpenCvSharp.Point(10, 25),
        HersheyFonts.HersheySimplex, 0.8, Scalar.White, 2);

    win.ShowImage(frame);
    var key = Cv2.WaitKey(1);
    if (key == 'q' || key == 'Q') break;
    if (key == 's' || key == 'S') show = !show;

    // Sahada hızlı auto-kalibrasyon: operatör bir ROI'yi tıklayıp etiketlemek isterse burayı geliştirip mouse callback ekleyebilirsiniz.
    // Basitçe "1": tüm tespitlerin kenar yoğunluğu -> DUZ, "2": -> TIRTIKLI kabul edilerek eşik adapte edilir.
    if (key == '1' || key == '2')
    {
        bool ribbed = key == '2';
        foreach (var r in results)
            classifier.AutoCalibrate(ribbed, r.EdgeDensity);
    }
}

source.Dispose();
