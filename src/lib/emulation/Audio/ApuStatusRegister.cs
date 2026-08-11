namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Status register ($4015) read and write handler.
/// </summary>
internal static class ApuStatusRegister
{
    internal static byte Read(
        bool clearFrameIrq,
        ulong cpuClock,
        ApuChannels channels,
        ApuFrameCounter frame,
        InterruptLines interrupts)
    {
        byte result = 0;
        if (channels.Pulse1.Length > 0)
        {
            result |= 0x01;
        }
        if (channels.Pulse2.Length > 0)
        {
            result |= 0x02;
        }
        if (channels.Triangle.Length > 0)
        {
            result |= 0x04;
        }
        if (channels.Noise.Length > 0)
        {
            result |= 0x08;
        }
        if (channels.Dmc.Enabled)
        {
            result |= 0x10;
        }
        if (interrupts.ApuFrameIrq)
        {
            result |= 0x40;
        }
        if (interrupts.ApuDmcIrq)
        {
            result |= 0x80;
        }
        if (clearFrameIrq)
        {
            frame.RequestIrqClear(cpuClock);
        }
        return result;
    }

    internal static void Write(
        byte value,
        ulong cpuClock,
        ApuChannels channels,
        InterruptLines interrupts)
    {
        channels.Pulse1.Enabled = (value & 0x01) != 0;
        channels.Pulse2.Enabled = (value & 0x02) != 0;
        channels.Triangle.Enabled = (value & 0x04) != 0;
        channels.Noise.Enabled = (value & 0x08) != 0;
        channels.Dmc.SetEnabled((value & 0x10) != 0, cpuClock);
        if (!channels.Pulse1.Enabled)
        {
            channels.Pulse1.Length = 0;
        }
        if (!channels.Pulse2.Enabled)
        {
            channels.Pulse2.Length = 0;
        }
        if (!channels.Triangle.Enabled)
        {
            channels.Triangle.Length = 0;
        }
        if (!channels.Noise.Enabled)
        {
            channels.Noise.Length = 0;
        }
        interrupts.ApuDmcIrq = false;
    }
}
