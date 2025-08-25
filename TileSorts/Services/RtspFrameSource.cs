using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace Hikvision.TileSort.Services;

public class RtspFrameSource : IFrameSource
{
    private readonly ILogger<RtspFrameSource> _logger;
    private readonly string _rtsp;
    private readonly int _w, _h;
    private VideoCapture? _cap;

    public RtspFrameSource(
        ILogger<RtspFrameSource> logger,
        IOptionsSnapshot<CameraOptions> opt)
    {
        _logger = logger;
        _rtsp = opt.Value.RtspUrl;
        _w = opt.Value.FrameWidth;
        _h = opt.Value.FrameHeight;
    }

    public bool Open()
    {
        _cap = new VideoCapture(_rtsp);
        if (!_cap.IsOpened())
        {
            _logger.LogError("RTSP açılamadı: {rtsp}", _rtsp);
            return false;
        }
        if (_w > 0) _cap.FrameWidth = _w;
        if (_h > 0) _cap.FrameHeight = _h;
        _logger.LogInformation("RTSP bağlı: {rtsp} ({w}x{h})", _rtsp, _cap.FrameWidth, _cap.FrameHeight);
        return true;
    }

    public bool Read(out Mat frame)
    {
        frame = new Mat();
        if (_cap is null) return false;
        return _cap.Read(frame) && !frame.Empty();
    }

    public void Dispose() => _cap?.Dispose();
}

public class CameraOptions
{
    public string RtspUrl { get; set; } = "";
    public int FrameWidth { get; set; } = 0;
    public int FrameHeight { get; set; } = 0;
}
