namespace Sheep.Emulation.Nes.Audio;

/// <summary>
/// Routes writes for the pulse 1 and pulse 2 channels.
/// </summary>
internal static class ApuPulseRouter
{
    internal static void WritePulse1(ushort address, byte value, ApuPulse pulse1)
    {
        switch (address)
        {
            case 0x4000:
                pulse1.WriteControl(value);
                break;
            case 0x4001:
                pulse1.WriteSweep(value);
                break;
            case 0x4002:
                pulse1.WriteTimerLow(value);
                break;
            case 0x4003:
                pulse1.WriteTimerHigh(value);
                break;
        }
    }

    internal static void WritePulse2(ushort address, byte value, ApuPulse pulse2)
    {
        switch (address)
        {
            case 0x4004:
                pulse2.WriteControl(value);
                break;
            case 0x4005:
                pulse2.WriteSweep(value);
                break;
            case 0x4006:
                pulse2.WriteTimerLow(value);
                break;
            case 0x4007:
                pulse2.WriteTimerHigh(value);
                break;
        }
    }
}
