using System.Runtime.InteropServices.WindowsRuntime;

using Microsoft.UI.Xaml.Media.Imaging;

namespace EmuSheep;

internal sealed class MainPageFramePresenter : IDisposable
{
    private readonly WriteableBitmap _frameBitmap;
    private readonly byte[] _rgbaFrame = new byte[NesSystem.FrameBufferSize];
    private readonly byte[] _bgraFrame = new byte[NesSystem.FrameBufferSize];
    private readonly Stream _pixelBufferStream;

    public MainPageFramePresenter(WriteableBitmap frameBitmap)
    {
        _frameBitmap = frameBitmap;
        _pixelBufferStream = _frameBitmap.PixelBuffer.AsStream();
    }

    public void Present(NesEmulationSession session)
    {
        if (!session.TryCopyLatestFrame(_rgbaFrame, out _))
        {
            return;
        }

        RgbaToBgraConverter.Convert(_rgbaFrame, _bgraFrame);
        _pixelBufferStream.Position = 0;
        _pixelBufferStream.Write(_bgraFrame);
        _frameBitmap.Invalidate();
    }

    public void Dispose() => _pixelBufferStream.Dispose();
}
