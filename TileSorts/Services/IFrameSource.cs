using OpenCvSharp;

namespace Hikvision.TileSort.Services;

public interface IFrameSource : IDisposable
{
    bool Open();
    bool Read(out Mat frame);
}
