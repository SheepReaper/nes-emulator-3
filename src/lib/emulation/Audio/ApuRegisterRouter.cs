namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Handles $4000-$4017 register dispatch and status read/write for APU channels.
/// </summary>
internal static class ApuRegisterRouter
{
    internal static void Write(
        ushort address,
        byte value,
        ulong cpuClock,
        ApuChannels channels,
        ApuFrameCounter frame,
        InterruptLines interrupts)
    {
        if (address is >= 0x4000 and <= 0x4003)
        {
            ApuPulseRouter.WritePulse1(address, value, channels.Pulse1);
        }
        else if (address is >= 0x4004 and <= 0x4007)
        {
            ApuPulseRouter.WritePulse2(address, value, channels.Pulse2);
        }
        else if (address is >= 0x4008 and <= 0x4013)
        {
            ApuOtherChannelRouter.Write(address, value, channels);
        }
        else if (address == 0x4015)
        {
            ApuStatusRegister.Write(value, cpuClock, channels, interrupts);
        }
        else if (address == 0x4017)
        {
            frame.WriteRegister(value, cpuClock);
        }
    }

    internal static byte ReadStatus(
        bool clearFrameIrq,
        ulong cpuClock,
        ApuChannels channels,
        ApuFrameCounter frame,
        InterruptLines interrupts)
    {
        return ApuStatusRegister.Read(clearFrameIrq, cpuClock, channels, frame, interrupts);
    }
}
