using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

using Windows.Media;

using WinRT;

namespace Sheep.WinUI.Interop;

public static class AudioFrameBuffer
{
    public static unsafe void WithSpan<T, TState>(
        AudioFrame frame,
        AudioBufferAccessMode accessMode,
        int elementCount,
        TState state,
        SpanAction<T, TState> action)
        where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elementCount);

        using var buffer = frame.LockBuffer(accessMode);
        using var reference = buffer.CreateReference();
        reference.As<IMemoryBufferByteAccess>().GetBuffer(out var data, out var capacity);

        var requiredBytes = checked((uint)elementCount * (uint)sizeof(T));
        if (requiredBytes > capacity)
            throw new ArgumentException("The requested span exceeds the memory buffer capacity.", nameof(elementCount));

        action(new Span<T>(data, elementCount), state);
    }
}

[GeneratedComInterface]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe partial interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}
