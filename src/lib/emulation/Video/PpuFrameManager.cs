using System;
using System.Collections.Generic;

namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Triple-buffering frame slot manager for PPU output.
/// </summary>
internal sealed class PpuFrameManager
{
    private readonly object _frameLock = new();
    private List<FrameSlot> _frameSlots = [];
    private FrameSlot _renderSlot = new();
    private FrameSlot? _publishedSlot;
    private bool _hasPublishedFrame;

    internal byte[] RenderFrame => _renderSlot.Pixels;
    internal ulong FrameNumber { get; private set; }

    internal void Reset()
    {
        lock (_frameLock)
        {
            _frameSlots = [new(), new(), new()];
            _renderSlot = _frameSlots[0];
            _publishedSlot = null;
            FrameNumber = 0;
            _hasPublishedFrame = false;
        }

        Array.Clear(RenderFrame, 0, RenderFrame.Length);
        for (var i = 3; i < Ppu.FrameBufferSize; i += 4)
        {
            RenderFrame[i] = 0xFF;
        }
    }

    internal ulong PublishFrame()
    {
        lock (_frameLock)
        {
            _publishedSlot = _renderSlot;
            _renderSlot = FindRenderSlot();
            _hasPublishedFrame = true;
            return ++FrameNumber;
        }
    }

    internal bool TryCopyFrame(Span<byte> destination, out ulong frameNumber)
    {
        lock (_frameLock)
        {
            frameNumber = FrameNumber;
            if (!_hasPublishedFrame)
            {
                return false;
            }

            _publishedSlot!.Pixels.AsSpan().CopyTo(destination);
            return true;
        }
    }

    internal NesVideoFrame? TryAcquireFrame(NesVideoStandard videoStandard)
    {
        lock (_frameLock)
        {
            if (!_hasPublishedFrame || _publishedSlot == null)
            {
                return null;
            }

            var slot = _publishedSlot;
            slot.LeaseCount++;
            return new NesVideoFrame(slot.Pixels, FrameNumber, videoStandard, () => ReleaseFrame(slot));
        }
    }

    private FrameSlot FindRenderSlot()
    {
        foreach (var slot in _frameSlots)
        {
            if (!ReferenceEquals(slot, _publishedSlot) && slot.LeaseCount == 0)
            {
                return slot;
            }
        }

        var added = new FrameSlot();
        _frameSlots.Add(added);
        return added;
    }

    private void ReleaseFrame(FrameSlot slot)
    {
        lock (_frameLock)
        {
            slot.LeaseCount--;
        }
    }
}
